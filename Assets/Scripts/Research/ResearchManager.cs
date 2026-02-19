using System;
using System.Collections.Generic;
using UnityEngine;

public class ResearchManager : MonoBehaviour
{
    [Serializable]
    private class ResearchSaveData
    {
        public List<string> purchasedIds = new List<string>();
    }

    public static ResearchManager Instance { get; private set; }

    [SerializeField] private List<ResearchDatabase> researchDatabases = new List<ResearchDatabase>();
    [SerializeField] private string playerPrefsKey = "ClickerKingdom.Research.Purchased";
    [SerializeField] private bool enableSaveLoad = false;

    private readonly HashSet<string> purchased = new HashSet<string>();
    private bool appliedOnLoad;

    public event Action ResearchChanged;

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

    public bool IsPurchased(ResearchDefinition research)
    {
        if (research == null)
        {
            return false;
        }

        string key = GetResearchKey(research);
        return purchased.Contains(key);
    }

    public bool ArePrerequisitesMet(ResearchDefinition research)
    {
        if (research == null)
        {
            return false;
        }

        var prereqs = research.Prerequisites;
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

    public bool CanAfford(ResourceManager manager, ResearchDefinition research)
    {
        if (manager == null || research == null)
        {
            return false;
        }

        var costs = research.Costs;
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

        ResearchChanged?.Invoke();
    }

    public bool TryPurchase(ResourceManager manager, ResearchDefinition research)
    {
        if (research == null || manager == null)
        {
            return false;
        }

        if (IsPurchased(research))
        {
            return false;
        }

        if (!ArePrerequisitesMet(research))
        {
            return false;
        }

        if (!CanAfford(manager, research))
        {
            return false;
        }

        SpendCosts(manager, research);
        MarkPurchased(research);
        ApplyEffect(research);
        if (enableSaveLoad)
        {
            Save();
        }
        ResearchChanged?.Invoke();
        return true;
    }

    public void MarkPurchased(ResearchDefinition research)
    {
        if (research == null)
        {
            return;
        }

        purchased.Add(GetResearchKey(research));
    }

    public void ApplyPurchasedEffects()
    {
        if (appliedOnLoad)
        {
            return;
        }

        appliedOnLoad = true;
        var all = CollectResearchDefinitions();
        for (int i = 0; i < all.Count; i++)
        {
            var research = all[i];
            if (research == null)
            {
                continue;
            }

            if (IsPurchased(research))
            {
                ApplyEffect(research);
            }
        }
    }

    public List<ResearchDefinition> CollectResearchDefinitions()
    {
        var list = new List<ResearchDefinition>();
        for (int i = 0; i < researchDatabases.Count; i++)
        {
            var db = researchDatabases[i];
            if (db == null || db.Research == null)
            {
                continue;
            }

            for (int j = 0; j < db.Research.Count; j++)
            {
                var research = db.Research[j];
                if (research == null || list.Contains(research))
                {
                    continue;
                }

                list.Add(research);
            }
        }

        return list;
    }

    private void SpendCosts(ResourceManager manager, ResearchDefinition research)
    {
        var costs = research.Costs;
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

    private void ApplyEffect(ResearchDefinition research)
    {
        var modifiers = GlobalModifiers.Instance != null
            ? GlobalModifiers.Instance
            : FindAnyObjectByType<GlobalModifiers>();
        if (modifiers == null || research == null)
        {
            return;
        }

        var effect = research.ResearchEffect;
        switch (effect.type)
        {
            case ResearchDefinition.EffectType.ProductionPercent:
                modifiers.AddProductionBonus(effect.targetResource, effect.percentValue);
                break;
            case ResearchDefinition.EffectType.ProductionAllPercent:
                modifiers.AddAllProductionBonus(effect.percentValue);
                break;
            case ResearchDefinition.EffectType.BuildCostReductionPercent:
                modifiers.AddBuildCostReduction(effect.percentValue);
                break;
        }
    }

    private string GetResearchKey(ResearchDefinition research)
    {
        if (research == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(research.Id))
        {
            return research.Id;
        }

        return research.name;
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
            var data = JsonUtility.FromJson<ResearchSaveData>(json);
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
            Debug.LogWarning($"ResearchManager: Failed to load save data. {e.Message}");
        }
    }

    private void Save()
    {
        var data = new ResearchSaveData
        {
            purchasedIds = new List<string>(purchased)
        };

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(playerPrefsKey, json);
        PlayerPrefs.Save();
    }
}


