// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Planetsmith.Generation.Passes;

/// <summary>
/// Fills the hollows that rivers run into but never out of. Drainage has already
/// worked out how much water crosses every tile; where a great deal of it arrives at
/// ground ringed entirely by higher land, it has nowhere left to go and stands there.
/// That is how the Caspian and the Great Salt Lake came to be, and it is the one
/// place a large body of water belongs nowhere near a coast.
///
/// The hollow is flooded up to the lip of the surrounding land rather than a fixed
/// depth, so a shallow wide basin becomes a broad lake and a tight one a small tarn.
/// Because a tile counts as water simply by lying below sea level, dropping these
/// tiles is enough for the lake and ocean biomes to claim them on their own.
///
/// Runs after drainage, which tells it where the water goes, and before the biome
/// pass, which needs the finished coastline.
/// </summary>
public sealed class BasinLakePass : IGenerationPass
{
	//// Constants
	
	// Share of the landscape's runoff a hollow must gather before it holds a lake. Read
	// this against the logarithmic scale it is measured on, not as a plain fraction:
	// because the flow field is compressed, even a middling-looking figure here amounts
	// to barely one tile's rainfall, which every dip in the ground clears. This asks for
	// something closer to a hundred tiles draining in.
	private const float MINIMUM_INFLOW = 0.855f;
	
	// Most tiles one basin may flood, so a vast flat interior cannot become a sea.
	private const int MAXIMUM_LAKE_TILES = 25;
	
	// Fewest tiles that make a lake; a single dimple is a puddle.
	private const int MINIMUM_LAKE_TILES = 2;
	
	// How far above the deepest point the water may climb, which is what keeps a
	// shallow dimple small and lets a genuinely deep hollow hold a broader lake.
	private const float MAXIMUM_BASIN_RELIEF = 50f;
	
	// Metres of water left above the deepest point once the basin has filled.
	private const float LAKE_DEPTH = 30f;
	
	//// References and State
	
	private static readonly List<PlanetTile> neighbors = new();
	
	//// Private Functions
	
	/// <summary>True when no neighbour lies lower, so water arriving here cannot leave.</summary>
	private static bool IsSink(PlanetLayer layer, List<Tile> tiles, int tileId, int count)
	{
		layer.GetTileNeighbors(tileId, neighbors);
		float here = tiles[tileId].elevation;
		foreach (PlanetTile neighbor in neighbors)
		{
			int neighborId = neighbor.tileId;
			if (neighborId >= 0 && neighborId < count && tiles[neighborId].elevation < here)
			{
				return false;
			}
		}
		return true;
	}
	
	/// <summary>
	/// Raises the water from the lowest point outwards, always taking the lowest tile
	/// still on the shore, until it would spill over the rim or the basin grows past
	/// what one hollow can plausibly hold.
	/// </summary>
	private static int FloodBasin(PlanetLayer layer, List<Tile> tiles, int count, int sink, bool[] flooded, List<int> basin, List<int> frontier)
	{
		// Start from the lowest point
		basin.Clear();
		frontier.Clear();
		HashSet<int> inBasin = new() { sink };
		basin.Add(sink);
		AddNeighbors(layer, tiles, count, sink, inBasin, frontier);
		float floor = tiles[sink].elevation;
		float waterLevel = floor;
		
		// Raise the water until it spills or the basin is full
		while (basin.Count < MAXIMUM_LAKE_TILES && frontier.Count > 0)
		{
			// The rim gives way at its lowest point, so that is where the water goes next.
			int lowestIndex = 0;
			for (int index = 1; index < frontier.Count; index++)
			{
				if (tiles[frontier[index]].elevation < tiles[frontier[lowestIndex]].elevation)
				{
					lowestIndex = index;
				}
			}
			int next = frontier[lowestIndex];
			frontier.RemoveAt(lowestIndex);
			
			if (tiles[next].elevation <= 0f)
			{
				// The hollow drains to the sea after all; it is no basin.
				return 0;
			}
			if (tiles[next].elevation > floor + MAXIMUM_BASIN_RELIEF)
			{
				// The rim rises out of reach: the water has found its own level.
				break;
			}
			
			waterLevel = tiles[next].elevation;
			basin.Add(next);
			inBasin.Add(next);
			AddNeighbors(layer, tiles, count, next, inBasin, frontier);
		}
		
		// Leave puddles dry
		if (basin.Count < MINIMUM_LAKE_TILES)
		{
			return 0;
		}
		
		// Sink the basin below its new waterline
		foreach (int tileId in basin)
		{
			flooded[tileId] = true;
			tiles[tileId].elevation = Mathf.Min(tiles[tileId].elevation - LAKE_DEPTH, waterLevel - LAKE_DEPTH);
			tiles[tileId].hilliness = Hilliness.Flat;
		}
		return basin.Count;
	}
	
	private static void AddNeighbors(PlanetLayer layer, List<Tile> tiles, int count, int tileId, HashSet<int> inBasin, List<int> frontier)
	{
		layer.GetTileNeighbors(tileId, neighbors);
		foreach (PlanetTile neighbor in neighbors)
		{
			int neighborId = neighbor.tileId;
			if (neighborId >= 0 && neighborId < count && !inBasin.Contains(neighborId) && !frontier.Contains(neighborId))
			{
				frontier.Add(neighborId);
			}
		}
	}
	
	//// Public API
	
	public string Name => "BasinLakes";
	
	public void Run(GenerationContext context)
	{
		// Prepare the buffers
		PlanetLayer layer = context.Layer;
		List<Tile> tiles = layer.Tiles;
		int count = tiles.Count;
		bool[] flooded = new bool[count];
		List<int> basin = new();
		List<int> frontier = new();
		int lakes = 0;
		int lakeTiles = 0;
		
		// Flood every sink enough water drains into
		for (int tileId = 0; tileId < count; tileId++)
		{
			if (flooded[tileId] || tiles[tileId].elevation <= 0f)
			{
				continue;
			}
			if (context.FlowAccumulation[tileId] < MINIMUM_INFLOW || !IsSink(layer, tiles, tileId, count))
			{
				continue;
			}
			
			int filled = FloodBasin(layer, tiles, count, tileId, flooded, basin, frontier);
			if (filled > 0)
			{
				lakes++;
				lakeTiles += filled;
			}
		}
		
		// Report the lakes
		if (lakes > 0)
		{
			Log.Message($"[Planetsmith] Filled {lakes} inland basins with water, covering {lakeTiles} tiles.");
		}
	}
}
