// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Planetsmith.Generation;

namespace Planetsmith.UI;

/// <summary>
/// Per-biome controls for the planet being generated: whether a biome may appear at
/// all, how much ground it takes where it already suits the climate, and a flat nudge
/// to its score. Lists every biome that generates naturally, including modded ones,
/// since they are read straight from the def database.
/// </summary>
public class Dialog_BiomeSettings : PlanetsmithDialog
{
	//// Constants
	
	private const float ROW_HEIGHT = 34f;
	private const float NAME_WIDTH = 240f;
	private const float VALUE_WIDTH = 60f;
	private const float SLIDER_WIDTH = 190f;
	private const float COLUMN_GAP = 12f;
	private const float BOTTOM_BAR_HEIGHT = 40f;
	
	//// References and State
	
	private Vector2 scrollPosition;
	private List<BiomeDef> biomes = new();
	
	public override Vector2 InitialSize => new(900f, 760f);
	
	//// Private Functions
	
	/// <summary>
	/// Whether a biome belongs in the list. Sea, lake and ice-sheet water are left out:
	/// those tiles have no dry-land alternative to fall back on, so switching one off
	/// could only produce a planet that fails to draw. Ice sheet itself stays, being dry
	/// land, and a world without polar caps is a perfectly sensible thing to ask for.
	/// </summary>
	private static bool IsListed(BiomeDef biome)
	{
		return biome.implemented && biome.generatesNaturally && !BiomeProfiler.IsWaterBiome(biome);
	}
	
	private static string SortName(BiomeDef biome)
	{
		return biome.label ?? biome.defName;
	}
	
	private static void DrawHeader(Rect rect)
	{
		GUI.color = new Color(1f, 1f, 1f, 0.55f);
		float x = rect.x + NAME_WIDTH + COLUMN_GAP;
		Widgets.Label(new Rect(x, rect.y, VALUE_WIDTH + SLIDER_WIDTH, rect.height), "Planetsmith_BiomeFrequency".Translate());
		x += VALUE_WIDTH + SLIDER_WIDTH + COLUMN_GAP;
		Widgets.Label(new Rect(x, rect.y, VALUE_WIDTH + SLIDER_WIDTH, rect.height), "Planetsmith_BiomeScoreOffset".Translate());
		GUI.color = Color.white;
	}
	
	private static void DrawBiomeRow(Rect rect, BiomeDef biome, PlanetsmithWorldSettings world)
	{
		// Highlight the row under the mouse
		if (Mouse.IsOver(rect))
		{
			Widgets.DrawHighlight(rect);
		}
		
		// Read the biome's current settings
		BiomeSettings? existing = world.ForBiomeOrNull(biome.defName);
		bool isEnabled = existing?.isEnabled ?? true;
		float commonality = existing?.commonality ?? 1f;
		float offset = existing?.scoreOffset ?? 0f;
		
		// Draw the switch and the name
		Rect checkboxRect = new(rect.x, rect.y + 4f, 24f, 24f);
		bool isEnabledBefore = isEnabled;
		Widgets.Checkbox(checkboxRect.position, ref isEnabled, 24f);
		Rect labelRect = new(rect.x + 30f, rect.y + 4f, NAME_WIDTH - 30f, 26f);
		Widgets.Label(labelRect, biome.LabelCap);
		if (!biome.description.NullOrEmpty())
		{
			TooltipHandler.TipRegion(labelRect, biome.description);
		}
		
		// Draw the frequency slider
		float x = rect.x + NAME_WIDTH + COLUMN_GAP;
		Widgets.Label(new Rect(x, rect.y + 4f, VALUE_WIDTH, 26f), commonality.ToString("0.0") + "x");
		float newCommonality = Widgets.HorizontalSlider(new Rect(x + VALUE_WIDTH, rect.y + 8f, SLIDER_WIDTH, 20f), commonality, 0f, BiomeSettings.MAXIMUM_COMMONALITY, roundTo: 0.1f);
		
		// Draw the offset slider
		x += VALUE_WIDTH + SLIDER_WIDTH + COLUMN_GAP;
		Widgets.Label(new Rect(x, rect.y + 4f, VALUE_WIDTH, 26f), offset.ToString("+0;-0;0"));
		float newOffset = Widgets.HorizontalSlider(new Rect(x + VALUE_WIDTH, rect.y + 8f, SLIDER_WIDTH, 20f), offset, -BiomeSettings.MAXIMUM_SCORE_OFFSET, BiomeSettings.MAXIMUM_SCORE_OFFSET, roundTo: 1f);
		
		// Store what changed
		// Only materialise an entry once the player actually changes something, so a
		// planet's saved settings stay limited to what was deliberately adjusted.
		if (isEnabled != isEnabledBefore || newCommonality != commonality || newOffset != offset)
		{
			BiomeSettings settings = world.ForBiome(biome.defName);
			settings.isEnabled = isEnabled;
			settings.commonality = newCommonality;
			settings.scoreOffset = newOffset;
		}
	}
	
	//// Event Wiring
	
	public override void PreOpen()
	{
		base.PreOpen();
		biomes = DefDatabase<BiomeDef>.AllDefsListForReading.Where(IsListed).OrderBy(SortName).ToList();
	}
	
	public override void DoWindowContents(Rect inRect)
	{
		// Draw the title and explanation
		PlanetsmithWorldSettings world = PlanetsmithWorldParameters.Pending;
		float y = inRect.y;
		Text.Font = GameFont.Medium;
		Widgets.Label(new Rect(inRect.x, y, inRect.width, 36f), "Planetsmith_BiomesTitle".Translate());
		Text.Font = GameFont.Small;
		y += 40f;
		// Two lines tall: this sentence wraps at the dialog's width.
		Widgets.Label(new Rect(inRect.x, y, inRect.width, 52f), "Planetsmith_BiomesIntro".Translate());
		y += 62f;
		
		// Draw the column headings
		DrawHeader(new Rect(inRect.x, y, inRect.width, 24f));
		y += 26f;
		
		// Draw a row per biome
		Rect outRect = new(inRect.x, y, inRect.width, inRect.height - y - BOTTOM_BAR_HEIGHT);
		Rect viewRect = new(0f, 0f, outRect.width - 20f, biomes.Count * ROW_HEIGHT);
		Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
		float rowY = 0f;
		foreach (BiomeDef biome in biomes)
		{
			DrawBiomeRow(new Rect(0f, rowY, viewRect.width, ROW_HEIGHT), biome, world);
			rowY += ROW_HEIGHT;
		}
		Widgets.EndScrollView();
		
		// Draw the reset button
		Rect resetRect = new(inRect.x, inRect.yMax - 32f, 200f, 30f);
		if (Widgets.ButtonText(resetRect, "Planetsmith_ResetAllBiomes".Translate()))
		{
			world.ResetAllBiomes();
		}
	}
}
