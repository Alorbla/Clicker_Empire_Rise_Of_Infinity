using System.Collections.Generic;
using UnityEngine;

public class GlobalModifiers : MonoBehaviour
{
    public static GlobalModifiers Instance { get; private set; }

    private readonly Dictionary<ResourceType, float> productionBonuses =
        new Dictionary<ResourceType, float>();
    private readonly Dictionary<BuildingType, float> buildingProductionBonuses =
        new Dictionary<BuildingType, float>();

    private float allProductionBonus;
    private float buildCostReduction;
    private float manualClickBonus;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResetToBase();
    }

    public void ResetToBase()
    {
        productionBonuses.Clear();
        buildingProductionBonuses.Clear();
        allProductionBonus = 0f;
        buildCostReduction = 0f;
        manualClickBonus = 0f;
    }

    public void AddProductionBonus(ResourceType type, float percent)
    {
        if (type == null || percent <= 0f)
        {
            return;
        }

        if (productionBonuses.ContainsKey(type))
        {
            productionBonuses[type] += percent;
        }
        else
        {
            productionBonuses.Add(type, percent);
        }
    }

    public void AddAllProductionBonus(float percent)
    {
        if (percent <= 0f)
        {
            return;
        }

        allProductionBonus += percent;
    }

    public void AddBuildingOutputBonus(BuildingType type, float percent)
    {
        if (type == null || percent <= 0f)
        {
            return;
        }

        if (buildingProductionBonuses.ContainsKey(type))
        {
            buildingProductionBonuses[type] += percent;
        }
        else
        {
            buildingProductionBonuses.Add(type, percent);
        }
    }

    public void AddBuildCostReduction(float percent)
    {
        if (percent <= 0f)
        {
            return;
        }

        buildCostReduction = Mathf.Clamp01(buildCostReduction + percent);
    }

    public void AddManualClickBonus(float percent)
    {
        if (percent <= 0f)
        {
            return;
        }

        manualClickBonus += percent;
    }

    public float GetProductionMultiplier(ResourceType type)
    {
        return GetProductionMultiplier(type, null);
    }

    public float GetProductionMultiplier(ResourceType type, BuildingType buildingType)
    {
        float bonus = allProductionBonus;
        if (type != null && productionBonuses.TryGetValue(type, out var specific))
        {
            bonus += specific;
        }
        if (buildingType != null && buildingProductionBonuses.TryGetValue(buildingType, out var buildingBonus))
        {
            bonus += buildingBonus;
        }

        return Mathf.Max(0f, 1f + bonus);
    }

    public float GetCostMultiplier()
    {
        return Mathf.Clamp01(1f - buildCostReduction);
    }

    public float GetManualClickMultiplier()
    {
        return Mathf.Max(0f, 1f + manualClickBonus);
    }

    public int ApplyCost(int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        float mult = GetCostMultiplier();
        int result = Mathf.CeilToInt(amount * mult);
        return Mathf.Max(0, result);
    }
}

