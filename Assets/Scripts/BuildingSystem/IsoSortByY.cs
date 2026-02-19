using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IdleHra.BuildingSystem
{
    public sealed class IsoSortByY : MonoBehaviour
    {
        [Header("Sorting")]
        [SerializeField] private int sortingFactor = 100;
        [SerializeField] private int baseSortingOrder = 0;
        [SerializeField] private bool updateEveryFrame = false;
        [SerializeField] private bool preferSortingGroup = true;
        [SerializeField] private bool includeInactiveRenderers = true;
        [SerializeField] private bool useColliderBottom = false;
        [SerializeField] private Vector3 sortingOffset = Vector3.zero;

        private readonly List<SpriteRenderer> cachedRenderers = new List<SpriteRenderer>(8);
        private SortingGroup cachedGroup;

        private void Awake()
        {
            CacheRenderers();
            ApplySortingNow();
        }

        private void LateUpdate()
        {
            if (updateEveryFrame)
            {
                ApplySortingNow();
            }
        }

        public void SetUpdateEveryFrame(bool value)
        {
            updateEveryFrame = value;
        }

        public void SetPreferSortingGroup(bool value)
        {
            preferSortingGroup = value;
            ApplySortingNow();
        }
        public void Configure(bool useColliderBottomValue, Vector3 offset, int factor, int baseOrder)
        {
            useColliderBottom = useColliderBottomValue;
            sortingOffset = offset;
            sortingFactor = factor;
            baseSortingOrder = baseOrder;
            ApplySortingNow();
        }

        public void ApplySortingNow()
        {
            float sortY = transform.position.y;
            if (useColliderBottom)
            {
                var collider = GetComponent<Collider2D>();
                if (collider == null)
                {
                    collider = GetComponentInChildren<Collider2D>();
                }

                if (collider != null)
                {
                    sortY = collider.bounds.min.y;
                }
            }
            else
            {
                if (cachedRenderers.Count == 0)
                {
                    CacheRenderers();
                }

                float minY = float.MaxValue;
                bool hasRenderer = false;
                for (int i = 0; i < cachedRenderers.Count; i++)
                {
                    var renderer = cachedRenderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    minY = Mathf.Min(minY, renderer.bounds.min.y);
                    hasRenderer = true;
                }

                if (hasRenderer)
                {
                    sortY = minY;
                }
            }

            sortY += sortingOffset.y;

            int order = baseSortingOrder + Mathf.RoundToInt(-sortY * sortingFactor);

            if (preferSortingGroup)
            {
                if (cachedGroup == null)
                {
                    cachedGroup = GetComponent<SortingGroup>();
                }

                if (cachedGroup != null)
                {
                    cachedGroup.sortingOrder = order;
                    return;
                }
            }

            if (cachedRenderers.Count == 0)
            {
                CacheRenderers();
            }

            for (int i = 0; i < cachedRenderers.Count; i++)
            {
                var renderer = cachedRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.sortingOrder = order;
            }
        }

        private void CacheRenderers()
        {
            cachedRenderers.Clear();
            GetComponentsInChildren(includeInactiveRenderers, cachedRenderers);
            cachedGroup = GetComponent<SortingGroup>();
        }
    }
}



