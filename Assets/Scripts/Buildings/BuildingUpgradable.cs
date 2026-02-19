using System.Collections.Generic;
using UnityEngine;

public class BuildingUpgradable : MonoBehaviour
{
    [SerializeField] private BuildingType buildingType;
    [SerializeField] private int currentLevel = 0;

    private BuildingProducer producer;
    private ManualClickUpgrade manualClickUpgrade;
    private bool useImplicitBase;

    private int appliedGlobalStorageBonus;
    private readonly Dictionary<ResourceType, int> appliedStorageBonusByResource = new Dictionary<ResourceType, int>();

    public BuildingType BuildingType => buildingType;
    public int CurrentLevel => currentLevel;
    public int DisplayLevel => Mathf.Max(0, currentLevel + 1);
    public bool IsAtImplicitBase => useImplicitBase && currentLevel < 0;

    private void Awake()
    {
        producer = GetComponent<BuildingProducer>();
        if (producer == null)
        {
            producer = GetComponentInChildren<BuildingProducer>();
        }
        manualClickUpgrade = GetComponent<ManualClickUpgrade>();
        if (manualClickUpgrade == null)
        {
            manualClickUpgrade = GetComponentInChildren<ManualClickUpgrade>();
        }
        useImplicitBase = ShouldUseImplicitBase();
        if (useImplicitBase && currentLevel == 0)
        {
            currentLevel = -1;
        }
        ApplyLevel();
    }

    public void SetBuildingType(BuildingType type)
    {
        buildingType = type;
        useImplicitBase = ShouldUseImplicitBase();
        if (useImplicitBase && currentLevel == 0)
        {
            currentLevel = -1;
        }
        ApplyLevel();
    }

    public bool HasNextLevel()
    {
        if (buildingType == null || buildingType.UpgradeLevels == null)
        {
            return false;
        }

        if (currentLevel + 1 >= buildingType.UpgradeLevels.Count)
        {
            return false;
        }

        return IsNextLevelUnlockedForCurrentEra();
    }

    public bool TryUpgrade(ResourceManager manager)
    {
        if (buildingType == null)
        {
            return false;
        }

        if (!HasNextLevel())
        {
            return false;
        }

        var nextLevel = buildingType.UpgradeLevels[currentLevel + 1];
        bool freeUpgrades = buildingType.DebugFreeUpgrades;
        if (!freeUpgrades)
        {
            if (manager == null)
            {
                return false;
            }

            if (!CanAfford(manager, nextLevel.costs))
            {
                return false;
            }

            Spend(manager, nextLevel.costs);
        }

        currentLevel++;
        ApplyLevel();
        return true;
    }

    public void SetLevelFromSave(int savedLevel)
    {
        useImplicitBase = ShouldUseImplicitBase();
        currentLevel = savedLevel;

        if (useImplicitBase && currentLevel < -1)
        {
            currentLevel = -1;
        }

        ApplyLevel();
    }

    private void ApplyLevel()
    {
        if (buildingType == null)
        {
            return;
        }

        if (producer == null)
        {
            producer = GetComponent<BuildingProducer>();
            if (producer == null)
            {
                producer = GetComponentInChildren<BuildingProducer>();
            }
        }

        if (manualClickUpgrade == null)
        {
            manualClickUpgrade = GetComponent<ManualClickUpgrade>();
            if (manualClickUpgrade == null)
            {
                manualClickUpgrade = GetComponentInChildren<ManualClickUpgrade>();
            }
        }

        if (producer != null)
        {
            buildingType.EnsureBaseUpgradeLevel(producer.AmountPerCycle, producer.IntervalSeconds, producer.InputResourcesPerCycle);
        }

        if (buildingType.UpgradeLevels == null || buildingType.UpgradeLevels.Count == 0)
        {
            if (manualClickUpgrade != null)
            {
                manualClickUpgrade.ApplyFromBuilding();
            }

            ApplyStorageBonusesForCurrentLevel();
            return;
        }

        if (currentLevel < 0)
        {
            if (manualClickUpgrade != null)
            {
                manualClickUpgrade.ApplyFromBuilding();
            }

            ApplyStorageBonusesForCurrentLevel();
            return;
        }

        int clamped = Mathf.Clamp(currentLevel, 0, buildingType.UpgradeLevels.Count - 1);
        currentLevel = clamped;

        var level = buildingType.UpgradeLevels[currentLevel];
        if (producer != null)
        {
            producer.SetOutput(level.amountPerCycle, level.intervalSeconds, level.inputResourcesPerCycle);
        }

        if (manualClickUpgrade != null)
        {
            manualClickUpgrade.ApplyFromBuilding();
        }

        ApplyStorageBonusesForCurrentLevel();
    }

