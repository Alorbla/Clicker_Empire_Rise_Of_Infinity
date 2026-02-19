using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Dialogue Sequence", fileName = "NewDialogueSequence")]
public class DialogueSequence : ScriptableObject
{
    public string id;
    public List<DialogueLine> lines = new List<DialogueLine>();
}
