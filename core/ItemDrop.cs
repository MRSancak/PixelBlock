using Godot;

namespace PixelBlock.Core;

public partial class ItemDrop : Node3D
{
    public ushort Id;
    public int Count = 1;
    public VoxelWorld World;
    public Node3D Player;
    public Inventory Inv;

    private float _age;
    private float _vy;
    private Vector3 _vel;
    private MeshInstance3D _mesh;
    private const float Lifetime = 300f;

    public static ItemDrop Spawn(Node parent, VoxelWorld world, Node3D player, Inventory inv,
        ushort id, int count, Vector3 pos, Vector3 impulse)
    {
        if (id == 0 || count <= 0 || parent == null) return null;
        var d = new ItemDrop { Id = id, Count = count, World = world, Player = player, Inv = inv, Name = "ItemDrop" };
        parent.AddChild(d);
        d.GlobalPosition = pos;
        d._vel = new Vector3(impulse.X, 0f, impulse.Z);
        d._vy = impulse.Y;
        return d;
    }

    public override void _Ready()
    {
        var b = BlockRegistry.Get(Id);
        Color col = b.Placeable ? (World?.Textures?.AverageColor(b) ?? b.IconColor) : b.IconColor;
        _mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.28f, 0.28f, 0.28f) },
            MaterialOverride = PixelMat.Get(col),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_mesh);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _age += dt;
        if (_age > Lifetime) { QueueFree(); return; }

        Vector3 p = GlobalPosition;
        if (World != null)
        {
            int cx = Mathf.FloorToInt(p.X), cz = Mathf.FloorToInt(p.Z);
            int below = Mathf.FloorToInt(p.Y - 0.18f);
            bool ground = World.GetBlock(cx, below, cz).Solid;
            if (ground && _vy <= 0f)
            {
                _vy = 0f;
                p.Y = below + 1f + 0.18f;
                _vel = _vel.Lerp(Vector3.Zero, dt * 6f);
            }
            else _vy -= 18f * dt;
        }
        else _vy -= 18f * dt;

        p += new Vector3(_vel.X, _vy, _vel.Z) * dt;
        GlobalPosition = p;

        if (_mesh != null)
        {
            _mesh.RotateY(dt * 1.6f);
            _mesh.Position = new Vector3(0f, 0.12f + Mathf.Sin(_age * 3f) * 0.05f, 0f);
        }

        if (_age > 0.7f && Player != null && IsInstanceValid(Player) && Inv != null)
        {
            Vector3 target = Player.GlobalPosition + new Vector3(0f, 0.9f, 0f);
            float dist = GlobalPosition.DistanceTo(target);
            if (dist < 1.7f)
            {
                int left = Inv.Add(Id, Count);
                if (left < Count) AudioManager.Play("select", 1.2f, -6f);
                if (left <= 0) { QueueFree(); return; }
                Count = left;
            }
        }
    }
}
