using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using IdleHra.BuildingSystem;

public class SaveGameManager : MonoBehaviour
{
    [Serializable]
    private class SaveData
    {
        public int version = 1;
        public long savedAtUtcTicks;
        public int storageCapacity;
        public List<ResourceManager.ResourceSnapshot> resources = new List<ResourceManager.ResourceSnapshot>();
        public List<string> purchasedResearchIds = new List<string>();
        public List<string> purchasedBlessingIds = new List<string>();
        public List<GridBuildingSystem.PlacedBuildingSaveData> placedBuildings = new List<GridBuildingSystem.PlacedBuildingSaveData>();
        public string townHallName = "";
        public bool hasWorldMapSeed;
        public int worldMapSeed;
        public int currentEraIndex;
        public List<FiniteNodeSaveData> finiteNodes = new List<FiniteNodeSaveData>();
    }

    [Serializable]
    private class FiniteNodeSaveData
    {
        public string id = string.Empty;
        public int currentAmount;
    }
    public static SaveGameManager Instance { get; private set; }

    [Header("Save File")]
    [SerializeField] private string saveFileName = "save_v1.json";
    [SerializeField] private string backupFileName = "save_v1.backup.json";

    [Header("Auto Save")]
    [SerializeField] private bool autoSave = true;
    [SerializeField] private float autoSaveIntervalSeconds = 20f;

    [Header("Auto Load")]
    [SerializeField] private bool autoLoadOnIdleSceneLoad = true;
    [SerializeField] private string idleMainSceneName = "IdleMain";
    [SerializeField] private string tutorialCompletedPrefKey = "ClickerKingdom.Tutorial.Completed";
    [SerializeField] private string legacyResearchPrefKey = "ClickerKingdom.Research.Purchased";
    [SerializeField] private string legacyBlessingPrefKey = "ClickerKingdom.Blessings.Purchased";

    [Header("Import / Export")]
    [SerializeField] private string exchangeDirectoryName = "Idle_hra";
    [SerializeField] private string exportFileName = "save_export.json";
    [SerializeField] private string importFileName = "save_import.json";
    
    [Header("Global Production")]
    [SerializeField] private bool simulateGlobalProductionOutsideIdleScene = true;
    [SerializeField] private float globalProductionTickSeconds = 1f;

    private SaveData currentData = new SaveData();
    private float autoSaveTimer;
    private bool isApplyingLoad;
    private bool hasLoadedData;
    
    private float globalProductionAccumulator;
    private readonly Dictionary<string, float> globalProductionCarryByResourceId = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void IdleHra_SaveExport(string fileName, string base64Content);

    [DllImport("__Internal")]
    private static extern void IdleHra_OpenImportFilePicker(string gameObjectName, string callbackMethodName);
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        var existing = FindAnyObjectByType<SaveGameManager>();
        if (existing != null)
        {
            return;
        }

        var go = new GameObject("SaveGameManager");
        go.AddComponent<SaveGameManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        hasLoadedData = LoadFromDisk();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Update()
    {
        if (isApplyingLoad)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;

        if (autoSave)
        {
            autoSaveTimer += dt;
            if (autoSaveTimer >= Mathf.Max(1f, autoSaveIntervalSeconds))
            {
                autoSaveTimer = 0f;
                SaveNow();
            }
        }

        UpdateGlobalProduction(dt);
    }

    private void UpdateGlobalProduction(float deltaTime)
    {
        if (!simulateGlobalProductionOutsideIdleScene)
        {
            return;
        }

        if (!ShouldRunGlobalProduction())
        {
            globalProductionAccumulator = 0f;
            return;
        }

        float tickStep = Mathf.Max(0.1f, globalProductionTickSeconds);
        globalProductionAccumulator += Mathf.Max(0f, deltaTime);

        int safety = 0;
        while (globalProductionAccumulator >= tickStep)
        {
            globalProductionAccumulator -= tickStep;
            ApplyGlobalProductionTick(tickStep);

            safety++;
            if (safety > 1000)
            {
                globalProductionAccumulator = 0f;
                break;
            }
        }
    }

