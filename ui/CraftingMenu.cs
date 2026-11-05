using System.Collections.Generic;
using Godot;
using PixelBlock.Core;

namespace PixelBlock.UI;

public partial class CraftingMenu : CanvasLayer
{
    public System.Action OnClose;

    public System.Func<bool> TableNearby;
    public System.Func<bool> HeatNearby;
    public System.Func<bool> IsCreative;
    public System.Func<int> SelectedHotbar;

    public System.Action<int, int> OnDropItem;
    public System.Action<ushort, int> OnCreativePick;
    public System.Func<ushort[]> GetArmor;
    public System.Action<int> OnUnequip;
    public System.Action<int> OnEquip;
    private readonly ArmorSlotUi[] _armorUi = new ArmorSlotUi[4];
    private Label _armorLabel;

    private Inventory _inv;
    private BlockTextures _tex;
    private Control _root;
    private readonly List<InvSlot> _slots = new();
    private readonly List<RowUi> _rows = new();
    private int _filter;
    private int _creativeFilter;
    private bool _creativeTab;
    private Label _tableLabel;
    private Label _title;
    private Button _tabCraft, _tabCreative;
    private Control _craftPane, _creativePane;
    private GridContainer _creativeGrid;
    private int _shownMode = -1;

    private struct RowUi
    {
        public Recipe Recipe;
        public Control Root;
        public Button Craft;
        public Label Needs;
        public int Group;
    }

    public void Setup(Inventory inv, BlockTextures tex)
    {
        _inv = inv;
        _tex = tex;
        _inv.Changed += Refresh;
        foreach (var a in _armorUi) if (a != null) a.Tex = _tex;
        foreach (var s in _slots) { s.Inv = _inv; s.Tex = _tex; }
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

        var main = new HBoxContainer();
        main.AddThemeConstantOverride("separation", 28);
        outer.AddChild(main);

        // ---- Sol: envanter + hızlı erişim ----
        var left = new VBoxContainer();
        left.AddThemeConstantOverride("separation", 8);
        main.AddChild(left);
        _title = UiKit.Title("Envanter", 34, UiKit.Gold);
        left.AddChild(_title);

        var armorRow = new HBoxContainer();
        armorRow.AddThemeConstantOverride("separation", 6);
        left.AddChild(armorRow);
        var al = new Label { Text = "Zırh:" };
        al.AddThemeFontSizeOverride("font_size", 16);
        armorRow.AddChild(al);
        string[] slotNames = { "Miğfer", "Zırh", "Pantolon", "Bot" };
        for (int i = 0; i < 4; i++)
        {
            var au = new ArmorSlotUi { Slot = i, SlotName = slotNames[i], Tex = _tex };
            int si = i;
            au.OnClick = () => OnUnequip?.Invoke(si);
            au.OnDropFrom = from => OnEquip?.Invoke(from);
            armorRow.AddChild(au);
            _armorUi[i] = au;
        }
        _armorLabel = new Label();
        _armorLabel.AddThemeFontSizeOverride("font_size", 15);
        armorRow.AddChild(_armorLabel);

        var grid = new GridContainer { Columns = 9 };
        grid.AddThemeConstantOverride("h_separation", 5);
        grid.AddThemeConstantOverride("v_separation", 5);
        left.AddChild(grid);
        for (int i = Inventory.HotbarSize; i < Inventory.TotalSlots; i++) grid.AddChild(MakeSlot(i));

        var hbLabel = new Label { Text = "Hızlı Erişim  (oyundaki alt çubuk)" };
        hbLabel.AddThemeFontSizeOverride("font_size", 16);
        hbLabel.Modulate = new Color(1, 1, 1, 0.8f);
        left.AddChild(hbLabel);
        var hot = new GridContainer { Columns = 9 };
        hot.AddThemeConstantOverride("h_separation", 5);
        left.AddChild(hot);
        for (int i = 0; i < Inventory.HotbarSize; i++) hot.AddChild(MakeSlot(i));

        var trashRow = new HBoxContainer();
        trashRow.AddThemeConstantOverride("separation", 10);
        left.AddChild(trashRow);
        var trash = new TrashSlot { Inv = _inv, Name = "Trash" };
        trashRow.AddChild(trash);
        _trash = trash;
        var tip = new Label { Text = "Sürükle-bırak ile taşı  ·  Sağ tık: yere at (Shift: tümü)\nSayı tuşları: hızlı erişim seçimi" };
        tip.AddThemeFontSizeOverride("font_size", 14);
        tip.Modulate = new Color(1, 1, 1, 0.6f);
        trashRow.AddChild(tip);

        // ---- Sağ: üretim / yaratıcı ----
        var right = new VBoxContainer { CustomMinimumSize = new Vector2(540, 0) };
        right.AddThemeConstantOverride("separation", 8);
        main.AddChild(right);

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 8);
        right.AddChild(tabs);
        _tabCraft = UiKit.Button("Üretim", 20);
        _tabCraft.CustomMinimumSize = new Vector2(200, 42);
        _tabCraft.Pressed += () => { _creativeTab = false; Refresh(); };
        tabs.AddChild(_tabCraft);
        _tabCreative = UiKit.Button("Yaratıcı (tüm bloklar)", 20);
        _tabCreative.CustomMinimumSize = new Vector2(260, 42);
        _tabCreative.Pressed += () => { _creativeTab = true; Refresh(); };
        tabs.AddChild(_tabCreative);

