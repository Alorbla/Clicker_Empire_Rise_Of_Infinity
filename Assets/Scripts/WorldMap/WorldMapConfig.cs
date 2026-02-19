using UnityEngine;

public enum WorldType
{
    Pangea = 0,
    Continents = 1,
    Islands = 2
}

[CreateAssetMenu(fileName = "WorldMapConfig", menuName = "ClickerEmpire/World Map Config")]
public class WorldMapConfig : ScriptableObject
{
    [Header("World Type")]
    public WorldType worldType = WorldType.Continents;
    [Range(0f, 1f)] public float landCoverage = 0.52f;
    [Tooltip("Automatically applies recommended defaults when WorldType changes.")]
    public bool autoApplyWorldTypePreset = false;

    [Header("Map Size")]
    [Min(1)] public int mapWidth = 56;
    [Min(1)] public int mapHeight = 36;

    [Header("Seed")]
    public bool useRandomSeed = false;
    public int seed = 12345;

    [Header("Noise Scales")]
    [Min(0.0001f)] public float elevationScale = 26f;
    [Min(0.0001f)] public float moistureScale = 20f;
    [Min(0.0001f)] public float ridgeScale = 11f;
    [Min(0.0001f)] public float forestPatchScale = 8f;

    [Header("World Type Shaping")]
    [Range(0f, 1f)] public float falloffStrength = 0.36f;
    [Min(1)] public int numContinents = 4;
    [Range(0.05f, 1f)] public float continentSpread = 0.42f;
    [Range(0.25f, 3f)] public float islandScaleMultiplier = 1.55f;
    [Range(0f, 0.5f)] public float extraWaterBias = 0.12f;

    [Header("Thresholds")]
    [Range(0f, 1f)] public float waterThreshold = 0.44f;
    [Range(0f, 1f)] public float mountainThreshold = 0.76f;
    [Range(0f, 1f)] public float ridgeThreshold = 0.62f;
    [Range(0f, 1f)] public float desertMoistureThreshold = 0.27f;
    [Range(0f, 1f)] public float forestMoistureThreshold = 0.63f;
    [Range(0f, 1f)] public float forestPatchThreshold = 0.48f;

    [Header("Toggles")]
    public bool useRadialFalloff = true;
    public bool useSmoothing = true;
    public bool useCoastDrynessRule = true;
    public bool useForestPatchBreakup = true;
    public bool useDesertAdjacencyPenalty = true;

    [Header("Climate Rules")]
    [Range(1, 6)] public int coastWaterDistanceMax = 3;
    [Range(0f, 1f)] public float desertMaxElevation = 0.65f;
    [Range(0, 6)] public int maxDesertNeighborsForForest = 1;

    [Header("Smoothing")]
    [Range(1, 3)] public int smoothingPasses = 1;

    [Header("Biome Prefabs")]
    public GameObject plainsPrefab;
    public GameObject waterPrefab;
    public GameObject desertPrefab;
    public GameObject forestPrefab;
    public GameObject mountainsPrefab;

    [Header("Special Prefabs")]
    public GameObject villagePrefab;

    [SerializeField, HideInInspector] private WorldType lastPresetWorldType = WorldType.Continents;

    [ContextMenu("Apply World Type Preset")]
    public void ApplyWorldTypePreset()
    {
        switch (worldType)
        {
            case WorldType.Pangea:
                landCoverage = 0.62f;
                useRadialFalloff = true;
                falloffStrength = 0.52f;
                elevationScale = 30f;
                moistureScale = 22f;
                ridgeScale = 12f;
                waterThreshold = 0.43f;
                mountainThreshold = 0.78f;
                numContinents = 1;
                continentSpread = 0.30f;
                islandScaleMultiplier = 1f;
                extraWaterBias = 0f;
                break;

            case WorldType.Continents:
                landCoverage = 0.52f;
                useRadialFalloff = true;
                falloffStrength = 0.34f;
                elevationScale = 26f;
                moistureScale = 20f;
                ridgeScale = 11f;
                waterThreshold = 0.44f;
                mountainThreshold = 0.76f;
                numContinents = 4;
                continentSpread = 0.42f;
                islandScaleMultiplier = 1.25f;
                extraWaterBias = 0.04f;
                break;

            case WorldType.Islands:
                landCoverage = 0.34f;
                useRadialFalloff = false;
                falloffStrength = 0.14f;
                elevationScale = 18f;
                moistureScale = 18f;
                ridgeScale = 10f;
                waterThreshold = 0.48f;
                mountainThreshold = 0.74f;
                numContinents = 2;
                continentSpread = 0.55f;
                islandScaleMultiplier = 1.85f;
                extraWaterBias = 0.16f;
                break;
        }

        ridgeThreshold = 0.62f;
        desertMoistureThreshold = 0.27f;
        forestMoistureThreshold = 0.63f;
        forestPatchThreshold = 0.48f;
        coastWaterDistanceMax = 3;
        desertMaxElevation = 0.65f;
        maxDesertNeighborsForForest = 1;
        smoothingPasses = 1;

        lastPresetWorldType = worldType;
    }

    private void OnValidate()
    {
        if (autoApplyWorldTypePreset && lastPresetWorldType != worldType)
        {
            ApplyWorldTypePreset();
        }

        mapWidth = Mathf.Max(1, mapWidth);
        mapHeight = Mathf.Max(1, mapHeight);

        landCoverage = Mathf.Clamp01(landCoverage);

        elevationScale = Mathf.Max(0.0001f, elevationScale);
        moistureScale = Mathf.Max(0.0001f, moistureScale);
        ridgeScale = Mathf.Max(0.0001f, ridgeScale);
        forestPatchScale = Mathf.Max(0.0001f, forestPatchScale);

        falloffStrength = Mathf.Clamp01(falloffStrength);
        numContinents = Mathf.Clamp(numContinents, 1, 12);
        continentSpread = Mathf.Clamp(continentSpread, 0.05f, 1f);
        islandScaleMultiplier = Mathf.Clamp(islandScaleMultiplier, 0.25f, 3f);
        extraWaterBias = Mathf.Clamp(extraWaterBias, 0f, 0.5f);

        waterThreshold = Mathf.Clamp01(waterThreshold);
        mountainThreshold = Mathf.Max(waterThreshold, Mathf.Clamp01(mountainThreshold));

        ridgeThreshold = Mathf.Clamp01(ridgeThreshold);
        desertMoistureThreshold = Mathf.Clamp01(desertMoistureThreshold);
        forestMoistureThreshold = Mathf.Max(desertMoistureThreshold, Mathf.Clamp01(forestMoistureThreshold));
        forestPatchThreshold = Mathf.Clamp01(forestPatchThreshold);

        coastWaterDistanceMax = Mathf.Clamp(coastWaterDistanceMax, 1, 6);
        desertMaxElevation = Mathf.Clamp01(desertMaxElevation);
        maxDesertNeighborsForForest = Mathf.Clamp(maxDesertNeighborsForForest, 0, 6);
        smoothingPasses = Mathf.Clamp(smoothingPasses, 1, 3);
    }
}
