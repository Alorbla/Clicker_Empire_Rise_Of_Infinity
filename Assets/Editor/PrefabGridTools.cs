using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Experimental.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class PrefabGridTools
{
    private const string WorkspaceScenePath = "Assets/Scenes/PrefabWorkspace.unity";
    private const string HelperRootName = "PrefabGridHelper";
    private const string HelperTilemapName = "OverlayTilemap";
    private const string CheckerSpritePath = "Assets/Sprites/Tileset/isometric_128_64.png";

    [MenuItem("Tools/IdleHra/Create Prefab Grid Workspace")]
    public static void CreatePrefabGridWorkspace()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var grid = CreateGridWithOverlay(scene, ResolveSourceTilemap(), out _);

        if (grid == null)
        {
            EditorUtility.DisplayDialog("Prefab Workspace", "Failed to create grid.", "OK");
            return;
        }

        EditorSceneManager.SaveScene(scene, WorkspaceScenePath);
        Debug.Log($"Prefab workspace saved to {WorkspaceScenePath}");
    }

    [MenuItem("Tools/IdleHra/Open Prefab With Grid")]
    public static void OpenPrefabWithGrid()
    {
        var prefab = Selection.activeObject as GameObject;
        if (prefab == null || PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.NotAPrefab)
        {
            EditorUtility.DisplayDialog("Open Prefab With Grid", "Select a prefab asset in the Project view.", "OK");
            return;
        }

        PrefabStage stage = PrefabStageUtility.OpenPrefab(AssetDatabase.GetAssetPath(prefab));
        if (stage == null)
        {
            EditorUtility.DisplayDialog("Open Prefab With Grid", "Could not open prefab stage.", "OK");
            return;
        }

        var existing = GameObject.Find(HelperRootName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        CreateGridWithOverlay(stage.scene, ResolveSourceTilemap(), out _);
    }

    private static Grid CreateGridWithOverlay(Scene scene, Tilemap source, out Tilemap createdOverlay)
    {
        createdOverlay = null;
        var root = new GameObject(HelperRootName);
        EditorSceneManager.MoveGameObjectToScene(root, scene);

        var grid = root.AddComponent<Grid>();
        grid.cellLayout = GridLayout.CellLayout.Isometric;
        grid.cellSize = new Vector3(2f, 1f, 1f);

        var overlayGO = new GameObject(HelperTilemapName);
        overlayGO.transform.SetParent(root.transform, false);
        var overlayMap = overlayGO.AddComponent<Tilemap>();
        var overlayRenderer = overlayGO.AddComponent<TilemapRenderer>();
        overlayRenderer.sortingOrder = 10000;
        overlayRenderer.material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        createdOverlay = overlayMap;

        if (source != null && CopyTiles(source, overlayMap))
        {
            return grid;
        }

        GenerateChecker(overlayMap, 20, 20, LoadCheckerSprite());
        return grid;
    }

    private static Tilemap ResolveSourceTilemap()
    {
        var selected = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<Tilemap>()
            : null;
        if (selected != null)
        {
            return selected;
        }

        var buildSystem = Object.FindFirstObjectByType<BuildSystem>();
        if (buildSystem != null)
        {
            var serialized = new SerializedObject(buildSystem);
            var prop = serialized.FindProperty("buildAreaTilemap");
            var tilemap = prop != null ? prop.objectReferenceValue as Tilemap : null;
            if (tilemap != null)
            {
                return tilemap;
            }
        }

        var overlay = GameObject.Find("GridOverlay");
        return overlay != null ? overlay.GetComponent<Tilemap>() : null;
    }

    private static bool CopyTiles(Tilemap source, Tilemap target)
    {
        if (source == null || target == null)
        {
            return false;
        }

        if (source.GetUsedTilesCount() == 0)
        {
            return false;
        }

        target.ClearAllTiles();
        BoundsInt bounds = source.cellBounds;
        TileBase[] tiles = source.GetTilesBlock(bounds);
        target.SetTilesBlock(bounds, tiles);
        EditorUtility.SetDirty(target);
        return true;
    }

    private static void GenerateChecker(Tilemap target, int width, int height, Sprite sprite)
    {
        if (target == null)
        {
            return;
        }

        target.ClearAllTiles();

        var light = ScriptableObject.CreateInstance<Tile>();
        light.color = new Color(0.45f, 1f, 0.45f, 0.45f);
        light.sprite = sprite;
        light.hideFlags = HideFlags.HideAndDontSave;

        var dark = ScriptableObject.CreateInstance<Tile>();
        dark.color = new Color(0.1f, 0.35f, 0.1f, 0.35f);
        dark.sprite = sprite;
        dark.hideFlags = HideFlags.HideAndDontSave;

        int startX = -width / 2;
        int startY = -height / 2;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int gx = startX + x;
                int gy = startY + y;
                TileBase tile = ((x + y) % 2 == 0) ? light : dark;
                target.SetTile(new Vector3Int(gx, gy, 0), tile);
            }
        }

        target.color = Color.white;
        EditorUtility.SetDirty(target);
    }

    private static Sprite LoadCheckerSprite()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CheckerSpritePath);
        if (sprite != null)
        {
            return sprite;
        }

        var assets = AssetDatabase.LoadAllAssetsAtPath(CheckerSpritePath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite found)
            {
                return found;
            }
        }

        Debug.LogWarning($"Checker sprite not found at {CheckerSpritePath}. Using runtime fallback.");
        return CreateFallbackSprite();
    }

    private static Sprite CreateFallbackSprite()
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}
