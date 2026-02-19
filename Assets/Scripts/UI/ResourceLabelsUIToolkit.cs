using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class ResourceLabelsUIToolkit : MonoBehaviour
{
    [Serializable]
    private class ResourceLabelEntry
    {
        public string resourceId;
        public ResourceType resourceType;
        public string labelName;
        public string prefix;
        public bool applyResourceColor;

        [NonSerialized] public Label label;
    }

    [SerializeField] private List<ResourceLabelEntry> entries = new List<ResourceLabelEntry>();

    private UIDocument _doc;
    private ResourceManager _manager;
    private bool _isBound;

    private readonly Dictionary<ResourceType, ResourceLabelEntry> _typeToEntry =
        new Dictionary<ResourceType, ResourceLabelEntry>();

    private readonly Dictionary<string, ResourceLabelEntry> _idToEntry =
        new Dictionary<string, ResourceLabelEntry>(StringComparer.OrdinalIgnoreCase);

    private Dictionary<string, ResourceType> _resourceTypeById;

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
        if (_doc == null)
        {
            return;
        }

        var root = _doc.rootVisualElement;
        if (root == null)
        {
            return;
        }

        if (_manager == null)
        {
            HookManager();
        }

        BuildCaches(root);

        if (_manager != null)
        {
            _isBound = true;
            RefreshAll();
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

    private void BuildCaches(VisualElement root)
    {
        _typeToEntry.Clear();
        _idToEntry.Clear();

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null)
            {
                continue;
            }

            entry.label = !string.IsNullOrEmpty(entry.labelName)
                ? root.Q<Label>(entry.labelName)
                : null;

            if (entry.label == null && !string.IsNullOrEmpty(entry.labelName))
            {
                Debug.LogWarning($"ResourceLabelsUIToolkit: Label '{entry.labelName}' not found.");
            }

            if (entry.resourceType == null && !string.IsNullOrEmpty(entry.resourceId))
            {
                entry.resourceType = FindResourceTypeById(entry.resourceId);
            }

            if (entry.resourceType != null && !_typeToEntry.ContainsKey(entry.resourceType))
            {
                _typeToEntry.Add(entry.resourceType, entry);
            }

            if (!string.IsNullOrEmpty(entry.resourceId) && !_idToEntry.ContainsKey(entry.resourceId))
            {
                _idToEntry.Add(entry.resourceId, entry);
            }
        }
    }

    private ResourceType FindResourceTypeById(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        if (_resourceTypeById == null)
        {
            _resourceTypeById = new Dictionary<string, ResourceType>(StringComparer.OrdinalIgnoreCase);
            var allTypes = Resources.FindObjectsOfTypeAll<ResourceType>();
            for (int i = 0; i < allTypes.Length; i++)
            {
                var type = allTypes[i];
                if (type == null || string.IsNullOrEmpty(type.Id))
                {
                    continue;
                }

                if (!_resourceTypeById.ContainsKey(type.Id))
                {
                    _resourceTypeById.Add(type.Id, type);
                }
            }
        }

        return _resourceTypeById.TryGetValue(id, out var found) ? found : null;
    }

    private void HandleResourceChanged(ResourceType type, int value)
    {
        if (type == null)
        {
            return;
        }

        if (_typeToEntry.TryGetValue(type, out var entry))
        {
            SetText(entry, value);
            return;
        }

        if (!string.IsNullOrEmpty(type.Id) && _idToEntry.TryGetValue(type.Id, out entry))
        {
            SetText(entry, value);
        }
    }

    private void RefreshAll()
    {
        if (_manager == null)
        {
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || entry.label == null || entry.resourceType == null)
            {
                continue;
            }

            SetText(entry, _manager.Get(entry.resourceType));
        }
    }

    private void SetText(ResourceLabelEntry entry, int value)
    {
        if (entry == null || entry.label == null)
        {
            return;
        }

        string formatted = NumberFormatter.Format(value);
        entry.label.text = string.IsNullOrEmpty(entry.prefix)
            ? formatted
            : $"{entry.prefix}{formatted}";

        if (entry.applyResourceColor && entry.resourceType != null)
        {
            entry.label.style.color = new StyleColor(entry.resourceType.Color);
        }
    }
}
