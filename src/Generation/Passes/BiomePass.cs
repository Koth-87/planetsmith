// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Planetsmith.Compatibility;

namespace Planetsmith.Generation.Passes;

/// <summary>
/// Re-selects every tile's biome against the freshly computed climate. It runs
/// each biome's own worker in a competitive best-score selection (the shape
/// vanilla uses), driven by Planetsmith's temperature and rainfall, and then layers
/// Planetsmith's own adjustments on top of that base score. The first adjustment is
/// a frost gate: a frost-intolerant biome (learned from its worker via
/// <see cref="BiomeProfiler"/>) is penalised where the winter minimum falls below
/// its cold tolerance, so a warm annual mean with a lethal winter no longer reads
/// as tropical. Water biomes fall out naturally because their workers key off
/// elevation.
/// </summary>
public sealed class BiomePass : IGenerationPass
{
	//// Types
	
	/// <summary>
	/// Another mod's per-biome adjustment, already looked up so the tile loop can just
	/// do the arithmetic. Selection asks every biome about every tile, which on a large
	/// planet is millions of questions, and a dictionary lookup inside that is a cost
	/// worth paying once per biome instead.
	/// </summary>
	private readonly struct ExternalTuning
	{
		public readonly float Offset;
		public readonly float Multiplier;
		public readonly bool HasEffect;
		
		public ExternalTuning(float offset, float multiplier)
		{
			Offset = offset;
			Multiplier = multiplier;
			HasEffect = offset != 0f || multiplier != 1f;
		}
	}
	
	//// Constants
	
	// A biome that cannot score below this temperature is treated as frost-intolerant.
	private const float FROST_SENSITIVE_THRESHOLD = 5f;
	
	// Score lost per degree the winter minimum sits below a biome's cold tolerance.
	private const float FROST_PENALTY_PER_DEGREE = 2.5f;
	
	// A biome that only accepts rainfall up to this is treated as belonging to dry
	// country. Set above the semi-arid scrub band (which tolerates up to 2000mm), or
	// scrub escapes the gate and takes over every warm region, since its score rises
	// with temperature faster than forest's does.
	private const float DRY_BIOME_RAINFALL_LIMIT = 1600f;
	
	// Effective moisture above which land is no longer meaningfully water limited.
	private const float HUMID_ARIDITY_INDEX = 0.65f;
	
	// Score lost per unit of effective moisture beyond that, for dry-country biomes.
	private const float ARIDITY_PENALTY_PER_UNIT = 25f;
	
	// A biome that accepts more rain than dry country but still stops short, up to this
	// on the probe's steps, is treated as sub-humid: savanna and grassland. Those give way
	// only on the wettest band they accept, from their probed limit up, which is where
	// forest takes over. Odyssey's grasslands, which stop at 2500mm and read as 2000 on
	// the probe, otherwise took most of the equator, soaked ground included. Treating
	// them as dry country overshot, and so did a penalty from an effective moisture of
	// 1.0: both took 1500 to 2000mm tropics from them that rainforest cannot use, and
	// temperate forest got it instead. Rainfall rather than moisture also keeps the
	// heat from shielding the hottest soaked ground.
	private const float SUBHUMID_BIOME_RAINFALL_LIMIT = 2000f;
	
	// Score a sub-humid biome loses on the wettest band it accepts. Covers the lead
	// grassland's steep warmth bonus gives it over rainforest even on the hottest ground.
	private const float SUBHUMID_WET_EDGE_PENALTY = 15f;
	
	// Worldbuilder stores a biome's frequency in tenths, so ten is a biome left alone.
	private const float WORLDBUILDER_FREQUENCY_SCALE = 10f;
	
	// Score Worldbuilder gives a biome whose frequency is zero and has no offset.
	private const float WORLDBUILDER_BANISHED_SCORE = -999f;
	
	// Mixed into each biome's hash so its failure is reported once.
	private const int WORKER_ERROR_KEY_SALT = 0x570B10;
	
	//// Private Functions
	
