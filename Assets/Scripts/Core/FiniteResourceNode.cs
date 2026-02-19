using System;
using UnityEngine;

public class FiniteResourceNode : MonoBehaviour, IGameTickListener
{
    [Header("Resource")]
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private int maxAmount = 1000;
    [SerializeField] private int currentAmount = 0;
    [SerializeField] private bool initializeToMaxOnEnable = true;
    [SerializeField] private string persistentId = "";
    [Header("Regeneration")]
    [SerializeField] private bool enableRegeneration = false;
    [SerializeField] private float regenPerSecond = 0f;
    [SerializeField] private float regenTickInterval = 0.5f;
    [SerializeField] private bool destroyOnDepleted = true;
    [SerializeField] private GameObject depletionEffectPrefab;

    public int MaxAmount => Mathf.Max(0, maxAmount);
    public int CurrentAmount => Mathf.Max(0, currentAmount);
    public bool IsDepleted => CurrentAmount <= 0;
    public ResourceType ResourceType => resourceType;
    public string PersistentId => GetPersistentId();

    public event Action<FiniteResourceNode> Depleted;
    public event Action<int, int> AmountChanged;

    private static readonly System.Collections.Generic.List<FiniteResourceNode> activeNodes =
        new System.Collections.Generic.List<FiniteResourceNode>(128);

    private SpriteRenderer[] cachedRenderers;
    private Collider2D[] cachedColliders;
    private ClickableNode[] cachedClickables;
    private FiniteResourceNodeBar[] cachedBars;
    private bool hiddenByDepletion;

    public static bool TryGetNearest(ResourceType type, Vector3 position,
        out FiniteResourceNode node, out float distance)
    {
        node = null;
        distance = 0f;

        if (type == null)
        {
            return false;
        }

        float bestDistSqr = float.MaxValue;
        bool found = false;

        for (int i = activeNodes.Count - 1; i >= 0; i--)
        {
            var candidate = activeNodes[i];
            if (candidate == null)
            {
                activeNodes.RemoveAt(i);
                continue;
            }

            if (candidate.IsDepleted)
            {
                continue;
            }

            var candidateType = candidate.resourceType;
            if (candidateType == null)
            {
                continue;
            }

            if (candidateType != type)
            {
                continue;
            }

            float dSqr = (candidate.transform.position - position).sqrMagnitude;
            if (dSqr <= bestDistSqr)
            {
                bestDistSqr = dSqr;
                node = candidate;
                found = true;
            }
        }

        if (!found)
        {
            return false;
        }

        distance = Mathf.Sqrt(bestDistSqr);
        return true;
    }

    public static int CountDepleted(ResourceType type)
    {
        if (type == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = activeNodes.Count - 1; i >= 0; i--)
        {
            var candidate = activeNodes[i];
            if (candidate == null)
            {
                activeNodes.RemoveAt(i);
                continue;
            }

            if (candidate.resourceType == type && candidate.IsDepleted)
            {
                count++;
            }
        }

        return count;
    }

    public static bool TryRestoreOneDepleted(ResourceType type, int amount, out FiniteResourceNode restoredNode)
    {
        restoredNode = null;
        if (type == null)
        {
            return false;
        }

        for (int i = activeNodes.Count - 1; i >= 0; i--)
        {
            var candidate = activeNodes[i];
            if (candidate == null)
            {
                activeNodes.RemoveAt(i);
                continue;
            }

            if (candidate.resourceType != type || !candidate.IsDepleted)
            {
                continue;
            }

            int restoreAmount = amount > 0 ? amount : candidate.MaxAmount;
            if (candidate.TryRestore(restoreAmount))
            {
                restoredNode = candidate;
                return true;
            }
        }

        return false;
    }

    private void Awake()
    {
        CacheHideableComponents();
    }

