using UnityEngine;

public class ManualClickUpgrade : MonoBehaviour
{
    private BuildingUpgradable cachedUpgradable;

    private void Awake()
    {
        cachedUpgradable = GetComponent<BuildingUpgradable>();
        if (cachedUpgradable == null)
        {
            cachedUpgradable = GetComponentInChildren<BuildingUpgradable>();
        }

        ApplyFromBuilding();
    }

    private void Start()
    {
        ApplyFromBuilding();
    }

    public void ApplyFromBuilding()
    {
        var manager = ManualClickSystem.Instance;
        if (manager == null)
        {
            return;
        }

        if (cachedUpgradable == null || cachedUpgradable.BuildingType == null)
        {
            manager.SetCurrentClickAmount(manager.BaseClickAmount);
            return;
        }

        var levels = cachedUpgradable.BuildingType.UpgradeLevels;
        if (levels == null || levels.Count == 0)
        {
            manager.SetCurrentClickAmount(manager.BaseClickAmount);
            return;
        }

        if (cachedUpgradable.IsAtImplicitBase || cachedUpgradable.CurrentLevel < 0)
        {
            manager.SetCurrentClickAmount(manager.BaseClickAmount);
            return;
        }

        int clamped = Mathf.Clamp(cachedUpgradable.CurrentLevel, 0, levels.Count - 1);
        var level = levels[clamped];
        int targetAmount = level.manualClickAmount > 0 ? level.manualClickAmount : manager.BaseClickAmount;
        manager.SetCurrentClickAmount(targetAmount);
    }
}
