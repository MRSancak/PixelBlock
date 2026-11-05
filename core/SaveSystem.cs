using System.Collections.Generic;
using Godot;

namespace PixelBlock.Core;

public sealed class SaveData
{
    public string Name = "World";
    public int Seed;
    public long SavedUnix;
    public Vector3 PlayerPos;
    public float TimeOfDay = 0.4f;
    public readonly Dictionary<string, int> Inventory = new();
    public readonly List<(int x, int y, int z, string block)> Edits = new();
    public readonly List<(Vector3 pos, string text)> Signposts = new();
    public readonly List<(string name, Vector3 pos)> Waypoints = new();

    public int DayCount;
    public readonly List<(int x, int y, int z, string kind, string inv, float burn, float burnMax, float prog)> Containers = new();
    public float Hunger = -1f;
    public readonly string[] Armor = { "", "", "", "" };

    public readonly List<(string name, int count)> Slots = new();
    public readonly List<(string key, Vector3 soil, float progress, bool fert)> Crops = new();
    public readonly List<(string key, Vector3 pos, float age, bool owned)> Animals = new();

    public readonly Dictionary<string, string> PeerInv = new();
}

public static class SaveSystem
{
    private const string Dir = "user://saves";
    private const int Version = 1;

    private static string Sanitize(string name)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in name)
            sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
        string s = sb.ToString().Trim('_');
        return string.IsNullOrEmpty(s) ? "world" : s;
    }

    public static string PathFor(string name) => $"{Dir}/{Sanitize(name)}.rsave";
    public static bool Exists(string name) => FileAccess.FileExists(PathFor(name));

    public static void Save(SaveData d)
    {
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Dir));
        string finalPath = PathFor(d.Name);
        string tmpPath = finalPath + ".tmp";

        using (var f = FileAccess.Open(tmpPath, FileAccess.ModeFlags.Write))
        {
            if (f == null) { GD.PushError($"[Save] cannot write {tmpPath}: {FileAccess.GetOpenError()}"); return; }

            var inv = new Godot.Collections.Dictionary();
            foreach (var (block, count) in d.Inventory) inv[block] = count;

            var edits = new Godot.Collections.Array();
            foreach (var (x, y, z, block) in d.Edits)
                edits.Add(new Godot.Collections.Array { x, y, z, block });

            var signs = new Godot.Collections.Array();
            foreach (var (pos, text) in d.Signposts)
                signs.Add(new Godot.Collections.Array { pos, text });

            var waypoints = new Godot.Collections.Array();
            foreach (var (name, pos) in d.Waypoints)
                waypoints.Add(new Godot.Collections.Array { name, pos });

            var slots = new Godot.Collections.Array();
            foreach (var (sname, scount) in d.Slots) slots.Add(new Godot.Collections.Array { sname, scount });
            var crops = new Godot.Collections.Array();
            foreach (var (ck, cs, cp, cf) in d.Crops) crops.Add(new Godot.Collections.Array { ck, cs, cp, cf });
            var animals = new Godot.Collections.Array();
            foreach (var (ak, ap, aa, ao) in d.Animals) animals.Add(new Godot.Collections.Array { ak, ap, aa, ao });

            var conts = new Godot.Collections.Array();
            foreach (var (cx, cy, cz, ck, ci, cb, cbm, cp) in d.Containers)
                conts.Add(new Godot.Collections.Array { cx, cy, cz, ck, ci, cb, cbm, cp });

            var peerInv = new Godot.Collections.Dictionary();
            foreach (var kv in d.PeerInv) peerInv[kv.Key] = kv.Value;

            var dict = new Godot.Collections.Dictionary
            {
                { "peerinv", peerInv },
                { "day", d.DayCount },
                { "hunger", d.Hunger },
                { "containers", conts },
                { "armor", new Godot.Collections.Array { d.Armor[0], d.Armor[1], d.Armor[2], d.Armor[3] } },
                { "slots", slots },
                { "crops", crops },
                { "animals", animals },
                { "version", Version },
                { "name", d.Name },
                { "seed", d.Seed },
                { "saved", d.SavedUnix },
                { "player", d.PlayerPos },
                { "time", d.TimeOfDay },
                { "inventory", inv },
                { "edits", edits },
                { "signposts", signs },
                { "waypoints", waypoints },
            };
            f.StoreVar(dict);
        }

        string absFinal = ProjectSettings.GlobalizePath(finalPath);
        string absTmp = ProjectSettings.GlobalizePath(tmpPath);
        string absBak = absFinal + ".bak";
        try
        {
            if (System.IO.File.Exists(absFinal))
                System.IO.File.Replace(absTmp, absFinal, absBak);
            else
                System.IO.File.Move(absTmp, absFinal);
        }
        catch (System.Exception e)
        {

            GD.PushWarning($"[Save] atomic swap failed ({e.Message}); writing directly.");
            try { System.IO.File.Copy(absTmp, absFinal, true); System.IO.File.Delete(absTmp); }
            catch (System.Exception e2) { GD.PushError($"[Save] direct write failed: {e2.Message}"); }
        }
        GD.Print($"[Save] wrote '{d.Name}' (seed {d.Seed}, {d.Edits.Count} edits)");
    }

    public static SaveData Load(string name)
    {
        string path = PathFor(name);
        var d = LoadFrom(path, name);
        if (d != null) return d;

        var bak = LoadFrom(path + ".bak", name);
        if (bak != null) GD.Print($"[Save] '{name}' main file unreadable; recovered from .bak");
        return bak;
    }

    private static SaveData LoadFrom(string path, string fallbackName)
    {
        if (!FileAccess.FileExists(path)) return null;
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null) return null;
        var dict = f.GetVar().AsGodotDictionary();
        if (dict == null || dict.Count == 0) return null;
        string name = fallbackName;

        var d = new SaveData
        {
            Name = dict.TryGetValue("name", out var n) ? n.AsString() : name,
            Seed = dict.TryGetValue("seed", out var s) ? s.AsInt32() : 0,
            SavedUnix = dict.TryGetValue("saved", out var sv) ? sv.AsInt64() : 0,
            PlayerPos = dict.TryGetValue("player", out var p) ? p.AsVector3() : Vector3.Zero,
            TimeOfDay = dict.TryGetValue("time", out var t) ? t.AsSingle() : 0.4f,
        };
        if (dict.TryGetValue("inventory", out var invV))
            foreach (var kv in invV.AsGodotDictionary())
                d.Inventory[kv.Key.AsString()] = kv.Value.AsInt32();
        if (dict.TryGetValue("edits", out var editsV))
            foreach (var e in editsV.AsGodotArray())
            {
                var a = e.AsGodotArray();
                if (a.Count >= 4)
                    d.Edits.Add((a[0].AsInt32(), a[1].AsInt32(), a[2].AsInt32(), a[3].AsString()));
            }
        if (dict.TryGetValue("signposts", out var signsV))
            foreach (var e in signsV.AsGodotArray())
            {
                var a = e.AsGodotArray();
                if (a.Count >= 2) d.Signposts.Add((a[0].AsVector3(), a[1].AsString()));
            }
        if (dict.TryGetValue("peerinv", out var piV))
            foreach (var kv in piV.AsGodotDictionary())
                d.PeerInv[kv.Key.AsString()] = kv.Value.AsString();
        if (dict.TryGetValue("day", out var dayV)) d.DayCount = dayV.AsInt32();
        if (dict.TryGetValue("containers", out var coV))
            foreach (var e in coV.AsGodotArray())
            {
                var a = e.AsGodotArray();
                if (a.Count >= 8)
                    d.Containers.Add((a[0].AsInt32(), a[1].AsInt32(), a[2].AsInt32(), a[3].AsString(), a[4].AsString(),
                        a[5].AsSingle(), a[6].AsSingle(), a[7].AsSingle()));
            }
        if (dict.TryGetValue("hunger", out var hgV)) d.Hunger = hgV.AsSingle();
        if (dict.TryGetValue("armor", out var arV))
        {
            var arr = arV.AsGodotArray();
            for (int i = 0; i < 4 && i < arr.Count; i++) d.Armor[i] = arr[i].AsString();
        }
        if (dict.TryGetValue("slots", out var slotsV))
            foreach (var e in slotsV.AsGodotArray())
            {
                var a = e.AsGodotArray();
                if (a.Count >= 2) d.Slots.Add((a[0].AsString(), a[1].AsInt32()));
            }
        if (dict.TryGetValue("crops", out var cropsV))
            foreach (var e in cropsV.AsGodotArray())
            {
                var a = e.AsGodotArray();
                if (a.Count >= 4) d.Crops.Add((a[0].AsString(), a[1].AsVector3(), a[2].AsSingle(), a[3].AsBool()));
            }
        if (dict.TryGetValue("animals", out var animalsV))
            foreach (var e in animalsV.AsGodotArray())
            {
                var a = e.AsGodotArray();
                if (a.Count >= 4) d.Animals.Add((a[0].AsString(), a[1].AsVector3(), a[2].AsSingle(), a[3].AsBool()));
            }
        if (dict.TryGetValue("waypoints", out var wpV))
            foreach (var e in wpV.AsGodotArray())
            {
                var a = e.AsGodotArray();
                if (a.Count >= 2) d.Waypoints.Add((a[0].AsString(), a[1].AsVector3()));
            }
        return d;
    }

    public static List<SaveData> List()
    {
        var list = new List<SaveData>();
        if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(Dir))) return list;
        using var dir = DirAccess.Open(Dir);
        if (dir == null) return list;
        foreach (string file in dir.GetFiles())
        {
            if (!file.EndsWith(".rsave")) continue;
            var d = Load(file.Substring(0, file.Length - ".rsave".Length));
            if (d != null) list.Add(d);
        }
        list.Sort((a, b) => b.SavedUnix.CompareTo(a.SavedUnix));
        return list;
    }

    public static void Delete(string name)
    {
        string abs = ProjectSettings.GlobalizePath(PathFor(name));
        if (System.IO.File.Exists(abs)) System.IO.File.Delete(abs);
    }
}
