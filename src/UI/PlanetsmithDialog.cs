// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using Verse;

namespace Planetsmith.UI;

/// <summary>
/// What every Planetsmith dialog shares: it pauses the game, takes the input around
/// it, and closes from its corner or a click outside.
/// </summary>
public abstract class PlanetsmithDialog : Window
{
	//// Public API
	
	protected PlanetsmithDialog()
	{
		forcePause = true;
		absorbInputAroundWindow = true;
		doCloseX = true;
		closeOnClickedOutside = true;
	}
}
