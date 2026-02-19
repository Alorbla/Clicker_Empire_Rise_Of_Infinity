using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public sealed class HarmonyIndicatorController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private string harmonyLabelName = "HarmonyLabel";
    [SerializeField] private string harmonyLabelFormat = "Harmony {0}%";

    [Header("Sources")]
    [SerializeField] private GameHUDController gameHudController;
    [SerializeField] private List<ResourceType> fallbackResourceTypes = new List<ResourceType>();

    [Header("Thresholds")]
    [SerializeField] private bool useHudThresholds = true;
    [SerializeField, Range(0f, 1f)] private float band1Threshold = 0.2f;
    [SerializeField, Range(0f, 1f)] private float band2Threshold = 0.4f;
    [SerializeField, Range(0f, 1f)] private float band3Threshold = 0.6f;
    [SerializeField, Range(0f, 1f)] private float band4Threshold = 0.8f;
    [SerializeField, Range(0f, 1f)] private float band5Threshold = 0.9f;

    [Header("Harmony Weights")]
    [SerializeField, Range(0f, 1f)] private float yellowBandWeight = 0.35f;
    [SerializeField, Range(0f, 1f)] private float greenBandWeight = 0.65f;
    [SerializeField, Range(0f, 1f)] private float blueBandWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float redBandWeight = 0f;

    [Header("Visual")]
    [SerializeField] private bool useHudHarmonyColor = true;
    [SerializeField] private Color harmonyTextColor = new Color(0.33f, 0.65f, 1f, 1f);

    [Header("Harmony Text Color Thresholds")]
    [SerializeField, Range(0, 100)] private int blueAbovePercent = 70;
    [SerializeField, Range(0, 100)] private int greenAbovePercent = 50;
    [SerializeField, Range(0, 100)] private int yellowAbovePercent = 20;
    [SerializeField] private Color harmonyBlueColor = new Color(0.33f, 0.65f, 1f, 1f);
    [SerializeField] private Color harmonyGreenColor = new Color(0.67f, 0.95f, 0.55f, 1f);
    [SerializeField] private Color harmonyYellowColor = new Color(1f, 0.95f, 0.35f, 1f);
    [SerializeField] private Color harmonyRedColor = new Color(0.88f, 0.24f, 0.18f, 1f);

    private UIDocument uiDocument;
    private int currentHarmonyPercent;
    private Label harmonyLabel;

    public int CurrentHarmonyPercent => Mathf.Clamp(currentHarmonyPercent, 0, 100);
    private ResourceManager boundResourceManager;

    private enum ResourceBand
    {
        Band1,
        Band2,
        Band3,
        Band4,
        Band5,
        Band6,
        Unlimited
    }

    private void OnEnable()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (gameHudController == null)
        {
            gameHudController = GetComponent<GameHUDController>();
        }

        ResolveHarmonyLabel();
        TryBindResourceManager();
        RefreshHarmony();
    }

    private void OnDisable()
    {
        UnbindResourceManager();
    }

    private void Update()
    {
        if (boundResourceManager == null)
        {
            TryBindResourceManager();
            RefreshHarmony();
        }
    }

    private void TryBindResourceManager()
    {
        var manager = ResourceManager.Instance;
        if (manager == null)
        {
            manager = Object.FindAnyObjectByType<ResourceManager>();
        }

        if (manager == null || manager == boundResourceManager)
        {
            return;
        }

        UnbindResourceManager();

        boundResourceManager = manager;
        boundResourceManager.ResourceChanged += HandleResourceChanged;
        boundResourceManager.StorageChanged += HandleStorageChanged;
    }

    private void UnbindResourceManager()
    {
        if (boundResourceManager == null)
        {
            return;
        }

        boundResourceManager.ResourceChanged -= HandleResourceChanged;
        boundResourceManager.StorageChanged -= HandleStorageChanged;
        boundResourceManager = null;
    }

    private void HandleResourceChanged(ResourceType _, int __)
    {
        RefreshHarmony();
    }

    private void HandleStorageChanged(int _, int __)
    {
        RefreshHarmony();
    }

    private void ResolveHarmonyLabel()
    {
        if (uiDocument == null)
        {
            return;
        }

        var root = uiDocument.rootVisualElement;
        harmonyLabel = root != null ? root.Q<Label>(harmonyLabelName) : null;
    }

    private IEnumerable<ResourceType> GetTrackedResources()
    {
        if (gameHudController != null)
        {
            var unlocked = gameHudController.HarmonyUnlockedResources;
            if (unlocked != null)
            {
                return unlocked;
            }

            var configured = gameHudController.ConfiguredResourceTypes;
            if (configured != null)
            {
                return configured;
            }
        }

        return fallbackResourceTypes;
    }

    private void RefreshHarmony()
    {
        if (harmonyLabel == null)
        {
            ResolveHarmonyLabel();
            if (harmonyLabel == null)
            {
                return;
            }
        }

        var manager = boundResourceManager != null ? boundResourceManager : ResourceManager.Instance;
        if (manager == null)
        {
            SetHarmonyLabel(0);
            return;
        }

        float totalScore = 0f;
        int countedResources = 0;

        foreach (var type in GetTrackedResources())
        {
            if (type == null || !type.ShowInHUD)
            {
                continue;
            }

            int cap = manager.GetCapacity(type);
            if (cap <= 0)
            {
                continue;
            }

            int amount = manager.Get(type);
            var band = ResolveBand(amount, cap);
            if (band == ResourceBand.Unlimited)
            {
                continue;
            }

            totalScore += GetBandWeight(band);
            countedResources++;
        }

        int percent = countedResources <= 0
            ? 0
            : Mathf.RoundToInt((totalScore / countedResources) * 100f);

        SetHarmonyLabel(percent);
    }

    private void SetHarmonyLabel(int percent)
    {
        percent = Mathf.Clamp(percent, 0, 100);
        currentHarmonyPercent = percent;
        harmonyLabel.text = string.Format(harmonyLabelFormat, percent);
        harmonyLabel.style.color = new StyleColor(ResolveHarmonyTextColor(percent));
    }

    private Color ResolveHarmonyTextColor(int percent)
    {
        int blueThreshold = Mathf.Clamp(blueAbovePercent, 0, 100);
        int greenThreshold = Mathf.Clamp(greenAbovePercent, 0, blueThreshold);
        int yellowThreshold = Mathf.Clamp(yellowAbovePercent, 0, greenThreshold);

        if (percent >= blueThreshold)
        {
            if (useHudHarmonyColor && gameHudController != null)
            {
                return gameHudController.ResourceHarmonyColor;
            }

            return harmonyBlueColor;
        }

        if (percent >= greenThreshold)
        {
            return harmonyGreenColor;
        }

        if (percent >= yellowThreshold)
        {
            return harmonyYellowColor;
        }

        return harmonyRedColor;
    }

    private ResourceBand ResolveBand(int amount, int capacity)
    {
        if (capacity <= 0)
        {
            return ResourceBand.Unlimited;
        }

        float fill = Mathf.Clamp01((float)Mathf.Max(0, amount) / Mathf.Max(1, capacity));

        float t1 = Mathf.Clamp01(useHudThresholds && gameHudController != null ? gameHudController.ResourceBand1Threshold : band1Threshold);
        float t2 = Mathf.Max(t1, Mathf.Clamp01(useHudThresholds && gameHudController != null ? gameHudController.ResourceBand2Threshold : band2Threshold));
        float t3 = Mathf.Max(t2, Mathf.Clamp01(useHudThresholds && gameHudController != null ? gameHudController.ResourceBand3Threshold : band3Threshold));
        float t4 = Mathf.Max(t3, Mathf.Clamp01(useHudThresholds && gameHudController != null ? gameHudController.ResourceBand4Threshold : band4Threshold));
        float t5 = Mathf.Max(t4, Mathf.Clamp01(useHudThresholds && gameHudController != null ? gameHudController.ResourceBand5Threshold : band5Threshold));

        if (fill < t1) return ResourceBand.Band1;
        if (fill < t2) return ResourceBand.Band2;
        if (fill < t3) return ResourceBand.Band3;
        if (fill < t4) return ResourceBand.Band4;
        if (fill < t5) return ResourceBand.Band5;
        return ResourceBand.Band6;
    }

    private float GetBandWeight(ResourceBand band)
    {
        switch (band)
        {
            case ResourceBand.Band3:
                return blueBandWeight;
            case ResourceBand.Band2:
            case ResourceBand.Band4:
                return greenBandWeight;
            case ResourceBand.Band1:
            case ResourceBand.Band5:
                return yellowBandWeight;
            case ResourceBand.Band6:
                return redBandWeight;
            default:
                return 0f;
        }
    }
}




