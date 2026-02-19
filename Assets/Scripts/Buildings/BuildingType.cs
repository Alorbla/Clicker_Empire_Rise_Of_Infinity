using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BuildingType", menuName = "IdleHra/Building Type")]
public class BuildingType : ScriptableObject
{
    public enum FiniteNodeActionKind
    {
        None = 0,
        Replant = 1,
        Fertile = 2,
        NewVein = 3
    }

    [Serializable]
    public struct ResourceCost
    {
        public ResourceType resourceType;
        public int amount;
    }

    [Serializable]
    public struct ResourceStorageBonus
    {
        public ResourceType resourceType;
        public int amount;
    }

    [Serializable]
    public class UpgradeLevel
    {
        public string levelName = "";
        public List<ResourceCost> costs = new List<ResourceCost>();
        public List<ResourceCost> inputResourcesPerCycle = new List<ResourceCost>();
        public int amountPerCycle = 1;
        public float intervalSeconds = 1f;
        public int manualClickAmount = 0;
        public int storageCapacityBonus = 0;
        public List<ResourceStorageBonus> storageCapacityBonuses = new List<ResourceStorageBonus>();
    }

    [Serializable]
    public class MarketTrade
    {
        public ResourceType resourceType;
        public int resourceAmount = 1;
        public int goldAmount = 1;
        public bool allowBuy = true;
        public bool allowSell = true;
    }

    [SerializeField] private string id = "";
    [SerializeField] private string displayName = "";
    [SerializeField] private GameObject prefab;
    [SerializeField] private bool usePrefabFootprint = false;
    [SerializeField] private Vector2Int footprint = Vector2Int.one;
    [SerializeField] private List<ResourceCost> costs = new List<ResourceCost>();
    [SerializeField] private List<UpgradeLevel> upgradeLevels = new List<UpgradeLevel>();
    [SerializeField] private int orderIndex = 0;
    [SerializeField] private bool showInBuildMenu = true;
    [SerializeField] private int storageCapacityBonus = 0;
    [SerializeField] private List<ResourceStorageBonus> storageCapacityBonuses = new List<ResourceStorageBonus>();
    [Header("Era")]
    [SerializeField, Min(0)] private int requiredEraIndex = 0;
    [SerializeField, Min(0)] private int maxUnlockedDisplayLevelAtRequiredEra = 0;
    [Header("UI")]
    [SerializeField] private bool showBuildingPanel = true;
    [SerializeField] private Sprite detailsBackgroundSprite;
    [SerializeField] private ResearchDatabase researchDatabase;
    [SerializeField] private BlessingDatabase blessingDatabase;
    [Header("Production")]
    [SerializeField] private bool allowProductionPauseControl = true;
    [Header("Market")]
    [SerializeField] private List<MarketTrade> marketTrades = new List<MarketTrade>();
    [Header("Proximity Bonus")]
    [SerializeField] private bool enableProximityBonus = false;
    [SerializeField] private ResourceType proximityResource;
    [SerializeField] private float proximityBonusStartDistance = 5f;
    [SerializeField] private float proximityBonusMaxDistance = 1f;
    [SerializeField] private float proximityMaxBonusPercent = 0.3f;
    [SerializeField] private float proximitySearchInterval = 1f;
    [Header("Finite Node Action (Details)")]
    [SerializeField] private FiniteNodeActionKind finiteNodeAction = FiniteNodeActionKind.None;
    [SerializeField] private string finiteNodeActionLabel = "";
    [SerializeField] private ResourceType finiteNodeTargetResource;
    [SerializeField, Min(0)] private int finiteNodeRestoreAmount = 0;
    [SerializeField] private List<ResourceCost> finiteNodeActionCosts = new List<ResourceCost>();
    [Header("Debug")]
    [SerializeField] private bool debugFreeBuild = false;
    [SerializeField] private bool debugFreeUpgrades = false;

    public string Id => id;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public GameObject Prefab => prefab;
    public bool UsePrefabFootprint => usePrefabFootprint;
    public Vector2Int Footprint => footprint;
    public IReadOnlyList<ResourceCost> Costs => costs;
    public IReadOnlyList<UpgradeLevel> UpgradeLevels => upgradeLevels;
    public IReadOnlyList<MarketTrade> MarketTrades => marketTrades;
    public int OrderIndex => orderIndex;
    public bool ShowInBuildMenu => showInBuildMenu;
    public int StorageCapacityBonus => storageCapacityBonus;
    public IReadOnlyList<ResourceStorageBonus> StorageCapacityBonuses => storageCapacityBonuses;
    public int RequiredEraIndex => Mathf.Max(0, requiredEraIndex);
    public int MaxUnlockedDisplayLevelAtRequiredEra => Mathf.Max(0, maxUnlockedDisplayLevelAtRequiredEra);
    public bool ShowBuildingPanel => showBuildingPanel;
    public Sprite DetailsBackgroundSprite => detailsBackgroundSprite;
    public bool HasDetailsView => detailsBackgroundSprite != null;
    public ResearchDatabase ResearchDatabase => researchDatabase;
    public BlessingDatabase BlessingDatabase => blessingDatabase;
    public bool AllowProductionPauseControl => allowProductionPauseControl;
    public bool EnableProximityBonus => enableProximityBonus;
    public ResourceType ProximityResource => proximityResource;
    public float ProximityBonusStartDistance => proximityBonusStartDistance;
    public float ProximityBonusMaxDistance => proximityBonusMaxDistance;
    public float ProximityMaxBonusPercent => proximityMaxBonusPercent;
    public float ProximitySearchInterval => proximitySearchInterval;
    public FiniteNodeActionKind NodeActionKind => finiteNodeAction;
    public ResourceType NodeActionTargetResource => finiteNodeTargetResource;
    public int NodeActionRestoreAmount => Mathf.Max(0, finiteNodeRestoreAmount);
    public IReadOnlyList<ResourceCost> NodeActionCosts => finiteNodeActionCosts;
    public bool HasNodeAction => finiteNodeAction != FiniteNodeActionKind.None && finiteNodeTargetResource != null;
    public bool DebugFreeBuild => debugFreeBuild;
    public bool DebugFreeUpgrades => debugFreeUpgrades;

