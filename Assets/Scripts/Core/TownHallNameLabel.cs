using UnityEngine;
using TMPro;

public class TownHallNameLabel : MonoBehaviour
{
    [SerializeField] private TownHallCity townHallCity;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private bool autoCreateLabel = true;
    [SerializeField] private bool forceParentToTownHall = true;
    [SerializeField] private bool renderAboveSortingGroups = true;
    [SerializeField] private TMP_FontAsset defaultFont;
    [SerializeField] private float defaultFontSize = 12f;
    [SerializeField] private Color defaultColor = new Color(1f, 0.95f, 0.7f);
    [SerializeField] private bool debugLog = false;
    [SerializeField] private bool fixMirroredText = true;
    [Header("Background")]
    [SerializeField] private bool showBackground = true;
    [SerializeField] private Vector2 backgroundPadding = new Vector2(0.3f, 0.15f);
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.75f);
    [SerializeField] private int backgroundSortingOffset = -1;
    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "";
    [SerializeField] private int sortingOrder = 0;
    private SpriteRenderer backgroundRenderer;
    private static Sprite cachedSprite;
    private bool ownsLabelObject;

    private void Awake()
    {
        if (townHallCity == null)
        {
            townHallCity = GetComponentInParent<TownHallCity>();
        }

        EnsureLabel();
        ApplySorting();
    }

    private void OnEnable()
    {
        if (townHallCity != null)
        {
            townHallCity.NameChanged += HandleNameChanged;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (townHallCity != null)
        {
            townHallCity.NameChanged -= HandleNameChanged;
        }
    }

    private void LateUpdate()
    {
        if (label == null)
        {
            return;
        }

        if (CanReparentLabel() && label.transform.parent != transform)
        {
            label.transform.SetParent(transform, true);
        }

        if (renderAboveSortingGroups)
        {
            label.transform.position = transform.position + localOffset;
        }
        else
        {
            label.transform.localPosition = localOffset;
        }

        if (fixMirroredText)
        {
            ApplyMirrorFix();
        }
        UpdateBackgroundSize();
    }

    private void HandleNameChanged(string nameValue)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (label == null || townHallCity == null)
        {
            return;
        }

        label.text = townHallCity.DisplayName;
        UpdateBackgroundSize();
    }

    private void EnsureLabel()
    {
        if (!gameObject.scene.IsValid())
        {
            return;
        }

        if (label == null)
        {
            label = GetComponentInChildren<TMP_Text>(true);
        }
        else if (label.gameObject.scene != gameObject.scene)
        {
            // Assigned label points to a prefab asset, ignore it at runtime.
            label = null;
        }

        if (label == null && autoCreateLabel)
        {
            var go = new GameObject("CityNameLabel");
            if (!renderAboveSortingGroups)
            {
                go.transform.SetParent(transform, false);
                go.transform.localPosition = localOffset;
            }
            else
            {
                go.transform.position = transform.position + localOffset;
            }
            label = go.AddComponent<TextMeshPro>();
            ownsLabelObject = true;
            label.text = townHallCity != null ? townHallCity.DisplayName : "City";
            label.fontSize = Mathf.Max(0.1f, defaultFontSize);
            label.alignment = TextAlignmentOptions.Center;
            label.color = defaultColor;
            if (defaultFont != null)
            {
                label.font = defaultFont;
            }
        }

        if (label != null)
        {
            label.gameObject.SetActive(true);
            label.color = new Color(label.color.r, label.color.g, label.color.b, 1f);
            if (label.fontSize <= 0.1f)
            {
                label.fontSize = Mathf.Max(0.1f, defaultFontSize);
            }
            label.transform.localScale = Vector3.one;
            if (CanReparentLabel() && label.transform.parent != transform)
            {
                label.transform.SetParent(transform, true);
            }
            EnsureBackground();
            if (debugLog)
            {
                Debug.Log($"TownHallNameLabel: Using label '{label.name}' at {label.transform.position} fontSize={label.fontSize}");
            }
        }
    }

    private bool CanReparentLabel()
    {
        if (renderAboveSortingGroups)
        {
            return false;
        }

        if (!forceParentToTownHall)
        {
            return false;
        }

        if (!Application.isPlaying)
        {
            return false;
        }

        return gameObject.scene.IsValid();
    }

    private void OnDestroy()
    {
        if (ownsLabelObject && label != null)
        {
            Destroy(label.gameObject);
        }
    }

    private void ApplySorting()
    {
        if (label == null)
        {
            return;
        }

        var renderer = label.GetComponent<Renderer>();
        if (renderer == null)
        {
            renderer = label.GetComponentInChildren<Renderer>();
        }

        if (renderer != null)
        {
            if (!string.IsNullOrEmpty(sortingLayerName))
            {
                renderer.sortingLayerID = SortingLayer.NameToID(sortingLayerName);
            }

            renderer.sortingOrder = sortingOrder;
        }

        if (backgroundRenderer != null)
        {
            if (!string.IsNullOrEmpty(sortingLayerName))
            {
                backgroundRenderer.sortingLayerID = SortingLayer.NameToID(sortingLayerName);
            }

            backgroundRenderer.sortingOrder = sortingOrder + backgroundSortingOffset;
        }

        var canvas = label.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            if (!string.IsNullOrEmpty(sortingLayerName))
            {
                canvas.sortingLayerName = sortingLayerName;
            }
            canvas.sortingOrder = sortingOrder;
        }
    }

    private void ApplyMirrorFix()
    {
        if (renderAboveSortingGroups)
        {
            label.transform.localScale = Vector3.one;
            return;
        }

        Vector3 scale = Vector3.one;
        if (transform.lossyScale.x < 0f)
        {
            scale.x = -1f;
        }
        if (transform.lossyScale.y < 0f)
        {
            scale.y = -1f;
        }
        label.transform.localScale = scale;
    }

    private void EnsureBackground()
    {
        if (!showBackground || label == null)
        {
            if (backgroundRenderer != null)
            {
                backgroundRenderer.enabled = false;
            }
            return;
        }

        if (backgroundRenderer == null)
        {
            var go = new GameObject("CityNameBackground");
            go.transform.SetParent(label.transform, false);
            go.transform.localPosition = Vector3.zero;
            backgroundRenderer = go.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = GetWhiteSprite();
        }

        backgroundRenderer.enabled = true;
        backgroundRenderer.color = backgroundColor;
        UpdateBackgroundSize();
    }

    private void UpdateBackgroundSize()
    {
        if (backgroundRenderer == null || label == null)
        {
            return;
        }

        var text = label.text;
        if (string.IsNullOrEmpty(text))
        {
            backgroundRenderer.enabled = false;
            return;
        }

        var bounds = label.GetRenderedValues(false);
        float width = Mathf.Max(0.1f, bounds.x + backgroundPadding.x * 2f);
        float height = Mathf.Max(0.1f, bounds.y + backgroundPadding.y * 2f);
        backgroundRenderer.transform.localScale = new Vector3(width, height, 1f);
        backgroundRenderer.transform.localPosition = Vector3.zero;
    }

    private static Sprite GetWhiteSprite()
    {
        if (cachedSprite != null)
        {
            return cachedSprite;
        }

        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        cachedSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        cachedSprite.name = "TownHallNameLabelSprite";
        return cachedSprite;
    }
}
