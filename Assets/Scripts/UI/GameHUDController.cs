using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using IdleHra.BuildingSystem;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

[RequireComponent(typeof(UIDocument))]
[RequireComponent(typeof(BuildingDetailsController))]
[RequireComponent(typeof(HarmonyIndicatorController))]
public class GameHUDController : MonoBehaviour
{
    public static GameHUDController Instance { get; private set; }
    [Header("Templates")]
    [SerializeField] private VisualTreeAsset resourceSlotTemplate;
    [SerializeField] private VisualTreeAsset buildCardTemplate;

    [Header("Data Sources")]
    [SerializeField] private List<ResourceType> resourceTypes = new List<ResourceType>();
    [SerializeField] private List<BuildingType> buildingTypes = new List<BuildingType>();
    [SerializeField] private bool autoDiscoverFromResources = true;
    [SerializeField] private bool showOnlyUnlockedResources = true;

    public IReadOnlyList<BuildingType> ConfiguredBuildingTypes => buildingTypes;
    public IReadOnlyList<ResourceType> ConfiguredResourceTypes => resourceTypes;
    public IEnumerable<ResourceType> HarmonyUnlockedResources => unlockedResources;
    public float ResourceBand1Threshold => resourceBand1Threshold;
    public float ResourceBand2Threshold => resourceBand2Threshold;
    public float ResourceBand3Threshold => resourceBand3Threshold;
    public float ResourceBand4Threshold => resourceBand4Threshold;
    public float ResourceBand5Threshold => resourceBand5Threshold;
    public Color ResourceHarmonyColor => resourceHarmonyColor;

    [Header("Icons")]
    [SerializeField] private List<BuildingIconMapping> buildingIcons = new List<BuildingIconMapping>();

    [System.Serializable]
    private struct BuildingIconMapping
    {
        public BuildingType buildingType;
        public Sprite icon;
    }

    private UIDocument document;
    private VisualElement root;
    private VisualElement resourceScrollWrapper;
    private ScrollView resourceScroll;
    private VisualElement resourceRow;
    private VisualElement resourceScrollBar;
    private VisualElement resourceScrollTrack;
    private VisualElement resourceScrollThumb;
    private VisualElement buildCardContainer;
    private VisualElement buildMenu;
    private ScrollView buildScroll;
    private VisualElement buildScrollBar;
    private VisualElement buildScrollTrack;
    private VisualElement buildScrollThumb;
    private bool isDraggingScrollThumb;
    private float dragPointerOffset;
    private int buildScrollThumbPointerId = -1;
    private bool isDraggingResourceScrollThumb;
    private float resourceDragPointerOffset;
    private int resourceScrollThumbPointerId = -1;
    private VisualElement inspectorPanel;
    private VisualElement buildingPanel;
    private Label inspectorBuildingLabel;
    private Label buildingPanelTitle;
    private Label buildingPanelPlaceholder;
    private VisualElement buildingPanelContent;
    private Label productionInfoLabel;
    private Label levelInfoLabel;
    private Label nextLevelTitleLabel;
    private Label nextStatsLabel;
    private VisualElement nextCostRow;
    private Button inspectorCloseButton;
    private Button openBuildMenuButton;
    private Button townHallDetailsButton;
    private Button buildMenuCloseButton;
    private Button buildingPanelCloseButton;
    private Button upgradeButton;
    private Button productionToggleButton;
    private Button worldMapButton;
    private Button talentsButton;
    private Button menuButton;
    private Label harmonyLabel;
    private VisualElement runtimeMenuPanel;
    private Button saveProgressButton;
    private Button exportSaveButton;
    private Button importSaveButton;
    private Button resetProgressButton;
    private Button runtimeMenuCloseButton;
    private VisualElement talentsPanel;
    private VisualElement talentsBackground;
    private Label talentsTitle;
    private Button talentsCloseButton;
    private VisualElement townHallNameSection;
    private VisualElement townHallPanelContent;
    private Label buildMenuEmptyLabel;
    private Label townHallNameTitle;
    private TextField townHallNameField;
    private VisualElement townHallNameCostRow;
    private Button townHallNameButton;
    private Button townHallNextEraButton;
    private VisualElement townHallEraRequirementsSection;
    private Label townHallEraRequirementsTitle;
    private VisualElement townHallEraConsumeRow;
    private VisualElement townHallEraKeepRow;
    private Label townHallEraHarmonyRequirementLabel;
    private BuildingUpgradable selectedUpgradable;
    private BuildingProducer selectedProducer;
    private BuildingType selectedBuildingType;
    private ManualClickUpgrade selectedManualClick;
    private TownHallCity activeTownHall;
    private BuildingDetailsController buildingDetailsController;
    private string selectedBuildingName = "";
    private bool resourceManagerBound;

    private readonly Dictionary<ResourceType, Label> resourceLabels = new Dictionary<ResourceType, Label>();
    private readonly Dictionary<BuildingType, Sprite> buildingIconLookup = new Dictionary<BuildingType, Sprite>();
    private readonly List<BuildButtonBinding> buildButtons = new List<BuildButtonBinding>();
    private readonly List<MarketTradeBinding> marketTradeButtons = new List<MarketTradeBinding>();
    private readonly List<ResearchBinding> researchBindings = new List<ResearchBinding>();
    private readonly List<BlessingBinding> blessingBindings = new List<BlessingBinding>();
    private readonly System.Collections.Generic.HashSet<ResourceType> unlockedResources =
        new System.Collections.Generic.HashSet<ResourceType>();
    private readonly System.Collections.Generic.HashSet<BuildingType> hiddenBuildMenuTypes =
        new System.Collections.Generic.HashSet<BuildingType>();
    private readonly Dictionary<Behaviour, bool> disabledCameraPanByTalents = new Dictionary<Behaviour, bool>();

    [Header("Inspector")]
    [SerializeField] private string townHallDisplayName = "Town Hall";
    [Header("Market")]
    [SerializeField] private ResourceType goldResource;
    [Header("Scenes")]
    [SerializeField] private string worldMapSceneName = "WorldMap";
    [Header("Talents")]
    [SerializeField] private Sprite talentsBackgroundSprite;
    [SerializeField] private string talentsTitleText = "Talents";
    [Header("Resource Capacity Colors")]
    [SerializeField, Range(0f, 1f)] private float resourceBand1Threshold = 0.2f;
    [SerializeField, Range(0f, 1f)] private float resourceBand2Threshold = 0.4f;
    [SerializeField, Range(0f, 1f)] private float resourceBand3Threshold = 0.6f;
    [SerializeField, Range(0f, 1f)] private float resourceBand4Threshold = 0.8f;
    [SerializeField, Range(0f, 1f)] private float resourceBand5Threshold = 0.9f;
    [SerializeField] private Color resourceBand1Color = new Color(1f, 0.95f, 0.35f, 1f);
    [SerializeField] private Color resourceBand2Color = new Color(0.67f, 0.95f, 0.55f, 1f);
    [SerializeField] private Color resourceHarmonyColor = new Color(0.33f, 0.65f, 1f, 1f);
    [SerializeField] private Color resourceBand4Color = new Color(0.67f, 0.95f, 0.55f, 1f);
    [SerializeField] private Color resourceBand5Color = new Color(1f, 0.95f, 0.35f, 1f);
    [SerializeField] private Color resourceBand6Color = new Color(0.88f, 0.24f, 0.18f, 1f);
    [SerializeField] private Color resourceUnlimitedColor = new Color(1f, 0.95f, 0.49f, 1f);
    [Header("Cost Colors")]
    [SerializeField] private Color affordableCostColor = new Color(0.67f, 0.95f, 0.55f, 1f);
    [SerializeField] private Color missingCostColor = new Color(0.88f, 0.24f, 0.18f, 1f);
    [Header("Build Scrollbar")]
    [SerializeField] private Color buildScrollBarBackgroundColor = new Color(0.16f, 0.1f, 0.05f, 0.95f);
    [SerializeField] private Color buildScrollTrackColor = new Color(0.35f, 0.23f, 0.12f, 0.9f);
    [SerializeField] private Color buildScrollThumbColor = new Color(0.69f, 0.46f, 0.25f, 1f);
    [SerializeField] private Color buildScrollThumbBorderColor = new Color(1f, 0.87f, 0.55f, 0.65f);
    [SerializeField] private bool debugBuildScrollbar = false;
    private const int BuildCardItemsPerRow = 2;
    private const float BuildCardRowHeight = 42f;
    private const float BuildCardBaseHeight = 100f;
    private const float BuildCardMinHeight = 140f;
    private const int BuildCardColumns = 3;
    private const float BuildCardColumnGap = 50f;
    private const float BuildCardBottomGap = 25f;
    private struct BuildButtonBinding
    {
        public BuildingType Type;
        public Button Button;
        public VisualElement CostRow;
    }

    private sealed class CostLabelMetadata
    {
        public ResourceType ResourceType;
        public int RequiredAmount;
    }

    private struct MarketTradeBinding
    {
        public BuildingType.MarketTrade Trade;
        public Button BuyButton;
        public Button SellButton;
    }

    private struct ResearchBinding
    {
        public ResearchDefinition Research;
        public VisualElement Tile;
        public Label StatusLabel;
        public Button BuyButton;
    }

    private struct BlessingBinding
    {
        public BlessingDefinition Blessing;
        public VisualElement Tile;
        public Label StatusLabel;
        public Button BuyButton;
    }

    private enum ResearchState
    {
        Locked,
        Available,
        Ready,
        Purchased
    }

    private enum BlessingState
    {
        Locked,
        Available,
        Ready,
        Purchased
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

        if (buildingDetailsController == null)
        {
            buildingDetailsController = GetComponent<BuildingDetailsController>();
        }

        if (buildingDetailsController == null)
        {
            Debug.LogWarning("[HUD] Missing BuildingDetailsController on GameHUD object. Add it in Unity Inspector.", this);
        }

        if (GetComponent<HarmonyIndicatorController>() == null)
        {
            gameObject.AddComponent<HarmonyIndicatorController>();
        }
    }
    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoadedRefresh;
        document = GetComponent<UIDocument>();
        root = document.rootVisualElement;

        TryAutoAssignTemplates();
        TryAutoAssignTalentsBackgroundSprite();
        TryLoadTemplatesFromResources();
        resourceScrollWrapper = root.Q<VisualElement>("ResourceScrollWrapper");
        resourceScroll = root.Q<ScrollView>("ResourceScroll");
        resourceRow = root.Q<VisualElement>("ResourceRow");
        resourceScrollBar = root.Q<VisualElement>("ResourceScrollbar");
        resourceScrollTrack = root.Q<VisualElement>("ResourceScrollbarTrack");
        resourceScrollThumb = root.Q<VisualElement>("ResourceScrollbarThumb");
        buildCardContainer = root.Q<VisualElement>("BuildCardContainer");
        buildMenu = root.Q<VisualElement>("BuildMenu");
        buildScroll = root.Q<ScrollView>("BuildScroll");
        buildScrollBar = root.Q<VisualElement>("BuildScrollbar");
        buildScrollTrack = root.Q<VisualElement>("BuildScrollbarTrack");
        buildScrollThumb = root.Q<VisualElement>("BuildScrollbarThumb");
        inspectorPanel = root.Q<VisualElement>("InspectorPanel");
        buildingPanel = root.Q<VisualElement>("BuildingPanel");
        inspectorBuildingLabel = root.Q<Label>("SelectedBuildingName");
        buildingPanelTitle = root.Q<Label>("BuildingPanelTitle");
        buildingPanelPlaceholder = root.Q<Label>("BuildingPanelPlaceholder");
        buildingPanelContent = root.Q<VisualElement>("BuildingPanelContent");
        productionInfoLabel = root.Q<Label>("ProductionInfo");
        levelInfoLabel = root.Q<Label>("LevelInfo");
        nextLevelTitleLabel = root.Q<Label>("NextLevelTitle");
        nextStatsLabel = root.Q<Label>("NextStatsInfo");
        nextCostRow = root.Q<VisualElement>("NextCostRow");
        inspectorCloseButton = root.Q<Button>("InspectorCloseButton");
        openBuildMenuButton = root.Q<Button>("OpenBuildMenuButton");
        townHallDetailsButton = root.Q<Button>("TownHallDetailsButton");
        buildMenuCloseButton = root.Q<Button>("BuildMenuCloseButton");
        buildingPanelCloseButton = root.Q<Button>("BuildingPanelCloseButton");
        upgradeButton = root.Q<Button>("UpgradeButton");
        productionToggleButton = root.Q<Button>("ProductionToggleButton");
        townHallNameSection = root.Q<VisualElement>("TownHallNameSection");
        townHallPanelContent = root.Q<VisualElement>("TownHallPanelContent");
        buildMenuEmptyLabel = root.Q<Label>("BuildMenuEmptyLabel");
        townHallNameTitle = root.Q<Label>("TownHallNameTitle");
        townHallNameField = root.Q<TextField>("TownHallNameField");
        townHallNameCostRow = root.Q<VisualElement>("TownHallNameCostRow");
        townHallNameButton = root.Q<Button>("TownHallNameButton");
        townHallNextEraButton = root.Q<Button>("TownHallNextEraButton");
        townHallEraRequirementsSection = root.Q<VisualElement>("TownHallEraRequirementsSection");
        townHallEraRequirementsTitle = root.Q<Label>("TownHallEraRequirementsTitle");
        townHallEraConsumeRow = root.Q<VisualElement>("TownHallEraConsumeRow");
        townHallEraKeepRow = root.Q<VisualElement>("TownHallEraKeepRow");
        townHallEraHarmonyRequirementLabel = root.Q<Label>("TownHallEraHarmonyRequirementLabel");
        worldMapButton = root.Q<Button>("WorldMapButton");
        talentsButton = root.Q<Button>("TalentsButton");
        menuButton = root.Q<Button>("MenuButton");
        harmonyLabel = root.Q<Label>("HarmonyLabel");
        runtimeMenuPanel = root.Q<VisualElement>("RuntimeMenuPanel");
        saveProgressButton = root.Q<Button>("RuntimeMenuSaveButton");
        exportSaveButton = root.Q<Button>("RuntimeMenuExportButton");
        importSaveButton = root.Q<Button>("RuntimeMenuImportButton");
        resetProgressButton = root.Q<Button>("RuntimeMenuResetProgressButton");
        runtimeMenuCloseButton = root.Q<Button>("RuntimeMenuCloseButton");
        talentsPanel = root.Q<VisualElement>("TalentsPanel");
        talentsBackground = root.Q<VisualElement>("TalentsBackground");
        talentsTitle = root.Q<Label>("TalentsTitle");
        talentsCloseButton = root.Q<Button>("TalentsCloseButton");

        BuildIconLookups();
        RebuildHiddenBuildMenuTypesFromScene();
        PrimeUnlockedResources();
        BuildResourceBar();
        RefreshHarmonyIndicator();
        BuildBuildMenu();
        HideAllPanels();

        TryBindResourceManager();

        ApplyBuildScrollTheme();
        if (buildScroll != null)
        {
            buildScroll.schedule.Execute(ApplyBuildScrollTheme).StartingIn(0);
        }

        SetupCustomBuildScrollbar();
        SetupCustomResourceScrollbar();

        if (inspectorCloseButton != null)
        {
            inspectorCloseButton.clicked += HideAllPanels;
        }

