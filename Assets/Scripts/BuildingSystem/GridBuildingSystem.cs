using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Tilemaps;

namespace IdleHra.BuildingSystem
{
    public sealed class GridBuildingSystem : MonoBehaviour
    {
        public static GridBuildingSystem Instance { get; private set; }

        [Header("Scene References")]
        [SerializeField] private Grid gridLayout;
        [SerializeField] private Tilemap mainTilemap;
        [SerializeField] private Tilemap tempTilemap;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Transform placementParent;

        [Header("Tiles")]
        [SerializeField] private TileBase whiteBuildableTile;
        [SerializeField] private TileBase greenValidTile;
        [SerializeField] private TileBase redInvalidTile;

        [Header("Input")]
        [SerializeField] private bool ignoreWhenPointerOverUI = true;
        [SerializeField] private bool requireTouchDoubleTapToPlace = true;
        [SerializeField] private float touchDoubleTapMaxDelay = 0.35f;
        [SerializeField] private float touchDoubleTapMaxDistance = 40f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = false;

        private readonly HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();
        private readonly List<Vector3Int> lastPaintedCells = new List<Vector3Int>(64);

        private Building activePreview;
        private BuildingType activeType;
        private GameObject activePreviewObject;
        private Vector3Int lastCellPosition = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
        private bool isEditingExisting;
        private Vector3Int editOriginalCell;
        private TilemapRenderer mainTilemapRenderer;
        private readonly List<Behaviour> cachedRuntimeComponents = new List<Behaviour>(16);
        private readonly List<SpriteRenderer> cachedSpriteRenderers = new List<SpriteRenderer>(8);
        private readonly List<Color> cachedSpriteColors = new List<Color>(8);
        private readonly List<SpriteRenderer> previewBoundsRenderers = new List<SpriteRenderer>(8);
        [SerializeField] [Range(0.05f, 1f)] private float ghostAlpha = 0.45f;
        private Vector3 previewCenterOffsetWorld;

        [System.Serializable]
        public class PlacedBuildingSaveData
        {
            public string buildingTypeId = "";
            public int cellX;
            public int cellY;
            public int cellZ;
            public int level;
        }
        private sealed class RuntimePlacedBuildingMarker : MonoBehaviour
        {
        }
        private bool touchConfirmArmed;
        private float lastTouchTapTime = -10f;
        private Vector2 lastTouchTapPos;

        #region Unity Methods
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("GridBuildingSystem: duplicate instance found, destroying this one.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            CacheMainTilemapRenderer();
            SetMainTilemapVisible(false);
            ResetTouchPlacementConfirm();
        }

        private void OnEnable()
        {
            BuildingShopEvents.OnRequestPlacement += InitializeWithBuilding;
        }

        private void OnDisable()
        {
            BuildingShopEvents.OnRequestPlacement -= InitializeWithBuilding;
        }

        private void Update()
        {
            if (activePreview == null)
            {
                return;
            }

            if (gridLayout == null)
            {
                Debug.LogWarning("GridBuildingSystem: gridLayout is not assigned.");
                return;
            }

            if (worldCamera == null)
            {
                Debug.LogWarning("GridBuildingSystem: worldCamera is not assigned.");
                return;
            }

            HandlePlacementInput();
            UpdatePreviewPosition();
        }

