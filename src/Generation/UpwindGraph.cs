// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;

namespace Planetsmith.Generation;

/// <summary>
/// For every land tile, the neighbours the wind arrives from and how much of the
/// arriving air each one accounts for. Gathering from the whole upwind arc rather
/// than a single neighbour lets anything carried on the wind travel as a broad
/// front instead of a thin streamline, while still stretching downwind.
///
/// Built once per world and shared, because working out the arcs means walking
/// every tile's neighbours and is the expensive half of any pass that uses it.
/// Ocean tiles are left empty on purpose: every pass that carries something on the
/// wind treats the sea as a source rather than something to gather from.
/// </summary>
public sealed class UpwindGraph
{
	//// Types
	
	/// <summary>One neighbour the wind arrives from, and its share of the arriving air.</summary>
	public readonly struct Source
	{
		public readonly int TileId;
		public readonly float Weight;
		
		public Source(int tileId, float weight)
		{
			TileId = tileId;
			Weight = weight;
		}
	}
	
	//// References and State
	
	/// <summary>
	/// Upwind neighbours per tile, their shares summing to 1, or null where there are
	/// none.
	/// </summary>
	public readonly Source[]?[] Sources;
	
	//// Private Functions
	
	private UpwindGraph(Source[]?[] sources)
	{
		Sources = sources;
	}
	
	//// Public API
	
	public static UpwindGraph Build(PlanetLayer layer)
	{
		// Prepare the buffers
		List<Tile> tiles = layer.Tiles;
		int count = tiles.Count;
		Source[]?[] sources = new Source[]?[count];
		List<PlanetTile> neighbors = new();
		List<Source> upwind = new(6);
		
		for (int tileId = 0; tileId < count; tileId++)
		{
			// Skip the sea, and anywhere the wind does not blow
			if (tiles[tileId].elevation <= 0f)
			{
				continue;
			}
			float latitude = layer.LongLatOf(tileId).y;
			Vector3 center = layer.GetTileCenter(tileId);
			Vector3 wind = WindModel.PrevailingWind(center, latitude);
			if (wind == Vector3.zero)
			{
				continue;
			}
			
			// Weigh each neighbour by how squarely it lies upwind
			layer.GetTileNeighbors(tileId, neighbors);
			upwind.Clear();
			float total = 0f;
			foreach (PlanetTile neighbor in neighbors)
			{
				int neighborId = neighbor.tileId;
				if (neighborId < 0 || neighborId >= count)
				{
					continue;
				}
				Vector3 direction = (layer.GetTileCenter(neighborId) - center).normalized;
				// Positive when the neighbour lies upwind of this tile.
				float upwindness = -Vector3.Dot(direction, wind);
				if (upwindness > 0f)
				{
					upwind.Add(new Source(neighborId, upwindness));
					total += upwindness;
				}
			}
			if (total <= 0f)
			{
				continue;
			}
			
			// Turn the weights into shares of the arriving air
			Source[] tileSources = new Source[upwind.Count];
			float inverseTotal = 1f / total;
			for (int index = 0; index < tileSources.Length; index++)
			{
				tileSources[index] = new Source(upwind[index].TileId, upwind[index].Weight * inverseTotal);
			}
			sources[tileId] = tileSources;
		}
		
		return new UpwindGraph(sources);
	}
}