        if (openBuildMenuButton != null)
        {
            openBuildMenuButton.clicked += OnOpenBuildMenuClicked;
        }

        if (townHallDetailsButton != null)
        {
            townHallDetailsButton.clicked += OnTownHallDetailsClicked;
        }

        if (buildMenuCloseButton != null)
        {
            buildMenuCloseButton.clicked += HideBuildMenu;
        }

        if (buildingPanelCloseButton != null)
        {
            buildingPanelCloseButton.clicked += HideBuildingPanel;
        }

        if (upgradeButton != null)
        {
            upgradeButton.clicked += OnUpgradeClicked;
        }

        if (productionToggleButton != null)
        {
            productionToggleButton.clicked += OnProductionToggleClicked;
        }

        if (townHallNameButton != null)
        {
            townHallNameButton.clicked += OnTownHallNameClicked;
        }

        if (townHallNextEraButton != null)
        {
            townHallNextEraButton.clicked += OnTownHallNextEraClicked;
        }

        if (worldMapButton != null)
        {
            worldMapButton.clicked += OnWorldMapButtonClicked;
        }

        if (talentsButton != null)
        {
            talentsButton.clicked += OnTalentsButtonClicked;
        }


        if (menuButton != null)
        {
            menuButton.clicked += OnMenuButtonClicked;
        }
        if (saveProgressButton != null)
        {
            saveProgressButton.clicked += OnSaveProgressButtonClicked;
        }
        if (exportSaveButton != null)
        {
            exportSaveButton.clicked += OnExportSaveButtonClicked;
        }
        if (importSaveButton != null)
        {
            importSaveButton.clicked += OnImportSaveButtonClicked;
        }
        if (resetProgressButton != null)
        {
            resetProgressButton.clicked += OnResetProgressButtonClicked;
        }

        if (runtimeMenuCloseButton != null)
        {
            runtimeMenuCloseButton.clicked += HideRuntimeMenu;

        }


        if (talentsCloseButton != null)
        {
            talentsCloseButton.clicked += HideTalentsPanel;
        }

        var researchManager = ResearchManager.Instance != null
            ? ResearchManager.Instance
            : Object.FindAnyObjectByType<ResearchManager>();
        if (researchManager != null)
        {
            researchManager.ResearchChanged += RefreshResearchBindings;
        }

