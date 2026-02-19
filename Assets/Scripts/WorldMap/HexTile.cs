using UnityEngine;

[DisallowMultipleComponent]
public class HexTile : MonoBehaviour
{
    [Header("Tile Data")]
    [SerializeField] private Vector2Int gridCoord;
    [SerializeField] private Vector2Int axialCoord;
    [SerializeField] private HexBiome biome;
    [SerializeField] [Range(0f, 1f)] private float elevation;
    [SerializeField] [Range(0f, 1f)] private float moisture;
    [SerializeField] private bool isVillage;

    [Header("Selection")]
    [SerializeField] private Color highlightColor = new Color(1f, 0.9f, 0.25f, 1f);
    [SerializeField] [Range(0f, 1f)] private float highlightStrength = 0.55f;
    [SerializeField] private float colorTransitionDuration = 0.15f;

    private SpriteRenderer[] spriteRenderers;
    private Color[] baseColors;
    private Coroutine colorRoutine;
    private bool selected;

    public Vector2Int GridCoord => gridCoord;
    public Vector2Int AxialCoord => axialCoord;
    public HexBiome Biome => biome;
    public float Elevation => elevation;
    public float Moisture => moisture;
    public bool IsSelected => selected;
    public bool IsVillage => isVillage;

    public void Initialize(
        Vector2Int coord,
        Vector2Int axial,
        HexBiome tileBiome,
        float tileElevation,
        float tileMoisture,
        bool tileIsVillage = false)
    {
        gridCoord = coord;
        axialCoord = axial;
        biome = tileBiome;
        elevation = Mathf.Clamp01(tileElevation);
        moisture = Mathf.Clamp01(tileMoisture);
        isVillage = tileIsVillage;

        CacheRenderers();
        ApplyInstantSelectionColor(false);
    }

    public void SetSelected(bool isSelected)
    {
        if (selected == isSelected)
        {
            return;
        }

        selected = isSelected;

        if (colorRoutine != null)
        {
            StopCoroutine(colorRoutine);
        }

        colorRoutine = StartCoroutine(AnimateSelectionColor(selected));
    }

    private System.Collections.IEnumerator AnimateSelectionColor(bool toSelected)
    {
        CacheRenderers();

        float duration = Mathf.Max(0.001f, colorTransitionDuration);
        float elapsed = 0f;

        Color[] from = new Color[spriteRenderers.Length];
        Color[] target = new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            var renderer = spriteRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            from[i] = renderer.color;
            target[i] = GetTargetColor(i, toSelected);
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                var renderer = spriteRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.color = Color.Lerp(from[i], target[i], t);
            }

            yield return null;
        }

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            var renderer = spriteRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.color = target[i];
        }

        colorRoutine = null;
    }

    private void ApplyInstantSelectionColor(bool isSelected)
    {
        CacheRenderers();

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            var renderer = spriteRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.color = GetTargetColor(i, isSelected);
        }
    }

    private Color GetTargetColor(int index, bool isSelected)
    {
        if (index < 0 || index >= baseColors.Length)
        {
            return Color.white;
        }

        return isSelected
            ? Color.Lerp(baseColors[index], highlightColor, Mathf.Clamp01(highlightStrength))
            : baseColors[index];
    }

    private void CacheRenderers()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            baseColors = System.Array.Empty<Color>();
            return;
        }

        if (baseColors == null || baseColors.Length != spriteRenderers.Length)
        {
            baseColors = new Color[spriteRenderers.Length];
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                baseColors[i] = spriteRenderers[i] != null ? spriteRenderers[i].color : Color.white;
            }
        }
    }
}
