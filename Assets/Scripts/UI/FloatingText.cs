using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class FloatingText : MonoBehaviour
{
    [SerializeField] private float floatSpeed = 1.0f;
    [SerializeField] private float lifetime = 0.8f;
    [SerializeField] private AnimationCurve alphaOverLifetime = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    private TMP_Text label;
    private float elapsed;
    private Color baseColor = Color.white;

    private void Awake()
    {
        label = GetComponent<TMP_Text>();
    }

    public void Init(string text, Color color)
    {
        if (label == null)
        {
            label = GetComponent<TMP_Text>();
        }

        label.text = text;
        baseColor = color;
        baseColor.a = 1f;
        label.color = baseColor;
        elapsed = 0f;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        float t = lifetime <= 0f ? 1f : Mathf.Clamp01(elapsed / lifetime);
        float alpha = alphaOverLifetime != null ? alphaOverLifetime.Evaluate(t) : 1f - t;
        var c = baseColor;
        c.a = alpha;
        label.color = c;

        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}
