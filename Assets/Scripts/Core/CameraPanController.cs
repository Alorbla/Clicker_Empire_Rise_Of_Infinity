using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class CameraPanController : MonoBehaviour
{
    [Header("WASD")]
    [SerializeField] private float keyboardSpeed = 6f;

    [Header("Edge Scroll")]
    [SerializeField] private bool enableEdgeScroll = true;
    [SerializeField] private float edgeSpeed = 10f;
    [SerializeField] private float edgeThreshold = 16f;

    [Header("Options")]
    [SerializeField] private bool ignoreWhenPointerOverUI = true;
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Dialogue")]
    [SerializeField] private string movedFlagKey = "CameraMoved";
    [SerializeField] private float moveEpsilon = 0.01f;
    [SerializeField] private float zoomEpsilon = 0.01f;

    [Header("Zoom")]
    [SerializeField] private bool enableZoom = true;
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minZoom = 3f;
    [SerializeField] private float maxZoom = 12f;

    [Header("Touch")]
    [SerializeField] private bool enableTouchPan = true;
    [SerializeField] private bool enableTouchPinchZoom = true;
    [SerializeField] private float touchPanMultiplier = 1f;
    [SerializeField] private float touchPinchZoomSpeed = 0.01f;

    [Header("World Bounds")]
    [SerializeField] private bool clampToBounds = true;
    [SerializeField] private Collider2D worldBoundsCollider;
    [SerializeField] private Vector2 boundsPadding = Vector2.zero;

    private Camera _camera;
    private Vector3 _lastPosition;
    private float _lastZoom;
    private bool _hasBaseline;
    private bool _flagSet;

    private void Awake()
    {
        CacheCamera();
        _lastPosition = transform.position;
        _lastZoom = GetCurrentZoom();
        _hasBaseline = true;
    }

    private void Update()
    {
        Vector2 move = GetKeyboardMove() * keyboardSpeed;
        if (enableEdgeScroll)
        {
            move += GetEdgeMove() * edgeSpeed;
        }

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        Vector3 delta = new Vector3(move.x, move.y, 0f) * dt;
        transform.position += delta;

        if (enableZoom)
        {
            ApplyZoom(dt);
        }

        ApplyTouchControls();

        if (clampToBounds)
        {
            ClampToWorldBounds();
        }

        TryFlagCameraMoved();
    }

    private Vector2 GetKeyboardMove()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        float x = 0f;
        float y = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;

        return new Vector2(x, y);
    }

    private Vector2 GetEdgeMove()
    {
        var mouseDevice = Mouse.current;
        if (mouseDevice == null)
        {
            return Vector2.zero;
        }

        Vector2 move = Vector2.zero;
        Vector2 mouse = mouseDevice.position.ReadValue();

        if (mouse.x <= edgeThreshold) move.x -= 1f;
        if (mouse.x >= Screen.width - edgeThreshold) move.x += 1f;
        if (mouse.y <= edgeThreshold) move.y -= 1f;
        if (mouse.y >= Screen.height - edgeThreshold) move.y += 1f;

        return move;
    }

    private void ApplyZoom(float dt)
    {
        var mouseDevice = Mouse.current;
        if (mouseDevice == null)
        {
            return;
        }

        float scroll = mouseDevice.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f))
        {
            return;
        }

        if (ShouldIgnoreMouseWheelZoom(mouseDevice.position.ReadValue()))
        {
            return;
        }

        if (!CacheCamera())
        {
            return;
        }

        float direction = -Mathf.Sign(scroll);
        float amount = zoomSpeed * direction * dt;

        if (_camera.orthographic)
        {
            float size = Mathf.Clamp(_camera.orthographicSize + amount, minZoom, maxZoom);
            _camera.orthographicSize = size;
        }
        else
        {
            float fov = Mathf.Clamp(_camera.fieldOfView + amount, minZoom, maxZoom);
            _camera.fieldOfView = fov;
        }
    }

    private void ApplyTouchControls()
    {
        var touchscreen = Touchscreen.current;
        if (touchscreen == null || !CacheCamera())
        {
            return;
        }

        var activeTouches = touchscreen.touches;
        TouchControl first = null;
        TouchControl second = null;

        for (int i = 0; i < activeTouches.Count; i++)
        {
            var touch = activeTouches[i];
            if (!touch.press.isPressed)
            {
                continue;
            }

            if (first == null)
            {
                first = touch;
            }
            else
            {
                second = touch;
                break;
            }
        }

        if (first == null)
        {
            return;
        }

        if (ignoreWhenPointerOverUI)
        {
            if (IsTouchOverUI(first) || (second != null && IsTouchOverUI(second)))
            {
                return;
            }
        }

        if (second != null && enableTouchPinchZoom && enableZoom)
        {
            ApplyTouchPinchZoom(first, second);
            return;
        }

        if (enableTouchPan)
        {
            ApplyTouchPan(first);
        }
    }

    private void ApplyTouchPan(TouchControl touch)
    {
        Vector2 delta = touch.delta.ReadValue();
        if (delta.sqrMagnitude <= 0f)
        {
            return;
        }

        Vector2 currentPos = touch.position.ReadValue();
        Vector2 previousPos = currentPos - delta;

        Vector3 currentWorld = _camera.ScreenToWorldPoint(new Vector3(currentPos.x, currentPos.y, -_camera.transform.position.z));
        Vector3 previousWorld = _camera.ScreenToWorldPoint(new Vector3(previousPos.x, previousPos.y, -_camera.transform.position.z));
        Vector3 worldDelta = currentWorld - previousWorld;

        transform.position -= worldDelta * touchPanMultiplier;
    }

    private void ApplyTouchPinchZoom(TouchControl first, TouchControl second)
    {
        Vector2 firstPos = first.position.ReadValue();
        Vector2 secondPos = second.position.ReadValue();
        Vector2 firstPrev = firstPos - first.delta.ReadValue();
        Vector2 secondPrev = secondPos - second.delta.ReadValue();

        float prevDistance = Vector2.Distance(firstPrev, secondPrev);
        float currentDistance = Vector2.Distance(firstPos, secondPos);
        float deltaDistance = currentDistance - prevDistance;
        if (Mathf.Approximately(deltaDistance, 0f))
        {
            return;
        }

        float amount = -deltaDistance * touchPinchZoomSpeed;

        if (_camera.orthographic)
        {
            _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize + amount, minZoom, maxZoom);
        }
        else
        {
            _camera.fieldOfView = Mathf.Clamp(_camera.fieldOfView + amount, minZoom, maxZoom);
        }
    }

    private void ClampToWorldBounds()
    {
        if (!CacheCamera())
        {
            return;
        }

        if (worldBoundsCollider == null)
        {
            return;
        }

        Bounds bounds = worldBoundsCollider.bounds;
        float minX = bounds.min.x + boundsPadding.x;
        float maxX = bounds.max.x - boundsPadding.x;
        float minY = bounds.min.y + boundsPadding.y;
        float maxY = bounds.max.y - boundsPadding.y;

        Vector3 pos = transform.position;

        if (_camera.orthographic)
        {
            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect;

            if (maxX - minX <= halfWidth * 2f)
            {
                pos.x = (minX + maxX) * 0.5f;
            }
            else
            {
                pos.x = Mathf.Clamp(pos.x, minX + halfWidth, maxX - halfWidth);
            }

            if (maxY - minY <= halfHeight * 2f)
            {
                pos.y = (minY + maxY) * 0.5f;
            }
            else
            {
                pos.y = Mathf.Clamp(pos.y, minY + halfHeight, maxY - halfHeight);
            }
        }
        else
        {
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
        }

        transform.position = pos;
    }

    
    private static bool ShouldIgnoreMouseWheelZoom(Vector2 screenPosition)
    {
        var hud = GameHUDController.Instance;
        if (hud == null)
        {
            return false;
        }

        return hud.IsPointerOverResourceBar(screenPosition);
    }

    private static bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        return EventSystem.current.IsPointerOverGameObject();
    }

    private static bool IsTouchOverUI(TouchControl touch)
    {
        if (EventSystem.current == null || touch == null)
        {
            return false;
        }

        int pointerId = touch.touchId.ReadValue();
        return EventSystem.current.IsPointerOverGameObject(pointerId);
    }

    private bool CacheCamera()
    {
        if (_camera != null)
        {
            return true;
        }

        _camera = GetComponent<Camera>();
        return _camera != null;
    }


    public void SetZoomToMin()
    {
        if (!CacheCamera())
        {
            return;
        }

        if (_camera.orthographic)
        {
            _camera.orthographicSize = minZoom;
        }
        else
        {
            _camera.fieldOfView = minZoom;
        }
    }
    private float GetCurrentZoom()
    {
        if (!CacheCamera())
        {
            return 0f;
        }

        return _camera.orthographic ? _camera.orthographicSize : _camera.fieldOfView;
    }

    private void TryFlagCameraMoved()
    {
        if (_flagSet || string.IsNullOrEmpty(movedFlagKey))
        {
            return;
        }

        if (!_hasBaseline)
        {
            _lastPosition = transform.position;
            _lastZoom = GetCurrentZoom();
            _hasBaseline = true;
            return;
        }

        float posDelta = (transform.position - _lastPosition).sqrMagnitude;
        float zoomDelta = Mathf.Abs(GetCurrentZoom() - _lastZoom);

        if (posDelta < moveEpsilon * moveEpsilon && zoomDelta < zoomEpsilon)
        {
            return;
        }

        var controller = DialogueController.Instance;
        if (controller == null || controller.Conditions == null)
        {
            return;
        }

        controller.Conditions.SetFlag(movedFlagKey, true);
        _flagSet = true;
    }
}




