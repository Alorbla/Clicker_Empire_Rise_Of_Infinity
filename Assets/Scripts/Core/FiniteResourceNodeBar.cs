using UnityEngine;

public class FiniteResourceNodeBar : MonoBehaviour
{
    [SerializeField] private FiniteResourceNode target;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private float width = 0.5f;
    [SerializeField] private float height = 0.06f;
    [SerializeField] private float yellowThreshold = 0.4f;
    [SerializeField] private float redThreshold = 0.2f;
    [SerializeField] private Color fullColor = new Color(0.2f, 0.9f, 0.2f);
    [SerializeField] private Color midColor = new Color(0.95f, 0.8f, 0.2f);
    [SerializeField] private Color lowColor = new Color(0.95f, 0.25f, 0.25f);
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.45f);
    [SerializeField] [Range(0f, 1f)] private float barAlpha = 0.85f;
    [SerializeField] private int sortingOrderOffset = 10;
    [SerializeField] private string sortingLayerName = "";
    [SerializeField] private bool hideWhenDepleted = true;

    private Transform barRoot;
    private SpriteRenderer background;
    private SpriteRenderer fill;

    private static Sprite cachedSprite;

    private void Awake()
    {
        if (target == null)
        {
            target = GetComponentInParent<FiniteResourceNode>();
        }

        CreateBar();
    }

    private void OnEnable()
    {
        if (target != null)
        {
            target.AmountChanged += HandleAmountChanged;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (target != null)
        {
            target.AmountChanged -= HandleAmountChanged;
        }
    }

    private void HandleAmountChanged(int current, int max)
    {
        Refresh();
    }

    private void CreateBar()
    {
        if (barRoot != null)
        {
            return;
        }

        var root = new GameObject("CapacityBar");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = localOffset;
        barRoot = root.transform;

        background = CreateRenderer("BarBackground", backgroundColor, barRoot);
        fill = CreateRenderer("BarFill", fullColor, barRoot);

        ApplySortingOrder();
        ApplySizes(1f);
    }

    private SpriteRenderer CreateRenderer(string name, Color color, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = GetWhiteSprite();
        renderer.color = color;
        return renderer;
    }

    private void ApplySortingOrder()
    {
        int baseOrder = 0;
        var renderer = GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
        {
            baseOrder = renderer.sortingOrder;
        }

        if (background != null)
        {
            background.sortingOrder = baseOrder + sortingOrderOffset;
        }

        if (fill != null)
        {
            fill.sortingOrder = baseOrder + sortingOrderOffset + 1;
        }

        if (!string.IsNullOrEmpty(sortingLayerName))
        {
            if (background != null)
            {
                background.sortingLayerName = sortingLayerName;
            }

            if (fill != null)
            {
                fill.sortingLayerName = sortingLayerName;
            }
        }
    }

    private void Refresh()
    {
        if (barRoot == null || background == null || fill == null)
        {
            return;
        }

        float percent = 1f;
        if (target != null && target.MaxAmount > 0)
        {
            percent = Mathf.Clamp01((float)target.CurrentAmount / target.MaxAmount);
        }

        if (hideWhenDepleted && percent <= 0f)
        {
            background.enabled = false;
            fill.enabled = false;
            return;
        }

        background.enabled = true;
        fill.enabled = true;

        Color color = fullColor;
        if (percent <= redThreshold)
        {
            color = lowColor;
        }
        else if (percent <= yellowThreshold)
        {
            color = midColor;
        }

        fill.color = ApplyAlpha(color, barAlpha);
        background.color = ApplyAlpha(backgroundColor, barAlpha);
        ApplySizes(percent);
    }

    private void ApplySizes(float percent)
    {
        percent = Mathf.Clamp01(percent);
        if (background != null)
        {
            background.transform.localScale = new Vector3(width, height, 1f);
        }

        if (fill != null)
        {
            float fillWidth = width * percent;
            fill.transform.localScale = new Vector3(fillWidth, height, 1f);
            float xOffset = -0.5f * (width - fillWidth);
            fill.transform.localPosition = new Vector3(xOffset, 0f, 0f);
        }
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
        cachedSprite.name = "FiniteNodeBarSprite";
        return cachedSprite;
    }

    private static Color ApplyAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
