using Godot;
using PixelBlock.Core;
using PixelBlock.PlayerSys;

namespace PixelBlock.Animals;

public enum AnimalState { Idle, Wander, Graze, Drink, Rest, Sleep, Flee, Chase, Follow, Love }

public sealed partial class Animal : Node3D
{
    public const float BabyTime = 120f;

    public SpeciesDef Spec;
    public VoxelWorld World;
    public AnimalDirector Dir;

    public float Hp;
    public float Age = 999f;
    public bool Owned;
    public float LoveTime, LoveCooldown, WoolTimer, MilkTimer;

    public bool Puppet;
    public int NetId;
    public int NetState;
    public int KillerPeer;
    private Vector3 _netPos;
    private float _netYaw;
    private bool _netInit;

    private AnimalState _state = AnimalState.Idle;
    private float _stateTimer;
    private Vector3 _target;
    private bool _hasTarget;
    private Animal _victim;
    private Animal _partner;
    private bool _chasePlayer;
    private float _think, _biteCd, _phase, _sndT, _eggT, _fullT, _accum;
    private Vector3 _kb;
    private float _speedNow;
    private float _headPitch;

    private Node3D _model, _head;
    private readonly Node3D[] _legs = new Node3D[4];
    private readonly RandomNumberGenerator _rng = new();

    public bool IsBaby => Age < BabyTime;
    public AnimalState State => _state;
    public float HeightMid => Spec.LegH + Spec.Size.Y * 0.5f;

    public override void _Ready()
    {
        _rng.Randomize();
        Hp = Spec.Hp;
        _sndT = _rng.RandfRange(4f, 14f);
        _eggT = _rng.RandfRange(80f, 140f);
        BuildModel();
        ApplyAgeScale();
    }

