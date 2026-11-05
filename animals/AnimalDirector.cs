using System.Collections.Generic;
using Godot;
using PixelBlock.Core;
using PixelBlock.PlayerSys;
using PixelBlock.UI;

namespace PixelBlock.Animals;

public sealed partial class AnimalDirector : Node3D
{
    public static AnimalDirector Instance { get; private set; }

    public VoxelWorld World;
    public Player Player;
    public EnvironmentController Env;
    public TerrainGenerator Gen;
    public Inventory Inv;
    public Hotbar Hotbar;
    public int MaxAnimals = 26;

    public bool PuppetMode;
    private int _nextNetId = 1;
    private readonly Dictionary<int, Animal> _byNet = new();

    public readonly List<Animal> All = new();
    public Node DropParent => GetParent();

    private float _spawnT = 2f;
    private float _atkCd;
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        Instance = this;
        _rng.Randomize();
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public string HeldName()
    {
        if (Hotbar == null) return "";
        ushort id = Hotbar.SelectedBlockId;
        return id == 0 ? "" : BlockRegistry.Get(id).Name;
    }

    public override void _Process(double delta)
    {
        if (PuppetMode) return;
        if (World == null || Player == null || Gen == null) return;
        _atkCd = Mathf.Max(0f, _atkCd - (float)delta);
        _spawnT -= (float)delta;
        if (_spawnT > 0f) return;
        _spawnT = 2.5f;
        TrySpawnWave();
    }

    private void TrySpawnWave()
    {
        if (All.Count >= MaxAnimals) return;
        Vector3 pp = Player.GlobalPosition;
        float ang = _rng.Randf() * Mathf.Tau, dist = _rng.RandfRange(22f, 44f);
        int x = Mathf.FloorToInt(pp.X + Mathf.Cos(ang) * dist);
        int z = Mathf.FloorToInt(pp.Z + Mathf.Sin(ang) * dist);
        int h = Gen.SurfaceHeight(x, z);
        if (h <= TerrainGenerator.SeaLevel) return;
        if (World.GetBlockId(x, h, z) == 0) return;
        if (!World.GetBlock(x, h, z).Solid) return;
        if (World.GetBlock(x, h + 1, z).Solid) return;

        Biome biome = Gen.BiomeAt(x, z, h);
        var candidates = new List<SpeciesDef>();
        foreach (var sp in Species.All)
            foreach (var b in sp.Biomes)
                if (b == biome) { candidates.Add(sp); break; }
        if (candidates.Count == 0) return;

        var pick = candidates[_rng.RandiRange(0, candidates.Count - 1)];

        if (pick.Predator && _rng.Randf() < 0.55f) return;
        int count = pick.Herd ? _rng.RandiRange(1, 3) : 1;
        for (int i = 0; i < count; i++)
        {
            int sx = x + _rng.RandiRange(-2, 2), sz = z + _rng.RandiRange(-2, 2);
            int sh = Gen.SurfaceHeight(sx, sz);
            if (sh <= TerrainGenerator.SeaLevel || !World.GetBlock(sx, sh, sz).Solid || World.GetBlock(sx, sh + 1, sz).Solid) continue;
            Spawn(pick, new Vector3(sx + 0.5f, sh + 1f, sz + 0.5f), 999f, false);
        }
    }

    public Animal Spawn(SpeciesDef sp, Vector3 pos, float age, bool owned)
    {
        var a = new Animal { Spec = sp, World = World, Dir = this, Age = age, Owned = owned, Name = "Animal_" + sp.Key, NetId = _nextNetId++ };
        _byNet[a.NetId] = a;
        AddChild(a);
        a.GlobalPosition = pos;
        a.Rotation = new Vector3(0f, _rng.Randf() * Mathf.Tau, 0f);
        All.Add(a);
        return a;
    }

    public void Remove(Animal a)
    {
        All.Remove(a);
        _byNet.Remove(a.NetId);
    }

