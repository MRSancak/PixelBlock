using System.Collections.Generic;
using Godot;
using PixelBlock.Core;

namespace PixelBlock.Characters;

/// <summary>
/// Sevimli, bakır golem esintili karakter. Toplam boy 1,75 blok (anten dahil):
/// bacak 0,50 + gövde 0,50 + büyük kafa 0,50 + anten 0,25.
/// </summary>
public sealed partial class AvatarModel : Node3D
{
    public const float TotalHeight = 1.75f;

    private const float HipY = 0.50f;
    private const float NeckY = 1.00f;
    private const float HeadSize = 0.50f;
    private const float S = HeadSize / 0.34f; // eski kafa ölçüsünden ölçekleme

    private Node3D _root, _head, _armL, _armR, _legL, _legR, _torso;
    private float _phase, _actT;
    private CharacterProfile _profile = new();
    private readonly List<Node> _armorNodes = new();
    private readonly ushort[] _armorIds = new ushort[4];

    public override void _Ready()
    {
        if (_root == null) Build(_profile);
    }

    public void Build(CharacterProfile p)
    {
        _profile = p ?? new CharacterProfile();
        if (_root != null) { _root.QueueFree(); }
        _armorNodes.Clear();
        _root = new Node3D { Name = "Avatar" };
        AddChild(_root);

        var pr = _profile;

        // ---- gövde ----
        _torso = new Node3D { Position = new Vector3(0, HipY, 0) };
        _root.AddChild(_torso);
        Box(_torso, new Vector3(0.46f, 0.50f, 0.28f), new Vector3(0, 0.25f, 0), pr.Shirt);
        if (pr.TopStyle == 2)
        {
            Box(_torso, new Vector3(0.07f, 0.50f, 0.30f), new Vector3(0, 0.25f, 0), Darker(pr.Shirt, 0.65f));
            Box(_torso, new Vector3(0.46f, 0.08f, 0.31f), new Vector3(0, 0.46f, 0), Darker(pr.Shirt, 0.8f));
        }
        else if (pr.TopStyle == 1)
            Box(_torso, new Vector3(0.48f, 0.05f, 0.30f), new Vector3(0, 0.03f, 0), Darker(pr.Shirt, 0.8f));
        // golem göbek kapağı + patina lekesi
        Box(_torso, new Vector3(0.12f, 0.12f, 0.02f), new Vector3(0, 0.26f, 0.145f), Darker(pr.Shirt, 0.7f));
        Box(_torso, new Vector3(0.1f, 0.08f, 0.02f), new Vector3(-0.12f, 0.1f, 0.145f), Patina);

        // ---- kafa ----
        _head = new Node3D { Position = new Vector3(0, NeckY, 0) };
        _root.AddChild(_head);
        HBox(new Vector3(0.34f, 0.34f, 0.34f), new Vector3(0, 0.17f, 0), pr.Skin);
        Color eyeDark = new(0.05f, 0.05f, 0.07f);

        HBox(new Vector3(0.09f, 0.08f, 0.02f), new Vector3(0.08f, 0.2f, 0.175f), Colors.White);
        HBox(new Vector3(0.09f, 0.08f, 0.02f), new Vector3(-0.08f, 0.2f, 0.175f), Colors.White);
        HBox(new Vector3(0.05f, 0.07f, 0.025f), new Vector3(0.08f, 0.2f, 0.18f), pr.Eye);
        HBox(new Vector3(0.05f, 0.07f, 0.025f), new Vector3(-0.08f, 0.2f, 0.18f), pr.Eye);
        HBox(new Vector3(0.02f, 0.025f, 0.03f), new Vector3(0.095f, 0.225f, 0.185f), Colors.White);
        HBox(new Vector3(0.02f, 0.025f, 0.03f), new Vector3(-0.065f, 0.225f, 0.185f), Colors.White);
        // yanak kızarıklığı
        Color blush = new(0.95f, 0.55f, 0.55f);
        HBox(new Vector3(0.06f, 0.035f, 0.02f), new Vector3(0.115f, 0.115f, 0.175f), blush);
        HBox(new Vector3(0.06f, 0.035f, 0.02f), new Vector3(-0.115f, 0.115f, 0.175f), blush);
        // golem burnu
        HBox(new Vector3(0.07f, 0.07f, 0.06f), new Vector3(0, 0.145f, 0.2f), Darker(pr.Skin, 0.78f));

        switch (pr.Face)
        {
            case 0: HBox(new Vector3(0.1f, 0.025f, 0.02f), new Vector3(0, 0.07f, 0.175f), new Color(0.6f, 0.2f, 0.2f)); break;
            case 1: HBox(new Vector3(0.08f, 0.02f, 0.02f), new Vector3(0, 0.07f, 0.175f), eyeDark); break;
            case 2:
                HBox(new Vector3(0.3f, 0.12f, 0.04f), new Vector3(0, 0.05f, 0.16f), pr.Hair);
                HBox(new Vector3(0.08f, 0.02f, 0.02f), new Vector3(0, 0.09f, 0.185f), eyeDark);
                break;
            default: HBox(new Vector3(0.05f, 0.06f, 0.02f), new Vector3(0, 0.065f, 0.175f), new Color(0.5f, 0.15f, 0.15f)); break;
        }

        // yan patina lekeleri
        HBox(new Vector3(0.02f, 0.09f, 0.1f), new Vector3(0.175f, 0.12f, -0.03f), Patina);
        HBox(new Vector3(0.1f, 0.02f, 0.08f), new Vector3(-0.06f, 0.345f, -0.06f), Patina);

        switch (pr.HairStyle)
        {
            case 0:
                HBox(new Vector3(0.36f, 0.1f, 0.36f), new Vector3(0, 0.37f, 0), pr.Hair);
                HBox(new Vector3(0.36f, 0.2f, 0.06f), new Vector3(0, 0.27f, -0.15f), pr.Hair);
                break;
            case 1:
                HBox(new Vector3(0.36f, 0.1f, 0.36f), new Vector3(0, 0.37f, 0), pr.Hair);
                HBox(new Vector3(0.38f, 0.5f, 0.08f), new Vector3(0, 0.12f, -0.15f), pr.Hair);
                HBox(new Vector3(0.06f, 0.3f, 0.3f), new Vector3(0.18f, 0.22f, -0.02f), pr.Hair);
                HBox(new Vector3(0.06f, 0.3f, 0.3f), new Vector3(-0.18f, 0.22f, -0.02f), pr.Hair);
                break;
            case 2:
                HBox(new Vector3(0.46f, 0.2f, 0.46f), new Vector3(0, 0.4f, -0.02f), pr.Hair);
                HBox(new Vector3(0.44f, 0.3f, 0.1f), new Vector3(0, 0.28f, -0.17f), pr.Hair);
                break;
            case 4:
                HBox(new Vector3(0.36f, 0.1f, 0.36f), new Vector3(0, 0.37f, 0), pr.Hair);
                HBox(new Vector3(0.12f, 0.12f, 0.1f), new Vector3(0, 0.36f, -0.22f), pr.Hair);
                HBox(new Vector3(0.1f, 0.34f, 0.1f), new Vector3(0, 0.18f, -0.26f), pr.Hair);
                break;
        }

        switch (pr.HatStyle)
        {
            case 1:
                HBox(new Vector3(0.5f, 0.04f, 0.5f), new Vector3(0, 0.4f, 0), pr.HatColor);
                HBox(new Vector3(0.32f, 0.2f, 0.32f), new Vector3(0, 0.52f, 0), pr.HatColor);
                break;
            case 2:
                HBox(new Vector3(0.37f, 0.12f, 0.37f), new Vector3(0, 0.42f, 0), pr.HatColor);
                HBox(new Vector3(0.3f, 0.03f, 0.2f), new Vector3(0, 0.38f, 0.26f), Darker(pr.HatColor, 0.8f));
                break;
            case 3:
                HBox(new Vector3(0.36f, 0.1f, 0.36f), new Vector3(0, 0.4f, 0), new Color(0.95f, 0.8f, 0.2f));
                for (int i = 0; i < 4; i++)
                {
                    float x = (i % 2 == 0 ? -1 : 1) * 0.12f, z = (i < 2 ? -1 : 1) * 0.12f;
                    HBox(new Vector3(0.07f, 0.12f, 0.07f), new Vector3(x, 0.5f, z), new Color(0.95f, 0.8f, 0.2f));
                }
                break;
            default:
                // bakır golem anteni (paratoner): şapka yokken görünür
                Box(_head, new Vector3(0.035f, 0.17f, 0.035f), new Vector3(0, HeadSize + 0.085f + 0.02f, 0), new Color(0.72f, 0.42f, 0.26f));
                Box(_head, new Vector3(0.085f, 0.085f, 0.085f), new Vector3(0, HeadSize + 0.17f, 0), new Color(0.35f, 0.80f, 0.70f));
                break;
        }

        switch (pr.AccStyle)
        {
            case 1:
                HBox(new Vector3(0.12f, 0.1f, 0.02f), new Vector3(0.08f, 0.2f, 0.19f), pr.AccColor);
                HBox(new Vector3(0.12f, 0.1f, 0.02f), new Vector3(-0.08f, 0.2f, 0.19f), pr.AccColor);
                HBox(new Vector3(0.36f, 0.02f, 0.02f), new Vector3(0, 0.23f, 0.19f), pr.AccColor);
                break;
            case 2:
                Box(_torso, new Vector3(0.38f, 0.1f, 0.34f), new Vector3(0, 0.5f, 0), pr.AccColor);
                Box(_torso, new Vector3(0.1f, 0.26f, 0.06f), new Vector3(0.1f, 0.36f, 0.17f), pr.AccColor);
                break;
            case 3:
                Box(_torso, new Vector3(0.34f, 0.4f, 0.16f), new Vector3(0, 0.27f, -0.22f), pr.AccColor);
                break;
        }

        _armL = MakeArm(-0.30f, pr);
        _armR = MakeArm(0.30f, pr);
        _legL = MakeLeg(-0.12f, pr);
        _legR = MakeLeg(0.12f, pr);

        ApplyArmor();
    }