        public List<PlacedBuildingSaveData> CapturePlacedBuildingStates()
        {
            var result = new List<PlacedBuildingSaveData>(32);
            var buildings = Object.FindObjectsByType<Building>(FindObjectsSortMode.None);
            for (int i = 0; i < buildings.Length; i++)
            {
                var building = buildings[i];
                if (building == null)
                {
                    continue;
                }

                if (!IsRuntimePlacedBuilding(building))
                {
                    continue;
                }

                // Scene-authored Town Hall should not be serialized as a placed building.
                if (IsPersistentSceneBuilding(building))
                {
                    continue;
                }

                var upgradable = building.GetComponent<BuildingUpgradable>();
                if (upgradable == null)
                {
                    upgradable = building.GetComponentInChildren<BuildingUpgradable>();
                }

                var inspector = building.GetComponent<BuildingInspectorTarget>();
                if (inspector == null)
                {
                    inspector = building.GetComponentInChildren<BuildingInspectorTarget>();
                }

                var type = upgradable != null ? upgradable.BuildingType : (inspector != null ? inspector.BuildingType : null);
                if (type == null)
                {
                    continue;
                }

                var cell = building.HasGridPosition
                    ? building.CurrentCell
                    : (gridLayout != null ? gridLayout.WorldToCell(building.transform.position) : Vector3Int.zero);

                result.Add(new PlacedBuildingSaveData
                {
                    buildingTypeId = GetBuildingTypeKey(type),
                    cellX = cell.x,
                    cellY = cell.y,
                    cellZ = cell.z,
                    level = upgradable != null ? upgradable.CurrentLevel : 0
                });
            }

            return result;
        }

        public void RestorePlacedBuildingStates(List<PlacedBuildingSaveData> states, Dictionary<string, BuildingType> typeLookup)
        {
            if (states == null || states.Count == 0)
            {
                occupiedCells.Clear();
                return;
            }

            ClearPlacedBuildingsForRestore();
            occupiedCells.Clear();

            var resolvedTypes = typeLookup;
            if (resolvedTypes == null)
            {
                resolvedTypes = new Dictionary<string, BuildingType>(global::System.StringComparer.OrdinalIgnoreCase);
            }

            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state == null || global::System.String.IsNullOrWhiteSpace(state.buildingTypeId))
                {
                    continue;
                }

                if (!resolvedTypes.TryGetValue(state.buildingTypeId, out var type) || type == null || type.Prefab == null)
                {
                    continue;
                }

                var root = Instantiate(type.Prefab, ResolvePlacementParent());
                if (root == null)
                {
                    continue;
                }

                MarkAsRuntimePlaced(root);

                var building = root.GetComponent<Building>();
                if (building == null)
                {
                    building = root.AddComponent<Building>();
                }

                if (!type.UsePrefabFootprint)
                {
                    building.ApplyFootprint(type.Footprint);
                }

                var cell = new Vector3Int(state.cellX, state.cellY, state.cellZ);
                building.SetGridPosition(cell);
                root.transform.position = GetAnchorWorldPosition(cell);

                RegisterOccupied(building.GetPlacementBounds());
                EnsureIsoSorter(root, false);
                AssignInspectorTarget(root, type);
                AssignUpgradable(root, type);

                var upgradable = root.GetComponent<BuildingUpgradable>();
                if (upgradable == null)
                {
                    upgradable = root.GetComponentInChildren<BuildingUpgradable>();
                }

                if (upgradable != null)
                {
                    upgradable.SetLevelFromSave(state.level);
                }

