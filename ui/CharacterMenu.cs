using System;
using Godot;
using PixelBlock.Characters;
using PixelBlock.Core;

namespace PixelBlock.UI;

public partial class CharacterMenu : CanvasLayer
{
    public Action OnBack;
    private AvatarModel _avatar;
    private float _spin;

    public override void _Ready()
    {
        Layer = 20;
        ProcessMode = ProcessModeEnum.Always;
        var root = UiKit.Dim(new Color(0.04f, 0.05f, 0.08f, 0.97f));
        AddChild(root);

        var outer = new VBoxContainer();
        outer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        outer.AddThemeConstantOverride("separation", 10);
        root.AddChild(outer);
        outer.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });
        outer.AddChild(UiKit.Title("Karakter", 44, UiKit.Gold));

        var main = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        main.AddThemeConstantOverride("separation", 30);
        outer.AddChild(main);

        var svc = new SubViewportContainer { Stretch = true, CustomMinimumSize = new Vector2(360, 500) };
        var vp = new SubViewport { OwnWorld3D = true, Size = new Vector2I(360, 500) };
        svc.AddChild(vp);
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.10f, 0.12f, 0.17f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = Colors.White,
            AmbientLightEnergy = 0.7f,
        };
        vp.AddChild(new WorldEnvironment { Environment = env });
        vp.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-40f, -30f, 0f), LightEnergy = 1.2f });
        vp.AddChild(new Camera3D { Position = new Vector3(0f, 1.15f, 4.2f), RotationDegrees = new Vector3(-4f, 0f, 0f), Fov = 38f, Current = true });
        _avatar = new AvatarModel { Name = "Preview" };
        vp.AddChild(_avatar);
        _avatar.Build(CharacterProfile.Current);
        main.AddChild(svc);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(460, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        main.AddChild(scroll);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(440, 0) };
        box.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(box);

        var p = CharacterProfile.Current;

        box.AddChild(Lbl("Oyuncu adı"));
        var name = new LineEdit { Text = p.PlayerName, MaxLength = 16, CustomMinimumSize = new Vector2(440, 38) };
        name.TextChanged += t => { p.PlayerName = string.IsNullOrWhiteSpace(t) ? "Oyuncu" : t.Trim(); p.Save(); };
        box.AddChild(name);

        Opt(box, "Yüz", CharacterProfile.FaceNames, p.Face, i => { p.Face = i; Changed(); });
        Opt(box, "Saç modeli", CharacterProfile.HairNames, p.HairStyle, i => { p.HairStyle = i; Changed(); });
        Col(box, "Saç rengi", p.Hair, c => { p.Hair = c; Changed(); });
        Col(box, "Ten rengi", p.Skin, c => { p.Skin = c; Changed(); });
        Col(box, "Göz rengi", p.Eye, c => { p.Eye = c; Changed(); });
        Opt(box, "Üst kıyafet", CharacterProfile.TopNames, p.TopStyle, i => { p.TopStyle = i; Changed(); });
        Col(box, "Üst kıyafet rengi", p.Shirt, c => { p.Shirt = c; Changed(); });
        Opt(box, "Alt kıyafet", CharacterProfile.BottomNames, p.BottomStyle, i => { p.BottomStyle = i; Changed(); });
        Col(box, "Pantolon rengi", p.Pants, c => { p.Pants = c; Changed(); });
        Col(box, "Ayakkabı rengi", p.Shoes, c => { p.Shoes = c; Changed(); });
        Opt(box, "Şapka", CharacterProfile.HatNames, p.HatStyle, i => { p.HatStyle = i; Changed(); });
        Col(box, "Şapka rengi", p.HatColor, c => { p.HatColor = c; Changed(); });
        Opt(box, "Aksesuar", CharacterProfile.AccNames, p.AccStyle, i => { p.AccStyle = i; Changed(); });
        Col(box, "Aksesuar rengi", p.AccColor, c => { p.AccColor = c; Changed(); });

        var back = UiKit.AnimatedButton("Kaydet ve Geri");
        back.Pressed += () => { CharacterProfile.Current.Save(); OnBack?.Invoke(); };
        var row = new CenterContainer();
        row.AddChild(back);
        outer.AddChild(row);
        outer.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });

        UiKit.FadeIn(root, 0.25);
    }

    public override void _Process(double delta)
    {
        if (_avatar == null) return;
        float dt = (float)delta;
        _spin += dt * 0.7f;
        _avatar.Rotation = new Vector3(0f, Mathf.Sin(_spin) * 0.9f, 0f);
        _avatar.Tick(dt, 3.2f, true, false, false, false);
    }

    private void Changed()
    {
        CharacterProfile.Current.Save();
        _avatar.Build(CharacterProfile.Current);
    }

    private static Label Lbl(string t)
    {
        var l = new Label { Text = t };
        l.AddThemeFontSizeOverride("font_size", 18);
        return l;
    }

    private static void Opt(VBoxContainer parent, string label, string[] items, int selected, Action<int> onChange)
    {
        parent.AddChild(Lbl(label));
        var o = new OptionButton { CustomMinimumSize = new Vector2(440, 34) };
        foreach (var it in items) o.AddItem(it);
        o.Selected = Mathf.Clamp(selected, 0, items.Length - 1);
        o.ItemSelected += idx => onChange((int)idx);
        parent.AddChild(o);
    }

    private static void Col(VBoxContainer parent, string label, Color value, Action<Color> onChange)
    {
        var row = new HBoxContainer();
        var l = Lbl(label);
        l.CustomMinimumSize = new Vector2(250, 0);
        row.AddChild(l);
        var b = new ColorPickerButton { Color = value, CustomMinimumSize = new Vector2(150, 34), EditAlpha = false };
        b.ColorChanged += c => onChange(c);
        row.AddChild(b);
        parent.AddChild(row);
    }
}
