using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Godot;
using PixelBlock.UI;

namespace PixelBlock.Core;

public sealed class ChatCommands
{
    private readonly GameSession _s;
    private readonly ChatBox _chat;

    private static readonly Color Info = new(0.85f, 0.92f, 1f);
    private static readonly Color Ok = new(0.65f, 1f, 0.65f);
    private static readonly Color Err = new(1f, 0.55f, 0.5f);

    private static readonly string[] Names =
    {
        "yardim", "oyunmodu", "yaratici", "hayatta", "zaman", "hava", "ver", "esyalar",
        "tp", "konum", "can", "doyur", "temizle", "sohbet", "canavar", "gececanavari",
    };

    public ChatCommands(GameSession s, ChatBox chat) { _s = s; _chat = chat; }

    public static string Norm(string s)
    {
        var sb = new StringBuilder();
        foreach (char ch in s.Trim())
        {
            char c = ch switch
            {
                'ı' or 'İ' or 'I' => 'i', 'ç' or 'Ç' => 'c', 'ğ' or 'Ğ' => 'g',
                'ö' or 'Ö' => 'o', 'ş' or 'Ş' => 's', 'ü' or 'Ü' => 'u', _ => char.ToLowerInvariant(ch),
            };
            sb.Append(c);
        }
        return sb.ToString();
    }

    public string Complete(string text)
    {
        if (!text.StartsWith("/") || text.Contains(' ')) return null;
        string p = Norm(text.Substring(1));
        foreach (string n in Names) if (n.StartsWith(p)) return "/" + n + " ";
        return null;
    }

    public void Submit(string text)
    {
        if (!text.StartsWith("/"))
        {
            _chat.Print($"<{CharacterProfile.Current.PlayerName}> {text}");
            return;
        }
        var parts = text.Substring(1).Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) { Help(); return; }
        string cmd = Norm(parts[0]);
        var a = new List<string>(parts);
        a.RemoveAt(0);

