using System;
using System.Collections.Generic;
using UnityEngine;

public class BlessingManager : MonoBehaviour
{
    [Serializable]
    private class BlessingSaveData
    {
        public List<string> purchasedIds = new List<string>();
    }

    public static BlessingManager Instance { get; private set; }

    [SerializeField] private List<BlessingDatabase> blessingDatabases = new List<BlessingDatabase>();
    [SerializeField] private string playerPrefsKey = "ClickerKingdom.Blessings.Purchased";
    [SerializeField] private bool enableSaveLoad = false;

    private readonly HashSet<string> purchased = new HashSet<string>();
    private bool appliedOnLoad;

    public event Action BlessingsChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (enableSaveLoad)
        {
            Load();
        }
    }

    private void Start()
    {
        ApplyPurchasedEffects();
    }

    public bool IsPurchased(BlessingDefinition blessing)
    {
        if (blessing == null)
        {
            return false;
        }

        return purchased.Contains(GetBlessingKey(blessing));
    }

    public bool ArePrerequisitesMet(BlessingDefinition blessing)
    {
        if (blessing == null)
        {
            return false;
        }

        var prereqs = blessing.Prerequisites;
        if (prereqs == null || prereqs.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < prereqs.Count; i++)
        {
            if (!IsPurchased(prereqs[i]))
            {
                return false;
            }
        }

        return true;
    }

    public bool CanAfford(ResourceManager manager, BlessingDefinition blessing)
    {
        if (manager == null || blessing == null)
        {
            return false;
        }

        var costs = blessing.Costs;
        if (costs == null || costs.Count == 0)
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

            if (manager.Get(cost.resourceType) < cost.amount)
            {
                return false;
            }
        }

        return true;
    }

    public List<string> GetPurchasedIds()
    {
        return new List<string>(purchased);
    }

    public void SetPurchasedIds(IEnumerable<string> ids, bool applyEffects)
    {
        purchased.Clear();
        if (ids != null)
        {
            foreach (var id in ids)
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    purchased.Add(id.Trim());
                }
            }
        }

        appliedOnLoad = false;
        if (applyEffects)
        {
            ApplyPurchasedEffects();
        }

        BlessingsChanged?.Invoke();
    }

    public bool TryPurchase(ResourceManager manager, BlessingDefinition blessing)
    {
        if (blessing == null || manager == null)
        {
            return false;
        }

        if (IsPurchased(blessing))
        {
            return false;
        }

        if (!ArePrerequisitesMet(blessing))
        {
            return false;
        }

        if (!CanAfford(manager, blessing))
        {
            return false;
        }

        SpendCosts(manager, blessing);
        MarkPurchased(blessing);
        ApplyEffect(blessing);
        if (enableSaveLoad)
        {
            Save();
        }
        BlessingsChanged?.Invoke();
        return true;
    }

    public void MarkPurchased(BlessingDefinition blessing)
    {
        if (blessing == null)
        {
            return;
        }

        purchased.Add(GetBlessingKey(blessing));
    }

    public void ApplyPurchasedEffects()
    {
        if (appliedOnLoad)
        {
            return;
        }

        appliedOnLoad = true;
        var all = CollectBlessingDefinitions();
        for (int i = 0; i < all.Count; i++)
        {
            var blessing = all[i];
            if (blessing == null)
            {
                continue;
            }

            if (IsPurchased(blessing))
            {
                ApplyEffect(blessing);
            }
        }
    }

    public List<BlessingDefinition> CollectBlessingDefinitions()
    {
        var list = new List<BlessingDefinition>();
        for (int i = 0; i < blessingDatabases.Count; i++)
        {
            var db = blessingDatabases[i];
            if (db == null || db.Blessings == null)
            {
                continue;
            }

            for (int j = 0; j < db.Blessings.Count; j++)
            {
                var blessing = db.Blessings[j];
                if (blessing == null || list.Contains(blessing))
                {
                    continue;
                }

                list.Add(blessing);
            }
        }

        return list;
    }

    private void SpendCosts(ResourceManager manager, BlessingDefinition blessing)
    {
        var costs = blessing.Costs;
        if (costs == null)
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

            manager.Spend(cost.resourceType, cost.amount);
        }
    }

    private void ApplyEffect(BlessingDefinition blessing)
    {
        var modifiers = GlobalModifiers.Instance != null
            ? GlobalModifiers.Instance
            : FindAnyObjectByType<GlobalModifiers>();
        if (modifiers == null || blessing == null)
        {
            return;
        }

        var effect = blessing.BlessingEffect;
        switch (effect.type)
        {
            case BlessingDefinition.EffectType.BuildingOutputPercent:
                modifiers.AddBuildingOutputBonus(effect.targetBuilding, effect.percentValue);
                break;
            case BlessingDefinition.EffectType.ProductionPercent:
                modifiers.AddProductionBonus(effect.targetResource, effect.percentValue);
                break;
            case BlessingDefinition.EffectType.ProductionAllPercent:
                modifiers.AddAllProductionBonus(effect.percentValue);
                break;
            case BlessingDefinition.EffectType.BuildCostReductionPercent:
                modifiers.AddBuildCostReduction(effect.percentValue);
                break;
            case BlessingDefinition.EffectType.ManualClickPercent:
                modifiers.AddManualClickBonus(effect.percentValue);
                break;
        }
    }

    private string GetBlessingKey(BlessingDefinition blessing)
    {
        if (blessing == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(blessing.Id))
        {
            return blessing.Id;
        }

        return blessing.name;
    }

    private void Load()
    {
        purchased.Clear();
        if (!PlayerPrefs.HasKey(playerPrefsKey))
        {
            return;
        }

        string json = PlayerPrefs.GetString(playerPrefsKey, string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        try
        {
            var data = JsonUtility.FromJson<BlessingSaveData>(json);
            if (data == null || data.purchasedIds == null)
            {
                return;
            }

            for (int i = 0; i < data.purchasedIds.Count; i++)
            {
                string id = data.purchasedIds[i];
                if (!string.IsNullOrEmpty(id))
                {
                    purchased.Add(id);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"BlessingManager: Failed to load save data. {e.Message}");
        }
    }

    private void Save()
    {
        var data = new BlessingSaveData
        {
            purchasedIds = new List<string>(purchased)
        };

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(playerPrefsKey, json);
        PlayerPrefs.Save();
    }
}


