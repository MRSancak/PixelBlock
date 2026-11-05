namespace PixelBlock.Quests;

public enum ObjectiveKind { Talk, Defeat, Reach, Break, Place, Collect }

public sealed record Objective(
    ObjectiveKind Kind,
    string Label,
    string Key = null,
    int Count = 1,
    System.Action<GameSession> OnComplete = null)
{

    public bool Optional { get; init; }

    public bool Counted => Count > 1 || (Kind == ObjectiveKind.Talk && Key == null);
}

public sealed class Quest
{
    public required System.Collections.Generic.IReadOnlyList<Objective> Objectives { get; init; }

    public System.Action<GameSession> OnComplete { get; init; }

    public static Objective Talk(string npcName, string label, System.Action<GameSession> onComplete = null, bool optional = false)
        => new(ObjectiveKind.Talk, label, npcName, 1, onComplete) { Optional = optional };

    public static Objective TalkAny(int count, string label, System.Action<GameSession> onComplete = null, bool optional = false)
        => new(ObjectiveKind.Talk, label, null, count, onComplete) { Optional = optional };

    public static Objective Defeat(string enemyTypeName, string label, System.Action<GameSession> onComplete = null, bool optional = false)
        => new(ObjectiveKind.Defeat, label, enemyTypeName, 1, onComplete) { Optional = optional };

    public static Objective Reach(string triggerId, string label, System.Action<GameSession> onComplete = null, bool optional = false)
        => new(ObjectiveKind.Reach, label, triggerId, 1, onComplete) { Optional = optional };

    public static Objective Break(string block, int count, string label, System.Action<GameSession> onComplete = null, bool optional = false)
        => new(ObjectiveKind.Break, label, block, count, onComplete) { Optional = optional };

    public static Objective Place(string block, int count, string label, System.Action<GameSession> onComplete = null, bool optional = false)
        => new(ObjectiveKind.Place, label, block, count, onComplete) { Optional = optional };

    public static Objective Collect(string block, int count, string label, System.Action<GameSession> onComplete = null, bool optional = false)
        => new(ObjectiveKind.Collect, label, block, count, onComplete) { Optional = optional };
}