    private void BuildModel()
    {
        _model = new Node3D { Name = "Model" };
        AddChild(_model);
        var sz = Spec.Size;
        string k = Spec.Key;
        float bodyY = Spec.LegH + sz.Y * 0.5f;
        Color body = Spec.Body, acc = Spec.Accent;
        Color belly = body.Lerp(Colors.White, 0.3f);
        Color dark = new(0.06f, 0.05f, 0.06f);
        Color blush = new(1f, 0.60f, 0.66f);
        Color hoof = new(0.20f, 0.15f, 0.12f);
        bool wool = k == "koyun";

        // ---- gövde ----
        if (wool)
        {
            Color puff = body.Lerp(Colors.White, 0.35f);
            Box(_model, sz * 1.16f, new Vector3(0, bodyY + 0.03f, 0), body);
            Box(_model, new Vector3(sz.X * 1.05f, sz.Y * 0.3f, sz.Z * 0.42f), new Vector3(0, bodyY + sz.Y * 0.6f, sz.Z * 0.2f), puff);
            Box(_model, new Vector3(sz.X * 1.05f, sz.Y * 0.3f, sz.Z * 0.42f), new Vector3(0, bodyY + sz.Y * 0.6f, -sz.Z * 0.22f), puff);
            Box(_model, new Vector3(sz.X * 0.3f, sz.Y * 0.5f, sz.Z * 0.5f), new Vector3(sz.X * 0.6f, bodyY, 0), puff);
            Box(_model, new Vector3(sz.X * 0.3f, sz.Y * 0.5f, sz.Z * 0.5f), new Vector3(-sz.X * 0.6f, bodyY, 0), puff);
        }
        else
        {
            Box(_model, sz, new Vector3(0, bodyY, 0), body);
            Box(_model, new Vector3(sz.X * 0.92f, sz.Y * 0.3f, sz.Z * 0.8f), new Vector3(0, bodyY - sz.Y * 0.4f, 0), belly);
        }
        if (k == "inek")
        {
            Box(_model, new Vector3(sz.X + 0.02f, 0.3f, 0.4f), new Vector3(0, bodyY + 0.12f, -0.2f), acc);
            Box(_model, new Vector3(0.3f, 0.3f, 0.45f), new Vector3(sz.X * 0.5f, bodyY - 0.1f, 0.3f), acc);
            Box(_model, new Vector3(0.28f, 0.26f, 0.3f), new Vector3(-sz.X * 0.5f, bodyY + 0.05f, 0.35f), acc);
            Box(_model, new Vector3(0.2f, 0.12f, 0.24f), new Vector3(0, bodyY - sz.Y * 0.5f, -0.2f), new Color(1f, 0.72f, 0.74f));
        }
        else if (k == "tilki" || k == "tavsan")
            Box(_model, new Vector3(sz.X * 0.8f, sz.Y * 0.4f, sz.Z * 0.5f), new Vector3(0, bodyY - sz.Y * 0.15f, sz.Z * 0.25f), acc);
        else if (k == "at")
        {
            Box(_model, new Vector3(0.12f, 0.5f, 0.6f), new Vector3(0, bodyY + sz.Y * 0.55f, sz.Z * 0.3f), acc);
            Box(_model, new Vector3(sz.X * 0.9f, 0.08f, sz.Z * 0.35f), new Vector3(0, bodyY + sz.Y * 0.52f, -sz.Z * 0.05f), new Color(0.65f, 0.12f, 0.12f));
        }
        else if (k == "chicken" || k == "tavuk")
        {
            Box(_model, new Vector3(0.07f, sz.Y * 0.55f, sz.Z * 0.6f), new Vector3(sz.X * 0.55f, bodyY, -sz.Z * 0.02f), belly);
            Box(_model, new Vector3(0.07f, sz.Y * 0.55f, sz.Z * 0.6f), new Vector3(-sz.X * 0.55f, bodyY, -sz.Z * 0.02f), belly);
        }

        // ---- kafa ----
        _head = new Node3D { Name = "Head", Position = new Vector3(0, Spec.LegH + sz.Y * 0.85f, sz.Z * 0.5f) };
        _model.AddChild(_head);
        float hs = Spec.HeadS * 1.28f;
        Color headCol = wool ? acc : body;
        Box(_head, new Vector3(hs, hs, hs), new Vector3(0, 0, hs * 0.4f), headCol);
        float faceZ = hs * 0.9f;

        // gözler: beyaz + göz bebeği + parlama
        foreach (float sx in new[] { -1f, 1f })
        {
            Box(_head, new Vector3(hs * 0.24f, hs * 0.26f, hs * 0.05f), new Vector3(sx * hs * 0.28f, hs * 0.1f, faceZ + 0.005f), Colors.White);
            Box(_head, new Vector3(hs * 0.14f, hs * 0.2f, hs * 0.05f), new Vector3(sx * hs * 0.26f, hs * 0.09f, faceZ + 0.02f), dark);
            Box(_head, new Vector3(hs * 0.05f, hs * 0.06f, hs * 0.05f), new Vector3(sx * hs * 0.29f, hs * 0.17f, faceZ + 0.03f), Colors.White);
            if (k != "kurt" && k != "ayi")
                Box(_head, new Vector3(hs * 0.2f, hs * 0.1f, hs * 0.04f), new Vector3(sx * hs * 0.36f, -hs * 0.14f, faceZ + 0.005f), blush);
        }

        // burun / ağız
        bool longSnout = k == "at" || k == "kurt" || k == "tilki" || k == "geyik";
        Color snoutCol = k == "domuz" ? acc : (k == "inek" ? new Color(1f, 0.78f, 0.78f) : body.Lerp(Colors.White, k == "ayi" ? 0.0f : 0.25f));
        if (k == "ayi") snoutCol = new Color(0.62f, 0.45f, 0.30f);
        if (k != "tavuk")
        {
            float sw = k == "domuz" ? 0.7f : 0.58f, sh = k == "domuz" ? 0.42f : 0.36f, sl = longSnout ? 0.62f : 0.4f;
            Box(_head, new Vector3(hs * sw, hs * sh, hs * sl), new Vector3(0, -hs * 0.2f, hs * (0.9f + sl * 0.35f)), snoutCol);
            float nz = hs * (0.9f + sl * 0.55f) + 0.005f;
            Box(_head, new Vector3(hs * 0.2f, hs * 0.13f, hs * 0.05f), new Vector3(0, -hs * 0.08f, nz), k == "domuz" ? new Color(0.85f, 0.4f, 0.45f) : dark);
            if (k == "domuz")
            {
                Box(_head, new Vector3(hs * 0.07f, hs * 0.07f, hs * 0.05f), new Vector3(hs * 0.12f, -hs * 0.2f, nz + 0.01f), dark);
                Box(_head, new Vector3(hs * 0.07f, hs * 0.07f, hs * 0.05f), new Vector3(-hs * 0.12f, -hs * 0.2f, nz + 0.01f), dark);
            }
        }

        switch (k)
        {
            case "tavuk":
                Box(_head, new Vector3(0.07f, 0.06f, 0.1f), new Vector3(0, -hs * 0.05f, hs * 1.0f), new Color(0.98f, 0.72f, 0.15f));
                Box(_head, new Vector3(0.05f, 0.09f, 0.14f), new Vector3(0, hs * 0.62f, hs * 0.4f), acc);
                Box(_head, new Vector3(0.05f, 0.07f, 0.04f), new Vector3(0, -hs * 0.25f, hs * 0.95f), acc);
                break;
            case "inek":
                Box(_head, new Vector3(hs * 0.12f, hs * 0.28f, hs * 0.12f), new Vector3(hs * 0.42f, hs * 0.62f, hs * 0.3f), new Color(0.96f, 0.93f, 0.82f));
                Box(_head, new Vector3(hs * 0.12f, hs * 0.28f, hs * 0.12f), new Vector3(-hs * 0.42f, hs * 0.62f, hs * 0.3f), new Color(0.96f, 0.93f, 0.82f));
                Box(_head, new Vector3(hs * 0.34f, hs * 0.16f, hs * 0.2f), new Vector3(hs * 0.68f, hs * 0.22f, hs * 0.3f), acc);
                Box(_head, new Vector3(hs * 0.34f, hs * 0.16f, hs * 0.2f), new Vector3(-hs * 0.68f, hs * 0.22f, hs * 0.3f), acc);
                break;
            case "koyun":
                Box(_head, new Vector3(hs * 1.1f, hs * 0.3f, hs * 0.9f), new Vector3(0, hs * 0.55f, hs * 0.35f), body.Lerp(Colors.White, 0.3f));
                Box(_head, new Vector3(hs * 0.4f, hs * 0.14f, hs * 0.2f), new Vector3(hs * 0.7f, hs * 0.18f, hs * 0.3f), acc);
                Box(_head, new Vector3(hs * 0.4f, hs * 0.14f, hs * 0.2f), new Vector3(-hs * 0.7f, hs * 0.18f, hs * 0.3f), acc);
                break;
            case "domuz":
                Box(_head, new Vector3(hs * 0.28f, hs * 0.3f, hs * 0.1f), new Vector3(hs * 0.34f, hs * 0.6f, hs * 0.3f), acc);
                Box(_head, new Vector3(hs * 0.28f, hs * 0.3f, hs * 0.1f), new Vector3(-hs * 0.34f, hs * 0.6f, hs * 0.3f), acc);
                break;
            case "tavsan":
            case "geyik":
            case "kurt":
            case "tilki":
            case "at":
            case "ayi":
                {
                    bool rabbit = k == "tavsan", bear = k == "ayi";
                    float ear = rabbit ? hs * 1.5f : (bear ? hs * 0.3f : hs * 0.55f);
                    float ew = bear ? hs * 0.32f : hs * 0.22f;
                    float ex = bear ? hs * 0.4f : hs * 0.28f;
                    Color earCol = rabbit ? body : (k == "geyik" ? body : body);
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        Box(_head, new Vector3(ew, ear, hs * 0.14f), new Vector3(sx * ex, hs * 0.5f + ear * 0.5f, hs * 0.2f), earCol);
                        Box(_head, new Vector3(ew * 0.55f, ear * 0.7f, hs * 0.16f), new Vector3(sx * ex, hs * 0.5f + ear * 0.45f, hs * 0.22f), rabbit || k == "geyik" ? new Color(1f, 0.75f, 0.78f) : acc);
                    }
                    if (k == "geyik")
                    {
                        foreach (float sx in new[] { -1f, 1f })
                        {
                            Box(_head, new Vector3(0.05f, 0.4f, 0.05f), new Vector3(sx * 0.14f, hs * 0.5f + 0.2f, 0), new Color(0.82f, 0.72f, 0.55f));
                            Box(_head, new Vector3(0.05f, 0.05f, 0.18f), new Vector3(sx * 0.14f, hs * 0.5f + 0.3f, 0.07f), new Color(0.82f, 0.72f, 0.55f));
                            Box(_head, new Vector3(0.05f, 0.05f, 0.14f), new Vector3(sx * 0.14f, hs * 0.5f + 0.4f, -0.05f), new Color(0.82f, 0.72f, 0.55f));
                        }
                    }
                    if (k == "tilki")
                    {
                        Box(_head, new Vector3(hs * 0.3f, hs * 0.26f, hs * 0.06f), new Vector3(hs * 0.38f, -hs * 0.18f, faceZ + 0.01f), Colors.White);
                        Box(_head, new Vector3(hs * 0.3f, hs * 0.26f, hs * 0.06f), new Vector3(-hs * 0.38f, -hs * 0.18f, faceZ + 0.01f), Colors.White);
                    }
                    if (k == "at")
                        Box(_head, new Vector3(hs * 0.14f, hs * 0.7f, hs * 0.3f), new Vector3(0, hs * 0.55f, -hs * 0.05f), acc);
                    break;
                }
        }

