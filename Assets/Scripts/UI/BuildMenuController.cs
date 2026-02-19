using UnityEngine;
using UnityEngine.UIElements;
using IdleHra.BuildingSystem;

[RequireComponent(typeof(UIDocument))]
public class BuildMenuController : MonoBehaviour
{
    [System.Serializable]
    private class BuildButtonEntry
    {
        public string itemRootName;
        public string buttonName;
        public BuildingType buildingType;
        public string costContainerName = "cost";
    }

    [System.Serializable]
    private struct ResourceIcon
    {
        public ResourceType resourceType;
        public Sprite icon;
    }

    [SerializeField] private bool startHidden = true;
    [SerializeField] private string closeButtonName = "close";
    [SerializeField] private GridBuildingSystem gridBuildingSystem;
    [SerializeField] private BuildButtonEntry[] buildButtons;
    [SerializeField] private ResourceIcon[] resourceIcons;

    private UIDocument _document;
    private VisualElement _root;
    private VisualElement _buildMenu;
    private Button _closeButton;
    private struct ButtonBinding
    {
        public Button Button;
        public System.Action Handler;
    }

    private readonly System.Collections.Generic.List<ButtonBinding> _wiredButtons =
        new System.Collections.Generic.List<ButtonBinding>();
    private readonly System.Collections.Generic.Dictionary<ResourceType, Sprite> _iconLookup =
        new System.Collections.Generic.Dictionary<ResourceType, Sprite>();

    public bool IsOpen
    {
        get
        {
            if (_buildMenu == null)
            {
                return false;
            }

            var display = _buildMenu.style.display;
            if (display.keyword == StyleKeyword.Undefined || display.keyword == StyleKeyword.Null)
            {
                return _buildMenu.resolvedStyle.display != DisplayStyle.None;
            }

            return display.value != DisplayStyle.None;
        }
    }

    private void OnEnable()
    {
        _document = GetComponent<UIDocument>();
        _root = _document.rootVisualElement;
        _buildMenu = _root.Q<VisualElement>("BuildMenu");

        if (_buildMenu == null)
        {
            Debug.LogWarning("BuildMenuController: VisualElement 'BuildMenu' not found.");
            return;
        }

        _closeButton = _buildMenu.Q<Button>(closeButtonName);
        if (_closeButton != null)
        {
            _closeButton.clicked += CloseBuildMenu;
        }
        else
        {
            Debug.LogWarning($"BuildMenuController: Button '{closeButtonName}' not found in BuildMenu.");
        }

        if (startHidden)
        {
            Hide();
        }

        BuildIconLookup();
        WireBuildButtons();
    }

    private void OnDisable()
    {
        if (_closeButton != null)
        {
            _closeButton.clicked -= CloseBuildMenu;
        }

        UnwireBuildButtons();
    }

    public void Show()
    {
        if (_buildMenu == null)
        {
            return;
        }

        _buildMenu.style.display = DisplayStyle.Flex;
        Debug.Log("BuildMenu opened.");
    }

    public void Hide()
    {
        if (_buildMenu == null)
        {
            return;
        }

        _buildMenu.style.display = DisplayStyle.None;
        CancelPlacement();
        Debug.Log("BuildMenu closed.");
    }