        switch (cmd)
        {
            case "yardim": case "help": case "?": Help(); break;
            case "oyunmodu": case "gamemode": case "gm": GameMode(a); break;
            case "yaratici": case "creative": SetCreative(true); break;
            case "hayatta": case "survival": SetCreative(false); break;
            case "zaman": case "time": Time(a); break;
            case "hava": case "weather": WeatherCmd(a); break;
            case "ver": case "give": Give(a); break;
            case "esyalar": case "items": ListItems(a); break;
            case "tp": case "isinla": case "teleport": Tp(a); break;
            case "konum": case "pos": Pos(); break;
            case "can": case "heal": Heal(); break;
            case "doyur": case "feed": Feed(); break;
            case "temizle": case "clear": ClearInv(); break;
            case "sohbet": case "cls": _chat.ClearLog(); break;
            case "canavar": case "spawn": Monster(a); break;
            case "gececanavari": case "nightmobs": ToggleNight(a); break;
            default: Say($"Bilinmeyen komut: /{parts[0]}  (/yardim)", Err); break;
        }
    }

    private void Say(string t, Color? c = null) => _chat.Print(t, c ?? Info);

    private void Help()
    {
        Say("Komutlar:\n" +
            "/oyunmodu yaratici|hayatta   (/yaratici  /hayatta)\n" +
            "/zaman gun|ogle|aksam|gece|0-24\n" +
            "/hava acik|yagmur|kar|bulutlu|firtina|sis\n" +
            "/ver <esya> [adet]   ·   /esyalar [ara]\n" +
            "/tp <x> <y> <z>  (~ göreli)   ·   /konum\n" +
            "/canavar zombi|iskelet|orumcek   ·   /gececanavari ac|kapat\n" +
            "/can  ·  /doyur  ·  /temizle  ·  /sohbet (kutuyu temizle)\n" +
            "Tab: komut tamamlama  ·  Yukarı/Aşağı: geçmiş");
    }

    private void GameMode(List<string> a)
    {
        if (a.Count == 0) { Say("Kullanım: /oyunmodu yaratici|hayatta", Err); return; }
        string m = Norm(a[0]);
        if (m is "yaratici" or "creative" or "c" or "1") SetCreative(true);
        else if (m is "hayatta" or "survival" or "s" or "0") SetCreative(false);
        else Say("Bilinmeyen mod: " + a[0], Err);
    }

    private void SetCreative(bool on)
    {
        _s.SetCreativeMode(on);
        Say(on ? "Yaratıcı moda geçildi — Tab ile tüm bloklara ulaşırsın." : "Hayatta kalma moduna geçildi.", Ok);
    }

    private void Time(List<string> a)
    {
        if (_s.Env == null) return;
        if (a.Count > 0 && Norm(a[0]) is "set" or "ayarla") a.RemoveAt(0);
        if (a.Count == 0) { Say("Kullanım: /zaman gun|ogle|aksam|gece|0-24", Err); return; }
        string t = Norm(a[0]);
        float? v = t switch
        {
            "gun" or "day" or "sabah" => 0.30f,
            "ogle" or "noon" => 0.50f,
            "aksam" or "sunset" => 0.72f,
            "gece" or "night" => 0.92f,
            "geceyarisi" or "midnight" => 0.0f,
            _ => null,
        };
        if (v == null)
        {
            string num = a[0].Replace(':', '.').Replace(',', '.');
            if (float.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out float h) && h >= 0f && h <= 24f)
                v = h / 24f;
        }
        if (v == null) { Say("Geçersiz zaman: " + a[0], Err); return; }
        _s.Env.TimeOfDay = Mathf.PosMod(v.Value, 1f);
        Say($"Zaman ayarlandı: {_s.Env.ClockText()}", Ok);
    }

    private void WeatherCmd(List<string> a)
    {
        if (_s.Env == null) return;
        if (a.Count == 0) { Say("Kullanım: /hava acik|yagmur|kar|bulutlu|firtina|sis", Err); return; }
        Weather? w = Norm(a[0]) switch
        {
            "acik" or "clear" or "gunesli" => Weather.Clear,
            "yagmur" or "rain" => Weather.Rain,
            "kar" or "snow" => Weather.Snow,
            "bulutlu" or "cloudy" => Weather.Cloudy,
            "firtina" or "storm" => Weather.Storm,
            "sis" or "fog" => Weather.Fog,
            _ => null,
        };
        if (w == null) { Say("Bilinmeyen hava: " + a[0], Err); return; }
        _s.Env.SetWeather(w.Value);
        Say("Hava: " + WeatherDirector.NameOf(w.Value), Ok);
    }

    private static BlockType FindItem(string q)
    {
        string n = Norm(q).Replace(' ', '_');
        BlockType best = null;
        foreach (var b in BlockRegistry.All)
        {
            if (b.IsAir) continue;
            string key = Norm(b.Name);
            string disp = Norm(b.DisplayName).Replace(' ', '_');
            if (key == n || disp == n) return b;
            if (best == null && (key.Contains(n) || disp.Contains(n))) best = b;
        }
        return best;
    }

    private void Give(List<string> a)
    {
        if (_s.Inventory == null) { Say("Bu dünyada envanter yok.", Err); return; }
        if (a.Count == 0) { Say("Kullanım: /ver <esya> [adet]  (/esyalar ile ara)", Err); return; }
        int n = 1;
        var nameParts = new List<string>(a);
        if (a.Count > 1 && int.TryParse(a[^1], out int cnt)) { n = Mathf.Clamp(cnt, 1, 999); nameParts.RemoveAt(nameParts.Count - 1); }
        var b = FindItem(string.Join(" ", nameParts));
        if (b == null) { Say("Eşya bulunamadı: " + string.Join(" ", nameParts), Err); return; }
        int left = _s.Inventory.Add(b.Id, n);
        if (left > 0) _s.DropStack(b.Id, left);
        Say($"{n - left}× {b.DisplayName} verildi" + (left > 0 ? $" ({left} tanesi yere düştü)" : ""), Ok);
    }

    private void ListItems(List<string> a)
    {
        string q = a.Count > 0 ? Norm(string.Join("_", a)) : "";
        var sb = new StringBuilder();
        int n = 0;
        foreach (var b in BlockRegistry.All)
        {
            if (b.IsAir || b.IsLiquid) continue;
            if (q.Length > 0 && !Norm(b.Name).Contains(q) && !Norm(b.DisplayName).Replace(' ', '_').Contains(q)) continue;
            if (n++ > 0) sb.Append(", ");
            sb.Append(b.Name);
            if (sb.Length > 420) { sb.Append(" …"); break; }
        }
        Say(n == 0 ? "Eşleşen eşya yok." : sb.ToString());
    }

    private void Tp(List<string> a)
    {
        if (a.Count < 3) { Say("Kullanım: /tp <x> <y> <z>   (~ = bulunduğun yer)", Err); return; }
        Vector3 p = _s.Player.GlobalPosition;
        float[] v = new float[3];
        float[] cur = { p.X, p.Y, p.Z };
        for (int i = 0; i < 3; i++)
        {
            string s = a[i].Replace(',', '.');
            if (s.StartsWith("~"))
            {
                float off = 0;
                if (s.Length > 1 && !float.TryParse(s.Substring(1), NumberStyles.Float, CultureInfo.InvariantCulture, out off))
                { Say("Geçersiz sayı: " + a[i], Err); return; }
                v[i] = cur[i] + off;
            }
            else if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v[i]))
            { Say("Geçersiz sayı: " + a[i], Err); return; }
        }
        _s.Player.Velocity = Vector3.Zero;
        _s.Player.GlobalPosition = new Vector3(v[0], v[1], v[2]);
        Say($"Işınlandın: {v[0]:F0}, {v[1]:F0}, {v[2]:F0}", Ok);
    }

    private void Pos()
    {
        Vector3 p = _s.Player.GlobalPosition;
        Say($"Konum: {p.X:F1}, {p.Y:F1}, {p.Z:F1}");
    }

    private void Monster(List<string> a)
    {
        if (a.Count == 0) { Say("Kullanım: /canavar zombi|iskelet|orumcek", Err); return; }
        Combat.EnemyType t = Norm(a[0]) switch
        {
            "zombi" or "zombie" => Combat.EnemyType.Zombie(),
            "iskelet" or "skeleton" => Combat.EnemyType.Skeleton(),
            "orumcek" or "spider" => Combat.EnemyType.Spider(),
            _ => null,
        };
        if (t == null) { Say("Bilinmeyen canavar: " + a[0], Err); return; }
        Vector3 fwd = -_s.Player.GlobalTransform.Basis.Z;
        var e = _s.SpawnEnemy(t, _s.Player.GlobalPosition + fwd * 5f);
        e.DayFactor = () => _s.Env != null ? _s.Env.DayFactor : 0f;
        Say($"{t.Name} çağrıldı.", Ok);
    }

    private void ToggleNight(List<string> a)
    {
        if (a.Count == 0) { Say("Gece canavarları: " + (Combat.NightSpawner.Enabled ? "açık" : "kapalı") + "  (/gececanavari ac|kapat)"); return; }
        string m = Norm(a[0]);
        Combat.NightSpawner.Enabled = m is "ac" or "on" or "acik";
        Say("Gece canavarları " + (Combat.NightSpawner.Enabled ? "açıldı." : "kapatıldı."), Ok);
    }

    private void Heal()
    {
        _s.Player.Heal(_s.Player.MaxHealth);
        Say("Canın doldu.", Ok);
    }

    private void Feed()
    {
        _s.Player.SetHunger(_s.Player.MaxHunger);
        Say("Karnın doydu.", Ok);
    }

    private void ClearInv()
    {
        if (_s.Inventory == null) { Say("Bu dünyada envanter yok.", Err); return; }
        _s.Inventory.Clear();
        Say("Envanter temizlendi.", Ok);
    }
}
