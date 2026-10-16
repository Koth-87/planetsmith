// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;

namespace Planetsmith.Generation.Passes;

/// <summary>
/// Derives each tile's seasonal temperature extremes (coldest and warmest of the
/// year) around its annual mean. The swing grows with latitude (poles have long
/// dark winters) and with continentality (the sea moderates coasts, so interiors
/// bake in summer and freeze in winter). These extremes feed the biome scoring
/// gates; vanilla biome workers only see the annual mean, so this is where
/// Planetsmith adds the "a warm average with a lethal winter isn't tropical" rule.
/// </summary>
public sealed class SeasonalityPass : IGenerationPass
{
	//// Constants
	
	// Half of the summer-to-winter swing (deg C) at the equator and at the poles.
	private const float EQUATOR_HALF_SWING = 3f;
	private const float POLE_HALF_SWING = 22f;
	
	// Extra swing in deep interiors relative to the coast.
	private const float CONTINENTALITY_BOOST = 0.6f;
	
	// Residual swing on a world with no tilt, from its elliptical orbit alone.
	private const float MINIMUM_TILT_FACTOR = 0.12f;
	
	//// Public API
	
	public string Name => "Seasonality";
	
	public void Run(GenerationContext context)
	{
		// Scale the swing by the planet's tilt
		// Seasons exist because the planet is tilted, so the whole swing scales with it:
		// an upright world has near-constant temperatures year round.
		PlanetLayer layer = context.Layer;
		List<Tile> tiles = layer.Tiles;
		float tiltScale = Mathf.Max(MINIMUM_TILT_FACTOR, context.TiltFactor) * context.Tuning.seasonIntensity;
		
		// Swing each tile's mean either way
		for (int tileId = 0; tileId < tiles.Count; tileId++)
		{
			float latitudeFraction = Mathf.Clamp01(Mathf.Abs(layer.LongLatOf(tileId).y) / 90f);
			float baseSwing = Mathf.Lerp(EQUATOR_HALF_SWING, POLE_HALF_SWING, latitudeFraction);
			float swing = baseSwing * tiltScale * (1f + CONTINENTALITY_BOOST * context.Continentality[tileId]);
			float mean = tiles[tileId].temperature;
			context.WinterMinimumTemperature[tileId] = mean - swing;
			context.SummerMaximumTemperature[tileId] = mean + swing;
		}
	}
}
