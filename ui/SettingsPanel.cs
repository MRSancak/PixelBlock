using System;
using Godot;
using PixelBlock.Core;

namespace PixelBlock.UI;

public partial class SettingsPanel : CanvasLayer
{
    public Action OnBack;
    private Control _root;

    private OptionButton _qualityOpt, _shadowQualityOpt, _fpsOpt, _resOpt;
    private CheckButton _shadowsCheck, _vsyncCheck, _fullCheck, _ssaoCheck, _bloomCheck, _fogCheck;
    private OptionButton _aaOpt;
    private HSlider _viewSlider;
    private Label _viewLabel;
    private bool _syncing;

    private static readonly int[] FpsOptions = { 0, 30, 60, 90, 120, 144, 240 };

    public override void _Ready()
    {
        Layer = 20;
        ProcessMode = ProcessModeEnum.Always;
        _root = UiKit.Dim(new Color(0.04f, 0.05f, 0.08f, 0.97f));
        AddChild(_root);

        var outer = new VBoxContainer();
        outer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        outer.AddThemeConstantOverride("separation", 12);
        _root.AddChild(outer);

        outer.AddChild(new Control { CustomMinimumSize = new Vector2(0, 16) });
        outer.AddChild(UiKit.Title("Ayarlar", 44, UiKit.Gold));

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        outer.AddChild(scroll);
        var centerWrap = new CenterContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(centerWrap);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 14);
        centerWrap.AddChild(box);

        box.AddChild(Section("Kontroller"));
        box.AddChild(MouseModeRow());
        box.AddChild(Slider("Fare hassasiyeti", 0.2f, 3.0f, Settings.MouseSensitivity,
            v => { Settings.MouseSensitivity = v; Settings.Save(); }));
        box.AddChild(Slider("Klavye bakış hızı", 40f, 220f, Settings.KeyboardLookSpeed,
            v => { Settings.KeyboardLookSpeed = v; Settings.Save(); }, "0"));

        box.AddChild(Section("Ses"));
        box.AddChild(Slider("Ana ses", 0f, 1f, Settings.MasterVolume,
            v => { Settings.MasterVolume = v; Settings.Save(); }));
        box.AddChild(Slider("Müzik ve ortam sesleri", 0f, 1f, Settings.MusicVolume,
            v => { Settings.MusicVolume = v; Settings.Save(); }));

        box.AddChild(Section("Oyun"));
        box.AddChild(Slider("Simülasyon mesafesi (chunk)", 2f, 12f, Settings.SimDistance,
            v => { Settings.SimDistance = (int)v; Settings.Save(); }, "0"));

        box.AddChild(Section("Grafik"));
        BuildGraphics(box);

