using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BlessingSetupUtility
{
    private const string BaseFolder = "Assets/Data/Blessings";
    private const string DatabasePath = "Assets/Data/Blessings/DefaultBlessingDatabase.asset";

    [MenuItem("Tools/Blessings/Generate Default Blessings")]
    public static void GenerateDefaultBlessings()
    {
        EnsureFolder("Assets/Data");
        EnsureFolder(BaseFolder);

        var faith = FindResourceType("Faith");
        var gold = FindResourceType("Gold");
        var farm = FindBuildingType("Farm");
        var market = FindBuildingType("Market");

        var b1 = GetOrCreateBlessing("Blessed Harvest", "blessed_harvest");
        var b2 = GetOrCreateBlessing("Prosperous Trade", "prosperous_trade");
        var b3 = GetOrCreateBlessing("Hands of the People", "hands_of_the_people");
        var b4 = GetOrCreateBlessing("Sacred Foundations", "sacred_foundations");
        var b5 = GetOrCreateBlessing("Unity of the Kingdom", "unity_of_the_kingdom");
        var b6 = GetOrCreateBlessing("Divine Favor", "divine_favor");

        SetBlessingData(b1, "Blessed Harvest", "Farm output +15%.",
            new List<(ResourceType, int)> { (faith, 3) },
            new List<BlessingDefinition>(),
            BlessingDefinition.EffectType.BuildingOutputPercent, farm, null, 0.15f);

        SetBlessingData(b2, "Prosperous Trade", "Market output +15%.",
            new List<(ResourceType, int)> { (faith, 4) },
            new List<BlessingDefinition> { b1 },
            BlessingDefinition.EffectType.BuildingOutputPercent, market, null, 0.15f);

        SetBlessingData(b3, "Hands of the People", "Manual click power +10%.",
            new List<(ResourceType, int)> { (faith, 5) },
            new List<BlessingDefinition> { b2 },
            BlessingDefinition.EffectType.ManualClickPercent, null, null, 0.10f);

        SetBlessingData(b4, "Sacred Foundations", "All build+upgrade costs -5%.",
            new List<(ResourceType, int)> { (faith, 6) },
            new List<BlessingDefinition> { b3 },
            BlessingDefinition.EffectType.BuildCostReductionPercent, null, null, 0.05f);

        SetBlessingData(b5, "Unity of the Kingdom", "All production +10%.",
            new List<(ResourceType, int)> { (faith, 8) },
            new List<BlessingDefinition> { b4 },
            BlessingDefinition.EffectType.ProductionAllPercent, null, null, 0.10f);

        SetBlessingData(b6, "Divine Favor", "Gold production +20%.",
            new List<(ResourceType, int)> { (faith, 12) },
            new List<BlessingDefinition> { b5 },
            BlessingDefinition.EffectType.ProductionPercent, null, gold, 0.20f);

        var database = AssetDatabase.LoadAssetAtPath<BlessingDatabase>(DatabasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<BlessingDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
        }

        var dbSerialized = new SerializedObject(database);
        var listProp = dbSerialized.FindProperty("blessings");
        listProp.ClearArray();
        listProp.InsertArrayElementAtIndex(0);
        listProp.GetArrayElementAtIndex(0).objectReferenceValue = b1;
        listProp.InsertArrayElementAtIndex(1);
        listProp.GetArrayElementAtIndex(1).objectReferenceValue = b2;
        listProp.InsertArrayElementAtIndex(2);
        listProp.GetArrayElementAtIndex(2).objectReferenceValue = b3;
        listProp.InsertArrayElementAtIndex(3);
        listProp.GetArrayElementAtIndex(3).objectReferenceValue = b4;
        listProp.InsertArrayElementAtIndex(4);
        listProp.GetArrayElementAtIndex(4).objectReferenceValue = b5;
        listProp.InsertArrayElementAtIndex(5);
        listProp.GetArrayElementAtIndex(5).objectReferenceValue = b6;
        dbSerialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Default blessings generated.");
    }

    private static void SetBlessingData(
        BlessingDefinition blessing,
        string displayName,
        string description,
        List<(ResourceType resourceType, int amount)> costs,
        List<BlessingDefinition> prerequisites,
        BlessingDefinition.EffectType effectType,
        BuildingType targetBuilding,
        ResourceType targetResource,
        float percentValue)
    {
        if (blessing == null)
        {
            return;
        }

        var so = new SerializedObject(blessing);
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
        effectProp.FindPropertyRelative("targetBuilding").objectReferenceValue = targetBuilding;
        effectProp.FindPropertyRelative("targetResource").objectReferenceValue = targetResource;
        effectProp.FindPropertyRelative("percentValue").floatValue = percentValue;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(blessing);
    }

    private static BlessingDefinition GetOrCreateBlessing(string assetName, string id)
    {
        string path = $"{BaseFolder}/{assetName}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<BlessingDefinition>(path);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<BlessingDefinition>();
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

    private static BuildingType FindBuildingType(string name)
    {
        string[] guids = AssetDatabase.FindAssets($"{name} t:BuildingType");
        if (guids == null || guids.Length == 0)
        {
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<BuildingType>(path);
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
