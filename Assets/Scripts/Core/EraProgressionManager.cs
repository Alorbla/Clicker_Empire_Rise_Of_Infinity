using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EraProgressionManager : MonoBehaviour
{
    [Serializable]
    public class ResourceRequirement
    {
        public ResourceType resourceType;
        [Min(0)] public int amount = 0;
    }

    [Serializable]
    public class EraStage
    {
        public string eraName = "Era";
        public List<ResourceRequirement> consumeOnAdvance = new List<ResourceRequirement>();
        public List<ResourceRequirement> requiredToKeep = new List<ResourceRequirement>();
        [Range(0, 100)] public int requiredHarmonyPercent = 0;
    }

    public static EraProgressionManager Instance { get; private set; }

    [SerializeField] private List<EraStage> eraStages = new List<EraStage>();
    [SerializeField] private int startingEraIndex = 0;

    private int currentEraIndex;

    public event Action<int> EraChanged;

    public int CurrentEraIndex => Mathf.Max(0, currentEraIndex);

    public string CurrentEraName
    {
        get
        {
            if (eraStages == null || eraStages.Count == 0)
            {
                return "Era 1";
            }

            int idx = Mathf.Clamp(currentEraIndex, 0, eraStages.Count - 1);
            string configured = eraStages[idx] != null ? eraStages[idx].eraName : string.Empty;
            return string.IsNullOrWhiteSpace(configured) ? $"Era {idx + 1}" : configured.Trim();
        }
    }

    public string NextEraName
    {
        get
        {
            if (!HasNextEra())
            {
                return string.Empty;
            }

            int next = Mathf.Clamp(currentEraIndex + 1, 0, eraStages.Count - 1);
            string configured = eraStages[next] != null ? eraStages[next].eraName : string.Empty;
            return string.IsNullOrWhiteSpace(configured) ? $"Era {next + 1}" : configured.Trim();
        }
    }

    public IReadOnlyList<ResourceRequirement> CurrentConsumeRequirements
    {
        get
        {
            EraStage stage = GetTargetStage();
            if (stage == null || stage.consumeOnAdvance == null)
            {
                return Array.Empty<ResourceRequirement>();
            }

            return stage.consumeOnAdvance;
        }
    }

    public IReadOnlyList<ResourceRequirement> CurrentKeepRequirements
    {
        get
        {
            EraStage stage = GetTargetStage();
            if (stage == null || stage.requiredToKeep == null)
            {
                return Array.Empty<ResourceRequirement>();
            }

            return stage.requiredToKeep;
        }
    }

    public int CurrentRequiredHarmonyPercent
    {
        get
        {
            EraStage stage = GetTargetStage();
            return stage == null ? 0 : Mathf.Clamp(stage.requiredHarmonyPercent, 0, 100);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        currentEraIndex = Mathf.Clamp(startingEraIndex, 0, Mathf.Max(0, eraStages.Count - 1));
    }

    public bool HasNextEra()
    {
        return eraStages != null && eraStages.Count > 0 && currentEraIndex < eraStages.Count - 1;
    }

    public void SetCurrentEraIndexDirect(int index)
    {
        int clamped = Mathf.Clamp(index, 0, Mathf.Max(0, eraStages.Count - 1));
        if (clamped == currentEraIndex)
        {
            return;
        }

        currentEraIndex = clamped;
        EraChanged?.Invoke(currentEraIndex);
    }

    public bool CanAdvance(ResourceManager manager, int harmonyPercent, out string reason)
    {
        if (!HasNextEra())
        {
            reason = "Max era reached";
            return false;
        }

        if (manager == null)
        {
            reason = "Missing ResourceManager";
            return false;
        }

        EraStage stage = GetTargetStage();
        if (stage == null)
        {
            reason = "Era data missing";
            return false;
        }

        int requiredHarmony = Mathf.Clamp(stage.requiredHarmonyPercent, 0, 100);
        if (harmonyPercent < requiredHarmony)
        {
            reason = $"Need Harmony {requiredHarmony}%";
            return false;
        }

        if (!HasRequiredResources(manager, stage.requiredToKeep, out reason))
        {
            return false;
        }

        if (!HasRequiredResources(manager, stage.consumeOnAdvance, out reason))
        {
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool TryAdvance(ResourceManager manager, int harmonyPercent, out string reason)
    {
        if (!CanAdvance(manager, harmonyPercent, out reason))
        {
            return false;
        }

        EraStage stage = GetTargetStage();
        if (stage == null)
        {
            reason = "Era data missing";
            return false;
        }

        SpendResources(manager, stage.consumeOnAdvance);

        currentEraIndex = Mathf.Clamp(currentEraIndex + 1, 0, Mathf.Max(0, eraStages.Count - 1));
        EraChanged?.Invoke(currentEraIndex);
        reason = string.Empty;
        return true;
    }

    private EraStage GetTargetStage()
    {
        if (eraStages == null || eraStages.Count == 0)
        {
            return null;
        }

        int idx = Mathf.Clamp(currentEraIndex + 1, 0, eraStages.Count - 1);
        return eraStages[idx];
    }

    private static bool HasRequiredResources(ResourceManager manager, List<ResourceRequirement> requirements, out string reason)
    {
        if (requirements == null || requirements.Count == 0)
        {
            reason = string.Empty;
            return true;
        }

        for (int i = 0; i < requirements.Count; i++)
        {
            ResourceRequirement req = requirements[i];
            if (req == null || req.resourceType == null || req.amount <= 0)
            {
                continue;
            }

            int current = manager.Get(req.resourceType);
            if (current < req.amount)
            {
                reason = $"Need {req.amount} {req.resourceType.DisplayName}";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static void SpendResources(ResourceManager manager, List<ResourceRequirement> requirements)
    {
        if (requirements == null || requirements.Count == 0)
        {
            return;
        }

        for (int i = 0; i < requirements.Count; i++)
        {
            ResourceRequirement req = requirements[i];
            if (req == null || req.resourceType == null || req.amount <= 0)
            {
                continue;
            }

            manager.Spend(req.resourceType, req.amount);
        }
    }
}
