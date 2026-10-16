// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Planetsmith.UI;

namespace Planetsmith.Patches;

/// <summary>
/// Adds a Planetsmith button to the world creation page, opening the per-world
/// parameters dialog.
///
/// It goes on the bottom button row rather than into the page's two-column body,
/// which is laid out at fixed offsets and which other mods rewrite wholesale. The
/// row is already crowded: vanilla puts Back hard left, Generate hard right, and
/// centres Reset all and Reset factions around the middle. That leaves one gap,
/// between Back and Reset all, so the button is sized like a standard one and
/// tucked into it.
/// </summary>
[HarmonyPatch(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.DoWindowContents))]
public static class Patch_Page_CreateWorldParams
{
	//// Constants
	
	// Matches Page.BottomButSize and the page's own spacing, which are protected.
	private const float BUTTON_WIDTH = 150f;
	private const float BUTTON_HEIGHT = 38f;
	private const float BUTTON_GAP = 8.5f;
	
	//// Event Wiring
	
	[HarmonyPostfix]
	public static void OnWindowContentsDrawn(Rect rect)
	{
		Rect buttonRect = new(rect.x + BUTTON_WIDTH + BUTTON_GAP, rect.y + rect.height - BUTTON_HEIGHT, BUTTON_WIDTH, BUTTON_HEIGHT);
		if (Widgets.ButtonText(buttonRect, "Planetsmith..."))
		{
			Find.WindowStack.Add(new Dialog_PlanetsmithWorldParameters());
		}
	}
}