        var blessingManager = BlessingManager.Instance != null
            ? BlessingManager.Instance
            : Object.FindAnyObjectByType<BlessingManager>();
        if (blessingManager != null)
        {
            blessingManager.BlessingsChanged += RefreshBlessingBindings;
        }
    }

    private void OnDisable()
    {
        ReleaseScrollbarPointerCaptures();
        SceneManager.sceneLoaded -= HandleSceneLoadedRefresh;
        var manager = ResourceManager.Instance;
        if (manager != null)
        {
            manager.ResourceChanged -= HandleResourceChanged;
        }
        resourceManagerBound = false;

        if (inspectorCloseButton != null)
        {
            inspectorCloseButton.clicked -= HideAllPanels;
        }

        if (openBuildMenuButton != null)
        {
            openBuildMenuButton.clicked -= OnOpenBuildMenuClicked;
        }

        if (townHallDetailsButton != null)
        {
            townHallDetailsButton.clicked -= OnTownHallDetailsClicked;
        }

        if (buildMenuCloseButton != null)
        {
            buildMenuCloseButton.clicked -= HideBuildMenu;
        }

        if (buildingPanelCloseButton != null)
        {
            buildingPanelCloseButton.clicked -= HideBuildingPanel;
        }

        if (upgradeButton != null)
        {
            upgradeButton.clicked -= OnUpgradeClicked;
        }

        if (productionToggleButton != null)
        {
            productionToggleButton.clicked -= OnProductionToggleClicked;
        }

        if (townHallNameButton != null)
        {
            townHallNameButton.clicked -= OnTownHallNameClicked;
        }

        if (townHallNextEraButton != null)
        {
            townHallNextEraButton.clicked -= OnTownHallNextEraClicked;
        }

        if (worldMapButton != null)
        {
            worldMapButton.clicked -= OnWorldMapButtonClicked;
        }

        if (talentsButton != null)
        {
            talentsButton.clicked -= OnTalentsButtonClicked;
        }


        if (menuButton != null)
        {
            menuButton.clicked -= OnMenuButtonClicked;
        }
        if (saveProgressButton != null)
        {
            saveProgressButton.clicked -= OnSaveProgressButtonClicked;
        }
        if (exportSaveButton != null)
        {
            exportSaveButton.clicked -= OnExportSaveButtonClicked;
        }
        if (importSaveButton != null)
        {
            importSaveButton.clicked -= OnImportSaveButtonClicked;
        }
        if (resetProgressButton != null)
        {
            resetProgressButton.clicked -= OnResetProgressButtonClicked;
        }

        if (runtimeMenuCloseButton != null)
        {
            runtimeMenuCloseButton.clicked -= HideRuntimeMenu;
        }

        if (talentsCloseButton != null)
        {
            talentsCloseButton.clicked -= HideTalentsPanel;
        }

        RestoreCameraPanAfterTalents();

        var researchManager = ResearchManager.Instance;
        if (researchManager != null)
        {
            researchManager.ResearchChanged -= RefreshResearchBindings;
        }

        var blessingManager = BlessingManager.Instance;
        if (blessingManager != null)
        {
            blessingManager.BlessingsChanged -= RefreshBlessingBindings;
        }

        ResetDebugFlagsForEditorStop();
    }
    private void HandleSceneLoadedRefresh(Scene scene, LoadSceneMode mode)
    {
        resourceManagerBound = false;

        // Rebuild top bar labels after scene switch so UI always reflects current resources.
        RebuildHiddenBuildMenuTypesFromScene();
        PrimeUnlockedResources();
        BuildResourceBar();
        RefreshHarmonyIndicator();
        BuildBuildMenu();
        TryBindResourceManager();
        RefreshBuildButtons();
        RefreshMarketButtons();
        RefreshResearchBindings();
        RefreshBlessingBindings();
        RefreshTownHallNameUI();
        HideTalentsPanel();
    }
    
    public bool IsPointerOverResourceBar(Vector2 screenPosition)
    {
        if (resourceScrollWrapper == null || resourceScrollWrapper.panel == null)
        {
            return false;
        }

        var panel = resourceScrollWrapper.panel;
        Vector2 panelPositionA = RuntimePanelUtils.ScreenToPanel(panel, screenPosition);
        if (resourceScrollWrapper.worldBound.Contains(panelPositionA))
        {
            return true;
        }

        // Some platforms/input paths report wheel coordinates with opposite Y origin.
        Vector2 flippedScreen = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        Vector2 panelPositionB = RuntimePanelUtils.ScreenToPanel(panel, flippedScreen);
        if (resourceScrollWrapper.worldBound.Contains(panelPositionB))
        {
            return true;
        }

        if (resourceScroll != null)
        {
            if (resourceScroll.worldBound.Contains(panelPositionA) || resourceScroll.worldBound.Contains(panelPositionB))
            {
                return true;
            }
        }

        return false;
    }

    private void Update()
    {
        if (!resourceManagerBound)
        {
            TryBindResourceManager();
        }
    }

    private void TryBindResourceManager()
    {
        var manager = ResourceManager.Instance;
        if (manager == null)
        {
            manager = Object.FindAnyObjectByType<ResourceManager>();
        }

        if (manager == null)
        {
            return;
        }

        manager.ResourceChanged -= HandleResourceChanged;
        manager.ResourceChanged += HandleResourceChanged;
        resourceManagerBound = true;

        PrimeUnlockedResources();
        BuildResourceBar();
        RefreshHarmonyIndicator();
        RefreshBuildButtons();
        UpdateUpgradeUI();
        RefreshMarketButtons();
        RefreshResearchBindings();
        RefreshBlessingBindings();
        RefreshTownHallNameUI();
    }

    private void BuildIconLookups()
    {
        buildingIconLookup.Clear();
        for (int i = 0; i < buildingIcons.Count; i++)
        {
            var entry = buildingIcons[i];
            if (entry.buildingType == null || entry.icon == null)
            {
                continue;
            }

            if (!buildingIconLookup.ContainsKey(entry.buildingType))
            {
                buildingIconLookup.Add(entry.buildingType, entry.icon);
            }
        }
    }

    private void ApplyBuildScrollTheme()
    {
        if (buildScroll == null)
        {
            return;
        }

        buildScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;

        var scroller = buildScroll.verticalScroller;
        if (scroller == null)
        {
            return;
        }

        scroller.style.backgroundColor = buildScrollBarBackgroundColor;
        scroller.style.backgroundImage = new StyleBackground((Texture2D)null);
        scroller.style.unityBackgroundImageTintColor = new Color(0f, 0f, 0f, 0f);

        var track = scroller.Q<VisualElement>(className: "unity-base-slider__track");
        if (track != null)
        {
            track.style.backgroundColor = buildScrollTrackColor;
            track.style.backgroundImage = new StyleBackground((Texture2D)null);
            track.style.unityBackgroundImageTintColor = new Color(0f, 0f, 0f, 0f);
        }
        var dragContainer = scroller.Q<VisualElement>(className: "unity-base-slider__drag-container");
        if (dragContainer != null)
        {
            dragContainer.style.backgroundColor = buildScrollTrackColor;
            dragContainer.style.backgroundImage = new StyleBackground((Texture2D)null);
            dragContainer.style.unityBackgroundImageTintColor = new Color(0f, 0f, 0f, 0f);
        }

        var dragger = scroller.Q<VisualElement>(className: "unity-base-slider__dragger");
        if (dragger != null)
        {
            dragger.style.backgroundColor = buildScrollThumbColor;
            dragger.style.backgroundImage = new StyleBackground((Texture2D)null);
            dragger.style.unityBackgroundImageTintColor = new Color(0f, 0f, 0f, 0f);
            dragger.style.borderLeftWidth = 1;
            dragger.style.borderRightWidth = 1;
            dragger.style.borderTopWidth = 1;
            dragger.style.borderBottomWidth = 1;
            dragger.style.borderLeftColor = buildScrollThumbBorderColor;
            dragger.style.borderRightColor = buildScrollThumbBorderColor;
            dragger.style.borderTopColor = buildScrollThumbBorderColor;
            dragger.style.borderBottomColor = buildScrollThumbBorderColor;
            dragger.style.borderBottomLeftRadius = 6;
            dragger.style.borderBottomRightRadius = 6;
            dragger.style.borderTopLeftRadius = 6;
            dragger.style.borderTopRightRadius = 6;
        }

        scroller.Query<Button>().ForEach(button =>
        {
            button.style.backgroundColor = buildScrollBarBackgroundColor;
            button.style.unityBackgroundImageTintColor = buildScrollBarBackgroundColor;
            button.style.backgroundImage = new StyleBackground((Texture2D)null);
            button.style.borderLeftColor = buildScrollThumbBorderColor;
            button.style.borderRightColor = buildScrollThumbBorderColor;
            button.style.borderTopColor = buildScrollThumbBorderColor;
            button.style.borderBottomColor = buildScrollThumbBorderColor;
        });

        scroller.Query<VisualElement>().ForEach(ve =>
        {
            if (ve.name == "unity-up-button" || ve.name == "unity-down-button")
            {
                ve.style.backgroundColor = buildScrollBarBackgroundColor;
                ve.style.unityBackgroundImageTintColor = buildScrollBarBackgroundColor;
                ve.style.backgroundImage = new StyleBackground((Texture2D)null);
            }
        });

        if (debugBuildScrollbar)
        {
            Debug.Log($"BuildScroll theme applied. Track={(track != null)} DragContainer={(dragContainer != null)} Dragger={(dragger != null)}");
        }
    }

    private void SetupCustomBuildScrollbar()
    {
        if (buildScroll == null || buildScrollTrack == null || buildScrollThumb == null)
        {
            return;
        }

        buildScroll.RegisterCallback<GeometryChangedEvent>(_ => UpdateCustomScrollbar());
        buildScroll.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => UpdateCustomScrollbar());

        buildScroll.RegisterCallback<WheelEvent>(_ => UpdateCustomScrollbar());

        buildScrollThumb.RegisterCallback<PointerDownEvent>(OnScrollbarThumbDown);
        buildScrollThumb.RegisterCallback<PointerMoveEvent>(OnScrollbarThumbMove);
        buildScrollThumb.RegisterCallback<PointerUpEvent>(OnScrollbarThumbUp);
        buildScrollThumb.RegisterCallback<PointerCaptureOutEvent>(OnScrollbarThumbCaptureOut);

        buildScrollTrack.RegisterCallback<PointerDownEvent>(OnScrollbarTrackDown);
    }

    private void UpdateCustomScrollbar()
    {
        if (buildScroll == null || buildScrollTrack == null || buildScrollThumb == null)
        {
            return;
        }

        float viewportHeight = buildScroll.contentViewport.layout.height;
        float contentHeight = buildScroll.contentContainer.layout.height;
        float trackHeight = buildScrollTrack.layout.height;

        if (viewportHeight <= 0f || contentHeight <= 0f || trackHeight <= 0f)
        {
            return;
        }

        float maxScroll = Mathf.Max(0f, contentHeight - viewportHeight);
        if (maxScroll <= 0f)
        {
            buildScrollBar?.SetEnabled(false);
            buildScrollThumb.style.display = DisplayStyle.None;
            return;
        }

        buildScrollBar?.SetEnabled(true);
        buildScrollThumb.style.display = DisplayStyle.Flex;

        float visibleRatio = Mathf.Clamp01(viewportHeight / contentHeight);
        float thumbHeight = Mathf.Max(30f, trackHeight * visibleRatio);
        float maxThumbTop = Mathf.Max(0f, trackHeight - thumbHeight);

        float scrollY = Mathf.Clamp(buildScroll.scrollOffset.y, 0f, maxScroll);
        float thumbTop = maxThumbTop * (scrollY / maxScroll);

        buildScrollThumb.style.height = thumbHeight;
        buildScrollThumb.style.top = thumbTop;
    }

    private void OnScrollbarThumbDown(PointerDownEvent evt)
    {
        isDraggingScrollThumb = true;
        float pointerTrackY = buildScrollTrack != null
            ? buildScrollTrack.WorldToLocal(evt.position).y
            : evt.localPosition.y;
        float currentThumbTop = buildScrollThumb != null ? buildScrollThumb.layout.y : 0f;
        dragPointerOffset = pointerTrackY - currentThumbTop;
        buildScrollThumbPointerId = evt.pointerId;
        buildScrollThumb.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnScrollbarThumbMove(PointerMoveEvent evt)
    {
        if (!isDraggingScrollThumb || buildScroll == null || buildScrollTrack == null || buildScrollThumb == null)
        {
            return;
        }

        float trackHeight = buildScrollTrack.layout.height;
        float thumbHeight = buildScrollThumb.layout.height;
        float maxThumbTop = Mathf.Max(0f, trackHeight - thumbHeight);
        float pointerTrackY = buildScrollTrack.WorldToLocal(evt.position).y;
        float desiredTop = Mathf.Clamp(pointerTrackY - dragPointerOffset, 0f, maxThumbTop);

        float viewportHeight = buildScroll.contentViewport.layout.height;
        float contentHeight = buildScroll.contentContainer.layout.height;
        float maxScroll = Mathf.Max(0f, contentHeight - viewportHeight);

        float scrollY = (maxScroll <= 0f || maxThumbTop <= 0f) ? 0f : (desiredTop / maxThumbTop) * maxScroll;
        buildScroll.scrollOffset = new Vector2(0f, scrollY);
        UpdateCustomScrollbar();
        evt.StopPropagation();
    }

    private void OnScrollbarTrackDown(PointerDownEvent evt)
    {
        if (buildScroll == null || buildScrollTrack == null || buildScrollThumb == null)
        {
            return;
        }

        float trackHeight = buildScrollTrack.layout.height;
        float thumbHeight = buildScrollThumb.layout.height;
        float maxThumbTop = Mathf.Max(0f, trackHeight - thumbHeight);

        float desiredTop = Mathf.Clamp(evt.localPosition.y - thumbHeight * 0.5f, 0f, maxThumbTop);

        float viewportHeight = buildScroll.contentViewport.layout.height;
        float contentHeight = buildScroll.contentContainer.layout.height;
        float maxScroll = Mathf.Max(0f, contentHeight - viewportHeight);

        float scrollY = (maxScroll <= 0f || maxThumbTop <= 0f) ? 0f : (desiredTop / maxThumbTop) * maxScroll;
        buildScroll.scrollOffset = new Vector2(0f, scrollY);
        UpdateCustomScrollbar();
        evt.StopPropagation();
    }

    private void OnScrollbarThumbUp(PointerUpEvent evt)
    {
        isDraggingScrollThumb = false;
        if (buildScrollThumb != null && buildScrollThumb.HasPointerCapture(evt.pointerId))
        {
            buildScrollThumb.ReleasePointer(evt.pointerId);
        }

        if (buildScrollThumbPointerId == evt.pointerId)
        {
            buildScrollThumbPointerId = -1;
        }

        evt.StopPropagation();
    }

    private void OnScrollbarThumbCaptureOut(PointerCaptureOutEvent _)
    {
        isDraggingScrollThumb = false;
        buildScrollThumbPointerId = -1;
    }

    private void SetupCustomResourceScrollbar()
    {
        if (resourceScroll == null || resourceScrollTrack == null || resourceScrollThumb == null)
        {
            return;
        }

        resourceScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        resourceScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;

        resourceScroll.RegisterCallback<GeometryChangedEvent>(_ => UpdateCustomResourceScrollbar());
        resourceScroll.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => UpdateCustomResourceScrollbar());
        resourceScroll.RegisterCallback<WheelEvent>(OnResourceScrollWheel);

        resourceScrollThumb.RegisterCallback<PointerDownEvent>(OnResourceScrollbarThumbDown);
        resourceScrollThumb.RegisterCallback<PointerMoveEvent>(OnResourceScrollbarThumbMove);
        resourceScrollThumb.RegisterCallback<PointerUpEvent>(OnResourceScrollbarThumbUp);
        resourceScrollThumb.RegisterCallback<PointerCaptureOutEvent>(OnResourceScrollbarThumbCaptureOut);

        // Track is non-interactive to avoid blocking top-bar/world clicks.

        resourceScroll.schedule.Execute(UpdateCustomResourceScrollbar).StartingIn(1);
    }

    private void UpdateCustomResourceScrollbar()
    {
        if (resourceScroll == null || resourceScrollTrack == null || resourceScrollThumb == null)
        {
            return;
        }

        resourceScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        resourceScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;

        float viewportWidth = resourceScroll.contentViewport.layout.width;
        float contentWidth = resourceScroll.contentContainer.layout.width;
        float trackWidth = resourceScrollTrack.layout.width;

        if (viewportWidth <= 0f || contentWidth <= 0f || trackWidth <= 0f)
        {
            return;
        }

        float maxScroll = Mathf.Max(0f, contentWidth - viewportWidth);
        if (maxScroll <= 0f)
        {
            resourceScrollBar?.SetEnabled(false);
            resourceScrollThumb.style.display = DisplayStyle.None;
            return;
        }

        resourceScrollBar?.SetEnabled(true);
        resourceScrollThumb.style.display = DisplayStyle.Flex;

        float visibleRatio = Mathf.Clamp01(viewportWidth / contentWidth);
        float thumbWidth = Mathf.Max(36f, trackWidth * visibleRatio);
        float maxThumbLeft = Mathf.Max(0f, trackWidth - thumbWidth);

        float scrollX = Mathf.Clamp(resourceScroll.scrollOffset.x, 0f, maxScroll);
        float thumbLeft = maxThumbLeft * (scrollX / maxScroll);

        resourceScrollThumb.style.width = thumbWidth;
        resourceScrollThumb.style.left = thumbLeft;
    }

    private void OnResourceScrollbarThumbDown(PointerDownEvent evt)
    {
        isDraggingResourceScrollThumb = true;
        float pointerTrackX = resourceScrollTrack != null
            ? resourceScrollTrack.WorldToLocal(evt.position).x
            : evt.localPosition.x;
        float currentThumbLeft = resourceScrollThumb != null ? resourceScrollThumb.layout.x : 0f;
        resourceDragPointerOffset = pointerTrackX - currentThumbLeft;
        resourceScrollThumbPointerId = evt.pointerId;
        resourceScrollThumb.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnResourceScrollbarThumbMove(PointerMoveEvent evt)
    {
        if (!isDraggingResourceScrollThumb || resourceScroll == null || resourceScrollTrack == null || resourceScrollThumb == null)
        {
            return;
        }

        float trackWidth = resourceScrollTrack.layout.width;
        float thumbWidth = resourceScrollThumb.layout.width;
        float maxThumbLeft = Mathf.Max(0f, trackWidth - thumbWidth);
        float pointerTrackX = resourceScrollTrack.WorldToLocal(evt.position).x;
        float desiredLeft = Mathf.Clamp(pointerTrackX - resourceDragPointerOffset, 0f, maxThumbLeft);

        float viewportWidth = resourceScroll.contentViewport.layout.width;
        float contentWidth = resourceScroll.contentContainer.layout.width;
        float maxScroll = Mathf.Max(0f, contentWidth - viewportWidth);

        float scrollX = (maxScroll <= 0f || maxThumbLeft <= 0f) ? 0f : (desiredLeft / maxThumbLeft) * maxScroll;
        resourceScroll.scrollOffset = new Vector2(scrollX, resourceScroll.scrollOffset.y);
        UpdateCustomResourceScrollbar();
        evt.StopPropagation();
    }

    private void OnResourceScrollbarTrackDown(PointerDownEvent evt)
    {
        if (resourceScroll == null || resourceScrollTrack == null || resourceScrollThumb == null)
        {
            return;
        }

        resourceScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        resourceScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;

        float trackWidth = resourceScrollTrack.layout.width;
        float thumbWidth = resourceScrollThumb.layout.width;
        float maxThumbLeft = Mathf.Max(0f, trackWidth - thumbWidth);

        float desiredLeft = Mathf.Clamp(evt.localPosition.x - thumbWidth * 0.5f, 0f, maxThumbLeft);

        float viewportWidth = resourceScroll.contentViewport.layout.width;
        float contentWidth = resourceScroll.contentContainer.layout.width;
        float maxScroll = Mathf.Max(0f, contentWidth - viewportWidth);

        float scrollX = (maxScroll <= 0f || maxThumbLeft <= 0f) ? 0f : (desiredLeft / maxThumbLeft) * maxScroll;
        resourceScroll.scrollOffset = new Vector2(scrollX, resourceScroll.scrollOffset.y);
        UpdateCustomResourceScrollbar();
        evt.StopPropagation();
    }

    
    private void OnResourceScrollWheel(WheelEvent evt)
    {
        if (resourceScroll == null)
        {
            return;
        }

        float viewportWidth = resourceScroll.contentViewport.layout.width;
        float contentWidth = resourceScroll.contentContainer.layout.width;
        float maxScroll = Mathf.Max(0f, contentWidth - viewportWidth);
        if (maxScroll <= 0f)
        {
            return;
        }

        float step = Mathf.Abs(evt.delta.y);
        if (step <= 0.01f)
        {
            step = Mathf.Abs(evt.delta.x);
        }
        if (step <= 0.01f)
        {
            step = 24f;
        }

        float direction = evt.delta.y > 0f ? 1f : -1f;
        float newX = Mathf.Clamp(resourceScroll.scrollOffset.x + direction * step, 0f, maxScroll);
        resourceScroll.scrollOffset = new Vector2(newX, resourceScroll.scrollOffset.y);
        UpdateCustomResourceScrollbar();
        evt.StopPropagation();
    }

    private void OnResourceScrollbarThumbUp(PointerUpEvent evt)
    {
        isDraggingResourceScrollThumb = false;
        if (resourceScrollThumb != null && resourceScrollThumb.HasPointerCapture(evt.pointerId))
        {
            resourceScrollThumb.ReleasePointer(evt.pointerId);
        }

        if (resourceScrollThumbPointerId == evt.pointerId)
        {
            resourceScrollThumbPointerId = -1;
        }

        evt.StopPropagation();
    }

    private void OnResourceScrollbarThumbCaptureOut(PointerCaptureOutEvent _)
    {
        isDraggingResourceScrollThumb = false;
        resourceScrollThumbPointerId = -1;
    }

    private void ReleaseScrollbarPointerCaptures()
    {
        isDraggingScrollThumb = false;
        if (buildScrollThumb != null && buildScrollThumbPointerId >= 0 && buildScrollThumb.HasPointerCapture(buildScrollThumbPointerId))
        {
            buildScrollThumb.ReleasePointer(buildScrollThumbPointerId);
        }
        buildScrollThumbPointerId = -1;

        isDraggingResourceScrollThumb = false;
        if (resourceScrollThumb != null && resourceScrollThumbPointerId >= 0 && resourceScrollThumb.HasPointerCapture(resourceScrollThumbPointerId))
        {
            resourceScrollThumb.ReleasePointer(resourceScrollThumbPointerId);
        }
        resourceScrollThumbPointerId = -1;
    }

    private void BuildResourceBar()
    {
        if (resourceRow == null || resourceSlotTemplate == null)
        {
            Debug.LogWarning("GameHUDController: Resource bar setup missing.");
            return;
        }

        resourceRow.Clear();
        resourceLabels.Clear();

        var manager = ResourceManager.Instance;
        var types = GetResourceTypes();
        for (int i = 0; i < types.Count; i++)
        {
            var type = types[i];
            if (type == null)
            {
                continue;
            }

            if (!type.ShowInHUD)
            {
                continue;
            }

            if (showOnlyUnlockedResources && !unlockedResources.Contains(type))
            {
                continue;
            }

            var slot = resourceSlotTemplate.CloneTree();
            var icon = slot.Q<Image>("ResourceIcon");
            var nameLabel = slot.Q<Label>("ResourceName");
            var valueLabel = slot.Q<Label>("ResourceValue");

            if (icon != null)
            {
                var sprite = ResolveResourceIcon(type);
                if (sprite != null)
                {
                    icon.style.backgroundImage = new StyleBackground(sprite);
                }
            }

            if (nameLabel != null)
            {
                nameLabel.text = type.DisplayName;
            }

            int value = manager != null ? manager.Get(type) : 0;
            if (valueLabel != null)
            {
                UpdateResourceLabelVisual(manager, type, valueLabel, value);
                resourceLabels[type] = valueLabel;
            }

            resourceRow.Add(slot);
        }

        if (resourceScroll != null)
        {
            resourceScroll.schedule.Execute(UpdateCustomResourceScrollbar).StartingIn(1);
        }
    }

    private void BuildBuildMenu()
    {
        if (buildCardContainer == null || buildCardTemplate == null)
        {
            Debug.LogWarning("GameHUDController: Build menu setup missing.");
            return;
        }

        buildCardContainer.Clear();
        buildButtons.Clear();
        var types = GetBuildingTypes();
        int visibleCardIndex = 0;
        for (int i = 0; i < types.Count; i++)
        {
            var type = types[i];
            if (type == null)
            {
                continue;
            }

            var card = buildCardTemplate.CloneTree();
            var nameLabel = card.Q<Label>("BuildingName");
            var costRow = card.Q<VisualElement>("CostRow");
            var buildButton = card.Q<Button>("BuildButton");

            if (nameLabel != null)
            {
                nameLabel.text = type.DisplayName;
            }

            int costCount = SetupCostRow(costRow, type);
            ApplyBuildCardDynamicSize(card, costRow, costCount);

            if (buildButton != null)
            {
                buildButton.clicked += () => HandleBuildClicked(type);
                buildButtons.Add(new BuildButtonBinding
                {
                    Type = type,
                    Button = buildButton,
                    CostRow = costRow
                });
            }

            ApplyBuildCardGridSpacing(card, visibleCardIndex);
            visibleCardIndex++;
            buildCardContainer.Add(card);
        }

        if (buildMenuEmptyLabel != null)
        {
            buildMenuEmptyLabel.style.display = buildButtons.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        RefreshBuildButtons();
        ApplyBuildScrollTheme();
        if (buildScroll != null)
        {
            buildScroll.schedule.Execute(UpdateCustomScrollbar).StartingIn(1);
        }
    }

    public void RefreshBuildMenu()
    {
        BuildBuildMenu();
    }

    public void HideFromBuildMenu(BuildingType type)
    {
        if (type == null)
        {
            return;
        }

        if (hiddenBuildMenuTypes.Add(type))
        {
            RefreshBuildMenu();
        }
    }

    private void RebuildHiddenBuildMenuTypesFromScene()
    {
        hiddenBuildMenuTypes.Clear();

        var inspectorTargets = Object.FindObjectsByType<BuildingInspectorTarget>(FindObjectsSortMode.None);
        for (int i = 0; i < inspectorTargets.Length; i++)
        {
            var target = inspectorTargets[i];
            if (target == null || target.BuildingType == null)
            {
                continue;
            }

            hiddenBuildMenuTypes.Add(target.BuildingType);
        }

        var upgradables = Object.FindObjectsByType<BuildingUpgradable>(FindObjectsSortMode.None);
        for (int i = 0; i < upgradables.Length; i++)
        {
            var upgradable = upgradables[i];
            if (upgradable == null || upgradable.BuildingType == null)
            {
                continue;
            }

            hiddenBuildMenuTypes.Add(upgradable.BuildingType);
        }
    }

    private int SetupCostRow(VisualElement costRow, BuildingType type)
    {
        if (costRow == null || type == null)
        {
            return 0;
        }

        costRow.Clear();
        var manager = ResourceManager.Instance;
        if (type.DebugFreeBuild)
        {
            var freeLabel = new Label("Free");
            freeLabel.AddToClassList("cost-amount");
            costRow.Add(freeLabel);
            return 1;
        }

        var costs = type.Costs;
        if (costs == null || costs.Count == 0)
        {
            var freeLabel = new Label("Free");
            freeLabel.AddToClassList("cost-amount");
            costRow.Add(freeLabel);
            return 1;
        }

        int visibleCostCount = 0;
        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost.resourceType == null || cost.amount <= 0)
            {
                continue;
            }

            var item = new VisualElement();
            item.AddToClassList("cost-item");

            var icon = new VisualElement();
            icon.AddToClassList("cost-icon");
            Sprite iconSprite = ResolveResourceIcon(cost.resourceType);
            if (iconSprite != null)
            {
                icon.style.backgroundImage = new StyleBackground(iconSprite);
            }
            item.Add(icon);

            int requiredAmount = GetModifiedCost(cost.amount);
            var amount = new Label(requiredAmount.ToString());
            amount.AddToClassList("cost-amount");
            amount.userData = new CostLabelMetadata
            {
                ResourceType = cost.resourceType,
                RequiredAmount = requiredAmount
            };
            ApplyCostLabelColor(amount, manager, cost.resourceType, requiredAmount);
            item.Add(amount);

            costRow.Add(item);
            visibleCostCount++;
        }

        if (visibleCostCount == 0)
        {
            var freeLabel = new Label("Free");
            freeLabel.AddToClassList("cost-amount");
            costRow.Add(freeLabel);
            return 1;
        }

        return visibleCostCount;
    }

    private void ApplyBuildCardDynamicSize(VisualElement card, VisualElement costRow, int costCount)
    {
        if (card == null || costRow == null)
        {
            return;
        }

        int normalizedCount = Mathf.Max(1, costCount);
        int rows = Mathf.CeilToInt(normalizedCount / (float)BuildCardItemsPerRow);
        float costHeight = rows * BuildCardRowHeight;
        float cardHeight = Mathf.Max(BuildCardMinHeight, BuildCardBaseHeight + costHeight);

        costRow.style.minHeight = costHeight;

        var cardRoot = card.Q<VisualElement>(className: "build-card");
        if (cardRoot == null)
        {
            cardRoot = card;
        }

        cardRoot.style.height = cardHeight;
    }
    private void ApplyBuildCardGridSpacing(VisualElement card, int visibleIndex)
    {
        if (card == null)
        {
            return;
        }

        var cardRoot = card.Q<VisualElement>(className: "build-card");
        if (cardRoot == null)
        {
            cardRoot = card;
        }

        int column = Mathf.Abs(visibleIndex) % BuildCardColumns;
        card.style.marginLeft = 0f;
        card.style.marginRight = column < (BuildCardColumns - 1) ? BuildCardColumnGap : 0f;
        card.style.marginBottom = BuildCardBottomGap;
        cardRoot.style.marginLeft = 0f;
        cardRoot.style.marginRight = 0f;
        cardRoot.style.marginBottom = 0f;
    }

    private void HandleBuildClicked(BuildingType type)
    {
        UpdateInspector(type);

        var manager = ResourceManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("GameHUDController: ResourceManager not found.");
            return;
        }

        if (!CanAfford(manager, type))
        {
            Debug.Log("GameHUDController: Not enough resources.");
            return;
        }

        var grid = GridBuildingSystem.Instance != null
            ? GridBuildingSystem.Instance
            : Object.FindAnyObjectByType<GridBuildingSystem>();
        if (grid == null)
        {
            Debug.LogWarning("GameHUDController: GridBuildingSystem not found.");
            return;
        }

        grid.InitializeWithBuildingType(type);
    }

    private void UpdateInspector(BuildingType type)
    {
        if (inspectorBuildingLabel == null)
        {
            return;
        }

        selectedBuildingName = type != null ? type.DisplayName : "(none)";
        inspectorBuildingLabel.text = selectedBuildingName;
    }
    public void ShowInspectorFor(BuildingType type)
    {
        activeTownHall = null;
        UpdateInspector(type);
        UpdateBuildingPanel(type);

        selectedBuildingType = type;
        selectedUpgradable = null;
        selectedProducer = null;
        selectedManualClick = null;

        SetProductionInfo(null, false);
        UpdateProductionToggleButton(false);
        HideTownHallPanel();
        HideBuildMenu();
        HideRuntimeMenu();
        ShowInspectorOnly(type != null ? type.DisplayName : "(none)", false);
        UpdateUpgradeUI();
    }

    public void ShowInspectorFor(BuildingType type, BuildingProducer producer, BuildingUpgradable upgradable)
    {
        activeTownHall = null;
        UpdateInspector(type);
        UpdateBuildingPanel(type);

        selectedBuildingType = type;
        selectedUpgradable = upgradable;
        selectedProducer = producer;
        selectedManualClick = upgradable != null ? upgradable.GetComponentInChildren<ManualClickUpgrade>() : null;

        SetProductionInfo(producer, false);
        UpdateProductionToggleButton(false);
        HideTownHallPanel();
        HideBuildMenu();
        ShowInspectorOnly(type != null ? type.DisplayName : "(none)", false);
        UpdateUpgradeUI();
    }
    private void OnUpgradeClicked()
    {
        if (selectedUpgradable == null)
        {
            return;
        }

        var manager = ResourceManager.Instance;
        if (manager == null &&
            (selectedUpgradable.BuildingType == null || !selectedUpgradable.BuildingType.DebugFreeUpgrades))
        {
            return;
        }

        selectedUpgradable.TryUpgrade(manager);
        SetProductionInfo(selectedProducer, false);
        UpdateUpgradeUI();
    }
    private void OnProductionToggleClicked()
    {
        if (selectedProducer == null)
        {
            return;
        }

        if (selectedProducer.IsProductionPaused)
        {
            selectedProducer.ResumeProduction();
        }
        else
        {
            selectedProducer.PauseProduction();
        }

        UpdateProductionToggleButton(false);
        SetProductionInfo(selectedProducer, false);
    }

    private void UpdateProductionToggleButton(bool isTownHall)
    {
        if (productionToggleButton == null)
        {
            return;
        }

        bool canShow = !isTownHall &&
                       selectedBuildingType != null &&
                       selectedBuildingType.AllowProductionPauseControl &&
                       selectedProducer != null &&
                       selectedProducer.OutputResource != null;

        productionToggleButton.style.display = canShow ? DisplayStyle.Flex : DisplayStyle.None;
        if (!canShow)
        {
            return;
        }

        productionToggleButton.text = selectedProducer.IsProductionPaused
            ? "RESUME PRODUCTION"
            : "PAUSE PRODUCTION";
    }
    private void HandleResourceChanged(ResourceType type, int amount)
    {
        if (type == null)
        {
            return;
        }

        var manager = ResourceManager.Instance;
        bool wasUnlocked = unlockedResources.Contains(type);
        if (amount > 0)
        {
            unlockedResources.Add(type);
        }

        if (resourceLabels.TryGetValue(type, out var label) && label != null)
        {
            UpdateResourceLabelVisual(manager, type, label, amount);
        }
        else if (autoDiscoverFromResources || showOnlyUnlockedResources)
        {
            BuildResourceBar();
        }

        if (showOnlyUnlockedResources && !wasUnlocked && unlockedResources.Contains(type))
        {
            BuildResourceBar();
        }

        RefreshHarmonyIndicator();
        RefreshBuildButtons();
        RefreshMarketButtons();
        RefreshResearchBindings();
        RefreshBlessingBindings();
        UpdateUpgradeUI();
        RefreshTownHallNameUI();
    }

    private void HandleStorageChanged(int total, int capacity)
    {
        UpdateAllResourceValueLabels();
        RefreshMarketButtons();
    }

    private void UpdateAllResourceValueLabels()
    {
        var manager = ResourceManager.Instance;
        if (manager == null)
        {
            return;
        }

        foreach (var pair in resourceLabels)
        {
            var type = pair.Key;
            var label = pair.Value;
            if (type == null || label == null)
            {
                continue;
            }

            UpdateResourceLabelVisual(manager, type, label, manager.Get(type));
        }

        RefreshHarmonyIndicator();
    }

    private static string FormatResourceValue(ResourceManager manager, ResourceType type, int currentAmount)
    {
        string current = NumberFormatter.Format(currentAmount);
        if (manager == null || type == null)
        {
            return current;
        }

        int cap = manager.GetCapacity(type);
        if (cap <= 0)
        {
            return current;
        }

        return $"{current}/{NumberFormatter.Format(cap)}";
    }

    private void UpdateResourceLabelVisual(ResourceManager manager, ResourceType type, Label label, int currentAmount)
    {
        if (label == null)
        {
            return;
        }

        label.text = FormatResourceValue(manager, type, currentAmount);
        label.style.color = new StyleColor(GetResourceFillColor(manager, type, currentAmount));
    }

    private Color GetResourceFillColor(ResourceManager manager, ResourceType type, int currentAmount)
    {
        if (manager == null || type == null)
        {
            return resourceUnlimitedColor;
        }

        int cap = manager.GetCapacity(type);
        if (cap <= 0)
        {
            return resourceUnlimitedColor;
        }

        float fill = Mathf.Clamp01((float)Mathf.Max(0, currentAmount) / Mathf.Max(1, cap));

        float t1 = Mathf.Clamp01(resourceBand1Threshold);
        float t2 = Mathf.Max(t1, Mathf.Clamp01(resourceBand2Threshold));
        float t3 = Mathf.Max(t2, Mathf.Clamp01(resourceBand3Threshold));
        float t4 = Mathf.Max(t3, Mathf.Clamp01(resourceBand4Threshold));
        float t5 = Mathf.Max(t4, Mathf.Clamp01(resourceBand5Threshold));

        if (fill < t1)
        {
            return resourceBand1Color;
        }

        if (fill < t2)
        {
            return resourceBand2Color;
        }

        if (fill < t3)
        {
            return resourceHarmonyColor;
        }

        if (fill < t4)
        {
            return resourceBand4Color;
        }

        if (fill < t5)
        {
            return resourceBand5Color;
        }

        return resourceBand6Color;
    }

    private enum ResourceFillBand
    {
        Band1,
        Band2,
        ResourceHarmony,
        Band4,
        Band5,
        Band6,
        Unlimited
    }

    private ResourceFillBand GetResourceFillBand(ResourceManager manager, ResourceType type, int currentAmount)
    {
        if (manager == null || type == null)
        {
            return ResourceFillBand.Unlimited;
        }

        int cap = manager.GetCapacity(type);
        if (cap <= 0)
        {
            return ResourceFillBand.Unlimited;
        }

        float fill = Mathf.Clamp01((float)Mathf.Max(0, currentAmount) / Mathf.Max(1, cap));

        float t1 = Mathf.Clamp01(resourceBand1Threshold);
        float t2 = Mathf.Max(t1, Mathf.Clamp01(resourceBand2Threshold));
        float t3 = Mathf.Max(t2, Mathf.Clamp01(resourceBand3Threshold));
        float t4 = Mathf.Max(t3, Mathf.Clamp01(resourceBand4Threshold));
        float t5 = Mathf.Max(t4, Mathf.Clamp01(resourceBand5Threshold));

        if (fill < t1) return ResourceFillBand.Band1;
        if (fill < t2) return ResourceFillBand.Band2;
        if (fill < t3) return ResourceFillBand.ResourceHarmony;
        if (fill < t4) return ResourceFillBand.Band4;
        if (fill < t5) return ResourceFillBand.Band5;
        return ResourceFillBand.Band6;
    }

    private void RefreshHarmonyIndicator()
    {
        // Harmony indicator is handled by HarmonyIndicatorController.
    }

    private void RefreshTownHallNameUI()
    {
        if (activeTownHall == null)
        {
            return;
        }

        if (inspectorBuildingLabel != null)
        {
            inspectorBuildingLabel.text = GetTownHallDisplayNameWithEra(activeTownHall);
        }

        bool hasNamed = activeTownHall.HasNamed;
        bool canRename = activeTownHall.AllowRename && hasNamed;
        bool hasCosts = activeTownHall.RenameCosts != null && activeTownHall.RenameCosts.Count > 0;

        if (townHallNameField != null)
        {
            if (string.IsNullOrWhiteSpace(townHallNameField.value))
            {
                townHallNameField.value = activeTownHall.DisplayName;
            }
        }

        if (townHallNameButton != null)
        {
            townHallNameButton.text = hasNamed ? "Rename City" : "Name City";

            bool enabled = !hasNamed || (canRename && CanAffordRename(activeTownHall));
            townHallNameButton.SetEnabled(enabled);
            if (enabled)
            {
                townHallNameButton.RemoveFromClassList("is-disabled");
            }
            else
            {
                townHallNameButton.AddToClassList("is-disabled");
            }
        }

        if (townHallNameCostRow != null)
        {
            townHallNameCostRow.Clear();
        var manager = ResourceManager.Instance;            if (canRename && hasCosts)
            {
                BuildTownHallCostRow(townHallNameCostRow, activeTownHall);
            }
        }

        RefreshTownHallNextEraButton();
        RefreshTownHallEraRequirementsUI();
    }

    private void OnTownHallNameClicked()
    {
        if (activeTownHall == null || townHallNameField == null)
        {
            return;
        }

        string newName = townHallNameField.value;
        var manager = ResourceManager.Instance;
        bool success = activeTownHall.TrySetName(newName, manager);
        if (success)
        {
            ApplyDebugModeFromCityName(activeTownHall.DisplayName);
            if (inspectorBuildingLabel != null)
            {
                inspectorBuildingLabel.text = GetTownHallDisplayNameWithEra(activeTownHall);
            }
            RefreshTownHallNameUI();
        }
    }


    private void OnTownHallNextEraClicked()
    {
        if (activeTownHall == null)
        {
            return;
        }

        var eraManager = EraProgressionManager.Instance != null
            ? EraProgressionManager.Instance
            : Object.FindAnyObjectByType<EraProgressionManager>();
        if (eraManager == null || !eraManager.HasNextEra())
        {
            return;
        }

        bool advanced = false;

        if (IsDebugEraSkipEnabled())
        {
            eraManager.SetCurrentEraIndexDirect(eraManager.CurrentEraIndex + 1);
            advanced = true;
        }
        else
        {
            var resourceManager = ResourceManager.Instance != null
                ? ResourceManager.Instance
                : Object.FindAnyObjectByType<ResourceManager>();
            int harmonyPercent = ResolveHarmonyPercent();
            advanced = eraManager.TryAdvance(resourceManager, harmonyPercent, out _);
        }

        if (advanced)
        {
            RefreshTownHallNameUI();
            RefreshBuildMenu();
        }
    }

    private void RefreshTownHallNextEraButton()
    {
        if (townHallNextEraButton == null)
        {
            return;
        }

        var eraManager = EraProgressionManager.Instance != null
            ? EraProgressionManager.Instance
            : Object.FindAnyObjectByType<EraProgressionManager>();

        if (eraManager == null)
        {
            townHallNextEraButton.text = "Next Era";
            SetTownHallNextEraButtonEnabled(false);
            return;
        }

        if (!eraManager.HasNextEra())
        {
            townHallNextEraButton.text = "Max Era";
            SetTownHallNextEraButtonEnabled(false);
            return;
        }

        string nextEraName = eraManager.NextEraName;
        townHallNextEraButton.text = string.IsNullOrWhiteSpace(nextEraName)
            ? "Next Era"
            : $"Next Era ({nextEraName})";

        var resourceManager = ResourceManager.Instance != null
            ? ResourceManager.Instance
            : Object.FindAnyObjectByType<ResourceManager>();
        int harmonyPercent = ResolveHarmonyPercent();

        bool canAdvance = IsDebugEraSkipEnabled() || eraManager.CanAdvance(resourceManager, harmonyPercent, out _);
        SetTownHallNextEraButtonEnabled(canAdvance);
    }

    private void SetTownHallNextEraButtonEnabled(bool enabled)
    {
        if (townHallNextEraButton == null)
        {
            return;
        }

        townHallNextEraButton.SetEnabled(enabled);
        if (enabled)
        {
            townHallNextEraButton.RemoveFromClassList("is-disabled");
        }
        else
        {
            townHallNextEraButton.AddToClassList("is-disabled");
        }
    }


    private void RefreshTownHallEraRequirementsUI()
    {
        if (townHallEraRequirementsSection == null)
        {
            return;
        }

        if (activeTownHall == null)
        {
            townHallEraRequirementsSection.style.display = DisplayStyle.None;
            return;
        }

        var eraManager = EraProgressionManager.Instance != null
            ? EraProgressionManager.Instance
            : Object.FindAnyObjectByType<EraProgressionManager>();
        var resourceManager = ResourceManager.Instance != null
            ? ResourceManager.Instance
            : Object.FindAnyObjectByType<ResourceManager>();

        if (eraManager == null)
        {
            townHallEraRequirementsSection.style.display = DisplayStyle.None;
            return;
        }

        townHallEraRequirementsSection.style.display = DisplayStyle.Flex;

        bool hasNextEra = eraManager.HasNextEra();

        if (townHallEraRequirementsTitle != null)
        {
            if (hasNextEra)
            {
                string nextEraName = eraManager.NextEraName;
                townHallEraRequirementsTitle.text = string.IsNullOrWhiteSpace(nextEraName)
                    ? "Next Era Requirements"
                    : $"Next Era Requirements ({nextEraName})";
            }
            else
            {
                townHallEraRequirementsTitle.text = "Next Era Requirements";
            }
        }

        if (hasNextEra)
        {
            BuildTownHallEraRequirementRow(townHallEraConsumeRow, eraManager.CurrentConsumeRequirements, resourceManager, "Spend");
            BuildTownHallEraRequirementRow(townHallEraKeepRow, eraManager.CurrentKeepRequirements, resourceManager, "Have");
        }
        else
        {
            BuildTownHallEraRequirementRow(townHallEraConsumeRow, Array.Empty<EraProgressionManager.ResourceRequirement>(), resourceManager, "Spend");
            BuildTownHallEraRequirementRow(townHallEraKeepRow, Array.Empty<EraProgressionManager.ResourceRequirement>(), resourceManager, "Have");
        }

        if (townHallEraHarmonyRequirementLabel != null)
        {
            int currentHarmony = ResolveHarmonyPercent();
            int requiredHarmony = eraManager.CurrentRequiredHarmonyPercent;
            bool enoughHarmony = currentHarmony >= requiredHarmony;

            if (hasNextEra)
            {
                townHallEraHarmonyRequirementLabel.text = $"Harmony: {currentHarmony}%/{requiredHarmony}%";
                townHallEraHarmonyRequirementLabel.style.color = new StyleColor(
                    enoughHarmony ? new Color(0.67f, 0.95f, 0.55f, 1f) : new Color(0.88f, 0.24f, 0.18f, 1f));
            }
            else
            {
                townHallEraHarmonyRequirementLabel.text = "Max era reached";
                townHallEraHarmonyRequirementLabel.style.color = new StyleColor(new Color(0.67f, 0.95f, 0.55f, 1f));
            }
        }
    }

    private void BuildTownHallEraRequirementRow(
        VisualElement row,
        IReadOnlyList<EraProgressionManager.ResourceRequirement> requirements,
        ResourceManager manager,
        string prefix)
    {
        if (row == null)
        {
            return;
        }

        row.Clear();
        row.AddToClassList("townhall-era-requirement-row");

        var prefixLabel = new Label(prefix + ":");
        prefixLabel.AddToClassList("townhall-era-inline-label");
        row.Add(prefixLabel);

        var itemsRow = new VisualElement();
        itemsRow.AddToClassList("townhall-era-items-row");
        row.Add(itemsRow);

        bool addedAny = false;
        if (requirements != null)
        {
            for (int i = 0; i < requirements.Count; i++)
            {
                var req = requirements[i];
                if (req == null || req.resourceType == null || req.amount <= 0)
                {
                    continue;
                }

                int current = manager != null ? manager.Get(req.resourceType) : 0;
                bool enough = current >= req.amount;

                var item = new VisualElement();
                item.AddToClassList("townhall-era-cost-item");

                var icon = new VisualElement();
                icon.AddToClassList("townhall-era-cost-icon");
                Sprite iconSprite = ResolveResourceIcon(req.resourceType);
                if (iconSprite != null)
                {
                    icon.style.backgroundImage = new StyleBackground(iconSprite);
                }
                item.Add(icon);

                var amountLabel = new Label($"{NumberFormatter.Format(current)}/{NumberFormatter.Format(req.amount)}");
                amountLabel.AddToClassList("townhall-era-amount");
                amountLabel.style.color = new StyleColor(
                    enough ? new Color(0.67f, 0.95f, 0.55f, 1f) : new Color(0.88f, 0.24f, 0.18f, 1f));
                item.Add(amountLabel);

                var nameLabel = new Label(req.resourceType.DisplayName);
                nameLabel.AddToClassList("townhall-era-resource-name");
                item.Add(nameLabel);

                itemsRow.Add(item);
                addedAny = true;
            }
        }

        if (!addedAny)
        {
            var none = new Label("None");
            none.AddToClassList("townhall-era-resource-name");
            itemsRow.Add(none);
        }
    }
    private int ResolveHarmonyPercent()
    {
        HarmonyIndicatorController harmony = GetComponent<HarmonyIndicatorController>();
        if (harmony == null)
        {
            harmony = Object.FindAnyObjectByType<HarmonyIndicatorController>();
        }

        return harmony != null ? harmony.CurrentHarmonyPercent : 0;
    }


    private bool IsDebugEraSkipEnabled()
    {
        return activeTownHall != null && IsDebugCityName(activeTownHall.DisplayName);
    }
    private void ApplyDebugModeFromCityName(string cityName)
    {
        bool enableDebug = IsDebugCityName(cityName);
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

            type.SetDebugFlags(enableDebug, enableDebug);
        }
    }

    private static bool IsDebugCityName(string cityName)
    {
        if (string.IsNullOrWhiteSpace(cityName))
        {
            return false;
        }

        return string.Equals(cityName.Trim(), "Debug", System.StringComparison.OrdinalIgnoreCase);
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

    private static void ResetDebugFlagsForEditorStop()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ResetAllBuildingTypeDebugFlags();
        }
