using System;
using UnityEngine;

namespace IdleHra.BuildingSystem
{
    public static class BuildingShopEvents
    {
        public static event Action<GameObject> OnRequestPlacement;

        public static void RequestPlacement(GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogWarning("BuildingShopEvents: RequestPlacement called with null prefab.");
                return;
            }

            OnRequestPlacement?.Invoke(prefab);
        }
    }
}
