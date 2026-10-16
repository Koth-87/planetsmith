// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using UnityEngine;

namespace Planetsmith.Generation;

/// <summary>Small curves the climate passes share.</summary>
public static class ClimateMath
{
	//// Public API
	
	/// <summary>A bell curve: 1 where the value sits on the mean, falling to 1/e a spread away.</summary>
	public static float Gaussian(float value, float mean, float spread)
	{
		float distance = (value - mean) / spread;
		return Mathf.Exp(-distance * distance);
	}
}
