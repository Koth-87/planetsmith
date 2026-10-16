// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using UnityEngine;
using Verse;

namespace Planetsmith.UI;

/// <summary>
/// Dials on the climate model for the planet being generated. Every one of these
/// nudges a part of the simulation rather than setting a value outright, so leaving
/// them alone produces the world Planetsmith would have built anyway.
/// </summary>
public class Dialog_ClimateTuning : PlanetsmithDialog
{
	//// Constants
	
	private const float ROW_HEIGHT = 28f;
	
	// Range of every multiplier dial.
	private const float MAXIMUM_FACTOR = 2f;
	private const float FACTOR_STEP = 0.05f;
	
	// Range of the temperature dial, in degrees.
	private const float TEMPERATURE_OFFSET_RANGE = 20f;
	
	//// References and State
	
	public override Vector2 InitialSize => new(640f, 660f);
	
	//// Private Functions
	
	private static void DrawFactorSlider(Listing_Standard listing, string label, ref float value, string tooltip)
	{
		Rect row = listing.GetRect(ROW_HEIGHT);
		TooltipHandler.TipRegion(row, tooltip);
		Widgets.Label(row.LeftPart(0.52f), $"{label}: {value:0.00}x");
		value = Widgets.HorizontalSlider(row.RightPart(0.46f).ContractedBy(0f, 4f), value, 0f, MAXIMUM_FACTOR, roundTo: FACTOR_STEP);
	}
	
	private static void DrawOffsetSlider(Listing_Standard listing, string label, ref float value, float minimum, float maximum, string unit, string tooltip)
	{
		Rect row = listing.GetRect(ROW_HEIGHT);
		TooltipHandler.TipRegion(row, tooltip);
		Widgets.Label(row.LeftPart(0.52f), $"{label}: {value:+0;-0;0}{unit}");
		value = Widgets.HorizontalSlider(row.RightPart(0.46f).ContractedBy(0f, 4f), value, minimum, maximum, roundTo: 1f);
	}
	
	//// Event Wiring
	
	public override void DoWindowContents(Rect inRect)
	{
		// Draw the title and explanation
		ClimateTuning tuning = PlanetsmithWorldParameters.Pending.tuning;
		Listing_Standard listing = new();
		listing.Begin(inRect);
		Text.Font = GameFont.Medium;
		listing.Label("Planetsmith_ClimateTuningTitle".Translate());
		Text.Font = GameFont.Small;
		listing.Gap(4f);
		listing.Label("Planetsmith_ClimateTuningIntro".Translate());
		listing.GapLine(10f);
		
		// Draw a slider per dial
		DrawOffsetSlider(listing, "Planetsmith_GlobalTemperature".Translate(), ref tuning.temperatureOffset, -TEMPERATURE_OFFSET_RANGE, TEMPERATURE_OFFSET_RANGE, "Planetsmith_TemperatureUnit".Translate(), "Planetsmith_GlobalTemperatureTip".Translate());
		DrawFactorSlider(listing, "Planetsmith_ElevationCooling".Translate(), ref tuning.elevationCooling, "Planetsmith_ElevationCoolingTip".Translate());
		DrawFactorSlider(listing, "Planetsmith_RainfallScale".Translate(), ref tuning.rainfallScale, "Planetsmith_RainfallScaleTip".Translate());
		DrawFactorSlider(listing, "Planetsmith_RainShadow".Translate(), ref tuning.rainShadow, "Planetsmith_RainShadowTip".Translate());
		DrawFactorSlider(listing, "Planetsmith_MoistureReach".Translate(), ref tuning.moistureReach, "Planetsmith_MoistureReachTip".Translate());
		DrawFactorSlider(listing, "Planetsmith_MonsoonStrength".Translate(), ref tuning.monsoonStrength, "Planetsmith_MonsoonStrengthTip".Translate());
		DrawFactorSlider(listing, "Planetsmith_SeasonIntensity".Translate(), ref tuning.seasonIntensity, "Planetsmith_SeasonIntensityTip".Translate());
		DrawFactorSlider(listing, "Planetsmith_CoastalInfluence".Translate(), ref tuning.coastalInfluence, "Planetsmith_CoastalInfluenceTip".Translate());
		
		// Draw the reset button
		listing.Gap(10f);
		if (listing.ButtonText((tuning.IsDefault ? "Planetsmith_TuningAtDefaults" : "Planetsmith_ResetTuning").Translate()))
		{
			tuning.Reset();
		}
		listing.End();
	}
}
