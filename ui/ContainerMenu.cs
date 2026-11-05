using System.Collections.Generic;
using Godot;
using PixelBlock.Core;

namespace PixelBlock.UI;

/// <summary>Sandık / fırın ekranı: üstte konteyner, altta oyuncu envanteri + hızlı erişim.</summary>
public partial class ContainerMenu : CanvasLayer
{
    public System.Action OnClose;
    public System.Action<int, int> OnDropItem;

    private Inventory _inv;
    private BlockTextures _tex;
    private Control _root;
    private Label _title;
    private VBoxContainer _top;
    private readonly List<InvSlot> _playerSlots = new();
    private readonly List<InvSlot> _contSlots = new();
    private ContainerData _data;
    private ProgressBar _smelt, _fire;
    private bool _built;

    public void Setup(Inventory inv, BlockTextures tex)
    {
        _inv = inv;
        _tex = tex;
        _inv.Changed += RefreshAll;
    }

    public override void _ExitTree()
    {
        if (_inv != null) _inv.Changed -= RefreshAll;
        if (_data != null) _data.Inv.Changed -= RefreshAll;
    }

    public override void _Ready()
    {
        Layer = 18;
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;
        _root = UiKit.Dim(new Color(0.03f, 0.04f, 0.07f, 0.90f));
        AddChild(_root);
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(center);
        var outer = new VBoxContainer();
        outer.AddThemeConstantOverride("separation", 10);
        center.AddChild(outer);

        _title = UiKit.Title("Sandık", 34, UiKit.Gold);
        outer.AddChild(_title);
        _top = new VBoxContainer();
        _top.AddThemeConstantOverride("separation", 8);
        outer.AddChild(_top);

        var il = new Label { Text = "Envanter" };
        il.AddThemeFontSizeOverride("font_size", 16);
        il.Modulate = new Color(1, 1, 1, 0.8f);
        outer.AddChild(il);
        var grid = new GridContainer { Columns = 9 };
        grid.AddThemeConstantOverride("h_separation", 5);
        grid.AddThemeConstantOverride("v_separation", 5);
        outer.AddChild(grid);
        for (int i = Inventory.HotbarSize; i < Inventory.TotalSlots; i++) grid.AddChild(PlayerSlot(i));
        var hl = new Label { Text = "Hızlı Erişim" };
        hl.AddThemeFontSizeOverride("font_size", 16);
        hl.Modulate = new Color(1, 1, 1, 0.8f);
        outer.AddChild(hl);
        var hot = new GridContainer { Columns = 9 };
        hot.AddThemeConstantOverride("h_separation", 5);
        outer.AddChild(hot);
        for (int i = 0; i < Inventory.HotbarSize; i++) hot.AddChild(PlayerSlot(i));

        var tip = new Label { Text = "Sürükle-bırak ya da Shift+tık ile aktar  ·  Esc: kapat" };
        tip.AddThemeFontSizeOverride("font_size", 14);
        tip.Modulate = new Color(1, 1, 1, 0.6f);
        outer.AddChild(tip);
        var back = UiKit.AnimatedButton("Kapat");
        back.CustomMinimumSize = new Vector2(260, 44);
        back.Pressed += Close;
        var row = new CenterContainer();
        row.AddChild(back);
        outer.AddChild(row);
        _built = true;
    }

    private InvSlot PlayerSlot(int i)
    {
        var s = new InvSlot { Index = i, Inv = _inv, Tex = _tex };
        s.OnDropOut = (idx, n) => OnDropItem?.Invoke(idx, n);
        s.OnShift = slot => ShiftFromPlayer(slot.Index);
        _playerSlots.Add(s);
        return s;
    }

    private InvSlot ContSlot(int i)
    {
        var s = new InvSlot { Index = i, Inv = _data.Inv, Tex = _tex };
        s.OnShift = slot => ShiftFromContainer(slot.Index);
        _contSlots.Add(s);
        return s;
    }

    private void ShiftFromPlayer(int idx)
    {
        if (_data == null) return;
        ushort id = _inv.SlotId(idx);
        int n = _inv.SlotCount(idx);
        if (id == 0) return;
        int left;
        if (_data.Kind == "furnace")
        {
            int target = Containers.IsSmeltable(id) ? 0 : Containers.IsFuel(id) ? 1 : -1;
            if (target < 0) return;
            left = _data.Inv.AddToRange(id, n, target, target);
        }
        else left = _data.Inv.AddToRange(id, n, 0, Containers.ChestSlots - 1);
        if (left != n) _inv.TakeFromSlot(idx, n - left);
    }

