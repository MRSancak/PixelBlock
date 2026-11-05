using System.Collections.Generic;
using Godot;

namespace PixelBlock.Core;

/// <summary>
/// Çimen üstünde yabani çiçekler ve uzun otlar. Yerleri seed + koordinattan hesaplanır (kayıt gerekmez).
/// Toplanınca yabani tohum düşürür; bir süre sonra yeniden büyür.
/// </summary>
public sealed partial class WildPlants : Node3D
{
    public static WildPlants Instance { get; private set; }

    public VoxelWorld World;
    public Node3D Player;
    public Inventory Inv;
    public int Seed;

    public struct Plant
    {
        public int Kind;
        public Vector3 Pos;
        public int Cx, Cz;
    }

    private sealed class KindDef
    {
        public string Name;
        public Mesh Mesh;
        public float SeedChance;
        public Color Spark;
    }

    private const int Radius = 28;
    private const float RegrowSeconds = 420f;

    private KindDef[] _kinds;
    private readonly List<Plant> _plants = new();
    private readonly Dictionary<(int, int), double> _picked = new();
    private readonly MultiMeshInstance3D[] _mmi = new MultiMeshInstance3D[6];
    private StandardMaterial3D _mat;
    private Vector3 _lastBuild = new(float.MaxValue, 0, 0);
    private int _lastChunks = -1;
    private float _timer;
    private bool _dirty;
    private double _now;
    private ushort _grass;
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        Instance = this;
        _rng.Randomize();
        _grass = BlockRegistry.IdOf("grass");
        _mat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 1f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        _kinds = new[]
        {
            new KindDef { Name = "Gelincik", Mesh = BuildFlower(new Color(0.88f, 0.10f, 0.10f), new Color(0.15f, 0.08f, 0.08f), 0.42f), SeedChance = 1f, Spark = new Color(0.9f, 0.1f, 0.1f) },
            new KindDef { Name = "Papatya", Mesh = BuildFlower(new Color(0.98f, 0.98f, 0.96f), new Color(1.0f, 0.82f, 0.15f), 0.40f), SeedChance = 1f, Spark = Colors.White },
            new KindDef { Name = "Lavanta", Mesh = BuildSpike(new Color(0.58f, 0.36f, 0.82f), 0.62f), SeedChance = 1f, Spark = new Color(0.6f, 0.4f, 0.85f) },
            new KindDef { Name = "Karahindiba", Mesh = BuildFlower(new Color(1.0f, 0.86f, 0.1f), new Color(0.95f, 0.6f, 0.05f), 0.36f), SeedChance = 1f, Spark = new Color(1f, 0.85f, 0.1f) },
            new KindDef { Name = "Peygamber Çiçeği", Mesh = BuildFlower(new Color(0.25f, 0.45f, 0.95f), new Color(0.12f, 0.15f, 0.45f), 0.46f), SeedChance = 1f, Spark = new Color(0.3f, 0.5f, 0.95f) },
            new KindDef { Name = "Uzun Ot", Mesh = BuildTallGrass(), SeedChance = 0.4f, Spark = new Color(0.4f, 0.8f, 0.3f) },
        };
        for (int i = 0; i < _mmi.Length; i++)
        {
            _mmi[i] = new MultiMeshInstance3D { Name = "Plants_" + i, MaterialOverride = _mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(_mmi[i]);
        }
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public override void _Process(double delta)
    {
        _now += delta;
        if (World == null || Player == null || !IsInstanceValid(Player)) return;
        _timer += (float)delta;
        if (_timer < 0.6f && !_dirty) return;
        _timer = 0f;

        Vector3 p = Player.GlobalPosition;
        bool moved = new Vector2(p.X - _lastBuild.X, p.Z - _lastBuild.Z).Length() > 6f || Mathf.Abs(p.Y - _lastBuild.Y) > 6f;
        bool chunksChanged = World.ChunkCount != _lastChunks;
        if (moved || _dirty || chunksChanged)
        {
            Rebuild(p);
            _lastBuild = p;
            _lastChunks = World.ChunkCount;
            _dirty = false;
        }
    }

    private static uint H(int x, int z, int salt) => ValueNoise2D.Hash(x, z, salt);

    private void Rebuild(Vector3 center)
    {
        _plants.Clear();
        int px = Mathf.FloorToInt(center.X), pz = Mathf.FloorToInt(center.Z), py = Mathf.FloorToInt(center.Y);
        for (int x = px - Radius; x <= px + Radius; x++)
        for (int z = pz - Radius; z <= pz + Radius; z++)
        {
            int kind = KindAt(x, z);
            if (kind < 0) continue;
            if (_picked.TryGetValue((x, z), out double t) && _now - t < RegrowSeconds) continue;

            int sy = SurfaceY(x, py, z);
            if (sy == int.MinValue) continue;

            uint h = H(x, z, Seed ^ 0x51F1);
            float ox = ((h & 0xFF) / 255f - 0.5f) * 0.6f;
            float oz = (((h >> 8) & 0xFF) / 255f - 0.5f) * 0.6f;
            _plants.Add(new Plant { Kind = kind, Cx = x, Cz = z, Pos = new Vector3(x + 0.5f + ox, sy + 1f, z + 0.5f + oz) });
        }
        Apply();
    }

    /// <summary>Hücrede hangi bitki var? -1 = yok. Çiçekler 6x6'lık yama halinde kümelenir.</summary>
    private int KindAt(int x, int z)
    {
        uint hp = H(Mathf.FloorToInt(x / 6f), Mathf.FloorToInt(z / 6f), Seed ^ 0x9A71);
        bool flowerPatch = (hp % 100u) < 22u;
        uint hc = H(x, z, Seed ^ 0x3C07);
        if (flowerPatch)
        {
            if ((hc % 100u) < 30u) return (int)((hp >> 8) % 5u);
        }
        else if ((hc % 1000u) < 14u) return (int)((hc >> 10) % 5u);
        if (((hc >> 12) % 100u) < 11u) return 5;
        return -1;
    }

    private int SurfaceY(int x, int py, int z)
    {
        for (int y = py + 12; y >= py - 16; y--)
        {
            ushort id = World.GetBlockId(x, y, z);
            if (id == 0) continue;
            if (id != _grass) return int.MinValue;
            return World.GetBlockId(x, y + 1, z) == 0 ? y : int.MinValue;
        }
        return int.MinValue;
    }

    private void Apply()
    {
        var lists = new List<Transform3D>[6];
        for (int i = 0; i < 6; i++) lists[i] = new List<Transform3D>();
        foreach (var pl in _plants)
        {
            uint h = H(pl.Cx, pl.Cz, Seed ^ 0x77);
            float yaw = (h & 0x3FF) / 1023f * Mathf.Tau;
            float sc = 0.85f + ((h >> 10) & 0xFF) / 255f * 0.35f;
            var basis = Basis.FromEuler(new Vector3(0, yaw, 0)).Scaled(new Vector3(sc, sc, sc));
            lists[pl.Kind].Add(new Transform3D(basis, pl.Pos));
        }
        for (int k = 0; k < 6; k++)
        {
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = false,
                Mesh = _kinds[k].Mesh,
                InstanceCount = lists[k].Count,
            };
            for (int i = 0; i < lists[k].Count; i++) mm.SetInstanceTransform(i, lists[k][i]);
            _mmi[k].Multimesh = mm;
        }
    }

