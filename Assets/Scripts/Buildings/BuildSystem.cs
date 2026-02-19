using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

public class BuildSystem : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private Grid grid;
    [SerializeField] private Tilemap buildTilemap;
    [SerializeField] private Tilemap buildAreaTilemap;
    [SerializeField] private Tilemap occupiedTilemap;
    [SerializeField] private TileBase occupiedTile;

    [Header("Placement")]
    [SerializeField] private LayerMask blockingMask = ~0;
    [SerializeField] private bool ignoreWhenPointerOverUI = true;
    [SerializeField] private bool showPreview = true;
    [SerializeField] private Color validColor = new Color(0.2f, 1f, 0.2f, 0.6f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.6f);
    [SerializeField] private GameObject gridOverlay;
    [SerializeField] private bool requireBuildAreaTilemap = true;
    [SerializeField] private bool requireUnoccupiedTiles = true;
    [SerializeField] private bool blockByPolygonColliders = false;

    private BuildingType _currentType;
    private GameObject _previewInstance;
    private SpriteRenderer[] _previewRenderers;
    private TileBase _runtimeOccupiedTile;

    public bool IsPlacing => _currentType != null;

    public void SetBuildAreaTilemap(Tilemap tilemap)
    {
        buildAreaTilemap = tilemap;
    }

    public void EnterPlacement(BuildingType type)
    {
        if (type == null || type.Prefab == null)
        {
            Debug.LogWarning("BuildSystem: Invalid BuildingType or missing prefab.");
            return;
        }

        _currentType = type;
        CreatePreview();
        SetGridOverlayVisible(true);
    }

    public void CancelPlacement()
    {
        _currentType = null;
        DestroyPreview();
        SetGridOverlayVisible(false);
    }

    private void Update()
    {
        if (_currentType == null)
        {
            return;
        }

        if (ignoreWhenPointerOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        var mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        Vector3 worldPos = GetMouseWorld(mouse);
        Vector3 placePos = GetSnappedPosition(worldPos, _currentType.Footprint);
        bool canPlace = CanPlaceAt(placePos, _currentType);

        UpdatePreview(placePos, canPlace);

        if (mouse.leftButton.wasPressedThisFrame && canPlace)
        {
            PlaceBuilding(placePos, _currentType);
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            CancelPlacement();
        }
    }

    private void OnDisable()
    {
        SetGridOverlayVisible(false);
    }

    private Vector3 GetMouseWorld(Mouse mouse)
    {
        Vector2 mousePos = mouse.position.ReadValue();
        Camera cam = Camera.main;
        if (cam == null)
        {
            return Vector3.zero;
        }

        Vector3 world = cam.ScreenToWorldPoint(mousePos);
        world.z = 0f;
        return world;
    }

    private Vector3 GetSnappedPosition(Vector3 worldPos, Vector2Int footprint)
    {
        if (grid == null)
        {
            return worldPos;
        }

        Vector3Int cell = grid.WorldToCell(worldPos);
        Vector3 center = grid.GetCellCenterWorld(cell);
        Vector3 cellSize = grid.cellSize;
        Vector3 offset = new Vector3((footprint.x - 1) * cellSize.x * 0.5f, (footprint.y - 1) * cellSize.y * 0.5f, 0f);
        return center + offset;
    }

    private bool CanPlaceAt(Vector3 position, BuildingType type)
    {
        if (type == null)
        {
            return false;
        }

        if (!IsInsideBuildArea(position, type))
        {
            return false;
        }

        if (requireUnoccupiedTiles && IsOccupied(position, type))
        {
            return false;
        }

        if (blockByPolygonColliders)
        {
            Vector2 size = GetPlacementSize(type);
            Collider2D[] hits = Physics2D.OverlapBoxAll(position, size, 0f, blockingMask);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] is PolygonCollider2D)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool IsInsideBuildArea(Vector3 position, BuildingType type)
    {
        if (buildAreaTilemap == null || grid == null)
        {
            return !requireBuildAreaTilemap;
        }

        Vector3Int origin = GetFootprintOriginCell(position, type.Footprint);
        for (int x = 0; x < Mathf.Max(1, type.Footprint.x); x++)
        {
            for (int y = 0; y < Mathf.Max(1, type.Footprint.y); y++)
            {
                var cell = new Vector3Int(origin.x + x, origin.y + y, origin.z);
                if (!buildAreaTilemap.HasTile(cell))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool IsOccupied(Vector3 position, BuildingType type)
    {
        if (occupiedTilemap == null || grid == null)
        {
            return false;
        }

        Vector3Int origin = GetFootprintOriginCell(position, type.Footprint);
        for (int x = 0; x < Mathf.Max(1, type.Footprint.x); x++)
        {
            for (int y = 0; y < Mathf.Max(1, type.Footprint.y); y++)
            {
                var cell = new Vector3Int(origin.x + x, origin.y + y, origin.z);
                if (occupiedTilemap.HasTile(cell))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void MarkOccupied(Vector3 position, BuildingType type)
    {
        if (!requireUnoccupiedTiles || grid == null)
        {
            return;
        }

        if (occupiedTilemap == null)
        {
            occupiedTilemap = GetOrCreateOccupiedTilemap();
        }

        if (occupiedTilemap == null)
        {
            return;
        }

        TileBase tile = occupiedTile != null ? occupiedTile : GetRuntimeOccupiedTile();
        Vector3Int origin = GetFootprintOriginCell(position, type.Footprint);
        for (int x = 0; x < Mathf.Max(1, type.Footprint.x); x++)
        {
            for (int y = 0; y < Mathf.Max(1, type.Footprint.y); y++)
            {
                var cell = new Vector3Int(origin.x + x, origin.y + y, origin.z);
                occupiedTilemap.SetTile(cell, tile);
            }
        }
    }

    private Vector3Int GetFootprintOriginCell(Vector3 position, Vector2Int footprint)
    {
        if (grid == null)
        {
            return Vector3Int.zero;
        }

        Vector3 cellSize = grid.cellSize;
        Vector3 offset = new Vector3((footprint.x - 1) * cellSize.x * 0.5f, (footprint.y - 1) * cellSize.y * 0.5f, 0f);
        Vector3 originWorld = position - offset;
        return grid.WorldToCell(originWorld);
    }

    private Tilemap GetOrCreateOccupiedTilemap()
    {
        if (grid == null)
        {
            return null;
        }

        Transform parent = grid.transform;
        var existing = parent.Find("OccupiedArea");
        if (existing != null)
        {
            return existing.GetComponent<Tilemap>();
        }

        var go = new GameObject("OccupiedArea");
        go.transform.SetParent(parent, false);
        var tilemap = go.AddComponent<Tilemap>();
        var renderer = go.AddComponent<TilemapRenderer>();
        renderer.enabled = false;
        return tilemap;
    }

    private TileBase GetRuntimeOccupiedTile()
    {
        if (_runtimeOccupiedTile != null)
        {
            return _runtimeOccupiedTile;
        }

        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.color = new Color(0f, 0f, 0f, 0f);
        tile.hideFlags = HideFlags.HideAndDontSave;
        _runtimeOccupiedTile = tile;
        return _runtimeOccupiedTile;
    }

    private Vector2 GetPlacementSize(BuildingType type)
    {
        if (type.Prefab != null)
        {
            var collider = type.Prefab.GetComponentInChildren<BoxCollider2D>();
            if (collider != null)
            {
                Vector3 lossy = collider.transform.lossyScale;
                return new Vector2(collider.size.x * lossy.x, collider.size.y * lossy.y);
            }
        }

        if (grid != null)
        {
            Vector3 cell = grid.cellSize;
            return new Vector2(cell.x * Mathf.Max(1, type.Footprint.x), cell.y * Mathf.Max(1, type.Footprint.y));
        }

        return Vector2.one;
    }

    private void PlaceBuilding(Vector3 position, BuildingType type)
    {
        if (!CanAfford(type))
        {
            Debug.Log("BuildSystem: Not enough resources.");
            return;
        }

        SpendCosts(type);
        GameObject placedInstance = Instantiate(type.Prefab, position, Quaternion.identity);
        MarkOccupied(position, type);

        var upgradable = placedInstance != null
            ? placedInstance.GetComponentInChildren<BuildingUpgradable>()
            : null;
        if (upgradable != null)
        {
            upgradable.SetBuildingType(type);
        }

        ApplyPlacementEffects(type, placedInstance);
    }

    private bool CanAfford(BuildingType type)
    {
        var manager = ResourceManager.Instance;
        if (manager == null)
        {
            return true;
        }

        var costs = type.Costs;
        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost.resourceType == null || cost.amount <= 0)
            {
                continue;
            }

            int amount = GetModifiedCost(cost.amount);
            if (manager.Get(cost.resourceType) < amount)
            {
                return false;
            }
        }

        return true;
    }

    private void SpendCosts(BuildingType type)
    {
        var manager = ResourceManager.Instance;
        if (manager == null)
        {
            return;
        }

        var costs = type.Costs;
        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost.resourceType == null || cost.amount <= 0)
            {
                continue;
            }

            int amount = GetModifiedCost(cost.amount);
            manager.Spend(cost.resourceType, amount);
        }
    }

    private int GetModifiedCost(int amount)
    {
        var modifiers = GlobalModifiers.Instance != null
            ? GlobalModifiers.Instance
            : Object.FindAnyObjectByType<GlobalModifiers>();
        return modifiers != null ? modifiers.ApplyCost(amount) : amount;
    }

    private void CreatePreview()
    {
        DestroyPreview();

        if (!showPreview || _currentType == null || _currentType.Prefab == null)
        {
            return;
        }

        _previewInstance = Instantiate(_currentType.Prefab);
        _previewInstance.name = $"{_currentType.Prefab.name}_Preview";

        var colliders = _previewInstance.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        var behaviours = _previewInstance.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != this)
            {
                behaviours[i].enabled = false;
            }
        }

        _previewRenderers = _previewInstance.GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void UpdatePreview(Vector3 position, bool canPlace)
    {
        if (_previewInstance == null)
        {
            return;
        }

        _previewInstance.transform.position = position;
        if (_previewRenderers == null)
        {
            return;
        }

        Color color = canPlace ? validColor : invalidColor;
        for (int i = 0; i < _previewRenderers.Length; i++)
        {
            _previewRenderers[i].color = color;
        }
    }

    private void DestroyPreview()
    {
        if (_previewInstance != null)
        {
            Destroy(_previewInstance);
            _previewInstance = null;
            _previewRenderers = null;
        }
    }

    private void ApplyPlacementEffects(BuildingType type, GameObject placedInstance)
    {
        if (type == null)
        {
            return;
        }

        if (placedInstance != null)
        {
            var upgradable = placedInstance.GetComponentInChildren<BuildingUpgradable>();
            if (upgradable != null)
            {
                // Storage is handled by BuildingUpgradable per level to avoid duplicate bonuses.
                return;
            }
        }

        var manager = ResourceManager.Instance;
        if (manager == null)
        {
            manager = Object.FindAnyObjectByType<ResourceManager>();
        }

        if (manager == null)
        {
            return;
        }

        if (type.StorageCapacityBonus > 0)
        {
            manager.IncreaseStorageCapacity(type.StorageCapacityBonus);
        }

        var resourceBonuses = type.StorageCapacityBonuses;
        if (resourceBonuses == null)
        {
            return;
        }

        for (int i = 0; i < resourceBonuses.Count; i++)
        {
            var bonus = resourceBonuses[i];
            if (bonus.resourceType == null || bonus.amount <= 0)
            {
                continue;
            }

            manager.IncreaseStorageCapacity(bonus.resourceType, bonus.amount);
        }
    }

    private void SetGridOverlayVisible(bool isVisible)
    {
        if (gridOverlay == null)
        {
            return;
        }

        if (gridOverlay.activeSelf != isVisible)
        {
            gridOverlay.SetActive(isVisible);
        }
    }
}

