using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class BuildAreaTilemapGenerator
{
    private const string TargetName = "BuildableArea";

    [MenuItem("Tools/IdleHra/Create Buildable Tilemap From Overlay")]
    public static void CreateBuildableTilemapFromOverlay()
    {
        var source = GetSelectedTilemap();
        if (source == null)
        {
            EditorUtility.DisplayDialog("Buildable Tilemap", "Select a Tilemap (grid overlay) in the Hierarchy.", "OK");
            return;
        }

        Grid grid = source.GetComponentInParent<Grid>();
        if (grid == null)
        {
            EditorUtility.DisplayDialog("Buildable Tilemap", "Selected Tilemap is not under a Grid.", "OK");
            return;
        }

        Tilemap target = FindOrCreateTarget(grid.transform);
        CopyTiles(source, target);
        AssignToBuildSystem(target);

        Selection.activeObject = target.gameObject;
        Debug.Log($"Buildable tilemap created/updated: {target.name}");
    }

    private static Tilemap GetSelectedTilemap()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            return null;
        }

        return go.GetComponent<Tilemap>();
    }

    private static Tilemap FindOrCreateTarget(Transform gridTransform)
    {
        var existing = gridTransform.Find(TargetName);
        if (existing != null)
        {
            var tilemap = existing.GetComponent<Tilemap>();
            if (tilemap != null)
            {
                return tilemap;
            }
        }

        var go = new GameObject(TargetName);
        go.transform.SetParent(gridTransform, false);
        var newTilemap = go.AddComponent<Tilemap>();
        var renderer = go.AddComponent<TilemapRenderer>();
        renderer.enabled = false;
        return newTilemap;
    }

    private static void CopyTiles(Tilemap source, Tilemap target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.ClearAllTiles();
        BoundsInt bounds = source.cellBounds;
        TileBase[] tiles = source.GetTilesBlock(bounds);
        target.SetTilesBlock(bounds, tiles);
        EditorUtility.SetDirty(target);
    }

    private static void AssignToBuildSystem(Tilemap target)
    {
        var systems = Object.FindObjectsByType<BuildSystem>(FindObjectsSortMode.None);
        if (systems == null || systems.Length == 0)
        {
            return;
        }

        if (systems.Length == 1)
        {
            systems[0].SetBuildAreaTilemap(target);
            EditorUtility.SetDirty(systems[0]);
            return;
        }

        Debug.LogWarning("Multiple BuildSystem instances found. Assign build area tilemap manually.");
    }
}
