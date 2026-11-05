using System.Collections.Generic;
using Godot;

namespace PixelBlock.Lessons;

public interface ILesson
{
    string Id { get; }
    string Title { get; }
    string Subtitle { get; }
    Vector3 Spawn { get; }
    void Build(GameSession session);

    float? TimeOfDay => Core.EnvironmentController.Morning;

    Core.MusicMood Mood => Core.MusicMood.Calm;

    Quests.Quest BuildQuest(GameSession session) => null;
}

public static class LessonCatalog
{
    private const string Dir = "res://assets/lessons";
    private static List<ILesson> _all;

    public static IReadOnlyList<ILesson> List => _all ??= Build();

    public static ILesson Get(string id)
    {
        foreach (var l in List)
            if (l.Id == id) return l;

        GD.PushError($"[Lessons] no lesson with id '{id}'; falling back to '{(List.Count > 0 ? List[0].Id : "<none>")}'");
        return List.Count > 0 ? List[0] : null;
    }

    private static List<ILesson> Build()
    {
        var list = new List<ILesson> { new DavidAndGoliath(), new CreationGarden() };
        foreach (string path in ScanJson())
        {
            var l = JsonLesson.FromFile(path);
            if (l == null || string.IsNullOrEmpty(l.Id)) continue;
            if (list.Exists(x => x.Id == l.Id))
            { GD.PushWarning($"[Lessons] duplicate id '{l.Id}' in {path}; skipped"); continue; }
            list.Add(l);
        }
        return list;
    }

    private static List<string> ScanJson()
    {
        var paths = new List<string>();
        using var dir = DirAccess.Open(Dir);
        if (dir == null) return paths;
        dir.ListDirBegin();
        for (string f = dir.GetNext(); f != ""; f = dir.GetNext())
            if (!dir.CurrentIsDir() && f.EndsWith(".json")) paths.Add($"{Dir}/{f}");
        dir.ListDirEnd();
        paths.Sort(System.StringComparer.OrdinalIgnoreCase);
        return paths;
    }
}
