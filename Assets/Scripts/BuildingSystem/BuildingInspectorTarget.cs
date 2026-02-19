using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.EventSystems;

namespace IdleHra.BuildingSystem
{
    public sealed class BuildingInspectorTarget : MonoBehaviour
    {
        [SerializeField] private BuildingType buildingType;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private bool ignoreWhenPointerOverUI = true;

        private Collider2D cachedCollider;
        private BuildingProducer cachedProducer;
        private BuildingUpgradable cachedUpgradable;

        public BuildingType BuildingType => buildingType;

        private void Awake()
        {
            cachedCollider = GetComponent<Collider2D>();
            if (cachedCollider == null)
            {
                cachedCollider = GetComponentInChildren<Collider2D>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            cachedProducer = GetComponent<BuildingProducer>();
            if (cachedProducer == null)
            {
                cachedProducer = GetComponentInChildren<BuildingProducer>();
            }

            cachedUpgradable = GetComponent<BuildingUpgradable>();
            if (cachedUpgradable == null)
            {
                cachedUpgradable = GetComponentInChildren<BuildingUpgradable>();
            }
        }

        public void SetBuildingType(BuildingType type)
        {
            buildingType = type;
        }

        private void Update()
        {
            if (!TryGetPressScreenPosition(out var pressScreenPos, out var pointerId, out var isTouch))
            {
                return;
            }

            if (ignoreWhenPointerOverUI && EventSystem.current != null)
            {
                if (isTouch)
                {
                    if (EventSystem.current.IsPointerOverGameObject(pointerId))
                    {
                        return;
                    }
                }
                else if (EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }
            }

            if (cachedCollider == null || targetCamera == null)
            {
                return;
            }

            Vector2 worldPos = targetCamera.ScreenToWorldPoint(pressScreenPos);

            if (!cachedCollider.OverlapPoint(worldPos))
            {
                return;
            }

            if (cachedProducer == null)
            {
                cachedProducer = GetComponent<BuildingProducer>();
                if (cachedProducer == null)
                {
                    cachedProducer = GetComponentInChildren<BuildingProducer>();
                }
            }

            if (cachedUpgradable == null)
            {
                cachedUpgradable = GetComponent<BuildingUpgradable>();
                if (cachedUpgradable == null)
                {
                    cachedUpgradable = GetComponentInChildren<BuildingUpgradable>();
                }
            }

            var hud = Object.FindAnyObjectByType<GameHUDController>();
            if (hud == null)
            {
                return;
            }

            hud.CloseAllPanels();
            hud.ShowInspectorFor(buildingType, cachedProducer, cachedUpgradable);
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
}


