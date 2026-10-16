// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System;
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;

namespace Planetsmith.Generation.Passes;

/// <summary>
/// Traces where the rain goes once it lands. Water runs downhill, so each patch of
/// ground sheds its rainfall onto the lowest neighbour it touches, and that neighbour
/// passes on everything it received along with its own. Work down the slopes from the
/// peaks and the totals build: ridges carry almost nothing, valley floors gather
/// whole watersheds, and the places rivers would run stand out on their own.
///
/// Tiles ringed entirely by higher ground keep what reaches them, which is the right
/// answer rather than an oversight: that is how a basin with no outlet to the sea
/// works, and it is where salt flats and inland lakes belong.
///
/// The totals span such a range that they are stored on a logarithmic scale, so a
/// mere damp hollow can still be told apart from a major river's floodplain.
/// </summary>
public sealed class DrainagePass : IGenerationPass
{
	//// Constants
	
	// Accumulated flow (in mm of gathered rainfall) treated as a full watercourse.
	private const float FULL_FLOW = 400000f;
	
	// Downhill neighbour of a tile every neighbour stands above.
	private const int NO_OUTLET = -1;
	
	//// References and State
	
	private static readonly List<PlanetTile> neighbors = new();
	
	//// Private Functions
	
	/// <summary>
	/// The neighbour water would run to, or -1 when every neighbour stands higher.
	/// Ocean counts, so coastal tiles drain to the sea rather than pooling.
	/// </summary>
	private static int LowestNeighbor(PlanetLayer layer, List<Tile> tiles, int tileId, int count)
	{
		layer.GetTileNeighbors(tileId, neighbors);
		float lowest = tiles[tileId].elevation;
		int result = NO_OUTLET;
		foreach (PlanetTile neighbor in neighbors)
		{
			int neighborId = neighbor.tileId;
			if (neighborId < 0 || neighborId >= count)
			{
				continue;
			}
			float elevation = tiles[neighborId].elevation;
			if (elevation < lowest)
			{
				lowest = elevation;
				result = neighborId;
			}
		}
		return result;
	}
	
	//// Public API
	
	public string Name => "Drainage";
	
	public void Run(GenerationContext context)
	{
		// Find where each land tile's rain runs off to
		PlanetLayer layer = context.Layer;
		List<Tile> tiles = layer.Tiles;
		int count = tiles.Count;
		int[] downhill = new int[count];
		float[] flow = new float[count];
		List<int> landTiles = new(count);
		for (int tileId = 0; tileId < count; tileId++)
		{
			downhill[tileId] = NO_OUTLET;
			if (tiles[tileId].elevation <= 0f)
			{
				continue;
			}
			landTiles.Add(tileId);
			// Each tile starts with the water that fell on it.
			flow[tileId] = Mathf.Max(0f, tiles[tileId].rainfall);
			downhill[tileId] = LowestNeighbor(layer, tiles, tileId, count);
		}
		
		// Pass the water downhill, highest ground first
		// Settle the high ground first so every tile has already collected from upstream
		// by the time it hands its own total onwards.
		int CompareHighestFirst(int first, int second)
		{
			return tiles[second].elevation.CompareTo(tiles[first].elevation);
		}
		
		int[] order = landTiles.ToArray();
		Array.Sort(order, CompareHighestFirst);
		foreach (int tileId in order)
		{
			int next = downhill[tileId];
			if (next >= 0 && tiles[next].elevation > 0f)
			{
				flow[next] += flow[tileId];
			}
		}
		
		// Store the flow on a logarithmic scale
		float logFull = Mathf.Log(FULL_FLOW + 1f);
		for (int tileId = 0; tileId < count; tileId++)
		{
			if (tiles[tileId].elevation <= 0f)
			{
				continue;
			}
			context.FlowAccumulation[tileId] = Mathf.Clamp01(Mathf.Log(flow[tileId] + 1f) / logFull);
		}
	}
}