    private void OnEnable()
    {
        if (resourceType == null)
        {
            var clickable = GetComponent<ClickableNode>();
            if (clickable == null)
            {
                clickable = GetComponentInParent<ClickableNode>();
            }

            if (clickable != null)
            {
                resourceType = clickable.ResourceType;
            }
        }

        CacheHideableComponents();

        bool appliedSavedState = false;
        var save = SaveGameManager.Instance != null
            ? SaveGameManager.Instance
            : FindAnyObjectByType<SaveGameManager>();
        if (save != null && save.TryGetSavedFiniteNodeAmount(PersistentId, out int savedAmount))
        {
            currentAmount = Mathf.Clamp(savedAmount, 0, MaxAmount);
            appliedSavedState = true;
        }

        if (!appliedSavedState && initializeToMaxOnEnable && currentAmount <= 0 && maxAmount > 0)
        {
            currentAmount = maxAmount;
        }

        SetHiddenState(destroyOnDepleted && currentAmount <= 0);
        NotifyAmountChanged();

        if (!activeNodes.Contains(this))
        {
            activeNodes.Add(this);
        }

        TryRegisterToTick();
    }

    private void OnDisable()
    {
        activeNodes.Remove(this);
        UnregisterFromTick();
    }

    public void Initialize(int amount)
    {
        maxAmount = Mathf.Max(0, amount);
        currentAmount = maxAmount;
        SetHiddenState(false);
        NotifyAmountChanged();
    }

    public int PeekAvailable(int requested)
    {
        if (requested <= 0)
        {
            return 0;
        }

        return Mathf.Min(CurrentAmount, requested);
    }

    public int Consume(int amount)
    {
        if (amount <= 0 || IsDepleted)
        {
            return 0;
        }

        int consumed = Mathf.Min(amount, currentAmount);
        currentAmount -= consumed;

        NotifyAmountChanged();

        if (currentAmount <= 0)
        {
            currentAmount = 0;
            HandleDepleted();
        }

        return consumed;
    }

    public bool TryRestore(int amount)
    {
        if (amount <= 0 || maxAmount <= 0)
        {
            return false;
        }

        int before = currentAmount;
        currentAmount = Mathf.Clamp(currentAmount + amount, 0, maxAmount);
        if (currentAmount <= before)
        {
            return false;
        }

        if (currentAmount > 0)
        {
            SetHiddenState(false);
        }

        NotifyAmountChanged();
        return true;
    }


    public void SetCurrentAmountFromSave(int amount)
    {
        currentAmount = Mathf.Clamp(amount, 0, MaxAmount);
        SetHiddenState(destroyOnDepleted && currentAmount <= 0);
        NotifyAmountChanged();
    }

    private string GetPersistentId()
    {
        if (!string.IsNullOrWhiteSpace(persistentId))
        {
            return persistentId.Trim();
        }

        string scenePath = gameObject.scene.path;
        if (string.IsNullOrWhiteSpace(scenePath))
        {
            scenePath = gameObject.scene.name;
        }

        string resourceId = resourceType != null && !string.IsNullOrWhiteSpace(resourceType.Id)
            ? resourceType.Id
            : (resourceType != null ? resourceType.name : "unknown");

                Vector3 p = transform.position;
        int px = Mathf.RoundToInt(p.x * 100f);
        int py = Mathf.RoundToInt(p.y * 100f);
        int pz = Mathf.RoundToInt(p.z * 100f);
        return $"{scenePath}:{resourceId}:{px}:{py}:{pz}:{gameObject.name}";
    }

    private void HandleDepleted()
    {
        if (depletionEffectPrefab != null)
        {
            Instantiate(depletionEffectPrefab, transform.position, Quaternion.identity);
        }

        Depleted?.Invoke(this);

        if (destroyOnDepleted)
        {
            regenAccumulator = 0f;
            regenRemainder = 0f;
            SetHiddenState(true);
        }
    }

    private void NotifyAmountChanged()
    {
        AmountChanged?.Invoke(CurrentAmount, MaxAmount);
    }

