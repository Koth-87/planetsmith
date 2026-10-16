// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using HarmonyLib;
using RimWorld;

namespace Planetsmith.Patches;

/// <summary>
/// Each time the creation page opens, start from the mod-wide defaults so a fresh
/// planet is not silently shaped by whatever the previous one used.
/// </summary>
[HarmonyPatch(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.PreOpen))]
public static class Patch_Page_CreateWorldParams_PreOpen
{
	//// Event Wiring
	
	[HarmonyPostfix]
	public static void OnPreOpen()
	{
		PlanetsmithWorldParameters.ResetToDefaults();
	}
}
