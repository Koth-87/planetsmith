// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;

namespace Planetsmith.Generation;

/// <summary>
/// Shared per-tile geometry helpers for generation passes. Single-threaded
/// generation only: the neighbour buffer is reused between calls.
/// </summary>
public static class TileGeometry
{
	//// References and State
	
	private static readonly List<PlanetTile> neighborBuffer = new();
	
	//// Public API
	
	/// <summary>
	/// Returns the neighbour lying furthest upwind of the tile (the one whose
	/// direction is most opposed to the wind flow), or -1 if there is none.
	/// </summary>
	public static int UpwindNeighbor(PlanetLayer layer, int tileId, Vector3 center, Vector3 wind)
	{
		// Gather the neighbours
		layer.GetTileNeighbors(tileId, neighborBuffer);
		int tileCount = layer.Tiles.Count;
		
		// Keep the one lying most squarely against the wind
		float bestAlignment = 0f;
		int upwind = -1;
		foreach (PlanetTile neighbor in neighborBuffer)
		{
			int neighborId = neighbor.tileId;
			if (neighborId < 0 || neighborId >= tileCount)
			{
				continue;
			}
			Vector3 direction = (layer.GetTileCenter(neighborId) - center).normalized;
			float alignment = Vector3.Dot(direction, wind);
			if (alignment < bestAlignment)
			{
				bestAlignment = alignment;
				upwind = neighborId;
			}
		}
		return upwind;
	}
}
