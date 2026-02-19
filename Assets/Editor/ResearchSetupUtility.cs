using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ResearchSetupUtility
{
    private const string BaseFolder = "Assets/Data/Research";
    private const string DatabasePath = "Assets/Data/Research/DefaultResearchDatabase.asset";

    [MenuItem("Tools/Research/Generate Default Research")]
    public static void GenerateDefaultResearch()
    {
        EnsureFolder("Assets/Data");
        EnsureFolder(BaseFolder);

        var science = FindResourceType("Science");
        var wood = FindResourceType("Wood");
        var stone = FindResourceType("Stone");
        var food = FindResourceType("Food");
        var gold = FindResourceType("Gold");

        var research1 = GetOrCreateResearch("Efficient Logging", "efficient_logging");
        var research2 = GetOrCreateResearch("Reinforced Pickaxes", "reinforced_pickaxes");
        var research3 = GetOrCreateResearch("Crop Rotation", "crop_rotation");
        var research4 = GetOrCreateResearch("Trade Accounting", "trade_accounting");
        var research5 = GetOrCreateResearch("Smart Planning", "smart_planning");
        var research6 = GetOrCreateResearch("Optimized Workflow", "optimized_workflow");

        SetResearchData(research1, "Efficient Logging", "Wood production +10%.",
            new List<(ResourceType, int)> { (science, 25) },
            new List<ResearchDefinition>(),
            ResearchDefinition.EffectType.ProductionPercent, wood, 0.10f);

        SetResearchData(research2, "Reinforced Pickaxes", "Stone production +10%.",
            new List<(ResourceType, int)> { (science, 25) },
            new List<ResearchDefinition> { research1 },
            ResearchDefinition.EffectType.ProductionPercent, stone, 0.10f);

        SetResearchData(research3, "Crop Rotation", "Food production +10%.",
            new List<(ResourceType, int)> { (science, 40) },
            new List<ResearchDefinition> { research2 },
            ResearchDefinition.EffectType.ProductionPercent, food, 0.10f);

        SetResearchData(research4, "Trade Accounting", "Gold production +10%.",
            new List<(ResourceType, int)> { (science, 60) },
            new List<ResearchDefinition> { research3 },
            ResearchDefinition.EffectType.ProductionPercent, gold, 0.10f);

        SetResearchData(research5, "Smart Planning", "All build+upgrade costs -10%.",
            new List<(ResourceType, int)> { (science, 80) },
            new List<ResearchDefinition> { research4 },
            ResearchDefinition.EffectType.BuildCostReductionPercent, null, 0.10f);

        SetResearchData(research6, "Optimized Workflow", "All production +5%.",
            new List<(ResourceType, int)> { (science, 120) },
            new List<ResearchDefinition> { research5 },
            ResearchDefinition.EffectType.ProductionAllPercent, null, 0.05f);

        var database = AssetDatabase.LoadAssetAtPath<ResearchDatabase>(DatabasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<ResearchDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
        }

        var dbSerialized = new SerializedObject(database);
        var listProp = dbSerialized.FindProperty("research");
        listProp.ClearArray();
        listProp.InsertArrayElementAtIndex(0);
        listProp.GetArrayElementAtIndex(0).objectReferenceValue = research1;
        listProp.InsertArrayElementAtIndex(1);
        listProp.GetArrayElementAtIndex(1).objectReferenceValue = research2;
        listProp.InsertArrayElementAtIndex(2);
        listProp.GetArrayElementAtIndex(2).objectReferenceValue = research3;
        listProp.InsertArrayElementAtIndex(3);
        listProp.GetArrayElementAtIndex(3).objectReferenceValue = research4;
        listProp.InsertArrayElementAtIndex(4);
        listProp.GetArrayElementAtIndex(4).objectReferenceValue = research5;
        listProp.InsertArrayElementAtIndex(5);
        listProp.GetArrayElementAtIndex(5).objectReferenceValue = research6;
        dbSerialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Default research assets generated.");
    }

    private static void SetResearchData(
        ResearchDefinition research,
        string displayName,
        string description,
        List<(ResourceType resourceType, int amount)> costs,
        List<ResearchDefinition> prerequisites,
        ResearchDefinition.EffectType effectType,
        ResourceType targetResource,
        float percentValue)
    {
        if (research == null)
        {
            return;
        }

        var so = new SerializedObject(research);
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("description").stringValue = description;

        var costsProp = so.FindProperty("costs");
        costsProp.ClearArray();
        for (int i = 0; i < costs.Count; i++)
        {
            costsProp.InsertArrayElementAtIndex(i);
            var entry = costsProp.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("resourceType").objectReferenceValue = costs[i].resourceType;
            entry.FindPropertyRelative("amount").intValue = costs[i].amount;
        }

        var prereqProp = so.FindProperty("prerequisites");
        prereqProp.ClearArray();
        for (int i = 0; i < prerequisites.Count; i++)
        {
            prereqProp.InsertArrayElementAtIndex(i);
            prereqProp.GetArrayElementAtIndex(i).objectReferenceValue = prerequisites[i];
        }

        var effectProp = so.FindProperty("effect");
        effectProp.FindPropertyRelative("type").enumValueIndex = (int)effectType;
        effectProp.FindPropertyRelative("targetResource").objectReferenceValue = targetResource;
        effectProp.FindPropertyRelative("percentValue").floatValue = percentValue;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(research);
    }

    private static ResearchDefinition GetOrCreateResearch(string assetName, string id)
    {
        string path = $"{BaseFolder}/{assetName}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<ResearchDefinition>(path);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<ResearchDefinition>();
        var so = new SerializedObject(asset);
        so.FindProperty("id").stringValue = id;
        so.ApplyModifiedProperties();

        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static ResourceType FindResourceType(string name)
    {
        string[] guids = AssetDatabase.FindAssets($"{name} t:ResourceType");
        if (guids == null || guids.Length == 0)
        {
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<ResourceType>(path);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        int lastSlash = path.LastIndexOf('/');
        if (lastSlash <= 0)
        {
            return;
        }

        string parent = path.Substring(0, lastSlash);
        string folder = path.Substring(lastSlash + 1);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, folder);
    }
}
