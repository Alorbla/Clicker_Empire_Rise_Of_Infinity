using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class ResourceTextTMP : MonoBehaviour
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private string prefix = "";

    private TMP_Text label;
    private ResourceManager manager;

    private void Awake()
    {
        label = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        manager = ResourceManager.Instance;
        if (manager != null)
        {
            manager.ResourceChanged += HandleResourceChanged;
            Refresh();
        }
    }

    private void OnDisable()
    {
        if (manager != null)
        {
            manager.ResourceChanged -= HandleResourceChanged;
        }
    }

    private void HandleResourceChanged(ResourceType type, int value)
    {
        if (type == resourceType)
        {
            SetText(value);
        }
    }

    private void Refresh()
    {
        if (manager == null || resourceType == null)
        {
            return;
        }

        SetText(manager.Get(resourceType));
    }

    private void SetText(int value)
    {
        var name = resourceType != null ? resourceType.DisplayName : "Resource";
        string formatted = NumberFormatter.Format(value);
        label.text = string.IsNullOrEmpty(prefix) ? $"{name}: {formatted}" : $"{prefix}{formatted}";
        if (resourceType != null)
        {
            label.color = resourceType.Color;
        }
    }
}
