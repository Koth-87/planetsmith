// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace Planetsmith.Compatibility;

/// <summary>
/// Soft (reflection-only) detection of other mods Planetsmith needs to cooperate
/// with. No hard assembly references: everything is probed by type name so the
/// mod loads and runs whether or not these are present.
///
/// Notes on the mods present in this environment:
///  - AlienWorlds: adds planet types with their own climate intent and patches
///    BiomeWorker.GetScore. When one of its planet types is active we defer
///    entirely so we don't fight its design; when we do run, its GetScore patch
///    still applies because our biome pass calls the workers.
///  - EarthLikePlanet: rebalances elevation and sea level during vanilla terrain
///    generation. We read the resulting elevation in a postfix, so its work flows
///    through unchanged. No action needed.
///  - Worldbuilder: its world presets can carry saved terrain. When one of those is
///    loaded the planet is meant to come back as it was stored, so we stand aside;
///    presets that only keep factions or names leave generation to us as usual.
///  - ReGrowthCore and other biome mods: their biomes are scored through their own
///    workers by our biome pass automatically.
///  - RealisticAxialTilt: models what a planet's tilt does to daylight, the sun's
///    path, plants and the seasons, in far more depth than we ever set out to. Where
///    the two meet is only the tilt itself, so we hand that over rather than run a
///    second, cruder model of the same thing beside it. See <see cref="IsAxialTiltHandledExternally"/>.
/// </summary>
public static class ModCompatibility
{
	//// Constants
	
	private const string ALIEN_WORLDS_DEFAULT_PLANET = "Default";
	
	private const BindingFlags PUBLIC_STATIC = BindingFlags.Public | BindingFlags.Static;
	private const BindingFlags PUBLIC_INSTANCE = BindingFlags.Public | BindingFlags.Instance;
	
	// Keys that hold each failed probe to one warning a session.
	private const int ALIEN_WORLDS_WARNING_KEY = 0x5701A1;
	private const int WORLDBUILDER_PRESET_WARNING_KEY = 0x5701A2;
	private const int AXIAL_TILT_WARNING_KEY = 0x5701A3;
	private const int WORLDBUILDER_TUNING_WARNING_KEY = 0x5701A4;
	
	//// References and State
	
	private static bool isInitialized;
	private static PropertyInfo? alienWorldsCurrent;
	private static PropertyInfo? worldbuilderCurrentPreset;
	private static FieldInfo? worldbuilderSaveTerrain;
	private static FieldInfo? worldbuilderGenerationData;
	private static FieldInfo? worldbuilderScoreOffsets;
	private static FieldInfo? worldbuilderCommonalities;
	private static PropertyInfo? axialTiltDegrees;
	private static PropertyInfo? axialGeometryReady;
	private static Func<float, float>? annualTemperatureCorrection;
	
	public static bool IsAlienWorldsLoaded { get; private set; }
	public static bool IsEarthLikePlanetLoaded { get; private set; }
	public static bool IsWorldbuilderLoaded { get; private set; }
	public static bool IsRealisticAxialTiltLoaded { get; private set; }
	
	//// Private Functions
	
	private static void BindAlienWorlds()
	{
		// Sanity check
		Type? planetTypeManager = GenTypes.GetTypeInAnyAssembly("AlienWorlds.PlanetTypeManager");
		if (planetTypeManager == null)
		{
			return;
		}
		IsAlienWorldsLoaded = true;
		
		// Bind the active planet type
		alienWorldsCurrent = planetTypeManager.GetProperty("Current", PUBLIC_STATIC);
	}
	
	/// <summary>Binds Worldbuilder's preset being loaded and its own per-biome settings.</summary>
	private static void BindWorldbuilder()
	{
		// Sanity check
		Type? presetManager = GenTypes.GetTypeInAnyAssembly("Worldbuilder.WorldPresetManager");
		if (presetManager == null)
		{
			return;
		}
		IsWorldbuilderLoaded = true;
		
		// Bind the preset being loaded
		worldbuilderCurrentPreset = presetManager.GetProperty("CurrentlyLoadedPreset", PUBLIC_STATIC);
		worldbuilderSaveTerrain = GenTypes.GetTypeInAnyAssembly("Worldbuilder.WorldPreset")?.GetField("saveTerrain", PUBLIC_INSTANCE);
		
		// Bind its biome settings
		worldbuilderGenerationData = GenTypes.GetTypeInAnyAssembly("Worldbuilder.World_ExposeData_Patch")?.GetField("worldGenerationData", PUBLIC_STATIC);
		Type? generationData = GenTypes.GetTypeInAnyAssembly("Worldbuilder.WorldGenerationData");
		worldbuilderScoreOffsets = generationData?.GetField("biomeScoreOffsets", PUBLIC_INSTANCE);
		worldbuilderCommonalities = generationData?.GetField("biomeCommonalities", PUBLIC_INSTANCE);
	}
	
