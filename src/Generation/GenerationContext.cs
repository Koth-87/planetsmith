// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Noise;
using Planetsmith.Compatibility;

namespace Planetsmith.Generation;

/// <summary>
/// Shared state threaded through the generation passes for a single layer.
/// Baselines are derived once, up front, from the world's overall temperature
/// and rainfall settings so every pass sees the player's chosen climate.
/// </summary>
public sealed class GenerationContext
{
	//// Constants
	
	/// <summary>Earth's axial tilt in degrees, which the climate model is calibrated against.</summary>
	public const float EARTH_AXIAL_TILT = 23.4f;
	
	// The world's temperature and rainfall settings are both seven-step enums, and the
	// model is anchored to an Earth-like planet at the middle step, Normal.
	private const int NORMAL_SETTING_INDEX = 3;
	private const int HIGHEST_SETTING_INDEX = 6;
	
	// Mean annual temperatures at Normal, and how far each step either side moves them,
	// in degrees.
	private const float EQUATOR_MEAN_TEMPERATURE = 30f;
	private const float EQUATOR_TEMPERATURE_STEP = 7f;
	private const float POLE_MEAN_TEMPERATURE = -37f;
	private const float POLE_TEMPERATURE_STEP = 10f;
	
	// Degrees the poles warm for each degree of tilt beyond Earth's.
	private const float POLE_WARMING_PER_TILT_DEGREE = 0.6f;
	
	// Seasonal extremes a tile starts with before the seasonality pass reaches it.
	private const float UNSET_SEASONAL_TEMPERATURE = 999f;
	
	// Continent-scale coherent noise. Low frequency means large, gentle patches.
	private const double TEMPERATURE_NOISE_FREQUENCY = 0.022;
	private const double RAINFALL_NOISE_FREQUENCY = 0.028;
	private const double NOISE_LACUNARITY = 2.0;
	private const double NOISE_PERSISTENCE = 0.5;
	private const int NOISE_OCTAVES = 3;
	private const int RAINFALL_NOISE_SEED_SALT = 0x51EED;
	
	//// References and State
	
	public readonly PlanetLayer Layer;
	public readonly int TileCount;
	public readonly int Seed;
	
	/// <summary>Mean annual temperature (deg C) at the equator, from the world temp setting.</summary>
	public readonly float EquatorMeanTemperature;
	
	/// <summary>Mean annual temperature (deg C) at the poles, from the world temp setting.</summary>
	public readonly float PoleMeanTemperature;
	
	/// <summary>Global rainfall scale from the world rainfall setting (1.0 = Normal).</summary>
	public readonly float RainfallMultiplier;
	
	/// <summary>Per-tile continentality (0 = maritime, 1 = deep interior). Filled by ContinentalityPass.</summary>
	public readonly float[] Continentality;
	
	/// <summary>Per-tile distance in tiles to the nearest ocean (0 for ocean itself). Filled by ContinentalityPass.</summary>
	public readonly int[] CoastDistance;
	
	/// <summary>
	/// Per-tile share of ocean-sourced moisture still carried by the wind (1 at sea,
	/// falling with each land tile crossed). A high value means the air reaching this
	/// tile came off the water recently, so it doubles as a smooth, direction-aware
	/// measure of onshore versus offshore wind. Filled by MoistureAdvectionPass.
	/// </summary>
	public readonly float[] OceanicMoisture;
	
	/// <summary>
	/// Per-tile share of the landscape's runoff passing through, on a logarithmic
	/// scale: near 0 on ridges, near 1 where a major river would run. Filled by
	/// DrainagePass.
	/// </summary>
	public readonly float[] FlowAccumulation;
	
	/// <summary>
	/// Per-tile count of land tiles whose runoff passes through, the tile itself
	/// included: 1 on a ridge, thousands along a major river. Filled by DrainagePass.
	/// </summary>
	public readonly int[] UpstreamTiles;
	
	/// <summary>
	/// Per-tile share of the moisture the arriving air has already left on high
	/// ground upwind: 0 where nothing has been taken from it, 1 where it has been
	/// wrung out entirely. Filled by OrographyPass.
	/// </summary>
	public readonly float[] RainShadow;
	
	/// <summary>
	/// Per-tile monsoon strength, 0 where the seasonal rains never reach and 1 where
	/// they are at their fiercest. Filled by MonsoonPass.
	/// </summary>
	public readonly float[] MonsoonStrength;
	
	/// <summary>
	/// Per-tile effective moisture: rainfall divided by how much this climate could
	/// evaporate. Below 0.2 is arid, 0.65 and above humid. Filled by AridityPass.
	/// </summary>
	public readonly float[] AridityIndex;
	
	/// <summary>
	/// Signed strength of the coastal ocean-current effect: positive where onshore
	/// wind brings mild damp maritime air, negative over cold subtropical upwelling,
	/// zero where no current anomaly applies. Filled by OceanCurrentPass.
	/// </summary>
	public readonly float[] CoastalAnomaly;
	
	/// <summary>Per-tile coldest-of-year temperature (deg C). Filled by SeasonalityPass.</summary>
	public readonly float[] WinterMinimumTemperature;
	
	/// <summary>Per-tile warmest-of-year temperature (deg C). Filled by SeasonalityPass.</summary>
	public readonly float[] SummerMaximumTemperature;
	
	/// <summary>Low-frequency coherent noise that breaks up the latitude bands. Seeded from the world.</summary>
	public readonly ModuleBase TemperatureNoise;
	public readonly ModuleBase RainfallNoise;
	
