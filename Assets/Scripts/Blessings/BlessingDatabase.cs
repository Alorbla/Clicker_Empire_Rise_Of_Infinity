using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BlessingDatabase", menuName = "IdleHra/Blessing Database")]
public class BlessingDatabase : ScriptableObject
{
    [SerializeField] private List<BlessingDefinition> blessings = new List<BlessingDefinition>();

    public IReadOnlyList<BlessingDefinition> Blessings => blessings;
}
