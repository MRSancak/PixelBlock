using Godot;

namespace PixelBlock.Core;

public sealed class CharacterProfile
{
    public static CharacterProfile Current = new();
    private const string Path = "user://character.cfg";

    public string PlayerName = "Oyuncu";
    public Color Skin = new(0.86f, 0.52f, 0.34f);
    public Color Hair = new(0.30f, 0.72f, 0.62f);
    public Color Eye = new(0.10f, 0.20f, 0.26f);
    public Color Shirt = new(0.66f, 0.38f, 0.25f);
    public Color Pants = new(0.22f, 0.24f, 0.38f);
    public Color Shoes = new(0.30f, 0.20f, 0.14f);
    public Color HatColor = new(0.75f, 0.20f, 0.20f);
    public Color AccColor = new(0.90f, 0.75f, 0.20f);
    public int Face;
    public int HairStyle;
    public int HatStyle;
    public int AccStyle;
    public int TopStyle;
    public int BottomStyle;

    public static readonly string[] FaceNames = { "Gülümseme", "Ciddi", "Sakallı", "Şaşkın" };
    public static readonly string[] HairNames = { "Kısa", "Uzun", "Kabarık", "Kel", "At kuyruğu" };
    public static readonly string[] HatNames = { "Yok", "Şapka", "Kasket", "Taç" };
    public static readonly string[] AccNames = { "Yok", "Gözlük", "Atkı", "Sırt çantası" };
    public static readonly string[] TopNames = { "Kısa kollu", "Uzun kollu", "Ceket" };
    public static readonly string[] BottomNames = { "Pantolon", "Şort" };

    public static void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(Path) != Error.Ok) return;
        var p = new CharacterProfile();
        p.PlayerName = cfg.GetValue("c", "name", p.PlayerName).AsString();
        p.Skin = cfg.GetValue("c", "skin", p.Skin).AsColor();
        p.Hair = cfg.GetValue("c", "hair", p.Hair).AsColor();
        p.Eye = cfg.GetValue("c", "eye", p.Eye).AsColor();
        p.Shirt = cfg.GetValue("c", "shirt", p.Shirt).AsColor();
        p.Pants = cfg.GetValue("c", "pants", p.Pants).AsColor();
        p.Shoes = cfg.GetValue("c", "shoes", p.Shoes).AsColor();
        p.HatColor = cfg.GetValue("c", "hat_color", p.HatColor).AsColor();
        p.AccColor = cfg.GetValue("c", "acc_color", p.AccColor).AsColor();
        p.Face = cfg.GetValue("c", "face", 0).AsInt32();
        p.HairStyle = cfg.GetValue("c", "hair_style", 0).AsInt32();
        p.HatStyle = cfg.GetValue("c", "hat_style", 0).AsInt32();
        p.AccStyle = cfg.GetValue("c", "acc_style", 0).AsInt32();
        p.TopStyle = cfg.GetValue("c", "top_style", 0).AsInt32();
        p.BottomStyle = cfg.GetValue("c", "bottom_style", 0).AsInt32();
        Current = p;
    }

    public void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("c", "name", PlayerName);
        cfg.SetValue("c", "skin", Skin);
        cfg.SetValue("c", "hair", Hair);
        cfg.SetValue("c", "eye", Eye);
        cfg.SetValue("c", "shirt", Shirt);
        cfg.SetValue("c", "pants", Pants);
        cfg.SetValue("c", "shoes", Shoes);
        cfg.SetValue("c", "hat_color", HatColor);
        cfg.SetValue("c", "acc_color", AccColor);
        cfg.SetValue("c", "face", Face);
        cfg.SetValue("c", "hair_style", HairStyle);
        cfg.SetValue("c", "hat_style", HatStyle);
        cfg.SetValue("c", "acc_style", AccStyle);
        cfg.SetValue("c", "top_style", TopStyle);
        cfg.SetValue("c", "bottom_style", BottomStyle);
        cfg.Save(Path);
    }

    public string Encode()
    {
        string n = (PlayerName ?? "Oyuncu").Replace("|", "");
        return string.Join("|", n, Skin.ToHtml(false), Hair.ToHtml(false), Eye.ToHtml(false), Shirt.ToHtml(false),
            Pants.ToHtml(false), Shoes.ToHtml(false), HatColor.ToHtml(false), AccColor.ToHtml(false),
            Face, HairStyle, HatStyle, AccStyle, TopStyle, BottomStyle);
    }

    public static CharacterProfile Decode(string s)
    {
        var p = new CharacterProfile();
        if (string.IsNullOrEmpty(s)) return p;
        var t = s.Split('|');
        if (t.Length < 15) return p;
        p.PlayerName = t[0];
        p.Skin = Color.FromHtml(t[1]); p.Hair = Color.FromHtml(t[2]); p.Eye = Color.FromHtml(t[3]);
        p.Shirt = Color.FromHtml(t[4]); p.Pants = Color.FromHtml(t[5]); p.Shoes = Color.FromHtml(t[6]);
        p.HatColor = Color.FromHtml(t[7]); p.AccColor = Color.FromHtml(t[8]);
        int.TryParse(t[9], out p.Face); int.TryParse(t[10], out p.HairStyle); int.TryParse(t[11], out p.HatStyle);
        int.TryParse(t[12], out p.AccStyle); int.TryParse(t[13], out p.TopStyle); int.TryParse(t[14], out p.BottomStyle);
        return p;
    }
}