    public float[] ExportNet()
    {
        var arr = new float[All.Count * 8];
        int i = 0;
        foreach (var a in All)
        {
            arr[i++] = a.NetId;
            arr[i++] = System.Array.IndexOf(Species.All, a.Spec);
            arr[i++] = a.GlobalPosition.X;
            arr[i++] = a.GlobalPosition.Y;
            arr[i++] = a.GlobalPosition.Z;
            arr[i++] = a.Rotation.Y;
            arr[i++] = (int)a.State;
            arr[i++] = a.Age;
        }
        return arr;
    }

    public void ApplyNet(float[] d)
    {
        if (d == null) return;
        var seen = new HashSet<int>();
        for (int i = 0; i + 7 < d.Length; i += 8)
        {
            int id = (int)d[i];
            int sp = (int)d[i + 1];
            if (sp < 0 || sp >= Species.All.Length) continue;
            seen.Add(id);
            if (!_byNet.TryGetValue(id, out var a))
            {
                a = new Animal { Spec = Species.All[sp], World = World, Dir = this, Puppet = true, NetId = id, Name = "Puppet_" + id };
                AddChild(a);
                All.Add(a);
                _byNet[id] = a;
            }
            a.SetNet(new Vector3(d[i + 2], d[i + 3], d[i + 4]), d[i + 5], (int)d[i + 6], d[i + 7]);
        }
        var gone = new List<int>();
        foreach (var k in _byNet.Keys) if (!seen.Contains(k)) gone.Add(k);
        foreach (var k in gone)
        {
            var a = _byNet[k];
            _byNet.Remove(k);
            All.Remove(a);
            if (IsInstanceValid(a)) a.QueueFree();
        }
    }

    public void HitByNetId(int netId, float dmg, Vector3 from, int peer)
    {
        if (_byNet.TryGetValue(netId, out var a)) a.Hurt(Mathf.Clamp(dmg, 0f, 12f), from, false, peer);
    }

    public Animal NearestPredator(Animal self, float range)
    {
        Animal best = null; float bd = range;
        foreach (var o in All)
        {
            if (o == self || !o.Spec.Predator) continue;
            if (!IsPreyOf(o.Spec, self.Spec)) continue;
            float d = o.GlobalPosition.DistanceTo(self.GlobalPosition);
            if (d < bd) { bd = d; best = o; }
        }
        return best;
    }

    public Animal NearestPrey(Animal self, float range)
    {
        Animal best = null; float bd = range;
        foreach (var o in All)
        {
            if (o == self || o.Spec.Predator) continue;
            if (!IsPreyOf(self.Spec, o.Spec)) continue;
            float d = o.GlobalPosition.DistanceTo(self.GlobalPosition);
            if (d < bd) { bd = d; best = o; }
        }
        return best;
    }

    private static bool IsPreyOf(SpeciesDef hunter, SpeciesDef prey)
    {
        foreach (var k in hunter.Prey) if (k == prey.Key) return true;
        return false;
    }

    public Animal FindPartner(Animal self, float range)
    {
        Animal best = null; float bd = range;
        foreach (var o in All)
        {
            if (o == self || o.Spec != self.Spec || o.LoveTime <= 0f || o.IsBaby) continue;
            float d = o.GlobalPosition.DistanceTo(self.GlobalPosition);
            if (d < bd) { bd = d; best = o; }
        }
        return best;
    }

    public void Breed(Animal a, Animal b)
    {
        if (a.LoveTime <= 0f || b.LoveTime <= 0f) return;
        a.LoveTime = b.LoveTime = 0f;
        a.LoveCooldown = b.LoveCooldown = 90f;
        Vector3 mid = (a.GlobalPosition + b.GlobalPosition) * 0.5f;
        Spawn(a.Spec, mid, 0f, true);
        Fx.Burst(mid + new Vector3(0, 0.8f, 0), FxKind.Sparkle, new Color(1f, 0.5f, 0.7f), 14);
        AudioManager.Play("chime", 1.2f, -8f);
    }

