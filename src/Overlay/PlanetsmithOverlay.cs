// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using RimWorld.Planet;
using UnityEngine;
using Verse;
using Planetsmith.Generation;

namespace Planetsmith.Overlay;

/// <summary>
/// Debug map-mode state and colour mapping for visualising Planetsmith's
/// generated climate. Switching mode marks the climate draw layer dirty so it
/// regenerates on the next frame.
/// </summary>
public static class PlanetsmithOverlay
{
	//// Types
	
	/// <summary>A value on a colour scale and the colour it is drawn in.</summary>
	private readonly struct ColorStop
	{
		public readonly float Value;
		public readonly Color Color;
		
		public ColorStop(float value, Color color)
		{
			Value = value;
			Color = color;
		}
	}
	
	//// Constants
	
	// The colour of a tile the overlay leaves showing through.
	private static readonly Color32 TRANSPARENT = new(0, 0, 0, 0);
	
	// Cold -> hot, in degrees Celsius.
	private static readonly ColorStop[] TEMPERATURE_STOPS =
	{
		new(-50f, new Color(0.10f, 0.10f, 0.55f)),
		new(-20f, new Color(0.00f, 0.55f, 0.90f)),
		new(0f, new Color(0.20f, 0.80f, 0.65f)),
		new(15f, new Color(0.45f, 0.85f, 0.25f)),
		new(30f, new Color(0.95f, 0.85f, 0.10f)),
		new(45f, new Color(0.90f, 0.20f, 0.10f)),
	};
	
	// Arid -> wet, in mm/year.
	private static readonly ColorStop[] RAINFALL_STOPS =
	{
		new(0f, new Color(0.85f, 0.75f, 0.45f)),
		new(400f, new Color(0.90f, 0.80f, 0.30f)),
		new(1000f, new Color(0.55f, 0.80f, 0.30f)),
		new(2000f, new Color(0.10f, 0.60f, 0.30f)),
		new(3500f, new Color(0.10f, 0.50f, 0.70f)),
		new(5000f, new Color(0.10f, 0.20f, 0.80f)),
	};
	
	// Dry -> waterlogged, swampiness 0..1.
	private static readonly ColorStop[] SWAMPINESS_STOPS =
	{
		new(0f, new Color(0.80f, 0.72f, 0.50f)),
		new(0.5f, new Color(0.55f, 0.65f, 0.35f)),
		new(0.75f, new Color(0.25f, 0.50f, 0.35f)),
		new(1f, new Color(0.10f, 0.35f, 0.40f)),
	};
	
	// Cold upwelling (-1) -> none (0) -> mild maritime (+1). Grey marks tiles the
	// ocean-current pass left alone, so the affected coasts stand out on their own.
	private static readonly ColorStop[] COASTAL_ANOMALY_STOPS =
	{
		new(-1f, new Color(0.75f, 0.25f, 0.15f)),
		new(-0.05f, new Color(0.90f, 0.70f, 0.55f)),
		new(0f, new Color(0.28f, 0.28f, 0.30f)),
		new(0.05f, new Color(0.55f, 0.85f, 0.75f)),
		new(1f, new Color(0.10f, 0.45f, 0.85f)),
	};
	
	// Effective moisture, on the standard aridity bands: hyper-arid, arid, semi-arid,
	// dry sub-humid, then humid.
	private static readonly ColorStop[] ARIDITY_STOPS =
	{
		new(0f, new Color(0.75f, 0.35f, 0.20f)),
		new(0.2f, new Color(0.90f, 0.75f, 0.35f)),
		new(0.5f, new Color(0.80f, 0.85f, 0.35f)),
		new(0.65f, new Color(0.45f, 0.80f, 0.35f)),
		new(1.5f, new Color(0.15f, 0.55f, 0.35f)),
		new(3f, new Color(0.10f, 0.35f, 0.65f)),
	};
	
	// No monsoon (grey) through to a drenching one. Grey marks tiles the seasonal
	// rains never reach, so the monsoon belt stands out on its own.
	private static readonly ColorStop[] MONSOON_STOPS =
	{
		new(0f, new Color(0.28f, 0.28f, 0.30f)),
		new(0.05f, new Color(0.70f, 0.75f, 0.45f)),
		new(0.4f, new Color(0.35f, 0.70f, 0.45f)),
		new(0.7f, new Color(0.15f, 0.55f, 0.70f)),
		new(1f, new Color(0.35f, 0.20f, 0.75f)),
	};
	
	// Ridges (dry, pale) through to the valleys whole watersheds drain along.
	private static readonly ColorStop[] DRAINAGE_STOPS =
	{
		new(0f, new Color(0.82f, 0.78f, 0.62f)),
		new(0.35f, new Color(0.55f, 0.72f, 0.45f)),
		new(0.6f, new Color(0.25f, 0.60f, 0.60f)),
		new(0.8f, new Color(0.12f, 0.40f, 0.80f)),
		new(1f, new Color(0.05f, 0.15f, 0.55f)),
	};
	
	// Air that arrived with all its water (grey) through to air wrung out crossing the
	// peaks. Grey marks tiles no mountain upwind has taken anything from, so a range's
	// reach downwind stands out on its own rather than blending into the dry country
	// it happens to sit in.
	private static readonly ColorStop[] RAIN_SHADOW_STOPS =
	{
		new(0f, new Color(0.28f, 0.28f, 0.30f)),
		new(0.05f, new Color(0.55f, 0.70f, 0.50f)),
		new(0.35f, new Color(0.85f, 0.80f, 0.40f)),
		new(0.65f, new Color(0.85f, 0.55f, 0.25f)),
		new(1f, new Color(0.65f, 0.20f, 0.15f)),
	};
	
