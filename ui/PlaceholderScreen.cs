using System;
using Godot;

namespace PixelBlock.UI;

public partial class PlaceholderScreen : CanvasLayer
{
    public Action OnBack;
    public string TitleText = "";
    public string BodyText = "";

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
        box.AddThemeConstantOverride("separation", 18);
        center.AddChild(box);
        box.AddChild(UiKit.Title(TitleText, 44, UiKit.Gold));
        var body = new Label
        {
            Text = BodyText,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(560, 0),
        };
        body.AddThemeFontSizeOverride("font_size", 20);
        box.AddChild(body);
        var back = UiKit.AnimatedButton("Geri");
        back.Pressed += () => OnBack?.Invoke();
        box.AddChild(back);
        UiKit.FadeIn(root, 0.25);
    }
}
