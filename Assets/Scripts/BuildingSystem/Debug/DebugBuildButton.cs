using UnityEngine;

namespace IdleHra.BuildingSystem
{
    public sealed class DebugBuildButton : MonoBehaviour
    {
        [SerializeField] private GridBuildingSystem gridBuildingSystem;
        [SerializeField] private GameObject housePrefab;

        public void StartPlaceHouse()
        {
            if (housePrefab == null)
            {
                Debug.LogWarning("DebugBuildButton: housePrefab is not assigned.");
                return;
            }

            if (gridBuildingSystem == null)
            {
                gridBuildingSystem = Object.FindAnyObjectByType<GridBuildingSystem>();
            }

            if (gridBuildingSystem == null)
            {
                Debug.LogWarning("DebugBuildButton: GridBuildingSystem not found in scene.");
                return;
            }

            gridBuildingSystem.InitializeWithBuilding(housePrefab);
        }
    }
}
