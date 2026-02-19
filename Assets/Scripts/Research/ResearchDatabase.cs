using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ResearchDatabase", menuName = "IdleHra/Research Database")]
public class ResearchDatabase : ScriptableObject
{
    [SerializeField] private List<ResearchDefinition> research = new List<ResearchDefinition>();

    public IReadOnlyList<ResearchDefinition> Research => research;
}
