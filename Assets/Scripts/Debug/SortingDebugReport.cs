using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

public class SortingDebugReport : MonoBehaviour
{
    [SerializeField] private bool runOnStart = true;
    [SerializeField] private string filterNameContains = "";

    private void Start()
    {
        if (runOnStart)
        {
            Dump();
        }
    }

    [ContextMenu("Dump Sorting Report")]
    public void Dump()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Sorting Debug Report ===");

        var groups = FindObjectsOfType<SortingGroup>(true);
        sb.AppendLine($"SortingGroups: {groups.Length}");
        for (int i = 0; i < groups.Length; i++)
        {
            var g = groups[i];
            if (!MatchesFilter(g.gameObject))
            {
                continue;
            }

            sb.AppendLine($"[Group] {GetPath(g.transform)} layer={g.sortingLayerName} order={g.sortingOrder}");
        }

        var renderers = FindObjectsOfType<SpriteRenderer>(true);
        sb.AppendLine($"SpriteRenderers: {renderers.Length}");
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (!MatchesFilter(r.gameObject))
            {
                continue;
            }

            var parentGroup = r.GetComponentInParent<SortingGroup>();
            string groupInfo = parentGroup != null
                ? $" group=({parentGroup.sortingLayerName},{parentGroup.sortingOrder})"
                : "";
            sb.AppendLine($"[Renderer] {GetPath(r.transform)} layer={r.sortingLayerName} order={r.sortingOrder}{groupInfo}");
        }

        Debug.Log(sb.ToString());
    }

    private bool MatchesFilter(GameObject obj)
    {
        if (string.IsNullOrEmpty(filterNameContains))
        {
            return true;
        }

        return obj.name.IndexOf(filterNameContains, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetPath(Transform t)
    {
        if (t == null) return "(null)";
        var sb = new StringBuilder(t.name);
        while (t.parent != null)
        {
            t = t.parent;
            sb.Insert(0, t.name + "/");
        }
        return sb.ToString();
    }
}
