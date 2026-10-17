// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections.Generic;
using Verse;

namespace Planetsmith;

/// <summary>
/// The generation parameters belonging to one particular planet, as opposed to the
/// mod-wide preferences that seed them. These travel with the world: they are chosen
/// on the world creation page, used while generating, and saved into the game.
/// </summary>
public class PlanetsmithWorldSettings : IExposable
{
	//// References and State
	
	public bool isSeaLevelControlEnabled;
	public float targetLandFraction;
	public float axialTilt;
	
	/// <summary>Dials on the climate model for this planet.</summary>
	public ClimateTuning tuning = new();
	
	/// <summary>
	/// Per-biome adjustments, keyed by defName. Only biomes the player has actually
	/// touched are stored; everything absent behaves as its worker intends.
	/// </summary>
	private Dictionary<string, BiomeSettings> biomes = new();
	
	/// <summary>
	/// Whether a raised score offset only helps a biome on ground its own worker accepts.
	/// Off by default, so an offset can lift a biome onto ground the worker refuses
	/// outright, which is how every offset has always worked and what makes a single
	/// biome planet possible.
	/// </summary>
	public bool shouldLimitOffsetsToSuitableGround;
	
	/// <summary>True once the player has moved any biome away from how its worker scores it.</summary>
	public bool HasAdjustedBiomes
	{
		get
		{
			foreach (BiomeSettings settings in biomes.Values)
			{
				if (!settings.IsDefault)
				{
					return true;
				}
			}
			return false;
		}
	}
	
	//// Public API
	
	public PlanetsmithWorldSettings()
	{
		CopyFrom(PlanetsmithMod.Settings);
	}
	
	public void CopyFrom(PlanetsmithSettings? defaults)
	{
		// Fall back to the built-in defaults before the mod has loaded its own
		if (defaults == null)
		{
			isSeaLevelControlEnabled = false;
			targetLandFraction = PlanetsmithSettings.DEFAULT_TARGET_LAND_FRACTION;
			axialTilt = PlanetsmithSettings.DEFAULT_AXIAL_TILT;
			return;
		}
		
		// Copy the mod-wide defaults
		isSeaLevelControlEnabled = defaults.isSeaLevelControlEnabled;
		targetLandFraction = defaults.targetLandFraction;
		axialTilt = defaults.axialTilt;
	}
	
	/// <summary>Settings for a biome, creating a default entry the first time it is asked for.</summary>
	public BiomeSettings ForBiome(string defName)
	{
		// Reuse the existing entry
		if (biomes.TryGetValue(defName, out BiomeSettings settings))
		{
			return settings;
		}
		
		// Create a default one
		settings = new BiomeSettings();
		biomes[defName] = settings;
		return settings;
	}
	
	/// <summary>Settings for a biome, or null when it has never been adjusted.</summary>
	public BiomeSettings? ForBiomeOrNull(string defName)
	{
		return biomes.TryGetValue(defName, out BiomeSettings? settings) ? settings : null;
	}
	
	public void ResetAllBiomes()
	{
		biomes.Clear();
		shouldLimitOffsetsToSuitableGround = false;
	}
	
	public PlanetsmithWorldSettings Clone()
	{
		// Copy the planet's own values
		PlanetsmithWorldSettings clone = new()
		{
			isSeaLevelControlEnabled = isSeaLevelControlEnabled,
			targetLandFraction = targetLandFraction,
			axialTilt = axialTilt,
			tuning = tuning.Clone(),
			shouldLimitOffsetsToSuitableGround = shouldLimitOffsetsToSuitableGround,
		};
		
		// Copy each biome's adjustments
		foreach (KeyValuePair<string, BiomeSettings> pair in biomes)
		{
			clone.biomes[pair.Key] = pair.Value.Clone();
		}
		return clone;
	}
	
	//// Event Wiring
	
	public void ExposeData()
	{
		// Save or load the planet's own values
		// The sea level label keeps the field's old name, so planets and presets saved
		// before the rename still load.
		Scribe_Values.Look(ref isSeaLevelControlEnabled, "enableSeaLevelControl", defaultValue: false);
		Scribe_Values.Look(ref targetLandFraction, "targetLandFraction", PlanetsmithSettings.DEFAULT_TARGET_LAND_FRACTION);
		Scribe_Values.Look(ref axialTilt, "axialTilt", PlanetsmithSettings.DEFAULT_AXIAL_TILT);
		
		// Save or load the climate tuning, filling it in for saves that have none
		Scribe_Deep.Look(ref tuning, "tuning");
		if (Scribe.mode == LoadSaveMode.PostLoadInit && tuning == null)
		{
			tuning = new ClimateTuning();
		}
		
		// Save or load the biome adjustments, filling them in for saves that have none
		Scribe_Collections.Look(ref biomes, "biomes", LookMode.Value, LookMode.Deep);
		if (Scribe.mode == LoadSaveMode.PostLoadInit && biomes == null)
		{
			biomes = new Dictionary<string, BiomeSettings>();
		}
		
		// Save or load the offset limit
		// Older planets and presets have no entry, and read as the limit being off, which is
		// how they were made.
		Scribe_Values.Look(ref shouldLimitOffsetsToSuitableGround, "limitOffsetsToSuitableGround", defaultValue: false);
	}
}
