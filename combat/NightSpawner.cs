using System.Collections.Generic;
using Godot;
using PixelBlock.Core;
using PixelBlock.PlayerSys;

namespace PixelBlock.Combat;

/// <summary>Gece olunca oyuncunun etrafında zombi, iskelet ve örümcek doğurur; gündüz yanar/kaybolur.</summary>
public sealed partial class NightSpawner : Node
{
    public static bool Enabled = true;

    public VoxelWorld World;
    public Player Player;
    public EnvironmentController Env;
    public Inventory Inv;
    public Node SpawnParent;
    public System.Action<string> Notify;

    private const int MaxMonsters = 7;
    private const float NightBelow = 0.35f;
    private readonly List<Enemy> _mobs = new();
    private readonly RandomNumberGenerator _rng = new();
    private float _t;
    private bool _wasNight;

    public override void _Ready() => _rng.Randomize();

    public static EnemyType RandomType(RandomNumberGenerator rng)
    {
        float r = rng.Randf();
        if (r < 0.45f) return EnemyType.Zombie();
        if (r < 0.75f) return EnemyType.Skeleton();
        return EnemyType.Spider();
    }

    public override void _Process(double delta)
    {
        if (World == null || Player == null || Env == null || !IsInstanceValid(Player)) return;
        _t += (float)delta;
        if (_t < 2.5f) return;
        _t = 0f;

        _mobs.RemoveAll(m => m == null || !IsInstanceValid(m) || !m.IsAlive);
        bool night = Env.DayFactor < NightBelow;

        if (night && !_wasNight && Enabled && !Player.Creative) Notify?.Invoke("Gece bastı — canavarlar dolaşıyor!");
        if (!night && _wasNight && _mobs.Count > 0) Notify?.Invoke("Sabah oldu.");
        _wasNight = night;

        Vector3 pp = Player.GlobalPosition;
        for (int i = _mobs.Count - 1; i >= 0; i--)
        {
            var m = _mobs[i];
            bool far = m.GlobalPosition.DistanceTo(pp) > 64f;
            // örümcek yanmaz: gündüz uzaktaysa ya da şansla yok olur
            bool dayDespawn = !night && !m.Type.BurnsInDaylight && _rng.Randf() < 0.25f;
            if (far || dayDespawn) { m.QueueFree(); _mobs.RemoveAt(i); }
        }

        if (!night || !Enabled || Player.Creative || Player.SafeMode || Player.IsDead) return;
        if (_mobs.Count >= MaxMonsters) return;
        if (_rng.Randf() > 0.6f) return;
        TrySpawn(pp);
    }

    private void TrySpawn(Vector3 pp)
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            float ang = _rng.Randf() * Mathf.Tau, d = _rng.RandfRange(16f, 30f);
            int x = Mathf.FloorToInt(pp.X + Mathf.Cos(ang) * d), z = Mathf.FloorToInt(pp.Z + Mathf.Sin(ang) * d);
            int py = Mathf.FloorToInt(pp.Y);
            for (int y = py + 10; y >= py - 14; y--)
            {
                var below = World.GetBlock(x, y, z);
                if (!below.Solid) continue;
                if (World.GetBlock(x, y + 1, z).Solid || World.GetBlock(x, y + 2, z).Solid || World.GetBlock(x, y + 3, z).Solid) break;
                if (World.GetBlock(x, y + 1, z).IsLiquid) break;
                Spawn(RandomType(_rng), new Vector3(x + 0.5f, y + 1f, z + 0.5f));
                return;
            }
        }
    }

    public Enemy Spawn(EnemyType type, Vector3 pos)
    {
        var e = new Enemy { Type = type, Target = Player, World = World, DayFactor = () => Env.DayFactor, Name = "Night_" + type.Name };
        (SpawnParent ?? this).AddChild(e);
        e.GlobalPosition = pos;
        string name = type.Name;
        e.Defeated += () => OnDefeated(name, pos + new Vector3(0, 0.5f, 0), e);
        _mobs.Add(e);
        return e;
    }

    private void OnDefeated(string name, Vector3 at, Enemy e)
    {
        if (IsInstanceValid(e)) at = e.GlobalPosition + new Vector3(0, 0.6f, 0);
        switch (name)
        {
            case "Zombi": Loot("et", _rng.RandiRange(0, 2), at); Loot("komur", _rng.Randf() < 0.3f ? 1 : 0, at); break;
            case "İskelet": Loot("sopa", _rng.RandiRange(1, 3), at); Loot("komur", _rng.Randf() < 0.4f ? 1 : 0, at); break;
            default: Loot("yun", _rng.RandiRange(0, 2), at); Loot("komur", _rng.Randf() < 0.3f ? 1 : 0, at); break;
        }
    }

    private void Loot(string item, int n, Vector3 at)
    {
        ushort id = BlockRegistry.IdOf(item);
        if (id == 0 || n <= 0) return;
        ItemDrop.Spawn(SpawnParent ?? this, World, Player, Inv, id, n, at, new Vector3(_rng.RandfRange(-1f, 1f), 3f, _rng.RandfRange(-1f, 1f)));
    }
}