    private static readonly Color Patina = new(0.30f, 0.74f, 0.62f);

    private Node3D MakeArm(float x, CharacterProfile pr)
    {
        var piv = new Node3D { Position = new Vector3(x, 0.96f, 0) };
        _root.AddChild(piv);
        bool longSleeve = pr.TopStyle != 0;
        Box(piv, new Vector3(0.15f, 0.42f, 0.15f), new Vector3(0, -0.21f, 0), longSleeve ? pr.Shirt : pr.Skin);
        if (!longSleeve) Box(piv, new Vector3(0.16f, 0.18f, 0.16f), new Vector3(0, -0.09f, 0), pr.Shirt);
        else Box(piv, new Vector3(0.16f, 0.1f, 0.16f), new Vector3(0, -0.38f, 0), pr.Skin);
        return piv;
    }

    private Node3D MakeLeg(float x, CharacterProfile pr)
    {
        var piv = new Node3D { Position = new Vector3(x, HipY, 0) };
        _root.AddChild(piv);
        bool shorts = pr.BottomStyle == 1;
        Box(piv, new Vector3(0.2f, 0.5f, 0.2f), new Vector3(0, -0.25f, 0), shorts ? pr.Skin : pr.Pants);
        if (shorts) Box(piv, new Vector3(0.21f, 0.24f, 0.21f), new Vector3(0, -0.12f, 0), pr.Pants);
        Box(piv, new Vector3(0.23f, 0.1f, 0.31f), new Vector3(0, -0.45f, 0.04f), pr.Shoes);
        return piv;
    }

