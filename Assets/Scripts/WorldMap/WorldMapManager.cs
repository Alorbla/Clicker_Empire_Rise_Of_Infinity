using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
public class WorldMapManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Grid hexGrid;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Transform generatedRoot;
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool clearExistingBeforeGenerate = true;
    [SerializeField] private bool useCellCenter = true;

    [Header("Generation Config")]
    [SerializeField] private WorldMapConfig config;

    [Header("Input")]
    [SerializeField] private bool ignoreClickWhenPointerOverUI = true;

    [Header("Debug Input")]
    [SerializeField] private bool enableDebugRegenerateKeys = true;

    [Header("Scene Transition")]
    [SerializeField] private bool loadIdleSceneOnVillageClick = true;
    [SerializeField] private string idleMainSceneName = "IdleMain";

    private readonly Dictionary<Vector2Int, HexTile> tilesByGridCoord = new Dictionary<Vector2Int, HexTile>();
    private HexTile selectedTile;
    private bool hasGeneratedAtLeastOnce;

    private static readonly Vector2Int[] evenRowOffsets =
    {
        new Vector2Int(+1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(0, +1),
        new Vector2Int(-1, +1)
    };

    private static readonly Vector2Int[] oddRowOffsets =
    {
        new Vector2Int(+1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(+1, -1),
        new Vector2Int(0, -1),
        new Vector2Int(+1, +1),
        new Vector2Int(0, +1)
    };

    public event Action<HexTile> TileSelected;

    public IReadOnlyDictionary<Vector2Int, HexTile> TilesByGridCoord => tilesByGridCoord;
    public int MapWidth => config != null ? config.mapWidth : 0;
    public int MapHeight => config != null ? config.mapHeight : 0;
    public int EffectiveSeed { get; private set; }

    private void Awake()
    {
        if (hexGrid == null)
        {
            hexGrid = FindAnyObjectByType<Grid>();
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }
    }

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateMap();
        }
    }

    private void Update()
    {
        HandleDebugKeys();
        HandleTileClickInput();
    }

    private void HandleDebugKeys()
    {
        if (!enableDebugRegenerateKeys)
        {
            return;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.rKey.wasPressedThisFrame)
        {
            RegenerateWithCurrentSeed();
        }

        if (keyboard.nKey.wasPressedThisFrame)
        {
            GenerateWithNewRandomSeed();
        }
    }

    private void HandleTileClickInput()
    {
        if (!TryGetClickScreenPosition(out Vector2 screenPos, out int pointerId, out bool isTouch))
        {
            return;
        }

        if (ignoreClickWhenPointerOverUI && IsPointerOverUI(pointerId, isTouch))
        {
            return;
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
            if (worldCamera == null)
            {
                return;
            }
        }

        Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -worldCamera.transform.position.z));
        Vector2 point = new Vector2(world.x, world.y);

        Collider2D[] hits = Physics2D.OverlapPointAll(point);
        if (hits == null || hits.Length == 0)
        {
            return;
        }

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            HexTile tile = hit.GetComponent<HexTile>();
            if (tile == null)
            {
                tile = hit.GetComponentInParent<HexTile>();
            }

            if (tile != null)
            {
                OnTileClicked(tile);
                return;
            }
        }
    }

    private static bool TryGetClickScreenPosition(out Vector2 screenPos, out int pointerId, out bool isTouch)
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPos = mouse.position.ReadValue();
            pointerId = -1;
            isTouch = false;
            return true;
        }

        var touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            var touch = touchscreen.primaryTouch;
            if (touch != null && touch.press.wasPressedThisFrame)
            {
                screenPos = touch.position.ReadValue();
                pointerId = touch.touchId.ReadValue();
                isTouch = true;
                return true;
            }
        }

        screenPos = default;
        pointerId = -1;
        isTouch = false;
        return false;
    }

    private static bool IsPointerOverUI(int pointerId, bool isTouch)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        if (isTouch)
        {
            return EventSystem.current.IsPointerOverGameObject(pointerId);
        }

        return EventSystem.current.IsPointerOverGameObject();
    }

    [ContextMenu("Generate World Map")]
    public void GenerateMap()
    {
        int seedToUse;
        var save = SaveGameManager.Instance;
        if (save != null && save.TryGetWorldMapSeed(out int savedSeed))
        {
            seedToUse = savedSeed;
        }
        else
        {
            seedToUse = ResolveSeedFromConfig();
        }

        GenerateFromSeed(seedToUse);
    }

    [ContextMenu("Regenerate With Current Seed")]
    public void RegenerateWithCurrentSeed()
    {
        if (!hasGeneratedAtLeastOnce)
        {
            GenerateMap();
            return;
        }

        GenerateFromSeed(EffectiveSeed);
    }

    [ContextMenu("Generate With New Random Seed")]
    public void GenerateWithNewRandomSeed()
    {
        int randomSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        if (config != null)
        {
            config.seed = randomSeed;
        }

        GenerateFromSeed(randomSeed);
    }

    private void GenerateFromSeed(int seed)
    {
        if (!ValidateConfig())
        {
            return;
        }

        EnsureGrid();
        EnsureGeneratedRoot();

        EffectiveSeed = seed;

        if (clearExistingBeforeGenerate)
        {
            ClearGeneratedTiles();
        }

        tilesByGridCoord.Clear();
        selectedTile = null;

        int width = Mathf.Max(1, config.mapWidth);
        int height = Mathf.Max(1, config.mapHeight);

        float[,] elevation = new float[width, height];
        float[,] moisture = new float[width, height];
        float[,] ridge = new float[width, height];
        float[,] forestPatch = new float[width, height];
        int[,] waterDistance = new int[width, height];
        HexBiome[,] biomeMap = new HexBiome[width, height];

        BuildBaseNoiseMaps(width, height, elevation, moisture, ridge, forestPatch);
        ClassifyBaseBiomes(width, height, elevation, moisture, ridge, biomeMap);

        if (config.useCoastDrynessRule)
        {
            ComputeDistanceToWater(width, height, biomeMap, waterDistance);
        }
        else
        {
            FillDistanceMap(width, height, waterDistance, int.MaxValue);
        }

        ApplyClimateAdjustments(width, height, elevation, moisture, forestPatch, waterDistance, biomeMap);

        if (config.useSmoothing)
        {
            SmoothLandBiomes(width, height, biomeMap, Mathf.Clamp(config.smoothingPasses, 1, 3));
        }

        ForceCenterVillageTile(width, height, biomeMap);
        SpawnTiles(width, height, elevation, moisture, biomeMap);
        FocusCameraOnVillageTile(width, height);

        hasGeneratedAtLeastOnce = true;

        var save = SaveGameManager.Instance;
        if (save != null)
        {
            save.SetWorldMapSeed(EffectiveSeed);
            save.SaveNow();
        }

        Debug.Log($"[WorldMap] Generated {tilesByGridCoord.Count} hexes ({width}x{height}), seed={EffectiveSeed}, worldType={config.worldType}");
    }

    public void OnTileClicked(HexTile tile)
    {
        if (tile == null)
        {
            return;
        }

        if (selectedTile != null && selectedTile != tile)
        {
            selectedTile.SetSelected(false);
        }

        selectedTile = tile;
        selectedTile.SetSelected(true);

        Debug.Log($"[WorldMap] Clicked grid={tile.GridCoord.x},{tile.GridCoord.y} axial={tile.AxialCoord.x},{tile.AxialCoord.y} biome={tile.Biome} elevation={tile.Elevation:0.000} moisture={tile.Moisture:0.000} village={tile.IsVillage}");

        if (tile.IsVillage && loadIdleSceneOnVillageClick)
        {
            TryLoadIdleMainScene();
            return;
        }

        TileSelected?.Invoke(tile);
    }

    public bool TryGetTile(Vector2Int gridCoord, out HexTile tile)
    {
        return tilesByGridCoord.TryGetValue(gridCoord, out tile);
    }

    public static Vector2Int OffsetToAxialOddR(Vector2Int offset)
    {
        int q = offset.x - ((offset.y - (offset.y & 1)) / 2);
        int r = offset.y;
        return new Vector2Int(q, r);
    }

    private int ResolveSeedFromConfig()
    {
        if (config == null)
        {
            return 0;
        }

        if (config.useRandomSeed)
        {
            int randomSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            config.seed = randomSeed;
            return randomSeed;
        }

        return config.seed;
    }

    private void BuildBaseNoiseMaps(int width, int height, float[,] elevation, float[,] moisture, float[,] ridge, float[,] forestPatch)
    {
        var random = new System.Random(EffectiveSeed);

        float elevationOffsetX = random.Next(-100000, 100000);
        float elevationOffsetY = random.Next(-100000, 100000);
        float moistureOffsetX = random.Next(-100000, 100000);
        float moistureOffsetY = random.Next(-100000, 100000);
        float ridgeOffsetX = random.Next(-100000, 100000);
        float ridgeOffsetY = random.Next(-100000, 100000);
        float forestOffsetX = random.Next(-100000, 100000);
        float forestOffsetY = random.Next(-100000, 100000);

        Vector2[] continentCenters = BuildContinentCenters(random);

        float coverageBias = (Mathf.Clamp01(config.landCoverage) - 0.5f) * 0.7f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = width <= 1 ? 0f : x / (float)(width - 1);
                float ny = height <= 1 ? 0f : y / (float)(height - 1);

                float worldTypeScale = GetWorldTypeElevationScale();
                float baseElevation = SamplePerlin(x, y, config.elevationScale * worldTypeScale, elevationOffsetX, elevationOffsetY);
                float shapedElevation = ApplyWorldTypeElevationShaping(baseElevation, nx, ny, continentCenters);
                shapedElevation += coverageBias;

                elevation[x, y] = Mathf.Clamp01(shapedElevation);
                moisture[x, y] = Mathf.Clamp01(SamplePerlin(x, y, config.moistureScale, moistureOffsetX, moistureOffsetY));
                ridge[x, y] = Mathf.Clamp01(SamplePerlin(x, y, config.ridgeScale, ridgeOffsetX, ridgeOffsetY));
                forestPatch[x, y] = Mathf.Clamp01(SamplePerlin(x, y, config.forestPatchScale, forestOffsetX, forestOffsetY));
            }
        }
    }

    private float GetWorldTypeElevationScale()
    {
        if (config.worldType != WorldType.Islands)
        {
            return 1f;
        }

        float multiplier = Mathf.Clamp(config.islandScaleMultiplier, 0.25f, 3f);
        return 1f / multiplier;
    }

    private float ApplyWorldTypeElevationShaping(float baseElevation, float nx, float ny, Vector2[] continentCenters)
    {
        float shaped = baseElevation;

        switch (config.worldType)
        {
            case WorldType.Pangea:
            {
                float radial = GetRadialDistance(nx, ny);
                if (config.useRadialFalloff)
                {
                    shaped -= radial * radial * Mathf.Clamp01(config.falloffStrength);
                }

                float centerBias = Mathf.Pow(1f - radial, 2f) * 0.42f;
                shaped += centerBias;
                break;
            }

            case WorldType.Continents:
            {
                float radial = GetRadialDistance(nx, ny);
                if (config.useRadialFalloff)
                {
                    shaped -= radial * radial * Mathf.Clamp01(config.falloffStrength) * 0.6f;
                }

                float continentalInfluence = GetContinentalInfluence(nx, ny, continentCenters, config.continentSpread);
                shaped += continentalInfluence * 0.46f;
                break;
            }

            case WorldType.Islands:
            {
                if (config.useRadialFalloff)
                {
                    float radial = GetRadialDistance(nx, ny);
                    shaped -= radial * radial * Mathf.Clamp01(config.falloffStrength) * 0.2f;
                }

                shaped -= Mathf.Clamp(config.extraWaterBias, 0f, 0.5f);
                break;
            }
        }

        return shaped;
    }

    private Vector2[] BuildContinentCenters(System.Random random)
    {
        if (config.worldType == WorldType.Pangea)
        {
            return new[] { new Vector2(0.5f, 0.5f) };
        }

        int count = config.worldType == WorldType.Continents
            ? Mathf.Clamp(config.numContinents, 2, 8)
            : Mathf.Clamp(config.numContinents, 1, 4);

        var centers = new List<Vector2>(count);

        float spread = Mathf.Clamp(config.continentSpread, 0.05f, 1f);
        float minDist = Mathf.Lerp(0.06f, 0.28f, spread);
        int maxAttempts = count * 30;

        for (int attempt = 0; attempt < maxAttempts && centers.Count < count; attempt++)
        {
            float cx = Mathf.Lerp(0.1f, 0.9f, (float)random.NextDouble());
            float cy = Mathf.Lerp(0.1f, 0.9f, (float)random.NextDouble());
            Vector2 candidate = new Vector2(cx, cy);

            bool tooClose = false;
            for (int i = 0; i < centers.Count; i++)
            {
                if (Vector2.Distance(candidate, centers[i]) < minDist)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                centers.Add(candidate);
            }
        }

        while (centers.Count < count)
        {
            centers.Add(new Vector2((float)random.NextDouble(), (float)random.NextDouble()));
        }

        return centers.ToArray();
    }

    private static float GetContinentalInfluence(float nx, float ny, Vector2[] centers, float spread)
    {
        if (centers == null || centers.Length == 0)
        {
            return 0f;
        }

        float sigma = Mathf.Lerp(0.08f, 0.32f, Mathf.Clamp01(spread));
        float sigmaSq = sigma * sigma;

        float sum = 0f;
        for (int i = 0; i < centers.Length; i++)
        {
            Vector2 c = centers[i];
            float dx = nx - c.x;
            float dy = ny - c.y;
            float d2 = dx * dx + dy * dy;
            float influence = Mathf.Exp(-d2 / (2f * sigmaSq));
            sum += influence;
        }

        return Mathf.Clamp01(sum / Mathf.Max(1, centers.Length) * 1.8f);
    }

    private static float GetRadialDistance(float nx, float ny)
    {
        float x = nx * 2f - 1f;
        float y = ny * 2f - 1f;
        float dist = Mathf.Sqrt(x * x + y * y) / Mathf.Sqrt(2f);
        return Mathf.Clamp01(dist);
    }

    private void ClassifyBaseBiomes(int width, int height, float[,] elevation, float[,] moisture, float[,] ridge, HexBiome[,] biomeMap)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float e = elevation[x, y];
                if (e < config.waterThreshold)
                {
                    biomeMap[x, y] = HexBiome.Water;
                    continue;
                }

                bool mountainCandidate = e > config.mountainThreshold;
                bool ridgeAccepted = ridge[x, y] > config.ridgeThreshold;
                if (mountainCandidate && ridgeAccepted)
                {
                    biomeMap[x, y] = HexBiome.Mountains;
                    continue;
                }

                biomeMap[x, y] = moisture[x, y] < config.desertMoistureThreshold
                    ? HexBiome.Desert
                    : HexBiome.Plains;
            }
        }
    }

    private void ComputeDistanceToWater(int width, int height, HexBiome[,] biomeMap, int[,] waterDistance)
    {
        FillDistanceMap(width, height, waterDistance, int.MaxValue);

        var queue = new Queue<Vector2Int>(width * height / 4);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (biomeMap[x, y] == HexBiome.Water)
                {
                    waterDistance[x, y] = 0;
                    queue.Enqueue(new Vector2Int(x, y));
                }
            }
        }

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int currentDist = waterDistance[current.x, current.y];

            var offsets = ((current.y & 1) == 1) ? oddRowOffsets : evenRowOffsets;
            for (int i = 0; i < offsets.Length; i++)
            {
                int nx = current.x + offsets[i].x;
                int ny = current.y + offsets[i].y;
                if (!IsInBounds(nx, ny, width, height))
                {
                    continue;
                }

                int nextDist = currentDist + 1;
                if (nextDist < waterDistance[nx, ny])
                {
                    waterDistance[nx, ny] = nextDist;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
        }
    }

    private void ApplyClimateAdjustments(int width, int height, float[,] elevation, float[,] moisture, float[,] forestPatch,
        int[,] waterDistance, HexBiome[,] biomeMap)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                HexBiome biome = biomeMap[x, y];
                if (biome == HexBiome.Water || biome == HexBiome.Mountains)
                {
                    continue;
                }

                bool moistureDry = moisture[x, y] < config.desertMoistureThreshold;
                bool lowEnough = elevation[x, y] < config.desertMaxElevation;
                bool closeToWater = config.useCoastDrynessRule && waterDistance[x, y] <= config.coastWaterDistanceMax;

                bool canBeDesert = moistureDry && lowEnough && !closeToWater;
                biomeMap[x, y] = canBeDesert ? HexBiome.Desert : HexBiome.Plains;
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (biomeMap[x, y] != HexBiome.Plains)
                {
                    continue;
                }

                if (moisture[x, y] <= config.forestMoistureThreshold)
                {
                    continue;
                }

                if (config.useForestPatchBreakup && forestPatch[x, y] < config.forestPatchThreshold)
                {
                    continue;
                }

                if (config.useDesertAdjacencyPenalty)
                {
                    int desertNeighbors = CountNeighborsOfBiome(width, height, biomeMap, x, y, HexBiome.Desert);
                    if (desertNeighbors > config.maxDesertNeighborsForForest)
                    {
                        continue;
                    }
                }

                biomeMap[x, y] = HexBiome.Forest;
            }
        }
    }

    private void SmoothLandBiomes(int width, int height, HexBiome[,] biomeMap, int passes)
    {
        HexBiome[,] buffer = new HexBiome[width, height];

        for (int pass = 0; pass < passes; pass++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    HexBiome current = biomeMap[x, y];
                    if (!IsLandBiome(current))
                    {
                        buffer[x, y] = current;
                        continue;
                    }

                    if (TryGetLandNeighborMajority(width, height, biomeMap, x, y,
                            out HexBiome majority, out int majorityCount, out int currentCount))
                    {
                        if (majority != current && majorityCount >= 4 && currentCount <= 1)
                        {
                            buffer[x, y] = majority;
                            continue;
                        }
                    }

                    buffer[x, y] = current;
                }
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    biomeMap[x, y] = buffer[x, y];
                }
            }
        }
    }

    private bool TryGetLandNeighborMajority(int width, int height, HexBiome[,] biomeMap, int x, int y,
        out HexBiome majority, out int majorityCount, out int currentCount)
    {
        HexBiome current = biomeMap[x, y];
        majority = current;
        majorityCount = 0;
        currentCount = 0;

        var counts = new Dictionary<HexBiome, int>(3);
        var offsets = ((y & 1) == 1) ? oddRowOffsets : evenRowOffsets;

        int validLandNeighbors = 0;

        for (int i = 0; i < offsets.Length; i++)
        {
            int nx = x + offsets[i].x;
            int ny = y + offsets[i].y;
            if (!IsInBounds(nx, ny, width, height))
            {
                continue;
            }

            HexBiome neighbor = biomeMap[nx, ny];
            if (!IsLandBiome(neighbor))
            {
                continue;
            }

            validLandNeighbors++;
            if (neighbor == current)
            {
                currentCount++;
            }

            if (counts.TryGetValue(neighbor, out int count))
            {
                counts[neighbor] = count + 1;
            }
            else
            {
                counts[neighbor] = 1;
            }
        }

        if (validLandNeighbors < 3)
        {
            return false;
        }

        foreach (var pair in counts)
        {
            if (pair.Value > majorityCount)
            {
                majority = pair.Key;
                majorityCount = pair.Value;
            }
        }

        return majorityCount > 0;
    }

    private int CountNeighborsOfBiome(int width, int height, HexBiome[,] biomeMap, int x, int y, HexBiome target)
    {
        int count = 0;
        var offsets = ((y & 1) == 1) ? oddRowOffsets : evenRowOffsets;

        for (int i = 0; i < offsets.Length; i++)
        {
            int nx = x + offsets[i].x;
            int ny = y + offsets[i].y;
            if (!IsInBounds(nx, ny, width, height))
            {
                continue;
            }

            if (biomeMap[nx, ny] == target)
            {
                count++;
            }
        }

        return count;
    }

    private void ForceCenterVillageTile(int width, int height, HexBiome[,] biomeMap)
    {
        if (config == null || config.villagePrefab == null || biomeMap == null)
        {
            return;
        }

        int centerX = width / 2;
        int centerY = height / 2;
        if (!IsInBounds(centerX, centerY, width, height))
        {
            return;
        }

        biomeMap[centerX, centerY] = HexBiome.Plains;
    }

    private float SamplePerlin(int x, int y, float scale, float offsetX, float offsetY)
    {
        float s = Mathf.Max(0.0001f, scale);
        float sampleX = (x + offsetX) / s;
        float sampleY = (y + offsetY) / s;
        return Mathf.PerlinNoise(sampleX, sampleY);
    }

    private void SpawnTiles(int width, int height, float[,] elevation, float[,] moisture, HexBiome[,] biomeMap)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                HexBiome biome = biomeMap[x, y];
                bool isCenterTile = x == width / 2 && y == height / 2;
                GameObject prefab = isCenterTile && config.villagePrefab != null
                    ? config.villagePrefab
                    : GetBiomePrefab(biome);
                if (prefab == null)
                {
                    continue;
                }

                Vector3Int cell = new Vector3Int(x, y, 0);
                Vector3 position = useCellCenter ? hexGrid.GetCellCenterWorld(cell) : hexGrid.CellToWorld(cell);
                position.z = 0f;

                GameObject tileObject = Instantiate(prefab, position, Quaternion.identity, generatedRoot);
                tileObject.name = $"Hex_{x}_{y}_{biome}";

                var tile = tileObject.GetComponent<HexTile>();
                if (tile == null)
                {
                    tile = tileObject.AddComponent<HexTile>();
                }

                Vector2Int gridCoord = new Vector2Int(x, y);
                Vector2Int axialCoord = OffsetToAxialOddR(gridCoord);

                tile.Initialize(gridCoord, axialCoord, biome, elevation[x, y], moisture[x, y], isCenterTile);
                if (isCenterTile)
                {
                    ApplyVillageCityName(tile);
                }
                tilesByGridCoord[gridCoord] = tile;
            }
        }
    }

    private void FocusCameraOnVillageTile(int width, int height)
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
            if (worldCamera == null)
            {
                return;
            }
        }

        Vector2Int villageCoord = new Vector2Int(width / 2, height / 2);
        if (!tilesByGridCoord.TryGetValue(villageCoord, out HexTile villageTile) || villageTile == null)
        {
            foreach (HexTile tile in tilesByGridCoord.Values)
            {
                if (tile != null && tile.IsVillage)
                {
                    villageTile = tile;
                    break;
                }
            }
        }

        if (villageTile == null)
        {
            return;
        }

        var panController = worldCamera.GetComponent<CameraPanController>();
        if (panController != null)
        {
            panController.SetZoomToMin();
        }

        Vector3 camPos = worldCamera.transform.position;
        Vector3 targetPos = villageTile.transform.position;
        worldCamera.transform.position = new Vector3(targetPos.x, targetPos.y, camPos.z);
    }
    private void ApplyVillageCityName(HexTile tile)
    {
        if (tile == null || !tile.IsVillage)
        {
            return;
        }

        string cityName = ResolveVillageCityName();
        var label = tile.GetComponent<WorldMapVillageNameLabel>();
        if (label == null)
        {
            label = tile.gameObject.AddComponent<WorldMapVillageNameLabel>();
        }

        label.SetCityName(cityName);
    }

    private string ResolveVillageCityName()
    {
        const string fallbackName = "Unnamed City";

        var townHall = FindAnyObjectByType<TownHallCity>();
        if (townHall != null && !string.IsNullOrWhiteSpace(townHall.DisplayName))
        {
            return townHall.DisplayName;
        }

        var save = SaveGameManager.Instance;
        if (save != null && save.TryGetTownHallDisplayName(out string savedName) && !string.IsNullOrWhiteSpace(savedName))
        {
            return savedName;
        }

        return fallbackName;
    }
    private GameObject GetBiomePrefab(HexBiome biome)
    {
        return biome switch
        {
            HexBiome.Water => config.waterPrefab,
            HexBiome.Plains => config.plainsPrefab,
            HexBiome.Forest => config.forestPrefab,
            HexBiome.Desert => config.desertPrefab,
            HexBiome.Mountains => config.mountainsPrefab,
            _ => null
        };
    }

    private void TryLoadIdleMainScene()
    {
        var save = SaveGameManager.Instance;
        if (save != null)
        {
            save.SaveNow();
        }

        if (!string.IsNullOrWhiteSpace(idleMainSceneName) && Application.CanStreamedLevelBeLoaded(idleMainSceneName))
        {
            SceneManager.LoadScene(idleMainSceneName);
            return;
        }

        string[] fallbackNames = { "IdleMain", "idle_main" };
        for (int i = 0; i < fallbackNames.Length; i++)
        {
            string candidate = fallbackNames[i];
            if (Application.CanStreamedLevelBeLoaded(candidate))
            {
                SceneManager.LoadScene(candidate);
                return;
            }
        }

        int sceneCount = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < sceneCount; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            if (path.EndsWith("/IdleMain.unity", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("/idle_main.unity", StringComparison.OrdinalIgnoreCase))
            {
                SceneManager.LoadScene(i);
                return;
            }
        }

#if UNITY_EDITOR
        string[] editorPaths =
        {
            "Assets/Scenes/IdleMain.unity",
            "Assets/Scenes/idle_main.unity"
        };

        for (int i = 0; i < editorPaths.Length; i++)
        {
            string p = editorPaths[i];
            if (File.Exists(p))
            {
                EditorSceneManager.OpenScene(p);
                return;
            }
        }
#endif

        Debug.LogError("[WorldMap] Could not load Idle scene. Check Build Settings and idleMainSceneName.");
    }

    private bool ValidateConfig()
    {
        if (config == null)
        {
            Debug.LogError("[WorldMap] WorldMapConfig is not assigned.");
            return false;
        }

        if (config.waterPrefab == null || config.plainsPrefab == null || config.desertPrefab == null ||
            config.forestPrefab == null || config.mountainsPrefab == null)
        {
            Debug.LogError("[WorldMap] Missing one or more biome prefab references in WorldMapConfig.");
            return false;
        }

        return true;
    }

    private static bool IsLandBiome(HexBiome biome)
    {
        return biome == HexBiome.Plains || biome == HexBiome.Forest || biome == HexBiome.Desert;
    }

    private static bool IsInBounds(int x, int y, int width, int height)
    {
        return x >= 0 && y >= 0 && x < width && y < height;
    }

    private static void FillDistanceMap(int width, int height, int[,] distanceMap, int value)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                distanceMap[x, y] = value;
            }
        }
    }

    private void EnsureGrid()
    {
        if (hexGrid == null)
        {
            hexGrid = FindAnyObjectByType<Grid>();
        }

        if (hexGrid == null)
        {
            Debug.LogError("[WorldMap] Grid reference is missing. Assign Hexgrid in WorldMapManager.");
        }
    }

    private void EnsureGeneratedRoot()
    {
        if (generatedRoot != null)
        {
            return;
        }

        Transform existing = transform.Find("GeneratedHexes");
        if (existing != null)
        {
            generatedRoot = existing;
            return;
        }

        var root = new GameObject("GeneratedHexes");
        generatedRoot = root.transform;
        generatedRoot.SetParent(transform, false);
    }

    private void ClearGeneratedTiles()
    {
        if (generatedRoot == null)
        {
            return;
        }

        for (int i = generatedRoot.childCount - 1; i >= 0; i--)
        {
            var child = generatedRoot.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }
}








