using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class StorageLabelUIToolkit : MonoBehaviour
{
    [SerializeField] private string labelName = "storage_label";
    [SerializeField] private string barName = "storage_fill";
    [SerializeField] private string format = "Storage {0}/{1}";

    private UIDocument _doc;
    private Label _label;
    private VisualElement _bar;
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
            _manager.StorageChanged -= HandleStorageChanged;
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
        _bar = root?.Q<VisualElement>(barName);

        if (_label == null)
        {
            Debug.LogWarning($"StorageLabelUIToolkit: Label '{labelName}' not found on UIDocument.");
        }

        if (_bar == null && !string.IsNullOrEmpty(barName))
        {
            Debug.LogWarning($"StorageLabelUIToolkit: Bar '{barName}' not found on UIDocument.");
        }
    }

    private void HookManager()
    {
        _manager = ResourceManager.Instance;
        if (_manager != null)
        {
            _manager.StorageChanged -= HandleStorageChanged;
            _manager.StorageChanged += HandleStorageChanged;
        }
    }

    private void HandleStorageChanged(int used, int capacity)
    {
        SetText(used, capacity);
    }

    private void Refresh()
    {
        if (_manager == null)
        {
            return;
        }

        SetText(_manager.TotalStored, _manager.StorageCapacity);
    }

    private void SetText(int used, int capacity)
    {
        if (_label == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(format))
        {
            _label.text = $"{used}/{capacity}";
        }
        else
        {
            _label.text = string.Format(format, used, capacity);
        }

        UpdateBar(used, capacity);
    }

    private void UpdateBar(int used, int capacity)
    {
        if (_bar == null)
        {
            return;
        }

        float ratio = capacity <= 0 ? 0f : Mathf.Clamp01((float)used / capacity);
        _bar.style.width = Length.Percent(ratio * 100f);
        _bar.style.backgroundColor = new StyleColor(GetFillColor(ratio));
    }

    private static Color GetFillColor(float ratio)
    {
        if (ratio <= 0.6f)
        {
            return new Color(0.35f, 0.85f, 0.35f);
        }
        if (ratio <= 0.85f)
        {
            return new Color(0.95f, 0.75f, 0.2f);
        }

        return new Color(0.9f, 0.35f, 0.3f);
    }
}
