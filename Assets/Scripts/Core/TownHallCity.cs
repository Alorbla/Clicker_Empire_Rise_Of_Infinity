using System;
using System.Collections.Generic;
using UnityEngine;

public class TownHallCity : MonoBehaviour
{
    [Serializable]
    public struct ResourceCost
    {
        public ResourceType resourceType;
        public int amount;
    }

    [SerializeField] private string defaultCityName = "Unnamed City";
    [SerializeField] private string cityName = "";
    [SerializeField] private bool allowRename = true;
    [SerializeField] private List<ResourceCost> renameCosts = new List<ResourceCost>();

    public string DisplayName => HasNamed ? cityName : defaultCityName;
    public bool HasNamed => !string.IsNullOrWhiteSpace(cityName);
    public bool AllowRename => allowRename;
    public IReadOnlyList<ResourceCost> RenameCosts => renameCosts;

    public event Action<string> NameChanged;
    private void Awake()
    {
        if (GetComponent<EraProgressionManager>() == null)
        {
            gameObject.AddComponent<EraProgressionManager>();
        }
    }


    public string SavedCityName => cityName;

    public void SetNameDirect(string savedName)
    {
        cityName = savedName == null ? string.Empty : savedName.Trim();
        NameChanged?.Invoke(cityName);
    }

    public bool CanAffordRename(ResourceManager manager)
    {
        if (!AllowRename || manager == null)
        {
            return false;
        }

        if (renameCosts == null || renameCosts.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < renameCosts.Count; i++)
        {
            var cost = renameCosts[i];
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

    public bool TrySetName(string newName, ResourceManager manager)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return false;
        }

        string trimmed = newName.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (!HasNamed)
        {
            cityName = trimmed;
            NameChanged?.Invoke(cityName);
            return true;
        }

        if (!AllowRename)
        {
            return false;
        }

        if (!CanAffordRename(manager))
        {
            return false;
        }

        SpendRenameCosts(manager);
        cityName = trimmed;
        NameChanged?.Invoke(cityName);
        return true;
    }

    private void SpendRenameCosts(ResourceManager manager)
    {
        if (manager == null || renameCosts == null)
        {
            return;
        }

        for (int i = 0; i < renameCosts.Count; i++)
        {
            var cost = renameCosts[i];
            if (cost.resourceType == null || cost.amount <= 0)
            {
                continue;
            }

            manager.Spend(cost.resourceType, cost.amount);
        }
    }
}


