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
		listing.Label("Planetsmith_WorldParametersTitle".Translate());
		Text.Font = GameFont.Small;
		listing.Gap(4f);
		listing.Label("Planetsmith_WorldParametersIntro".Translate());
		listing.GapLine(12f);
		
		// Draw the sea level control
		listing.CheckboxLabeled("Planetsmith_ControlSeaLevel".Translate(), ref world.isSeaLevelControlEnabled, "Planetsmith_ControlSeaLevelTip".Translate());
		if (world.isSeaLevelControlEnabled)
		{
			listing.Label("Planetsmith_LandFraction".Translate(world.targetLandFraction.ToString("P0")));
			world.targetLandFraction = listing.Slider(world.targetLandFraction, PlanetsmithSettings.MINIMUM_LAND_FRACTION, PlanetsmithSettings.MAXIMUM_LAND_FRACTION);
			string? seaLevelRival = OtherSeaLevelMod();
			if (seaLevelRival != null)
			{
				DrawNote(listing, "Planetsmith_SeaLevelRivalNote".Translate(seaLevelRival));
			}
		}
		
		// Draw the tilt, or say which mod owns it
		listing.Gap(12f);
		if (ModCompatibility.IsAxialTiltOwnedExternally())
		{
			// Two sliders for one planet's tilt could only ever disagree, and that mod
			// models the whole business (daylight, the sun's path, the plants) far past
			// where we stop. Its number, our climate.
			listing.Label("Planetsmith_AxialTiltExternal".Translate());
			DrawNote(listing, "Planetsmith_AxialTiltExternalNote".Translate());
		}
		else
		{
			listing.Label("Planetsmith_AxialTilt".Translate(world.axialTilt.ToString("F1"), PlanetsmithMod.TiltDescription(world.axialTilt)));
			world.axialTilt = listing.Slider(world.axialTilt, 0f, PlanetsmithSettings.MAXIMUM_AXIAL_TILT);
			listing.Label("Planetsmith_AxialTiltExplanation".Translate());
			if (ModCompatibility.IsWorldbuilderLoaded)
			{
				DrawNote(listing, "Planetsmith_WorldbuilderTiltNote".Translate());
			}
		}
		
		// Draw the climate tuning button
		listing.GapLine(16f);
		if (listing.ButtonText((world.tuning.IsDefault ? "Planetsmith_OpenClimateTuning" : "Planetsmith_OpenClimateTuningAdjusted").Translate()))
		{
			Find.WindowStack.Add(new Dialog_ClimateTuning());
		}
		
		// Draw the biomes button
		listing.Gap(8f);
		if (listing.ButtonText((world.HasAdjustedBiomes ? "Planetsmith_OpenBiomesAdjusted" : "Planetsmith_OpenBiomes").Translate()))
		{
			Find.WindowStack.Add(new Dialog_BiomeSettings());
		}
		if (ModCompatibility.IsWorldbuilderLoaded)
		{
			DrawNote(listing, "Planetsmith_WorldbuilderBiomesNote".Translate());
		}
		
		// Draw the presets and reset buttons
		listing.Gap(8f);
		if (listing.ButtonText("Planetsmith_OpenPresets".Translate()))
		{
			Find.WindowStack.Add(new Dialog_PlanetsmithPresets());
		}
		listing.Gap(12f);
		if (listing.ButtonText("Planetsmith_ResetToModDefaults".Translate()))
		{
			PlanetsmithWorldParameters.ResetToDefaults();
		}
		listing.End();
	}
}