#endif
    }

    private bool CanAffordRename(TownHallCity townHall)
    {
        if (townHall == null)
        {
            return false;
        }

        var manager = ResourceManager.Instance;
        return townHall.CanAffordRename(manager);
    }

    private void BuildTownHallCostRow(VisualElement costRow, TownHallCity townHall)
    {
        if (costRow == null || townHall == null || townHall.RenameCosts == null)
        {
            return;
        }

        var costs = townHall.RenameCosts;
        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost.resourceType == null || cost.amount <= 0)
            {
                continue;
            }

            var item = new VisualElement();
            item.AddToClassList("townhall-name-cost-item");

            var icon = new VisualElement();
            icon.AddToClassList("townhall-name-cost-icon");
            Sprite iconSprite = ResolveResourceIcon(cost.resourceType);
            if (iconSprite != null)
            {
                icon.style.backgroundImage = new StyleBackground(iconSprite);
            }
            item.Add(icon);

            var amount = new Label(NumberFormatter.Format(cost.amount));
            amount.AddToClassList("townhall-name-cost-amount");
            item.Add(amount);

            costRow.Add(item);
        }
    }

    private List<ResourceType> GetResourceTypes()
    {
        if (!autoDiscoverFromResources)
        {
            var manual = new List<ResourceType>(resourceTypes);
            manual.Sort((a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                int order = a.OrderIndex.CompareTo(b.OrderIndex);
                if (order != 0) return order;
                return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
            });
            return manual;
        }

        var found = Resources.FindObjectsOfTypeAll<ResourceType>();
        var unique = new System.Collections.Generic.HashSet<ResourceType>();
        var list = new System.Collections.Generic.List<ResourceType>();
        if (found != null)
        {
            for (int i = 0; i < found.Length; i++)
            {
                var type = found[i];
                if (type == null || unique.Contains(type))
                {
                    continue;
                }

                unique.Add(type);
                list.Add(type);
            }
        }

        list.Sort((a, b) =>
        {
            int order = a.OrderIndex.CompareTo(b.OrderIndex);
            if (order != 0) return order;
            return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
        });
        return list;
    }

    private void PrimeUnlockedResources()
    {
        unlockedResources.Clear();

        var manager = ResourceManager.Instance;
        if (manager == null)
        {
            return;
        }

        var types = GetResourceTypes();
        for (int i = 0; i < types.Count; i++)
        {
            var type = types[i];
            if (type == null)
            {
                continue;
            }

            if (manager.Get(type) > 0)
            {
                unlockedResources.Add(type);
            }
        }
    }

    private List<BuildingType> GetBuildingTypes()
    {
        if (!autoDiscoverFromResources)
        {
            var manual = new System.Collections.Generic.List<BuildingType>();
            for (int i = 0; i < buildingTypes.Count; i++)
            {
                var type = buildingTypes[i];
                if (type == null)
                {
                    continue;
                }

                if (!type.ShowInBuildMenu)
                {
                    continue;
                }

                if (hiddenBuildMenuTypes.Contains(type))
                {
                    continue;
                }

                if (!IsBuildingUnlockedForCurrentEra(type))
                {
                    continue;
                }

                manual.Add(type);
            }
            manual.Sort((a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                int order = a.OrderIndex.CompareTo(b.OrderIndex);
                if (order != 0) return order;
                return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
            });
            return manual;
        }

        var found = Resources.FindObjectsOfTypeAll<BuildingType>();
        var unique = new System.Collections.Generic.HashSet<BuildingType>();
        var list = new System.Collections.Generic.List<BuildingType>();
        if (found != null)
        {
            for (int i = 0; i < found.Length; i++)
            {
                var type = found[i];
                if (type == null || unique.Contains(type))
                {
                    continue;
                }

                if (!type.ShowInBuildMenu)
                {
                    continue;
                }

                if (hiddenBuildMenuTypes.Contains(type))
                {
                    continue;
                }

                if (!IsBuildingUnlockedForCurrentEra(type))
                {
                    continue;
                }

                unique.Add(type);
                list.Add(type);
            }
        }

        list.Sort((a, b) =>
        {
            int order = a.OrderIndex.CompareTo(b.OrderIndex);
            if (order != 0) return order;
            return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
        });
        return list;
    }


    private static bool IsBuildingUnlockedForCurrentEra(BuildingType type)
    {
        if (type == null)
        {
            return false;
        }

        var eraManager = EraProgressionManager.Instance != null
            ? EraProgressionManager.Instance
            : Object.FindAnyObjectByType<EraProgressionManager>();

        // If no era system is present, keep previous behavior.
        if (eraManager == null)
        {
            return true;
        }

        return eraManager.CurrentEraIndex >= type.RequiredEraIndex;
    }
    private Sprite ResolveResourceIcon(ResourceType type)
    {
        if (type == null)
        {
            return null;
        }

        return type.Icon;
    }

    private Sprite ResolveBuildingIcon(BuildingType type)
    {
        if (type == null)
        {
            return null;
        }

        if (buildingIconLookup.TryGetValue(type, out var icon))
        {
            return icon;
        }

        if (type.Prefab == null)
        {
            return null;
        }

        var renderer = type.Prefab.GetComponentInChildren<SpriteRenderer>(true);
        return renderer != null ? renderer.sprite : null;
    }

    private static bool CanAfford(ResourceManager manager, BuildingType type)
    {
        if (manager == null || type == null)
        {
            return false;
        }

        if (type.DebugFreeBuild)
        {
            return true;
        }

        var costs = type.Costs;
        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost.resourceType == null || cost.amount <= 0)
            {
                continue;
            }

            int amount = GetModifiedCost(cost.amount);
            if (manager.Get(cost.resourceType) < amount)
            {
                return false;
            }
        }

        return true;
    }

    private static void SpendCosts(ResourceManager manager, BuildingType type)
    {
        if (manager == null || type == null)
        {
            return;
        }

        if (type.DebugFreeBuild)
        {
            return;
        }

        var costs = type.Costs;
        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost.resourceType == null || cost.amount <= 0)
            {
                continue;
            }

            int amount = GetModifiedCost(cost.amount);
            manager.Spend(cost.resourceType, amount);
        }
    }

    private void RefreshBuildButtons()
    {
        var manager = ResourceManager.Instance;

        for (int i = 0; i < buildButtons.Count; i++)
        {
            var entry = buildButtons[i];
            if (entry.Type == null)
            {
                continue;
            }

            if (entry.Button != null)
            {
                bool canAfford = manager != null && CanAfford(manager, entry.Type);
                entry.Button.SetEnabled(canAfford);
                if (canAfford)
                {
                    entry.Button.RemoveFromClassList("is-disabled");
                }
                else
                {
                    entry.Button.AddToClassList("is-disabled");
                }
            }

            RefreshCostRowAmountColors(entry.CostRow, manager);
        }
    }

    private void RefreshCostRowAmountColors(VisualElement costRow, ResourceManager manager)
    {
        if (costRow == null)
        {
            return;
        }

        var amountLabels = costRow.Query<Label>(className: "cost-amount").ToList();
        for (int i = 0; i < amountLabels.Count; i++)
        {
            var label = amountLabels[i];
            if (label == null || !(label.userData is CostLabelMetadata metadata))
            {
                continue;
            }

            ApplyCostLabelColor(label, manager, metadata.ResourceType, metadata.RequiredAmount);
        }
    }

    private void ApplyCostLabelColor(Label label, ResourceManager manager, ResourceType resourceType, int requiredAmount)
    {
        if (label == null || resourceType == null || requiredAmount <= 0)
        {
            return;
        }

        bool hasEnough = manager != null && manager.Get(resourceType) >= requiredAmount;
        label.style.color = new StyleColor(hasEnough ? affordableCostColor : missingCostColor);
    }

    public void ShowTownHallUI()
    {
        ShowTownHallUI(null);
    }

    public void ShowTownHallUI(TownHallCity townHall)
    {
        SetProductionInfo(null, true);
        selectedUpgradable = null;
        selectedProducer = null;
        UpdateProductionToggleButton(false);
        selectedBuildingType = null;
        selectedManualClick = null;
        activeTownHall = townHall;

        string displayName = GetTownHallDisplayNameWithEra(townHall);
        ShowInspectorOnly(displayName, true);
        HideBuildingPanel();
        UpdateUpgradeUI();
        HideBuildMenu();
        HideTownHallPanel();
        ShowTownHallPanel();
        RefreshTownHallNameUI();
    }

    public void HideTownHallUI()
    {
        HideAllPanels();
    }

    public void CloseAllPanels()
    {
        HideAllPanels();
    }

    public void ToggleTownHallUI()
    {
        if (IsInspectorVisible())
        {
            HideAllPanels();
        }
        else
        {
            ShowBuildMenuOnly();
        }
    }


    private string GetTownHallDisplayNameWithEra(TownHallCity townHall)
    {
        string cityName = townHall != null ? townHall.DisplayName : townHallDisplayName;

        var eraManager = EraProgressionManager.Instance != null
            ? EraProgressionManager.Instance
            : Object.FindAnyObjectByType<EraProgressionManager>();
        string eraName = eraManager != null ? eraManager.CurrentEraName : string.Empty;

        if (string.IsNullOrWhiteSpace(eraName))
        {
            return cityName;
        }

        return $"{cityName} - {eraName}";
    }
    private void ShowInspectorOnly(string displayName, bool isTownHall)
    {
        if (inspectorBuildingLabel != null)
        {
            selectedBuildingName = string.IsNullOrEmpty(displayName) ? "(none)" : displayName;
            inspectorBuildingLabel.text = selectedBuildingName;
        }

        if (inspectorPanel != null)
        {
            inspectorPanel.style.display = DisplayStyle.Flex;
        }

        if (openBuildMenuButton != null)
        {
            openBuildMenuButton.style.display = isTownHall ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (townHallDetailsButton != null)
        {
            bool canShowDetails = isTownHall || (selectedBuildingType != null && selectedBuildingType.HasDetailsView);
            townHallDetailsButton.style.display = canShowDetails ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (upgradeButton != null)
        {
            upgradeButton.style.display = isTownHall ? DisplayStyle.None : DisplayStyle.Flex;
        }

        UpdateProductionToggleButton(isTownHall);

        if (townHallNameSection != null)
        {
            townHallNameSection.style.display = isTownHall ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (townHallNextEraButton != null)
        {
            townHallNextEraButton.style.display = isTownHall ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (townHallEraRequirementsSection != null)
        {
            townHallEraRequirementsSection.style.display = isTownHall ? DisplayStyle.Flex : DisplayStyle.None;
        }

        HideBuildMenu();
    }
    private void UpdateUpgradeUI()
    {
        if (upgradeButton != null)
        {
            upgradeButton.RemoveFromClassList("is-disabled");
        }

        if (levelInfoLabel != null)
        {
            levelInfoLabel.text = string.Empty;
            levelInfoLabel.style.display = DisplayStyle.None;
        }

        if (nextLevelTitleLabel != null)
        {
            nextLevelTitleLabel.style.display = DisplayStyle.None;
            nextLevelTitleLabel.text = "Next level:";
        }

        if (nextStatsLabel != null)
        {
            nextStatsLabel.text = string.Empty;
            nextStatsLabel.style.display = DisplayStyle.None;
        }

        if (nextCostRow != null)
        {
            nextCostRow.Clear();
            nextCostRow.style.display = DisplayStyle.None;
        }

        if (selectedUpgradable == null || selectedUpgradable.BuildingType == null ||
            selectedUpgradable.BuildingType.UpgradeLevels == null)
        {
            if (upgradeButton != null)
            {
                upgradeButton.SetEnabled(false);
                upgradeButton.AddToClassList("is-disabled");
            }
            return;
        }

        UpdateManualClickInfo();

        bool hasNext = selectedUpgradable.HasNextLevel();
        if (upgradeButton != null)
        {
            upgradeButton.SetEnabled(hasNext);
            if (!hasNext)
            {
                upgradeButton.AddToClassList("is-disabled");
            }
        }

        string levelText = string.Empty;
        if (selectedUpgradable.IsAtImplicitBase)
        {
            levelText = "Level 1";
        }
        else if (selectedUpgradable.BuildingType != null &&
                 selectedUpgradable.BuildingType.UpgradeLevels != null &&
                 selectedUpgradable.BuildingType.UpgradeLevels.Count > 0)
        {
            int idx = Mathf.Clamp(selectedUpgradable.CurrentLevel, 0,
                selectedUpgradable.BuildingType.UpgradeLevels.Count - 1);
            string name = selectedUpgradable.BuildingType.UpgradeLevels[idx].levelName;
            levelText = string.IsNullOrEmpty(name) ? $"Level {selectedUpgradable.DisplayLevel}" : name;
        }
        else
        {
            levelText = $"Level {selectedUpgradable.DisplayLevel}";
        }

        if (inspectorBuildingLabel != null)
        {
            inspectorBuildingLabel.text = $"{selectedBuildingName} {levelText}".Trim();
        }

        if (!hasNext)
        {
            if (inspectorBuildingLabel != null)
            {
                inspectorBuildingLabel.text = $"{selectedBuildingName} {levelText} (Max)".Trim();
            }

            if (nextLevelTitleLabel != null)
            {
                nextLevelTitleLabel.text = "Max level reached";
                nextLevelTitleLabel.style.display = DisplayStyle.Flex;
            }
            return;
        }

        var nextLevel = selectedUpgradable.BuildingType.UpgradeLevels[selectedUpgradable.CurrentLevel + 1];
        bool freeUpgrades = selectedUpgradable.BuildingType.DebugFreeUpgrades;
        if (nextLevelTitleLabel != null)
        {
            nextLevelTitleLabel.text = "Next level:";
            nextLevelTitleLabel.style.display = DisplayStyle.Flex;
        }

        if (nextStatsLabel != null)
        {
            if (selectedManualClick != null && nextLevel.manualClickAmount > 0)
            {
                nextStatsLabel.text = $"Manual click: +{NumberFormatter.Format(nextLevel.manualClickAmount)}";
            }
            else if (selectedProducer != null && selectedProducer.OutputResource != null)
            {
                nextStatsLabel.text = FormatProductionRule(
                    nextLevel.inputResourcesPerCycle,
                    nextLevel.amountPerCycle,
                    selectedProducer.OutputResource,
                    nextLevel.intervalSeconds);
            }
            else
            {
                string nextStorageSummary = BuildStorageBonusSummary(selectedUpgradable.BuildingType, selectedUpgradable.CurrentLevel + 1);
                nextStatsLabel.text = string.IsNullOrEmpty(nextStorageSummary)
                    ? string.Empty
                    : $"After upgrade: {nextStorageSummary}";
            }

            nextStatsLabel.style.display = string.IsNullOrEmpty(nextStatsLabel.text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        bool canAfford = freeUpgrades || CanAfford(ResourceManager.Instance, nextLevel.costs);
        if (upgradeButton != null)
        {
            upgradeButton.SetEnabled(canAfford);
            if (!canAfford)
            {
                upgradeButton.AddToClassList("is-disabled");
            }
        }

        if (nextCostRow != null)
        {
            BuildInspectorCostRow(nextCostRow, nextLevel.costs, freeUpgrades);
            nextCostRow.style.display = DisplayStyle.Flex;
        }
    }
    private void BuildInspectorCostRow(VisualElement container, System.Collections.Generic.IReadOnlyList<BuildingType.ResourceCost> costs, bool isFree)
    {
        if (container == null)
        {
            return;
        }

        container.Clear();
        var manager = ResourceManager.Instance;
        if (isFree || costs == null || costs.Count == 0)
        {
            var label = new Label("Free");
            label.AddToClassList("inspector-cost-amount");
            container.Add(label);
            return;
        }

        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost.resourceType == null || cost.amount <= 0)
            {
                continue;
            }

            var item = new VisualElement();
            item.AddToClassList("inspector-cost-item");

            var icon = new VisualElement();
            icon.AddToClassList("inspector-cost-icon");
            var iconSprite = ResolveResourceIcon(cost.resourceType);
            if (iconSprite != null)
            {
                icon.style.backgroundImage = new StyleBackground(iconSprite);
            }
            item.Add(icon);

            int requiredAmount = GetModifiedCost(cost.amount);
            var amount = new Label(requiredAmount.ToString());
            amount.AddToClassList("inspector-cost-amount");
            ApplyCostLabelColor(amount, manager, cost.resourceType, requiredAmount);
            item.Add(amount);

            container.Add(item);
        }
    }

    private static bool CanAfford(ResourceManager manager, System.Collections.Generic.IReadOnlyList<BuildingType.ResourceCost> costs)
    {
        if (manager == null || costs == null)
        {
            return false;
        }

        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost.resourceType == null || cost.amount <= 0)
            {
                continue;
            }

            int amount = GetModifiedCost(cost.amount);
            if (manager.Get(cost.resourceType) < amount)
            {
                return false;
            }
        }

        return true;
    }

    private static int GetModifiedCost(int amount)
    {
        var modifiers = GlobalModifiers.Instance != null
            ? GlobalModifiers.Instance
            : Object.FindAnyObjectByType<GlobalModifiers>();
        return modifiers != null ? modifiers.ApplyCost(amount) : amount;
    }
    private static string FormatProductionRule(
        System.Collections.Generic.IReadOnlyList<BuildingType.ResourceCost> inputCosts,
        int outputAmount,
        ResourceType outputResource,
        float intervalSeconds)
    {
        string intervalText = intervalSeconds <= 0f ? "instant" : $"{intervalSeconds:0.##} seconds";
        string outputName = outputResource != null ? outputResource.DisplayName : "Output";

        var inputs = new List<string>();
        if (inputCosts != null)
        {
            for (int i = 0; i < inputCosts.Count; i++)
            {
                var cost = inputCosts[i];
                if (cost.resourceType == null || cost.amount <= 0)
                {
                    continue;
                }

                inputs.Add($"-{NumberFormatter.Format(cost.amount)} {cost.resourceType.DisplayName}");
            }
        }

        string outputPart = $"+{NumberFormatter.Format(outputAmount)} {outputName}";
        if (inputs.Count == 0)
        {
            return $"{outputPart} every {intervalText}.";
        }

        return $"{string.Join(" + ", inputs)} -> {outputPart} every {intervalText}.";
    }
    private void SetProductionInfo(BuildingProducer producer, bool isTownHall)
    {
        if (productionInfoLabel == null)
        {
            return;
        }

        if (!isTownHall && selectedManualClick != null && BuildingHasManualClickLevels(selectedUpgradable))
        {
            int clickAmount = ResolveManualClickAmount();
            productionInfoLabel.text = $"Manual click: +{NumberFormatter.Format(clickAmount)}";
            productionInfoLabel.style.display = DisplayStyle.Flex;
            return;
        }

        if (!isTownHall && producer == null && selectedUpgradable != null)
        {
            string storageSummary = BuildStorageBonusSummary(selectedUpgradable.BuildingType, selectedUpgradable.CurrentLevel);
            if (!string.IsNullOrEmpty(storageSummary))
            {
                productionInfoLabel.text = $"Storage bonus: {storageSummary}";
                productionInfoLabel.style.display = DisplayStyle.Flex;
                return;
            }
        }

        if (isTownHall || producer == null || producer.OutputResource == null)
        {
            productionInfoLabel.text = string.Empty;
            productionInfoLabel.style.display = DisplayStyle.None;
            return;
        }

        float interval = producer.IntervalSeconds;
        if (interval <= 0f)
        {
            interval = 1f;
        }

        int amount = producer.AmountPerCycle;
        string baseLine = FormatProductionRule(producer.InputResourcesPerCycle, amount, producer.OutputResource, interval);

        if (producer.TryGetProximityInfo(out var proximityResource, out var distance, out var bonusPercent, out var hasTarget))
        {
            string proximityName = proximityResource != null ? proximityResource.DisplayName : "Resource";
            if (!hasTarget)
            {
                baseLine += $"\nProximity: no {proximityName} node found.";
            }
            else if (bonusPercent > 0f)
            {
                baseLine += $"\nProximity bonus: +{bonusPercent * 100f:0.#}% ({proximityName}, {distance:0.##}m)";
            }
            else
            {
                baseLine += $"\nProximity bonus: 0% ({proximityName}, {distance:0.##}m)";
            }
        }

        if (producer.IsProductionPaused)
        {
            baseLine += "\nStatus: Paused";
        }

        productionInfoLabel.text = baseLine;
        productionInfoLabel.style.display = DisplayStyle.Flex;
    }
    private void UpdateManualClickInfo()
    {
        if (selectedManualClick == null)
        {
            return;
        }

        SetProductionInfo(selectedProducer, false);
    }

    private static bool BuildingHasManualClickLevels(BuildingUpgradable upgradable)
    {
        if (upgradable == null || upgradable.BuildingType == null)
        {
            return false;
        }

        var levels = upgradable.BuildingType.UpgradeLevels;
        if (levels == null || levels.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i] != null && levels[i].manualClickAmount > 0)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsPointerOverHudUI(Vector2 screenPosition)
    {
        if (root == null || root.panel == null)
        {
            return false;
        }

        var panel = root.panel;
        Vector2 panelPositionA = RuntimePanelUtils.ScreenToPanel(panel, screenPosition);
        if (IsInteractiveElementPicked(panel, panelPositionA))
        {
            return true;
        }

        Vector2 flippedScreen = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        Vector2 panelPositionB = RuntimePanelUtils.ScreenToPanel(panel, flippedScreen);
        return IsInteractiveElementPicked(panel, panelPositionB);
    }

    private static bool IsInteractiveElementPicked(IPanel panel, Vector2 panelPosition)
    {
        if (panel == null)
        {
            return false;
        }

        VisualElement picked = panel.Pick(panelPosition);
        while (picked != null)
        {
            if (picked.pickingMode == PickingMode.Position &&
                picked.resolvedStyle.display != DisplayStyle.None &&
                picked.resolvedStyle.visibility == Visibility.Visible)
            {
                return true;
            }

            picked = picked.parent;
        }

        return false;
    }
    private int ResolveManualClickAmount()
    {
        var manualSystem = ManualClickSystem.Instance;
        if (manualSystem != null)
        {
            return manualSystem.CurrentClickAmount;
        }

        if (selectedUpgradable == null || selectedUpgradable.BuildingType == null)
        {
            return 1;
        }

        var levels = selectedUpgradable.BuildingType.UpgradeLevels;
        if (levels == null || levels.Count == 0)
        {
            return 1;
        }

        if (selectedUpgradable.IsAtImplicitBase || selectedUpgradable.CurrentLevel < 0)
        {
            return 1;
        }

        int clamped = Mathf.Clamp(selectedUpgradable.CurrentLevel, 0, levels.Count - 1);
        int amount = levels[clamped].manualClickAmount;
        return amount > 0 ? amount : 1;
    }

    private static string BuildStorageBonusSummary(BuildingType type, int levelInclusive)
    {
        if (type == null)
        {
            return string.Empty;
        }

        int globalBonus = 0;
        var resourceBonuses = new Dictionary<ResourceType, int>();

        type.AccumulateBaseStorageBonuses(resourceBonuses, ref globalBonus);
        type.AccumulateUpgradeStorageBonusesUpToLevel(levelInclusive, resourceBonuses, ref globalBonus);

        var parts = new List<string>();
        if (globalBonus > 0)
        {
            parts.Add($"+{NumberFormatter.Format(globalBonus)} all storage");
        }

        foreach (var pair in resourceBonuses)
        {
            if (pair.Key == null || pair.Value <= 0)
            {
                continue;
            }

            parts.Add($"+{NumberFormatter.Format(pair.Value)} {pair.Key.DisplayName}");
        }

        return parts.Count == 0 ? string.Empty : string.Join(" | ", parts);
    }
    private void ShowBuildMenu()
    {
        if (activeTownHall == null)
        {
            HideBuildMenu();
            return;
        }

        BuildBuildMenu();
        if (buildMenu != null)
        {
            buildMenu.style.display = DisplayStyle.Flex;
        }
    }

    private void ShowBuildMenuOnly()
    {
        HideAllPanels();
        if (activeTownHall != null)
        {
            ShowBuildMenu();
        }
    }

    private void HideBuildMenu()
    {
        ReleaseScrollbarPointerCaptures();
        if (buildMenu != null)
        {
            buildMenu.style.display = DisplayStyle.None;
        }
    }

    private void HideAllPanels()
    {
        ReleaseScrollbarPointerCaptures();
        if (inspectorPanel != null)
        {
            inspectorPanel.style.display = DisplayStyle.None;
        }

        activeTownHall = null;
        selectedBuildingType = null;
        HideTownHallPanel();
        HideBuildingPanel();
        HideBuildMenu();
        HideTalentsPanel();
    }

    
    private void OnTownHallDetailsClicked()
    {
        if (buildingDetailsController == null)
        {
            buildingDetailsController = GetComponent<BuildingDetailsController>();
            if (buildingDetailsController == null)
            {
                return;
            }
        }

        if (activeTownHall != null)
        {
            buildingDetailsController.EnterTownHallDetails(activeTownHall);
            return;
        }

        if (selectedBuildingType != null)
        {
            buildingDetailsController.EnterBuildingDetails(selectedBuildingType);
        }
    }

    private void OnOpenBuildMenuClicked()
    {
        if (activeTownHall != null)
        {
            ShowBuildMenu();
            ShowTownHallPanel();
            return;
        }

        ShowBuildMenu();
    }

    private void ShowTownHallPanel()
    {
        EnsureTownHallPanelContent();
        if (townHallPanelContent != null)
        {
            townHallPanelContent.style.display = DisplayStyle.Flex;
        }

        if (buildingPanel != null)
        {
            buildingPanel.style.display = DisplayStyle.Flex;
        }

        ClearBuildingPanelForTownHall();

        if (buildingPanelPlaceholder != null)
        {
            buildingPanelPlaceholder.style.display = DisplayStyle.None;
        }

        if (buildingPanelTitle != null)
        {
            buildingPanelTitle.style.display = DisplayStyle.None;
        }

        ShowBuildMenu();
        if (buildMenu != null)
        {
            buildMenu.style.display = DisplayStyle.Flex;
        }
        if (buildScroll != null)
        {
            buildScroll.style.display = DisplayStyle.Flex;
        }
        RefreshTownHallNameUI();
        ApplyBuildScrollTheme();
        if (buildScroll != null)
        {
            buildScroll.schedule.Execute(ApplyBuildScrollTheme).StartingIn(1);
            buildScroll.schedule.Execute(UpdateCustomScrollbar).StartingIn(1);
        }
    }

    private void HideTownHallPanel()
    {
        if (townHallPanelContent != null)
        {
            townHallPanelContent.style.display = DisplayStyle.None;
        }

        if (buildingPanelTitle != null)
        {
            buildingPanelTitle.style.display = DisplayStyle.Flex;
        }
    }

    private void ClearBuildingPanelForTownHall()
    {
        if (buildingPanelContent == null)
        {
            return;
        }

        for (int i = buildingPanelContent.childCount - 1; i >= 0; i--)
        {
            var child = buildingPanelContent[i];
            if (child == null)
            {
                continue;
            }

            if (child == townHallPanelContent || child == buildingPanelPlaceholder)
            {
                continue;
            }

            child.RemoveFromHierarchy();
        }
    }

    private void EnsureTownHallPanelContent()
    {
        if (buildingPanelContent == null || townHallPanelContent == null)
        {
            return;
        }

        if (townHallPanelContent.parent != buildingPanelContent)
        {
            buildingPanelContent.Add(townHallPanelContent);
        }
    }

    private void UpdateBuildingPanel(BuildingType type)
    {
        if (buildingPanel == null)
        {
            return;
        }

        if (type == null)
        {
            buildingPanel.style.display = DisplayStyle.None;
            return;
        }

        if (!type.ShowBuildingPanel)
        {
            buildingPanel.style.display = DisplayStyle.None;
            return;
        }

        if (buildingPanelTitle != null)
        {
            buildingPanelTitle.text = type.DisplayName;
        }

        if (buildingPanelPlaceholder != null)
        {
            buildingPanelPlaceholder.text = $"{type.DisplayName} panel";
        }

        BuildBuildingPanelContent(type);
        buildingPanel.style.display = DisplayStyle.Flex;
    }

    private void HideBuildingPanel()
    {
        if (buildingPanel != null)
        {
            buildingPanel.style.display = DisplayStyle.None;
        }
    }

    private void BuildBuildingPanelContent(BuildingType type)
    {
        if (buildingPanelContent == null)
        {
            return;
        }

        buildingPanelContent.Clear();
        marketTradeButtons.Clear();
        researchBindings.Clear();
        blessingBindings.Clear();

        if (type.BlessingDatabase != null)
        {
            BuildBlessingList(type.BlessingDatabase);
            return;
        }

        if (type.ResearchDatabase != null)
        {
            BuildResearchList(type.ResearchDatabase);
            return;
        }

        var trades = type.MarketTrades;
        if (trades == null || trades.Count == 0)
        {
            if (buildingPanelPlaceholder != null)
            {
                buildingPanelPlaceholder.style.display = DisplayStyle.Flex;
            }
            buildingPanelContent.Add(buildingPanelPlaceholder);
            return;
        }

        if (goldResource == null)
        {
            if (buildingPanelPlaceholder != null)
            {
                buildingPanelPlaceholder.text = "Gold resource not assigned.";
                buildingPanelPlaceholder.style.display = DisplayStyle.Flex;
            }
            buildingPanelContent.Add(buildingPanelPlaceholder);
            return;
        }

        if (buildingPanelPlaceholder != null)
        {
            buildingPanelPlaceholder.style.display = DisplayStyle.None;
        }

        for (int i = 0; i < trades.Count; i++)
        {
            var trade = trades[i];
            if (trade == null || trade.resourceType == null)
            {
                continue;
            }

            if (trade.resourceAmount <= 0 || trade.goldAmount <= 0)
            {
                continue;
            }

            if (!trade.allowBuy && !trade.allowSell)
            {
                continue;
            }

            var row = new VisualElement();
            row.AddToClassList("trade-row");

            var left = new VisualElement();
            left.AddToClassList("trade-left");

            var resourceIcon = new VisualElement();
            resourceIcon.AddToClassList("trade-icon");
            var resourceSprite = ResolveResourceIcon(trade.resourceType);
            if (resourceSprite != null)
            {
                resourceIcon.style.backgroundImage = new StyleBackground(resourceSprite);
            }
            left.Add(resourceIcon);

            var resourceText = new Label($"{trade.resourceAmount} {trade.resourceType.DisplayName}");
            resourceText.AddToClassList("trade-info");
            left.Add(resourceText);

            var equals = new Label("=");
            equals.AddToClassList("trade-equals");
            left.Add(equals);

            var goldIcon = new VisualElement();
            goldIcon.AddToClassList("trade-icon");
            var goldSprite = ResolveResourceIcon(goldResource);
            if (goldSprite != null)
            {
                goldIcon.style.backgroundImage = new StyleBackground(goldSprite);
            }
            left.Add(goldIcon);

            var goldText = new Label($"{trade.goldAmount} {goldResource.DisplayName}");
            goldText.AddToClassList("trade-info");
            left.Add(goldText);

            row.Add(left);

            var buttons = new VisualElement();
            buttons.AddToClassList("trade-buttons");

            Button sellButton = null;
            if (trade.allowSell)
            {
                sellButton = new Button(() => TrySellTrade(trade)) { text = "Sell" };
                sellButton.AddToClassList("trade-button");
                buttons.Add(sellButton);
            }

            Button buyButton = null;
            if (trade.allowBuy)
            {
                buyButton = new Button(() => TryBuyTrade(trade)) { text = "Buy" };
                buyButton.AddToClassList("trade-button");
                buttons.Add(buyButton);
            }

            row.Add(buttons);
            buildingPanelContent.Add(row);

            marketTradeButtons.Add(new MarketTradeBinding
            {
                Trade = trade,
                BuyButton = buyButton,
                SellButton = sellButton
            });
        }

        RefreshMarketButtons();
    }

    private void BuildResearchList(ResearchDatabase database)
    {
        if (database == null || database.Research == null || database.Research.Count == 0)
        {
            if (buildingPanelPlaceholder != null)
            {
                buildingPanelPlaceholder.text = "No research available.";
                buildingPanelPlaceholder.style.display = DisplayStyle.Flex;
                buildingPanelContent.Add(buildingPanelPlaceholder);
            }
            return;
        }

        if (buildingPanelPlaceholder != null)
        {
            buildingPanelPlaceholder.style.display = DisplayStyle.None;
        }

        var researchManager = ResearchManager.Instance != null
            ? ResearchManager.Instance
            : Object.FindAnyObjectByType<ResearchManager>();

        ResearchDefinition nextResearch = null;
        for (int i = 0; i < database.Research.Count; i++)
        {
            var research = database.Research[i];
            if (research == null)
            {
                continue;
            }

            if (researchManager != null && !researchManager.IsPurchased(research))
            {
                nextResearch = research;
                break;
            }
        }

        if (nextResearch == null)
        {
            if (buildingPanelPlaceholder != null)
            {
                buildingPanelPlaceholder.text = "No research available.";
                buildingPanelPlaceholder.style.display = DisplayStyle.Flex;
                buildingPanelContent.Add(buildingPanelPlaceholder);
            }
            return;
        }

        var tile = new VisualElement();
        tile.AddToClassList("research-tile");

        var header = new VisualElement();
        header.AddToClassList("research-header");

        if (nextResearch.Icon != null)
        {
            var icon = new VisualElement();
            icon.AddToClassList("research-icon");
            icon.style.backgroundImage = new StyleBackground(nextResearch.Icon);
            header.Add(icon);
        }

        var title = new Label(nextResearch.DisplayName);
        title.AddToClassList("research-title");
        header.Add(title);
        tile.Add(header);

        if (!string.IsNullOrEmpty(nextResearch.Description))
        {
            var desc = new Label(nextResearch.Description);
            desc.AddToClassList("research-desc");
            tile.Add(desc);
        }

        var costRow = new VisualElement();
        costRow.AddToClassList("research-cost-row");
        BuildResearchCostRow(costRow, nextResearch);
        tile.Add(costRow);

        var footer = new VisualElement();
        footer.AddToClassList("research-footer");

        var status = new Label("LOCKED");
        status.AddToClassList("research-status");
        footer.Add(status);

        var buyButton = new Button(() => TryPurchaseResearch(nextResearch)) { text = "Buy" };
        buyButton.AddToClassList("research-buy");
        footer.Add(buyButton);

        tile.Add(footer);
        buildingPanelContent.Add(tile);

        researchBindings.Add(new ResearchBinding
        {
            Research = nextResearch,
            Tile = tile,
            StatusLabel = status,
            BuyButton = buyButton
        });

        RefreshResearchBindings();
    }

    private void BuildBlessingList(BlessingDatabase database)
    {
        if (database == null || database.Blessings == null || database.Blessings.Count == 0)
        {
            if (buildingPanelPlaceholder != null)
            {
                buildingPanelPlaceholder.text = "No blessings available.";
                buildingPanelPlaceholder.style.display = DisplayStyle.Flex;
                buildingPanelContent.Add(buildingPanelPlaceholder);
            }
            return;
        }

        if (buildingPanelPlaceholder != null)
        {
            buildingPanelPlaceholder.style.display = DisplayStyle.None;
        }

        var blessingManager = BlessingManager.Instance != null
            ? BlessingManager.Instance
            : Object.FindAnyObjectByType<BlessingManager>();

        BlessingDefinition nextBlessing = null;
        for (int i = 0; i < database.Blessings.Count; i++)
        {
            var blessing = database.Blessings[i];
            if (blessing == null)
            {
                continue;
            }

            if (blessingManager != null && !blessingManager.IsPurchased(blessing))
            {
                nextBlessing = blessing;
                break;
            }
        }

        if (nextBlessing == null)
        {
            if (buildingPanelPlaceholder != null)
            {
                buildingPanelPlaceholder.text = "No blessings available.";
                buildingPanelPlaceholder.style.display = DisplayStyle.Flex;
                buildingPanelContent.Add(buildingPanelPlaceholder);
            }
            return;
        }

        var tile = new VisualElement();
        tile.AddToClassList("blessing-tile");

        var header = new VisualElement();
        header.AddToClassList("blessing-header");

        if (nextBlessing.Icon != null)
        {
            var icon = new VisualElement();
            icon.AddToClassList("blessing-icon");
            icon.style.backgroundImage = new StyleBackground(nextBlessing.Icon);
            header.Add(icon);
        }

        var title = new Label(nextBlessing.DisplayName);
        title.AddToClassList("blessing-title");
        header.Add(title);
        tile.Add(header);

        if (!string.IsNullOrEmpty(nextBlessing.Description))
        {
            var desc = new Label(nextBlessing.Description);
            desc.AddToClassList("blessing-desc");
            tile.Add(desc);
        }

        var costRow = new VisualElement();
        costRow.AddToClassList("blessing-cost-row");
        BuildBlessingCostRow(costRow, nextBlessing);
        tile.Add(costRow);

        var footer = new VisualElement();
        footer.AddToClassList("blessing-footer");

        var status = new Label("LOCKED");
        status.AddToClassList("blessing-status");
        footer.Add(status);

        var buyButton = new Button(() => TryPurchaseBlessing(nextBlessing)) { text = "Buy" };
        buyButton.AddToClassList("blessing-buy");
        footer.Add(buyButton);

        tile.Add(footer);
        buildingPanelContent.Add(tile);

        blessingBindings.Add(new BlessingBinding
        {
            Blessing = nextBlessing,
            Tile = tile,
            StatusLabel = status,
            BuyButton = buyButton
        });

        RefreshBlessingBindings();
    }

    private void BuildBlessingCostRow(VisualElement container, BlessingDefinition blessing)
    {
        if (container == null || blessing == null)
        {
            return;
        }

        container.Clear();
        var costs = blessing.Costs;
        if (costs == null || costs.Count == 0)
        {
            var free = new Label("Free");
            free.AddToClassList("blessing-cost-amount");
            container.Add(free);
            return;
        }

        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost.resourceType == null || cost.amount <= 0)
            {
                continue;
            }

            var item = new VisualElement();
            item.AddToClassList("blessing-cost-item");

            var icon = new VisualElement();
            icon.AddToClassList("blessing-cost-icon");
            var sprite = ResolveResourceIcon(cost.resourceType);
            if (sprite != null)
            {
                icon.style.backgroundImage = new StyleBackground(sprite);
            }
            item.Add(icon);

            var amount = new Label(NumberFormatter.Format(cost.amount));
            amount.AddToClassList("blessing-cost-amount");
            item.Add(amount);

            container.Add(item);
        }
    }

    private void RefreshBlessingBindings()
    {
        if (blessingBindings.Count == 0)
        {
            return;
        }

        var blessingManager = BlessingManager.Instance != null
            ? BlessingManager.Instance
            : Object.FindAnyObjectByType<BlessingManager>();
        var resourceManager = ResourceManager.Instance;

        for (int i = 0; i < blessingBindings.Count; i++)
        {
            var binding = blessingBindings[i];
            if (binding.Blessing == null || binding.Tile == null)
            {
                continue;
            }

            BlessingState state = GetBlessingState(blessingManager, resourceManager, binding.Blessing);
            UpdateBlessingTile(binding, state);
        }
    }

    private BlessingState GetBlessingState(BlessingManager manager, ResourceManager resourceManager, BlessingDefinition blessing)
    {
        if (manager == null || blessing == null)
        {
            return BlessingState.Locked;
        }

        if (manager.IsPurchased(blessing))
        {
            return BlessingState.Purchased;
        }

        if (!manager.ArePrerequisitesMet(blessing))
        {
            return BlessingState.Locked;
        }

        if (resourceManager == null)
        {
            return BlessingState.Available;
        }

        return manager.CanAfford(resourceManager, blessing) ? BlessingState.Ready : BlessingState.Available;
    }

    private void UpdateBlessingTile(BlessingBinding binding, BlessingState state)
    {
        if (binding.Tile == null)
        {
            return;
        }

        binding.Tile.RemoveFromClassList("blessing-locked");
        binding.Tile.RemoveFromClassList("blessing-available");
        binding.Tile.RemoveFromClassList("blessing-ready");
        binding.Tile.RemoveFromClassList("blessing-purchased");

        string statusText = "LOCKED";
        bool buttonEnabled = false;
        string buttonText = "Buy";

        switch (state)
        {
            case BlessingState.Locked:
                binding.Tile.AddToClassList("blessing-locked");
                statusText = "LOCKED";
                buttonEnabled = false;
                break;
            case BlessingState.Available:
                binding.Tile.AddToClassList("blessing-available");
                statusText = "AVAILABLE";
                buttonEnabled = false;
                break;
            case BlessingState.Ready:
                binding.Tile.AddToClassList("blessing-ready");
                statusText = "READY";
                buttonEnabled = true;
                break;
            case BlessingState.Purchased:
                binding.Tile.AddToClassList("blessing-purchased");
                statusText = "PURCHASED";
                buttonEnabled = false;
                buttonText = "Purchased";
                break;
        }

        if (binding.StatusLabel != null)
        {
            binding.StatusLabel.text = statusText;
        }

        if (binding.BuyButton != null)
        {
            binding.BuyButton.text = buttonText;
            binding.BuyButton.SetEnabled(buttonEnabled);
            if (buttonEnabled)
            {
                binding.BuyButton.RemoveFromClassList("is-disabled");
            }
            else
            {
                binding.BuyButton.AddToClassList("is-disabled");
            }
        }
    }

    private void TryPurchaseBlessing(BlessingDefinition blessing)
    {
        var blessingManager = BlessingManager.Instance != null
            ? BlessingManager.Instance
            : Object.FindAnyObjectByType<BlessingManager>();
        if (blessingManager == null)
        {
            return;
        }

        var resourceManager = ResourceManager.Instance;
        if (resourceManager == null)
        {
            return;
        }

        blessingManager.TryPurchase(resourceManager, blessing);
        RefreshBlessingBindings();
    }

    private void BuildResearchCostRow(VisualElement container, ResearchDefinition research)
    {
        if (container == null || research == null)
        {
            return;
        }

        container.Clear();
        var costs = research.Costs;
        if (costs == null || costs.Count == 0)
        {
            var free = new Label("Free");
            free.AddToClassList("research-cost-amount");
            container.Add(free);
            return;
        }

        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost.resourceType == null || cost.amount <= 0)
            {
                continue;
            }

            var item = new VisualElement();
            item.AddToClassList("research-cost-item");

            var icon = new VisualElement();
            icon.AddToClassList("research-cost-icon");
            var sprite = ResolveResourceIcon(cost.resourceType);
            if (sprite != null)
            {
                icon.style.backgroundImage = new StyleBackground(sprite);
            }
            item.Add(icon);

            var amount = new Label(NumberFormatter.Format(cost.amount));
            amount.AddToClassList("research-cost-amount");
            item.Add(amount);

            container.Add(item);
        }
    }

    private void RefreshResearchBindings()
    {
        if (researchBindings.Count == 0)
        {
            return;
        }

        var researchManager = ResearchManager.Instance != null
            ? ResearchManager.Instance
            : Object.FindAnyObjectByType<ResearchManager>();
        var resourceManager = ResourceManager.Instance;

        for (int i = 0; i < researchBindings.Count; i++)
        {
            var binding = researchBindings[i];
            if (binding.Research == null || binding.Tile == null)
            {
                continue;
            }

            ResearchState state = GetResearchState(researchManager, resourceManager, binding.Research);
            UpdateResearchTile(binding, state);
        }
    }

    private ResearchState GetResearchState(ResearchManager manager, ResourceManager resourceManager, ResearchDefinition research)
    {
        if (manager == null || research == null)
        {
            return ResearchState.Locked;
        }

        if (manager.IsPurchased(research))
        {
            return ResearchState.Purchased;
        }

        if (!manager.ArePrerequisitesMet(research))
        {
            return ResearchState.Locked;
        }

        if (resourceManager == null)
        {
            return ResearchState.Available;
        }

        return manager.CanAfford(resourceManager, research) ? ResearchState.Ready : ResearchState.Available;
    }

    private void UpdateResearchTile(ResearchBinding binding, ResearchState state)
    {
        if (binding.Tile == null)
        {
            return;
        }

        binding.Tile.RemoveFromClassList("research-locked");
        binding.Tile.RemoveFromClassList("research-available");
        binding.Tile.RemoveFromClassList("research-ready");
        binding.Tile.RemoveFromClassList("research-purchased");

        string statusText = "LOCKED";
        bool buttonEnabled = false;
        string buttonText = "Buy";

        switch (state)
        {
            case ResearchState.Locked:
                binding.Tile.AddToClassList("research-locked");
                statusText = "LOCKED";
                buttonEnabled = false;
                break;
            case ResearchState.Available:
                binding.Tile.AddToClassList("research-available");
                statusText = "AVAILABLE";
                buttonEnabled = false;
                break;
            case ResearchState.Ready:
                binding.Tile.AddToClassList("research-ready");
                statusText = "READY";
                buttonEnabled = true;
                break;
            case ResearchState.Purchased:
                binding.Tile.AddToClassList("research-purchased");
                statusText = "PURCHASED";
                buttonEnabled = false;
                buttonText = "Purchased";
                break;
        }

        if (binding.StatusLabel != null)
        {
            binding.StatusLabel.text = statusText;
        }

        if (binding.BuyButton != null)
        {
            binding.BuyButton.text = buttonText;
            binding.BuyButton.SetEnabled(buttonEnabled);
            if (buttonEnabled)
            {
                binding.BuyButton.RemoveFromClassList("is-disabled");
            }
            else
            {
                binding.BuyButton.AddToClassList("is-disabled");
            }
        }
    }

    private void TryPurchaseResearch(ResearchDefinition research)
    {
        var researchManager = ResearchManager.Instance != null
            ? ResearchManager.Instance
            : Object.FindAnyObjectByType<ResearchManager>();
        if (researchManager == null)
        {
            return;
        }

        var resourceManager = ResourceManager.Instance;
        if (resourceManager == null)
        {
            return;
        }

        researchManager.TryPurchase(resourceManager, research);
        RefreshResearchBindings();
    }

    private void RefreshMarketButtons()
    {
        var manager = ResourceManager.Instance;
        if (manager == null || goldResource == null)
        {
            return;
        }

        for (int i = 0; i < marketTradeButtons.Count; i++)
        {
            var binding = marketTradeButtons[i];
            var trade = binding.Trade;
            if (trade == null || trade.resourceType == null)
            {
                continue;
            }

            if (binding.SellButton != null)
            {
                bool canSell = manager.Get(trade.resourceType) >= trade.resourceAmount;
                binding.SellButton.SetEnabled(canSell);
                if (canSell)
                {
                    binding.SellButton.RemoveFromClassList("is-disabled");
                }
                else
                {
                    binding.SellButton.AddToClassList("is-disabled");
                }
            }

            if (binding.BuyButton != null)
            {
                bool hasGold = manager.Get(goldResource) >= trade.goldAmount;
                bool hasSpace = manager.GetAvailableStorage(trade.resourceType) >= trade.resourceAmount;
                bool canBuy = hasGold && hasSpace;
                binding.BuyButton.SetEnabled(canBuy);
                if (canBuy)
                {
                    binding.BuyButton.RemoveFromClassList("is-disabled");
                }
                else
                {
                    binding.BuyButton.AddToClassList("is-disabled");
                }
            }
        }
    }

    private void TrySellTrade(BuildingType.MarketTrade trade)
    {
        var manager = ResourceManager.Instance;
        if (manager == null || trade == null || trade.resourceType == null || goldResource == null)
        {
            return;
        }

        if (trade.resourceAmount <= 0 || trade.goldAmount <= 0)
        {
            return;
        }

        if (!manager.Spend(trade.resourceType, trade.resourceAmount))
        {
            return;
        }

        manager.Add(goldResource, trade.goldAmount);
    }

    private void TryBuyTrade(BuildingType.MarketTrade trade)
    {
        var manager = ResourceManager.Instance;
        if (manager == null || trade == null || trade.resourceType == null || goldResource == null)
        {
            return;
        }

        if (trade.resourceAmount <= 0 || trade.goldAmount <= 0)
        {
            return;
        }

        if (!manager.Spend(goldResource, trade.goldAmount))
        {
            return;
        }

        int added = manager.Add(trade.resourceType, trade.resourceAmount);
        if (added < trade.resourceAmount)
        {
            manager.Add(goldResource, trade.goldAmount);
        }
    }


    private void OnMenuButtonClicked()
    {
        if (runtimeMenuPanel == null)
        {
            return;
        }

        if (runtimeMenuPanel.style.display == DisplayStyle.Flex)
        {
            HideRuntimeMenu();
            return;
        }

        runtimeMenuPanel.style.display = DisplayStyle.Flex;
    }

    private void HideRuntimeMenu()
    {
        if (runtimeMenuPanel == null)
        {
            return;
        }

        runtimeMenuPanel.style.display = DisplayStyle.None;
    }

    private void OnWorldMapButtonClicked()
    {
        var save = SaveGameManager.Instance;
        if (save != null)
        {
            save.SaveNow();
        }

        HideRuntimeMenu();
        TryLoadWorldMapScene();
    }
    private void OnTalentsButtonClicked()
    {
        HideRuntimeMenu();
        ShowTalentsPanel();
    }

    private void ShowTalentsPanel()
    {
        if (talentsPanel == null)
        {
            return;
        }

        if (talentsTitle != null)
        {
            talentsTitle.text = string.IsNullOrWhiteSpace(talentsTitleText) ? "Talents" : talentsTitleText;
        }

        if (talentsBackground != null)
        {
            if (talentsBackgroundSprite != null)
            {
                talentsBackground.style.backgroundImage = new StyleBackground(talentsBackgroundSprite);
            }
            else
            {
                talentsBackground.style.backgroundImage = null;
                Debug.LogWarning("[HUD] Talents background sprite is not assigned.");
            }
        }

        DisableCameraPanForTalents();
        talentsPanel.style.display = DisplayStyle.Flex;
    }

    private void HideTalentsPanel()
    {
        if (talentsPanel != null)
        {
            talentsPanel.style.display = DisplayStyle.None;
        }

        RestoreCameraPanAfterTalents();
    }

    private void DisableCameraPanForTalents()
    {
        disabledCameraPanByTalents.Clear();

        var cameraPans = Object.FindObjectsByType<CameraPanController>(FindObjectsSortMode.None);
        if (cameraPans == null || cameraPans.Length == 0)
        {
            return;
        }

        for (int i = 0; i < cameraPans.Length; i++)
        {
            var cameraPan = cameraPans[i];
            if (cameraPan == null)
            {
                continue;
            }

            if (!disabledCameraPanByTalents.ContainsKey(cameraPan))
            {
                disabledCameraPanByTalents.Add(cameraPan, cameraPan.enabled);
            }

            cameraPan.enabled = false;
        }
    }

    private void RestoreCameraPanAfterTalents()
    {
        if (disabledCameraPanByTalents.Count == 0)
        {
            return;
        }

        foreach (var pair in disabledCameraPanByTalents)
        {
            if (pair.Key == null)
            {
                continue;
            }

            pair.Key.enabled = pair.Value;
        }

        disabledCameraPanByTalents.Clear();
    }

    private void OnSaveProgressButtonClicked()
    {
        var save = SaveGameManager.Instance;
        if (save == null)
        {
            Debug.LogWarning("[HUD] SaveGameManager not found. Save is unavailable.");
            return;
        }

        save.SaveNow();
        HideRuntimeMenu();
    }
    private void OnExportSaveButtonClicked()
    {
        var save = SaveGameManager.Instance;
        if (save == null)
        {
            Debug.LogWarning("[HUD] SaveGameManager not found. Export is unavailable.");
            return;
        }

        if (save.TryExportSaveForPlatform(out string exportPath, out string errorMessage))
        {
            Debug.Log($"[HUD] Save exported: {exportPath}");
            HideRuntimeMenu();
            return;
        }

        Debug.LogWarning($"[HUD] Export Save failed: {errorMessage}");
    }

    private void OnImportSaveButtonClicked()
    {
        var save = SaveGameManager.Instance;
        if (save == null)
        {
            Debug.LogWarning("[HUD] SaveGameManager not found. Import is unavailable.");
            return;
        }

        if (save.TryImportSaveForPlatform(out string importPath, out string errorMessage))
        {
            Debug.Log($"[HUD] Save imported: {importPath}");
            HideRuntimeMenu();
            return;
        }

        Debug.LogWarning($"[HUD] Import Save failed ({importPath}): {errorMessage}");
    }
    private void OnResetProgressButtonClicked()
    {
        var save = SaveGameManager.Instance;
        if (save == null)
        {
            Debug.LogWarning("[HUD] SaveGameManager not found. Reset Progress is unavailable.");
            return;
        }

        save.ResetProgressAndRestart();
    }

    private void TryLoadWorldMapScene()
    {
        if (!string.IsNullOrWhiteSpace(worldMapSceneName) && Application.CanStreamedLevelBeLoaded(worldMapSceneName))
        {
            SceneManager.LoadScene(worldMapSceneName);
            return;
        }
        string[] fallbackNames = { "WorldMap", "world_map" };
        for (int i = 0; i < fallbackNames.Length; i++)
        {
            string candidate = fallbackNames[i];
            if (Application.CanStreamedLevelBeLoaded(candidate))
            {
                SceneManager.LoadScene(candidate);
                return;
            }
        }
#if UNITY_EDITOR
        string[] editorPaths =
        {
            "Assets/Scenes/WorldMap.unity",
            "Assets/Scenes/world_map.unity"
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
        Debug.LogError("[HUD] Could not load World Map scene. Check Build Settings and worldMapSceneName.");
    }

    public bool IsInspectorVisible()
    {
        if (inspectorPanel == null)
        {
            return false;
        }

        return inspectorPanel.resolvedStyle.display != DisplayStyle.None;
    }

    private void TryAutoAssignTemplates()
    {
#if UNITY_EDITOR
        if (resourceSlotTemplate == null)
        {
            resourceSlotTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI Toolkit/ResourceSlot.uxml");
        }

        if (buildCardTemplate == null)
        {
            buildCardTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI Toolkit/BuildCard.uxml");
        }
#endif
    }

    private void TryAutoAssignTalentsBackgroundSprite()
    {
#if UNITY_EDITOR
        if (talentsBackgroundSprite != null)
        {
            return;
        }

        talentsBackgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Sprites/Background/Talent_tree.png");

        if (talentsBackgroundSprite == null)
        {
            talentsBackgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Sprites/Background/Telent_tree.png");
        }
#endif
    }

    private void TryLoadTemplatesFromResources()
    {
        if (resourceSlotTemplate == null)
        {
            resourceSlotTemplate = Resources.Load<VisualTreeAsset>("UI/ResourceSlot");
        }

        if (buildCardTemplate == null)
        {
            buildCardTemplate = Resources.Load<VisualTreeAsset>("UI/BuildCard");
        }
    }

}


































































