    // ---- zırh görünümü ----

    public void SetArmor(ushort[] ids)
    {
        for (int i = 0; i < 4; i++) _armorIds[i] = ids != null && i < ids.Length ? ids[i] : (ushort)0;
        ApplyArmor();
    }

    private void ApplyArmor()
    {
        foreach (var n in _armorNodes) if (IsInstanceValid(n)) n.QueueFree();
        _armorNodes.Clear();
        if (_head == null || _torso == null || _legL == null) return;

        if (_armorIds[0] != 0)
        {
            Color c = ColorOf(_armorIds[0]);
            _armorNodes.Add(Box(_head, new Vector3(0.54f, 0.2f, 0.54f), new Vector3(0, 0.42f, 0), c));
            _armorNodes.Add(Box(_head, new Vector3(0.54f, 0.3f, 0.06f), new Vector3(0, 0.3f, -0.24f), Darker(c, 0.85f)));
            _armorNodes.Add(Box(_head, new Vector3(0.06f, 0.3f, 0.5f), new Vector3(0.26f, 0.3f, 0), Darker(c, 0.85f)));
            _armorNodes.Add(Box(_head, new Vector3(0.06f, 0.3f, 0.5f), new Vector3(-0.26f, 0.3f, 0), Darker(c, 0.85f)));
        }
        if (_armorIds[1] != 0)
        {
            Color c = ColorOf(_armorIds[1]);
            _armorNodes.Add(Box(_torso, new Vector3(0.5f, 0.46f, 0.33f), new Vector3(0, 0.25f, 0), c));
            _armorNodes.Add(Box(_torso, new Vector3(0.12f, 0.4f, 0.02f), new Vector3(0, 0.25f, 0.17f), Darker(c, 0.8f)));
            _armorNodes.Add(Box(_armL, new Vector3(0.2f, 0.14f, 0.2f), new Vector3(0, -0.04f, 0), c));
            _armorNodes.Add(Box(_armR, new Vector3(0.2f, 0.14f, 0.2f), new Vector3(0, -0.04f, 0), c));
        }
        if (_armorIds[2] != 0)
        {
            Color c = ColorOf(_armorIds[2]);
            _armorNodes.Add(Box(_torso, new Vector3(0.5f, 0.1f, 0.32f), new Vector3(0, 0.03f, 0), c));
            _armorNodes.Add(Box(_legL, new Vector3(0.235f, 0.3f, 0.235f), new Vector3(0, -0.15f, 0), c));
            _armorNodes.Add(Box(_legR, new Vector3(0.235f, 0.3f, 0.235f), new Vector3(0, -0.15f, 0), c));
        }
        if (_armorIds[3] != 0)
        {
            Color c = ColorOf(_armorIds[3]);
            _armorNodes.Add(Box(_legL, new Vector3(0.25f, 0.16f, 0.35f), new Vector3(0, -0.42f, 0.04f), c));
            _armorNodes.Add(Box(_legR, new Vector3(0.25f, 0.16f, 0.35f), new Vector3(0, -0.42f, 0.04f), c));
        }
    }

