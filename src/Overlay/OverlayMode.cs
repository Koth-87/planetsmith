// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

namespace Planetsmith.Overlay;

/// <summary>The climate layers the planet can be shaded by.</summary>
public enum OverlayMode
{
	None,
	Temperature,
	Rainfall,
	Swampiness,
	Continentality,
	WinterTemperature,
	OceanCurrents,
	Aridity,
	Monsoon,
	Drainage,
	RainShadow,
}