    public Animal FindInFront(Vector3 camPos, Vector3 fwd, float range)
    {
        Animal best = null; float bestDot = 0.9f;
        foreach (var a in All)
        {
            Vector3 to = a.GlobalPosition + new Vector3(0, a.HeightMid, 0) - camPos;
            float d = to.Length();
            if (d > range || d < 0.01f) continue;
            float dot = to.Normalized().Dot(fwd);
            if (dot <= bestDot) continue;
            var hit = VoxelRay.Cast(World, camPos, to, d);
            if (hit.Ok && hit.Distance < d - 0.6f) continue;
            bestDot = dot; best = a;
        }
        return best;
    }

    public void Attack(Animal a, float damage, Vector3 from)
    {
        if (_atkCd > 0f || a == null) return;
        _atkCd = 0.45f;
        AudioManager.Play("swing", 1.0f, -6f);
        if (a.Puppet) { PixelBlock.Network.NetworkManager.Instance?.SendAnimalHit(a.NetId, damage); return; }
        a.Hurt(damage, from);
    }

    public bool TryInteract(Player player, BlockType held, Inventory inv)
    {
        var cam = player?.Camera;
        if (cam == null || All.Count == 0) return false;
        var a = FindInFront(cam.GlobalPosition, -cam.GlobalTransform.Basis.Z, 4.5f);
        if (a == null || a.Puppet) return false;
        Vector3 at = a.GlobalPosition + new Vector3(0, a.HeightMid, 0);

        if (held.Name == "kova" && a.Spec.Key == "inek" && !a.IsBaby)
        {
            if (a.MilkTimer > 0f) return false;
            a.MilkTimer = 90f;
            Give(inv, "sut", 1, at);
            AudioManager.Play("select", 0.7f, -4f);
            return true;
        }
        if (held.Tool == ToolKind.Sword && a.Spec.Key == "koyun" && !a.IsBaby)
        {
            if (a.WoolTimer > 0f) return false;
            a.WoolTimer = 120f;
            Give(inv, "yun", _rng.RandiRange(1, 3), at);
            AudioManager.Play("swing", 1.3f, -6f);
            return true;
        }
        if (Species.Eats(a.Spec, held.Name))
        {
            if (a.IsBaby) { a.Age += 30f; }
            else if (a.LoveTime > 0f || a.LoveCooldown > 0f) return false;
            else a.LoveTime = 25f;
            if (inv != null && !inv.TryConsume(held.Id)) return false;
            a.Owned = true;
            Fx.Burst(at + new Vector3(0, 0.4f, 0), FxKind.Sparkle, new Color(1f, 0.5f, 0.7f), 8);
            AudioManager.Play("select", 1.1f, -4f);
            return true;
        }
        return false;
    }

    private void Give(Inventory inv, string item, int n, Vector3 at)
    {
        ushort id = BlockRegistry.IdOf(item);
        if (id == 0) return;
        int left = inv != null ? inv.Add(id, n) : n;
        if (left > 0) ItemDrop.Spawn(DropParent, World, Player, inv, id, left, at, new Vector3(0, 2.5f, 0));
    }

    public List<(string key, Vector3 pos, float age, bool owned)> Export()
    {
        var list = new List<(string, Vector3, float, bool)>();
        foreach (var a in All)
        {
            if (list.Count >= 80) break;
            if (!a.Owned && Player != null && a.GlobalPosition.DistanceTo(Player.GlobalPosition) > 64f) continue;
            list.Add((a.Spec.Key, a.GlobalPosition, a.Age, a.Owned));
        }
        return list;
    }

    public void Import(IEnumerable<(string key, Vector3 pos, float age, bool owned)> animals)
    {
        foreach (var (key, pos, age, owned) in animals)
        {
            var sp = Species.ByKey(key);
            if (sp != null) Spawn(sp, pos, age, owned);
        }
    }
}
