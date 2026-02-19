using UnityEngine;

[CreateAssetMenu(fileName = "ResourceType", menuName = "IdleHra/Resource Type")]
public class ResourceType : ScriptableObject
{
    [SerializeField] private string id = "";
    [SerializeField] private string displayName = "";
    [SerializeField] private Color color = Color.white;
    [SerializeField] private Sprite icon;
    [SerializeField] private int orderIndex = 0;
    [SerializeField] private bool showInHUD = true;
    [SerializeField] private int baseStorageCapacity = 0;

    public string Id => id;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public Color Color => color;
    public Sprite Icon => icon;
    public int OrderIndex => orderIndex;
    public bool ShowInHUD => showInHUD;
    public int BaseStorageCapacity => Mathf.Max(0, baseStorageCapacity);
}