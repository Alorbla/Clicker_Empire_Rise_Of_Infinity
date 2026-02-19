using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Rendering;
using IdleHra.BuildingSystem;

public class TownHallClick : MonoBehaviour
{
    [SerializeField] private GameHUDController gameHUDController;
    [SerializeField] private TownHallCity townHallCity;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool ignoreWhenPointerOverUI = false;
    [Header("Sorting")]
    [SerializeField] private bool ensureIsoSorter = true;
    [SerializeField] private bool sorterUseColliderBottom = true;
    [SerializeField] private Vector3 sorterOffset = new Vector3(0f, -1f, 0f);
    [SerializeField] private int sorterFactor = 100;
    [SerializeField] private int sorterBaseOrder = 0;
    [SerializeField] private bool forceSortingLayer = true;
    [SerializeField] private string sortingLayerName = "Buildings";
    [SerializeField] private int sortingOrderOverride = 10000;
    [SerializeField] private bool applySortingToChildren = true;
    [SerializeField] private bool useSortingGroupOverride = true;
    [SerializeField] private bool preferSortingGroupForIsoSorter = false;
    [Header("Tutorial Gate")]
    [SerializeField] private bool requireTutorialComplete = true;
    [SerializeField] private string tutorialCompleteFlag = "TutorialCompleted";

    private Collider2D _collider2D;

    private void Awake()
    {
        _collider2D = GetComponent<Collider2D>();
        if (_collider2D == null)
        {
            Debug.LogWarning("TownHallClick: Collider2D missing on TownHall.");
        }

        EnsureSorter();
        ApplySortingOverride();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (gameHUDController == null)
        {
            gameHUDController = Object.FindAnyObjectByType<GameHUDController>();
        }

        if (gameHUDController == null)
        {
            Debug.LogWarning("TownHallClick: GameHUDController not found in scene.");
        }

        if (townHallCity == null)
        {
            townHallCity = GetComponent<TownHallCity>();
        }
    }

    private void EnsureSorter()
    {
        if (!ensureIsoSorter)
        {
            return;
        }

        var sorter = GetComponent<IsoSortByY>();
        if (sorter == null)
        {
            sorter = gameObject.AddComponent<IsoSortByY>();
        }

        sorter.SetPreferSortingGroup(preferSortingGroupForIsoSorter);
        sorter.SetUpdateEveryFrame(false);
        sorter.Configure(sorterUseColliderBottom, sorterOffset, sorterFactor, sorterBaseOrder);

        var group = GetComponent<SortingGroup>();
        if (group != null && !preferSortingGroupForIsoSorter)
        {
            // Avoid group-level ordering forcing TownHall above normal renderer-sorted buildings.
            group.enabled = false;
        }
        else if (group != null)
        {
            group.enabled = true;
        }
    }

    private void ApplySortingOverride()
    {
        if (!forceSortingLayer)
        {
            return;
        }

        int layerId = SortingLayer.NameToID(sortingLayerName);
        if (layerId == 0 && sortingLayerName != "Default")
        {
            Debug.LogWarning($"TownHallClick: Sorting layer '{sortingLayerName}' not found.");
        }

        if (useSortingGroupOverride && preferSortingGroupForIsoSorter)
        {
            var group = GetComponent<SortingGroup>();
            if (group == null)
            {
                group = gameObject.AddComponent<SortingGroup>();
            }

            group.enabled = true;
            group.sortingLayerID = layerId;
            if (!ensureIsoSorter)
            {
                group.sortingOrder = sortingOrderOverride;
            }
            return;
        }

        var renderers = applySortingToChildren
            ? GetComponentsInChildren<SpriteRenderer>(true)
            : GetComponents<SpriteRenderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.sortingLayerID = layerId;
            if (!ensureIsoSorter)
            {
                renderer.sortingOrder = sortingOrderOverride;
            }
        }
    }

    private void Update()
    {
        if (!TryGetPressScreenPosition(out var pressScreenPos, out var pointerId, out var isTouch))
        {
            return;
        }

        if (requireTutorialComplete)
        {
            var controller = DialogueController.Instance;
            if (controller != null && controller.Conditions != null)
            {
                if (!controller.Conditions.GetFlag(tutorialCompleteFlag))
                {
                    return;
                }
            }
        }

        if (ignoreWhenPointerOverUI && UnityEngine.EventSystems.EventSystem.current != null)
        {
            if (isTouch)
            {
                if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(pointerId))
                {
                    return;
                }
            }
            else if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }
        }

        if (_collider2D == null || targetCamera == null)
        {
            return;
        }

        Vector2 worldPos = targetCamera.ScreenToWorldPoint(pressScreenPos);

        if (!_collider2D.OverlapPoint(worldPos))
        {
            return;
        }

        if (gameHUDController == null)
        {
            gameHUDController = Object.FindAnyObjectByType<GameHUDController>();
            if (gameHUDController == null)
            {
                Debug.LogWarning("TownHallClick: GameHUDController not found on click.");
                return;
            }
        }

        gameHUDController.CloseAllPanels();
        gameHUDController.ShowTownHallUI(townHallCity);
    }

    private static bool TryGetPressScreenPosition(out Vector2 screenPos, out int pointerId, out bool isTouch)
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPos = mouse.position.ReadValue();
            pointerId = -1;
            isTouch = false;
            return true;
        }

        var touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            if (GetActiveTouchCount(touchscreen) > 1)
            {
                screenPos = default;
                pointerId = -1;
                isTouch = false;
                return false;
            }

            TouchControl touch = touchscreen.primaryTouch;
            if (touch != null && touch.press.wasPressedThisFrame)
            {
                screenPos = touch.position.ReadValue();
                pointerId = touch.touchId.ReadValue();
                isTouch = true;
                return true;
            }
        }

        screenPos = default;
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
}
