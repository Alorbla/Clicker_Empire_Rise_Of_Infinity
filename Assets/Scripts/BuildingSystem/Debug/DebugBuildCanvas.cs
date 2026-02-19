using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace IdleHra.BuildingSystem
{
    public sealed class DebugBuildCanvas : MonoBehaviour
    {
        [SerializeField] private DebugBuildButton debugBuildButton;
        [SerializeField] private string buttonLabel = "Place House (Debug)";
        [SerializeField] private Vector2 anchoredPosition = new Vector2(140f, -80f);
        [SerializeField] private Vector2 size = new Vector2(220f, 44f);

        private void Awake()
        {
            EnsureEventSystem();
            EnsureDebugButton();
            CreateCanvasAndButton();
        }

        private void EnsureDebugButton()
        {
            if (debugBuildButton == null)
            {
                debugBuildButton = GetComponent<DebugBuildButton>();
            }

            if (debugBuildButton == null)
            {
                debugBuildButton = gameObject.AddComponent<DebugBuildButton>();
            }
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private void CreateCanvasAndButton()
        {
            var canvasGo = new GameObject("DebugCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGo.AddComponent<GraphicRaycaster>();

            var buttonGo = new GameObject("PlaceHouseButton");
            buttonGo.transform.SetParent(canvasGo.transform, false);

            var rect = buttonGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = buttonGo.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            var button = buttonGo.AddComponent<Button>();
            button.onClick.AddListener(debugBuildButton.StartPlaceHouse);

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(buttonGo.transform, false);

            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGo.AddComponent<Text>();
            text.text = buttonLabel;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