    private void ShiftFromContainer(int idx)
    {
        if (_data == null) return;
        ushort id = _data.Inv.SlotId(idx);
        int n = _data.Inv.SlotCount(idx);
        if (id == 0) return;
        int left = _inv.Add(id, n);
        if (left != n) _data.Inv.TakeFromSlot(idx, n - left);
    }

    public void Open(ContainerData data)
    {
        if (!_built) return;
        if (_data != null) _data.Inv.Changed -= RefreshAll;
        _data = data;
        _data.Inv.Changed += RefreshAll;

        foreach (Node c in _top.GetChildren()) c.QueueFree();
        _contSlots.Clear();
        _smelt = _fire = null;

        if (data.Kind == "furnace")
        {
            _title.Text = "Fırın";
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 14);
            _top.AddChild(row);

            var left = new VBoxContainer();
            left.AddThemeConstantOverride("separation", 6);
            row.AddChild(left);
            var l1 = new Label { Text = "Eritilecek" };
            l1.AddThemeFontSizeOverride("font_size", 15);
            left.AddChild(l1);
            var inSlot = ContSlot(0);
            inSlot.CanAccept = Containers.IsSmeltable;
            left.AddChild(inSlot);
            var l2 = new Label { Text = "Yakıt" };
            l2.AddThemeFontSizeOverride("font_size", 15);
            left.AddChild(l2);
            var fuelSlot = ContSlot(1);
            fuelSlot.CanAccept = Containers.IsFuel;
            left.AddChild(fuelSlot);

            var mid = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            mid.AddThemeConstantOverride("separation", 10);
            row.AddChild(mid);
            _fire = new ProgressBar { CustomMinimumSize = new Vector2(120, 16), ShowPercentage = false, MaxValue = 1 };
            _fire.Modulate = new Color(1f, 0.6f, 0.2f);
            mid.AddChild(_fire);
            _smelt = new ProgressBar { CustomMinimumSize = new Vector2(120, 16), ShowPercentage = false, MaxValue = 1 };
            _smelt.Modulate = new Color(0.6f, 1f, 0.6f);
            mid.AddChild(_smelt);
            var hint = new Label { Text = "Cevher · et · odun · kil · taş\nYakıt: kömür, odun, tahta" };
            hint.AddThemeFontSizeOverride("font_size", 13);
            hint.Modulate = new Color(1, 1, 1, 0.6f);
            mid.AddChild(hint);

            var right = new VBoxContainer();
            right.AddThemeConstantOverride("separation", 6);
            row.AddChild(right);
            var l3 = new Label { Text = "Sonuç" };
            l3.AddThemeFontSizeOverride("font_size", 15);
            right.AddChild(l3);
            var outSlot = ContSlot(2);
            outSlot.TakeOnly = true;
            right.AddChild(outSlot);
        }
        else
        {
            _title.Text = "Sandık";
            var g = new GridContainer { Columns = 9 };
            g.AddThemeConstantOverride("h_separation", 5);
            g.AddThemeConstantOverride("v_separation", 5);
            _top.AddChild(g);
            for (int i = 0; i < Containers.ChestSlots; i++) g.AddChild(ContSlot(i));
        }

        Visible = true;
        UiKit.FadeIn(_root, 0.15);
        RefreshAll();
    }

    public void Close()
    {
        if (!Visible) return;
        Visible = false;
        if (_data != null) { _data.Inv.Changed -= RefreshAll; _data = null; }
        OnClose?.Invoke();
    }

    public void ForceHide()
    {
        Visible = false;
        if (_data != null) { _data.Inv.Changed -= RefreshAll; _data = null; }
    }

    private void RefreshAll()
    {
        if (!Visible) return;
        foreach (var s in _playerSlots) s.Refresh();
        foreach (var s in _contSlots) s.Refresh();
    }

    public override void _Process(double delta)
    {
        if (!Visible || _data == null) return;
        if (_smelt != null) _smelt.Value = _data.Prog / Containers.SmeltTime;
        if (_fire != null) _fire.Value = _data.BurnMax > 0f ? _data.Burn / _data.BurnMax : 0f;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Visible) return;
        if (e.IsActionPressed(GameInput.Actions.Pause))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }
}
