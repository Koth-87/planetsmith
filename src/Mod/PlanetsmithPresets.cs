// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Verse;

namespace Planetsmith;

/// <summary>
/// Named sets of planet parameters, kept as individual files beside RimWorld's own
/// saves so they outlive any one world and can be shared. A preset stores everything
/// the world creation dialog can change, biome adjustments included.
/// </summary>
public static class PlanetsmithPresets
{
	//// Constants
	
	private const string FILE_EXTENSION = ".xml";
	private const string DOCUMENT_ROOT = "planetsmithPreset";
	private const string SETTINGS_LABEL = "settings";
	
	//// Private Functions
	
	private static string PathFor(string name)
	{
		return Path.Combine(FolderPath, SanitizeName(name) + FILE_EXTENSION);
	}
	
	//// Public API
	
	/// <summary>The folder presets are kept in, created on first use.</summary>
	public static string FolderPath
	{
		get
		{
			string path = Path.Combine(GenFilePaths.SaveDataFolderPath, "Planetsmith", "Presets");
			Directory.CreateDirectory(path);
			return path;
		}
	}
	
	/// <summary>Preset names, alphabetically. Empty when none have been saved.</summary>
	public static List<string> AllNames()
	{
		try
		{
			List<string> names = new();
			foreach (FileInfo file in new DirectoryInfo(FolderPath).GetFiles("*" + FILE_EXTENSION))
			{
				names.Add(Path.GetFileNameWithoutExtension(file.Name));
			}
			names.Sort();
			return names;
		}
		catch (Exception exception)
		{
			Log.Error($"[Planetsmith] Could not list presets: {exception}");
			return new List<string>();
		}
	}
	
	public static bool Exists(string name)
	{
		return File.Exists(PathFor(name));
	}
	
	public static bool Save(string name, PlanetsmithWorldSettings settings)
	{
		// Sanity check
		if (name.NullOrEmpty())
		{
			return false;
		}
		
		// Write a copy to the preset's file
		try
		{
			PlanetsmithWorldSettings copy = settings.Clone();
			Scribe.saver.InitSaving(PathFor(name), DOCUMENT_ROOT);
			try
			{
				Scribe_Deep.Look(ref copy, SETTINGS_LABEL);
			}
			finally
			{
				Scribe.saver.FinalizeSaving();
			}
			return true;
		}
		catch (Exception exception)
		{
			Log.Error($"[Planetsmith] Could not save preset '{name}': {exception}");
			Scribe.ForceStop();
			return false;
		}
	}
	
	/// <summary>The preset saved under a name, or null when there is none or it cannot be read.</summary>
	public static PlanetsmithWorldSettings? Load(string name)
	{
		// Sanity check
		string path = PathFor(name);
		if (!File.Exists(path))
		{
			return null;
		}
		
		// Read the preset's file
		PlanetsmithWorldSettings? loaded = null;
		try
		{
			Scribe.loader.InitLoading(path);
			try
			{
				Scribe_Deep.Look(ref loaded, SETTINGS_LABEL);
			}
			finally
			{
				Scribe.loader.FinalizeLoading();
			}
		}
		catch (Exception exception)
		{
			Log.Error($"[Planetsmith] Could not load preset '{name}': {exception}");
			Scribe.ForceStop();
			return null;
		}
		return loaded;
	}
	
	public static bool Delete(string name)
	{
		try
		{
			string path = PathFor(name);
			if (!File.Exists(path))
			{
				return false;
			}
			File.Delete(path);
			return true;
		}
		catch (Exception exception)
		{
			Log.Error($"[Planetsmith] Could not delete preset '{name}': {exception}");
			return false;
		}
	}
	
	/// <summary>Strips anything the filesystem would reject, so any typed name is usable.</summary>
	public static string SanitizeName(string name)
	{
		// Sanity check
		if (name.NullOrEmpty())
		{
			return string.Empty;
		}
		
		// Keep only the characters a file name can hold
		char[] invalid = Path.GetInvalidFileNameChars();
		StringBuilder sanitized = new(name.Length);
		foreach (char character in name)
		{
			if (Array.IndexOf(invalid, character) < 0)
			{
				sanitized.Append(character);
			}
		}
		return sanitized.ToString().Trim();
	}
}
