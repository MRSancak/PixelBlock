using System.Collections.Generic;
using Godot;

namespace PixelBlock.UI;

public partial class ChatBox : CanvasLayer
{
    public System.Action<string> OnSubmit;
    public System.Action OnClosed;
    public System.Func<string, string> Complete;

    public bool IsOpen { get; private set; }

    private const int MaxLines = 14;
    private const float ShowSeconds = 9f;

    private Control _root;
    private VBoxContainer _log;
    private LineEdit _input;
    private readonly List<(Label label, float born)> _lines = new();
    private readonly List<string> _history = new();
    private int _histPos;
    private float _clock;

    public override void _Ready()
    {
        Layer = 19;
        ProcessMode = ProcessModeEnum.Always;

        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        _log = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End, MouseFilter = Control.MouseFilterEnum.Ignore };
        _log.AddThemeConstantOverride("separation", 2);
        _log.AnchorLeft = 0; _log.AnchorRight = 0; _log.AnchorTop = 1; _log.AnchorBottom = 1;
        _log.OffsetLeft = 16; _log.OffsetRight = 560; _log.OffsetTop = -440; _log.OffsetBottom = -156;
        _log.GrowVertical = Control.GrowDirection.Begin;
        _root.AddChild(_log);

        _input = new LineEdit
        {
            Visible = false,
            PlaceholderText = "Mesaj yaz  ·  / ile komut  ·  /yardim",
            MaxLength = 200,
        };
        var bg = new StyleBoxFlat
        {
            BgColor = new Color(0.03f, 0.04f, 0.07f, 0.85f),
            BorderColor = UiKit.Gold,
            ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 6, ContentMarginBottom = 6,
        };
        bg.BorderWidthLeft = bg.BorderWidthRight = bg.BorderWidthTop = bg.BorderWidthBottom = 2;
        _input.AddThemeStyleboxOverride("normal", bg);
        _input.AddThemeStyleboxOverride("focus", bg);
        _input.AddThemeFontSizeOverride("font_size", 18);
        _input.AnchorLeft = 0; _input.AnchorRight = 0; _input.AnchorTop = 1; _input.AnchorBottom = 1;
        _input.OffsetLeft = 16; _input.OffsetRight = 560; _input.OffsetTop = -146; _input.OffsetBottom = -106;
        _input.GrowVertical = Control.GrowDirection.Begin;
        _input.TextSubmitted += OnTextSubmitted;
        _input.GuiInput += OnInputGui;
        _root.AddChild(_input);
    }

    public void Open(string prefill = "")
    {
        IsOpen = true;
        _input.Visible = true;
        _input.Text = prefill;
        _input.CaretColumn = prefill.Length;
        _histPos = _history.Count;
        CallDeferred(nameof(FocusInput));
    }

    private void FocusInput()
    {
        if (!IsOpen) return;
        _input.GrabFocus();
        _input.CaretColumn = _input.Text.Length;
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        _input.ReleaseFocus();
        _input.Visible = false;
        _input.Text = "";
        OnClosed?.Invoke();
    }

    public void Print(string text, Color? color = null)
    {
        foreach (string part in text.Split('\n'))
        {
            var l = new Label
            {
                Text = part,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                CustomMinimumSize = new Vector2(540, 0),
            };
            l.AddThemeFontSizeOverride("font_size", 18);
            l.AddThemeColorOverride("font_color", color ?? Colors.White);
            l.AddThemeColorOverride("font_outline_color", Colors.Black);
            l.AddThemeConstantOverride("outline_size", 5);
            _log.AddChild(l);
            _lines.Add((l, _clock));
            while (_lines.Count > MaxLines)
            {
                _lines[0].label.QueueFree();
                _lines.RemoveAt(0);
            }
        }
    }

    public void ClearLog()
    {
        foreach (var (l, _) in _lines) l.QueueFree();
        _lines.Clear();
    }

    public override void _Process(double delta)
    {
        _clock += (float)delta;
        foreach (var (label, born) in _lines)
        {
            float a = IsOpen ? 1f : Mathf.Clamp((ShowSeconds - (_clock - born)) / 1.5f, 0f, 1f);
            label.Modulate = new Color(1, 1, 1, a);
        }
    }

    private void OnTextSubmitted(string text)
    {
        text = text.Trim();
        if (text.Length > 0)
        {
            if (_history.Count == 0 || _history[^1] != text) _history.Add(text);
            Close();
            OnSubmit?.Invoke(text);
        }
        else Close();
    }

    private void OnInputGui(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true } k) return;
        switch (k.Keycode)
        {
            case Key.Escape:
                Close();
                _input.AcceptEvent();
                break;
            case Key.Up:
                if (_history.Count > 0)
                {
                    _histPos = Mathf.Max(0, _histPos - 1);
                    _input.Text = _history[_histPos];
                    _input.CaretColumn = _input.Text.Length;
                }
                _input.AcceptEvent();
                break;
            case Key.Down:
                if (_history.Count > 0)
                {
                    _histPos = Mathf.Min(_history.Count, _histPos + 1);
                    _input.Text = _histPos >= _history.Count ? "" : _history[_histPos];
                    _input.CaretColumn = _input.Text.Length;
                }
                _input.AcceptEvent();
                break;
            case Key.Tab:
                if (Complete != null)
                {
                    string r = Complete(_input.Text);
                    if (!string.IsNullOrEmpty(r)) { _input.Text = r; _input.CaretColumn = r.Length; }
                }
                _input.AcceptEvent();
                break;
        }
    }
}
