// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using UnityEngine;
using Verse;
using Planetsmith.Compatibility;

namespace Planetsmith;

/// <summary>
/// Mod entry point. RimWorld instantiates this once at startup and uses it to
/// surface the settings window. The heavy world-generation machinery lives
/// elsewhere; this class only owns configuration and the settings UI.
/// </summary>
public class PlanetsmithMod : Verse.Mod
{
	//// Constants
	
	public const string PACKAGE_ID = "aspctt.planetsmith";
	
	//// References and State
	
	private readonly PlanetsmithSettings settings;
	
	/// <summary>The mod-wide settings, or null before RimWorld has created the mod.</summary>
	public static PlanetsmithSettings? Settings { get; private set; }
	
	//// Public API
	
	public PlanetsmithMod(ModContentPack content) : base(content)
	{
		settings = GetSettings<PlanetsmithSettings>();
		Settings = settings;
	}
	
	/// <summary>A few words on what a tilt does to the seasons, for beside its slider.</summary>
	public static string TiltDescription(float tilt)
	{
		if (tilt < 5f)
		{
			return "Planetsmith_TiltNoSeasons".Translate();
		}
		if (tilt < 18f)
		{
			return "Planetsmith_TiltMildSeasons".Translate();
		}
		if (tilt < 30f)
		{
			return "Planetsmith_TiltEarthLike".Translate();
		}
		if (tilt < 50f)
		{
			return "Planetsmith_TiltHarshSeasons".Translate();
		}
		return "Planetsmith_TiltExtremeSeasons".Translate();
	}
	
	//// Event Wiring
	
	public override string SettingsCategory() => "Planetsmith";
	
	public override void DoSettingsWindowContents(Rect inRect)
	{
		// Draw the overhaul switch
		Listing_Standard listing = new();
		listing.Begin(inRect);
		listing.Label("Planetsmith");
		listing.Gap(6f);
		listing.CheckboxLabeled("Planetsmith_EnableOverhaul".Translate(), ref settings.isClimateOverhaulEnabled, "Planetsmith_EnableOverhaulTip".Translate());
		
		// Draw the sea level control
		listing.Gap(12f);
		listing.CheckboxLabeled("Planetsmith_ControlSeaLevel".Translate(), ref settings.isSeaLevelControlEnabled, "Planetsmith_ControlSeaLevelDefaultTip".Translate());
		if (settings.isSeaLevelControlEnabled)
		{
			listing.Label("Planetsmith_LandFraction".Translate(settings.targetLandFraction.ToString("P0")));
			settings.targetLandFraction = listing.Slider(settings.targetLandFraction, PlanetsmithSettings.MINIMUM_LAND_FRACTION, PlanetsmithSettings.MAXIMUM_LAND_FRACTION);
		}
		
		// Draw the map mode switch
		listing.Gap(12f);
		listing.CheckboxLabeled("Planetsmith_ShowMapModeButton".Translate(), ref settings.shouldShowMapModeButton, "Planetsmith_ShowMapModeButtonTip".Translate());
		
		// Draw the tilt, unless another mod owns it
		listing.Gap(12f);
		if (!ModCompatibility.IsAxialTiltOwnedExternally())
		{
			listing.Label("Planetsmith_AxialTilt".Translate(settings.axialTilt.ToString("F1"), TiltDescription(settings.axialTilt)));
			settings.axialTilt = listing.Slider(settings.axialTilt, 0f, PlanetsmithSettings.MAXIMUM_AXIAL_TILT);
		}
		
		// Point at the per-planet settings
		listing.Gap(6f);
		listing.Label("Planetsmith_DefaultsNote".Translate());
		listing.End();
	}
}
