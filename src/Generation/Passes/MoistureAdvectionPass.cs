// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;

namespace Planetsmith.Generation.Passes;

/// <summary>
/// Carries moisture inland from the oceans along the prevailing wind. Ocean tiles
/// are saturated sources; over land the carried moisture decays with each tile of
/// fetch, so continental interiors and tiles far downwind of the sea dry out. The
/// resulting moisture field scales rainfall, adding continentality on top of the
/// latitude bands (ClimatePass) and orographic effects (OrographyPass). Warm, wet
/// land hands much of its rain back to the air, so its interiors dry out far less.
///
/// Each land tile draws moisture from the whole arc the wind arrives through (see
/// <see cref="UpwindGraph"/>). Implemented as a wavefront relaxation: oceans stay
/// saturated, and one tile of fetch resolves per step. Purely geometric and
/// deterministic.
/// </summary>
public sealed class MoistureAdvectionPass : IGenerationPass
{
	//// Constants
	
	// Fraction of moisture retained per land tile of fetch.
	private const float LAND_RETENTION = 0.94f;
	
	// Furthest inland fetch resolved, in tiles.
	private const int PROPAGATION_STEPS = 64;
	
	// Rainfall multiplier where no ocean moisture reaches (fully dry interior).
	private const float DRIEST_RAINFALL_FACTOR = 0.45f;
	
	// Over warm, wet land much of the rain is the land's own water evaporated again and
	// carried on, so the air does not run dry crossing it the way it does over a cool or
	// dry interior. Where both hold in full, the driest interior keeps this share of its
	// rain instead. Without it, almost every equatorial interior sat at the dry floor,
	// short of the 2000mm the game's rainforests need, and the hot middle of each
	// continent went to temperate forest.
	private const float RECYCLED_RAINFALL_FACTOR = 0.75f;
	
	// Warmth, in degrees, over which the land starts and finishes handing water back.
	private const float RECYCLING_START_TEMPERATURE = 18f;
	private const float RECYCLING_FULL_TEMPERATURE = 24f;
	
	// Rainfall, in mm a year, over which the ground holds enough water to give back.
	private const float RECYCLING_START_RAINFALL = 1200f;
	private const float RECYCLING_FULL_RAINFALL = 2200f;
	
	// Bounds on the player's reach dial, and on the retention it produces.
	private const float MINIMUM_REACH = 0.01f;
	private const float MINIMUM_RETENTION = 0.5f;
	private const float MAXIMUM_RETENTION = 0.995f;
	
	//// Private Functions
	
	/// <summary>How much of its rain a land tile hands back to the air, from 0 to 1.</summary>
	private static float RecyclingShare(Tile tile)
	{
		float warmth = Mathf.InverseLerp(RECYCLING_START_TEMPERATURE, RECYCLING_FULL_TEMPERATURE, tile.temperature);
		float wetness = Mathf.InverseLerp(RECYCLING_START_RAINFALL, RECYCLING_FULL_RAINFALL, tile.rainfall);
		return warmth * wetness;
	}
	
	//// Public API
	
	public string Name => "MoistureAdvection";
	
	public void Run(GenerationContext context)
	{
		// Start with saturated air everywhere
		List<Tile> tiles = context.Layer.Tiles;
		int count = tiles.Count;
		UpwindGraph graph = context.Upwind;
		bool[] isLand = new bool[count];
		float[] moisture = new float[count];
		float[] next = new float[count];
		for (int tileId = 0; tileId < count; tileId++)
		{
			isLand[tileId] = tiles[tileId].elevation > 0f;
			moisture[tileId] = 1f;
		}
		
		// Carry the moisture inland, one tile of fetch per step
		float retention = Mathf.Clamp(1f - (1f - LAND_RETENTION) / Mathf.Max(MINIMUM_REACH, context.Tuning.moistureReach), MINIMUM_RETENTION, MAXIMUM_RETENTION);
		for (int step = 0; step < PROPAGATION_STEPS; step++)
		{
			for (int tileId = 0; tileId < count; tileId++)
			{
				if (!isLand[tileId])
				{
					next[tileId] = 1f; // ocean: saturated source
					continue;
				}
				UpwindGraph.Source[]? sources = graph.Sources[tileId];
				if (sources == null)
				{
					next[tileId] = moisture[tileId]; // no upwind arc: leave unchanged
					continue;
				}
				float carried = 0f;
				foreach (UpwindGraph.Source source in sources)
				{
					carried += source.Weight * moisture[source.TileId];
				}
				next[tileId] = carried * retention;
			}
			
			(moisture, next) = (next, moisture);
		}
		
		// Dry the rainfall wherever the air has run out
		for (int tileId = 0; tileId < count; tileId++)
		{
			// Share the settled field: it doubles as an onshore/offshore wind measure for
			// the coastal passes, and it is far smoother than tracing a single upwind path
			// across a hex grid.
			context.OceanicMoisture[tileId] = Mathf.Clamp01(moisture[tileId]);
			if (!isLand[tileId])
			{
				continue;
			}
			
			// Read the recycling from the rain the latitude and the mountains give the tile,
			// before any of it is taken away for distance from the sea.
			Tile tile = tiles[tileId];
			float driest = Mathf.Lerp(DRIEST_RAINFALL_FACTOR, RECYCLED_RAINFALL_FACTOR, RecyclingShare(tile));
			float factor = Mathf.Lerp(driest, 1f, Mathf.Clamp01(moisture[tileId]));
			tile.rainfall = Mathf.Max(0f, tile.rainfall * factor);
		}
	}
}
