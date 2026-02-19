using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Tutorial Sequence", fileName = "NewTutorialSequence")]
public class TutorialSequence : ScriptableObject
{
    public string id;
    public List<DialogueStep> steps = new List<DialogueStep>();
}
