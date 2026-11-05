using System;
using Godot;

namespace PixelBlock.UI;

public partial class PauseMenu : CanvasLayer
{
    public Func<bool> CanPause;
    public Action OnReturnToMenu;

    private Control _root;
    private SettingsPanel _settings;
    private bool _paused;
    private Input.MouseModeEnum _preMouse = Input.MouseModeEnum.Visible;

    public bool IsPaused => _paused;

    public override void _Ready()
    {
        Layer = 15;
        ProcessMode = ProcessModeEnum.Always;

        _root = UiKit.Dim(new Color(0.03f, 0.04f, 0.07f, 0.78f));
        _root.Visible = false;
        AddChild(_root);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(center);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 14);
        center.AddChild(box);

        box.AddChild(UiKit.Title("Duraklatıldı", 48, UiKit.Gold));
        Add(box, "Devam Et", Resume);
        Add(box, "Ayarlar", () => { _settings ??= MakeSettings(); _settings.Open(); });
        Add(box, "Ana Menü", () => { SetPaused(false); OnReturnToMenu?.Invoke(); });
        Add(box, "Oyundan Çıkış", () => GetTree().Quit());
    }

    private void Add(VBoxContainer box, string text, Action onPressed)
    {
        var b = UiKit.Button(text);
        b.Pressed += onPressed;
        box.AddChild(b);
    }

    private SettingsPanel MakeSettings()
    {
        var s = new SettingsPanel();
        AddChild(s);
        s.Visible = false;
        return s;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!e.IsActionPressed(Core.GameInput.Actions.Pause)) return;
        if (_paused) Resume();
        else if (CanPause?.Invoke() ?? true) SetPaused(true);
        GetViewport().SetInputAsHandled();
    }

    private void Resume() => SetPaused(false);

    private void SetPaused(bool p)
    {
        _paused = p;
        GetTree().Paused = p;
        _root.Visible = p;

        if (p) { _preMouse = Input.MouseMode; Input.MouseMode = Input.MouseModeEnum.Visible; }
        else Input.MouseMode = _preMouse;
    }
}