    // ---- etkileşim ----

    public bool Probe(Vector3 origin, Vector3 dir, float maxDist, out Plant found)
    {
        found = default;
        float best = float.MaxValue;
        bool ok = false;
        foreach (var pl in _plants)
        {
            Vector3 c = pl.Pos + new Vector3(0, pl.Kind == 5 ? 0.45f : 0.28f, 0);
            Vector3 to = c - origin;
            float t = to.Dot(dir);
            if (t < 0.2f || t > maxDist) continue;
            float d = (to - dir * t).Length();
            if (d > (pl.Kind == 5 ? 0.42f : 0.32f)) continue;
            if (t < best) { best = t; found = pl; ok = true; }
        }
        return ok;
    }

    public void Harvest(Plant pl, Inventory inv, Node dropParent)
    {
        _picked[(pl.Cx, pl.Cz)] = _now;
        _dirty = true;
        _plants.Remove(pl);
        var def = _kinds[pl.Kind];

        int n = 0;
        if (_rng.Randf() < def.SeedChance) n = 1;
        if (pl.Kind < 5 && _rng.Randf() < 0.25f) n++;

        Vector3 at = pl.Pos + new Vector3(0, 0.4f, 0);
        Fx.Burst(at, FxKind.Sparkle, def.Spark, 8);
        AudioManager.Play("break_Grass", 1.2f, -6f);
        if (n <= 0) return;

        ushort seed = BlockRegistry.IdOf(ItemCatalog.WildSeedKey);
        if (seed == 0) return;
        int left = inv != null ? inv.Add(seed, n) : n;
        if (left > 0 && dropParent != null)
            ItemDrop.Spawn(dropParent, World, Player, inv, seed, left, at, new Vector3(0, 2.5f, 0));
    }

    // ---- mesh üretimi ----

