using System;
using Godot;
using PixelBlock.Network;

namespace PixelBlock.UI;

public partial class MultiplayerMenu : CanvasLayer
{
    public static string LastIp = "";

    public Action OnBack;
    public Action OnHost;
    public Action<string> OnJoin;
    public LanDiscovery Lan;

    private LineEdit _ip;
    private VBoxContainer _list;
    private Label _empty;
    private string _signature = "#";
    private float _t;

    public override void _Ready()
    {
        Layer = 20;
        ProcessMode = ProcessModeEnum.Always;
        var root = UiKit.Dim(new Color(0.04f, 0.05f, 0.08f, 0.97f));
        AddChild(root);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0) };
        box.AddThemeConstantOverride("separation", 12);
        center.AddChild(box);

        box.AddChild(UiKit.Title("Çok Oyunculu", 44, UiKit.Gold));

        var host = UiKit.AnimatedButton("Oyun Kur (Host)");
        host.CustomMinimumSize = new Vector2(520, 54);
        host.Pressed += () => OnHost?.Invoke();
        box.AddChild(host);
        var hostHint = new Label { Text = "Bir dünya seç; aynı ağdaki (LAN) arkadaşların bu dünyaya katılabilir." };
        hostHint.AddThemeFontSizeOverride("font_size", 14);
        hostHint.Modulate = new Color(1, 1, 1, 0.65f);
        hostHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(hostHint);

        var lanTitle = new Label { Text = "Yerel ağdaki (LAN) oyunlar" };
        lanTitle.AddThemeFontSizeOverride("font_size", 20);
        lanTitle.AddThemeColorOverride("font_color", UiKit.Gold);
        box.AddChild(lanTitle);
        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 6);
        box.AddChild(_list);
        _empty = new Label { Text = "Oyun aranıyor…" };
        _empty.AddThemeFontSizeOverride("font_size", 16);
        _empty.Modulate = new Color(1, 1, 1, 0.6f);
        box.AddChild(_empty);

        var ipTitle = new Label { Text = "IP adresi ile katıl" };
        ipTitle.AddThemeFontSizeOverride("font_size", 20);
        ipTitle.AddThemeColorOverride("font_color", UiKit.Gold);
        box.AddChild(ipTitle);
        _ip = new LineEdit { PlaceholderText = "Örn. 192.168.1.20", Text = LastIp, CustomMinimumSize = new Vector2(520, 42) };
        _ip.AddThemeFontSizeOverride("font_size", 20);
        box.AddChild(_ip);
        var join = UiKit.AnimatedButton("Katıl");
        join.CustomMinimumSize = new Vector2(520, 50);
        join.Pressed += () =>
        {
            string ip = _ip.Text.Trim();
            if (ip.Length > 0) OnJoin?.Invoke(ip);
        };
        box.AddChild(join);

        var back = UiKit.AnimatedButton("Geri");
        back.CustomMinimumSize = new Vector2(520, 50);
        back.Pressed += () => OnBack?.Invoke();
        box.AddChild(back);

        Lan?.StartListen();
        UiKit.FadeIn(root, 0.25);
    }

    public override void _ExitTree()
    {
        Lan?.StopListen();
    }

    public override void _Process(double delta)
    {
        _t -= (float)delta;
        if (_t > 0f || Lan == null || _list == null) return;
        _t = 0.5f;

        string sig = "";
        foreach (var s in Lan.Servers) sig += s.Ip + ":" + s.Port + ":" + s.Name + ";";
        if (sig == _signature) return;
        _signature = sig;

        foreach (Node c in _list.GetChildren()) c.QueueFree();
        _empty.Visible = Lan.Servers.Count == 0;
        foreach (var s in Lan.Servers)
        {
            string ip = s.Ip;
            var b = UiKit.Button($"{s.Name}  —  {s.Ip}", 18);
            b.CustomMinimumSize = new Vector2(520, 44);
            b.Pressed += () => OnJoin?.Invoke(ip);
            _list.AddChild(b);
        }
    }
}
