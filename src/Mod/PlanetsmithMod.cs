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
			return "almost no seasons";
		}
		if (tilt < 18f)
		{
			return "mild seasons";
		}
		if (tilt < 30f)
		{
			return "Earth-like";
		}
		if (tilt < 50f)
		{
			return "harsh seasons";
		}
		return "extreme seasons";
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
		listing.CheckboxLabeled("Enable climate & biome overhaul", ref settings.isClimateOverhaulEnabled, "When on, Planetsmith recomputes each tile's temperature, rainfall, and biome from its own climate model during world generation. When off, worlds generate as vanilla.");
		
		// Draw the sea level control
		listing.Gap(12f);
		listing.CheckboxLabeled("Control sea level", ref settings.isSeaLevelControlEnabled, "When on, Planetsmith raises or lowers sea level during world generation until the planet has the land fraction set below. Leave off to let vanilla, or another mod, decide the coastline.");
		if (settings.isSeaLevelControlEnabled)
		{
			listing.Label($"Land: {settings.targetLandFraction:P0} of the planet's surface");
			settings.targetLandFraction = listing.Slider(settings.targetLandFraction, PlanetsmithSettings.MINIMUM_LAND_FRACTION, PlanetsmithSettings.MAXIMUM_LAND_FRACTION);
		}
		
		// Draw the map mode switch
		listing.Gap(12f);
		listing.CheckboxLabeled("Show map-mode button on the planet view", ref settings.shouldShowMapModeButton, "Adds a button to the planet view for shading the world by temperature, rainfall and Planetsmith's other climate layers.");
		
		// Draw the tilt, unless another mod owns it
		listing.Gap(12f);
		if (!ModCompatibility.IsAxialTiltOwnedExternally())
		{
			listing.Label($"Axial tilt: {settings.axialTilt:F1}°  ({TiltDescription(settings.axialTilt)})");
			settings.axialTilt = listing.Slider(settings.axialTilt, 0f, PlanetsmithSettings.MAXIMUM_AXIAL_TILT);
		}
		
		// Point at the per-planet settings
		listing.Gap(6f);
		listing.Label("These are the defaults new planets start from. To shape one planet, use the Planetsmith button on the world creation page.");
		listing.End();
	}
}
