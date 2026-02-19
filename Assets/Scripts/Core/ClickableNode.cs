using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class ClickableNode : MonoBehaviour
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private int amountPerClick = 1;
    [SerializeField] private FloatingText floatingTextPrefab;
    [SerializeField] private Color floatingTextColor = new Color(1f, 0.9f, 0.2f);
    [SerializeField] private string floatingTextFormat = "+{0}";
    [SerializeField] private bool ignoreWhenPointerOverUI = false;

    private Collider2D cachedCollider;
    private Camera cachedCamera;
    private FiniteResourceNode finiteNode;

    public ResourceType ResourceType => resourceType;

    private void Awake()
    {
        cachedCollider = GetComponent<Collider2D>();
        cachedCamera = Camera.main;
        finiteNode = GetComponent<FiniteResourceNode>();
        if (finiteNode == null)
        {
            finiteNode = GetComponentInParent<FiniteResourceNode>();
        }
    }

    private void Update()
    {
        if (!TryGetPressScreenPosition(out var screenPos, out var pointerId, out var isTouch))
        {
            return;
        }

        if (ignoreWhenPointerOverUI && IsPointerOverUI(pointerId, isTouch))
        {
            return;
        }

        if (cachedCollider == null)
        {
            Debug.LogWarning("ClickableNode needs a Collider2D.");
            return;
        }

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
            if (cachedCamera == null)
            {
                Debug.LogWarning("No Main Camera found.");
                return;
            }
        }

        Vector3 worldPos3 = cachedCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cachedCamera.transform.position.z));
        Vector2 worldPos = new Vector2(worldPos3.x, worldPos3.y);

        if (cachedCollider.OverlapPoint(worldPos))
        {
            if (ResourceManager.Instance == null)
            {
                Debug.LogWarning("ResourceManager not found in scene.");
                return;
            }

            int clickAmount = amountPerClick;
            var manualClick = ManualClickSystem.Instance;
            if (manualClick != null)
            {
                clickAmount = manualClick.GetClickAmount(amountPerClick);
            }

            int requestAmount = clickAmount;
            if (finiteNode != null)
            {
                requestAmount = finiteNode.PeekAvailable(clickAmount);
                if (requestAmount <= 0)
                {
                    return;
                }
            }

            int added = ResourceManager.Instance.Add(resourceType, requestAmount);
            if (added > 0)
            {
                if (finiteNode != null)
                {
                    finiteNode.Consume(added);
                }
                SpawnFloatingText(worldPos, added);
                TryTriggerDialogueFlag();
            }
        }
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

            var touch = touchscreen.primaryTouch;
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

    private void TryTriggerDialogueFlag()
    {
        if (resourceType == null)
        {
            return;
        }

        if (!IsWoodResource(resourceType))
        {
            return;
        }

        var controller = DialogueController.Instance;
        if (controller == null || controller.Conditions == null)
        {
            return;
        }

        controller.Conditions.SetFlag("TreeClicked", true);
    }

    private static bool IsWoodResource(ResourceType type)
    {
        if (!string.IsNullOrEmpty(type.Id))
        {
            return string.Equals(type.Id, "wood", System.StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(type.name, "wood", System.StringComparison.OrdinalIgnoreCase);
    }

    private void SpawnFloatingText(Vector2 worldPos, int amount)
    {
        if (floatingTextPrefab == null)
        {
            return;
        }

        FloatingText instance = Instantiate(floatingTextPrefab, worldPos, Quaternion.identity);
        string formattedAmount = NumberFormatter.Format(amount);
        string text = string.IsNullOrEmpty(floatingTextFormat)
            ? $"+{formattedAmount}"
            : string.Format(floatingTextFormat, formattedAmount);
        instance.Init(text, floatingTextColor);
    }
}
