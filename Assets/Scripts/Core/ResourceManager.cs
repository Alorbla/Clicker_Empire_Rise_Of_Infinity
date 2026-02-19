using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    [Serializable]
    public class ResourceEntry
    {
        public ResourceType type;
        public int amount;
    }

    [Serializable]
    public class ResourceSnapshot
    {
        public string resourceId;
        public int amount;
    }

    public static ResourceManager Instance { get; private set; }

    [SerializeField] private List<ResourceEntry> startingResources = new List<ResourceEntry>();

    private readonly Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();
    private readonly Dictionary<string, int> storageBonusByResourceId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    private int globalStorageBonus;

    public event Action<ResourceType, int> ResourceChanged;
    // Legacy event preserved for compatibility with older UI scripts.
    public event Action<int, int> StorageChanged;

    public int StorageCapacity => GetGlobalStorageCapacityLegacy();
    public int TotalStored => GetTotalStored();
    public bool HasConfiguredStartingResources => CountConfiguredStartingEntries() > 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Never destroy whole scene objects (e.g. City_center/TownHall) because of manager duplicates.
            Destroy(this);
            return;
        }

        Instance = this;
        if (!IsBoundToTownHallHierarchy())
        {
            DontDestroyOnLoad(gameObject);
        }

        ResetToStartingResources();
    }

    private bool IsBoundToTownHallHierarchy()
    {
        if (GetComponent<TownHallCity>() != null)
        {
            return true;
        }

        if (GetComponentInParent<TownHallCity>() != null)
        {
            return true;
        }

        if (GetComponentInChildren<TownHallCity>(true) != null)
        {
            return true;
        }

        return false;
    }

    public void ResetToStartingResources()
    {
        var notifyTypes = new HashSet<ResourceType>();
        foreach (var pair in resources)
        {
            if (pair.Key != null)
            {
                notifyTypes.Add(pair.Key);
            }
        }

        resources.Clear();

        for (int i = 0; i < startingResources.Count; i++)
        {
            var entry = startingResources[i];
            if (entry == null || entry.type == null || entry.amount <= 0)
            {
                continue;
            }

            notifyTypes.Add(entry.type);
            AddInternal(entry.type, entry.amount, false);
        }

        foreach (var type in notifyTypes)
        {
            int value = Get(type);
            SyncDialogueNumber(type, value);
            ResourceChanged?.Invoke(type, value);
        }

        NotifyLegacyStorageChanged();
    }

    private int CountConfiguredStartingEntries()
    {
        int count = 0;
        for (int i = 0; i < startingResources.Count; i++)
        {
            var entry = startingResources[i];
            if (entry == null || entry.type == null || entry.amount <= 0)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    public int Get(ResourceType type)
    {
        if (type == null)
        {
            return 0;
        }

        return resources.TryGetValue(type, out var value) ? value : 0;
    }

    public int Add(ResourceType type, int amount)
    {
        return AddInternal(type, amount, true);
    }

    // Legacy global storage API preserved for older systems.
    public int GetAvailableStorage()
    {
        int capacity = GetGlobalStorageCapacityLegacy();
        if (capacity <= 0)
        {
            return int.MaxValue;
        }

        return Mathf.Max(0, capacity - GetTotalStored());
    }

    public int GetAvailableStorage(ResourceType type)
    {
        if (type == null)
        {
            return 0;
        }

        int capacity = GetCapacity(type);
        if (capacity <= 0)
        {
            return int.MaxValue;
        }

        return Mathf.Max(0, capacity - Get(type));
    }

    public int GetCapacity(ResourceType type)
    {
        if (type == null)
        {
            return 0;
        }

        int baseCapacity = Mathf.Max(0, type.BaseStorageCapacity);
        string key = GetResourceKey(type);
        storageBonusByResourceId.TryGetValue(key, out int perResourceBonus);

        if (baseCapacity <= 0 && perResourceBonus <= 0)
        {
            return 0;
        }
        int total = baseCapacity + perResourceBonus + Mathf.Max(0, globalStorageBonus);
        if (total <= 0)
        {
            return 0;
        }

        return total;
    }

    public bool IsStorageUnlimited(ResourceType type)
    {
        return GetCapacity(type) <= 0;
    }

    // Compatibility shim: existing building effects call this method.
    public void IncreaseStorageCapacity(int amount)
    {
        IncreaseAllStorageCapacity(amount);
    }

    public void IncreaseAllStorageCapacity(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        globalStorageBonus += amount;
        NotifyLegacyStorageChanged();

        foreach (var pair in resources)
        {
            if (pair.Key != null)
            {
                ResourceChanged?.Invoke(pair.Key, pair.Value);
            }
        }
    }

    public void IncreaseStorageCapacity(ResourceType type, int amount)
    {
        if (type == null || amount <= 0)
        {
            return;
        }

        string key = GetResourceKey(type);
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        if (storageBonusByResourceId.TryGetValue(key, out int existing))
        {
            storageBonusByResourceId[key] = existing + amount;
        }
        else
        {
            storageBonusByResourceId[key] = amount;
        }

        ResourceChanged?.Invoke(type, Get(type));
        NotifyLegacyStorageChanged();
    }

    public bool Spend(ResourceType type, int amount)
    {
        if (type == null || amount <= 0)
        {
            return false;
        }

        var current = Get(type);
        if (current < amount)
        {
            return false;
        }

        resources[type] = current - amount;
        SyncDialogueNumber(type, resources[type]);
        ResourceChanged?.Invoke(type, resources[type]);
        NotifyLegacyStorageChanged();
        return true;
    }

    public List<ResourceSnapshot> CaptureRuntimeState()
    {
        var snapshots = new List<ResourceSnapshot>(resources.Count);
        foreach (var pair in resources)
        {
            var type = pair.Key;
            if (type == null)
            {
                continue;
            }

            snapshots.Add(new ResourceSnapshot
            {
                resourceId = GetResourceKey(type),
                amount = Mathf.Max(0, pair.Value)
            });
        }

        return snapshots;
    }

    public void RestoreRuntimeState(List<ResourceSnapshot> snapshots)
    {
        var amountById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (snapshots != null)
        {
            for (int i = 0; i < snapshots.Count; i++)
            {
                var snapshot = snapshots[i];
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.resourceId))
                {
                    continue;
                }

                string key = snapshot.resourceId.Trim();
                amountById[key] = Mathf.Max(0, snapshot.amount);
            }
        }

        resources.Clear();

        var allTypes = Resources.FindObjectsOfTypeAll<ResourceType>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < allTypes.Length; i++)
        {
            var type = allTypes[i];
            if (type == null)
            {
                continue;
            }

            string key = GetResourceKey(type);
            if (string.IsNullOrEmpty(key) || !seenKeys.Add(key))
            {
                continue;
            }

            if (!amountById.TryGetValue(key, out int amount) || amount <= 0)
            {
                continue;
            }

            AddInternal(type, amount, false);
        }

        foreach (var pair in resources)
        {
            SyncDialogueNumber(pair.Key, pair.Value);
            ResourceChanged?.Invoke(pair.Key, pair.Value);
        }

        NotifyLegacyStorageChanged();
    }

    // Legacy overload kept so older save code compiles.
    public void RestoreRuntimeState(List<ResourceSnapshot> snapshots, int _ignoredSavedStorageCapacity)
    {
        RestoreRuntimeState(snapshots);
    }

    private int AddInternal(ResourceType type, int amount, bool invokeEvents)
    {
        if (type == null || amount <= 0)
        {
            return 0;
        }

        int addAmount = amount;
        int capacity = GetCapacity(type);
        if (capacity > 0)
        {
            int current = Get(type);
            int space = capacity - current;
            if (space <= 0)
            {
                return 0;
            }

            addAmount = Mathf.Min(addAmount, space);
        }

        if (resources.ContainsKey(type))
        {
            resources[type] += addAmount;
        }
        else
        {
            resources.Add(type, addAmount);
        }

        if (invokeEvents)
        {
            SyncDialogueNumber(type, resources[type]);
            ResourceChanged?.Invoke(type, resources[type]);
            NotifyLegacyStorageChanged();
        }

        return addAmount;
    }

    private int GetGlobalStorageCapacityLegacy()
    {
        int totalCapacity = 0;
        var allTypes = Resources.FindObjectsOfTypeAll<ResourceType>();
        if (allTypes == null || allTypes.Length == 0)
        {
            return 0;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < allTypes.Length; i++)
        {
            var type = allTypes[i];
            if (type == null)
            {
                continue;
            }

            string key = GetResourceKey(type);
            if (string.IsNullOrEmpty(key) || !seen.Add(key))
            {
                continue;
            }

            int cap = GetCapacity(type);
            if (cap <= 0)
            {
                return 0;
            }

            totalCapacity += cap;
        }

        return totalCapacity;
    }

    private int GetTotalStored()
    {
        int total = 0;
        foreach (var pair in resources)
        {
            total += Mathf.Max(0, pair.Value);
        }

        return total;
    }

    private void NotifyLegacyStorageChanged()
    {
        StorageChanged?.Invoke(GetTotalStored(), GetGlobalStorageCapacityLegacy());
    }

    private static string GetResourceKey(ResourceType type)
    {
        if (type == null)
        {
            return string.Empty;
        }

        return string.IsNullOrEmpty(type.Id) ? type.name : type.Id;
    }

    private static void SyncDialogueNumber(ResourceType type, int value)
    {
        if (type == null)
        {
            return;
        }

        var controller = DialogueController.Instance;
        if (controller == null || controller.Conditions == null)
        {
            return;
        }

        string key = string.IsNullOrEmpty(type.Id) ? type.name : type.Id;
        controller.Conditions.SetNumber(key, value);
    }
}