    private void ApplyStorageBonusesForCurrentLevel()
    {
        if (buildingType == null)
        {
            return;
        }

        var manager = ResourceManager.Instance != null
            ? ResourceManager.Instance
            : Object.FindAnyObjectByType<ResourceManager>();
        if (manager == null)
        {
            return;
        }

        int targetGlobalBonus = 0;
        var targetResourceBonuses = new Dictionary<ResourceType, int>();

        buildingType.AccumulateBaseStorageBonuses(targetResourceBonuses, ref targetGlobalBonus);
        buildingType.AccumulateUpgradeStorageBonusesUpToLevel(currentLevel, targetResourceBonuses, ref targetGlobalBonus);

        if (targetGlobalBonus > appliedGlobalStorageBonus)
        {
            manager.IncreaseAllStorageCapacity(targetGlobalBonus - appliedGlobalStorageBonus);
            appliedGlobalStorageBonus = targetGlobalBonus;
        }

        foreach (var pair in targetResourceBonuses)
        {
            ResourceType resource = pair.Key;
            if (resource == null)
            {
                continue;
            }

            int targetAmount = Mathf.Max(0, pair.Value);
            int appliedAmount = 0;
            appliedStorageBonusByResource.TryGetValue(resource, out appliedAmount);

            if (targetAmount > appliedAmount)
            {
                manager.IncreaseStorageCapacity(resource, targetAmount - appliedAmount);
                appliedStorageBonusByResource[resource] = targetAmount;
            }
        }
    }

    private bool IsNextLevelUnlockedForCurrentEra()
    {
        if (buildingType == null)
        {
            return false;
        }

        var eraManager = EraProgressionManager.Instance != null
            ? EraProgressionManager.Instance
            : Object.FindAnyObjectByType<EraProgressionManager>();

        if (eraManager == null)
        {
            return true;
        }

        int maxDisplayLevel = buildingType.GetMaxUnlockedDisplayLevelForEra(eraManager.CurrentEraIndex);
        if (maxDisplayLevel <= 0)
        {
            return true;
        }

        int nextDisplayLevel = useImplicitBase ? currentLevel + 3 : currentLevel + 2;
        return nextDisplayLevel <= maxDisplayLevel;
    }
    private bool ShouldUseImplicitBase()
    {
        if (buildingType == null || buildingType.UpgradeLevels == null || buildingType.UpgradeLevels.Count == 0)
        {
            return false;
        }

        var first = buildingType.UpgradeLevels[0];
        if (first.costs == null)
        {
            return false;
        }

        for (int i = 0; i < first.costs.Count; i++)
        {
            if (first.costs[i].resourceType != null && first.costs[i].amount > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanAfford(ResourceManager manager, System.Collections.Generic.IReadOnlyList<BuildingType.ResourceCost> costs)
    {
        if (manager == null || costs == null)
        {
            return true;
        }

        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost.resourceType == null || cost.amount <= 0)
            {
                continue;
            }

            int amount = GetModifiedCost(cost.amount);
            if (manager.Get(cost.resourceType) < amount)
            {
                return false;
            }
        }

        return true;
    }

    private static void Spend(ResourceManager manager, System.Collections.Generic.IReadOnlyList<BuildingType.ResourceCost> costs)
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

            int amount = GetModifiedCost(cost.amount);
            manager.Spend(cost.resourceType, amount);
        }
    }

    private static int GetModifiedCost(int amount)
    {
        var modifiers = GlobalModifiers.Instance != null
            ? GlobalModifiers.Instance
            : Object.FindAnyObjectByType<GlobalModifiers>();
        return modifiers != null ? modifiers.ApplyCost(amount) : amount;
    }
}
