// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System;
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Planetsmith.Generation.Passes;

/// <summary>
/// Raises or lowers sea level until the planet has the requested fraction of land.
/// Rather than inventing new terrain, it finds the elevation that already sits at
/// the target percentile and shifts every tile so that height becomes the new
/// shoreline: flooding low ground when the target is small, exposing continental
/// shelf when it is large. Runs first, because every later pass keys off elevation
/// and the land/ocean split.
///
/// Off by default so vanilla (and mods that shape the coastline themselves) keep
/// control; enabling it makes Planetsmith the authority on the land fraction.
/// </summary>
public sealed class SeaLevelPass : IGenerationPass
{
	//// Constants
	
	// Depth given to tiles that the new sea level submerges but that sat above the
	// old shoreline, so freshly flooded ground reads as shallow sea rather than 0m.
	private const float MINIMUM_FLOOD_DEPTH = 20f;
	
	// Lowest the land may sit once it is stretched back over its old heights, and the
	// narrowest span either side of the shoreline is allowed, so neither collapses.
	private const float MINIMUM_LAND_HEIGHT = 1f;
	private const float MINIMUM_SPAN = 1f;
	
	//// Public API
	
	public string Name => "SeaLevel";
	
	public void Run(GenerationContext context)
	{
		// Sanity check
		PlanetsmithWorldSettings settings = PlanetsmithWorldParameters.Active;
		if (!settings.isSeaLevelControlEnabled)
		{
			return;
		}
		List<Tile> tiles = context.Layer.Tiles;
		int count = tiles.Count;
		if (count == 0)
		{
			return;
		}
		
		// Find the height at the requested percentile
		// The tiles above the (1 - targetLand) percentile become the new land.
		float targetLand = Mathf.Clamp(settings.targetLandFraction, PlanetsmithSettings.MINIMUM_LAND_FRACTION, PlanetsmithSettings.MAXIMUM_LAND_FRACTION);
		float[] sorted = new float[count];
		for (int tileId = 0; tileId < count; tileId++)
		{
			sorted[tileId] = tiles[tileId].elevation;
		}
		Array.Sort(sorted);
		int index = Mathf.Clamp(Mathf.RoundToInt((1f - targetLand) * count), 0, count - 1);
		float newShoreline = sorted[index];
		if (Mathf.Approximately(newShoreline, 0f))
		{
			return; // already at the requested land fraction
		}
		
		// Stretch each side of the new shoreline back over the range it held
		// Moving the shoreline by sliding every height up or down the same amount would
		// drag the sea floor thousands of metres below anything the game otherwise
		// produces, and leave the peaks correspondingly wrong. Instead each side is
		// stretched back over the range it originally occupied: the land that survives
		// spans the old heights, the ground now drowned spans the old depths, and the
		// waterline sits at nothing in between.
		float deepest = Mathf.Min(sorted[0], -MINIMUM_FLOOD_DEPTH);
		float tallest = Mathf.Max(sorted[count - 1], MINIMUM_LAND_HEIGHT);
		float aboveShore = Mathf.Max(MINIMUM_SPAN, tallest - newShoreline);
		float belowShore = Mathf.Max(MINIMUM_SPAN, newShoreline - deepest);
		foreach (Tile tile in tiles)
		{
			float elevation = tile.elevation;
			if (elevation > newShoreline)
			{
				tile.elevation = Mathf.Max(MINIMUM_LAND_HEIGHT, (elevation - newShoreline) / aboveShore * tallest);
				continue;
			}
			
			// Underwater, and flattened: hilliness carried over from dry land would
			// otherwise show through as relief on the sea bed.
			float depth = deepest * (1f - (elevation - deepest) / belowShore);
			tile.elevation = Mathf.Min(depth, -MINIMUM_FLOOD_DEPTH);
			tile.hilliness = Hilliness.Flat;
		}
		
		// Report the new waterline
		Log.Message($"[Planetsmith] Sea level set for {targetLand:P0} land (waterline was at {newShoreline:F0}m).");
	}
}