    private static void AddBox(SurfaceTool st, Vector3 c, Vector3 s, Color col)
    {
        Vector3 h = s * 0.5f;
        Vector3[] v =
        {
            c + new Vector3(-h.X, -h.Y, -h.Z), c + new Vector3(h.X, -h.Y, -h.Z),
            c + new Vector3(h.X, h.Y, -h.Z), c + new Vector3(-h.X, h.Y, -h.Z),
            c + new Vector3(-h.X, -h.Y, h.Z), c + new Vector3(h.X, -h.Y, h.Z),
            c + new Vector3(h.X, h.Y, h.Z), c + new Vector3(-h.X, h.Y, h.Z),
        };
        int[][] faces =
        {
            new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 4, 7, 3 },
            new[] { 1, 2, 6, 5 }, new[] { 3, 7, 6, 2 }, new[] { 0, 1, 5, 4 },
        };
        Vector3[] normals = { Vector3.Back * -1, Vector3.Back, Vector3.Left, Vector3.Right, Vector3.Up, Vector3.Down };
        for (int f = 0; f < 6; f++)
        {
            int[] q = faces[f];
            Color shade = col * (f == 4 ? 1.08f : f == 5 ? 0.7f : 0.92f);
            shade.A = 1f;
            st.SetColor(shade); st.SetNormal(normals[f]); st.AddVertex(v[q[0]]);
            st.SetColor(shade); st.SetNormal(normals[f]); st.AddVertex(v[q[1]]);
            st.SetColor(shade); st.SetNormal(normals[f]); st.AddVertex(v[q[2]]);
            st.SetColor(shade); st.SetNormal(normals[f]); st.AddVertex(v[q[0]]);
            st.SetColor(shade); st.SetNormal(normals[f]); st.AddVertex(v[q[2]]);
            st.SetColor(shade); st.SetNormal(normals[f]); st.AddVertex(v[q[3]]);
        }
    }

    private static readonly Color Stem = new(0.20f, 0.52f, 0.18f);
    private static readonly Color Leaf = new(0.28f, 0.62f, 0.22f);

    private static Mesh BuildFlower(Color petal, Color center, float height)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        AddBox(st, new Vector3(0, height * 0.5f, 0), new Vector3(0.05f, height, 0.05f), Stem);
        AddBox(st, new Vector3(0.09f, height * 0.35f, 0), new Vector3(0.16f, 0.03f, 0.06f), Leaf);
        AddBox(st, new Vector3(-0.09f, height * 0.5f, 0), new Vector3(0.16f, 0.03f, 0.06f), Leaf);
        float y = height + 0.04f;
        AddBox(st, new Vector3(0.1f, y, 0), new Vector3(0.1f, 0.08f, 0.1f), petal);
        AddBox(st, new Vector3(-0.1f, y, 0), new Vector3(0.1f, 0.08f, 0.1f), petal);
        AddBox(st, new Vector3(0, y, 0.1f), new Vector3(0.1f, 0.08f, 0.1f), petal);
        AddBox(st, new Vector3(0, y, -0.1f), new Vector3(0.1f, 0.08f, 0.1f), petal);
        AddBox(st, new Vector3(0, y + 0.015f, 0), new Vector3(0.1f, 0.1f, 0.1f), center);
        return st.Commit();
    }

    private static Mesh BuildSpike(Color col, float height)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        AddBox(st, new Vector3(0, height * 0.35f, 0), new Vector3(0.04f, height * 0.7f, 0.04f), Stem);
        for (int i = 0; i < 5; i++)
        {
            float y = height * 0.5f + i * 0.07f;
            float w = 0.11f - i * 0.012f;
            AddBox(st, new Vector3(0, y, 0), new Vector3(w, 0.075f, w), col * (1f - i * 0.04f));
        }
        AddBox(st, new Vector3(0.07f, height * 0.2f, 0), new Vector3(0.14f, 0.03f, 0.05f), Leaf);
        AddBox(st, new Vector3(-0.07f, height * 0.3f, 0.02f), new Vector3(0.14f, 0.03f, 0.05f), Leaf);
        return st.Commit();
    }

    private static Mesh BuildTallGrass()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var rng = new RandomNumberGenerator { Seed = 4242 };
        for (int i = 0; i < 9; i++)
        {
            float ang = rng.Randf() * Mathf.Tau, r = rng.Randf() * 0.28f;
            float h = 0.6f + rng.Randf() * 0.5f;
            Color c = new Color(0.22f, 0.55f, 0.16f).Lerp(new Color(0.45f, 0.75f, 0.28f), rng.Randf());
            AddBox(st, new Vector3(Mathf.Cos(ang) * r, h * 0.5f, Mathf.Sin(ang) * r), new Vector3(0.05f, h, 0.05f), c);
            AddBox(st, new Vector3(Mathf.Cos(ang) * r, h, Mathf.Sin(ang) * r), new Vector3(0.06f, 0.1f, 0.06f), c * 1.15f);
        }
        return st.Commit();
    }
}
