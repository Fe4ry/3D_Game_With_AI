
using System;

[Serializable]
public class OllamaResponse
{
    public string model;
    public string response;
    public bool done;
}

public enum NpcAction
{
    Follow,
    Stay
}

public sealed class NpcDecision
{
    public NpcAction Action { get; }
    public string Dialogue { get; }
    public bool Persuaded { get; }

    public NpcDecision(NpcAction action, string dialogue, bool persuaded)
    {
        Action = action;
        Dialogue = dialogue;
        Persuaded = persuaded;
    }
}