	private static ExternalTuning[]? BuildExternalTuning(List<BiomeDef> biomes)
	{
		// Sanity check
		if (!ModCompatibility.TryGetWorldbuilderBiomeTuning(out Dictionary<string, int>? offsets, out Dictionary<string, int>? commonalities))
		{
			return null;
		}
		
		// Look up each biome's adjustment once
		ExternalTuning[] tuning = new ExternalTuning[biomes.Count];
		int adjusted = 0;
		for (int index = 0; index < biomes.Count; index++)
		{
			// Skip what selection itself skips, in the same order. This walks the whole
			// biome database rather than the candidates for one tile, so it meets defs
			// selection never does.
			BiomeDef biome = biomes[index];
			if (!biome.implemented || !biome.generatesNaturally)
			{
				continue;
			}
			
			// Sea, lake and their modded cousins are left out of Planetsmith's own list
			// deliberately: they have no dry-land alternative to fall back on, so turning
			// one off could only produce a planet that fails to draw. The other mod's list
			// does include them, so its settings for them are ignored here rather than
			// carried through a door we closed on purpose.
			if (BiomeProfiler.IsWaterBiome(biome))
			{
				tuning[index] = new ExternalTuning(0f, 1f);
				continue;
			}
			
			// Read its offset and frequency
			// Read with a default rather than through their own accessor, which fills the
			// entry in as a side effect; their settings are theirs to write.
			float offset = 0f;
			if (offsets != null && offsets.TryGetValue(biome.defName, out int storedOffset))
			{
				offset = storedOffset;
			}
			float multiplier = 1f;
			if (commonalities != null && commonalities.TryGetValue(biome.defName, out int storedFrequency))
			{
				multiplier = storedFrequency / WORLDBUILDER_FREQUENCY_SCALE;
			}
			tuning[index] = new ExternalTuning(offset, multiplier);
			if (tuning[index].HasEffect)
			{
				adjusted++;
			}
		}
		
		// Hand it over only when something is adjusted
		if (adjusted == 0)
		{
			return null;
		}
		
		// Say so
		// Worth saying out loud. A handover that failed to bind would look exactly like
		// one that worked, and the symptom would be a menu that silently does nothing,
		// which is the very thing this is here to stop.
		Log.Message($"[Planetsmith] Carrying Worldbuilder's own settings for {adjusted} biome(s) into biome selection.");
		return tuning;
	}
	
	/// <summary>
	/// Applies Worldbuilder's adjustment exactly as it would have been applied to
	/// vanilla's own selection, including its convention that a frequency of zero
	/// banishes the biome outright unless an offset was set to argue otherwise.
	/// </summary>
	private static float ApplyExternalTuning(float score, ExternalTuning tuning)
	{
		score += tuning.Offset;
		if (tuning.Multiplier == 0f)
		{
			return tuning.Offset != 0f ? tuning.Offset : WORLDBUILDER_BANISHED_SCORE;
		}
		return score >= 0f ? score * tuning.Multiplier : score / tuning.Multiplier;
	}
	
	/// <summary>The best-scoring biome for a tile, or null when none can be placed there.</summary>
	private static BiomeDef? SelectBiome(List<BiomeDef> biomes, ExternalTuning[]? external, Tile tile, PlanetLayer layer, float winterMinimum, float aridityIndex, PlanetsmithWorldSettings world, out float bestScore)
	{
		BiomeDef? best = null;
		bestScore = 0f;
		for (int index = 0; index < biomes.Count; index++)
		{
			// Skip biomes that cannot go here
			BiomeDef biome = biomes[index];
			if (!biome.implemented || !biome.generatesNaturally)
			{
				continue;
			}
			BiomeSettings? tuning = world.ForBiomeOrNull(biome.defName);
			if (tuning != null && !tuning.isEnabled)
			{
				continue; // the player has banished this biome from the planet
			}
			BiomeWorker worker = biome.Worker;
			if (worker == null || !worker.CanPlaceOnLayer(biome, layer))
			{
				continue;
			}
			
			// Score it, and keep it if it beats the rest
			try
			{
				float workerScore = worker.GetScore(biome, tile, tile.tile);
				float score = workerScore;
				// Straight after the worker, which is where the mod that owns these
				// settings puts them, so the two agree about what a score means before
				// anything else touches it.
				if (external != null && external[index].HasEffect)
				{
					score = ApplyExternalTuning(score, external[index]);
				}
				score = ApplyFrostGate(biome, score, winterMinimum);
				score = ApplyAridityGate(biome, score, aridityIndex, tile.rainfall);
				score = ApplyPlayerTuning(score, tuning, workerScore > 0f || !world.shouldLimitOffsetsToSuitableGround);
				if (best == null || score > bestScore)
				{
					best = biome;
					bestScore = score;
				}
			}
			catch (Exception exception)
			{
				Log.ErrorOnce($"[Planetsmith] Biome worker '{biome.defName}' failed during selection: {exception.Message}", biome.shortHash ^ WORKER_ERROR_KEY_SALT);
			}
		}
		return best;
	}
	
	/// <summary>Holds frost-intolerant biomes back from ground whose winters fall below what they can take.</summary>
	private static float ApplyFrostGate(BiomeDef biome, float score, float winterMinimum)
	{
		// Sanity check
		if (score <= 0f)
		{
			return score;
		}
		
		// Let through biomes the winter cannot hurt
		float coldTolerance = BiomeProfiler.ColdTolerance(biome);
		if (coldTolerance <= FROST_SENSITIVE_THRESHOLD)
		{
			return score; // biome tolerates cold; the winter minimum is irrelevant
		}
		if (winterMinimum >= coldTolerance)
		{
			return score; // winters stay above what the biome needs
		}
		
		// Penalise each degree of frost below its tolerance
		return score - FROST_PENALTY_PER_DEGREE * (coldTolerance - winterMinimum);
	}
	
