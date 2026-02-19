using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class WorldMapVillageNameLabel : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField] private string defaultName = "Unnamed City";
    [SerializeField] private int maxCharacters = 18;
    [SerializeField] private float fontSize = 1.35f;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.08f, 0f);
    [SerializeField] private Color textColor = new Color(1f, 0.95f, 0.75f, 1f);
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.8f);
    [SerializeField] private Vector2 backgroundPadding = new Vector2(0.22f, 0.12f);
    [SerializeField] private string sortingLayerName = "";
    [SerializeField] private int sortingOrder = 12;

    private static Sprite cachedWhiteSprite;

    public void SetCityName(string cityName)
    {
        EnsureLabel();
        EnsureBackground();

        if (label == null)
        {
            return;
        }

        string sanitized = string.IsNullOrWhiteSpace(cityName) ? defaultName : cityName.Trim();
        label.text = TruncateToHexWidth(sanitized);
        label.transform.localPosition = localOffset;

        UpdateBackgroundSize();
        ApplySorting();
    }

    private string TruncateToHexWidth(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return defaultName;
        }

        int safeMax = Mathf.Max(4, maxCharacters);
        if (text.Length <= safeMax)
        {
            return text;
        }

        return text.Substring(0, safeMax - 3) + "...";
    }

    private void EnsureLabel()
    {
        if (label != null)
        {
            return;
        }

        label = GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            ConfigureLabel(label);
            return;
        }

        var go = new GameObject("VillageCityNameLabel");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localOffset;

        label = go.AddComponent<TextMeshPro>();
        ConfigureLabel(label);
    }

    private void ConfigureLabel(TMP_Text target)
    {
        if (target == null)
        {
            return;
        }

        target.text = defaultName;
        target.alignment = TextAlignmentOptions.Center;
        target.fontSize = Mathf.Max(0.1f, fontSize);
        target.color = textColor;
        target.enableWordWrapping = false;
        target.overflowMode = TextOverflowModes.Truncate;
    }

    private void EnsureBackground()
    {
        if (label == null)
        {
            return;
        }

        if (backgroundRenderer == null)
        {
            var go = new GameObject("VillageCityNameBackground");
            go.transform.SetParent(label.transform, false);
            go.transform.localPosition = Vector3.zero;

            backgroundRenderer = go.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = GetWhiteSprite();
        }

        backgroundRenderer.enabled = true;
        backgroundRenderer.color = backgroundColor;
    }

    private void UpdateBackgroundSize()
    {
        if (backgroundRenderer == null || label == null)
        {
            return;
        }

        label.ForceMeshUpdate();

        float textWidth = label.preferredWidth;
        float textHeight = label.preferredHeight;

        float width = Mathf.Max(0.22f, textWidth + backgroundPadding.x * 2f);
        float height = Mathf.Max(0.14f, textHeight + backgroundPadding.y * 2f);

        backgroundRenderer.transform.localPosition = Vector3.zero;
        backgroundRenderer.transform.localScale = new Vector3(width, height, 1f);
    }

    private void ApplySorting()
    {
        if (label == null)
        {
            return;
        }

        var labelRenderer = label.GetComponent<Renderer>();
        if (labelRenderer == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(sortingLayerName))
        {
            int sortingLayerId = SortingLayer.NameToID(sortingLayerName);
            labelRenderer.sortingLayerID = sortingLayerId;
            if (backgroundRenderer != null)
            {
                backgroundRenderer.sortingLayerID = sortingLayerId;
            }
        }

        labelRenderer.sortingOrder = sortingOrder;
        if (backgroundRenderer != null)
        {
            backgroundRenderer.sortingOrder = sortingOrder - 1;
        }
    }

    private static Sprite GetWhiteSprite()
    {
        if (cachedWhiteSprite != null)
        {
            return cachedWhiteSprite;
        }

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();

        cachedWhiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        cachedWhiteSprite.name = "WorldMapVillageNameBackground";
        return cachedWhiteSprite;
    }
}

