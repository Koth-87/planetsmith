// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;

namespace Planetsmith.Generation.Passes;

/// <summary>
/// Coastal climate anomalies from the sea and its currents. Which coast of a
/// landmass you are on matters enormously on a real planet, and the deciding factor
/// is whether the prevailing wind arrives off the water or off the land.
///
/// Where the wind blows onshore, the sea hands the coast mild, moist air, so those
/// shores run warmer and wetter than their latitude suggests. Where it blows
/// offshore in the subtropics, water welling up from the deep runs cold along the
/// shore: it chills the air and kills the rainfall, which is how Earth ends up with
/// deserts (the Atacama, the Namib) sitting directly on an ocean.
///
/// The onshore/offshore call is made once at the waterline, where the advected
/// moisture still cleanly reflects wind direction, and is then carried inland across
/// the coastal band. Testing every tile against the moisture directly would instead
/// measure distance from the sea, since moisture decays inland on any coast, and
/// would wrongly flip a genuine maritime coast to offshore a few tiles in.
/// </summary>
public sealed class OceanCurrentPass : IGenerationPass
{
	//// Types
	
	/// <summary>Which way the wind crosses a tile in the coastal band.</summary>
	private enum CoastWind : sbyte
	{
		Unclassified = 0,
		Onshore = 1,
		Offshore = -1,
	}
	
	//// Constants
	
	// How far inland (in tiles) each influence reaches. Upwelling deserts hug the
	// shore far more tightly than general maritime mildness.
	private const int COASTAL_REACH = 6;
	private const int UPWELLING_REACH = 3;
	
	// Distance of the land touching the sea.
	private const int SHORELINE_DISTANCE = 1;
	
	// Share of ocean moisture still in the air above which a shoreline tile counts
	// as having the wind at its back off the water.
	private const float ONSHORE_MOISTURE = 0.75f;
	
	// Onshore wind: mild, damp maritime air.
	private const float MARITIME_WARMING = 2.5f;
	private const float MARITIME_RAIN_BOOST = 0.25f;
	
	// Offshore wind over subtropical upwelling: cold water, suppressed rainfall.
	private const float UPWELLING_COOLING = 3.5f;
	private const float UPWELLING_RAIN_SUPPRESSION = 0.55f;
	private const float UPWELLING_MINIMUM_LATITUDE = 12f;
	private const float UPWELLING_MAXIMUM_LATITUDE = 38f;
	
	//// References and State
	
	private static readonly List<PlanetTile> neighbors = new();
	
	//// Private Functions
	
	/// <summary>
	/// Labels every tile in the coastal band as sitting on an onshore or offshore
	/// wind. Tiles touching the sea are judged from the moisture the wind carries;
	/// tiles further in adopt the verdict of the shore they belong to, walking out
	/// one ring at a time so a coastal strip stays of one mind.
	/// </summary>
	private static CoastWind[] ClassifyCoast(GenerationContext context, PlanetLayer layer, int count)
	{
		// Judge the shoreline by the moisture the wind brings
		List<Tile> tiles = layer.Tiles;
		CoastWind[] wind = new CoastWind[count];
		for (int tileId = 0; tileId < count; tileId++)
		{
			if (tiles[tileId].elevation > 0f && context.CoastDistance[tileId] == SHORELINE_DISTANCE)
			{
				wind[tileId] = context.OceanicMoisture[tileId] >= ONSHORE_MOISTURE ? CoastWind.Onshore : CoastWind.Offshore;
			}
		}
		
		// Carry each verdict inland, one ring at a time
		for (int ring = SHORELINE_DISTANCE + 1; ring <= COASTAL_REACH; ring++)
		{
			for (int tileId = 0; tileId < count; tileId++)
			{
				if (wind[tileId] != CoastWind.Unclassified || tiles[tileId].elevation <= 0f || context.CoastDistance[tileId] != ring)
				{
					continue;
				}
				layer.GetTileNeighbors(tileId, neighbors);
				foreach (PlanetTile neighbor in neighbors)
				{
					int neighborId = neighbor.tileId;
					if (neighborId < 0 || neighborId >= count || context.CoastDistance[neighborId] != ring - 1)
					{
						continue;
					}
					if (wind[neighborId] != CoastWind.Unclassified)
					{
						wind[tileId] = wind[neighborId];
						break;
					}
				}
			}
		}
		
		return wind;
	}
	
	//// Public API
	
	public string Name => "OceanCurrents";
	
	public void Run(GenerationContext context)
	{
		PlanetLayer layer = context.Layer;
		List<Tile> tiles = layer.Tiles;
		int count = tiles.Count;
		CoastWind[] wind = ClassifyCoast(context, layer, count);
		float influence = context.Tuning.coastalInfluence;
		
		for (int tileId = 0; tileId < count; tileId++)
		{
			// Skip tiles outside the coastal band
			CoastWind kind = wind[tileId];
			if (kind == CoastWind.Unclassified)
			{
				continue;
			}
			
			// Fade the influence with distance from the shore
			int distance = context.CoastDistance[tileId];
			float coastal = 1f - (distance - 1f) / COASTAL_REACH;
			Tile tile = tiles[tileId];
			
			// Warm and wet the coast the wind blows onto
			if (kind == CoastWind.Onshore)
			{
				tile.temperature += MARITIME_WARMING * coastal * influence;
				tile.rainfall *= 1f + MARITIME_RAIN_BOOST * coastal * influence;
				context.CoastalAnomaly[tileId] = coastal;
				continue;
			}
			
			// Chill and dry the subtropical coast the wind blows off
			if (distance > UPWELLING_REACH)
			{
				continue;
			}
			float absoluteLatitude = Mathf.Abs(layer.LongLatOf(tileId).y);
			if (absoluteLatitude >= UPWELLING_MINIMUM_LATITUDE && absoluteLatitude <= UPWELLING_MAXIMUM_LATITUDE)
			{
				tile.temperature -= UPWELLING_COOLING * coastal * influence;
				tile.rainfall *= 1f - UPWELLING_RAIN_SUPPRESSION * coastal * influence;
				context.CoastalAnomaly[tileId] = -coastal;
			}
		}
	}
}