	// Maritime -> continental, continentality 0..1.
	private static readonly ColorStop[] CONTINENTALITY_STOPS =
	{
		new(0f, new Color(0.20f, 0.60f, 0.75f)),
		new(0.5f, new Color(0.85f, 0.80f, 0.50f)),
		new(1f, new Color(0.70f, 0.35f, 0.20f)),
	};
	
	//// References and State
	
	public static OverlayMode Mode { get; private set; } = OverlayMode.None;
	
	//// Private Functions
	
	/// <summary>A tile's colour from one of the fields only kept for the last generated world.</summary>
	private static Color32 CachedColor(ColorStop[] stops, float[]? field, int tileIndex)
	{
		if (!PlanetsmithClimateCache.IsValid || field == null || tileIndex < 0 || tileIndex >= field.Length)
		{
			return TRANSPARENT;
		}
		return Evaluate(stops, field[tileIndex]);
	}
	
	/// <summary>The colour a value falls on, blended between the stops either side of it.</summary>
	private static Color Evaluate(ColorStop[] stops, float value)
	{
		// Clamp to the ends of the scale
		if (value <= stops[0].Value)
		{
			return stops[0].Color;
		}
		int last = stops.Length - 1;
		if (value >= stops[last].Value)
		{
			return stops[last].Color;
		}
		
		// Blend between the stops either side
		for (int index = 1; index < stops.Length; index++)
		{
			if (value <= stops[index].Value)
			{
				float blend = Mathf.InverseLerp(stops[index - 1].Value, stops[index].Value, value);
				return Color.Lerp(stops[index - 1].Color, stops[index].Color, blend);
			}
		}
		return stops[last].Color;
	}
	
	//// Public API
	
	public static void SetMode(OverlayMode mode)
	{
		Mode = mode;
		World world = Find.World;
		PlanetLayer? surface = Find.WorldGrid?.Surface;
		if (world?.renderer != null && surface != null)
		{
			world.renderer.SetDirty<WorldDrawLayer_PlanetsmithClimate>(surface);
		}
	}
	
	/// <summary>Player-facing name for a map mode.</summary>
	public static string Label(OverlayMode mode)
	{
		switch (mode)
		{
			case OverlayMode.Temperature: return "Planetsmith_MapModeTemperature".Translate();
			case OverlayMode.Rainfall: return "Planetsmith_MapModeRainfall".Translate();
			case OverlayMode.Swampiness: return "Planetsmith_MapModeSwampiness".Translate();
			case OverlayMode.Continentality: return "Planetsmith_MapModeContinentality".Translate();
			case OverlayMode.WinterTemperature: return "Planetsmith_MapModeWinterTemperature".Translate();
			case OverlayMode.OceanCurrents: return "Planetsmith_MapModeOceanCurrents".Translate();
			case OverlayMode.Aridity: return "Planetsmith_MapModeAridity".Translate();
			case OverlayMode.Monsoon: return "Planetsmith_MapModeMonsoon".Translate();
			case OverlayMode.Drainage: return "Planetsmith_MapModeDrainage".Translate();
			case OverlayMode.RainShadow: return "Planetsmith_MapModeRainShadow".Translate();
			default: return "Planetsmith_MapModeOff".Translate();
		}
	}
	
	/// <summary>Whether a mode shows a layer worked out only while generating, which saves do not keep.</summary>
	public static bool IsBuiltDuringGeneration(OverlayMode mode)
	{
		return mode is OverlayMode.Continentality or OverlayMode.WinterTemperature or OverlayMode.OceanCurrents or OverlayMode.Aridity or OverlayMode.Monsoon or OverlayMode.Drainage or OverlayMode.RainShadow;
	}
	
	public static Color32 ColorFor(int tileIndex, Tile tile)
	{
		switch (Mode)
		{
			case OverlayMode.Temperature:
				return Evaluate(TEMPERATURE_STOPS, tile.temperature);
			case OverlayMode.Rainfall:
				return Evaluate(RAINFALL_STOPS, tile.rainfall);
			case OverlayMode.Swampiness:
				return Evaluate(SWAMPINESS_STOPS, tile.swampiness);
			case OverlayMode.Continentality:
				return CachedColor(CONTINENTALITY_STOPS, PlanetsmithClimateCache.Continentality, tileIndex);
			case OverlayMode.WinterTemperature:
				return CachedColor(TEMPERATURE_STOPS, PlanetsmithClimateCache.WinterMinimumTemperature, tileIndex);
			case OverlayMode.OceanCurrents:
				return CachedColor(COASTAL_ANOMALY_STOPS, PlanetsmithClimateCache.CoastalAnomaly, tileIndex);
			case OverlayMode.Aridity:
				return CachedColor(ARIDITY_STOPS, PlanetsmithClimateCache.AridityIndex, tileIndex);
			case OverlayMode.Monsoon:
				return CachedColor(MONSOON_STOPS, PlanetsmithClimateCache.MonsoonStrength, tileIndex);
			case OverlayMode.RainShadow:
				// The sea is where air refills rather than anywhere a shadow falls, so leave
				// it showing through: the shadows then read against a coastline, which is
				// what tells you whether one has any room left to fade in.
				return tile.WaterCovered ? TRANSPARENT : CachedColor(RAIN_SHADOW_STOPS, PlanetsmithClimateCache.RainShadow, tileIndex);
			case OverlayMode.Drainage:
				// Water carries no runoff of its own, and shading it would make the sea look
				// like the driest ground there is. Leave it showing through so the drainage
				// networks read against a real coastline.
				return tile.WaterCovered ? TRANSPARENT : CachedColor(DRAINAGE_STOPS, PlanetsmithClimateCache.FlowAccumulation, tileIndex);
			default:
				return TRANSPARENT;
		}
	}
}
