// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using Verse;

namespace Planetsmith;

/// <summary>
/// Holds the parameters the player is currently editing on the world creation page.
/// Generation reads these, and the world component copies them so they persist with
/// the finished planet.
/// </summary>
public static class PlanetsmithWorldParameters
{
	//// References and State
	
	private static PlanetsmithWorldSettings? pending;
	
	/// <summary>Parameters for the world about to be created; seeded from the mod defaults.</summary>
	public static PlanetsmithWorldSettings Pending
	{
		get => pending ??= new PlanetsmithWorldSettings();
		set => pending = value;
	}
	
	/// <summary>
	/// The parameters generation should obey: the current world's own settings when
	/// it has them, otherwise whatever is pending for the world being made.
	/// </summary>
	public static PlanetsmithWorldSettings Active => Find.World?.GetComponent<PlanetsmithWorldComponent>()?.Settings ?? Pending;
	
	//// Public API
	
	/// <summary>Re-seed the pending parameters from the mod-wide defaults.</summary>
	public static void ResetToDefaults()
	{
		pending = new PlanetsmithWorldSettings();
	}
}
