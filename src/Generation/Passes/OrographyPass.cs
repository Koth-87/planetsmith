// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Planetsmith.Generation.Passes;

/// <summary>
/// Terrain-driven rainfall adjustment. Where the prevailing wind climbs into higher
/// ground the air is forced upward and rains out on the windward slope; whatever
/// crosses the top arrives with that much less water to give.
///
/// The loss is carried downwind rather than applied where it happens. A rain shadow
/// is not really a property of the slope behind a range, it is a property of the air
/// that came over it, and that air stays dry for a long way: the Great Basin lies
/// several hundred kilometres behind the Sierra Nevada and is dry the whole way
/// across. So each tile's climb is turned into a share of moisture taken from the
/// passing air, and that depletion is then advected along the wind, recovering
/// gradually as the air draws moisture back off the ground it crosses, and resetting
/// entirely once it reaches open sea.
///
/// Runs after the base climate, before biomes.
/// </summary>
public sealed class OrographyPass : IGenerationPass
{
	//// Constants
	
	// Both of the numbers below are calibrated against what the terrain actually offers,
	// which is a much narrower range than it looks from the map: measured over a whole
	// planet, 74% of land climbs less than 25m to the tile upwind of it, 98% climbs less
	// than 100m, and the single steepest rise anywhere on the globe was 504m. A scale set
	// by eye from how tall the mountains are, rather than from how abruptly they rise
	// between one tile and the next, leaves them inert.
	//
	// Rise to the upwind neighbour that wrings the air out in a single tile. Reachable on
	// a mountain front, which is the point: a threshold nothing ever crosses is the same
	// as having no mountains at all.
	private const float SLOPE_SCALE = 320f;
	
	// Rise below which the air is not lifted enough to rain out, and simply flows over.
	// Set above the rolling country that makes up most of a continent. Without a floor
	// here every slight undulation sheds a crumb, and those crumbs compound along the
	// wind until the whole landmass carries a haze of shadow belonging to no mountain in
	// particular, which the ranges that should own it never stand out from.
	private const float MINIMUM_LIFTING_RISE = 80f;
	
	// Air forced up a slope sheds a great deal of its water there, so the windward flank
	// gains more than the lee loses. Worth being generous with: a range whose two sides
	// look alike is the clearest sign the mountains are doing nothing.
	private const float WINDWARD_BOOST = 1.2f;
	
	// Rain lost where the arriving air has been wrung out completely. Can afford to be
	// this heavy because so little ground is ever deeply shaded: measured over a planet,
	// the mean depletion is about 0.03, so this costs the world under 2% of its rain
	// overall while halving it in the places that have genuinely earned it. Still a long
	// way short of all of it, though, since dry country is the aim rather than dead
	// ground, and rain that reaches nothing is a floor everything downstream multiplies
	// back into nothing.
	private const float LEEWARD_SHADOW = 0.55f;
	
	// Share of the depletion still carried after one tile of travel. Sets how far a
	// shadow reaches: 0.85 leaves a range still drying the land six or seven tiles behind
	// it, and spent by twenty.
	private const float LEE_RECOVERY = 0.85f;
	
	// Tiles of reach resolved. Past this the depletion left is under a hundredth.
	private const int PROPAGATION_STEPS = 32;
	
	// Depletion at which a tile counts as meaningfully shaded, for the log line.
	private const float NOTABLE_SHADOW = 0.25f;
	
	// Bucket edges, in metres, for reporting what the terrain actually offers the wind.
	private static readonly float[] RISE_BUCKETS = { 25f, 50f, 100f, 200f, 400f };
	
	//// Private Functions
	
	/// <summary>
	/// Rains out the windward slopes and records how much each climb takes from the
	/// air crossing it. Both come from the same measured rise, so a slope's own gain
	/// and the loss it inflicts downwind can never disagree.
	/// </summary>
	private static void MeasureClimbs(GenerationContext context, PlanetLayer layer, List<Tile> tiles, int count, bool[] isLand, float[] shed)
	{
		int[] histogram = new int[RISE_BUCKETS.Length + 1];
		float steepest = 0f;
		int land = 0;
		for (int tileId = 0; tileId < count; tileId++)
		{
			// Skip the sea, and anywhere the wind does not blow
			Tile tile = tiles[tileId];
			if (!isLand[tileId])
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
			
			// Measure the climb from the tile upwind
			land++;
			int upwind = TileGeometry.UpwindNeighbor(layer, tileId, center, wind);
			float upwindElevation = upwind >= 0 ? tiles[upwind].elevation : tile.elevation;
			float rise = tile.elevation - upwindElevation;
			RecordRise(histogram, ref steepest, rise);
			if (rise <= MINIMUM_LIFTING_RISE)
			{
				continue; // level, descending, or too slight a rise to lift the air
			}
			
			// Rain out the windward slope
			float climb = Mathf.Clamp01((rise - MINIMUM_LIFTING_RISE) / (SLOPE_SCALE - MINIMUM_LIFTING_RISE));
			tile.rainfall = Mathf.Max(0f, tile.rainfall * (1f + WINDWARD_BOOST * climb));
			
			// Take the same share from the air crossing the top
			// The player's dial belongs here rather than on the final loss, so that turning
			// it up both deepens a shadow and lengthens it: air stripped harder at the
			// peaks stays dry further downwind. It also means no setting can push rainfall
			// toward nothing, since the loss it feeds is capped and rain never falls all
			// the way to zero.
			shed[tileId] = Mathf.Clamp01(climb * context.Tuning.rainShadow);
		}
		
		ReportRises(histogram, steepest, land);
	}
	