        // üretim paneli
        _craftPane = new VBoxContainer();
        ((VBoxContainer)_craftPane).AddThemeConstantOverride("separation", 8);
        right.AddChild(_craftPane);

        var filters = new HBoxContainer();
        filters.AddThemeConstantOverride("separation", 6);
        string[] names = { "Tümü", "Yapı", "Alet", "Tarım", "Yiyecek" };
        for (int i = 0; i < names.Length; i++)
        {
            int f = i;
            var fb = UiKit.Button(names[i], 16);
            fb.CustomMinimumSize = new Vector2(96, 36);
            fb.Pressed += () => { _filter = f; Refresh(); };
            filters.AddChild(fb);
        }
        _craftPane.AddChild(filters);

        _tableLabel = new Label();
        _tableLabel.AddThemeFontSizeOverride("font_size", 15);
        _craftPane.AddChild(_tableLabel);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(540, 380) };
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        _craftPane.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(list);
        foreach (var r in CraftBook.All) list.AddChild(BuildRow(r));

        // yaratıcı panel
        _creativePane = new VBoxContainer();
        ((VBoxContainer)_creativePane).AddThemeConstantOverride("separation", 8);
        right.AddChild(_creativePane);

        var cfilters = new HBoxContainer();
        cfilters.AddThemeConstantOverride("separation", 4);
        string[] cnames = { "Tümü", "Doğal", "Yapı", "Dekor", "Alet", "Tarım", "Yiyecek" };
        for (int i = 0; i < cnames.Length; i++)
        {
            int f = i;
            var fb = UiKit.Button(cnames[i], 15);
            fb.CustomMinimumSize = new Vector2(72, 34);
            fb.Pressed += () => { _creativeFilter = f; FillCreative(); };
            cfilters.AddChild(fb);
        }
        _creativePane.AddChild(cfilters);

        var cscroll = new ScrollContainer { CustomMinimumSize = new Vector2(540, 400) };
        cscroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        _creativePane.AddChild(cscroll);
        _creativeGrid = new GridContainer { Columns = 8 };
        _creativeGrid.AddThemeConstantOverride("h_separation", 5);
        _creativeGrid.AddThemeConstantOverride("v_separation", 5);
        cscroll.AddChild(_creativeGrid);
        var ctip = new Label { Text = "Tıkla: tam yığın  ·  Sağ tık: 1 adet  ·  Sürükle: hızlı erişim kutusuna bırak" };
        ctip.AddThemeFontSizeOverride("font_size", 14);
        ctip.Modulate = new Color(1, 1, 1, 0.6f);
        _creativePane.AddChild(ctip);

        var back = UiKit.AnimatedButton("Kapat  (Tab)");
        back.CustomMinimumSize = new Vector2(300, 46);
        back.Pressed += Close;
        var backRow = new CenterContainer();
        backRow.AddChild(back);
        outer.AddChild(backRow);
    }

    private TrashSlot _trash;

    public void RefreshArmor()
    {
        var arm = GetArmor?.Invoke();
        if (arm == null || _armorLabel == null) return;
        float pts = 0f;
        for (int i = 0; i < 4; i++)
        {
            _armorUi[i].Tex = _tex;
            _armorUi[i].SetItem(arm[i]);
            if (arm[i] != 0) pts += BlockRegistry.Get(arm[i]).ArmorValue;
        }
        _armorLabel.Text = $"Koruma {pts:F0}  (-%{Mathf.Min(80f, pts * 4f):F0} hasar)";
    }

    private static ItemCategory[] CategoriesFor(int filter) => filter switch
    {
        1 => new[] { ItemCategory.Natural, ItemCategory.Block },
        2 => new[] { ItemCategory.Building },
        3 => new[] { ItemCategory.Decoration },
        4 => new[] { ItemCategory.Tool, ItemCategory.Weapon, ItemCategory.Armor },
        5 => new[] { ItemCategory.Farming },
        6 => new[] { ItemCategory.Food },
        _ => null,
    };

    private void FillCreative()
    {
        if (_creativeGrid == null) return;
        foreach (Node c in _creativeGrid.GetChildren()) { _creativeGrid.RemoveChild(c); c.QueueFree(); }
        var cats = CategoriesFor(_creativeFilter);
        foreach (var b in BlockRegistry.All)
        {
            if (b.IsAir || b.IsLiquid || b.Name == "bedrock") continue;
            if (cats != null && System.Array.IndexOf(cats, b.Category) < 0) continue;
            var s = new CreativeSlot { BlockId = b.Id, Tex = _tex, OnPick = (id, n) => OnCreativePick?.Invoke(id, n) };
            _creativeGrid.AddChild(s);
        }
    }

    private Control MakeSlot(int index)
    {
        var s = new InvSlot { Index = index, Inv = _inv, Tex = _tex };
        s.OnDropOut = (slot, n) => OnDropItem?.Invoke(slot, n);
        _slots.Add(s);
        return s;
    }

    private Control BuildRow(Recipe r)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(520, 54) };
        row.AddThemeConstantOverride("separation", 10);

        var outBlock = BlockRegistry.Get(r.Output.block);
        var icon = new TextureRect
        {
            CustomMinimumSize = new Vector2(44, 44),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            Texture = _tex?.GetIcon(outBlock),
        };
        row.AddChild(icon);

        var info = new VBoxContainer { CustomMinimumSize = new Vector2(330, 0) };
        var title = new Label { Text = $"{r.Output.count}× {CraftBook.DisplayOf(r)}" };
        title.AddThemeFontSizeOverride("font_size", 18);
        info.AddChild(title);
        var needs = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        needs.AddThemeFontSizeOverride("font_size", 13);
        info.AddChild(needs);
        row.AddChild(info);

        var craft = UiKit.Button("Üret", 16);
        craft.CustomMinimumSize = new Vector2(110, 42);
        craft.Pressed += () =>
        {
            if (r.NeedsTable && !(TableNearby?.Invoke() ?? false)) { AudioManager.Play("talk"); return; }
            if (r.NeedsHeat && !(HeatNearby?.Invoke() ?? false)) { AudioManager.Play("talk"); return; }
            if (_inv.Craft(r)) AudioManager.Play("place");
            else AudioManager.Play("talk");
        };
        row.AddChild(craft);

        int group = outBlock.Category switch
        {
            ItemCategory.Tool or ItemCategory.Weapon or ItemCategory.Armor => 2,
            ItemCategory.Farming => 3,
            ItemCategory.Food => 4,
            _ => 1,
        };
        _rows.Add(new RowUi { Recipe = r, Root = row, Craft = craft, Needs = needs, Group = group });
        return row;
    }

    public void Open()
    {
        Visible = true;
        _creativeTab = IsCreative?.Invoke() ?? false;
        _shownMode = -1;
        if (_trash != null) _trash.Inv = _inv;
        Refresh();
        UiKit.FadeIn(_root, 0.15);
    }

    public void Close()
    {
        Visible = false;
        OnClose?.Invoke();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (Visible && e.IsActionPressed(GameInput.Actions.Pause))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        int sel = SelectedHotbar?.Invoke() ?? -1;
        foreach (var s in _slots)
            if (s.Index < Inventory.HotbarSize) s.SetSelected(s.Index == sel);
    }

    public override void _ExitTree()
    {
        if (_inv != null) _inv.Changed -= Refresh;
    }

    private void Refresh()
    {
        if (!Visible || _inv == null) return;
        foreach (var s in _slots) s.Refresh();
        RefreshArmor();

        bool creative = IsCreative?.Invoke() ?? false;
        _tabCreative.Visible = creative;
        if (!creative) _creativeTab = false;
        _craftPane.Visible = !_creativeTab;
        _creativePane.Visible = _creativeTab;
        _trash.Visible = creative;
        _title.Text = creative ? "Envanter  ·  Yaratıcı Mod" : "Envanter";

        int mode = _creativeTab ? 1 : 0;
        if (_creativeTab && (_shownMode != 1 || _creativeGrid.GetChildCount() == 0)) FillCreative();
        _shownMode = mode;
        if (_creativeTab) return;

        bool table = TableNearby?.Invoke() ?? false;
        bool heat = HeatNearby?.Invoke() ?? false;
        _tableLabel.Text = (table ? "✔ Üretim Masası yakında" : "✖ Aletler için Üretim Masası gerekir")
            + "   " + (heat ? "✔ Ateş yakında" : "✖ Pişirmek için Kamp Ateşi gerekir");
        _tableLabel.Modulate = table ? new Color(0.7f, 1f, 0.7f) : new Color(1f, 0.82f, 0.6f);

        foreach (var row in _rows)
        {
            row.Root.Visible = _filter == 0 || row.Group == _filter;
            var sb = new System.Text.StringBuilder("Gerekli: ");
            for (int i = 0; i < row.Recipe.Inputs.Length; i++)
            {
                var (block, count) = row.Recipe.Inputs[i];
                int have = _inv.Count(BlockRegistry.IdOf(block));
                if (i > 0) sb.Append(",  ");
                sb.Append($"{count} {BlockRegistry.Get(block).DisplayName} ({have})");
            }
            if (row.Recipe.NeedsTable) sb.Append("  [Masa]");
            if (row.Recipe.NeedsHeat) sb.Append("  [Ateş]");
            row.Needs.Text = sb.ToString();
            bool can = _inv.CanAfford(row.Recipe);
            bool stationOk = (!row.Recipe.NeedsTable || table) && (!row.Recipe.NeedsHeat || heat);
            row.Craft.Disabled = !(can && stationOk);
            row.Craft.Text = can && !stationOk ? (row.Recipe.NeedsHeat ? "Ateş Lazım" : "Masa Lazım") : "Üret";
            row.Needs.Modulate = can ? new Color(0.7f, 1f, 0.7f) : new Color(1f, 0.7f, 0.7f);
        }
    }
}
