using System;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class ResourceLabelUIToolkit : MonoBehaviour
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private string labelName = "wood_label";
    [SerializeField] private string prefix = "";
    [SerializeField] private bool matchById = true;
    [SerializeField] private string resourceIdOverride = "";
    [SerializeField] private bool applyResourceColor = false;

    private UIDocument _doc;
    private Label _label;
    private ResourceManager _manager;
    private bool _isBound;

    private void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        _isBound = false;
        TryBind();
    }

    private void OnDisable()
    {
        if (_manager != null)
        {
            _manager.ResourceChanged -= HandleResourceChanged;
        }
    }

    private void Update()
    {
        if (!_isBound)
        {
            TryBind();
        }
    }

    private void TryBind()
    {
        if (_label == null)
        {
            BindLabel();
        }

        if (_manager == null)
        {
            HookManager();
        }

        if (_label != null && _manager != null)
        {
            _isBound = true;
            Refresh();
        }
    }

    private void BindLabel()
    {
        if (_doc == null)
        {
            return;
        }

        var root = _doc.rootVisualElement;
        _label = root?.Q<Label>(labelName);

        if (_label == null)
        {
            Debug.LogWarning($"ResourceLabelUIToolkit: Label '{labelName}' not found on UIDocument.");
        }
    }

    private void HookManager()
    {
        _manager = ResourceManager.Instance;
        if (_manager != null)
        {
            _manager.ResourceChanged -= HandleResourceChanged;
            _manager.ResourceChanged += HandleResourceChanged;
        }
    }

    private void HandleResourceChanged(ResourceType type, int value)
    {
        if (IsTargetResource(type))
        {
            SetText(value);
        }
    }

    private void Refresh()
    {
        if (_manager == null || resourceType == null || _label == null)
        {
            return;
        }

        SetText(_manager.Get(resourceType));
    }

    private bool IsTargetResource(ResourceType type)
    {
        if (type == null)
        {
            return false;
        }

        if (resourceType != null && type == resourceType)
        {
            return true;
        }

        if (!matchById)
        {
            return false;
        }

        var targetId = !string.IsNullOrEmpty(resourceIdOverride)
            ? resourceIdOverride
            : resourceType != null ? resourceType.Id : "";

        if (string.IsNullOrEmpty(targetId))
        {
            return false;
        }

        return string.Equals(type.Id, targetId, StringComparison.OrdinalIgnoreCase);
    }

    private void SetText(int value)
    {
        if (_label == null)
        {
            return;
        }

        string formatted = NumberFormatter.Format(value);
        _label.text = string.IsNullOrEmpty(prefix) ? formatted : $"{prefix}{formatted}";
        if (applyResourceColor && resourceType != null)
        {
            _label.style.color = new StyleColor(resourceType.Color);
        }
    }
}