        var back = UiKit.AnimatedButton("Geri");
        back.Pressed += () => { Visible = false; OnBack?.Invoke(); };
        outer.AddChild(new CenterContainer { Name = "BackRow" });
        var row = (CenterContainer)outer.GetChild(outer.GetChildCount() - 1);
        row.AddChild(back);
        outer.AddChild(new Control { CustomMinimumSize = new Vector2(0, 16) });
    }

    private void BuildGraphics(VBoxContainer box)
    {
        _qualityOpt = Option("Grafik kalitesi", box,
            new[] { "Düşük", "Orta", "Yüksek", "Özel" }, (int)Settings.Quality, i =>
            {
                if (_syncing) return;
                var q = (Settings.GfxQuality)i;
                if (q != Settings.GfxQuality.Ozel) Settings.ApplyPreset(q);
                else Settings.Quality = q;
                Settings.Save();
                SyncGraphicsControls();
            });

        _shadowsCheck = Check("Gölgeler", box, Settings.Shadows, v => Custom(() => Settings.Shadows = v));
        _shadowQualityOpt = Option("Gölge kalitesi", box, new[] { "Düşük", "Orta", "Yüksek" },
            Settings.ShadowQuality, i => Custom(() => Settings.ShadowQuality = i));

        var viewRow = new VBoxContainer { CustomMinimumSize = new Vector2(460, 0) };
        _viewLabel = new Label { Text = $"Görüş mesafesi / chunk yükleme: {Settings.ViewDistance} chunk" };
        _viewLabel.AddThemeFontSizeOverride("font_size", 20);
        viewRow.AddChild(_viewLabel);
        _viewSlider = new HSlider { MinValue = 3, MaxValue = 12, Step = 1, Value = Settings.ViewDistance, CustomMinimumSize = new Vector2(460, 24) };
        _viewSlider.ValueChanged += v =>
        {
            if (_syncing) return;
            _viewLabel.Text = $"Görüş mesafesi / chunk yükleme: {(int)v} chunk";
            Custom(() => Settings.ViewDistance = (int)v);
        };
        viewRow.AddChild(_viewSlider);
        var note = new Label { Text = "Görüş mesafesi bir dünyaya girerken uygulanır." };
        note.AddThemeFontSizeOverride("font_size", 14);
        note.Modulate = new Color(1, 1, 1, 0.6f);
        viewRow.AddChild(note);
        box.AddChild(viewRow);

        var fpsNames = new string[FpsOptions.Length];
        int fpsSel = 0;
        for (int i = 0; i < FpsOptions.Length; i++)
        {
            fpsNames[i] = FpsOptions[i] == 0 ? "Sınırsız" : $"{FpsOptions[i]} FPS";
            if (FpsOptions[i] == Settings.FpsLimit) fpsSel = i;
        }
        _fpsOpt = Option("FPS sınırı", box, fpsNames, fpsSel, i => Custom(() => Settings.FpsLimit = FpsOptions[i]));

        _vsyncCheck = Check("Dikey senkronizasyon (VSync)", box, Settings.VSync, v => Custom(() => Settings.VSync = v));

        var resNames = new string[Settings.Resolutions.Length];
        for (int i = 0; i < resNames.Length; i++) resNames[i] = $"{Settings.Resolutions[i].X} x {Settings.Resolutions[i].Y}";
        _resOpt = Option("Çözünürlük (pencere modu)", box, resNames, Settings.ResolutionIndex,
            i => Custom(() => Settings.ResolutionIndex = i));
        _fullCheck = Check("Tam ekran", box, Settings.Fullscreen, v => Custom(() => Settings.Fullscreen = v));

        _ssaoCheck = Check("Ortam gölgelendirmesi (SSAO)", box, Settings.Ssao, v => Custom(() => Settings.Ssao = v));
        _bloomCheck = Check("Parlama efekti (Bloom)", box, Settings.Bloom, v => Custom(() => Settings.Bloom = v));
        _fogCheck = Check("Hacimsel sis ve ışık huzmeleri", box, Settings.VolFog, v => Custom(() => Settings.VolFog = v));
        _aaOpt = Option("Kenar yumuşatma", box, new[] { "Kapalı", "FXAA", "MSAA 2x", "MSAA 4x" },
            Settings.Aa, i => Custom(() => Settings.Aa = i));
    }

    private void Custom(Action change)
    {
        if (_syncing) return;
        change();
        Settings.Quality = Settings.GfxQuality.Ozel;
        Settings.Save();
        _syncing = true;
        _qualityOpt.Selected = (int)Settings.GfxQuality.Ozel;
        _syncing = false;
    }

    private void SyncGraphicsControls()
    {
        _syncing = true;
        _shadowsCheck.ButtonPressed = Settings.Shadows;
        _shadowQualityOpt.Selected = Settings.ShadowQuality;
        _viewSlider.Value = Settings.ViewDistance;
        _ssaoCheck.ButtonPressed = Settings.Ssao;
        _bloomCheck.ButtonPressed = Settings.Bloom;
        _fogCheck.ButtonPressed = Settings.VolFog;
        _aaOpt.Selected = Settings.Aa;
        _viewLabel.Text = $"Görüş mesafesi / chunk yükleme: {Settings.ViewDistance} chunk";
        _syncing = false;
    }

    private static Label Section(string text)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", 28);
        l.AddThemeColorOverride("font_color", UiKit.Gold);
        return l;
    }

    private Control Slider(string label, float min, float max, float value, Action<float> onChange, string fmt = "0.00")
    {
        var row = new VBoxContainer { CustomMinimumSize = new Vector2(460, 0) };
        var valueLabel = new Label { Text = $"{label}:  {value.ToString(fmt)}" };
        valueLabel.AddThemeFontSizeOverride("font_size", 20);
        row.AddChild(valueLabel);
        double step = fmt == "0" ? 5 : 0.05;
        var slider = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = value, CustomMinimumSize = new Vector2(460, 24) };
        slider.ValueChanged += v =>
        {
            valueLabel.Text = $"{label}:  {((float)v).ToString(fmt)}";
            onChange((float)v);
        };
        row.AddChild(slider);
        return row;
    }

    private static OptionButton Option(string label, VBoxContainer parent, string[] items, int selected, Action<int> onChange)
    {
        var row = new VBoxContainer { CustomMinimumSize = new Vector2(460, 0) };
        var l = new Label { Text = label };
        l.AddThemeFontSizeOverride("font_size", 20);
        row.AddChild(l);
        var opt = new OptionButton { CustomMinimumSize = new Vector2(460, 32) };
        foreach (var it in items) opt.AddItem(it);
        opt.Selected = Mathf.Clamp(selected, 0, items.Length - 1);
        opt.ItemSelected += idx => onChange((int)idx);
        row.AddChild(opt);
        parent.AddChild(row);
        return opt;
    }

    private static CheckButton Check(string label, VBoxContainer parent, bool value, Action<bool> onChange)
    {
        var c = new CheckButton { Text = label, ButtonPressed = value, CustomMinimumSize = new Vector2(460, 36) };
        c.AddThemeFontSizeOverride("font_size", 20);
        c.Toggled += v => onChange(v);
        parent.AddChild(c);
        return c;
    }

    private Control MouseModeRow()
    {
        var row = new VBoxContainer { CustomMinimumSize = new Vector2(460, 0) };
        var label = new Label { Text = "Fare" };
        label.AddThemeFontSizeOverride("font_size", 20);
        row.AddChild(label);

        var opt = new OptionButton { CustomMinimumSize = new Vector2(460, 32) };
        opt.AddItem("Serbest imleç — bakmak için tıkla", (int)Settings.MouseCapture.ClickToCapture);
        opt.AddItem("Yalnızca klavye — fareyi hiç yakalama", (int)Settings.MouseCapture.Off);
        opt.AddItem("Her zaman yakala (klasik FPS)", (int)Settings.MouseCapture.Always);
        opt.Selected = opt.GetItemIndex((int)Settings.CaptureMode);
        opt.ItemSelected += idx =>
        {
            Settings.CaptureMode = (Settings.MouseCapture)opt.GetItemId((int)idx);
            Settings.Save();
        };
        row.AddChild(opt);
        return row;
    }

    public void Open() => Visible = true;
}
