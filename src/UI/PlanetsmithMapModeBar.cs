// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System;
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Planetsmith.Generation;
using Planetsmith.Overlay;

namespace Planetsmith.UI;

/// <summary>
/// A button on the planet view for switching Planetsmith's map modes, so the climate
/// behind a world can be read without turning on development mode. Sits against the
/// empty left edge of the screen, clear of the tile inspector, the page buttons and
/// the compass.
/// </summary>
public static class PlanetsmithMapModeBar
{
	//// Constants
	
	// Two lines tall, so the mode name sits under its heading rather than being
	// squeezed onto one long line beside it.
	private const float BUTTON_WIDTH = 172f;
	private const float BUTTON_HEIGHT = 52f;
	private const float EDGE_MARGIN = 10f;
	private const float TOP_OFFSET = 200f;
	
	//// Private Functions
	
	/// <summary>
	/// Every mode, listed alphabetically by the name the player sees. Sorted each time
	/// the menu opens rather than written out in order, so a mode added later cannot
	/// end up out of place and the order follows whichever language is being played.
	/// Off stays pinned at the top: it turns the shading off rather than being a view
	/// of its own, and sorting it into the middle of the list would hide it among them.
	/// </summary>
	private static List<OverlayMode> BuildModeOrder()
	{
		// Gather every mode but Off
		// Enum.GetValues predates generics and hands each value back boxed, so the loop
		// unboxes them as it goes.
		List<OverlayMode> modes = new();
		foreach (OverlayMode mode in Enum.GetValues(typeof(OverlayMode)))
		{
			if (mode != OverlayMode.None)
			{
				modes.Add(mode);
			}
		}
		
		// Sort them by name, with Off on top
		modes.Sort(CompareByLabel);
		modes.Insert(0, OverlayMode.None);
		return modes;
	}
	
	private static int CompareByLabel(OverlayMode first, OverlayMode second)
	{
		return string.Compare(PlanetsmithOverlay.Label(first), PlanetsmithOverlay.Label(second), StringComparison.CurrentCultureIgnoreCase);
	}
	
	private static List<FloatMenuOption> BuildMenuOptions()
	{
		List<FloatMenuOption> options = new();
		bool hasData = PlanetsmithClimateCache.IsValid;
		foreach (OverlayMode mode in BuildModeOrder())
		{
			// These layers are worked out while a world is generated and are not kept in
			// the save, so after a reload there is nothing to show.
			string label = PlanetsmithOverlay.Label(mode);
			if (PlanetsmithOverlay.IsBuiltDuringGeneration(mode) && !hasData)
			{
				options.Add(new FloatMenuOption("Planetsmith_MapModeNeedsRegeneration".Translate(label), null));
				continue;
			}
			options.Add(CreateModeOption(mode, label));
		}
		return options;
	}
	
	/// <summary>A menu entry that switches the planet to one map mode.</summary>
	private static FloatMenuOption CreateModeOption(OverlayMode mode, string label)
	{
		void OnModeChosen()
		{
			PlanetsmithOverlay.SetMode(mode);
		}
		
		return new FloatMenuOption(label, OnModeChosen);
	}
	
	//// Event Wiring
	
	public static void OnGUI()
	{
		// Sanity check
		PlanetsmithSettings? settings = PlanetsmithMod.Settings;
		if (settings == null || !settings.shouldShowMapModeButton)
		{
			return;
		}
		if (!WorldRendererUtility.WorldRendered)
		{
			return;
		}
		
		// Drop a mode the loaded world has no data for
		// Loading a save leaves a generated layer selected but with nothing behind it.
		// Fall back rather than naming a view that cannot be drawn.
		OverlayMode current = PlanetsmithOverlay.Mode;
		if (PlanetsmithOverlay.IsBuiltDuringGeneration(current) && !PlanetsmithClimateCache.IsValid)
		{
			PlanetsmithOverlay.SetMode(OverlayMode.None);
			current = OverlayMode.None;
		}
		
		// Draw the button
		string label = current == OverlayMode.None ? "Planetsmith_MapModesButton".Translate() : "Planetsmith_MapModeButton".Translate(PlanetsmithOverlay.Label(current));
		Rect rect = new(EDGE_MARGIN, TOP_OFFSET, BUTTON_WIDTH, BUTTON_HEIGHT);
		if (Widgets.ButtonText(rect, label))
		{
			Find.WindowStack.Add(new FloatMenu(BuildMenuOptions()));
		}
		TooltipHandler.TipRegion(rect, "Planetsmith_MapModesTip".Translate());
	}
}