	private static void RecordRise(int[] histogram, ref float steepest, float rise)
	{
		// Track the steepest rise
		if (rise > steepest)
		{
			steepest = rise;
		}
		
		// Count it in its bucket
		for (int bucket = 0; bucket < RISE_BUCKETS.Length; bucket++)
		{
			if (rise < RISE_BUCKETS[bucket])
			{
				histogram[bucket]++;
				return;
			}
		}
		histogram[RISE_BUCKETS.Length]++;
	}
	
	private static string RiseBucketName(int bucket)
	{
		if (bucket == 0)
		{
			return $"under {RISE_BUCKETS[0]:F0}m";
		}
		if (bucket == RISE_BUCKETS.Length)
		{
			return $"over {RISE_BUCKETS[bucket - 1]:F0}m";
		}
		return $"{RISE_BUCKETS[bucket - 1]:F0}-{RISE_BUCKETS[bucket]:F0}m";
	}
	
	/// <summary>
	/// Reports what the terrain gives the wind to climb. Everything this pass does
	/// hangs off that one distribution, and it is not something the map screen can
	/// show: two worlds whose mountains look identical can offer the air completely
	/// different slopes, depending on how finely the planet happens to be divided.
	/// </summary>
	private static void ReportRises(int[] histogram, float steepest, int land)
	{
		// Sanity check
		if (land <= 0)
		{
			return;
		}
		
		// List each bucket's share of the land
		List<string> buckets = new(histogram.Length);
		for (int bucket = 0; bucket < histogram.Length; bucket++)
		{
			// A share of whole tile counts, so one side becomes a float to keep the
			// division from rounding down to nothing.
			float share = (float)histogram[bucket] / land;
			buckets.Add($"{share:P1} {RiseBucketName(bucket)}");
		}
		Log.Message($"[Planetsmith] Upwind rise per tile: {string.Join(", ", buckets)}, steepest {steepest:F0}m.");
	}
	
	/// <summary>
	/// Settles how depleted the air arriving at each tile is, by relaxation along the
	/// wind: one tile of reach resolves per step. Depletions combine as shares of
	/// what is left rather than adding up, so a second range behind the first dries
	/// the air further without the total ever passing the whole of it.
	/// </summary>
	private static void CarryDownwind(GenerationContext context, bool[] isLand, int count, float[] shed)
	{
		UpwindGraph graph = context.Upwind;
		float[] arriving = context.RainShadow;
		float[] leaving = new float[count];
		float[] next = new float[count];
		for (int step = 0; step < PROPAGATION_STEPS; step++)
		{
			for (int tileId = 0; tileId < count; tileId++)
			{
				// Let the sea refill the air
				// Open water hands the air its moisture back almost at once, so a shadow
				// never survives a crossing.
				if (!isLand[tileId])
				{
					next[tileId] = 0f;
					continue;
				}
				
				// Gather the depletion the wind brings
				UpwindGraph.Source[]? sources = graph.Sources[tileId];
				float carried = 0f;
				if (sources != null)
				{
					foreach (UpwindGraph.Source source in sources)
					{
						carried += source.Weight * leaving[source.TileId];
					}
					carried *= LEE_RECOVERY;
				}
				
				// Add this tile's own climb to what it passes on
				// What this tile receives is what the air brought; what it passes on
				// includes its own climb. Keeping the two apart matters, or a peak would
				// stand in its own shadow while it is busy raining.
				arriving[tileId] = carried;
				next[tileId] = 1f - (1f - carried) * (1f - shed[tileId]);
			}
			
			(leaving, next) = (next, leaving);
		}
	}
	
	private static void ApplyShadow(GenerationContext context, List<Tile> tiles, int count, bool[] isLand)
	{
		// Dry each tile by the depletion it receives
		float land = 0f;
		float shaded = 0f;
		float total = 0f;
		for (int tileId = 0; tileId < count; tileId++)
		{
			if (!isLand[tileId])
			{
				continue;
			}
			
			Tile tile = tiles[tileId];
			float shadow = context.RainShadow[tileId];
			land++;
			total += shadow;
			if (shadow >= NOTABLE_SHADOW)
			{
				shaded++;
			}
			if (shadow > 0f)
			{
				tile.rainfall = Mathf.Max(0f, tile.rainfall * (1f - LEEWARD_SHADOW * shadow));
			}
		}
		if (land <= 0f)
		{
			return;
		}
		
		// Report how much of the planet lies in shade
		// How much of a planet ends up in shade is the one thing about this that is hard
		// to judge by eye, since the deep shadow beside a range looks the same whether it
		// fades over three tiles or fifteen. Worth a number.
		Log.Message($"[Planetsmith] Rain shadow over {shaded / land:P1} of land (mean depletion {total / land:F2}).");
	}
	
	//// Public API
	
	public string Name => "Orography";
	
	public void Run(GenerationContext context)
	{
		// Mark the land
		List<Tile> tiles = context.Layer.Tiles;
		int count = tiles.Count;
		bool[] isLand = new bool[count];
		for (int tileId = 0; tileId < count; tileId++)
		{
			isLand[tileId] = tiles[tileId].elevation > 0f;
		}
		
		// Rain out the slopes, carry the loss downwind, then apply it
		float[] shed = new float[count];
		MeasureClimbs(context, context.Layer, tiles, count, isLand, shed);
		CarryDownwind(context, isLand, count, shed);
		ApplyShadow(context, tiles, count, isLand);
	}
}
