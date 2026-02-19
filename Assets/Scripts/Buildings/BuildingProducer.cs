using System.Collections.Generic;
using UnityEngine;

public class BuildingProducer : MonoBehaviour, IGameTickListener
{
    [Header("Output")]
    [SerializeField] private ResourceType outputResource;
    [SerializeField] private int amountPerCycle = 1;
    [SerializeField] private float intervalSeconds = 1f;
    [SerializeField] private List<BuildingType.ResourceCost> inputResourcesPerCycle = new List<BuildingType.ResourceCost>();
    [SerializeField] private FiniteResourceNode targetNode;

    [Header("Floating Text")]
    [SerializeField] private FloatingText floatingTextPrefab;
    [SerializeField] private Vector3 floatingTextOffset = new Vector3(0f, 0.8f, 0f);
    [SerializeField] private Color floatingTextColor = Color.yellow;
    [SerializeField] private bool useResourceColorForFloatingText = true;
    [Header("Debug")]
    [SerializeField] private bool debugProximity = false;

    private bool isProductionPaused;
    private float timer;
    private bool isRegistered;
    private GameTickSystem tickSystem;
    private ResourceManager resourceManager;
    private BuildingUpgradable cachedUpgradable;
    private FiniteResourceNode cachedProximityNode;
    private float cachedProximityDistance;
    private float proximitySearchTimer;

    public ResourceType OutputResource => outputResource;
    public int AmountPerCycle => amountPerCycle;
    public float IntervalSeconds => intervalSeconds;
    public IReadOnlyList<BuildingType.ResourceCost> InputResourcesPerCycle => inputResourcesPerCycle;
    public BuildingType BuildingType => cachedUpgradable != null ? cachedUpgradable.BuildingType : null;
    public bool IsProductionPaused => isProductionPaused;

    public void SetOutput(int amount, float interval)
    {
        SetOutput(amount, interval, null);
    }

    public void SetOutput(int amount, float interval, IReadOnlyList<BuildingType.ResourceCost> inputs)
    {
        amountPerCycle = amount;
        intervalSeconds = interval;
        SetInputResources(inputs);
    }

    public void PauseProduction()
    {
        isProductionPaused = true;
    }

    public void ResumeProduction()
    {
        isProductionPaused = false;
    }

    public void SetProductionPaused(bool paused)
    {
        isProductionPaused = paused;
    }

    private void SetInputResources(IReadOnlyList<BuildingType.ResourceCost> inputs)
    {
        if (inputResourcesPerCycle == null)
        {
            inputResourcesPerCycle = new List<BuildingType.ResourceCost>();
        }
        else
        {
            inputResourcesPerCycle.Clear();
        }

        if (inputs == null)
        {
            return;
        }

        for (int i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            if (input.resourceType == null || input.amount <= 0)
            {
                continue;
            }

            inputResourcesPerCycle.Add(new BuildingType.ResourceCost
            {
                resourceType = input.resourceType,
                amount = input.amount
            });
        }
    }

    private void OnEnable()
    {
        TryRegister();
    }

    private void Start()
    {
        if (!isRegistered)
        {
            TryRegister();
        }
    }

    private void OnDisable()
    {
        Unregister();
    }

    private void OnDestroy()
    {
        Unregister();
    }

    public void OnGameTick(float tickDelta)
    {
        if (isProductionPaused)
        {
            return;
        }

        if (outputResource == null || amountPerCycle <= 0)
        {
            return;
        }

        if (intervalSeconds <= 0f)
        {
            ProduceCycles(1);
            return;
        }

        timer += tickDelta;
        int cycles = Mathf.FloorToInt(timer / intervalSeconds);
        if (cycles <= 0)
        {
            return;
        }

        timer -= cycles * intervalSeconds;
        ProduceCycles(cycles);
    }

    private void TryRegister()
    {
        tickSystem = GameTickSystem.Instance != null
            ? GameTickSystem.Instance
            : Object.FindAnyObjectByType<GameTickSystem>();
        if (tickSystem == null)
        {
            Debug.LogWarning($"BuildingProducer on {name}: GameTickSystem not found.");
            return;
        }

        tickSystem.Register(this);
        isRegistered = true;
    }

    private void Unregister()
    {
        if (!isRegistered)
        {
            return;
        }

        if (tickSystem == null)
        {
            tickSystem = GameTickSystem.Instance;
        }

        if (tickSystem != null)
        {
            tickSystem.Unregister(this);
        }

        isRegistered = false;
    }

