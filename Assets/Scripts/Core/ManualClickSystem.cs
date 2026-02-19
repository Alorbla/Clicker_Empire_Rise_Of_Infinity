using UnityEngine;

public class ManualClickSystem : MonoBehaviour
{
    public static ManualClickSystem Instance { get; private set; }

    [SerializeField] private int baseClickAmount = 1;
    [SerializeField] private int currentClickAmount = 1;

    public int BaseClickAmount => Mathf.Max(1, baseClickAmount);
    public int CurrentClickAmount => Mathf.Max(1, currentClickAmount);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        currentClickAmount = Mathf.Max(1, currentClickAmount);
        if (currentClickAmount < BaseClickAmount)
        {
            currentClickAmount = BaseClickAmount;
        }
    }

    public int GetClickAmount(int fallbackAmount)
    {
        var modifiers = GlobalModifiers.Instance != null
            ? GlobalModifiers.Instance
            : FindAnyObjectByType<GlobalModifiers>();
        float mult = modifiers != null ? modifiers.GetManualClickMultiplier() : 1f;

        if (currentClickAmount > 0)
        {
            return Mathf.Max(1, Mathf.RoundToInt(currentClickAmount * mult));
        }

        return Mathf.Max(1, Mathf.RoundToInt(fallbackAmount * mult));
    }

    public void SetCurrentClickAmount(int amount)
    {
        currentClickAmount = Mathf.Max(1, amount);
        if (currentClickAmount < BaseClickAmount)
        {
            currentClickAmount = BaseClickAmount;
        }
    }
}
