using Godot;
using PixelBlock.Characters;
using PixelBlock.Core;

namespace PixelBlock.Network;

public sealed partial class RemotePlayer : Node3D
{
    public int PeerId;
    public string PlayerName = "Oyuncu";

    private AvatarModel _avatar;
    private Label3D _label;
    private Vector3 _target;
    private float _targetYaw, _pitch;
    private int _flags;
    private bool _first = true;
    private Vector3 _last;
    private float _speed;

    public void Setup(int peerId, CharacterProfile profile)
    {
        PeerId = peerId;
        PlayerName = profile.PlayerName;
        Name = "Remote_" + peerId;
        _avatar = new AvatarModel { Name = "Avatar" };
        AddChild(_avatar);
        _avatar.Build(profile);
        _label = new Label3D
        {
            Text = PlayerName,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            PixelSize = 0.008f,
            FontSize = 56,
            OutlineSize = 10,
            Position = new Vector3(0f, 2.15f, 0f),
        };
        AddChild(_label);
    }

    public void SetState(Vector3 pos, float yaw, float pitch, int flags)
    {
        _target = pos;
        _targetYaw = yaw;
        _pitch = pitch;
        _flags = flags;
        if (_first) { GlobalPosition = pos; _last = pos; _first = false; }
    }

    public override void _Process(double delta)
    {
        if (_avatar == null) return;
        float dt = (float)delta;
        Vector3 p = GlobalPosition.Lerp(_target, Mathf.Min(1f, dt * 12f));
        GlobalPosition = p;

        _avatar.Rotation = new Vector3(0f, Mathf.LerpAngle(_avatar.Rotation.Y, _targetYaw + Mathf.Pi, Mathf.Min(1f, dt * 14f)), 0f);

        Vector3 d = p - _last;
        _last = p;
        float inst = new Vector2(d.X, d.Z).Length() / Mathf.Max(0.001f, dt);
        _speed = Mathf.Lerp(_speed, inst, Mathf.Min(1f, dt * 8f));

        bool crouch = (_flags & 1) != 0, sprint = (_flags & 2) != 0, acting = (_flags & 4) != 0, floor = (_flags & 8) != 0;
        _avatar.Tick(dt, _speed, floor, crouch, sprint, acting);
        _avatar.LookPitch(_pitch);
    }
}
