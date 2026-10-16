// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using UnityEngine;
using Verse;
using Planetsmith.Compatibility;

namespace Planetsmith.UI;

/// <summary>
/// Per-world generation parameters, opened from the world creation page so a planet
/// can be shaped without leaving for the mod options. Edits apply to the world about
/// to be generated; the mod settings remain the defaults new worlds start from.
/// </summary>
public class Dialog_PlanetsmithWorldParameters : PlanetsmithDialog
{
	//// References and State
	
	public override Vector2 InitialSize => new(640f, 660f);
	
	//// Private Functions
	
	/// <summary>
	/// A dimmed aside about another loaded mod that covers the same ground. Shown
	/// only when that mod is actually present, so a plain install stays uncluttered.
	/// </summary>
	private static void DrawNote(Listing_Standard listing, string text)
	{
		GUI.color = new Color(1f, 0.85f, 0.4f, 0.8f);
		Text.Font = GameFont.Tiny;
		listing.Label(text);
		Text.Font = GameFont.Small;
		GUI.color = Color.white;
	}
	
	/// <summary>Names whichever loaded mod is also deciding where the coastline sits.</summary>
	private static string? OtherSeaLevelMod()
	{
		if (ModCompatibility.IsWorldbuilderLoaded)
		{
			return "Worldbuilder";
		}
		return ModCompatibility.IsEarthLikePlanetLoaded ? "Earth-like planet" : null;
	}
	
	//// Event Wiring
	
	public override void DoWindowContents(Rect inRect)
	{
		// Draw the title and explanation
		PlanetsmithWorldSettings world = PlanetsmithWorldParameters.Pending;
		ModCompatibility.EnsureInitialized();
		Listing_Standard listing = new();
		listing.Begin(inRect);
		Text.Font = GameFont.Medium;
		listing.Label("Planetsmith: planet parameters");
		Text.Font = GameFont.Small;
		listing.Gap(4f);
		listing.Label("These apply to the world you are about to generate.");
		listing.GapLine(12f);
		
		// Draw the sea level control
		listing.CheckboxLabeled("Control sea level", ref world.isSeaLevelControlEnabled, "When on, Planetsmith raises or lowers sea level until the planet has the land fraction set below. Leave off to let vanilla, or another mod, decide the coastline.");
		if (world.isSeaLevelControlEnabled)
		{
			listing.Label($"Land: {world.targetLandFraction:P0} of the planet's surface");
			world.targetLandFraction = listing.Slider(world.targetLandFraction, PlanetsmithSettings.MINIMUM_LAND_FRACTION, PlanetsmithSettings.MAXIMUM_LAND_FRACTION);
			string? seaLevelRival = OtherSeaLevelMod();
			if (seaLevelRival != null)
			{
				DrawNote(listing, $"{seaLevelRival} also sets sea level. Planetsmith runs afterwards, so this setting wins; turn it off to leave the coastline to {seaLevelRival}.");
			}
		}
		
		// Draw the tilt, or say which mod owns it
		listing.Gap(12f);
		if (ModCompatibility.IsAxialTiltOwnedExternally())
		{
			// Two sliders for one planet's tilt could only ever disagree, and that mod
			// models the whole business (daylight, the sun's path, the plants) far past
			// where we stop. Its number, our climate.
			listing.Label("Axial tilt: set by Realistic Axial Tilt");
			DrawNote(listing, "Realistic Axial Tilt is installed, so the tilt slider on the world creation page sets it for both mods. Planetsmith builds the biomes around the temperatures it works out, so what you generate matches what you play.");
		}
		else
		{
			listing.Label($"Axial tilt: {world.axialTilt:F1}°  ({PlanetsmithMod.TiltDescription(world.axialTilt)})");
			world.axialTilt = listing.Slider(world.axialTilt, 0f, PlanetsmithSettings.MAXIMUM_AXIAL_TILT);
			listing.Label("Tilt is why a planet has seasons. Upright worlds barely change through the year; steeply tilted ones swing between harsh summers and winters.");
			if (ModCompatibility.IsWorldbuilderLoaded)
			{
				DrawNote(listing, "Worldbuilder has its own axial tilt, which sets how far temperatures swing through the year once you are playing. This one decides how the seasons shape the planet's biomes while it is generated. They are separate, so set both the same way if you want the world to match how it plays.");
			}
		}
		
		// Draw the climate tuning button
		listing.GapLine(16f);
		if (listing.ButtonText(world.tuning.IsDefault ? "Climate tuning..." : "Climate tuning (adjusted)..."))
		{
			Find.WindowStack.Add(new Dialog_ClimateTuning());
		}
		
		// Draw the biomes button
		listing.Gap(8f);
		if (listing.ButtonText(world.HasAdjustedBiomes ? "Biomes (adjusted)..." : "Biomes..."))
		{
			Find.WindowStack.Add(new Dialog_BiomeSettings());
		}
		if (ModCompatibility.IsWorldbuilderLoaded)
		{
			DrawNote(listing, "Worldbuilder's own biome menu offers the same two controls, and both are applied, so you can use either. This one leaves out sea, lake and ice, which have no dry-land alternative and would leave a planet unable to draw if they were turned off.");
		}
		
		// Draw the presets and reset buttons
		listing.Gap(8f);
		if (listing.ButtonText("Presets..."))
		{
			Find.WindowStack.Add(new Dialog_PlanetsmithPresets());
		}
		listing.Gap(12f);
		if (listing.ButtonText("Reset to mod defaults"))
		{
			PlanetsmithWorldParameters.ResetToDefaults();
		}
		listing.End();
	}
}
