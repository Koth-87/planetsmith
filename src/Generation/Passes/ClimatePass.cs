// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Planetsmith.Compatibility;

namespace Planetsmith.Generation.Passes;

/// <summary>
/// Assigns each tile an annual mean temperature and rainfall from a compact
/// latitude + elevation model. This is deliberately a first approximation:
/// dedicated wind, orographic, and monsoon passes will refine it later.
/// </summary>
public sealed class ClimatePass : IGenerationPass
{
	//// Constants
	
	// Environmental lapse: temperature falls with elevation above a threshold, at
	// roughly 6 deg C per kilometre, capped so peaks don't run away.
	private const float LAPSE_START_ALTITUDE = 250f;
	private const float LAPSE_RATE_PER_METRE = 0.006f;
	private const float MAXIMUM_LAPSE_REDUCTION = 45f;
	
	// Latitude of, and spread around, each rainfall band (degrees).
	private const float BAND_SPREAD = 17f;
	private const float SUBTROPICAL_DRY_LATITUDE = 25f;
	private const float MID_LATITUDE_WET_LATITUDE = 50f;
	
	// The dry belt is narrower than the wet belts either side of it. Air sinks back to
	// the ground over a tighter run of latitudes than the two it rose over, so giving all
	// three the same width spread the drought far past where it belongs: it was still
	// taking half the rain at 35 degrees and a third at 40, latitudes that should be
	// Mediterranean and temperate. That left the most habitable band of the planet as
	// desert, with the deserts themselves no drier for it.
	private const float SUBTROPICAL_SPREAD = 10f;
	
	// Rain that reaches everywhere, before the belts add their share.
	private const float BASE_RAINFALL = 450f;
	
	// Peak rain each wet belt adds, in mm a year.
	private const float EQUATORIAL_RAINFALL = 2600f;
	private const float MID_LATITUDE_RAINFALL = 1000f;
	
	// How much of the rain the subtropical high holds back at its worst.
	private const float SUBTROPICAL_SUPPRESSION = 0.72f;
	
	// Crude high-elevation drying, a stand-in until orographic rain shadows land: rain
	// thins from this height upwards, down to a share of itself over the span above it.
	private const float HIGH_DRYING_START = 1000f;
	private const float HIGH_DRYING_SPAN = 3000f;
	private const float HIGH_DRYING_FLOOR = 0.5f;
	
	// Strength of the coherent noise that dapples the otherwise-smooth bands.
	private const float TEMPERATURE_NOISE_AMPLITUDE = 4f; // deg C
	private const float RAINFALL_NOISE_AMPLITUDE = 0.35f; // fraction of local rainfall
	
	/// <summary>
	/// How much of the equator-to-pole warmth survives at a given fraction of the way
	/// to the pole. Sunlight thins out faster than a plain cosine suggests once past
	/// the tropics, and this follows the profile RimWorld itself uses, which is what
	/// the biome workers were balanced against. A gentler falloff leaves the middle
	/// latitudes several degrees too warm and pushes hot, dry biomes polewards.
	/// </summary>
	private static readonly SimpleCurve LATITUDE_WARMTH_CURVE = new()
	{
		new CurvePoint(0f, 1f),
		new CurvePoint(0.1f, 0.985f),
		new CurvePoint(0.5f, 0.657f),
		new CurvePoint(1f, 0f),
	};
	
	//// Private Functions
	
	private static float Temperature(GenerationContext context, float latitude, float elevation)
	{
		// Warm the latitude between pole and equator
		// The warmth is 1 at the equator and 0 at the poles.
		float warmth = LATITUDE_WARMTH_CURVE.Evaluate(Mathf.Clamp01(Mathf.Abs(latitude) / 90f));
		float baseTemperature = Mathf.Lerp(context.PoleMeanTemperature, context.EquatorMeanTemperature, warmth);
		
		// Take another mod's answer for what the tilt does
		// Where another mod works out what the tilt does to the sunlight a latitude
		// receives, take its answer here rather than ours. Folding it in this early
		// matters: everything after this reads the temperature we write, so the seasons,
		// the frost gates and the biomes all end up describing the same planet the player
		// will actually be living on.
		if (context.IsTiltHandledExternally)
		{
			baseTemperature += ModCompatibility.AnnualTemperatureCorrection(latitude);
		}
		
		// Cool the air with height
		float lapse = 0f;
		if (elevation > LAPSE_START_ALTITUDE)
		{
			lapse = Mathf.Min(MAXIMUM_LAPSE_REDUCTION, (elevation - LAPSE_START_ALTITUDE) * LAPSE_RATE_PER_METRE * context.Tuning.elevationCooling);
		}
		return baseTemperature - lapse + context.Tuning.temperatureOffset;
	}
	
