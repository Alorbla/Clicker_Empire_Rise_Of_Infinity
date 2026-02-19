using UnityEngine;

namespace IdleHra.BuildingSystem
{
    public sealed class Building : MonoBehaviour
    {
        [Header("Footprint (cells)")]
        [Min(1)]
        [SerializeField] private int width = 1;
        [Min(1)]
        [SerializeField] private int height = 1;

        private Vector3Int currentCell;
        private bool hasCell;

        public int Width => width;
        public int Height => height;
        public Vector3Int CurrentCell => currentCell;
        public bool HasGridPosition => hasCell;

        public void ApplyFootprint(Vector2Int footprint)
        {
            int newWidth = Mathf.Max(1, footprint.x);
            int newHeight = Mathf.Max(1, footprint.y);
            width = newWidth;
            height = newHeight;
        }

        public void SetGridPosition(Vector3Int cell)
        {
            currentCell = cell;
            hasCell = true;
        }

        public BoundsInt GetPlacementBounds()
        {
            // BoundsInt size must have z = 1 so tile iteration includes the cell layer.
            // A z of 0 can lead to empty bounds and no tiles being checked.
            return new BoundsInt(currentCell.x, currentCell.y, 0, width, height, 1);
        }
    }
}
