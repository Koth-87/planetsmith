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
	
	// Effective moisture below which no wetland forms, and where more stops helping. Read
	// from moisture rather than rainfall because a wetland is water the climate cannot
	// evaporate away. On rainfall alone a swamp needed over 1000mm, which only the wet
	// tropics reach, so temperate swamps grew in equatorial inlands and almost nowhere
	// temperate, while cool lowlands that hardly evaporate anything never got their bogs.
	private const float MINIMUM_WETLAND_ARIDITY_INDEX = 0.5f;
	private const float FULL_WETLAND_ARIDITY_INDEX = 1.5f;
	
	// Rainfall (mm) over which ground goes from too little water to pool at all to
	// enough. Keeps polar deserts dry, where almost nothing evaporates either.
	private const float MINIMUM_WETLAND_RAINFALL = 200f;
	private const float FULL_WETLAND_RAINFALL = 400f;
	
	// Metres a tile must sit below its neighbours to count as a full basin.
	private const float BASIN_DEPTH_SCALE = 150f;
	
	// Floor on the collection term so flat wet plains still hold some water.
	private const float BASIN_FLOOR = 0.35f;
	
	// How much of the collection term the upstream catchment can account for, as
	// opposed to the tile simply sitting in a dip. Floodplains along a big river are
	// waterlogged even where the ground barely dips at all.
	private const float FLOW_SHARE = 0.6f;
	
	// Tiles draining through a tile over which it goes from an ordinary valley floor to a
	// full floodplain, on a logarithmic scale. Counted in tiles rather than read from the
	// flow score, which includes a tile's own rain and so rated a lone hilltop as half a
	// river. Once wetness could reach full, that waterlogged nearly every flat tile in a
	// humid climate and turned whole latitudes into bog.
	private const float FLOODPLAIN_START_TILES = 10f;
	private const float FLOODPLAIN_FULL_TILES = 500f;
	
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
	
	/// <summary>How much of a floodplain a tile is, from the number of tiles draining through it.</summary>
	private static float FloodplainShare(int upstreamTiles)
	{
		return Mathf.InverseLerp(Mathf.Log(FLOODPLAIN_START_TILES), Mathf.Log(FLOODPLAIN_FULL_TILES), Mathf.Log(Mathf.Max(1, upstreamTiles)));
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
			float moistureFactor = Mathf.InverseLerp(MINIMUM_WETLAND_ARIDITY_INDEX, FULL_WETLAND_ARIDITY_INDEX, context.AridityIndex[tileId]);
			float waterFactor = Mathf.InverseLerp(MINIMUM_WETLAND_RAINFALL, FULL_WETLAND_RAINFALL, tile.rainfall);
			float wetFactor = moistureFactor * waterFactor;
			float flatFactor = FlatnessFactor(tile.hilliness);
			if (wetFactor <= 0f || flatFactor <= 0f)
			{
				tile.swampiness = 0f;
				continue;
			}
			
			// Collect the water
			// Water collects here either because the ground dips, or because a whole
			// catchment drains through; take whichever is the stronger claim.
			float basin = BasinFactor(layer, tiles, tileId, tile.elevation, count);
			float catchment = FloodplainShare(context.UpstreamTiles[tileId]);
			float collects = Mathf.Max(basin, catchment * FLOW_SHARE);
			float swampiness = wetFactor * flatFactor * (BASIN_FLOOR + (1f - BASIN_FLOOR) * collects);
			tile.swampiness = Mathf.Clamp01(swampiness);
		}
	}
}