	private static float Rainfall(GenerationContext context, float latitude, float elevation)
	{
		// Add the two wet belts every planet has
		// Rain rising off the equator, and the storm track of the middle latitudes.
		float absoluteLatitude = Mathf.Abs(latitude);
		float equatorial = EQUATORIAL_RAINFALL * ClimateMath.Gaussian(absoluteLatitude, 0f, BAND_SPREAD);
		float midLatitude = MID_LATITUDE_RAINFALL * ClimateMath.Gaussian(absoluteLatitude, MID_LATITUDE_WET_LATITUDE, BAND_SPREAD);
		float rain = BASE_RAINFALL + equatorial + midLatitude;
		
		// Hold the rain down under the subtropical high
		// Between the belts sits the subtropical high, where descending air suppresses
		// rain. It has to hold rainfall down rather than subtract from it: taking a fixed
		// amount away drove the whole belt to nothing at all, and nothing is a floor that
		// mountains, coasts and every tuning dial multiply straight back into nothing. A
		// desert should be dry, not empty.
		rain *= 1f - SUBTROPICAL_SUPPRESSION * ClimateMath.Gaussian(absoluteLatitude, SUBTROPICAL_DRY_LATITUDE, SUBTROPICAL_SPREAD);
		
		// Scale by the world's rainfall and the player's tuning
		rain *= context.RainfallMultiplier * context.Tuning.rainfallScale;
		
		// Dry out the high ground
		if (elevation > HIGH_DRYING_START)
		{
			rain *= Mathf.Lerp(1f, HIGH_DRYING_FLOOR, Mathf.Clamp01((elevation - HIGH_DRYING_START) / HIGH_DRYING_SPAN));
		}
		return rain;
	}
	
	//// Public API
	
	public string Name => "Climate";
	
	public void Run(GenerationContext context)
	{
		PlanetLayer layer = context.Layer;
		List<Tile> tiles = layer.Tiles;
		double vanillaTotal = 0d;
		int vanillaLand = 0;
		for (int tileId = 0; tileId < tiles.Count; tileId++)
		{
			// Measure vanilla's rainfall before replacing it
			// This is the only moment vanilla's own rainfall still exists, and it is the
			// yardstick for whether the rivers the game builds afterwards have as much
			// water to work with as they used to.
			Tile tile = tiles[tileId];
			if (tile.elevation > 0f)
			{
				vanillaTotal += tile.rainfall;
				vanillaLand++;
			}
			
			// Work out the climate from latitude and height, dappled by the noise
			float latitude = layer.LongLatOf(tileId).y;
			Vector3 center = layer.GetTileCenter(tileId);
			float temperatureOffset = context.TemperatureNoise.GetValue(center) * TEMPERATURE_NOISE_AMPLITUDE;
			float rainfallFactor = 1f + Mathf.Clamp(context.RainfallNoise.GetValue(center), -1f, 1f) * RAINFALL_NOISE_AMPLITUDE;
			tile.temperature = Temperature(context, latitude, tile.elevation) + temperatureOffset;
			tile.rainfall = Mathf.Max(0f, Rainfall(context, latitude, tile.elevation) * rainfallFactor);
		}
		
		// Keep vanilla's mean for the river report
		// Summed in double to keep a whole planet's rainfall exact; a float is plenty for
		// the mean.
		context.VanillaRainfallMean = vanillaLand > 0 ? (float)(vanillaTotal / vanillaLand) : 0f;
	}
}