	/// <summary>
	/// Holds dry-country biomes back from ground that is not really water starved.
	/// Vanilla decides deserts on rainfall alone, so a chilly place with little rain
	/// looks like a desert to it, when in truth almost nothing evaporates there and
	/// the ground stays damp. Comparing effective moisture instead keeps deserts in
	/// country that is genuinely parched. Sub-humid biomes such as grassland are only
	/// held back from the wettest band of rain they accept.
	/// </summary>
	private static float ApplyAridityGate(BiomeDef biome, float score, float aridityIndex, float rainfall)
	{
		// Sanity check
		if (score <= 0f)
		{
			return score;
		}
		
		// Let through dry ground, and biomes that do not need it
		if (aridityIndex <= HUMID_ARIDITY_INDEX)
		{
			return score; // genuinely dry ground; the biome is welcome to it
		}
		float wetLimit = BiomeProfiler.WetLimit(biome);
		if (wetLimit > SUBHUMID_BIOME_RAINFALL_LIMIT)
		{
			return score; // takes any amount of rain
		}
		
		// Hold sub-humid biomes back from the wet end of their range
		if (wetLimit > DRY_BIOME_RAINFALL_LIMIT)
		{
			return rainfall >= wetLimit ? score - SUBHUMID_WET_EDGE_PENALTY : score;
		}
		
		// Penalise dry-country biomes for how far past humid the ground is
		return score - ARIDITY_PENALTY_PER_UNIT * (aridityIndex - HUMID_ARIDITY_INDEX);
	}
	
	/// <summary>
	/// Applies the player's own adjustments last, on top of everything the climate
	/// decided. The offset shifts a biome across the line in places it would narrowly
	/// win or lose; the commonality multiplier then stretches or shrinks the ground it
	/// takes, and is applied only to a winning score so that scaling a biome up cannot
	/// drag a negative score further down.
	///
	/// When the player switches the limit on, a raised offset is held back on ground the
	/// biome's own worker refused. A worker refuses with a score of zero or less, and
	/// lifting that over the line puts a biome where it could never grow: raised
	/// Scarlands spread far past the ruins their worker keeps them to, and a raised
	/// desert takes the rainforests. A lowered offset always applies.
	/// </summary>
	private static float ApplyPlayerTuning(float score, BiomeSettings? tuning, bool canRaiseScore)
	{
		// Sanity check
		if (tuning == null)
		{
			return score;
		}
		
		// Shift, then scale a winning score
		if (tuning.scoreOffset < 0f || canRaiseScore)
		{
			score += tuning.scoreOffset;
		}
		if (score > 0f)
		{
			score *= tuning.commonality;
		}
		return score;
	}
	
	//// Public API
	
	public string Name => "Biome";
	
	public void Run(GenerationContext context)
	{
		// Gather what selection needs
		PlanetLayer layer = context.Layer;
		List<BiomeDef> biomes = DefDatabase<BiomeDef>.AllDefsListForReading;
		PlanetsmithWorldSettings world = PlanetsmithWorldParameters.Active;
		bool hasPlayerChosen = world.HasAdjustedBiomes;
		ExternalTuning[]? external = BuildExternalTuning(biomes);
		List<Tile> tiles = layer.Tiles;
		
		for (int tileId = 0; tileId < tiles.Count; tileId++)
		{
			// Find the best biome for the tile
			Tile tile = tiles[tileId];
			BiomeDef? best = SelectBiome(biomes, external, tile, layer, context.WinterMinimumTemperature[tileId], context.AridityIndex[tileId], world, out float bestScore);
			if (best == null)
			{
				continue;
			}
			
			// Give water a water biome
			// Anything under water must end up with a water biome, and that has to be
			// settled before the rule below. Open sea scores zero rather than positive, so
			// leaving it to that rule would let a tile the rising sea has just drowned keep
			// whatever it was when it was still dry land. A forest sitting on the sea floor
			// is bad enough on its own, but it also hides the coastline from the game,
			// which finds river mouths by looking for ocean, and so quietly costs the world
			// its rivers.
			if (tile.WaterCovered)
			{
				if (BiomeProfiler.IsWaterBiome(best))
				{
					tile.PrimaryBiome = best;
				}
				continue;
			}
			
			// Give land the biome that suits it
			// On dry land, a positive score means some biome genuinely suits the ground and
			// takes it. Otherwise every candidate was merely tolerating the tile, and
			// vanilla's own choice is left standing as the better of two poor fits.
			//
			// Unless the player has said what they want. Their settings only ever reach our
			// own selection, so handing the tile back to vanilla hands it to a choice made
			// before they touched anything, and made against a climate we have since
			// overwritten. A biome turned all the way down would sit there unmoved on
			// exactly the ground its owner was trying to clear it from, which reads as the
			// setting being broken, and fairly so.
			if (bestScore > 0f || hasPlayerChosen)
			{
				tile.PrimaryBiome = best;
			}
		}
	}
}
