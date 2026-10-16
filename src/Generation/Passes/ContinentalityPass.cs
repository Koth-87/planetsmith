// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;

namespace Planetsmith.Generation.Passes;

/// <summary>
/// Continentality as distance from the sea: a multi-source flood fill outward from
/// every ocean tile gives each land tile its graph distance to the nearest coast,
/// which maps to 0 (maritime) at the shore up to 1 (fully continental) deep inland.
/// This is isotropic on purpose. Maritime influence comes from being next to water,
/// not from the wind, so every coast fades smoothly regardless of wind direction.
/// (The one-directional downwind model stays where it belongs, in rainfall.) The
/// result feeds the seasonality swing.
/// </summary>
public sealed class ContinentalityPass : IGenerationPass
{
	//// Constants
	
	// Tiles inland at which a location counts as fully continental.
	private const int FULLY_CONTINENTAL_DISTANCE = 20;
	private const float DISTANCE_SCALE = FULLY_CONTINENTAL_DISTANCE;
	
	// Distance of a tile the fill has not reached yet.
	private const int UNREACHED = -1;
	
	//// References and State
	
	private static readonly List<PlanetTile> neighbors = new();
	
	//// Public API
	
	public string Name => "Continentality";
	
	public void Run(GenerationContext context)
	{
		// Start the fill from every ocean tile
		PlanetLayer layer = context.Layer;
		List<Tile> tiles = layer.Tiles;
		int count = tiles.Count;
		int[] distance = new int[count];
		Queue<int> frontier = new();
		for (int tileId = 0; tileId < count; tileId++)
		{
			distance[tileId] = UNREACHED;
			if (tiles[tileId].elevation <= 0f)
			{
				distance[tileId] = 0;
				frontier.Enqueue(tileId);
			}
		}
		
		// Spread inland one ring at a time
		while (frontier.Count > 0)
		{
			int current = frontier.Dequeue();
			layer.GetTileNeighbors(current, neighbors);
			foreach (PlanetTile neighbor in neighbors)
			{
				int neighborId = neighbor.tileId;
				if (neighborId < 0 || neighborId >= count || distance[neighborId] != UNREACHED)
				{
					continue;
				}
				distance[neighborId] = distance[current] + 1;
				frontier.Enqueue(neighborId);
			}
		}
		
		// Turn each distance into continentality
		for (int tileId = 0; tileId < count; tileId++)
		{
			if (tiles[tileId].elevation <= 0f)
			{
				context.Continentality[tileId] = 0f;
				continue;
			}
			
			// A landmass the fill never reached has no sea path at all, so it counts as
			// fully continental.
			int coastDistance = distance[tileId] < 0 ? FULLY_CONTINENTAL_DISTANCE : distance[tileId];
			context.CoastDistance[tileId] = coastDistance;
			context.Continentality[tileId] = Mathf.Clamp01(coastDistance / DISTANCE_SCALE);
		}
	}
}