	/// <summary>Player adjustments to the climate model for this planet.</summary>
	public readonly ClimateTuning Tuning;
	
	/// <summary>Planet's axial tilt in degrees.</summary>
	public readonly float AxialTilt;
	
	/// <summary>Axial tilt relative to Earth's 23.4 degrees. 1 = Earth-like, 0 = no seasons.</summary>
	public readonly float TiltFactor;
	
	/// <summary>
	/// True when another mod owns what tilt does to temperature, in which case our own
	/// version of it stands down and its figures are used instead. The tilt still
	/// shapes our seasons and biomes; it is simply somebody else's number now.
	/// </summary>
	public readonly bool IsTiltHandledExternally;
	
	/// <summary>
	/// Mean rainfall vanilla had given the land before Planetsmith replaced it. Kept
	/// because the game's own river step sizes rivers by the rainfall running through
	/// them, so what we hand it decides whether a planet gets rivers or trickles.
	/// </summary>
	public float VanillaRainfallMean;
	
	private UpwindGraph? upwind;
	
	/// <summary>
	/// Where the wind arrives from at every land tile. Worked out on first use and
	/// shared from there on, since more than one pass carries something downwind and
	/// walking the neighbours is the expensive part of doing so.
	/// </summary>
	public UpwindGraph Upwind => upwind ??= UpwindGraph.Build(Layer);
	
	//// Public API
	
	public GenerationContext(PlanetLayer layer)
	{
		// Size the per-tile fields
		Layer = layer;
		TileCount = layer.Tiles.Count;
		Seed = Find.World.info.Seed;
		Continentality = new float[TileCount];
		CoastDistance = new int[TileCount];
		CoastalAnomaly = new float[TileCount];
		OceanicMoisture = new float[TileCount];
		AridityIndex = new float[TileCount];
		MonsoonStrength = new float[TileCount];
		FlowAccumulation = new float[TileCount];
		UpstreamTiles = new int[TileCount];
		RainShadow = new float[TileCount];
		WinterMinimumTemperature = new float[TileCount];
		SummerMaximumTemperature = new float[TileCount];
		
		// Start every tile with no seasonal swing
		// A tile the seasonality pass never touches then cannot accidentally trip a frost
		// gate.
		for (int tileId = 0; tileId < TileCount; tileId++)
		{
			WinterMinimumTemperature[tileId] = UNSET_SEASONAL_TEMPERATURE;
			SummerMaximumTemperature[tileId] = UNSET_SEASONAL_TEMPERATURE;
		}
		
		// Take the player's tuning and the planet's tilt
		PlanetsmithWorldSettings world = PlanetsmithWorldParameters.Active;
		Tuning = world.tuning ?? new ClimateTuning();
		IsTiltHandledExternally = ModCompatibility.IsAxialTiltHandledExternally();
		AxialTilt = Mathf.Clamp(IsTiltHandledExternally ? ModCompatibility.ExternalAxialTiltDegrees(world.axialTilt) : world.axialTilt, 0f, PlanetsmithSettings.MAXIMUM_AXIAL_TILT);
		TiltFactor = AxialTilt / EARTH_AXIAL_TILT;
		
		// Anchor the temperatures to the world's temperature setting
		// The setting is an enum, and its underlying value is the step it stands for.
		// A steeper tilt swings the poles through long summers as well as long nights, so
		// over a whole year they collect more sunlight and the equator-to-pole gradient
		// flattens. No tilt leaves them permanently dark. This is a single term standing
		// in for the whole business, so where a mod works the sunlight out properly we
		// drop it and take theirs per latitude instead, rather than letting both bend the
		// same gradient.
		int temperatureIndex = Mathf.Clamp((int)Find.World.info.overallTemperature, 0, HIGHEST_SETTING_INDEX);
		EquatorMeanTemperature = EQUATOR_MEAN_TEMPERATURE + (temperatureIndex - NORMAL_SETTING_INDEX) * EQUATOR_TEMPERATURE_STEP;
		PoleMeanTemperature = POLE_MEAN_TEMPERATURE + (temperatureIndex - NORMAL_SETTING_INDEX) * POLE_TEMPERATURE_STEP + (IsTiltHandledExternally ? 0f : (AxialTilt - EARTH_AXIAL_TILT) * POLE_WARMING_PER_TILT_DEGREE);
		
		// Scale the rain by the world's rainfall setting
		// Each step is a quarter of Normal's rain, and the setting's underlying value is
		// the step it stands for.
		int rainfallIndex = Mathf.Clamp((int)Find.World.info.overallRainfall, 0, HIGHEST_SETTING_INDEX);
		RainfallMultiplier = (rainfallIndex + 1) / (NORMAL_SETTING_INDEX + 1f);
		
		// Seed the noise that dapples the bands
		// Independent seeds, so the temperature and rainfall patches don't line up.
		TemperatureNoise = new Perlin(TEMPERATURE_NOISE_FREQUENCY, NOISE_LACUNARITY, NOISE_PERSISTENCE, NOISE_OCTAVES, Seed, QualityMode.Medium);
		RainfallNoise = new Perlin(RAINFALL_NOISE_FREQUENCY, NOISE_LACUNARITY, NOISE_PERSISTENCE, NOISE_OCTAVES, Seed ^ RAINFALL_NOISE_SEED_SALT, QualityMode.Medium);
	}
}
