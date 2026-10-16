// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Planetsmith.Generation;

/// <summary>
/// Learns a biome's climate niche by probing its own worker, so Planetsmith can
/// reason about biomes it has no hardcoded knowledge of, including modded ones.
/// Currently derives the cold tolerance: the lowest temperature at which the
/// biome scores positively for any rainfall. Frost-intolerant biomes (tropical
/// and friends) have a high cold tolerance, which the biome pass gates against
/// the winter minimum rather than the annual mean.
///
/// Results are cached per biome for the session; workers are deterministic in the
/// temperature/rainfall inputs we vary, so one probe is enough.
/// </summary>
public static class BiomeProfiler
{
	//// Constants
	
	/// <summary>Sentinel for biomes that never score positive in the probe (e.g. water biomes on a land tile).</summary>
	public const float NO_COLD_TOLERANCE = -999f;
	
	/// <summary>Returned for biomes with no upper limit on rainfall.</summary>
	public const float NO_WET_LIMIT = float.MaxValue;
	
	/// <summary>Workers reject ground they cannot use with a large negative score.</summary>
	private const float REJECTED_SCORE = -50f;
	
	// Height of the dry land every probe offers, in metres.
	private const float PROBE_ELEVATION = 100f;
	
	// The climates the niche probes sweep, in degrees and millimetres of rain a year.
	private const float PROBE_COLDEST = -60f;
	private const float PROBE_HOTTEST = 50f;
	private const float PROBE_TEMPERATURE_STEP = 5f;
	private const float PROBE_WETTEST = 6000f;
	private const float PROBE_RAINFALL_STEP = 500f;
	
	// The water probe sweeps more coarsely, since a water biome refuses all of it.
	private const float WATER_PROBE_TEMPERATURE_STEP = 10f;
	private const float WATER_PROBE_WETTEST = 4000f;
	private const float WATER_PROBE_RAINFALL_STEP = 1000f;
	
	//// References and State
	
	private static readonly Dictionary<BiomeDef, float> coldToleranceCache = new();
	private static readonly Dictionary<BiomeDef, float> wetLimitCache = new();
	private static readonly Dictionary<BiomeDef, bool> waterBiomeCache = new();
	
	//// Private Functions
	
	/// <summary>
	/// A biome's worker, or null where it has none to give. Asking a def with no worker
	/// class for its worker does not hand back nothing, it looks the missing class up
	/// in a dictionary and throws on the null key. Biomes like that do exist: templates
	/// and placeholders other mods never meant to generate. Selection never meets them
	/// because it checks whether a biome generates at all first, so anything that
	/// probes the whole database has to be the careful one.
	/// </summary>
	private static BiomeWorker? WorkerOrNull(BiomeDef biome)
	{
		// Sanity check
		if (biome.workerClass == null)
		{
			return null;
		}
		
		// Ask for the worker
		try
		{
			return biome.Worker;
		}
		catch
		{
			return null;
		}
	}
	
	/// <summary>
	/// A real tile reference to probe with. Some workers, including those other mods
	/// configure through XML, build their context from the tile's id and would throw
	/// on an invalid one, which would cost us the profile and litter the log.
	/// </summary>
	private static PlanetTile ProbeTile()
	{
		// Reaching for the grid throws outright when there is no world at all, rather
		// than handing back nothing, and no world is exactly the situation the biome list
		// is built in. Whether one happens to be lying around depends on which other mods
		// are installed, so this cannot be assumed either way.
		try
		{
			PlanetLayer? surface = Find.WorldGrid?.Surface;
			return surface != null && surface.Tiles.Count > 0 ? new PlanetTile(0, surface) : PlanetTile.Invalid;
		}
		catch
		{
			return PlanetTile.Invalid;
		}
	}
	
	private static bool ProbeIsWaterBiome(BiomeDef biome)
	{
		// Sanity check
		BiomeWorker? worker = WorkerOrNull(biome);
		if (worker == null)
		{
			return false;
		}
		
		// Offer dry land in every climate and see whether anything is refused outright
		// A sea or lake biome turns all of it down flat, and crucially does so on its very
		// first check, before consulting the wider world. That matters because this runs
		// from the world creation page, where no world exists yet and any worker reaching
		// for one would throw.
		SurfaceTile probe = new() { elevation = PROBE_ELEVATION };
		PlanetTile probeTile = ProbeTile();
		for (float temperature = PROBE_COLDEST; temperature <= PROBE_HOTTEST; temperature += WATER_PROBE_TEMPERATURE_STEP)
		{
			for (float rainfall = 0f; rainfall <= WATER_PROBE_WETTEST; rainfall += WATER_PROBE_RAINFALL_STEP)
			{
				probe.temperature = temperature;
				probe.rainfall = rainfall;
				try
				{
					if (worker.GetScore(biome, probe, probeTile) > REJECTED_SCORE)
					{
						return false; // it will consider dry land, so it is a land biome
					}
				}
				catch
				{
					// Reaching for a world we do not have marks it as caring about land.
					// Treat it as land and leave it configurable.
					return false;
				}
			}
		}
		return true;
	}
	