        // ---- kuyruk ----
        Color tail = k == "tavsan" ? Colors.White : (Spec.Predator || k == "at" ? acc : body);
        if (k == "tavsan")
            Box(_model, new Vector3(0.18f, 0.18f, 0.18f), new Vector3(0, bodyY + sz.Y * 0.15f, -sz.Z * 0.5f - 0.06f), tail);
        else if (k == "tavuk")
        {
            Box(_model, new Vector3(0.1f, 0.2f, 0.14f), new Vector3(0, bodyY + sz.Y * 0.35f, -sz.Z * 0.5f - 0.04f), body);
            Box(_model, new Vector3(0.06f, 0.18f, 0.1f), new Vector3(0.07f, bodyY + sz.Y * 0.38f, -sz.Z * 0.5f - 0.08f), body);
            Box(_model, new Vector3(0.06f, 0.18f, 0.1f), new Vector3(-0.07f, bodyY + sz.Y * 0.38f, -sz.Z * 0.5f - 0.08f), body);
        }
        else if (k == "domuz")
        {
            Box(_model, new Vector3(0.07f, 0.07f, 0.12f), new Vector3(0, bodyY + sz.Y * 0.25f, -sz.Z * 0.5f - 0.05f), acc);
            Box(_model, new Vector3(0.07f, 0.1f, 0.07f), new Vector3(0, bodyY + sz.Y * 0.33f, -sz.Z * 0.5f - 0.1f), acc);
        }
        else
        {
            float tl = Spec.Predator ? sz.Z * 0.45f : sz.Z * 0.2f;
            float tw = (k == "tilki" || k == "kurt") ? 0.2f : 0.12f;
            Box(_model, new Vector3(tw, tw, tl), new Vector3(0, bodyY + sz.Y * 0.2f, -sz.Z * 0.5f - tl * 0.45f), tail);
            if (k == "tilki") Box(_model, new Vector3(tw * 0.95f, tw * 0.95f, tl * 0.35f), new Vector3(0, bodyY + sz.Y * 0.2f, -sz.Z * 0.5f - tl * 0.95f), Colors.White);
        }