    public void Toggle()
    {
        if (_buildMenu == null)
        {
            return;
        }

        if (IsOpen)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    public void CloseBuildMenu()
    {
        Hide();
    }

    private void CancelPlacement()
    {
        if (gridBuildingSystem == null)
        {
            gridBuildingSystem = Object.FindAnyObjectByType<GridBuildingSystem>();
        }

        if (gridBuildingSystem != null)
        {
            gridBuildingSystem.CancelPlacement();
        }
    }

    private void WireBuildButtons()
    {
        if (_buildMenu == null || buildButtons == null || buildButtons.Length == 0)
        {
            return;
        }

        if (gridBuildingSystem == null)
        {
            gridBuildingSystem = Object.FindAnyObjectByType<GridBuildingSystem>();
        }

        for (int i = 0; i < buildButtons.Length; i++)
        {
            var entry = buildButtons[i];
            if (entry == null || string.IsNullOrEmpty(entry.buttonName) || entry.buildingType == null)
            {
                continue;
            }

            LogBuildingCosts(entry);

            VisualElement itemRoot = ResolveItemRoot(entry);
            if (itemRoot == null)
            {
                Debug.LogWarning($"BuildMenuController: Item root not found for '{entry.buttonName}'.");
                continue;
            }

            var button = itemRoot.Q<Button>(entry.buttonName);
            if (button == null)
            {
                Debug.LogWarning($"BuildMenuController: Button '{entry.buttonName}' not found in BuildMenu.");
                continue;
            }

            SetupCostUI(itemRoot, entry);

            System.Action handler = () =>
            {
                if (gridBuildingSystem == null)
                {
                    Debug.LogWarning("BuildMenuController: GridBuildingSystem not found.");
                    return;
                }

                gridBuildingSystem.InitializeWithBuildingType(entry.buildingType);
            };

            button.clicked += handler;

            _wiredButtons.Add(new ButtonBinding { Button = button, Handler = handler });
        }
    }

    private void UnwireBuildButtons()
    {
        if (_wiredButtons.Count == 0)
        {
            return;
        }

        for (int i = 0; i < _wiredButtons.Count; i++)
        {
            var binding = _wiredButtons[i];
            if (binding.Button != null && binding.Handler != null)
            {
                binding.Button.clicked -= binding.Handler;
            }
        }

        _wiredButtons.Clear();
    }

    private void BuildIconLookup()
    {
        _iconLookup.Clear();

        if (resourceIcons == null)
        {
            return;
        }

        for (int i = 0; i < resourceIcons.Length; i++)
        {
            var entry = resourceIcons[i];
            if (entry.resourceType == null || entry.icon == null)
            {
                continue;
            }

            if (!_iconLookup.ContainsKey(entry.resourceType))
            {
                _iconLookup.Add(entry.resourceType, entry.icon);
            }
        }
    }

    private void SetupCostUI(VisualElement itemRoot, BuildButtonEntry entry)
    {
        if (itemRoot == null || entry == null || entry.buildingType == null)
        {
            return;
        }

        string costName = string.IsNullOrEmpty(entry.costContainerName) ? "cost" : entry.costContainerName;
        var costRoot = itemRoot.Q<VisualElement>(costName);
        if (costRoot == null)
        {
            Debug.LogWarning($"BuildMenuController: Cost container '{costName}' not found for '{entry.buttonName}'.");
            return;
        }

        Debug.Log($"BuildMenuController: '{entry.buildingType.name}' cost element type = {costRoot.GetType().Name}");

        var costs = entry.buildingType.Costs;
        if (costRoot is Label costLabel)
        {
            // Fallback for older UXML where "cost" is still a Label.
            costLabel.text = BuildCostText(costs);
            return;
        }

        costRoot.Clear();
        costRoot.style.flexDirection = FlexDirection.Row;
        costRoot.style.justifyContent = Justify.Center;
        costRoot.style.alignItems = Align.Center;

        if (costs == null || costs.Count == 0)
        {
            costRoot.Add(new Label("Free"));
            return;
        }

        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost.resourceType == null || cost.amount <= 0)
            {
                continue;
            }

            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.marginRight = 6;

            if (_iconLookup.TryGetValue(cost.resourceType, out var icon) && icon != null)
            {
                var iconElement = new VisualElement();
                iconElement.style.width = 18;
                iconElement.style.height = 18;
                iconElement.style.backgroundImage = new StyleBackground(icon);
                iconElement.style.marginRight = 2;
                item.Add(iconElement);

                item.Add(new Label(cost.amount.ToString()));
            }
            else
            {
                item.Add(new Label($"{cost.amount} {cost.resourceType.DisplayName}"));
            }

            costRoot.Add(item);
        }
    }

    private VisualElement ResolveItemRoot(BuildButtonEntry entry)
    {
        if (entry == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(entry.itemRootName))
        {
            return _buildMenu.Q<VisualElement>(entry.itemRootName);
        }

        // Fallback for older setup: search from build menu root.
        return _buildMenu;
    }

    private string BuildCostText(System.Collections.Generic.IReadOnlyList<BuildingType.ResourceCost> costs)
    {
        if (costs == null || costs.Count == 0)
        {
            return "Free";
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost.resourceType == null || cost.amount <= 0)
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append(" ");
            }

            sb.Append(cost.amount).Append(" ").Append(cost.resourceType.DisplayName);
        }

        return sb.Length == 0 ? "Free" : sb.ToString();
    }

    private void LogBuildingCosts(BuildButtonEntry entry)
    {
        if (entry == null || entry.buildingType == null)
        {
            return;
        }

        var costs = entry.buildingType.Costs;
        if (costs == null || costs.Count == 0)
        {
            Debug.Log($"BuildMenuController: '{entry.buildingType.name}' costs are EMPTY.");
            return;
        }

        string line = $"BuildMenuController: '{entry.buildingType.name}' costs:";
        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost.resourceType == null)
            {
                line += $" [null x{cost.amount}]";
            }
            else
            {
                line += $" {cost.amount} {cost.resourceType.DisplayName}";
            }
        }

        Debug.Log(line);
    }
}