    private bool ShouldRunGlobalProduction()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            return false;
        }

        return !IsIdleScene(activeScene.name);
    }

    private void ApplyGlobalProductionTick(float tickDelta)
    {
        if (tickDelta <= 0f || currentData == null)
        {
            return;
        }

        // Ensure ResourceManager exists in non-idle scenes so HUD can display clicked resources
        // even when there are no passive producers yet.
        var resourceManager = GetOrCreateResourceManager();
        if (resourceManager == null)
        {
            return;
        }

        if (currentData.placedBuildings == null || currentData.placedBuildings.Count == 0)
        {
            return;
        }

        var typeLookup = BuildBuildingTypeLookup();
        if (typeLookup == null || typeLookup.Count == 0)
        {
            return;
        }

        var modifiers = GlobalModifiers.Instance != null
            ? GlobalModifiers.Instance
            : FindAnyObjectByType<GlobalModifiers>();

        var perSecondRates = new Dictionary<ResourceType, float>();

        for (int i = 0; i < currentData.placedBuildings.Count; i++)
        {
            var state = currentData.placedBuildings[i];
            if (state == null || string.IsNullOrWhiteSpace(state.buildingTypeId))
            {
                continue;
            }

            if (!typeLookup.TryGetValue(state.buildingTypeId, out var type) || type == null || type.Prefab == null)
            {
                continue;
            }

            var producer = type.Prefab.GetComponent<BuildingProducer>();
            if (producer == null)
            {
                producer = type.Prefab.GetComponentInChildren<BuildingProducer>(true);
            }

            if (!TryResolveSavedProduction(type, state, producer, out var outputResource, out float amountPerCycle, out float intervalSeconds, out bool hasInputCosts))
            {
                continue;
            }

            if (hasInputCosts)
            {
                 // Global/off-scene simulation currently supports only pure producers.
                continue;
            }

            float multiplier = modifiers != null
                ? modifiers.GetProductionMultiplier(outputResource, type)
                : 1f;

            float ratePerSecond = (amountPerCycle / Mathf.Max(0.01f, intervalSeconds)) * Mathf.Max(0f, multiplier);
            if (ratePerSecond <= 0f)
            {
                continue;
            }

            if (perSecondRates.TryGetValue(outputResource, out float existing))
            {
                perSecondRates[outputResource] = existing + ratePerSecond;
            }
            else
            {
                perSecondRates[outputResource] = ratePerSecond;
            }
        }

        foreach (var pair in perSecondRates)
        {
            ResourceType resourceType = pair.Key;
            if (resourceType == null)
            {
                continue;
            }

            float produced = pair.Value * tickDelta;
            if (produced <= 0f)
            {
                continue;
            }

            string resourceKey = string.IsNullOrWhiteSpace(resourceType.Id) ? resourceType.name : resourceType.Id;
            globalProductionCarryByResourceId.TryGetValue(resourceKey, out float carry);

            float total = carry + produced;
            int wholeAmount = Mathf.FloorToInt(total);
            float remainder = total - wholeAmount;

            globalProductionCarryByResourceId[resourceKey] = remainder;

            if (wholeAmount <= 0)
            {
                continue;
            }

            resourceManager.Add(resourceType, wholeAmount);
        }
    }

    private static bool TryResolveSavedProduction(
        BuildingType type,
        GridBuildingSystem.PlacedBuildingSaveData state,
        BuildingProducer producer,
        out ResourceType outputResource,
        out float amountPerCycle,
        out float intervalSeconds,
        out bool hasInputCosts)
    {
        outputResource = null;
        amountPerCycle = 0f;
        intervalSeconds = 1f;
        hasInputCosts = false;

        if (type == null || state == null || producer == null)
        {
            return false;
        }

        outputResource = producer.OutputResource;
        if (outputResource == null)
        {
            return false;
        }

        amountPerCycle = Mathf.Max(0f, producer.AmountPerCycle);
        intervalSeconds = Mathf.Max(0.01f, producer.IntervalSeconds);
        hasInputCosts = HasInputCosts(producer.InputResourcesPerCycle);

        var levels = type.UpgradeLevels;
        if (state.level >= 0 && levels != null && levels.Count > 0)
        {
            int levelIndex = Mathf.Clamp(state.level, 0, levels.Count - 1);
            var level = levels[levelIndex];
            if (level != null)
            {
                amountPerCycle = Mathf.Max(0f, level.amountPerCycle);
                intervalSeconds = Mathf.Max(0.01f, level.intervalSeconds);
                hasInputCosts = HasInputCosts(level.inputResourcesPerCycle);
            }
        }

        return amountPerCycle > 0f;
    }

    private static bool HasInputCosts(System.Collections.Generic.IReadOnlyList<BuildingType.ResourceCost> costs)
    {
        if (costs == null)
        {
            return false;
        }

        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost.resourceType != null && cost.amount > 0)
            {
                return true;
            }
        }

        return false;
    }
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveNow();
        }
    }

    private void OnApplicationQuit()
    {
        SaveNow();
    }

    public void SaveNow()
    {
        if (isApplyingLoad)
        {
            return;
        }

        bool captured = CaptureRuntimeState();
        if (!captured && !hasLoadedData)
        {
            return;
        }

        hasLoadedData = true;
        currentData.savedAtUtcTicks = DateTime.UtcNow.Ticks;
        WriteToDisk(currentData);
    }
    public bool TryGetWorldMapSeed(out int seed)
    {
        if (currentData != null && currentData.hasWorldMapSeed)
        {
            seed = currentData.worldMapSeed;
            return true;
        }

        seed = 0;
        return false;
    }

    public void SetWorldMapSeed(int seed)
    {
        if (currentData == null)
        {
            currentData = new SaveData();
        }

        currentData.hasWorldMapSeed = true;
        currentData.worldMapSeed = seed;
        hasLoadedData = true;
    }
    public bool TryExportSave(out string exportPath, out string errorMessage)
    {
        exportPath = string.Empty;
        errorMessage = string.Empty;

        try
        {
            SaveNow();

            string sourcePath = GetPrimaryPath();
            if (!File.Exists(sourcePath))
            {
                sourcePath = GetBackupPath();
            }

            if (!File.Exists(sourcePath))
            {
                errorMessage = "No save file exists yet.";
                return false;
            }

            string exchangeDir = GetExchangeDirectoryPath();
            if (!Directory.Exists(exchangeDir))
            {
                Directory.CreateDirectory(exchangeDir);
            }

            exportPath = Path.Combine(exchangeDir, exportFileName);
            File.Copy(sourcePath, exportPath, true);
            return true;
        }
        catch (Exception e)
        {
            errorMessage = e.Message;
            return false;
        }
    }

    public bool TryImportSave(out string importPath, out string errorMessage)
    {
        importPath = GetImportFilePath();
        errorMessage = string.Empty;

        try
        {
            if (!File.Exists(importPath))
            {
                string fallbackPath = GetExportFilePath();
                if (File.Exists(fallbackPath))
                {
                    importPath = fallbackPath;
                }
                else
                {
                    errorMessage = $"Import file not found. Expected: '{GetImportFilePath()}' or fallback '{fallbackPath}'.";
                    return false;
                }
            }

            string primaryPath = GetPrimaryPath();
            string backupPath = GetBackupPath();

            string dir = Path.GetDirectoryName(primaryPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (File.Exists(primaryPath))
            {
                File.Copy(primaryPath, backupPath, true);
            }

            File.Copy(importPath, primaryPath, true);

            hasLoadedData = LoadFromDisk();
            if (!hasLoadedData)
            {
                errorMessage = "Imported file could not be loaded as save data.";
                return false;
            }

            ReloadIdleSceneOrActive();
            return true;
        }
        catch (Exception e)
        {
            errorMessage = e.Message;
            return false;
        }
    }

    public string GetImportFilePath()
    {
        return Path.Combine(GetExchangeDirectoryPath(), importFileName);
    }

    public string GetExportFilePath()
    {
        return Path.Combine(GetExchangeDirectoryPath(), exportFileName);
    }
    public bool TryExportSaveForPlatform(out string resultMessage, out string errorMessage)
    {
        resultMessage = string.Empty;
        errorMessage = string.Empty;

#if UNITY_WEBGL && !UNITY_EDITOR
        if (!TryGetCurrentSaveJson(out string json, out errorMessage))
        {
            return false;
        }

        string fileName = string.IsNullOrWhiteSpace(exportFileName) ? "save_export.json" : exportFileName;
        string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        IdleHra_SaveExport(fileName, base64);
        resultMessage = fileName;
        return true;
#else
        if (TryExportSave(out string exportPath, out errorMessage))
        {
            resultMessage = exportPath;
            return true;
        }

        return false;
#endif
    }

    public bool TryImportSaveForPlatform(out string resultMessage, out string errorMessage)
    {
        resultMessage = GetImportFilePath();
        errorMessage = string.Empty;

#if UNITY_WEBGL && !UNITY_EDITOR
        IdleHra_OpenImportFilePicker(gameObject.name, nameof(OnWebGLImportBase64));
        resultMessage = "File picker opened.";
        return true;
#else
        if (TryImportSave(out string importPath, out errorMessage))
        {
            resultMessage = importPath;
            return true;
        }

        return false;
#endif
    }

    // Called from WebGL .jslib via SendMessage(gameObjectName, methodName, base64String)
    public void OnWebGLImportBase64(string base64Json)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (string.IsNullOrWhiteSpace(base64Json))
        {
            Debug.LogWarning("SaveGameManager: WebGL import callback received empty payload.");
            return;
        }

        try
        {
            string json = Encoding.UTF8.GetString(Convert.FromBase64String(base64Json));
            if (TryImportSaveFromJson(json, out string errorMessage))
            {
                Debug.Log("SaveGameManager: WebGL save imported successfully.");
                return;
            }

            Debug.LogWarning($"SaveGameManager: WebGL import failed. {errorMessage}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SaveGameManager: WebGL import decode failed. {e.Message}");
        }
#endif
    }

    public bool TryImportSaveFromJson(string json, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            errorMessage = "Import JSON is empty.";
            return false;
        }

        try
        {
            SaveData imported = JsonUtility.FromJson<SaveData>(json);
            if (imported == null)
            {
                errorMessage = "Invalid save JSON format.";
                return false;
            }

            currentData = imported;
            hasLoadedData = true;
            isApplyingLoad = false;
            autoSaveTimer = 0f;
        globalProductionAccumulator = 0f;
        globalProductionCarryByResourceId.Clear();
            currentData.savedAtUtcTicks = DateTime.UtcNow.Ticks;
            WriteToDisk(currentData);
            ReloadIdleSceneOrActive();
            return true;
        }
        catch (Exception e)
        {
            errorMessage = e.Message;
            return false;
        }
    }

    public bool TryGetCurrentSaveJson(out string json, out string errorMessage)
    {
        json = string.Empty;
        errorMessage = string.Empty;

        try
        {
            SaveNow();

            string sourcePath = GetPrimaryPath();
            if (!File.Exists(sourcePath))
            {
                sourcePath = GetBackupPath();
            }

            if (!File.Exists(sourcePath))
            {
                errorMessage = "No save file exists yet.";
                return false;
            }

            json = File.ReadAllText(sourcePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                errorMessage = "Save file is empty.";
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            errorMessage = e.Message;
            return false;
        }
    }
    public bool TryGetSavedFiniteNodeAmount(string nodeId, out int amount)
    {
        amount = 0;

        if (!hasLoadedData || currentData == null || string.IsNullOrWhiteSpace(nodeId) || currentData.finiteNodes == null)
        {
            return false;
        }

        for (int i = 0; i < currentData.finiteNodes.Count; i++)
        {
            var saved = currentData.finiteNodes[i];
            if (saved == null || string.IsNullOrWhiteSpace(saved.id))
            {
                continue;
            }

            if (!string.Equals(saved.id, nodeId, StringComparison.Ordinal))
            {
                continue;
            }

            amount = Mathf.Max(0, saved.currentAmount);
            return true;
        }

        return false;
    }
    public bool TryGetTownHallDisplayName(out string cityName)
    {
        cityName = string.Empty;

        if (currentData != null && !string.IsNullOrWhiteSpace(currentData.townHallName))
        {
            cityName = currentData.townHallName.Trim();
            return true;
        }

        var townHall = FindAnyObjectByType<TownHallCity>();
        if (townHall != null && !string.IsNullOrWhiteSpace(townHall.DisplayName))
        {
            cityName = townHall.DisplayName;
            return true;
        }

        return false;
    }
    public void ResetProgressAndRestart()
    {
        try
        {
            string primaryPath = GetPrimaryPath();
            string backupPath = GetBackupPath();

            if (File.Exists(primaryPath))
            {
                File.Delete(primaryPath);
            }

            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SaveGameManager: Failed deleting save files during reset. {e.Message}");
        }

        currentData = new SaveData();
        hasLoadedData = false;
        isApplyingLoad = false;
        autoSaveTimer = 0f;
        globalProductionAccumulator = 0f;
        globalProductionCarryByResourceId.Clear();

        if (!string.IsNullOrWhiteSpace(tutorialCompletedPrefKey))
        {
            PlayerPrefs.DeleteKey(tutorialCompletedPrefKey);
        }

        if (!string.IsNullOrWhiteSpace(legacyResearchPrefKey))
        {
            PlayerPrefs.DeleteKey(legacyResearchPrefKey);
        }

        if (!string.IsNullOrWhiteSpace(legacyBlessingPrefKey))
        {
            PlayerPrefs.DeleteKey(legacyBlessingPrefKey);
        }

        PlayerPrefs.Save();

        var modifiers = GlobalModifiers.Instance != null
            ? GlobalModifiers.Instance
            : FindAnyObjectByType<GlobalModifiers>();
        if (modifiers != null)
        {
            modifiers.ResetToBase();
        }

        var resourceManager = ResourceManager.Instance != null
            ? ResourceManager.Instance
            : FindAnyObjectByType<ResourceManager>();
        if (resourceManager != null)
        {
            if (resourceManager.HasConfiguredStartingResources)
            {
                resourceManager.ResetToStartingResources();
            }
            else
            {
                Destroy(resourceManager);
            }
        }

        ResetManualClickToBase();
        ResetAllBuildingTypeDebugFlags();

        if (!string.IsNullOrWhiteSpace(idleMainSceneName) && Application.CanStreamedLevelBeLoaded(idleMainSceneName))
        {
            SceneManager.LoadScene(idleMainSceneName);
            return;
        }

        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid() && !string.IsNullOrWhiteSpace(active.name))
        {
            SceneManager.LoadScene(active.name);
        }
    }

    public bool HasSaveFile()
    {
        return File.Exists(GetPrimaryPath()) || File.Exists(GetBackupPath());
    }

    public void ApplyToCurrentIdleScene()
    {
        if (!hasLoadedData)
        {
            return;
        }
        if (currentData == null)
        {
            return;
        }

        isApplyingLoad = true;
        try
        {
            var modifiers = GlobalModifiers.Instance != null
                ? GlobalModifiers.Instance
                : FindAnyObjectByType<GlobalModifiers>();
            if (modifiers != null)
            {
                modifiers.ResetToBase();
            }

            var researchManager = ResearchManager.Instance != null
                ? ResearchManager.Instance
                : FindAnyObjectByType<ResearchManager>();
            if (researchManager != null)
            {
                researchManager.SetPurchasedIds(currentData.purchasedResearchIds, false);
            }

            var blessingManager = BlessingManager.Instance != null
                ? BlessingManager.Instance
                : FindAnyObjectByType<BlessingManager>();
            if (blessingManager != null)
            {
                blessingManager.SetPurchasedIds(currentData.purchasedBlessingIds, false);
            }

            if (researchManager != null)
            {
                researchManager.ApplyPurchasedEffects();
            }

            if (blessingManager != null)
            {
                blessingManager.ApplyPurchasedEffects();
            }

            var resourceManager = ResourceManager.Instance != null
                ? ResourceManager.Instance
                : FindAnyObjectByType<ResourceManager>();
            if (resourceManager != null)
            {
                resourceManager.RestoreRuntimeState(currentData.resources);
            }

            var gridBuildingSystem = GridBuildingSystem.Instance != null
                ? GridBuildingSystem.Instance
                : FindAnyObjectByType<GridBuildingSystem>();
            if (gridBuildingSystem != null)
            {
                var typeLookup = BuildBuildingTypeLookup();
                gridBuildingSystem.RestorePlacedBuildingStates(currentData.placedBuildings, typeLookup);
            }

            var townHall = FindAnyObjectByType<TownHallCity>();
            if (townHall != null)
            {
                townHall.SetNameDirect(currentData.townHallName);
            }

            var eraManager = EraProgressionManager.Instance != null
                ? EraProgressionManager.Instance
                : FindAnyObjectByType<EraProgressionManager>();
            if (eraManager != null)
            {
                eraManager.SetCurrentEraIndexDirect(currentData.currentEraIndex);
            }

            ApplyFiniteNodeStates(currentData.finiteNodes);

        }
        finally
        {
            isApplyingLoad = false;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!autoLoadOnIdleSceneLoad)
        {
            return;
        }

        if (!IsIdleScene(scene.name))
        {
            return;
        }

        ApplyToCurrentIdleScene();
    }

    private bool IsIdleScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        if (sceneName.Equals(idleMainSceneName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return sceneName.Equals("idle_main", StringComparison.OrdinalIgnoreCase);
    }

    private bool CaptureRuntimeState()
    {
        if (currentData == null)
        {
            currentData = new SaveData();
        }

        bool hasAnySource = false;

        var resourceManager = ResourceManager.Instance != null
            ? ResourceManager.Instance
            : FindAnyObjectByType<ResourceManager>();
        if (resourceManager != null)
        {
            hasAnySource = true;
            currentData.resources = resourceManager.CaptureRuntimeState();
        }

        var researchManager = ResearchManager.Instance != null
            ? ResearchManager.Instance
            : FindAnyObjectByType<ResearchManager>();
        if (researchManager != null)
        {
            hasAnySource = true;
            currentData.purchasedResearchIds = researchManager.GetPurchasedIds();
        }

        var blessingManager = BlessingManager.Instance != null
            ? BlessingManager.Instance
            : FindAnyObjectByType<BlessingManager>();
        if (blessingManager != null)
        {
            hasAnySource = true;
            currentData.purchasedBlessingIds = blessingManager.GetPurchasedIds();
        }

        var gridBuildingSystem = GridBuildingSystem.Instance != null
            ? GridBuildingSystem.Instance
            : FindAnyObjectByType<GridBuildingSystem>();
        if (gridBuildingSystem != null)
        {
            hasAnySource = true;
            currentData.placedBuildings = gridBuildingSystem.CapturePlacedBuildingStates();
        }

        var townHall = FindAnyObjectByType<TownHallCity>();
        if (townHall != null)
        {
            hasAnySource = true;
            currentData.townHallName = townHall.SavedCityName;
        }

        var eraManager = EraProgressionManager.Instance != null
            ? EraProgressionManager.Instance
            : FindAnyObjectByType<EraProgressionManager>();
        if (eraManager != null)
        {
            hasAnySource = true;
            currentData.currentEraIndex = eraManager.CurrentEraIndex;
        }

        var finiteNodes = FindObjectsByType<FiniteResourceNode>(FindObjectsSortMode.None);
        if (finiteNodes != null && finiteNodes.Length > 0)
        {
            hasAnySource = true;
            currentData.finiteNodes = CaptureFiniteNodeStates(finiteNodes);
        }

        return hasAnySource;
    }

    
    private static void ResetManualClickToBase()
    {
        var manualClick = ManualClickSystem.Instance != null
            ? ManualClickSystem.Instance
            : FindAnyObjectByType<ManualClickSystem>();
        if (manualClick == null)
        {
            return;
        }

        manualClick.SetCurrentClickAmount(manualClick.BaseClickAmount);
    }

    private static void ResetAllBuildingTypeDebugFlags()
    {
        var allTypes = Resources.FindObjectsOfTypeAll<BuildingType>();
        if (allTypes == null || allTypes.Length == 0)
        {
            return;
        }

        for (int i = 0; i < allTypes.Length; i++)
        {
            var type = allTypes[i];
            if (type == null)
            {
                continue;
            }

            type.SetDebugFlags(false, false);
        }
    }

    private static List<FiniteNodeSaveData> CaptureFiniteNodeStates(FiniteResourceNode[] nodes)
    {
        var result = new List<FiniteNodeSaveData>();
        if (nodes == null || nodes.Length == 0)
        {
            return result;
        }

        for (int i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            if (node == null)
            {
                continue;
            }

            string id = node.PersistentId;
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            result.Add(new FiniteNodeSaveData
            {
                id = id,
                currentAmount = node.CurrentAmount
            });
        }

        return result;
    }

    private static void ApplyFiniteNodeStates(List<FiniteNodeSaveData> savedNodes)
    {
        if (savedNodes == null || savedNodes.Count == 0)
        {
            return;
        }

        var lookup = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < savedNodes.Count; i++)
        {
            var saved = savedNodes[i];
            if (saved == null || string.IsNullOrWhiteSpace(saved.id))
            {
                continue;
            }

            lookup[saved.id] = Mathf.Max(0, saved.currentAmount);
        }

        if (lookup.Count == 0)
        {
            return;
        }

        var sceneNodes = FindObjectsByType<FiniteResourceNode>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneNodes.Length; i++)
        {
            var node = sceneNodes[i];
            if (node == null)
            {
                continue;
            }

            string id = node.PersistentId;
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (lookup.TryGetValue(id, out int amount))
            {
                node.SetCurrentAmountFromSave(amount);
            }
        }
    }
    private Dictionary<string, BuildingType> BuildBuildingTypeLookup()
    {
        var lookup = new Dictionary<string, BuildingType>(StringComparer.OrdinalIgnoreCase);

        var hud = FindAnyObjectByType<GameHUDController>();
        if (hud != null && hud.ConfiguredBuildingTypes != null)
        {
            var configured = hud.ConfiguredBuildingTypes;
            for (int i = 0; i < configured.Count; i++)
            {
                RegisterBuildingType(lookup, configured[i]);
            }
        }

        var allTypes = Resources.FindObjectsOfTypeAll<BuildingType>();
        for (int i = 0; i < allTypes.Length; i++)
        {
            RegisterBuildingType(lookup, allTypes[i]);
        }

        return lookup;
    }

    private static void RegisterBuildingType(Dictionary<string, BuildingType> lookup, BuildingType type)
    {
        if (lookup == null || type == null)
        {
            return;
        }

        string key = GetBuildingTypeKey(type);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (!lookup.ContainsKey(key))
        {
            lookup.Add(key, type);
        }
    }

    private static string GetBuildingTypeKey(BuildingType type)
    {
        if (type == null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(type.Id) ? type.name : type.Id;
    }

    private bool LoadFromDisk()
    {
        string path = GetPrimaryPath();
        string backupPath = GetBackupPath();

        if (TryReadSave(path, out SaveData loadedPrimary))
        {
            currentData = loadedPrimary ?? new SaveData();
            globalProductionAccumulator = 0f;
            globalProductionCarryByResourceId.Clear();
            return true;
        }

        if (TryReadSave(backupPath, out SaveData loadedBackup))
        {
            currentData = loadedBackup ?? new SaveData();
            globalProductionAccumulator = 0f;
            globalProductionCarryByResourceId.Clear();
            return true;
        }

        currentData = new SaveData();
        globalProductionAccumulator = 0f;
        globalProductionCarryByResourceId.Clear();
        return false;
    }
    private ResourceManager GetOrCreateResourceManager()
    {
        var manager = ResourceManager.Instance != null
            ? ResourceManager.Instance
            : FindAnyObjectByType<ResourceManager>();

        if (manager != null)
        {
            return manager;
        }

        var go = new GameObject("ResourceManager_Runtime");
        manager = go.AddComponent<ResourceManager>();
        DontDestroyOnLoad(go);

        if (currentData != null)
        {
            manager.RestoreRuntimeState(currentData.resources);
        }

        return manager;
    }
    private static bool TryReadSave(string path, out SaveData data)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            data = JsonUtility.FromJson<SaveData>(json);
            return data != null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SaveGameManager: Failed to read save '{path}'. {e.Message}");
            return false;
        }
    }

    private void WriteToDisk(SaveData data)
    {
        if (data == null)
        {
            return;
        }

        string primaryPath = GetPrimaryPath();
        string backupPath = GetBackupPath();
        string tmpPath = primaryPath + ".tmp";

        try
        {
            string dir = Path.GetDirectoryName(primaryPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(tmpPath, json);

            if (File.Exists(primaryPath))
            {
                File.Copy(primaryPath, backupPath, true);
                File.Delete(primaryPath);
            }

            File.Move(tmpPath, primaryPath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SaveGameManager: Failed to write save. {e.Message}");
            try
            {
                if (File.Exists(tmpPath))
                {
                    File.Delete(tmpPath);
                }
            }
            catch
            {
                 // ignored
            }
        }
    }
    private string GetExchangeDirectoryPath()
    {
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documentsPath))
        {
            documentsPath = Application.persistentDataPath;
        }

        string folderName = string.IsNullOrWhiteSpace(exchangeDirectoryName) ? "Idle_hra" : exchangeDirectoryName;
        return Path.Combine(documentsPath, folderName);
    }

    private void ReloadIdleSceneOrActive()
    {
        if (!string.IsNullOrWhiteSpace(idleMainSceneName) && Application.CanStreamedLevelBeLoaded(idleMainSceneName))
        {
            SceneManager.LoadScene(idleMainSceneName);
            return;
        }

        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid() && !string.IsNullOrWhiteSpace(active.name))
        {
            SceneManager.LoadScene(active.name);
        }
    }

    private string GetPrimaryPath()
    {
        return Path.Combine(Application.persistentDataPath, saveFileName);
    }

    private string GetBackupPath()
    {
        return Path.Combine(Application.persistentDataPath, backupFileName);
    }
}









































