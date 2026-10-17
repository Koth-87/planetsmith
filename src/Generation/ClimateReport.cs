// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Planetsmith.Generation;

/// <summary>
/// Reports the finished rainfall and biomes as a band-by-band summary over land.
///
/// The latitude model can be worked out on paper, but what a planet ends up with
/// cannot: moisture running out inland, mountains, monsoons and coastal currents
/// all multiply into it afterwards, and only land is ever looked at by a player.
/// A belt that reads as reasonable in the formula can still arrive as desert
/// everywhere it matters, which is exactly the complaint this is here to settle.
/// The biomes are reported too because they are what players actually see and
/// describe, and each biome's own worker decides what a given rainfall becomes.
/// </summary>
public static class ClimateReport
{
	//// Constants
	
	// Width of each reported band, in degrees of latitude.
	private const int BAND_DEGREES = 10;
	private const int BAND_COUNT = 90 / BAND_DEGREES;
	
	// Rainfall below which ground is dry enough that only arid biomes will take it, and
	// above which only the wettest will. Neither is a threshold the generator uses: they
	// are there so a band's spread can be read, since a mean alone cannot tell a belt
	// that is uniformly damp from one running between desert and rainforest, and the
	// difference is the whole of what "no variation" means.
	private const float ARID_RAINFALL = 400f;
	private const float SOAKED_RAINFALL = 2000f;
	
	// Swampiness from which the game's swamp and bog biomes will take a tile at all.
	private const float WATERLOGGED_SWAMPINESS = 0.5f;
	
	// How many of a band's most common biomes are named; the rest are summed together.
	private const int REPORTED_BIOMES_PER_BAND = 4;
	
	//// Private Functions
	
	private static string BandName(int band)
	{
		return $"{band * BAND_DEGREES}-{(band + 1) * BAND_DEGREES}";
	}
	
	private static int BandOf(PlanetLayer layer, int tileId)
	{
		// Truncating the latitude is what picks the band it falls in.
		return Mathf.Clamp((int)(Mathf.Abs(layer.LongLatOf(tileId).y) / BAND_DEGREES), 0, BAND_COUNT - 1);
	}
	
	/// <summary>Orders biomes from the most tiles to the fewest.</summary>
	private static int CompareByTileCount(KeyValuePair<BiomeDef, int> first, KeyValuePair<BiomeDef, int> second)
	{
		return second.Value.CompareTo(first.Value);
	}
	
	/// <summary>A band's most common biomes with their shares of its land, the rest summed together.</summary>
	private static string DescribeBiomes(Dictionary<BiomeDef, int> counts, int land)
	{
		// Rank the biomes
		List<KeyValuePair<BiomeDef, int>> ranked = new(counts);
		ranked.Sort(CompareByTileCount);
		
		// Name the most common, then sum the rest
		// The shares are of whole tile counts, so one side becomes a float to keep the
		// division from rounding down to nothing.
		List<string> parts = new(REPORTED_BIOMES_PER_BAND + 1);
		int named = 0;
		for (int index = 0; index < ranked.Count && index < REPORTED_BIOMES_PER_BAND; index++)
		{
			parts.Add($"{ranked[index].Key.defName} {(float)ranked[index].Value / land:P0}");
			named += ranked[index].Value;
		}
		if (named < land)
		{
			parts.Add($"others {(float)(land - named) / land:P0}");
		}
		return string.Join(", ", parts);
	}
	
	//// Public API
	