        // ---- bacaklar ----
        float lx = sz.X * 0.34f, lz = sz.Z * 0.34f;
        float lw = Mathf.Max(0.1f, sz.X * 0.2f);
        Vector2[] pos = { new(lx, lz), new(-lx, lz), new(lx, -lz), new(-lx, -lz) };
        bool hooved = k == "inek" || k == "koyun" || k == "domuz" || k == "at" || k == "geyik";
        Color legCol = Spec.Predator ? acc : (wool ? acc : body);
        for (int i = 0; i < 4; i++)
        {
            var piv = new Node3D { Position = new Vector3(pos[i].X, Spec.LegH, pos[i].Y) };
            _model.AddChild(piv);
            Box(piv, new Vector3(lw, Spec.LegH, lw), new Vector3(0, -Spec.LegH * 0.5f, 0), legCol);
            Box(piv, new Vector3(lw * 1.08f, Mathf.Min(0.1f, Spec.LegH * 0.3f), lw * 1.15f), new Vector3(0, -Spec.LegH + 0.04f, 0.01f), hooved ? hoof : legCol.Lerp(Colors.White, 0.5f));
            _legs[i] = piv;
        }
    }

    private static void Box(Node3D parent, Vector3 size, Vector3 pos, Color c)
    {
        var mi = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = PixelMat.Get(c),
            Position = pos,
        };
        parent.AddChild(mi);
    }

    private void ApplyAgeScale()
    {
        float k = IsBaby ? Mathf.Lerp(0.5f, 1f, Mathf.Clamp(Age / BabyTime, 0f, 1f)) : 1f;
        Scale = new Vector3(k, k, k);
    }

    public void SetNet(Vector3 pos, float yaw, int state, float age)
    {
        if (!_netInit) { GlobalPosition = pos; _netInit = true; }
        _netPos = pos; _netYaw = yaw; NetState = state; Age = age;
    }

    private void PuppetTick(float dt)
    {
        if (!_netInit || _model == null) return;
        Vector3 p = GlobalPosition;
        Vector3 np = p.Lerp(_netPos, Mathf.Min(1f, dt * 10f));
        _speedNow = new Vector2(np.X - p.X, np.Z - p.Z).Length() / Mathf.Max(0.001f, dt);
        GlobalPosition = np;
        Rotation = new Vector3(0f, Mathf.LerpAngle(Rotation.Y, _netYaw, Mathf.Min(1f, dt * 10f)), 0f);
        _state = (AnimalState)NetState;
        ApplyAgeScale();
        Animate(dt);
    }

    public override void _Process(double delta)
    {
        if (Puppet) { PuppetTick((float)delta); return; }
        if (World == null || Dir == null || Dir.Player == null) return;
        float dt = (float)delta;
        Vector3 pp = Dir.Player.GlobalPosition;
        float pd = GlobalPosition.DistanceTo(pp);

        if (!Owned && pd > 85f) { Dir.Remove(this); QueueFree(); return; }

        _accum += dt;
        if (pd > 48f && _accum < 0.4f) return;
        dt = _accum;
        _accum = 0f;

        if (IsBaby) { Age += dt; ApplyAgeScale(); }
        LoveTime = Mathf.Max(0f, LoveTime - dt);
        LoveCooldown = Mathf.Max(0f, LoveCooldown - dt);
        WoolTimer = Mathf.Max(0f, WoolTimer - dt);
        MilkTimer = Mathf.Max(0f, MilkTimer - dt);
        _biteCd = Mathf.Max(0f, _biteCd - dt);
        _fullT = Mathf.Max(0f, _fullT - dt);
        _stateTimer -= dt;

        if (Owned && Spec.Key == "tavuk" && !IsBaby)
        {
            _eggT -= dt;
            if (_eggT <= 0f)
            {
                _eggT = _rng.RandfRange(90f, 150f);
                ushort egg = BlockRegistry.IdOf("yumurta");
                ItemDrop.Spawn(Dir.DropParent, World, Dir.Player, Dir.Inv, egg, 1, GlobalPosition + new Vector3(0, 0.4f, 0), Vector3.Zero);
            }
        }

        _think -= dt;
        if (_think <= 0f) { _think = 0.35f; Think(pd); }

        Act(dt, pd);

        _sndT -= dt;
        if (_sndT <= 0f)
        {
            _sndT = _rng.RandfRange(9f, 22f);
            if (pd < 22f && !string.IsNullOrEmpty(Spec.Sound) && _state != AnimalState.Sleep)
                AudioManager.Play(Spec.Sound, _rng.RandfRange(0.9f, 1.15f) * (IsBaby ? 1.4f : 1f), -8f - pd * 0.5f);
        }

        if (pd < 45f) Animate(dt);
    }

    private void Think(float pd)
    {
        var p = Dir.Player;
        bool harmless = p == null || p.Creative || p.SafeMode || p.IsDead;
        bool night = Dir.Env != null && Dir.Env.DayFactor < 0.2f;
        string heldName = Dir.HeldName();
        bool holdingFood = Species.Eats(Spec, heldName);

        if (!Spec.Predator)
        {
            var pred = Dir.NearestPredator(this, 9f);
            if (pred != null) { SetFlee(pred.GlobalPosition, 3f); return; }
        }

        if (Spec.Predator)
        {
            if (Spec.Bite > 0f && !harmless && pd < Spec.ChaseDist * (night ? 1.5f : 1f))
            {
                _state = AnimalState.Chase; _chasePlayer = true; _victim = null; _stateTimer = 1f; return;
            }
            if (_fullT <= 0f && Spec.Prey.Length > 0)
            {
                var prey = Dir.NearestPrey(this, 13f);
                if (prey != null) { _state = AnimalState.Chase; _chasePlayer = false; _victim = prey; _stateTimer = 1f; return; }
            }
        }

        if (Spec.FleeDist > 0f && pd < Spec.FleeDist && !holdingFood && !(Owned && !IsBaby && Spec.Foods.Length > 0))
        {
            SetFlee(p.GlobalPosition, 2.5f); return;
        }

        if (holdingFood && pd < 10f && pd > 2.5f && !Spec.Predator)
        {
            _state = AnimalState.Follow; _stateTimer = 1f; return;
        }

        if (LoveTime > 0f)
        {
            _partner = Dir.FindPartner(this, 12f);
            if (_partner != null) { _state = AnimalState.Love; _stateTimer = 1f; return; }
        }

        if (_stateTimer > 0f && _state != AnimalState.Chase && _state != AnimalState.Flee
            && _state != AnimalState.Follow && _state != AnimalState.Love) return;

        _chasePlayer = false;
        _victim = null;
        _hasTarget = false;

        bool winter = SeasonClock.Instance != null && SeasonClock.Instance.Season == 3;
        if ((night && !Spec.Predator) || (winter && Spec.Key == "ayi"))
        {
            _state = AnimalState.Sleep; _stateTimer = _rng.RandfRange(6f, 12f); return;
        }
        float r = _rng.Randf();
        if (r < 0.42f) { _state = AnimalState.Wander; _stateTimer = _rng.RandfRange(4f, 9f); }
        else if (r < 0.65f && StandingOnGrass()) { _state = AnimalState.Graze; _stateTimer = _rng.RandfRange(3f, 6f); }
        else if (r < 0.78f && FindWaterSpot(out var w)) { _state = AnimalState.Drink; _target = w; _hasTarget = true; _stateTimer = _rng.RandfRange(9f, 14f); }
        else { _state = AnimalState.Rest; _stateTimer = _rng.RandfRange(3f, 7f); }
    }

    private void SetFlee(Vector3 from, float seconds)
    {
        Vector3 away = GlobalPosition - from; away.Y = 0f;
        if (away.LengthSquared() < 0.01f) away = new Vector3(_rng.RandfRange(-1f, 1f), 0f, _rng.RandfRange(-1f, 1f));
        away = away.Normalized();
        _target = GlobalPosition + away * 10f;
        _hasTarget = true;
        _state = AnimalState.Flee;
        _stateTimer = seconds;
    }

    private void Act(float dt, float pd)
    {
        _speedNow = 0f;
        Vector3 pos = GlobalPosition;

        if (_kb.LengthSquared() > 0.01f)
        {
            TryMove(_kb * dt, false);
            _kb = _kb.Lerp(Vector3.Zero, dt * 6f);
        }

        switch (_state)
        {
            case AnimalState.Wander:
                if (!_hasTarget || Flat(_target - pos).Length() < 0.8f)
                {
                    float a = _rng.Randf() * Mathf.Tau, d = _rng.RandfRange(3f, 8f);
                    _target = pos + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                    _hasTarget = true;
                }
                if (!MoveToward(_target, Spec.Walk, dt)) _hasTarget = false;
                break;

            case AnimalState.Drink:
                if (_hasTarget && Flat(_target - pos).Length() > 1.1f)
                { if (!MoveToward(_target, Spec.Walk, dt)) { _hasTarget = false; _stateTimer = 0f; } }
                break;

            case AnimalState.Flee:
                if (!MoveToward(_target, Spec.Run, dt)) _stateTimer = 0f;
                break;

            case AnimalState.Follow:
                if (pd > 2.5f) MoveToward(Dir.Player.GlobalPosition, Spec.Walk, dt);
                break;

            case AnimalState.Love:
                if (_partner == null || !IsInstanceValid(_partner) || _partner.LoveTime <= 0f) { _state = AnimalState.Idle; break; }
                MoveToward(_partner.GlobalPosition, Spec.Walk, dt);
                if (pos.DistanceTo(_partner.GlobalPosition) < 1.7f) Dir.Breed(this, _partner);
                break;

            case AnimalState.Chase:
                Vector3 goal;
                float reach;
                if (_chasePlayer) { goal = Dir.Player.GlobalPosition; reach = 1.7f; }
                else if (_victim != null && IsInstanceValid(_victim)) { goal = _victim.GlobalPosition; reach = 1.1f; }
                else { _state = AnimalState.Idle; break; }
                float dist = Flat(goal - pos).Length();
                if (dist > reach) MoveToward(goal, Spec.Run, dt);
                else if (_biteCd <= 0f)
                {
                    _biteCd = 1.3f;
                    if (_chasePlayer) { if (Spec.Bite > 0f) Dir.Player.Damage(Spec.Bite, "hayvan saldırısı"); }
                    else { _victim.Hurt(99f, pos, true); _fullT = 45f; _state = AnimalState.Idle; _stateTimer = 3f; }
                    AudioManager.Play("hit", 0.9f, -6f);
                }
                if (_chasePlayer && (Dir.Player.Creative || Dir.Player.SafeMode || Dir.Player.IsDead || pd > Spec.ChaseDist * 2.2f))
                { _state = AnimalState.Idle; _chasePlayer = false; _stateTimer = 0f; }
                break;
        }
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0f, v.Z);

    private bool MoveToward(Vector3 goal, float speed, float dt)
    {
        Vector3 dir = Flat(goal - GlobalPosition);
        float len = dir.Length();
        if (len < 0.05f) return true;
        dir /= len;
        float yaw = Mathf.Atan2(dir.X, dir.Z);
        Rotation = new Vector3(0f, Mathf.LerpAngle(Rotation.Y, yaw, Mathf.Min(1f, dt * 9f)), 0f);
        float baby = IsBaby ? 0.8f : 1f;
        bool moved = TryMove(dir * speed * baby * dt, true);
        if (moved) _speedNow = speed * baby;
        return moved;
    }

    private bool TryMove(Vector3 delta, bool snap)
    {
        Vector3 p = GlobalPosition;
        Vector3 np = p + new Vector3(delta.X, 0f, delta.Z);
        float g = GroundAt(np.X, np.Z, p.Y);
        if (float.IsNaN(g)) return false;
        if (g - p.Y > 1.15f) return false;
        if (p.Y - g > 3.2f) return false;
        int ix = Mathf.FloorToInt(np.X), iz = Mathf.FloorToInt(np.Z);
        if (World.GetBlock(ix, Mathf.FloorToInt(g), iz).IsLiquid) return false;
        if (World.GetBlock(ix, Mathf.FloorToInt(g) + 1, iz).Solid) return false;
        float ny = Mathf.Lerp(p.Y, g, 0.4f);
        if (Mathf.Abs(ny - g) < 0.02f) ny = g;
        GlobalPosition = new Vector3(np.X, ny, np.Z);
        return true;
    }

    private float GroundAt(float x, float z, float refY)
    {
        int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
        int top = Mathf.FloorToInt(refY) + 2;
        for (int y = top; y >= top - 9; y--)
            if (World.GetBlock(ix, y, iz).Solid && !World.GetBlock(ix, y + 1, iz).Solid) return y + 1;
        return float.NaN;
    }

    private bool StandingOnGrass()
    {
        Vector3 p = GlobalPosition;
        return World.GetBlock(Mathf.FloorToInt(p.X), Mathf.FloorToInt(p.Y) - 1, Mathf.FloorToInt(p.Z)).Name == "grass";
    }

    private bool FindWaterSpot(out Vector3 spot)
    {
        spot = Vector3.Zero;
        Vector3 p = GlobalPosition;
        for (int i = 0; i < 14; i++)
        {
            float a = _rng.Randf() * Mathf.Tau, d = _rng.RandfRange(3f, 11f);
            int x = Mathf.FloorToInt(p.X + Mathf.Cos(a) * d), z = Mathf.FloorToInt(p.Z + Mathf.Sin(a) * d);
            int y = Mathf.FloorToInt(p.Y);
            for (int yy = y + 1; yy >= y - 3; yy--)
            {
                if (!World.GetBlock(x, yy, z).IsLiquid) continue;

                for (int k = 0; k < 4; k++)
                {
                    int dx = k == 0 ? 1 : (k == 1 ? -1 : 0), dz = k == 2 ? 1 : (k == 3 ? -1 : 0);
                    if (!World.GetBlock(x + dx, yy, z + dz).IsLiquid && World.GetBlock(x + dx, yy - 1, z + dz).Solid)
                    { spot = new Vector3(x + dx + 0.5f, yy, z + dz + 0.5f); return true; }
                }
            }
        }
        return false;
    }

    private void Animate(float dt)
    {
        bool moving = _speedNow > 0.05f;
        if (moving) _phase += _speedNow * dt * 3.4f;
        float amp = moving ? Mathf.Clamp(_speedNow / Mathf.Max(0.1f, Spec.Walk), 0.4f, 1.3f) * 0.75f : 0f;
        float swing = Mathf.Sin(_phase) * amp;
        _legs[0].Rotation = new Vector3(swing, 0, 0);
        _legs[3].Rotation = new Vector3(swing, 0, 0);
        _legs[1].Rotation = new Vector3(-swing, 0, 0);
        _legs[2].Rotation = new Vector3(-swing, 0, 0);

        float headTarget = (_state == AnimalState.Graze || (_state == AnimalState.Drink && Flat(_target - GlobalPosition).Length() <= 1.3f)) ? 0.85f : 0f;
        _headPitch = Mathf.Lerp(_headPitch, headTarget, Mathf.Min(1f, dt * 6f));
        _head.Rotation = new Vector3(_headPitch, 0, 0);

        float hop = Spec.Hops && moving ? Mathf.Abs(Mathf.Sin(_phase * 0.9f)) * 0.22f : 0f;
        float sleepY = _state == AnimalState.Sleep ? Spec.LegH * 0.85f : 0f;
        _model.Position = new Vector3(0, hop - sleepY, 0);
        float sy = _state == AnimalState.Sleep ? 0.8f : 1f;
        _model.Scale = _model.Scale.Lerp(new Vector3(1f, sy, 1f), Mathf.Min(1f, dt * 4f));
    }

    public void Hurt(float dmg, Vector3 from, bool silent = false, int killer = 0)
    {
        if (killer > 0) KillerPeer = killer;
        Hp -= dmg;
        Vector3 away = Flat(GlobalPosition - from);
        if (away.LengthSquared() > 0.001f) _kb = away.Normalized() * 4f;
        if (!silent) AudioManager.Play("hit", _rng.RandfRange(1.1f, 1.4f), -4f);
        Fx.Burst(GlobalPosition + new Vector3(0, HeightMid, 0), FxKind.Debris, Spec.Body, 6);
        if (Hp <= 0f) { Die(); return; }
        if (!Spec.Predator) SetFlee(from, 4f);
        else if (Spec.Bite > 0f) { _state = AnimalState.Chase; _chasePlayer = true; _stateTimer = 3f; }
    }

    private void Die()
    {
        var player = Dir.Player;
        Vector3 at = GlobalPosition + new Vector3(0, 0.6f, 0);
        int meat = Spec.Meat;
        var net = PixelBlock.Network.NetworkManager.Instance;
        if (KillerPeer > 1 && net != null)
        {

            if (meat > 0) net.GiveItem(KillerPeer, "et", _rng.RandiRange(Mathf.Max(1, meat - 1), meat));
            if (Spec.Key == "koyun") net.GiveItem(KillerPeer, "yun", 1);
        }
        else
        {
            if (meat > 0)
                ItemDrop.Spawn(Dir.DropParent, World, player, Dir.Inv, BlockRegistry.IdOf("et"), _rng.RandiRange(Mathf.Max(1, meat - 1), meat), at, new Vector3(0, 3f, 0));
            if (Spec.Key == "koyun")
                ItemDrop.Spawn(Dir.DropParent, World, player, Dir.Inv, BlockRegistry.IdOf("yun"), 1, at, new Vector3(0.5f, 3f, 0));
        }
        Fx.Burst(at, FxKind.Poof, new Color(0.95f, 0.95f, 0.95f), 14);
        Dir.Remove(this);
        QueueFree();
    }
}
