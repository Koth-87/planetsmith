// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;

namespace Planetsmith.Generation.Passes;

/// <summary>
/// Effective moisture, which is what plants actually experience. Rainfall alone is
/// misleading: 400mm falling on baking desert and 400mm falling on cold tundra
/// support utterly different life, because warmth decides how much of that water
/// evaporates straight back out. This computes the ratio of rainfall to how much the
/// climate could evaporate given the chance, so a cold, dry place reads as damp and a
/// hot place with respectable rainfall can still read as arid.
///
/// Evaporative demand follows Holdridge's estimate from biotemperature (mean
/// temperature counted only between freezing and 30C, since plants neither transpire
/// below freezing nor benefit further past that). The resulting index uses the usual
/// bands: below 0.2 is arid, 0.2 to 0.5 semi-arid, 0.65 and up humid.
///
/// Must run after everything that alters temperature or rainfall.
/// </summary>
public sealed class AridityPass : IGenerationPass
{
	//// Constants
	
	// Millimetres of potential evaporation per year, per degree of biotemperature.
	private const float EVAPORATION_PER_DEGREE = 58.93f;
	
	// Floor on evaporative demand so frozen ground does not divide by nothing.
	private const float MINIMUM_EVAPORATION = 60f;
	
	// Ceiling on the index; past this a tile is simply very wet.
	private const float MAXIMUM_ARIDITY_INDEX = 3f;
	
	// Warmth past which plants gain nothing further, in degrees.
	private const float MAXIMUM_BIOTEMPERATURE = 30f;
	
	//// Public API
	
	public string Name => "Aridity";
	
	public void Run(GenerationContext context)
	{
		List<Tile> tiles = context.Layer.Tiles;
		for (int tileId = 0; tileId < tiles.Count; tileId++)
		{
			// Leave open water unlimited
			Tile tile = tiles[tileId];
			if (tile.elevation <= 0f)
			{
				context.AridityIndex[tileId] = MAXIMUM_ARIDITY_INDEX;
				continue;
			}
			
			// Weigh the rain against what the warmth could evaporate
			float biotemperature = Mathf.Clamp(tile.temperature, 0f, MAXIMUM_BIOTEMPERATURE);
			float evaporation = Mathf.Max(MINIMUM_EVAPORATION, EVAPORATION_PER_DEGREE * biotemperature);
			context.AridityIndex[tileId] = Mathf.Clamp(tile.rainfall / evaporation, 0f, MAXIMUM_ARIDITY_INDEX);
		}
	}
}