	private static float ProbeWetLimit(BiomeDef biome)
	{
		// Sanity check
		BiomeWorker? worker = WorkerOrNull(biome);
		if (worker == null)
		{
			return NO_WET_LIMIT;
		}
		
		// Find the wettest rainfall any temperature still scores positively at
		Tile probe = new() { elevation = PROBE_ELEVATION };
		PlanetTile probeTile = ProbeTile();
		float wettest = -1f;
		for (float rainfall = 0f; rainfall <= PROBE_WETTEST; rainfall += PROBE_RAINFALL_STEP)
		{
			for (float temperature = PROBE_COLDEST; temperature <= PROBE_HOTTEST; temperature += PROBE_TEMPERATURE_STEP)
			{
				probe.temperature = temperature;
				probe.rainfall = rainfall;
				float score;
				try
				{
					score = worker.GetScore(biome, probe, probeTile);
				}
				catch
				{
					return NO_WET_LIMIT;
				}
				if (score > 0f)
				{
					wettest = rainfall;
					break;
				}
			}
		}
		return wettest < 0f ? NO_WET_LIMIT : wettest;
	}
	
	private static float ProbeColdTolerance(BiomeDef biome)
	{
		// Sanity check
		BiomeWorker? worker = WorkerOrNull(biome);
		if (worker == null)
		{
			return NO_COLD_TOLERANCE;
		}
		
		// Find the coldest temperature any rainfall scores positively at
		// A bare land tile: WaterCovered is false, and we sweep temperature and rainfall
		// directly, from cold to warm, returning the first temperature that scores
		// positively for some rainfall.
		Tile probe = new() { elevation = PROBE_ELEVATION };
		PlanetTile probeTile = ProbeTile();
		for (float temperature = PROBE_COLDEST; temperature <= PROBE_HOTTEST; temperature += PROBE_TEMPERATURE_STEP)
		{
			for (float rainfall = 0f; rainfall <= PROBE_WETTEST; rainfall += PROBE_RAINFALL_STEP)
			{
				probe.temperature = temperature;
				probe.rainfall = rainfall;
				float score;
				try
				{
					score = worker.GetScore(biome, probe, probeTile);
				}
				catch
				{
					// Worker relies on real tile geometry we can't synthesise; give up on
					// profiling it and let it use its unmodified vanilla score.
					return NO_COLD_TOLERANCE;
				}
				if (score > 0f)
				{
					return temperature;
				}
			}
		}
		return NO_COLD_TOLERANCE;
	}
	
	//// Public API
	
	/// <summary>
	/// Whether a biome belongs to open water rather than dry land. Worked out by
	/// offering the worker a submerged tile: sea and lake biomes will take it, while
	/// land biomes reject it outright. Asking the worker keeps this true for biomes
	/// added by other mods, which a list of names never could.
	/// </summary>
	public static bool IsWaterBiome(BiomeDef biome)
	{
		// Reuse an earlier probe
		if (waterBiomeCache.TryGetValue(biome, out bool isCachedWater))
		{
			return isCachedWater;
		}
		
		// Probe the worker and remember the answer
		bool isWaterBiome = ProbeIsWaterBiome(biome);
		waterBiomeCache[biome] = isWaterBiome;
		return isWaterBiome;
	}
	
	/// <summary>The lowest temperature at which a biome scores positively for any rainfall.</summary>
	public static float ColdTolerance(BiomeDef biome)
	{
		// Reuse an earlier probe
		if (coldToleranceCache.TryGetValue(biome, out float cached))
		{
			return cached;
		}
		
		// Probe the worker and remember the answer
		float coldTolerance = ProbeColdTolerance(biome);
		coldToleranceCache[biome] = coldTolerance;
		return coldTolerance;
	}
	
	/// <summary>
	/// The wettest conditions a biome will still accept, in mm of rainfall. A low
	/// value marks a biome that only belongs in dry country, which is what lets the
	/// aridity gate tell a desert from a forest without naming either.
	/// </summary>
	public static float WetLimit(BiomeDef biome)
	{
		// Reuse an earlier probe
		if (wetLimitCache.TryGetValue(biome, out float cached))
		{
			return cached;
		}
		
		// Probe the worker and remember the answer
		float wetLimit = ProbeWetLimit(biome);
		wetLimitCache[biome] = wetLimit;
		return wetLimit;
	}
}