                DisableBuildMenuForPlacedType(type);
            }
        }
        #endregion

        #region Public API
        public void InitializeWithBuildingType(BuildingType type)
        {
            if (type == null || type.Prefab == null)
            {
                Debug.LogWarning("GridBuildingSystem: Invalid BuildingType or missing prefab.");
                return;
            }

            InitializeWithBuilding(type.Prefab);
            activeType = type;
            SetMainTilemapVisible(true);
            ResetTouchPlacementConfirm();
            SnapPreviewToCameraCenter();

            if (activePreview != null)
            {
                if (!type.UsePrefabFootprint)
                {
                    activePreview.ApplyFootprint(type.Footprint);
                }
            }
        }

        public void InitializeWithBuilding(GameObject buildingPrefab)
        {
            if (buildingPrefab == null)
            {
                Debug.LogWarning("GridBuildingSystem: InitializeWithBuilding called with null prefab.");
                return;
            }

            if (activePreviewObject != null)
            {
                CancelPlacement();
            }

            activePreviewObject = Instantiate(buildingPrefab, ResolvePlacementParent());
            activePreview = activePreviewObject.GetComponent<Building>();

            if (activePreview == null)
            {
                Debug.LogError("GridBuildingSystem: Building prefab is missing Building component.");
                Destroy(activePreviewObject);
                activePreviewObject = null;
                return;
            }

            SetRuntimeComponentsEnabled(activePreviewObject, false);
            SetGhostVisuals(activePreviewObject, true);
            CachePreviewCenterOffset();
            EnsureIsoSorter(activePreviewObject, true);

            if (verboseLogs)
            {
                Debug.Log($"GridBuildingSystem: Started placement for {buildingPrefab.name}");
            }

            SetMainTilemapVisible(true);
            ResetTouchPlacementConfirm();
            SnapPreviewToCameraCenter();
        }

        public void CancelPlacement()
        {
            ClearTempOverlay();

            if (isEditingExisting && activePreview != null && gridLayout != null)
            {
                activePreview.SetGridPosition(editOriginalCell);
                activePreviewObject.transform.position = GetAnchorWorldPosition(editOriginalCell);
                RegisterOccupied(activePreview.GetPlacementBounds());
                SetGhostVisuals(activePreviewObject, false);
                SetRuntimeComponentsEnabled(activePreviewObject, true);
            }
            else if (activePreviewObject != null)
            {
                Destroy(activePreviewObject);
            }

            activePreviewObject = null;
            activePreview = null;
            activeType = null;
            lastCellPosition = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            isEditingExisting = false;
            SetMainTilemapVisible(false);
            ResetTouchPlacementConfirm();

            if (verboseLogs)
            {
                Debug.Log("GridBuildingSystem: Placement cancelled.");
            }
        }

        public bool BeginEdit(Building existingBuilding)
        {
            if (existingBuilding == null)
            {
                Debug.LogWarning("GridBuildingSystem: BeginEdit called with null building.");
                return false;
            }

            if (gridLayout == null)
            {
                Debug.LogWarning("GridBuildingSystem: gridLayout is not assigned.");
                return false;
            }

            if (activePreviewObject != null)
            {
                CancelPlacement();
            }

            isEditingExisting = true;
            activePreview = existingBuilding;
            activePreviewObject = existingBuilding.gameObject;
            editOriginalCell = existingBuilding.CurrentCell;
            if (!existingBuilding.HasGridPosition)
            {
                editOriginalCell = gridLayout.WorldToCell(existingBuilding.transform.position);
                existingBuilding.SetGridPosition(editOriginalCell);
            }

            UnregisterOccupied(existingBuilding.GetPlacementBounds());
            SetRuntimeComponentsEnabled(activePreviewObject, false);
            SetGhostVisuals(activePreviewObject, true);
            CachePreviewCenterOffset();
            EnsureIsoSorter(activePreviewObject, true);
            SetMainTilemapVisible(true);
            ResetTouchPlacementConfirm();
            return true;
        }
        #endregion

        #region Building Placement
        private void HandlePlacementInput()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelPlacement();
                return;
            }

            bool placementPressed = TryGetPrimaryPressThisFrame(out var pressScreenPos, out var pointerId, out var isTouch);
            if (placementPressed && ignoreWhenPointerOverUI && IsPointerOverUI(pointerId, isTouch))
            {
                placementPressed = false;
            }

            if (placementPressed)
            {
                if (isTouch && requireTouchDoubleTapToPlace)
                {
                    if (TryConsumeTouchConfirmTap(pressScreenPos))
                    {
                        TryPlaceActiveBuilding();
                    }
                }
                else
                {
                    TryPlaceActiveBuilding();
                }
            }

            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                CancelPlacement();
            }
        }

        private void UpdatePreviewPosition()
        {
            if (activePreview == null)
            {
                return;
            }

            if (!TryGetPointerPositionForPreview(out var pointerScreenPos, out var pointerId, out var isTouch))
            {
                return;
            }

            if (ignoreWhenPointerOverUI && IsPointerOverUI(pointerId, isTouch))
            {
                return;
            }

            PlacePreviewAtScreenPosition(pointerScreenPos);
        }

        private void PlacePreviewAtScreenPosition(Vector2 pointerScreenPos)
        {
            Vector3 worldPos = worldCamera.ScreenToWorldPoint(pointerScreenPos);
            worldPos.z = 0f;
            // Keep placed object anchored to grid cell, but sample pointer against
            // a center-compensated position so cursor stays visually on sprite.
            Vector3 compensatedWorldPos = worldPos - previewCenterOffsetWorld;
            Vector3Int cellPos = gridLayout.WorldToCell(compensatedWorldPos);

            if (cellPos == lastCellPosition)
            {
                return;
            }

            lastCellPosition = cellPos;
            activePreview.SetGridPosition(cellPos);

            if (activePreviewObject == null && activePreview != null)
            {
                activePreviewObject = activePreview.gameObject;
            }

            if (activePreviewObject == null)
            {
                return;
            }

            activePreviewObject.transform.position = GetAnchorWorldPosition(cellPos);
            PaintOverlayForActive();
        }

        private void SnapPreviewToCameraCenter()
        {
            if (worldCamera == null)
            {
                return;
            }

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            PlacePreviewAtScreenPosition(center);
        }

        private static bool TryGetPrimaryPressThisFrame(out Vector2 pressScreenPos, out int pointerId, out bool isTouch)
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                pressScreenPos = mouse.position.ReadValue();
                pointerId = -1;
                isTouch = false;
                return true;
            }

            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                if (GetActiveTouchCount(touchscreen) > 1)
                {
                    pressScreenPos = default;
                    pointerId = -1;
                    isTouch = false;
                    return false;
                }

                TouchControl touch = touchscreen.primaryTouch;
                if (touch != null && touch.press.wasPressedThisFrame)
                {
                    pressScreenPos = touch.position.ReadValue();
                    pointerId = touch.touchId.ReadValue();
                    isTouch = true;
                    return true;
                }
            }

            pressScreenPos = default;
            pointerId = -1;
            isTouch = false;
            return false;
        }

        private static bool TryGetPointerPositionForPreview(out Vector2 pointerScreenPos, out int pointerId, out bool isTouch)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                if (GetActiveTouchCount(touchscreen) == 1)
                {
                    TouchControl touch = touchscreen.primaryTouch;
                    if (touch != null && touch.press.isPressed)
                    {
                        pointerScreenPos = touch.position.ReadValue();
                        pointerId = touch.touchId.ReadValue();
                        isTouch = true;
                        return true;
                    }
                }

                if (Application.isMobilePlatform)
                {
                    pointerScreenPos = default;
                    pointerId = -1;
                    isTouch = false;
                    return false;
                }
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                pointerScreenPos = mouse.position.ReadValue();
                pointerId = -1;
                isTouch = false;
                return true;
            }

            pointerScreenPos = default;
            pointerId = -1;
            isTouch = false;
            return false;
        }

        private static int GetActiveTouchCount(Touchscreen touchscreen)
        {
            if (touchscreen == null)
            {
                return 0;
            }

            int count = 0;
            var touches = touchscreen.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                if (touches[i].press.isPressed)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsPointerOverUI(int pointerId, bool isTouch)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            if (isTouch)
            {
                return EventSystem.current.IsPointerOverGameObject(pointerId);
            }

            return EventSystem.current.IsPointerOverGameObject();
        }

        private bool TryConsumeTouchConfirmTap(Vector2 tapPos)
        {
            float now = Time.unscaledTime;
            if (!touchConfirmArmed)
            {
                touchConfirmArmed = true;
                lastTouchTapTime = now;
                lastTouchTapPos = tapPos;
                return false;
            }

            bool withinDelay = (now - lastTouchTapTime) <= Mathf.Max(0.05f, touchDoubleTapMaxDelay);
            bool withinDistance = Vector2.Distance(tapPos, lastTouchTapPos) <= Mathf.Max(1f, touchDoubleTapMaxDistance);

            lastTouchTapTime = now;
            lastTouchTapPos = tapPos;

            if (withinDelay && withinDistance)
            {
                touchConfirmArmed = false;
                return true;
            }

            touchConfirmArmed = true;
            return false;
        }

        private void ResetTouchPlacementConfirm()
        {
            touchConfirmArmed = false;
            lastTouchTapTime = -10f;
            lastTouchTapPos = default;
        }

        private void TryPlaceActiveBuilding()
        {
            if (activePreview == null)
            {
                return;
            }

            BoundsInt area = activePreview.GetPlacementBounds();
            bool isValid = IsPlacementValid(area);

            if (!isValid)
            {
                if (verboseLogs)
                {
                    Debug.Log("GridBuildingSystem: placement invalid.");
                }
                return;
            }

            if (activeType != null)
            {
                if (!CanAfford(activeType))
                {
                    Debug.Log("GridBuildingSystem: Not enough resources.");
                    return;
                }

                SpendCosts(activeType);
            }

            RegisterOccupied(area);
            ClearTempOverlay();
            SetGhostVisuals(activePreviewObject, false);
            SetRuntimeComponentsEnabled(activePreviewObject, true);
            EnsureIsoSorter(activePreviewObject, false);
            AssignInspectorTarget(activePreviewObject, activeType);
            AssignUpgradable(activePreviewObject, activeType);
            ApplyPlacementEffects(activeType, activePreviewObject);
            MarkAsRuntimePlaced(activePreviewObject);
            DisableBuildMenuForPlacedType(activeType);

            if (verboseLogs)
            {
                Debug.Log("GridBuildingSystem: building placed.");
            }

            activePreview = null;
            activePreviewObject = null;
            activeType = null;
            lastCellPosition = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            isEditingExisting = false;
            SetMainTilemapVisible(false);
            ResetTouchPlacementConfirm();
        }
        #endregion

        #region Tilemap Management
        private void CacheMainTilemapRenderer()
        {
            if (mainTilemapRenderer == null && mainTilemap != null)
            {
                mainTilemapRenderer = mainTilemap.GetComponent<TilemapRenderer>();
            }
        }

        private void SetMainTilemapVisible(bool visible)
        {
            if (mainTilemap == null)
            {
                return;
            }

            CacheMainTilemapRenderer();

            if (mainTilemapRenderer != null)
            {
                mainTilemapRenderer.enabled = visible;
            }
        }

        private void SetRuntimeComponentsEnabled(GameObject root, bool enabled)
        {
            if (root == null)
            {
                return;
            }

            cachedRuntimeComponents.Clear();
            root.GetComponentsInChildren(true, cachedRuntimeComponents);

            for (int i = 0; i < cachedRuntimeComponents.Count; i++)
            {
                var component = cachedRuntimeComponents[i];
                if (component == null)
                {
                    continue;
                }

                if (component is ClickableNode || component is BuildingProducer)
                {
                    component.enabled = enabled;
                }
            }
        }

        private void SetGhostVisuals(GameObject root, bool enabled)
        {
            if (root == null)
            {
                return;
            }

            cachedSpriteRenderers.Clear();
            cachedSpriteColors.Clear();
            root.GetComponentsInChildren(true, cachedSpriteRenderers);

            if (cachedSpriteRenderers.Count == 0)
            {
                return;
            }

            if (enabled)
            {
                for (int i = 0; i < cachedSpriteRenderers.Count; i++)
                {
                    var renderer = cachedSpriteRenderers[i];
                    if (renderer == null)
                    {
                        cachedSpriteColors.Add(Color.white);
                        continue;
                    }

                    Color original = renderer.color;
                    cachedSpriteColors.Add(original);
                    renderer.color = new Color(original.r, original.g, original.b, ghostAlpha);
                }
            }
            else
            {
                for (int i = 0; i < cachedSpriteRenderers.Count; i++)
                {
                    var renderer = cachedSpriteRenderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    Color original = i < cachedSpriteColors.Count ? cachedSpriteColors[i] : Color.white;
                    renderer.color = original;
                }
            }
        }

        private void PaintOverlayForActive()
        {
            if (activePreview == null)
            {
                return;
            }

            BoundsInt area = activePreview.GetPlacementBounds();
            bool valid = IsPlacementValid(area);

            ClearTempOverlay();

            if (tempTilemap == null)
            {
                Debug.LogWarning("GridBuildingSystem: tempTilemap is not assigned.");
                return;
            }

            if (greenValidTile == null || redInvalidTile == null)
            {
                Debug.LogWarning("GridBuildingSystem: overlay tiles are not assigned.");
                return;
            }

            TileBase paintTile = valid ? greenValidTile : redInvalidTile;

            for (int x = area.xMin; x < area.xMax; x++)
            {
                for (int y = area.yMin; y < area.yMax; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    tempTilemap.SetTile(cell, paintTile);
                    lastPaintedCells.Add(cell);
                }
            }
        }

        private void ClearTempOverlay()
        {
            if (tempTilemap == null || lastPaintedCells.Count == 0)
            {
                return;
            }

            for (int i = 0; i < lastPaintedCells.Count; i++)
            {
                tempTilemap.SetTile(lastPaintedCells[i], null);
            }

            lastPaintedCells.Clear();
        }

        private bool IsPlacementValid(BoundsInt area)
        {
            if (mainTilemap == null)
            {
                Debug.LogWarning("GridBuildingSystem: mainTilemap is not assigned.");
                return false;
            }

            for (int x = area.xMin; x < area.xMax; x++)
            {
                for (int y = area.yMin; y < area.yMax; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);

                    if (occupiedCells.Contains(cell))
                    {
                        return false;
                    }

                    TileBase tile = mainTilemap.GetTile(cell);
                    if (tile == null || tile != whiteBuildableTile)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void RegisterOccupied(BoundsInt area)
        {
            for (int x = area.xMin; x < area.xMax; x++)
            {
                for (int y = area.yMin; y < area.yMax; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    occupiedCells.Add(cell);
                }
            }
        }

        private void UnregisterOccupied(BoundsInt area)
        {
            for (int x = area.xMin; x < area.xMax; x++)
            {
                for (int y = area.yMin; y < area.yMax; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    occupiedCells.Remove(cell);
                }
            }
        }

        private static void AssignInspectorTarget(GameObject root, BuildingType type)
        {
            if (root == null || type == null)
            {
                return;
            }

            var target = root.GetComponent<BuildingInspectorTarget>();
            if (target == null)
            {
                target = root.AddComponent<BuildingInspectorTarget>();
            }

            target.SetBuildingType(type);
        }

        private static void AssignUpgradable(GameObject root, BuildingType type)
        {
            if (root == null || type == null)
            {
                return;
            }

            var upgradable = root.GetComponent<BuildingUpgradable>();
            if (upgradable == null)
            {
                upgradable = root.AddComponent<BuildingUpgradable>();
            }

            upgradable.SetBuildingType(type);
        }

        private static void EnsureIsoSorter(GameObject root, bool updateEveryFrame)
        {
            if (root == null)
            {
                return;
            }

            var sorter = root.GetComponent<IsoSortByY>();
            if (sorter == null)
            {
                sorter = root.AddComponent<IsoSortByY>();
            }

            // Keep sorting consistent across buildables and resource nodes:
            // use transform Y as a shared anchor.
            sorter.Configure(false, Vector3.zero, 100, 0);
            sorter.SetUpdateEveryFrame(updateEveryFrame);
            sorter.ApplySortingNow();
        }

        private Vector3 GetAnchorWorldPosition(Vector3Int cellPos)
        {
            if (gridLayout == null)
            {
                return Vector3.zero;
            }

            return gridLayout.CellToWorld(cellPos);
        }

        private void CachePreviewCenterOffset()
        {
            previewCenterOffsetWorld = Vector3.zero;
            if (activePreviewObject == null)
            {
                return;
            }

            previewBoundsRenderers.Clear();
            activePreviewObject.GetComponentsInChildren(true, previewBoundsRenderers);
            if (previewBoundsRenderers.Count == 0)
            {
                return;
            }

            bool hasBounds = false;
            Bounds combined = default;
            for (int i = 0; i < previewBoundsRenderers.Count; i++)
            {
                var renderer = previewBoundsRenderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combined = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                return;
            }

            previewCenterOffsetWorld = combined.center - activePreviewObject.transform.position;
            previewCenterOffsetWorld.z = 0f;
        }

        private Transform ResolvePlacementParent()
        {
            if (placementParent != null)
            {
                return placementParent;
            }

            var elements = GameObject.Find("Elements");
            if (elements != null)
            {
                placementParent = elements.transform;
                return placementParent;
            }

            var anyNode = Object.FindAnyObjectByType<FiniteResourceNode>();
            if (anyNode != null && anyNode.transform.parent != null)
            {
                placementParent = anyNode.transform.parent;
                return placementParent;
            }

            return null;
        }

        private static void DisableBuildMenuForPlacedType(BuildingType type)
        {
            if (type == null)
            {
                return;
            }

            var hud = Object.FindAnyObjectByType<GameHUDController>();
            if (hud != null)
            {
                hud.HideFromBuildMenu(type);
            }
        }
        #endregion


        private void ClearPlacedBuildingsForRestore()
        {
            var buildings = Object.FindObjectsByType<Building>(FindObjectsSortMode.None);
            for (int i = 0; i < buildings.Length; i++)
            {
                var building = buildings[i];
                if (building == null)
                {
                    continue;
                }

                if (!IsRuntimePlacedBuilding(building))
                {
                    continue;
                }

                // Scene-authored Town Hall should not be serialized as a placed building.
                if (IsPersistentSceneBuilding(building))
                {
                    continue;
                }

                var upgradable = building.GetComponent<BuildingUpgradable>();
                if (upgradable == null)
                {
                    upgradable = building.GetComponentInChildren<BuildingUpgradable>();
                }

                var inspector = building.GetComponent<BuildingInspectorTarget>();
                if (inspector == null)
                {
                    inspector = building.GetComponentInChildren<BuildingInspectorTarget>();
                }

                if (upgradable == null && inspector == null)
                {
                    continue;
                }

                Destroy(building.gameObject);
            }
        }
        private static void MarkAsRuntimePlaced(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            if (root.GetComponent<RuntimePlacedBuildingMarker>() == null)
            {
                root.AddComponent<RuntimePlacedBuildingMarker>();
            }
        }

        private static bool IsRuntimePlacedBuilding(Building building)
        {
            if (building == null)
            {
                return false;
            }

            return building.GetComponent<RuntimePlacedBuildingMarker>() != null ||
                   building.GetComponentInParent<RuntimePlacedBuildingMarker>() != null;
        }
        private static bool IsPersistentSceneBuilding(Building building)
        {
            if (building == null)
            {
                return false;
            }

            var townHall = building.GetComponent<TownHallCity>();
            if (townHall == null)
            {
                townHall = building.GetComponentInChildren<TownHallCity>(true);
            }
            if (townHall == null)
            {
                townHall = building.GetComponentInParent<TownHallCity>();
            }

            return townHall != null;
        }
        private static string GetBuildingTypeKey(BuildingType type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            return global::System.String.IsNullOrWhiteSpace(type.Id) ? type.name : type.Id;
        }

        #region Costs
        private bool CanAfford(BuildingType type)
        {
            var manager = ResourceManager.Instance;
            if (manager == null)
            {
                return true;
            }

            if (type != null && type.DebugFreeBuild)
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

                if (manager.Get(cost.resourceType) < cost.amount)
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

            if (type != null && type.DebugFreeBuild)
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

                manager.Spend(cost.resourceType, cost.amount);
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
        #endregion
    }
}
















