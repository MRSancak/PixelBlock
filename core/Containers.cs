using System.Collections.Generic;
using Godot;

namespace PixelBlock.Core;

public sealed class ContainerData
{
    public string Kind;
    public Inventory Inv = new();
    public float Burn, BurnMax, Prog;
}

/// <summary>Sandık ve fırın gibi içi dolu blokların verisi. Fırınlar yakınken çalışır.</summary>
public sealed partial class Containers : Node
{
    public static Containers Instance { get; private set; }

    public const int ChestSlots = 27;
    public const float SmeltTime = 8f;

    public VoxelWorld World;
    public Node3D Player;
    public Node DropParent;
    public Inventory PlayerInv;

    private readonly Dictionary<Vector3I, ContainerData> _data = new();
    private float _tick;

    public static readonly Dictionary<string, string> Smelts = new()
    {
        { "ham_bakir", "bakir_kulce" }, { "ham_demir", "demir_kulce" }, { "ham_altin", "altin_kulce" },
        { "et", "pismis_et" }, { "oak_log", "komur" }, { "clay", "brick" }, { "cobblestone", "stone" },
    };

    public static readonly Dictionary<string, float> Fuels = new()
    {
        { "komur", 80f }, { "oak_log", 15f }, { "planks", 12f }, { "sopa", 5f }, { "thatch", 8f },
    };

    public static bool IsContainer(string blockName) => blockName == "chest" || blockName == "furnace";

    public static bool IsSmeltable(ushort id) => id != 0 && Smelts.ContainsKey(BlockRegistry.Get(id).Name);
    public static bool IsFuel(ushort id) => id != 0 && Fuels.ContainsKey(BlockRegistry.Get(id).Name);

    public override void _Ready() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public ContainerData GetOrCreate(Vector3I cell, string kind)
    {
        if (!_data.TryGetValue(cell, out var d) || d.Kind != kind)
        {
            d = new ContainerData { Kind = kind };
            _data[cell] = d;
        }
        return d;
    }

    /// <summary>Blok kırılınca içindekiler yere düşer.</summary>
    public void OnBroken(Vector3I cell)
    {
        if (!_data.TryGetValue(cell, out var d)) return;
        _data.Remove(cell);
        Vector3 at = (Vector3)cell + new Vector3(0.5f, 0.6f, 0.5f);
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        for (int i = 0; i < Inventory.TotalSlots; i++)
        {
            ushort id = d.Inv.SlotId(i);
            int n = d.Inv.SlotCount(i);
            if (id == 0 || n <= 0) continue;
            ItemDrop.Spawn(DropParent, World, Player, PlayerInv, id, n, at, new Vector3(rng.RandfRange(-1.5f, 1.5f), 3f, rng.RandfRange(-1.5f, 1.5f)));
        }
    }

    public override void _Process(double delta)
    {
        _tick += (float)delta;
        if (_tick < 0.25f) return;
        float dt = _tick;
        _tick = 0f;
        if (World == null || _data.Count == 0) return;
        Vector3 pp = Player != null && IsInstanceValid(Player) ? Player.GlobalPosition : Vector3.Zero;
        foreach (var kv in _data)
        {
            if (kv.Value.Kind != "furnace") continue;
            Vector3 c = (Vector3)kv.Key;
            if (new Vector2(c.X - pp.X, c.Z - pp.Z).LengthSquared() > 48f * 48f) continue;
            if (World.GetBlock(kv.Key).Name != "furnace") continue;
            TickFurnace(kv.Value, dt);
        }
    }

    private static void TickFurnace(ContainerData d, float dt)
    {
        var inv = d.Inv;
        ushort inId = inv.SlotId(0);
        ushort outId = 0;
        if (inId != 0 && Smelts.TryGetValue(BlockRegistry.Get(inId).Name, out string outName))
            outId = BlockRegistry.IdOf(outName);
        ushort have = inv.SlotId(2);
        bool canSmelt = outId != 0 && (have == 0 || (have == outId && inv.SlotCount(2) < Inventory.StackMax));

        if (d.Burn <= 0f && canSmelt)
        {
            ushort fuel = inv.SlotId(1);
            if (fuel != 0 && Fuels.TryGetValue(BlockRegistry.Get(fuel).Name, out float secs))
            {
                inv.TakeFromSlot(1, 1);
                d.Burn = d.BurnMax = secs;
            }
        }

        if (d.Burn > 0f)
        {
            d.Burn = Mathf.Max(0f, d.Burn - dt);
            if (canSmelt)
            {
                d.Prog += dt;
                if (d.Prog >= SmeltTime)
                {
                    d.Prog = 0f;
                    inv.TakeFromSlot(0, 1);
                    inv.SetSlot(2, outId, (have == outId ? inv.SlotCount(2) : 0) + 1);
                }
            }
            else d.Prog = Mathf.Max(0f, d.Prog - dt * 2f);
        }
        else d.Prog = Mathf.Max(0f, d.Prog - dt * 2f);
    }

    public List<(int x, int y, int z, string kind, string inv, float burn, float burnMax, float prog)> Export()
    {
        var list = new List<(int, int, int, string, string, float, float, float)>();
        foreach (var kv in _data)
        {
            if (World != null && !IsContainer(World.GetBlock(kv.Key).Name)) continue;
            var d = kv.Value;
            list.Add((kv.Key.X, kv.Key.Y, kv.Key.Z, d.Kind, d.Inv.Encode(), d.Burn, d.BurnMax, d.Prog));
        }
        return list;
    }

    public void Import(IEnumerable<(int x, int y, int z, string kind, string inv, float burn, float burnMax, float prog)> items)
    {
        foreach (var (x, y, z, kind, inv, burn, burnMax, prog) in items)
        {
            var d = new ContainerData { Kind = kind, Burn = burn, BurnMax = burnMax, Prog = prog };
            d.Inv.Decode(inv);
            _data[new Vector3I(x, y, z)] = d;
        }
    }
}
