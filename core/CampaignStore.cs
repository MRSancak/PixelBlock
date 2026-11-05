using System.Collections.Generic;
using Godot;
using PixelBlock.Lessons;

namespace PixelBlock.Core;

public sealed record Chapter(ILesson Lesson, string[] Requires);

public static class Campaign
{

    public static IReadOnlyList<Chapter> Chapters
    {
        get
        {
            var list = new List<Chapter>
            {
                new(LessonCatalog.Get("creation"), System.Array.Empty<string>()),
                new(LessonCatalog.Get("david"), new[] { "creation" }),
            };
            var json = new List<JsonLesson>();
            foreach (var l in LessonCatalog.List)
                if (l is JsonLesson jl && jl.IsChapter) json.Add(jl);
            json.Sort((a, b) => a.ChapterOrder != b.ChapterOrder
                ? a.ChapterOrder.CompareTo(b.ChapterOrder)
                : System.String.CompareOrdinal(a.Id, b.Id));
            foreach (var jl in json) list.Add(new Chapter(jl, jl.ChapterRequires));
            return list;
        }
    }

    public static Chapter For(string lessonId)
    {
        foreach (var c in Chapters)
            if (c.Lesson.Id == lessonId) return c;
        return null;
    }

    public static string NextAfter(string lessonId)
    {
        var chs = Chapters;
        for (int i = 0; i < chs.Count - 1; i++)
            if (chs[i].Lesson.Id == lessonId) return chs[i + 1].Lesson.Id;
        return null;
    }
}

public sealed class CampaignProgress
{
    public readonly HashSet<string> Completed = new();

    public bool IsComplete(string id) => Completed.Contains(id);

    public void MarkComplete(string id)
    {
        if (!string.IsNullOrEmpty(id)) Completed.Add(id);
    }

    public bool IsUnlocked(string id)
    {
        var ch = Campaign.For(id);
        if (ch == null) return true;
        foreach (string req in ch.Requires)
            if (!Completed.Contains(req)) return false;
        return true;
    }
}

public static class CampaignStore
{
    private const string FinalPath = "user://campaign.rprog";
    private const int Version = 1;

    public static CampaignProgress Load()
    {
        var p = new CampaignProgress();
        var dict = ReadDict(FinalPath) ?? ReadDict(FinalPath + ".bak");
        if (dict != null && dict.TryGetValue("completed", out var c))
            foreach (var v in c.AsGodotArray()) p.Completed.Add(v.AsString());
        return p;
    }

    public static void Save(CampaignProgress p)
    {
        string tmp = FinalPath + ".tmp";
        using (var f = FileAccess.Open(tmp, FileAccess.ModeFlags.Write))
        {
            if (f == null) { GD.PushError($"[Campaign] cannot write {tmp}: {FileAccess.GetOpenError()}"); return; }
            var completed = new Godot.Collections.Array();
            foreach (string id in p.Completed) completed.Add(id);
            f.StoreVar(new Godot.Collections.Dictionary { { "version", Version }, { "completed", completed } });
        }

        string absFinal = ProjectSettings.GlobalizePath(FinalPath);
        string absTmp = ProjectSettings.GlobalizePath(tmp);
        string absBak = absFinal + ".bak";
        try
        {
            if (System.IO.File.Exists(absFinal)) System.IO.File.Replace(absTmp, absFinal, absBak);
            else System.IO.File.Move(absTmp, absFinal);
        }
        catch (System.Exception e)
        {
            GD.PushWarning($"[Campaign] atomic swap failed ({e.Message}); writing directly.");
            try { System.IO.File.Copy(absTmp, absFinal, true); System.IO.File.Delete(absTmp); }
            catch (System.Exception e2) { GD.PushError($"[Campaign] direct write failed: {e2.Message}"); }
        }
    }

    public static void MarkComplete(string id)
    {
        var p = Load();
        p.MarkComplete(id);
        Save(p);
    }

    private static Godot.Collections.Dictionary ReadDict(string path)
    {
        if (!FileAccess.FileExists(path)) return null;
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null) return null;
        var dict = f.GetVar().AsGodotDictionary();
        return (dict == null || dict.Count == 0) ? null : dict;
    }

    public static void DeleteAll()
    {
        foreach (string path in new[] { FinalPath, FinalPath + ".bak" })
        {
            string abs = ProjectSettings.GlobalizePath(path);
            if (System.IO.File.Exists(abs)) System.IO.File.Delete(abs);
        }
    }
}
