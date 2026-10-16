// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;

namespace Planetsmith.Generation.Passes;

/// <summary>
/// Monsoon rains. Land gains and loses heat far faster than the sea, so a big
/// landmass in the tropics ends up much hotter than the water beside it each summer.
/// The air above it rises, ocean air floods in to replace it, and it arrives carrying
/// the sea with it: months of torrential rain, then a dry season when the flow
/// reverses in winter.
///
/// Three things have to line up for that to happen, and all three are already known
/// by the time this runs: the tile has to be in the tropics or subtropics, its
/// climate has to swing hard between summer and winter (which is the land-sea heat
/// contrast driving the whole thing), and the sea has to be close enough for the
/// inflowing air to still be wet. Where all three hold, a great deal of rain is
/// added; where any one fails, nothing happens.
///
/// Runs after seasonality, which supplies the swing, and before aridity, which has to
/// see the final rainfall.
/// </summary>
public sealed class MonsoonPass : IGenerationPass
{
	//// Constants
	
	// Monsoons belong to the tropics and subtropics, strongest around this latitude.
	private const float PEAK_LATITUDE = 18f;
	private const float LATITUDE_SPREAD = 13f;
	
	// Latitude factor below which a tile lies too far from the monsoon belt to bother.
	private const float MINIMUM_LATITUDE_FACTOR = 0.01f;
	
	// Half the summer-to-winter swing (deg C) at which land-sea contrast is at full
	// strength. Calibrated to what the tropics actually reach, not to temperate
	// extremes: a monsoon is driven by how much hotter the land gets than the sea in
	// summer, and the belt this happens in has a modest annual range to begin with.
	// Judging it against temperate swings leaves every monsoon at half strength.
	private const float FULL_CONTRAST_SWING = 7f;
	
	// Distance inland (tiles) beyond which the inflowing air has given up its water.
	private const float MOISTURE_REACH = 18f;
	
	// Rain (mm/year) delivered by a monsoon at full strength.
	private const float MONSOON_RAINFALL = 1400f;
	
	//// Public API
	
	public string Name => "Monsoon";
	
	public void Run(GenerationContext context)
	{
		PlanetLayer layer = context.Layer;
		List<Tile> tiles = layer.Tiles;
		for (int tileId = 0; tileId < tiles.Count; tileId++)
		{
			// Skip the sea and anywhere outside the monsoon belt
			Tile tile = tiles[tileId];
			if (tile.elevation <= 0f)
			{
				continue;
			}
			float latitude = Mathf.Abs(layer.LongLatOf(tileId).y);
			float latitudeFactor = ClimateMath.Gaussian(latitude, PEAK_LATITUDE, LATITUDE_SPREAD);
			if (latitudeFactor <= MINIMUM_LATITUDE_FACTOR)
			{
				continue;
			}
			
			// Weigh the land-sea contrast and the reach of the sea air
			float swing = (context.SummerMaximumTemperature[tileId] - context.WinterMinimumTemperature[tileId]) * 0.5f;
			float contrast = Mathf.Clamp01(swing / FULL_CONTRAST_SWING);
			float moistureAccess = Mathf.Clamp01(1f - context.CoastDistance[tileId] / MOISTURE_REACH);
			float strength = latitudeFactor * contrast * moistureAccess;
			if (strength <= 0f)
			{
				continue;
			}
			
			// Bring the rains
			context.MonsoonStrength[tileId] = strength;
			tile.rainfall += MONSOON_RAINFALL * strength * context.Tuning.monsoonStrength;
		}
	}
}
