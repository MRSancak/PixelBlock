using System;
using Godot;
using PixelBlock.Core;

namespace PixelBlock.UI;

public partial class NewWorldMenu : CanvasLayer
{
    public Action OnBack;

    public Action<string, int> OnCreate;
    public string DefaultName = "Dünya";

    private LineEdit _name, _seed;
    private Label _hint;

    public override void _Ready()
    {
        Layer = 20;
        ProcessMode = ProcessModeEnum.Always;
        var root = UiKit.Dim(new Color(0.04f, 0.05f, 0.08f, 0.97f));
        AddChild(root);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 14);
        center.AddChild(box);

        box.AddChild(UiKit.Title("Yeni Dünya Oluştur", 44, UiKit.Gold));

        box.AddChild(Lbl("Dünya adı"));
        _name = new LineEdit { Text = DefaultName, CustomMinimumSize = new Vector2(460, 42), MaxLength = 32 };
        _name.AddThemeFontSizeOverride("font_size", 20);
        box.AddChild(_name);

        box.AddChild(Lbl("Seed (boş bırakırsan rastgele olur)"));
        _seed = new LineEdit { PlaceholderText = "Örn. 12345 veya ormanlık", CustomMinimumSize = new Vector2(460, 42), MaxLength = 40 };
        _seed.AddThemeFontSizeOverride("font_size", 20);
        box.AddChild(_seed);

        var dice = UiKit.AnimatedButton("🎲  Rastgele Seed", 18);
        dice.CustomMinimumSize = new Vector2(460, 44);
        dice.Pressed += () => _seed.Text = ((int)(GD.Randi() & 0x7FFFFFFF)).ToString();
        box.AddChild(dice);

        _hint = new Label { Text = "Aynı Seed her zaman aynı dünyayı üretir." };
        _hint.AddThemeFontSizeOverride("font_size", 16);
        _hint.Modulate = new Color(1, 1, 1, 0.65f);
        box.AddChild(_hint);

        var create = UiKit.AnimatedButton("Dünyayı Oluştur");
        create.CustomMinimumSize = new Vector2(460, 56);
        create.Pressed += Create;
        box.AddChild(create);

        var back = UiKit.AnimatedButton("Geri");
        back.CustomMinimumSize = new Vector2(460, 50);
        back.Pressed += () => OnBack?.Invoke();
        box.AddChild(back);

        UiKit.FadeIn(root, 0.25);
    }

    private static Label Lbl(string t)
    {
        var l = new Label { Text = t };
        l.AddThemeFontSizeOverride("font_size", 20);
        return l;
    }

    private void Create()
    {
        string name = string.IsNullOrWhiteSpace(_name.Text) ? DefaultName : _name.Text.Trim();
        OnCreate?.Invoke(name, ParseSeed(_seed.Text));
    }

    public static int ParseSeed(string text)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0) return (int)(GD.Randi() & 0x7FFFFFFF);
        if (long.TryParse(text, out long n)) return (int)(Math.Abs(n) & 0x7FFFFFFF);
        uint h = 2166136261;
        foreach (char c in text) { h ^= c; h *= 16777619; }
        return (int)(h & 0x7FFFFFFF);
    }
}
