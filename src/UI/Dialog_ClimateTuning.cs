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
		listing.Label("Planetsmith: climate tuning");
		Text.Font = GameFont.Small;
		listing.Gap(4f);
		listing.Label("These adjust the simulation for the world you are about to generate. Left at their defaults they change nothing.");
		listing.GapLine(10f);
		
		// Draw a slider per dial
		DrawOffsetSlider(listing, "Global temperature", ref tuning.temperatureOffset, -TEMPERATURE_OFFSET_RANGE, TEMPERATURE_OFFSET_RANGE, "C", "Warms or cools the whole planet before anything else is worked out.");
		DrawFactorSlider(listing, "Cooling with altitude", ref tuning.elevationCooling, "How much colder high ground becomes. Higher values put snow on smaller mountains.");
		DrawFactorSlider(listing, "Overall rainfall", ref tuning.rainfallScale, "Scales the rain falling everywhere, before mountains, coasts and monsoons have their say.");
		DrawFactorSlider(listing, "Rain shadows", ref tuning.rainShadow, "How thoroughly mountains wring out the air crossing them. Air stripped harder stays dry further inland, so this sets how far a shadow reaches as well as how dry it is.");
		DrawFactorSlider(listing, "Moisture reach inland", ref tuning.moistureReach, "How far sea air carries its water before running dry. Higher values green the interiors.");
		DrawFactorSlider(listing, "Monsoon strength", ref tuning.monsoonStrength, "How heavy the seasonal rains are on tropical coasts.");
		DrawFactorSlider(listing, "Season intensity", ref tuning.seasonIntensity, "How far temperatures swing between summer and winter, which decides where frost-shy biomes can live.");
		DrawFactorSlider(listing, "Coastal influence", ref tuning.coastalInfluence, "How strongly the sea tempers the coasts beside it, mild where the wind comes off the water and cold and dry where it does not.");
		
		// Draw the reset button
		listing.Gap(10f);
		if (listing.ButtonText(tuning.IsDefault ? "Defaults" : "Reset to defaults"))
		{
			tuning.Reset();
		}
		listing.End();
	}
}