    private void ProduceCycles(int cycles)
    {
        if (cycles <= 0)
        {
            return;
        }

        if (resourceManager == null)
        {
            resourceManager = ResourceManager.Instance != null
                ? ResourceManager.Instance
                : Object.FindAnyObjectByType<ResourceManager>();
            if (resourceManager == null)
            {
                Debug.LogWarning($"BuildingProducer on {name}: ResourceManager not found.");
                return;
            }
        }

        if (cachedUpgradable == null)
        {
            cachedUpgradable = GetComponent<BuildingUpgradable>();
            if (cachedUpgradable == null)
            {
                cachedUpgradable = GetComponentInParent<BuildingUpgradable>();
            }
        }

        var buildingType = cachedUpgradable != null ? cachedUpgradable.BuildingType : null;
        float proximityBonus = 0f;
        var effectiveNode = targetNode;

        if (buildingType != null && buildingType.EnableProximityBonus && buildingType.ProximityResource != null)
        {
            UpdateProximityTarget(buildingType);
            if (cachedProximityNode != null)
            {
                effectiveNode = cachedProximityNode;
                proximityBonus = CalculateProximityBonus(buildingType, cachedProximityDistance);
            }
            else
            {
                effectiveNode = null;
            }
        }

        if (buildingType != null && buildingType.EnableProximityBonus && effectiveNode == null)
        {
            if (debugProximity)
            {
                string resName = buildingType != null && buildingType.ProximityResource != null
                    ? buildingType.ProximityResource.name
                    : "(none)";
                Debug.Log($"BuildingProducer on {name}: No nearby node found for {resName}.");
            }
            return;
        }

        var modifiers = GlobalModifiers.Instance != null
            ? GlobalModifiers.Instance
            : FindAnyObjectByType<GlobalModifiers>();

        int totalAdded = 0;

        for (int i = 0; i < cycles; i++)
        {
            int cycleAmount = amountPerCycle;
            if (cycleAmount <= 0)
            {
                continue;
            }

            if (effectiveNode != null)
            {
                cycleAmount = effectiveNode.PeekAvailable(cycleAmount);
                if (cycleAmount <= 0)
                {
                    if (debugProximity)
                    {
                        Debug.Log($"BuildingProducer on {name}: Target node depleted or zero available.");
                    }
                    break;
                }
            }

            if (modifiers != null)
            {
                float mult = modifiers.GetProductionMultiplier(outputResource, buildingType);
                if (proximityBonus > 0f)
                {
                    mult *= 1f + proximityBonus;
                }

                cycleAmount = Mathf.RoundToInt(cycleAmount * mult);
            }

            if (cycleAmount <= 0)
            {
                continue;
            }

            if (resourceManager.GetAvailableStorage(outputResource) < cycleAmount)
            {
                // Block this cycle when there is not enough storage for the full output.
                continue;
            }

            if (!CanAffordInputCosts(resourceManager, inputResourcesPerCycle))
            {
                continue;
            }

            SpendInputCosts(resourceManager, inputResourcesPerCycle);

            int added = resourceManager.Add(outputResource, cycleAmount);
            if (added <= 0)
            {
                continue;
            }

            totalAdded += added;

            if (effectiveNode != null)
            {
                effectiveNode.Consume(added);
            }
        }

        if (totalAdded <= 0)
        {
            return;
        }

        if (floatingTextPrefab != null)
        {
            Vector3 position = transform.position + floatingTextOffset;
            FloatingText instance = Instantiate(floatingTextPrefab, position, Quaternion.identity);
            Color color = floatingTextColor;
            if (useResourceColorForFloatingText && outputResource != null)
            {
                color = outputResource.Color;
            }
            instance.Init($"+{NumberFormatter.Format(totalAdded)}", color);
        }
    }

    private static bool CanAffordInputCosts(ResourceManager manager, IReadOnlyList<BuildingType.ResourceCost> costs)
    {
        if (manager == null || costs == null)
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

    private static void SpendInputCosts(ResourceManager manager, IReadOnlyList<BuildingType.ResourceCost> costs)
    {
        if (manager == null || costs == null)
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

    public bool TryGetProximityInfo(out ResourceType resource, out float distance, out float bonusPercent, out bool hasTarget)
    {
        resource = null;
        distance = 0f;
        bonusPercent = 0f;
        hasTarget = false;

        if (cachedUpgradable == null)
        {
            cachedUpgradable = GetComponent<BuildingUpgradable>();
            if (cachedUpgradable == null)
            {
                cachedUpgradable = GetComponentInParent<BuildingUpgradable>();
            }
        }

        var type = cachedUpgradable != null ? cachedUpgradable.BuildingType : null;
        if (type == null || !type.EnableProximityBonus || type.ProximityResource == null)
        {
            return false;
        }

        UpdateProximityTarget(type);

        resource = type.ProximityResource;
        if (cachedProximityNode != null)
        {
            hasTarget = true;
            distance = cachedProximityDistance;
            if (type.EnableProximityBonus)
            {
                bonusPercent = CalculateProximityBonus(type, distance);
            }
        }

        return true;
    }

    private void UpdateProximityTarget(BuildingType type)
    {
        if (type == null)
        {
            cachedProximityNode = null;
            cachedProximityDistance = 0f;
            return;
        }

        float interval = Mathf.Max(0f, type.ProximitySearchInterval);
        proximitySearchTimer -= Time.deltaTime;
        if (proximitySearchTimer > 0f && cachedProximityNode != null && !cachedProximityNode.IsDepleted)
        {
            float currentDist = Vector3.Distance(transform.position, cachedProximityNode.transform.position);
            cachedProximityDistance = currentDist;
            return;
        }

        proximitySearchTimer = interval;

        if (FiniteResourceNode.TryGetNearest(type.ProximityResource, transform.position,
                out var nearest, out var distance))
        {
            cachedProximityNode = nearest;
            cachedProximityDistance = distance;
        }
        else
        {
            cachedProximityNode = null;
            cachedProximityDistance = 0f;
        }
    }

    private static float CalculateProximityBonus(BuildingType type, float distance)
    {
        if (type == null)
        {
            return 0f;
        }

        float maxBonus = Mathf.Max(0f, type.ProximityMaxBonusPercent);
        if (maxBonus <= 0f)
        {
            return 0f;
        }

        float start = Mathf.Max(0f, type.ProximityBonusStartDistance);
        float maxDist = Mathf.Max(0f, type.ProximityBonusMaxDistance);

        if (start <= maxDist)
        {
            start = maxDist + 0.01f;
        }

        if (distance >= start)
        {
            return 0f;
        }

        if (distance <= maxDist)
        {
            return maxBonus;
        }

        float t = Mathf.Clamp01((distance - maxDist) / (start - maxDist));
        return Mathf.Lerp(maxBonus, 0f, t);
    }
}