    private static Color ColorOf(ushort id) => BlockRegistry.Get(id).IconColor;

    private static Color Darker(Color c, float k) => new(c.R * k, c.G * k, c.B * k, 1f);

    private void HBox(Vector3 size, Vector3 pos, Color c) => Box(_head, size * S, pos * S, c);

    private static MeshInstance3D Box(Node3D parent, Vector3 size, Vector3 pos, Color c)
    {
        var mi = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = PixelMat.Get(c),
            Position = pos,
        };
        parent.AddChild(mi);
        return mi;
    }

    public void Tick(float dt, float hSpeed, bool onFloor, bool crouch, bool sprint, bool acting)
    {
        if (_root == null || _legL == null) return;
        bool moving = hSpeed > 0.3f && onFloor;
        float amp = moving ? Mathf.Clamp(hSpeed / 4.2f, 0.3f, 1.3f) * (sprint ? 1.15f : 0.85f) : 0f;
        if (moving) _phase += hSpeed * dt * 2.4f;
        float sw = Mathf.Sin(_phase) * amp;

        float legL = sw, legR = -sw, armL = -sw, armR = sw;
        if (!onFloor) { legL = 0.5f; legR = -0.35f; armL = -2.4f; armR = -2.4f; }
        _legL.Rotation = new Vector3(legL, 0, 0);
        _legR.Rotation = new Vector3(legR, 0, 0);
        _armL.Rotation = new Vector3(armL, 0, 0);

        if (acting) _actT += dt * 12f; else _actT = 0f;
        float swing = acting ? -1.3f - Mathf.Abs(Mathf.Sin(_actT)) * 1.0f : armR;
        _armR.Rotation = new Vector3(swing, 0, 0);

        // sevimli hafif salınan baş
        float bob = moving ? Mathf.Sin(_phase * 2f) * 0.02f : Mathf.Sin(Time.GetTicksMsec() * 0.002f) * 0.008f;
        float dropY = crouch ? 0.22f : 0f;
        _root.Position = new Vector3(0, -dropY * 0.6f, 0);
        _torso.Rotation = new Vector3(crouch ? 0.35f : 0f, 0, 0);
        _head.Rotation = new Vector3((crouch ? -0.2f : 0f) + bob, 0, moving ? Mathf.Sin(_phase) * 0.04f : 0f);
    }

    public void LookPitch(float pitch)
    {
        if (_head != null) _head.Rotation = new Vector3(-pitch * 0.6f, 0, 0);
    }
}
