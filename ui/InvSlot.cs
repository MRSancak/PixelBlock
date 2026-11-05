using Godot;
using PixelBlock.Core;

namespace PixelBlock.UI;

public partial class InvSlot : Panel
{
    public int Index;
    public Inventory Inv;
    public BlockTextures Tex;

    public System.Action<int, int> OnDropOut;
    public System.Action<InvSlot> OnShift;
    public System.Func<ushort, bool> CanAccept;
    public bool TakeOnly;

    /// <summary>Sürüklemenin başladığı envanter (konteyner ↔ oyuncu aktarımı için).</summary>
    public static Inventory DragInv;

    private TextureRect _icon;
    private Label _count;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(54, 54);
        AddThemeStyleboxOverride("panel", Style(false));
        _icon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        if (Index < Inventory.HotbarSize)
        {
            var num = new Label { Text = (Index + 1).ToString(), Position = new Vector2(4, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
            num.AddThemeFontSizeOverride("font_size", 11);
            num.Modulate = new Color(1, 1, 1, 0.55f);
            AddChild(num);
        }
        _icon.OffsetLeft = 5; _icon.OffsetTop = 5; _icon.OffsetRight = -5; _icon.OffsetBottom = -5;
        AddChild(_icon);

        _count = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        _count.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _count.OffsetRight = -5; _count.OffsetTop = 32;
        _count.AddThemeFontSizeOverride("font_size", 15);
        _count.AddThemeColorOverride("font_outline_color", Colors.Black);
        _count.AddThemeConstantOverride("outline_size", 3);
        AddChild(_count);
        Refresh();
    }

    public void Refresh()
    {
        if (_icon == null || Inv == null) return;
        ushort id = Inv.SlotId(Index);
        int n = Inv.SlotCount(Index);
        var b = BlockRegistry.Get(id);
        _icon.Texture = id != 0 && Tex != null ? Tex.GetIcon(b) : null;
        _count.Visible = id != 0 && n > 1;
        _count.Text = n.ToString();
        TooltipText = id == 0 ? "" : $"{b.DisplayName}  ×{n}\n{CategoryName(b.Category)}\nSağ tık: yere at"
            + (b.Name == ItemCatalog.WildSeedKey ? $"\nHer bitkinin çıkma şansı: %{ItemCatalog.WildChancePercent:F1}" : "")
            + (b.ArmorSlot > 0 ? $"\nKoruma: {b.ArmorValue:F0}" : "");
    }

    public static string CategoryName(ItemCategory c) => c switch
    {
        ItemCategory.Natural => "Doğal kaynak",
        ItemCategory.Building => "İnşaat malzemesi",
        ItemCategory.Tool => "Alet",
        ItemCategory.Weapon => "Silah",
        ItemCategory.Farming => "Tarım malzemesi",
        ItemCategory.Food => "Yiyecek",
        ItemCategory.Decoration => "Dekorasyon",
        ItemCategory.Armor => "Zırh (sağ tık: giy)",
        _ => "Blok",
    };

    private static StyleBoxFlat Style(bool hot)
    {
        var s = new StyleBoxFlat
        {
            BgColor = hot ? new Color(0.22f, 0.24f, 0.32f, 0.95f) : new Color(0.09f, 0.10f, 0.14f, 0.92f),
            BorderColor = hot ? UiKit.Gold : new Color(0.45f, 0.47f, 0.55f, 0.9f),
            CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5,
        };
        s.BorderWidthLeft = s.BorderWidthRight = s.BorderWidthTop = s.BorderWidthBottom = 2;
        return s;
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Inv == null || Inv.SlotId(Index) == 0) return default;
        var preview = new TextureRect
        {
            Texture = _icon.Texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            CustomMinimumSize = new Vector2(44, 44),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        SetDragPreview(preview);
        DragInv = Inv;
        return Index;
    }

    public void SetSelected(bool on) => AddThemeStyleboxOverride("panel", Style(on));

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (TakeOnly) return false;
        if (data.VariantType == Variant.Type.Dictionary)
            return CanAccept == null;
        if (data.VariantType != Variant.Type.Int) return false;
        if (CanAccept != null && DragInv != null && !CanAccept(DragInv.SlotId(data.AsInt32()))) return false;
        return true;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (Inv == null) return;
        if (data.VariantType == Variant.Type.Dictionary)
        {
            var d = data.AsGodotDictionary();
            if (d.ContainsKey("creative_id"))
            {
                ushort cid = (ushort)d["creative_id"].AsInt32();
                var cb = BlockRegistry.Get(cid);
                Inv.SetSlot(Index, cid, CreativeSlot.StackFor(cb));
                AudioManager.Play("select", 1.1f, -8f);
            }
            return;
        }
        Inventory.Transfer(DragInv ?? Inv, data.AsInt32(), Inv, Index);
        AudioManager.Play("select", 1.1f, -8f);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && OnShift != null
            && Input.IsKeyPressed(Key.Shift) && Inv != null && Inv.SlotId(Index) != 0)
        {
            OnShift(this);
            AcceptEvent();
            return;
        }
        if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right && Inv != null && Inv.SlotId(Index) != 0)
        {
            int n = Input.IsKeyPressed(Key.Shift) ? Inv.SlotCount(Index) : 1;
            OnDropOut?.Invoke(Index, n);
            AcceptEvent();
        }
    }
}
