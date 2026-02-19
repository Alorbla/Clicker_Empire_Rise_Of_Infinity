using System.Collections.Generic;
using IdleHra.BuildingSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class BuildingDetailsController : MonoBehaviour
{
    [Header("Town Hall Details")]
    [SerializeField] private Sprite townHallBackgroundSprite;

    [Header("Action Cost Colors")]
    [SerializeField] private Color affordableCostColor = new Color(0.67f, 0.95f, 0.55f, 1f);
    [SerializeField] private Color missingCostColor = new Color(0.88f, 0.24f, 0.18f, 1f);

    private UIDocument document;
    private VisualElement panel;
    private VisualElement background;
    private Label titleLabel;
    private Button closeButton;
    private Label bottomTextLabel;
    private Button nodeActionButton;
    private VisualElement nodeActionCostRow;

    private BuildingType activeBuildingType;
    private ResourceManager boundResourceManager;

    private readonly Dictionary<Behaviour, bool> disabledBehaviours = new Dictionary<Behaviour, bool>();

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        BindUi();
        TryBindResourceManager();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (closeButton != null)
        {
            closeButton.clicked -= ExitDetails;
        }

        if (nodeActionButton != null)
        {
            nodeActionButton.clicked -= OnNodeActionClicked;
        }

        UnbindResourceManager();
        RestoreWorldInput();
    }

    private void Update()
    {
        if (boundResourceManager == null)
        {
            TryBindResourceManager();
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindUi();
        ExitDetails();
    }

    private void BindUi()
    {
        document = GetComponent<UIDocument>();
        if (document == null)
        {
            return;
        }

        var root = document.rootVisualElement;
        if (root == null)
        {
            return;
        }

        panel = root.Q<VisualElement>("BuildingDetailsPanel");
        background = root.Q<VisualElement>("BuildingDetailsBackground");
        titleLabel = root.Q<Label>("BuildingDetailsTitle");
        closeButton = root.Q<Button>("BuildingDetailsCloseButton");
        bottomTextLabel = root.Q<Label>("BuildingDetailsBottomText");
        nodeActionButton = root.Q<Button>("BuildingDetailsNodeActionButton");
        nodeActionCostRow = root.Q<VisualElement>("BuildingDetailsNodeActionCostRow");

        if (closeButton != null)
        {
            closeButton.clicked -= ExitDetails;
            closeButton.clicked += ExitDetails;
        }

        if (nodeActionButton != null)
        {
            nodeActionButton.clicked -= OnNodeActionClicked;
            nodeActionButton.clicked += OnNodeActionClicked;
        }

        if (panel != null)
        {
            panel.style.display = DisplayStyle.None;
        }
    }

    public void EnterTownHallDetails(TownHallCity townHall)
    {
        activeBuildingType = null;
        string cityName = townHall != null ? townHall.DisplayName : "Town Hall";
        string title = string.IsNullOrWhiteSpace(cityName) ? "Town Hall - Details" : cityName + " - Details";
        EnterDetails(title, townHallBackgroundSprite, "[BuildingDetails] TownHall background sprite is not assigned. Assign it in BuildingDetailsController.");
        ShowNoActionState();
    }

    public bool EnterBuildingDetails(BuildingType type)
    {
        if (type == null)
        {
            return false;
        }

        if (!type.HasDetailsView)
        {
            return false;
        }

        activeBuildingType = type;
        EnterDetails(type.DisplayName + " - Details", type.DetailsBackgroundSprite, null);
        RefreshNodeActionUi();
        return true;
    }

    public void ExitDetails()
    {
        activeBuildingType = null;

        if (panel != null)
        {
            panel.style.display = DisplayStyle.None;
        }

        RestoreWorldInput();
    }

    private void EnterDetails(string title, Sprite backgroundSprite, string missingSpriteWarning)
    {
        if (panel == null)
        {
            BindUi();
            if (panel == null)
            {
                return;
            }
        }

        if (titleLabel != null)
        {
            titleLabel.text = string.IsNullOrWhiteSpace(title) ? "Details" : title;
        }

        if (background != null)
        {
            if (backgroundSprite != null)
            {
                background.style.backgroundImage = new StyleBackground(backgroundSprite);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(missingSpriteWarning))
                {
                    Debug.LogWarning(missingSpriteWarning, this);
                }
                background.style.backgroundImage = null;
            }
        }

        DisableWorldInput();
        panel.style.display = DisplayStyle.Flex;
    }

    private void TryBindResourceManager()
    {
        var manager = ResourceManager.Instance != null
            ? ResourceManager.Instance
            : FindAnyObjectByType<ResourceManager>();

        if (manager == null || manager == boundResourceManager)
        {
            return;
        }

        UnbindResourceManager();
        boundResourceManager = manager;
        boundResourceManager.ResourceChanged += HandleResourceChanged;
    }

    private void UnbindResourceManager()
    {
        if (boundResourceManager == null)
        {
            return;
        }

        boundResourceManager.ResourceChanged -= HandleResourceChanged;
        boundResourceManager = null;
    }

    private void HandleResourceChanged(ResourceType _, int __)
    {
        if (panel == null || panel.resolvedStyle.display == DisplayStyle.None)
        {
            return;
        }

        RefreshNodeActionUi();
    }

    private void ShowNoActionState()
    {
        if (bottomTextLabel != null)
        {
            bottomTextLabel.text = "More Content Coming Soon";
            bottomTextLabel.style.display = DisplayStyle.Flex;
        }

        if (nodeActionButton != null)
        {
            nodeActionButton.style.display = DisplayStyle.None;
        }

        if (nodeActionCostRow != null)
        {
            nodeActionCostRow.Clear();
            nodeActionCostRow.style.display = DisplayStyle.None;
        }
    }

    private void RefreshNodeActionUi()
    {
        if (activeBuildingType == null || !activeBuildingType.HasNodeAction)
        {
            ShowNoActionState();
            return;
        }

        var manager = boundResourceManager != null
            ? boundResourceManager
            : ResourceManager.Instance;

        int depletedCount = FiniteResourceNode.CountDepleted(activeBuildingType.NodeActionTargetResource);
        bool hasDepletedTargets = depletedCount > 0;
        bool canAfford = CanAffordActionCosts(manager, activeBuildingType.NodeActionCosts);

        if (bottomTextLabel != null)
        {
            bottomTextLabel.style.display = DisplayStyle.None;
        }

        if (nodeActionButton != null)
        {
            nodeActionButton.style.display = DisplayStyle.Flex;
            string baseLabel = activeBuildingType.NodeActionLabel;
            nodeActionButton.text = hasDepletedTargets ? baseLabel : baseLabel + " (No depleted nodes)";
            nodeActionButton.SetEnabled(hasDepletedTargets && canAfford);
        }

        if (nodeActionCostRow != null)
        {
            BuildActionCostRow(nodeActionCostRow, manager, activeBuildingType.NodeActionCosts);
            nodeActionCostRow.style.display = DisplayStyle.Flex;
        }
    }

    private void OnNodeActionClicked()
    {
        if (activeBuildingType == null || !activeBuildingType.HasNodeAction)
        {
            return;
        }

        var manager = boundResourceManager != null
            ? boundResourceManager
            : ResourceManager.Instance;

        if (!CanAffordActionCosts(manager, activeBuildingType.NodeActionCosts))
        {
            RefreshNodeActionUi();
            return;
        }

        if (!SpendActionCosts(manager, activeBuildingType.NodeActionCosts))
        {
            RefreshNodeActionUi();
            return;
        }

        bool restored = FiniteResourceNode.TryRestoreOneDepleted(
            activeBuildingType.NodeActionTargetResource,
            activeBuildingType.NodeActionRestoreAmount,
            out _);

        if (!restored)
        {
            RefundActionCosts(manager, activeBuildingType.NodeActionCosts);
        }

        RefreshNodeActionUi();
    }

    private void BuildActionCostRow(VisualElement row, ResourceManager manager, IReadOnlyList<BuildingType.ResourceCost> costs)
    {
        if (row == null)
        {
            return;
        }

        row.Clear();
        if (costs == null || costs.Count == 0)
        {
            var free = new Label("Free");
            free.AddToClassList("building-details-action-cost-amount");
            free.style.color = new StyleColor(affordableCostColor);
            row.Add(free);
            return;
        }

        int visible = 0;
        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost.resourceType == null || cost.amount <= 0)
            {
                continue;
            }

            var item = new VisualElement();
            item.AddToClassList("building-details-action-cost-item");

            var icon = new VisualElement();
            icon.AddToClassList("building-details-action-cost-icon");
            if (cost.resourceType.Icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(cost.resourceType.Icon);
            }
            item.Add(icon);

            var amount = new Label(NumberFormatter.Format(cost.amount));
            amount.AddToClassList("building-details-action-cost-amount");
            bool hasEnough = manager != null && manager.Get(cost.resourceType) >= cost.amount;
            amount.style.color = new StyleColor(hasEnough ? affordableCostColor : missingCostColor);
            item.Add(amount);

            row.Add(item);
            visible++;
        }

        if (visible == 0)
        {
            var free = new Label("Free");
            free.AddToClassList("building-details-action-cost-amount");
            free.style.color = new StyleColor(affordableCostColor);
            row.Add(free);
        }
    }

    private static bool CanAffordActionCosts(ResourceManager manager, IReadOnlyList<BuildingType.ResourceCost> costs)
    {
        if (costs == null || costs.Count == 0)
        {
            return true;
        }

        if (manager == null)
        {
            return false;
        }

        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost.resourceType == null || cost.amount <= 0)
            {
                continue;
            }

            if (manager.Get(cost.resourceType) < cost.amount)
            {
                return false;
            }
        }

        return true;
    }

    private static bool SpendActionCosts(ResourceManager manager, IReadOnlyList<BuildingType.ResourceCost> costs)
    {
        if (costs == null || costs.Count == 0)
        {
            return true;
        }

        if (manager == null)
        {
            return false;
        }

        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost.resourceType == null || cost.amount <= 0)
            {
                continue;
            }

            if (!manager.Spend(cost.resourceType, cost.amount))
            {
                return false;
            }
        }

        return true;
    }

    private static void RefundActionCosts(ResourceManager manager, IReadOnlyList<BuildingType.ResourceCost> costs)
    {
        if (manager == null || costs == null)
        {
            return;
        }

        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost.resourceType == null || cost.amount <= 0)
            {
                continue;
            }

            manager.Add(cost.resourceType, cost.amount);
        }
    }

    private void DisableWorldInput()
    {
        disabledBehaviours.Clear();

        CacheAndDisable<ObjectClickBlocker<ClickableNode>>();
        CacheAndDisable<ObjectClickBlocker<TownHallClick>>();
        CacheAndDisable<ObjectClickBlocker<BuildingInspectorTarget>>();
        CacheAndDisable<ObjectClickBlocker<GridBuildingSystem>>();
        CacheAndDisable<ObjectClickBlocker<CameraPanController>>();
    }

    private void RestoreWorldInput()
    {
        if (disabledBehaviours.Count == 0)
        {
            return;
        }

        foreach (var pair in disabledBehaviours)
        {
            if (pair.Key == null)
            {
                continue;
            }

            pair.Key.enabled = pair.Value;
        }

        disabledBehaviours.Clear();
    }

    private void CacheAndDisable<TMarker>() where TMarker : IBehaviourLocator, new()
    {
        var marker = new TMarker();
        var behaviours = marker.Find();
        if (behaviours == null)
        {
            return;
        }

        for (int i = 0; i < behaviours.Length; i++)
        {
            var behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            if (!disabledBehaviours.ContainsKey(behaviour))
            {
                disabledBehaviours.Add(behaviour, behaviour.enabled);
            }

            behaviour.enabled = false;
        }
    }

    private interface IBehaviourLocator
    {
        Behaviour[] Find();
    }

    private struct ObjectClickBlocker<T> : IBehaviourLocator where T : Behaviour
    {
        public Behaviour[] Find()
        {
            var found = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            if (found == null || found.Length == 0)
            {
                return System.Array.Empty<Behaviour>();
            }

            var result = new Behaviour[found.Length];
            for (int i = 0; i < found.Length; i++)
            {
                result[i] = found[i];
            }

            return result;
        }
    }
}
