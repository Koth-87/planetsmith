// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

namespace Planetsmith.Generation;

/// <summary>
/// Holds the last-generated derived climate fields (continentality and seasonal
/// extremes) in memory so the debug overlays can display layers that aren't stored
/// on the tile itself. This is a dev/inspection aid, not saved with the world: it
/// is refreshed each time a world is generated and is empty after a reload until
/// the next generation.
/// </summary>
public static class PlanetsmithClimateCache
{
	//// References and State
	
	public static bool IsValid { get; private set; }
	public static float[]? Continentality { get; private set; }
	public static float[]? WinterMinimumTemperature { get; private set; }
	public static float[]? SummerMaximumTemperature { get; private set; }
	public static float[]? CoastalAnomaly { get; private set; }
	public static float[]? AridityIndex { get; private set; }
	public static float[]? MonsoonStrength { get; private set; }
	public static float[]? FlowAccumulation { get; private set; }
	public static float[]? RainShadow { get; private set; }
	
	//// Public API
	
	/// <summary>
	/// Forgets the last world's fields. Called as each world is built, including one
	/// being loaded from a save, because these layers are only worked out while a
	/// world is generated. Without this the overlays would happily paint one planet's
	/// climate onto another of the same size.
	/// </summary>
	public static void Invalidate()
	{
		IsValid = false;
		Continentality = null;
		WinterMinimumTemperature = null;
		SummerMaximumTemperature = null;
		CoastalAnomaly = null;
		AridityIndex = null;
		MonsoonStrength = null;
		FlowAccumulation = null;
		RainShadow = null;
	}
	
	public static void Store(GenerationContext context)
	{
		Continentality = context.Continentality;
		WinterMinimumTemperature = context.WinterMinimumTemperature;
		SummerMaximumTemperature = context.SummerMaximumTemperature;
		CoastalAnomaly = context.CoastalAnomaly;
		AridityIndex = context.AridityIndex;
		MonsoonStrength = context.MonsoonStrength;
		FlowAccumulation = context.FlowAccumulation;
		RainShadow = context.RainShadow;
		IsValid = true;
	}
}
