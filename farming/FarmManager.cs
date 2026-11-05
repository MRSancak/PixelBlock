using System.Collections.Generic;
using Godot;
using PixelBlock.Core;
using System.Globalization;
using PixelBlock.Network;
using PixelBlock.PlayerSys;

namespace PixelBlock.Farming;

public sealed partial class FarmManager : Node3D
{
    public static FarmManager Instance { get; private set; }

    public VoxelWorld World;
    public Player Player;
    public EnvironmentController Env;

    public bool ClientMode;

    public static System.Action<string> OnMessage;

    public sealed class Crop
    {
        public CropDef Def;
        public Vector3I Soil;
        public float Progress;
        public bool Fertilized;
        public double LastSim;
        public int Stage = -1;
        public Node3D Node;
        public bool WaterNear;
        public double NextWaterCheck;
        public bool Ripe => Progress >= 1f;
    }

    private readonly Dictionary<Vector3I, Crop> _crops = new();
    private readonly Dictionary<Vector3I, double> _wetUntil = new();
    private readonly Dictionary<Color, StandardMaterial3D> _mats = new();
    private double _now;
    private float _tick;
    private readonly RandomNumberGenerator _rng = new();

    public double Now => _now;
    public int CropCount => _crops.Count;

    public override void _Ready()
    {
        Instance = this;
        _rng.Randomize();
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public bool HasCropAt(Vector3I soil) => _crops.ContainsKey(soil);

    public bool UseOn(Vector3I soil, BlockType held, Inventory inv, Node dropParent)
    {
        if (World == null || World.GetBlock(soil).Name != "farmland") return false;
        if (ClientMode) return UseOnClient(soil, held, inv);

        if (held != null && held.Name == "kova") { Water(soil); return true; }

        if (_crops.TryGetValue(soil, out var crop))
        {
            if (held != null && held.Name == "gubre" && !crop.Fertilized && !crop.Ripe)
            {
                if (inv != null && !inv.TryConsume(held.Id)) return false;
                crop.Fertilized = true;
                Fx.Burst(CropPos(soil) + new Vector3(0, 0.3f, 0), FxKind.Sparkle, new Color(0.5f, 1f, 0.4f), 10);
                AudioManager.Play("select", 1.3f, -5f);
                return true;
            }
            if (crop.Ripe) { Harvest(crop, inv, dropParent); return true; }
            return false;
        }

        if (held == null) return false;
        bool wild = held.Name == ItemCatalog.WildSeedKey;
        var def = wild ? ItemCatalog.RollWildCrop(_rng) : ItemCatalog.CropForSeed(held.Name);
        if (def == null) return false;
        if (!World.GetBlock(soil + Vector3I.Up).IsAir) return false;
        if (inv != null && !inv.TryConsume(held.Id)) return false;
        Plant(soil, def, 0f);
        if (wild) OnMessage?.Invoke($"Yabani tohumdan çıkan: {def.Name}  (her bitki için şans %{ItemCatalog.WildChancePercent:F1})");
        AudioManager.Play("place", 1.2f, -4f);
        return true;
    }

    private bool UseOnClient(Vector3I soil, BlockType held, Inventory inv)
    {
        string hn = held != null ? held.Name : "";
        bool ok = false;
        if (hn == "kova") ok = true;
        else if (_crops.TryGetValue(soil, out var c))
        {
            if (hn == "gubre" && !c.Fertilized && !c.Ripe) ok = inv == null || inv.TryConsume(held.Id);
            else if (c.Ripe) ok = true;
        }
        else
        {
            var def = hn == ItemCatalog.WildSeedKey ? ItemCatalog.Crops[0] : ItemCatalog.CropForSeed(hn);
            if (def != null && World.GetBlock(soil + Vector3I.Up).IsAir) ok = inv == null || inv.TryConsume(held.Id);
        }
        if (ok) NetworkManager.Instance?.SendFarmUse(soil, hn);
        return ok;
    }

    public void UseRemote(Vector3I soil, string heldName, int peer)
    {
        if (ClientMode || World == null) return;
        if (World.GetBlock(soil).Name != "farmland") { Refund(peer, heldName); return; }
        if (heldName == "kova") { Water(soil); return; }
        if (_crops.TryGetValue(soil, out var crop))
        {
            if (heldName == "gubre" && !crop.Fertilized && !crop.Ripe) { crop.Fertilized = true; return; }
            if (crop.Ripe) { Harvest(crop, null, null, peer); return; }
            Refund(peer, heldName);
            return;
        }
        var def = heldName == ItemCatalog.WildSeedKey ? ItemCatalog.RollWildCrop(_rng) : ItemCatalog.CropForSeed(heldName);
        if (def != null && World.GetBlock(soil + Vector3I.Up).IsAir) { Plant(soil, def, 0f); return; }
        Refund(peer, heldName);
    }

    private static void Refund(int peer, string heldName)
    {
        if (string.IsNullOrEmpty(heldName)) return;
        if (heldName.StartsWith("tohum_") || heldName == ItemCatalog.WildSeedKey || heldName.StartsWith("fidan_") || heldName == "gubre")
            NetworkManager.Instance?.GiveItem(peer, heldName, 1);
    }

    public string ExportSnapshot()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in _crops.Values)
            sb.Append(c.Def.Key).Append(',').Append(c.Soil.X).Append(',').Append(c.Soil.Y).Append(',').Append(c.Soil.Z)
              .Append(',').Append(c.Progress.ToString("0.###", CultureInfo.InvariantCulture))
              .Append(',').Append(c.Fertilized ? 1 : 0).Append(';');
        return sb.ToString();
    }

    public void ApplySnapshot(string snap)
    {
        if (!ClientMode || snap == null) return;
        var keep = new HashSet<Vector3I>();
        foreach (var part in snap.Split(';'))
        {
            var t = part.Split(',');
            if (t.Length < 6) continue;
            var def = ItemCatalog.CropByKey(t[0]);
            if (def == null) continue;
            if (!int.TryParse(t[1], out int x) || !int.TryParse(t[2], out int y) || !int.TryParse(t[3], out int z)) continue;
            if (!float.TryParse(t[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float pr)) continue;
            var cell = new Vector3I(x, y, z);
            keep.Add(cell);
            if (_crops.TryGetValue(cell, out var c))
            {
                if (c.Def != def) { Remove(c); Plant(cell, def, pr); }
                else { c.Progress = pr; c.Fertilized = t[5] == "1"; UpdateVisual(c, force: false); }
            }
            else Plant(cell, def, pr);
        }
        var gone = new List<Crop>();
        foreach (var c in _crops.Values) if (!keep.Contains(c.Soil)) gone.Add(c);
        foreach (var c in gone) Remove(c);
    }

    public void Water(Vector3I soil)
    {
        _wetUntil[soil] = _now + 240.0;
        Fx.Burst(CropPos(soil) + new Vector3(0, 0.2f, 0), FxKind.Splash, new Color(0.5f, 0.7f, 1f), 12);
        AudioManager.Play("splash", 1.2f, -8f);
    }

    public bool IsWet(Vector3I soil)
    {
        if (_wetUntil.TryGetValue(soil, out double t) && t > _now) return true;
        if (Env != null && (Env.Weather == Weather.Rain || Env.Weather == Weather.Storm)) return true;
        return false;
    }

    public void Plant(Vector3I soil, CropDef def, float progress)
    {
        var crop = new Crop { Def = def, Soil = soil, Progress = progress, LastSim = _now };
        _crops[soil] = crop;
        crop.Node = new Node3D { Name = "Crop_" + def.Key };
        AddChild(crop.Node);
        crop.Node.GlobalPosition = CropPos(soil);
        UpdateVisual(crop, force: true);
    }

    public void RemoveAt(Vector3I cell)
    {
        if (_crops.TryGetValue(cell, out var c1)) { Remove(c1); return; }
        if (_crops.TryGetValue(cell + Vector3I.Down, out var c2)) Remove(c2);
    }

    private void Remove(Crop c)
    {
        _crops.Remove(c.Soil);
        if (c.Node != null && IsInstanceValid(c.Node)) c.Node.QueueFree();
    }

    private void Harvest(Crop crop, Inventory inv, Node dropParent, int peer = 0)
    {
        var def = crop.Def;
        int n = def.IsFruit ? _rng.RandiRange(2, 4) : _rng.RandiRange(1, 3);
        Vector3 at = CropPos(crop.Soil) + new Vector3(0, 0.5f, 0);
        GiveOrDrop(inv, dropParent, BlockRegistry.IdOf(def.Key), n, at, peer);
        if (!def.Perennial)
        {
            GiveOrDrop(inv, dropParent, BlockRegistry.IdOf(def.SeedKey), _rng.RandiRange(1, 2), at, peer);
            Remove(crop);
        }
        else
        {
            crop.Progress = 0.4f;
            crop.Fertilized = false;
            UpdateVisual(crop, force: true);
        }
        Fx.Burst(at, FxKind.Sparkle, def.Color, 12);
        AudioManager.Play("select", 0.9f, -3f);
    }

    private void GiveOrDrop(Inventory inv, Node dropParent, ushort id, int n, Vector3 at, int peer = 0)
    {
        if (id == 0) return;
        if (peer > 1) { NetworkManager.Instance?.GiveItem(peer, BlockRegistry.Get(id).Name, n); return; }
        int left = inv != null ? inv.Add(id, n) : n;
        if (left > 0 && dropParent != null)
            ItemDrop.Spawn(dropParent, World, Player, inv, id, left, at, new Vector3(0, 2.5f, 0));
    }

    public override void _Process(double delta)
    {
        _now += delta;
        _tick += (float)delta;
        if (_tick < 0.5f) return;
        _tick = 0f;
        SimulateTick();
    }

    private void SimulateTick()
    {
        if (ClientMode || World == null || _crops.Count == 0) return;
        float simRadius = Settings.SimDistance * Chunk.Size;
        Vector3 pp = Player != null && IsInstanceValid(Player) ? Player.GlobalPosition : Vector3.Zero;
        var dead = new List<Crop>();

        foreach (var crop in _crops.Values)
        {
            Vector3 cp = CropPos(crop.Soil);
            float dx = cp.X - pp.X, dz = cp.Z - pp.Z;
            if (dx * dx + dz * dz > simRadius * simRadius) continue;

            if (World.GetBlock(crop.Soil).Name != "farmland")
            {

                if (World.GetBlockId(crop.Soil) != 0) dead.Add(crop);
                continue;
            }

            double dt = System.Math.Min(900.0, _now - crop.LastSim);
            crop.LastSim = _now;
            if (dt <= 0.0) continue;
            Advance(crop, (float)dt);
        }
        foreach (var c in dead) Remove(c);
    }

    private void Advance(Crop crop, float dt)
    {
        if (crop.Progress < 1f)
        {
            if (_now >= crop.NextWaterCheck)
            {
                crop.WaterNear = NearWater(crop.Soil);
                crop.NextWaterCheck = _now + 10.0;
            }
            float sf = SeasonFactor(crop.Def);
            float wf = (IsWet(crop.Soil) || crop.WaterNear) ? 1f : 0.3f;
            float ff = crop.Fertilized ? 1.6f : 1f;
            crop.Progress = Mathf.Min(1f, crop.Progress + dt / crop.Def.GrowSeconds * sf * wf * ff);
        }
        UpdateVisual(crop, force: false);
    }

    public static float SeasonFactor(CropDef def)
    {
        int bit = SeasonClock.Instance != null ? SeasonClock.Instance.SeasonBit : SeasonBits.Summer;
        return (def.SeasonMask & bit) != 0 ? 1f : 0.08f;
    }

    private bool NearWater(Vector3I soil)
    {
        for (int dx = -4; dx <= 4; dx++)
        for (int dz = -4; dz <= 4; dz++)
        for (int dy = -1; dy <= 1; dy++)
            if (World.GetBlock(soil.X + dx, soil.Y + dy, soil.Z + dz).IsLiquid) return true;
        return false;
    }

    private static Vector3 CropPos(Vector3I soil) => new(soil.X + 0.5f, soil.Y + 1f, soil.Z + 0.5f);

    private void UpdateVisual(Crop crop, bool force)
    {
        int stage = crop.Progress >= 1f ? 3 : Mathf.Min(2, (int)(crop.Progress * 3f));
        if (!force && stage == crop.Stage) return;
        crop.Stage = stage;
        if (crop.Node == null) return;
        foreach (Node ch in crop.Node.GetChildren()) ch.QueueFree();

        var def = crop.Def;
        Color green = new(0.25f, 0.62f, 0.22f);
        float h = def.Height;
        bool ground = h <= 0.55f;

        switch (stage)
        {
            case 0:
                Box(crop.Node, new Vector3(0.06f, 0.14f, 0.06f), new Vector3(0, 0.07f, 0), green);
                break;
            case 1:
                Box(crop.Node, new Vector3(0.07f, 0.4f * h + 0.1f, 0.07f), new Vector3(0, (0.4f * h + 0.1f) / 2f, 0), green);
                Box(crop.Node, new Vector3(0.3f, 0.04f, 0.12f), new Vector3(0, 0.18f, 0), green);
                break;
            case 2:
                Box(crop.Node, new Vector3(0.08f, 0.75f * h, 0.08f), new Vector3(0, 0.75f * h / 2f, 0), green);
                Box(crop.Node, new Vector3(0.4f, 0.05f, 0.14f), new Vector3(0, 0.3f * h + 0.1f, 0), green);
                Box(crop.Node, new Vector3(0.14f, 0.05f, 0.4f), new Vector3(0, 0.5f * h + 0.1f, 0), green);
                break;
            default:
                if (ground)
                {
                    Box(crop.Node, new Vector3(0.45f, 0.1f, 0.45f), new Vector3(0, 0.15f, 0), green);
                    Box(crop.Node, new Vector3(0.3f, 0.26f, 0.3f), new Vector3(0, 0.13f, 0), def.Color);
                    Box(crop.Node, new Vector3(0.18f, 0.18f, 0.18f), new Vector3(0.18f, 0.09f, 0.12f), def.Color);
                }
                else
                {
                    Box(crop.Node, new Vector3(0.09f, h, 0.09f), new Vector3(0, h / 2f, 0), green);
                    Box(crop.Node, new Vector3(0.45f, 0.05f, 0.15f), new Vector3(0, h * 0.4f, 0), green);
                    Box(crop.Node, new Vector3(0.15f, 0.05f, 0.45f), new Vector3(0, h * 0.6f, 0), green);
                    float s = def.IsFruit ? 0.16f : 0.2f;
                    Box(crop.Node, new Vector3(s, s, s), new Vector3(0.18f, h * 0.55f, 0.05f), def.Color);
                    Box(crop.Node, new Vector3(s, s, s), new Vector3(-0.17f, h * 0.7f, -0.06f), def.Color);
                    Box(crop.Node, new Vector3(s, s, s), new Vector3(0.02f, h * 0.9f, 0.16f), def.Color);
                }
                break;
        }
    }

    private void Box(Node3D parent, Vector3 size, Vector3 pos, Color color)
    {
        if (!_mats.TryGetValue(color, out var mat))
        {
            mat = PixelMat.Get(color);
            _mats[color] = mat;
        }
        var mi = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = mat,
            Position = pos,
        };
        parent.AddChild(mi);
    }

    public List<(string key, Vector3 soil, float progress, bool fert)> Export()
    {
        var list = new List<(string, Vector3, float, bool)>();
        foreach (var c in _crops.Values)
            list.Add((c.Def.Key, new Vector3(c.Soil.X, c.Soil.Y, c.Soil.Z), c.Progress, c.Fertilized));
        return list;
    }

    public void Import(IEnumerable<(string key, Vector3 soil, float progress, bool fert)> crops)
    {
        foreach (var (key, soil, progress, fert) in crops)
        {
            var def = ItemCatalog.CropByKey(key);
            if (def == null) continue;
            var cell = new Vector3I(Mathf.RoundToInt(soil.X), Mathf.RoundToInt(soil.Y), Mathf.RoundToInt(soil.Z));
            Plant(cell, def, progress);
            if (_crops.TryGetValue(cell, out var c)) c.Fertilized = fert;
        }
    }
}