    public string NodeActionLabel
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(finiteNodeActionLabel))
            {
                return finiteNodeActionLabel.Trim();
            }

            switch (finiteNodeAction)
            {
                case FiniteNodeActionKind.Replant:
                    return "Replant";
                case FiniteNodeActionKind.Fertile:
                    return "Fertile";
                case FiniteNodeActionKind.NewVein:
                    return "New Vein";
                default:
                    return "Action";
            }
        }
    }

    public int GetMaxUnlockedDisplayLevelForEra(int currentEraIndex)
    {
        int configured = MaxUnlockedDisplayLevelAtRequiredEra;
        if (configured <= 0)
        {
            return 0;
        }

        return currentEraIndex <= RequiredEraIndex ? configured : 0;
    }
    public void EnsureBaseUpgradeLevel(
        int amountPerCycle,
        float intervalSeconds,
        IReadOnlyList<ResourceCost> inputResourcesPerCycle = null)
    {
        if (upgradeLevels == null)
        {
            upgradeLevels = new List<UpgradeLevel>();
        }

        if (upgradeLevels.Count > 0)
        {
            var first = upgradeLevels[0];
            bool hasCosts = first.costs != null && first.costs.Count > 0;
            bool hasInputs = first.inputResourcesPerCycle != null && first.inputResourcesPerCycle.Count > 0;
            bool hasValidStats = first.amountPerCycle > 0 || first.intervalSeconds > 0f;
            if (hasCosts || hasInputs || hasValidStats)
            {
                return;
            }

            // Treat empty/zeroed first level as uninitialized for producers.
            upgradeLevels.Clear();
        }

        var baseLevel = new UpgradeLevel
        {
            levelName = "Base",
            amountPerCycle = Mathf.Max(1, amountPerCycle),
            intervalSeconds = Mathf.Max(0.01f, intervalSeconds),
            costs = new List<ResourceCost>(),
            inputResourcesPerCycle = CloneResourceCosts(inputResourcesPerCycle)
        };

        upgradeLevels.Add(baseLevel);
    }

    public void SetShowInBuildMenu(bool value)
    {
        showInBuildMenu = value;
    }

    public void SetDebugFlags(bool freeBuild, bool freeUpgrades)
    {
        debugFreeBuild = freeBuild;
        debugFreeUpgrades = freeUpgrades;
    }

    public void AccumulateBaseStorageBonuses(Dictionary<ResourceType, int> resourceBonuses, ref int globalBonus)
    {
        globalBonus += Mathf.Max(0, storageCapacityBonus);
        AccumulateStorageBonuses(storageCapacityBonuses, resourceBonuses);
    }

    public void AccumulateUpgradeStorageBonusesUpToLevel(int levelInclusive, Dictionary<ResourceType, int> resourceBonuses, ref int globalBonus)
    {
        if (upgradeLevels == null || upgradeLevels.Count == 0 || levelInclusive < 0)
        {
            return;
        }

        int maxLevel = Mathf.Min(levelInclusive, upgradeLevels.Count - 1);
        for (int i = 0; i <= maxLevel; i++)
        {
            var level = upgradeLevels[i];
            if (level == null)
            {
                continue;
            }

            globalBonus += Mathf.Max(0, level.storageCapacityBonus);
            AccumulateStorageBonuses(level.storageCapacityBonuses, resourceBonuses);
        }
    }

    private static List<ResourceCost> CloneResourceCosts(IReadOnlyList<ResourceCost> source)
    {
        var result = new List<ResourceCost>();
        if (source == null)
        {
            return result;
        }

        for (int i = 0; i < source.Count; i++)
        {
            var cost = source[i];
            if (cost.resourceType == null || cost.amount <= 0)
            {
                continue;
            }

            result.Add(new ResourceCost
            {
                resourceType = cost.resourceType,
                amount = cost.amount
            });
        }

        return result;
    }

    private static void AccumulateStorageBonuses(IReadOnlyList<ResourceStorageBonus> source, Dictionary<ResourceType, int> target)
    {
        if (source == null || target == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            var bonus = source[i];
            if (bonus.resourceType == null || bonus.amount <= 0)
            {
                continue;
            }

            if (target.TryGetValue(bonus.resourceType, out int existing))
            {
                target[bonus.resourceType] = existing + bonus.amount;
            }
            else
            {
                target[bonus.resourceType] = bonus.amount;
            }
        }
    }
}