	/// <summary>
	/// Binds the parts of Realistic Axial Tilt we need: the tilt this world was made
	/// with, and how far that tilt moves a latitude's yearly average off an Earth-like
	/// one. The tilt is published on its API; the correction is not yet, so we take the
	/// published name if it appears and otherwise reach for where it currently lives.
	/// Missing either leaves the mod's other work untouched and ours running as normal.
	/// </summary>
	private static void BindAxialTilt()
	{
		// Sanity check
		Type? api = GenTypes.GetTypeInAnyAssembly("RealisticAxialTilt.Api.RealisticAxialTiltApi");
		if (api == null)
		{
			return;
		}
		IsRealisticAxialTiltLoaded = true;
		
		// Bind the tilt
		axialTiltDegrees = api.GetProperty("AxialTiltDegrees", PUBLIC_STATIC);
		axialGeometryReady = api.GetProperty("GeometryReady", PUBLIC_STATIC);
		
		// Bind the temperature correction, wherever it lives
		// A delegate rather than repeated Invoke: this is asked once per tile, and a
		// reflective call per tile would cost more than the whole climate pass.
		Type[] latitudeParameter = { typeof(float) };
		MethodInfo? correction = api.GetMethod("AnnualTemperatureCorrectionDegrees", PUBLIC_STATIC, null, latitudeParameter, null) ?? GenTypes.GetTypeInAnyAssembly("RealisticAxialTilt.AxialAnnualTemperature")?.GetMethod("AnnualTemperatureCorrection", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, latitudeParameter, null);
		if (correction != null && Delegate.CreateDelegate(typeof(Func<float, float>), correction, false) is Func<float, float> boundCorrection)
		{
			annualTemperatureCorrection = boundCorrection;
			return;
		}
		
		// Say so when the correction could not be read
		Log.Warning("[Planetsmith] Realistic Axial Tilt is installed but its annual temperature model could not be read, so Planetsmith will keep using its own. Worth reporting: the two will disagree about what tilt does to a planet.");
	}
	
	/// <summary>A copy of one of Worldbuilder's per-biome dictionaries, or null when it has none.</summary>
	private static Dictionary<string, int>? CopyBiomeDictionary(object? value)
	{
		return value is Dictionary<string, int> source ? new Dictionary<string, int>(source) : null;
	}
	
	//// Public API
	
	public static void EnsureInitialized()
	{
		// Sanity check
		if (isInitialized)
		{
			return;
		}
		isInitialized = true;
		
		// Look for each mod
		BindAlienWorlds();
		IsEarthLikePlanetLoaded = GenTypes.GetTypeInAnyAssembly("EarthLikePlanet.EarthLikePlanetMod") != null;
		BindWorldbuilder();
		BindAxialTilt();
	}
	
	/// <summary>
	/// Worldbuilder's own per-biome frequency and score offset, if it has any.
	///
	/// It applies these by rewriting vanilla's biome selection to adjust each score as
	/// that method reads it, rather than by changing what the biome workers return.
	/// Planetsmith asks the workers itself, so none of it reaches us and every slider
	/// in that menu goes dead the moment this mod is installed. Reading the settings
	/// here and applying them ourselves is what keeps them alive: the two mods offer
	/// the same two controls, and a player has no reason to expect one menu to be the
	/// working one.
	/// </summary>
	public static bool TryGetWorldbuilderBiomeTuning(out Dictionary<string, int>? offsets, out Dictionary<string, int>? commonalities)
	{
		// Sanity check
		EnsureInitialized();
		offsets = null;
		commonalities = null;
		if (!IsWorldbuilderLoaded || worldbuilderGenerationData == null || worldbuilderScoreOffsets == null || worldbuilderCommonalities == null)
		{
			return false;
		}
		
		// Copy its settings
		// Copied rather than held. That mod fills these in as a side effect of scoring,
		// which happens on the generation thread, while its own settings screen writes to
		// them from the interface thread. Reading cannot corrupt anything, but a read
		// landing in the middle of that will throw, and a neighbour's race is no reason to
		// lose the planet: taking a copy here puts the whole exposure inside this one
		// guarded moment.
		try
		{
			object? data = worldbuilderGenerationData.GetValue(null);
			if (data == null)
			{
				return false;
			}
			offsets = CopyBiomeDictionary(worldbuilderScoreOffsets.GetValue(data));
			commonalities = CopyBiomeDictionary(worldbuilderCommonalities.GetValue(data));
			return offsets != null || commonalities != null;
		}
		catch (Exception exception)
		{
			Log.WarningOnce($"[Planetsmith] Worldbuilder biome settings could not be read: {exception.Message}", WORLDBUILDER_TUNING_WARNING_KEY);
			return false;
		}
	}
	
