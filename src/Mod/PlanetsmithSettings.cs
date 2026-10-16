// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using Verse;

namespace Planetsmith;

/// <summary>
/// Persistent, global mod settings. Fields are added here as features land;
/// per-world generation parameters are stored separately with the save.
/// </summary>
public class PlanetsmithSettings : ModSettings
{
	//// Constants
	
	/// <summary>Share of the surface left as land when nothing else says otherwise.</summary>
	public const float DEFAULT_TARGET_LAND_FRACTION = 0.4f;
	
	/// <summary>Earth's tilt, which new planets start from.</summary>
	public const float DEFAULT_AXIAL_TILT = 23.4f;
	
	// Limits on the sea level and tilt sliders.
	public const float MINIMUM_LAND_FRACTION = 0.05f;
	public const float MAXIMUM_LAND_FRACTION = 0.95f;
	public const float MAXIMUM_AXIAL_TILT = 90f;
	
	//// References and State
	
	/// <summary>Master toggle for the climate and biome generation overhaul.</summary>
	public bool isClimateOverhaulEnabled = true;
	
	/// <summary>When set, Planetsmith moves sea level to hit <see cref="targetLandFraction"/>.</summary>
	public bool isSeaLevelControlEnabled = false;
	
	/// <summary>Fraction of the planet's surface that should be dry land.</summary>
	public float targetLandFraction = DEFAULT_TARGET_LAND_FRACTION;
	
	/// <summary>Planet's axial tilt in degrees. Earth is 23.4; 0 means no seasons.</summary>
	public float axialTilt = DEFAULT_AXIAL_TILT;
	
	/// <summary>Show the map-mode button on the planet view.</summary>
	public bool shouldShowMapModeButton = true;
	
	//// Event Wiring
	
	public override void ExposeData()
	{
		// The labels keep the fields' old names, so settings saved before the rename
		// still load.
		base.ExposeData();
		Scribe_Values.Look(ref isClimateOverhaulEnabled, "enableClimateOverhaul", defaultValue: true);
		Scribe_Values.Look(ref isSeaLevelControlEnabled, "enableSeaLevelControl", defaultValue: false);
		Scribe_Values.Look(ref targetLandFraction, "targetLandFraction", DEFAULT_TARGET_LAND_FRACTION);
		Scribe_Values.Look(ref axialTilt, "axialTilt", DEFAULT_AXIAL_TILT);
		Scribe_Values.Look(ref shouldShowMapModeButton, "showMapModeButton", defaultValue: true);
	}
}