    public void OnGameTick(float tickDelta)
    {
        if (!enableRegeneration || regenPerSecond <= 0f || maxAmount <= 0 || (destroyOnDepleted && IsDepleted))
        {
            return;
        }

        if (currentAmount >= maxAmount)
        {
            return;
        }

        float tick = regenTickInterval > 0f ? regenTickInterval : 0f;
        if (tick <= 0f)
        {
            ApplyRegen(tickDelta * regenPerSecond);
            return;
        }

        regenAccumulator += tickDelta;
        if (regenAccumulator < tick)
        {
            return;
        }

        int steps = Mathf.FloorToInt(regenAccumulator / tick);
        regenAccumulator -= steps * tick;
        ApplyRegen(steps * tick * regenPerSecond);
    }

    private void ApplyRegen(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        float total = amount + regenRemainder;
        int add = Mathf.FloorToInt(total);
        regenRemainder = total - add;
        if (add <= 0)
        {
            return;
        }

        int before = currentAmount;
        currentAmount = Mathf.Clamp(currentAmount + add, 0, maxAmount);
        if (currentAmount != before)
        {
            if (before <= 0 && currentAmount > 0)
            {
                SetHiddenState(false);
            }

            NotifyAmountChanged();
        }
    }

    private void CacheHideableComponents()
    {
        cachedRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        cachedColliders = GetComponentsInChildren<Collider2D>(true);
        cachedClickables = GetComponentsInChildren<ClickableNode>(true);
        cachedBars = GetComponentsInChildren<FiniteResourceNodeBar>(true);
    }

    private void SetHiddenState(bool hidden)
    {
        hiddenByDepletion = hidden;

        if (cachedRenderers != null)
        {
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                var r = cachedRenderers[i];
                if (r != null)
                {
                    r.enabled = !hidden;
                }
            }
        }

        if (cachedColliders != null)
        {
            for (int i = 0; i < cachedColliders.Length; i++)
            {
                var c = cachedColliders[i];
                if (c != null)
                {
                    c.enabled = !hidden;
                }
            }
        }

        if (cachedClickables != null)
        {
            for (int i = 0; i < cachedClickables.Length; i++)
            {
                var clickable = cachedClickables[i];
                if (clickable != null)
                {
                    clickable.enabled = !hidden;
                }
            }
        }

        if (cachedBars != null)
        {
            for (int i = 0; i < cachedBars.Length; i++)
            {
                var bar = cachedBars[i];
                if (bar != null)
                {
                    bar.enabled = !hidden;
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (!destroyOnDepleted || currentAmount > 0)
        {
            return;
        }

        if (HasAnyVisibleComponentEnabled())
        {
            SetHiddenState(true);
        }
    }

    private bool HasAnyVisibleComponentEnabled()
    {
        if (cachedRenderers != null)
        {
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                var r = cachedRenderers[i];
                if (r != null && r.enabled)
                {
                    return true;
                }
            }
        }

        if (cachedColliders != null)
        {
            for (int i = 0; i < cachedColliders.Length; i++)
            {
                var c = cachedColliders[i];
                if (c != null && c.enabled)
                {
                    return true;
                }
            }
        }

        if (cachedClickables != null)
        {
            for (int i = 0; i < cachedClickables.Length; i++)
            {
                var clickable = cachedClickables[i];
                if (clickable != null && clickable.enabled)
                {
                    return true;
                }
            }
        }

        if (cachedBars != null)
        {
            for (int i = 0; i < cachedBars.Length; i++)
            {
                var bar = cachedBars[i];
                if (bar != null && bar.enabled)
                {
                    return true;
                }
            }
        }

        return false;
    }

    
    private float regenAccumulator;
    private float regenRemainder;

    private bool isRegistered;

    private void TryRegisterToTick()
    {
        if (isRegistered)
        {
            return;
        }

        var tickSystem = GameTickSystem.Instance != null
            ? GameTickSystem.Instance
            : FindAnyObjectByType<GameTickSystem>();
        if (tickSystem == null)
        {
            return;
        }

        tickSystem.Register(this);
        isRegistered = true;
    }

    private void UnregisterFromTick()
    {
        if (!isRegistered)
        {
            return;
        }

        var tickSystem = GameTickSystem.Instance != null
            ? GameTickSystem.Instance
            : FindAnyObjectByType<GameTickSystem>();
        if (tickSystem == null)
        {
            return;
        }

        tickSystem.Unregister(this);
        isRegistered = false;
    }
}












