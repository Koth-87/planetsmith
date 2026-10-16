// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections;
using System.Collections.Generic;
using RimWorld.Planet;
using Unity.Collections;
using UnityEngine;
using Verse;

namespace Planetsmith.Overlay;

/// <summary>
/// Colours every surface tile by its generated temperature or rainfall. Registered
/// on the Surface planet layer via a Def patch and driven by <see cref="PlanetsmithOverlay"/>;
/// only draws when a mode is active. This is a debug/inspection tool, not a gameplay layer.
/// </summary>
public class WorldDrawLayer_PlanetsmithClimate : WorldDrawLayer
{
	//// Constants
	
	private const byte OVERLAY_ALPHA = 205;
	
	//// References and State
	
	private Material? material;
	
	public override bool Visible => base.Visible && PlanetsmithOverlay.Mode != OverlayMode.None;
	
	public override bool VisibleWhenLayerNotSelected => false;
	
	public override bool VisibleInBackground => false;
	
	//// Event Wiring
	
	public override IEnumerable Regenerate()
	{
		// Let the base layer clear the mesh
		foreach (object item in base.Regenerate())
		{
			yield return item;
		}
		
		// Draw nothing while the overlay is off
		if (PlanetsmithOverlay.Mode == OverlayMode.None)
		{
			FinalizeMesh(MeshParts.All);
			yield break;
		}
		
		// Colour every tile
		material ??= new Material(WorldMaterials.VertexColorTransparent);
		NativeArray<int> vertexOffsets = planetLayer.UnsafeTileIDToVerts_offsets;
		NativeArray<Vector3> vertices = planetLayer.UnsafeVerts;
		List<Tile> tiles = planetLayer.Tiles;
		int tileCount = Mathf.Min(planetLayer.TilesCount, tiles.Count);
		for (int tileId = 0; tileId < tileCount; tileId++)
		{
			Color32 color = PlanetsmithOverlay.ColorFor(tileId, tiles[tileId]);
			Color32 vertexColor = new(color.r, color.g, color.b, OVERLAY_ALPHA);
			LayerSubMesh subMesh = GetSubMesh(material, out _);
			int baseVertex = subMesh.verts.Count;
			int vertexInTile = 0;
			int end = tileId + 1 < vertexOffsets.Length ? vertexOffsets[tileId + 1] : vertices.Length;
			for (int vertex = vertexOffsets[tileId]; vertex < end; vertex++)
			{
				subMesh.verts.Add(vertices[vertex]);
				subMesh.colors.Add(vertexColor);
				if (vertex < end - 2)
				{
					subMesh.tris.Add(baseVertex + vertexInTile + 2);
					subMesh.tris.Add(baseVertex + vertexInTile + 1);
					subMesh.tris.Add(baseVertex);
				}
				vertexInTile++;
			}
		}
		FinalizeMesh(MeshParts.All);
	}
}
