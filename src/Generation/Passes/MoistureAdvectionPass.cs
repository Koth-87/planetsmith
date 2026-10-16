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
/// latitude bands (ClimatePass) and orographic effects (OrographyPass).
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
	
	// Bounds on the player's reach dial, and on the retention it produces.
	private const float MINIMUM_REACH = 0.01f;
	private const float MINIMUM_RETENTION = 0.5f;
	private const float MAXIMUM_RETENTION = 0.995f;
	
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
			float factor = Mathf.Lerp(DRIEST_RAINFALL_FACTOR, 1f, Mathf.Clamp01(moisture[tileId]));
			tiles[tileId].rainfall = Mathf.Max(0f, tiles[tileId].rainfall * factor);
		}
	}
}
