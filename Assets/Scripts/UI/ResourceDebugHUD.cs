using System.Collections.Generic;
using UnityEngine;

public class ResourceDebugHUD : MonoBehaviour
{
    [SerializeField] private List<ResourceType> trackedResources = new List<ResourceType>();
    [SerializeField] private Vector2 position = new Vector2(12f, 12f);
    [SerializeField] private Vector2 size = new Vector2(220f, 80f);
    [SerializeField] private bool showStorage = true;

    private ResourceManager manager;

    private void Awake()
    {
        manager = ResourceManager.Instance;
    }

    private void OnGUI()
    {
        if (manager == null)
        {
            manager = ResourceManager.Instance;
            if (manager == null)
            {
                return;
            }
        }

        int validCount = 0;
        for (int i = 0; i < trackedResources.Count; i++)
        {
            if (trackedResources[i] != null)
            {
                validCount++;
            }
        }

        int lineCount = validCount + (showStorage ? 1 : 0);
        float height = Mathf.Max(size.y, 28f + (lineCount * 18f));

        GUI.Box(new Rect(position.x, position.y, size.x, height), "Resources");
        float y = position.y + 24f;

        if (showStorage)
        {
            if (manager.StorageCapacity <= 0)
            {
                GUI.Label(new Rect(position.x + 10f, y, size.x - 20f, 20f), "Storage: unlimited");
            }
            else
            {
                int free = manager.GetAvailableStorage();
                string stored = NumberFormatter.Format(manager.TotalStored);
                string cap = NumberFormatter.Format(manager.StorageCapacity);
                string freeText = NumberFormatter.Format(free);
                GUI.Label(new Rect(position.x + 10f, y, size.x - 20f, 20f), $"Storage: {stored}/{cap} (free {freeText})");
            }
            y += 18f;
        }

        for (int i = 0; i < trackedResources.Count; i++)
        {
            var type = trackedResources[i];
            if (type == null)
            {
                continue;
            }

            var value = manager.Get(type);
            GUI.Label(new Rect(position.x + 10f, y, size.x - 20f, 20f), $"{type.DisplayName}: {NumberFormatter.Format(value)}");
            y += 18f;
        }
    }
}
