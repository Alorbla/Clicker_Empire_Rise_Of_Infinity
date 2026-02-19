using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Blessing", menuName = "IdleHra/Blessing Definition")]
public class BlessingDefinition : ScriptableObject
{
    [Serializable]
    public struct ResourceCost
    {
        public ResourceType resourceType;
        public int amount;
    }

    public enum EffectType
    {
        BuildingOutputPercent,
        ProductionPercent,
        ProductionAllPercent,
        BuildCostReductionPercent,
        ManualClickPercent
    }

    [Serializable]
    public struct Effect
    {
        public EffectType type;
        public BuildingType targetBuilding;
        public ResourceType targetResource;
        [Range(0f, 1f)] public float percentValue;
    }

    [SerializeField] private string id = "";
    [SerializeField] private string displayName = "";
    [TextArea(2, 4)]
    [SerializeField] private string description = "";
    [SerializeField] private Sprite icon;
    [SerializeField] private List<ResourceCost> costs = new List<ResourceCost>();
    [SerializeField] private List<BlessingDefinition> prerequisites = new List<BlessingDefinition>();
    [SerializeField] private Effect effect;

    public string Id => id;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public IReadOnlyList<ResourceCost> Costs => costs;
    public IReadOnlyList<BlessingDefinition> Prerequisites => prerequisites;
    public Effect BlessingEffect => effect;
}
