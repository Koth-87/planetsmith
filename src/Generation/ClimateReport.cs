// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Planetsmith.Generation;

/// <summary>
/// Reports the finished rainfall as a band-by-band summary over land.
///
/// The latitude model can be worked out on paper, but what a planet ends up with
/// cannot: moisture running out inland, mountains, monsoons and coastal currents
/// all multiply into it afterwards, and only land is ever looked at by a player.
/// A belt that reads as reasonable in the formula can still arrive as desert
/// everywhere it matters, which is exactly the complaint this is here to settle.
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
	
	//// Private Functions
	
	private static string BandName(int band)
	{
		return $"{band * BAND_DEGREES}-{(band + 1) * BAND_DEGREES}";
	}
	
	//// Public API
	
	/// <summary>Logs the rainfall over land band by band, and what it leaves the rivers.</summary>
	public static void LogSummary(GenerationContext context)
	{
		// Tally the land in each band
		PlanetLayer layer = context.Layer;
		List<Tile> tiles = layer.Tiles;
		int[] land = new int[BAND_COUNT];
		int[] arid = new int[BAND_COUNT];
		int[] soaked = new int[BAND_COUNT];
		double[] total = new double[BAND_COUNT];
		for (int tileId = 0; tileId < tiles.Count; tileId++)
		{
			Tile tile = tiles[tileId];
			if (tile.elevation <= 0f)
			{
				continue; // the sea has rainfall of its own, and nobody lives in it
			}
			
			// Truncating the latitude is what picks the band it falls in.
			int band = Mathf.Clamp((int)(Mathf.Abs(layer.LongLatOf(tileId).y) / BAND_DEGREES), 0, BAND_COUNT - 1);
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
			bands.Add($"{BandName(band)} {mean:F0}mm ({aridShare:P0} arid, {soakedShare:P0} soaked)");
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
}
