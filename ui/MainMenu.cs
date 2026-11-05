using System;
using Godot;
using PixelBlock.Core;
using PixelBlock.Lessons;

namespace PixelBlock.UI;

public partial class MainMenu : CanvasLayer
{
    public Action OnSingleplayer;
    public Action OnMultiplayer;
    public Action OnCharacter;
    public Action OnQuit;

    public Action<string> OnPlayLesson;
    public Action OnSandbox;
    public Action<string> OnShowcase;
    public CampaignProgress Progress;

    private SettingsPanel _settings;

    public override void _Ready()
    {
        Layer = 10;
        AddChild(UiKit.Dim(new Color(0.05f, 0.07f, 0.11f)));

        var bgTex = GD.Load<Texture2D>("res://docs/images/world.png");
        if (bgTex != null)
        {
            var bg = new TextureRect
            {
                Texture = bgTex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(bg);
            bg.Resized += () => bg.PivotOffset = bg.Size / 2f;
            var zoom = bg.CreateTween().SetLoops();
            zoom.TweenProperty(bg, "scale", new Vector2(1.06f, 1.06f), 18.0).SetTrans(Godot.Tween.TransitionType.Sine);
            zoom.TweenProperty(bg, "scale", Vector2.One, 18.0).SetTrans(Godot.Tween.TransitionType.Sine);
        }

        var shade = UiKit.Dim(new Color(0.02f, 0.03f, 0.06f, 0.55f));
        shade.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(shade);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 90);
        margin.AddThemeConstantOverride("margin_top", 60);
        margin.AddThemeConstantOverride("margin_bottom", 60);
        AddChild(margin);

        var center = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        center.AddThemeConstantOverride("separation", 14);
        margin.AddChild(center);

        var title = UiKit.Title("PIXELBLOCK", 78, UiKit.Gold);
        title.HorizontalAlignment = HorizontalAlignment.Left;
        center.AddChild(title);
        var sub = UiKit.Title("Blok Dünyaları", 24, new Color(0.88f, 0.9f, 0.96f));
        sub.HorizontalAlignment = HorizontalAlignment.Left;
        center.AddChild(sub);
        center.AddChild(new Control { CustomMinimumSize = new Vector2(0, 28) });

        var items = new (string text, Action act)[]
        {
            ("Tek Oyunculu", () => OnSingleplayer?.Invoke()),
            ("Çok Oyunculu", () => OnMultiplayer?.Invoke()),
            ("Ayarlar", OpenSettings),
            ("Karakter", () => OnCharacter?.Invoke()),
            ("Oyundan Çıkış", () => OnQuit?.Invoke()),
        };
        double delay = 0.1;
        foreach (var (text, act) in items)
        {
            var b = UiKit.AnimatedButton(text);
            var a = act;
            b.Pressed += () => a();
            var holder = new HBoxContainer();
            holder.AddChild(b);
            center.AddChild(holder);
            UiKit.SlideIn(holder, delay);
            delay += 0.08;
        }

        Input.MouseMode = Input.MouseModeEnum.Visible;
        UiKit.FadeIn(margin, 0.5);
    }

    private void OpenSettings()
    {
        if (_settings == null)
        {
            _settings = new SettingsPanel();
            AddChild(_settings);
            _settings.OnBack = () => { };
        }
        _settings.Open();
    }
}