	/// <summary>Logs the rainfall and waterlogged ground over land band by band, and what it leaves the rivers.</summary>
	public static void LogSummary(GenerationContext context)
	{
		// Tally the land in each band
		PlanetLayer layer = context.Layer;
		List<Tile> tiles = layer.Tiles;
		int[] land = new int[BAND_COUNT];
		int[] arid = new int[BAND_COUNT];
		int[] soaked = new int[BAND_COUNT];
		int[] waterlogged = new int[BAND_COUNT];
		double[] total = new double[BAND_COUNT];
		for (int tileId = 0; tileId < tiles.Count; tileId++)
		{
			Tile tile = tiles[tileId];
			if (tile.elevation <= 0f)
			{
				continue; // the sea has rainfall of its own, and nobody lives in it
			}
			
			int band = BandOf(layer, tileId);
			land[band]++;
			total[band] += tile.rainfall;
			if (tile.rainfall < ARID_RAINFALL)
			{
				arid[band]++;
			}
			else if (tile.rainfall >= SOAKED_RAINFALL)
			{
				soaked[band]++;
			}
			if (tile.swampiness >= WATERLOGGED_SWAMPINESS)
			{
				waterlogged[band]++;
			}
		}
		
		// Describe each band
		List<string> bands = new(BAND_COUNT);
		for (int band = 0; band < BAND_COUNT; band++)
		{
			if (land[band] == 0)
			{
				bands.Add($"{BandName(band)} no land");
				continue;
			}
			
			// The shares are of whole tile counts, so one side becomes a float to keep the
			// division from rounding down to nothing.
			double mean = total[band] / land[band];
			float aridShare = (float)arid[band] / land[band];
			float soakedShare = (float)soaked[band] / land[band];
			float waterloggedShare = (float)waterlogged[band] / land[band];
			bands.Add($"{BandName(band)} {mean:F0}mm ({aridShare:P0} arid, {soakedShare:P0} soaked, {waterloggedShare:P0} waterlogged)");
		}
		Log.Message($"[Planetsmith] Rainfall over land by latitude: {string.Join(", ", bands)}.");
		
		// Compare the water the rivers get with what vanilla gave them
		// Rivers are sized by the rainfall running through them, so a planet handed less
		// water grows smaller rivers however high the density is set.
		double ours = 0d;
		int landTotal = 0;
		for (int band = 0; band < BAND_COUNT; band++)
		{
			ours += total[band];
			landTotal += land[band];
		}
		if (landTotal > 0 && context.VanillaRainfallMean > 0f)
		{
			// Summed in double to keep a whole planet's rainfall exact; a float is plenty
			// for the mean that gets printed.
			float mean = (float)(ours / landTotal);
			Log.Message($"[Planetsmith] Rainfall feeding the rivers: {mean:F0}mm against vanilla's {context.VanillaRainfallMean:F0}mm ({mean / context.VanillaRainfallMean:P0}).");
		}
	}
	
	/// <summary>Logs the biomes the land in each band ended up with, most common first.</summary>
	public static void LogBiomes(GenerationContext context)
	{
		// Count each band's biomes
		PlanetLayer layer = context.Layer;
		List<Tile> tiles = layer.Tiles;
		int[] land = new int[BAND_COUNT];
		Dictionary<BiomeDef, int>[] counts = new Dictionary<BiomeDef, int>[BAND_COUNT];
		for (int band = 0; band < BAND_COUNT; band++)
		{
			counts[band] = new Dictionary<BiomeDef, int>();
		}
		for (int tileId = 0; tileId < tiles.Count; tileId++)
		{
			// Skip the sea, as the rainfall report does
			Tile tile = tiles[tileId];
			BiomeDef? biome = tile.PrimaryBiome;
			if (tile.elevation <= 0f || biome == null)
			{
				continue;
			}
			
			int band = BandOf(layer, tileId);
			land[band]++;
			counts[band].TryGetValue(biome, out int count);
			counts[band][biome] = count + 1;
		}
		
		// Describe each band
		List<string> bands = new(BAND_COUNT);
		for (int band = 0; band < BAND_COUNT; band++)
		{
			string description = land[band] == 0 ? "no land" : DescribeBiomes(counts[band], land[band]);
			bands.Add($"{BandName(band)} {description}");
		}
		Log.Message($"[Planetsmith] Biomes over land by latitude: {string.Join("; ", bands)}.");
	}
}
