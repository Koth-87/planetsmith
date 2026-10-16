// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;

namespace Planetsmith.Generation.Passes;

/// <summary>
/// Drainage-aware swampiness. Wetlands form where water arrives and cannot leave:
/// wet climates, flat ground, and local basins that sit lower than their
/// surroundings. Replaces vanilla's noise-driven swampiness, and because the
/// vanilla swamp and bog workers gate on <c>tile.swampiness</c>, this steers those
/// biomes toward genuinely waterlogged ground with no per-biome tuning.
/// </summary>
public sealed class SwampinessPass : IGenerationPass
{
	//// Constants
	
	// Rainfall (mm) below which no wetland forms, and where rainfall stops helping.
	private const float MINIMUM_WETLAND_RAINFALL = 500f;
	private const float FULL_WETLAND_RAINFALL = 1600f;
	
	// Metres a tile must sit below its neighbours to count as a full basin.
	private const float BASIN_DEPTH_SCALE = 150f;
	
	// Floor on the collection term so flat wet plains still hold some water.
	private const float BASIN_FLOOR = 0.35f;
	
	// How much of the collection term the upstream catchment can account for, as
	// opposed to the tile simply sitting in a dip. Floodplains along a big river are
	// waterlogged even where the ground barely dips at all.
	private const float FLOW_SHARE = 0.6f;
	
	//// References and State
	
	private static readonly List<PlanetTile> neighbors = new();
	
	//// Private Functions
	
	/// <summary>How readily the ground holds water, from flat plains down to none in the mountains.</summary>
	private static float FlatnessFactor(Hilliness hilliness)
	{
		return hilliness switch
		{
			Hilliness.Flat => 1f,
			Hilliness.SmallHills => 0.55f,
			Hilliness.LargeHills => 0.2f,
			_ => 0f, // mountainous, impassable, undefined
		};
	}
	
	/// <summary>How far a tile sits below its land neighbours, as a share of a full basin.</summary>
	private static float BasinFactor(PlanetLayer layer, List<Tile> tiles, int tileId, float elevation, int count)
	{
		// Average the land around the tile
		layer.GetTileNeighbors(tileId, neighbors);
		float sum = 0f;
		int landNeighbors = 0;
		foreach (PlanetTile neighbor in neighbors)
		{
			int neighborId = neighbor.tileId;
			if (neighborId < 0 || neighborId >= count)
			{
				continue;
			}
			float neighborElevation = tiles[neighborId].elevation;
			if (neighborElevation <= 0f)
			{
				continue; // a coast drains to the sea, not into this tile
			}
			sum += neighborElevation;
			landNeighbors++;
		}
		if (landNeighbors == 0)
		{
			return 0f;
		}
		
		// Measure the dip
		// Positive when this tile is a local low point that water flows toward.
		float meanNeighbor = sum / landNeighbors;
		return Mathf.Clamp01((meanNeighbor - elevation) / BASIN_DEPTH_SCALE);
	}
	
	//// Public API
	
	public string Name => "Swampiness";
	
	public void Run(GenerationContext context)
	{
		PlanetLayer layer = context.Layer;
		List<Tile> tiles = layer.Tiles;
		int count = tiles.Count;
		for (int tileId = 0; tileId < count; tileId++)
		{
			// Keep wetlands off the sea
			Tile tile = tiles[tileId];
			if (tile.elevation <= 0f)
			{
				tile.swampiness = 0f;
				continue;
			}
			
			// Rule out dry or steep ground
			float rainFactor = Mathf.Clamp01((tile.rainfall - MINIMUM_WETLAND_RAINFALL) / (FULL_WETLAND_RAINFALL - MINIMUM_WETLAND_RAINFALL));
			float flatFactor = FlatnessFactor(tile.hilliness);
			if (rainFactor <= 0f || flatFactor <= 0f)
			{
				tile.swampiness = 0f;
				continue;
			}
			
			// Collect the water
			// Water collects here either because the ground dips, or because a whole
			// catchment drains through; take whichever is the stronger claim.
			float basin = BasinFactor(layer, tiles, tileId, tile.elevation, count);
			float catchment = context.FlowAccumulation[tileId];
			float collects = Mathf.Max(basin, catchment * FLOW_SHARE);
			float swampiness = rainFactor * flatFactor * (BASIN_FLOOR + (1f - BASIN_FLOOR) * collects);
			tile.swampiness = Mathf.Clamp01(swampiness);
		}
	}
}
