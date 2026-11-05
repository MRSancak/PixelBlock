using Godot;
using PixelBlock.Core;

namespace PixelBlock.UI;

public static class UiKit
{
    public static readonly Color Gold = new(1f, 0.88f, 0.5f);

    public static Button Button(string text, int fontSize = 22)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(360, 54) };
        b.AddThemeFontSizeOverride("font_size", fontSize);
        b.Pressed += () => AudioManager.Play("click");
        var normal = Style(new Color(0.12f, 0.13f, 0.18f, 0.95f), new Color(0.5f, 0.5f, 0.6f, 0.8f));
        var hover = Style(new Color(0.2f, 0.22f, 0.3f, 0.98f), Gold);
        var pressed = Style(new Color(0.08f, 0.08f, 0.11f, 1f), Gold);
        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("hover", hover);
        b.AddThemeStyleboxOverride("pressed", pressed);
        b.AddThemeStyleboxOverride("focus", hover);
        return b;
    }

    public static Button AnimatedButton(string text, int fontSize = 22)
    {
        var b = Button(text, fontSize);
        b.Resized += () => b.PivotOffset = b.Size / 2f;
        b.MouseEntered += () => Tween(b, "scale", new Vector2(1.04f, 1.04f), 0.12);
        b.MouseExited += () => Tween(b, "scale", Vector2.One, 0.12);
        return b;
    }

    public static void Tween(Control c, string prop, Variant to, double seconds)
    {
        if (!GodotObject.IsInstanceValid(c) || !c.IsInsideTree()) return;
        var t = c.CreateTween();
        t.SetTrans(Godot.Tween.TransitionType.Cubic).SetEase(Godot.Tween.EaseType.Out);
        t.TweenProperty(c, prop, to, seconds);
    }

    public static void SlideIn(Control c, double delay, float fromX = -40f)
    {
        c.Modulate = new Color(1, 1, 1, 0);
        var t = c.CreateTween();
        t.SetTrans(Godot.Tween.TransitionType.Cubic).SetEase(Godot.Tween.EaseType.Out);
        t.TweenInterval(delay);
        t.TweenProperty(c, "modulate:a", 1.0f, 0.35);

        c.Scale = new Vector2(0.96f, 0.96f);
        t.Parallel().TweenProperty(c, "scale", Vector2.One, 0.35);
    }

    public static void FadeIn(Control c, double seconds = 0.3)
    {
        c.Modulate = new Color(1, 1, 1, 0);
        var t = c.CreateTween();
        t.TweenProperty(c, "modulate:a", 1.0f, seconds);
    }

    public static Label Title(string text, int size, Color color)
    {
        var l = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        l.AddThemeColorOverride("font_outline_color", Colors.Black);
        l.AddThemeConstantOverride("outline_size", 6);
        return l;
    }

    public static ColorRect Dim(Color color)
    {
        var r = new ColorRect { Color = color };
        r.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        return r;
    }

    private static StyleBoxFlat Style(Color bg, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 8, ContentMarginBottom = 8,
        };
    }
}
