// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Planetsmith.UI;

/// <summary>
/// Saves the current planet parameters under a name, and brings saved ones back.
/// Presets live outside any world, so a favourite set of settings can be reused for
/// every planet you make afterwards.
/// </summary>
public class Dialog_PlanetsmithPresets : PlanetsmithDialog
{
	//// Constants
	
	private const float ROW_HEIGHT = 34f;
	
	//// References and State
	
	private string nameBuffer = string.Empty;
	private Vector2 scrollPosition;
	private List<string> presets = new();
	
	public override Vector2 InitialSize => new(560f, 520f);
	
	//// Private Functions
	
	private void RefreshPresets()
	{
		presets = PlanetsmithPresets.AllNames();
	}
	
	private void SavePreset(string name)
	{
		// Sanity check
		if (!PlanetsmithPresets.Save(name, PlanetsmithWorldParameters.Pending))
		{
			return;
		}
		
		// Confirm and clear the name
		Messages.Message($"Saved preset '{name}'.", MessageTypeDefOf.TaskCompletion, historical: false);
		nameBuffer = string.Empty;
		RefreshPresets();
	}
	
	private static void LoadPreset(string name)
	{
		// Sanity check
		PlanetsmithWorldSettings? loaded = PlanetsmithPresets.Load(name);
		if (loaded == null)
		{
			Messages.Message($"Could not load preset '{name}'.", MessageTypeDefOf.RejectInput, historical: false);
			return;
		}
		
		// Make it the planet being edited
		PlanetsmithWorldParameters.Pending = loaded;
		Messages.Message($"Loaded preset '{name}'.", MessageTypeDefOf.TaskCompletion, historical: false);
	}
	
	/// <summary>Asks before deleting a preset, since there is no undo.</summary>
	private void ConfirmDelete(string name)
	{
		void OnDeleteConfirmed()
		{
			PlanetsmithPresets.Delete(name);
			RefreshPresets();
		}
		
		Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation($"Delete the preset '{name}'?", OnDeleteConfirmed, destructive: true));
	}
	
	//// Event Wiring
	
	public override void PreOpen()
	{
		base.PreOpen();
		RefreshPresets();
	}
	
	public override void DoWindowContents(Rect inRect)
	{
		// Draw the title
		float y = inRect.y;
		Text.Font = GameFont.Medium;
		Widgets.Label(new Rect(inRect.x, y, inRect.width, 36f), "Planetsmith: presets");
		Text.Font = GameFont.Small;
		y += 44f;
		
		// Draw the save row, dimmed until there is a name to save under
		Rect fieldRect = new(inRect.x, y, inRect.width - 130f, 30f);
		nameBuffer = Widgets.TextField(fieldRect, nameBuffer);
		Rect saveRect = new(fieldRect.xMax + 10f, y, 120f, 30f);
		string trimmed = PlanetsmithPresets.SanitizeName(nameBuffer);
		bool canSave = !trimmed.NullOrEmpty();
		if (!canSave)
		{
			GUI.color = new Color(1f, 1f, 1f, 0.4f);
		}
		if (Widgets.ButtonText(saveRect, PlanetsmithPresets.Exists(trimmed) ? "Overwrite" : "Save") && canSave)
		{
			SavePreset(trimmed);
		}
		GUI.color = Color.white;
		y += 40f;
		Widgets.DrawLineHorizontal(inRect.x, y, inRect.width);
		y += 8f;
		
		// Say so when there is nothing saved
		if (presets.Count == 0)
		{
			GUI.color = new Color(1f, 1f, 1f, 0.5f);
			Widgets.Label(new Rect(inRect.x, y, inRect.width, 30f), "No presets saved yet.");
			GUI.color = Color.white;
			return;
		}
		
		// Draw a row per preset
		Rect outRect = new(inRect.x, y, inRect.width, inRect.height - y - 8f);
		Rect viewRect = new(0f, 0f, outRect.width - 20f, presets.Count * ROW_HEIGHT);
		Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
		float rowY = 0f;
		string? toDelete = null;
		foreach (string preset in presets)
		{
			Rect rowRect = new(0f, rowY, viewRect.width, ROW_HEIGHT);
			if (Mouse.IsOver(rowRect))
			{
				Widgets.DrawHighlight(rowRect);
			}
			Widgets.Label(new Rect(rowRect.x, rowRect.y + 5f, rowRect.width - 190f, 26f), preset);
			if (Widgets.ButtonText(new Rect(rowRect.xMax - 185f, rowRect.y + 2f, 90f, 28f), "Load"))
			{
				LoadPreset(preset);
			}
			if (Widgets.ButtonText(new Rect(rowRect.xMax - 90f, rowRect.y + 2f, 90f, 28f), "Delete"))
			{
				toDelete = preset;
			}
			rowY += ROW_HEIGHT;
		}
		Widgets.EndScrollView();
		
		// Ask about a deletion once the list is drawn
		if (toDelete != null)
		{
			ConfirmDelete(toDelete);
		}
	}
}