	/// <summary>
	/// True when Realistic Axial Tilt owns this planet's tilt, so our own slider stands
	/// down in favour of its one. Kept apart from <see cref="IsAxialTiltHandledExternally"/>
	/// because the settings screens are drawn long before any world exists to be ready.
	/// </summary>
	public static bool IsAxialTiltOwnedExternally()
	{
		EnsureInitialized();
		return IsRealisticAxialTiltLoaded && axialTiltDegrees != null && annualTemperatureCorrection != null;
	}
	
	/// <summary>
	/// True when Realistic Axial Tilt is present and ready to say what this world's
	/// tilt does to its temperatures. Planetsmith then stops applying its own version
	/// of that, since the two are the same effect and would otherwise both land.
	/// </summary>
	public static bool IsAxialTiltHandledExternally()
	{
		// Sanity check
		if (!IsAxialTiltOwnedExternally())
		{
			return false;
		}
		
		// Ask whether its geometry is ready
		// The geometry reads as an upright planet until it is seeded, which would quietly
		// cost every world its seasons. Better to use our own model than a neighbour's
		// uninitialised one.
		try
		{
			return axialGeometryReady == null || axialGeometryReady.GetValue(null) is true;
		}
		catch (Exception exception)
		{
			Log.WarningOnce($"[Planetsmith] Realistic Axial Tilt compatibility probe failed: {exception.Message}", AXIAL_TILT_WARNING_KEY);
			return false;
		}
	}
	
	/// <summary>This world's tilt in degrees as Realistic Axial Tilt understands it.</summary>
	public static float ExternalAxialTiltDegrees(float fallback)
	{
		try
		{
			return axialTiltDegrees?.GetValue(null) is float tilt ? tilt : fallback;
		}
		catch
		{
			return fallback;
		}
	}
	
	/// <summary>
	/// Degrees this latitude's yearly average sits away from where an Earth-like tilt
	/// would put it. Zero when nothing is bound, so callers need no second path.
	/// </summary>
	public static float AnnualTemperatureCorrection(float latitudeDegrees)
	{
		return annualTemperatureCorrection?.Invoke(latitudeDegrees) ?? 0f;
	}
	
	/// <summary>
	/// True when a Worldbuilder preset that carries its own saved terrain is being
	/// loaded, in which case Planetsmith leaves the planet alone.
	/// </summary>
	public static bool IsWorldbuilderTerrainPresetActive()
	{
		// Sanity check
		EnsureInitialized();
		if (!IsWorldbuilderLoaded || worldbuilderCurrentPreset == null || worldbuilderSaveTerrain == null)
		{
			return false;
		}
		
		// Ask whether the preset kept its terrain
		try
		{
			object? preset = worldbuilderCurrentPreset.GetValue(null);
			return preset != null && worldbuilderSaveTerrain.GetValue(preset) is true;
		}
		catch (Exception exception)
		{
			Log.WarningOnce($"[Planetsmith] Worldbuilder compatibility probe failed: {exception.Message}", WORLDBUILDER_PRESET_WARNING_KEY);
			return false;
		}
	}
	
	/// <summary>
	/// True when AlienWorlds is present and a non-default planet type is active,
	/// in which case Planetsmith defers its climate overhaul to AlienWorlds.
	/// </summary>
	public static bool IsAlienWorldsCustomPlanetActive()
	{
		// Sanity check
		EnsureInitialized();
		if (!IsAlienWorldsLoaded || alienWorldsCurrent == null)
		{
			return false;
		}
		
		// Ask which planet type is active
		try
		{
			return alienWorldsCurrent.GetValue(null) is string current && !string.IsNullOrEmpty(current) && current != ALIEN_WORLDS_DEFAULT_PLANET;
		}
		catch (Exception exception)
		{
			Log.WarningOnce($"[Planetsmith] AlienWorlds compatibility probe failed: {exception.Message}", ALIEN_WORLDS_WARNING_KEY);
			return false;
		}
	}
}
