using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    public string speakerId;
    [TextArea(2, 4)]
    public string text;
    public Sprite portrait;
}
