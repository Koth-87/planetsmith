// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using Verse;

namespace Planetsmith;

/// <summary>
/// A player's adjustments to one biome on one planet: whether it may appear at all,
/// how strongly it competes for ground it already suits, and a flat nudge to its
/// score that can push it into or out of places it would otherwise narrowly lose or
/// win. Defaults leave the biome exactly as its own worker scores it.
/// </summary>
public class BiomeSettings : IExposable
{
	//// Constants
	
	public const float MAXIMUM_COMMONALITY = 5f;
	public const float MAXIMUM_SCORE_OFFSET = 50f;
	
	//// References and State
	
	public bool isEnabled = true;
	public float commonality = 1f;
	public float scoreOffset = 0f;
	
	public bool IsDefault => isEnabled && commonality == 1f && scoreOffset == 0f;
	
	//// Public API
	
	public BiomeSettings Clone()
	{
		return new BiomeSettings
		{
			isEnabled = isEnabled,
			commonality = commonality,
			scoreOffset = scoreOffset,
		};
	}
	
	public void Reset()
	{
		isEnabled = true;
		commonality = 1f;
		scoreOffset = 0f;
	}
	
	//// Event Wiring
	
	public void ExposeData()
	{
		// The label keeps the field's old name, so planets and presets saved before the
		// rename still load.
		Scribe_Values.Look(ref isEnabled, "enabled", defaultValue: true);
		Scribe_Values.Look(ref commonality, "commonality", 1f);
		Scribe_Values.Look(ref scoreOffset, "scoreOffset", 0f);
	}
}
