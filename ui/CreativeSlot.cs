using Godot;
using PixelBlock.Core;

namespace PixelBlock.UI;

public partial class CreativeSlot : Panel
{
    public ushort BlockId;
    public BlockTextures Tex;
    public System.Action<ushort, int> OnPick;

    private TextureRect _icon;

    public static int StackFor(BlockType b)
        => b.Category == ItemCategory.Tool || b.Category == ItemCategory.Weapon || b.Category == ItemCategory.Armor ? 1 : Inventory.StackMax;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(54, 54);
        var b = BlockRegistry.Get(BlockId);
        var sb = new StyleBoxFlat
        {
            BgColor = new Color(0.09f, 0.10f, 0.14f, 0.92f),
            BorderColor = new Color(0.45f, 0.47f, 0.55f, 0.9f),
            CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5,
        };
        sb.BorderWidthLeft = sb.BorderWidthRight = sb.BorderWidthTop = sb.BorderWidthBottom = 2;
        AddThemeStyleboxOverride("panel", sb);
        _icon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Texture = Tex?.GetIcon(b),
        };
        _icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _icon.OffsetLeft = 5; _icon.OffsetTop = 5; _icon.OffsetRight = -5; _icon.OffsetBottom = -5;
        AddChild(_icon);
        TooltipText = $"{b.DisplayName}\nTıkla: al  ·  Sağ tık: 1 adet\nSürükle: hızlı erişime bırak";
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true } mb)
        {
            var b = BlockRegistry.Get(BlockId);
            if (mb.ButtonIndex == MouseButton.Left) { OnPick?.Invoke(BlockId, StackFor(b)); AcceptEvent(); }
            else if (mb.ButtonIndex == MouseButton.Right) { OnPick?.Invoke(BlockId, 1); AcceptEvent(); }
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        SetDragPreview(new TextureRect
        {
            Texture = _icon.Texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            CustomMinimumSize = new Vector2(44, 44),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        });
        var d = new Godot.Collections.Dictionary { { "creative_id", (int)BlockId } };
        return d;
    }
}

public partial class TrashSlot : Panel
{
    public Inventory Inv;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(54, 54);
        var sb = new StyleBoxFlat
        {
            BgColor = new Color(0.25f, 0.07f, 0.07f, 0.92f),
            BorderColor = new Color(0.85f, 0.3f, 0.3f, 0.9f),
            CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5,
        };
        sb.BorderWidthLeft = sb.BorderWidthRight = sb.BorderWidthTop = sb.BorderWidthBottom = 2;
        AddThemeStyleboxOverride("panel", sb);
        var l = new Label { Text = "Çöp", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        l.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        l.AddThemeFontSizeOverride("font_size", 14);
        AddChild(l);
        TooltipText = "Eşyayı buraya sürükle: silinir";
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) => data.VariantType == Variant.Type.Int;

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (InvSlot.DragInv != null && InvSlot.DragInv != Inv) return;
        Inv?.SetSlot(data.AsInt32(), 0, 0);
        AudioManager.Play("break_Cloth", 1f, -8f);
    }
}

public partial class ArmorSlotUi : Panel
{
    public int Slot;
    public string SlotName = "";
    public BlockTextures Tex;
    public System.Action OnClick;
    public System.Action<int> OnDropFrom;

    private TextureRect _icon;
    private Label _hint;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(48, 48);
        var sb = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.10f, 0.16f, 0.92f),
            BorderColor = new Color(0.6f, 0.5f, 0.8f, 0.9f),
            CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5,
        };
        sb.BorderWidthLeft = sb.BorderWidthRight = sb.BorderWidthTop = sb.BorderWidthBottom = 2;
        AddThemeStyleboxOverride("panel", sb);
        _icon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _icon.OffsetLeft = 4; _icon.OffsetTop = 4; _icon.OffsetRight = -4; _icon.OffsetBottom = -4;
        AddChild(_icon);
        _hint = new Label { Text = SlotName.Length > 0 ? SlotName.Substring(0, 1) : "", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        _hint.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _hint.Modulate = new Color(1, 1, 1, 0.25f);
        _hint.AddThemeFontSizeOverride("font_size", 20);
        AddChild(_hint);
        TooltipText = SlotName + " (tıkla: çıkar · eşyayı sürükle: giy)";
    }

    public void SetItem(ushort id)
    {
        if (_icon == null) return;
        var b = BlockRegistry.Get(id);
        _icon.Texture = id != 0 && Tex != null ? Tex.GetIcon(b) : null;
        _hint.Visible = id == 0;
        TooltipText = id == 0 ? SlotName + " (eşyayı sürükle: giy)" : $"{b.DisplayName}\nKoruma: {b.ArmorValue:F0}\nTıkla: çıkar";
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { OnClick?.Invoke(); AcceptEvent(); }
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) => data.VariantType == Variant.Type.Int;

    public override void _DropData(Vector2 atPosition, Variant data) => OnDropFrom?.Invoke(data.AsInt32());
}
