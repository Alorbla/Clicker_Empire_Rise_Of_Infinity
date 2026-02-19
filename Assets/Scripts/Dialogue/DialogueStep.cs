using System;

[Serializable]
public struct DialogueStep
{
    public DialogueLine line;
    public CompletionMode completionMode;
    public string conditionExpressionOrKey;
    public string eventId;
    public bool lockInput;

    public enum CompletionMode
    {
        OnNext,
        OnCondition,
        OnEvent
    }
}
