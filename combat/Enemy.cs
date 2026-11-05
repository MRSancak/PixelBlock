using Godot;
using PixelBlock.Core;

namespace PixelBlock.Combat;

public sealed class EnemyType
{
    public string Name = "Foe";
    public float Health = 30f;
    public float Speed = 3.2f;
    public float Damage = 8f;
    public float AttackRange = 1.8f;
    public float AttackCooldown = 1.1f;
    public float Scale = 1f;
    public bool Beast = false;
    public Color Skin = new(0.82f, 0.68f, 0.55f);
    public Color Cloth = new(0.45f, 0.4f, 0.35f);
    public Color Accent = new(0.5f, 0.3f, 0.25f);

    /// <summary>0 = her zaman kovalar; &gt;0 = bu mesafeden yakındaysa kovalar.</summary>
    public float AggroRange = 0f;
    public bool BurnsInDaylight = false;

    public string ModelScene = null;

    public float ModelYawDeg = 0f;

    public static EnemyType Soldier() => new()
    {
        Name = "Soldier", Health = 40f, Speed = 3.4f, Damage = 9f, AttackRange = 2.0f,
        Skin = new(0.82f, 0.66f, 0.52f), Cloth = new(0.35f, 0.33f, 0.4f), Accent = new(0.55f, 0.45f, 0.3f),
    };

    public static EnemyType Wolf() => new()
    {
        Name = "Wolf", Health = 22f, Speed = 5.0f, Damage = 6f, AttackRange = 1.6f, AttackCooldown = 0.9f,
        Beast = true, Skin = new(0.4f, 0.36f, 0.32f), Cloth = new(0.6f, 0.56f, 0.5f),
    };

    public static EnemyType Zombie() => new()
    {
        Name = "Zombi", Health = 30f, Speed = 2.5f, Damage = 7f, AttackRange = 1.7f, AttackCooldown = 1.2f,
        AggroRange = 24f, BurnsInDaylight = true,
        Skin = new(0.36f, 0.56f, 0.33f), Cloth = new(0.20f, 0.28f, 0.50f), Accent = new(0.26f, 0.36f, 0.52f),
    };

    public static EnemyType Skeleton() => new()
    {
        Name = "İskelet", Health = 22f, Speed = 3.6f, Damage = 6f, AttackRange = 1.8f, AttackCooldown = 0.9f,
        AggroRange = 28f, BurnsInDaylight = true,
        Skin = new(0.88f, 0.88f, 0.82f), Cloth = new(0.80f, 0.80f, 0.74f), Accent = new(0.72f, 0.72f, 0.68f),
    };

    public static EnemyType Spider() => new()
    {
        Name = "Gece Örümceği", Health = 18f, Speed = 5.4f, Damage = 5f, AttackRange = 1.5f, AttackCooldown = 0.8f,
        AggroRange = 22f, Beast = true, Scale = 0.9f,
        Skin = new(0.14f, 0.11f, 0.15f), Cloth = new(0.55f, 0.10f, 0.12f),
    };

    public static EnemyType Giant() => new()
    {
        Name = "Goliath", Health = 220f, Speed = 2.6f, Damage = 26f, AttackRange = 3.2f,
        AttackCooldown = 1.6f, Scale = 1.9f,
        Skin = new(0.78f, 0.6f, 0.46f), Cloth = new(0.32f, 0.3f, 0.34f), Accent = new(0.68f, 0.46f, 0.2f),
    };
}

public partial class Enemy : CharacterBody3D, IDamageable
{
    [Signal] public delegate void DefeatedEventHandler();

    public EnemyType Type = EnemyType.Soldier();
    public Node3D Target;
    public VoxelWorld World;
    public System.Func<float> DayFactor;
    public float Health;
    public float MaxHealth;

    private HealthBar3D _bar;
    private Node3D _modelNode;
    private ICharacterModel _model;
    private CapsuleShape3D _capsule;
    private bool _dead;
    private float _attackTimer;
    private Vector3 _lastPos;
    private float _burnT;
    private const float Gravity = 22f;

    public bool IsAlive => !_dead;

    public override void _Ready()
    {
        AddToGroup("enemy");
        MaxHealth = Type.Health;
        Health = MaxHealth;
        float s = Type.Scale;

        _modelNode = Type.Beast
            ? CharacterModel.BuildBeast(Type.Skin, Type.Cloth, Type.ModelScene, Type.ModelYawDeg)
            : CharacterModel.BuildHumanoid(Type.Skin, Type.Cloth, Type.Accent, Type.ModelScene, Type.ModelYawDeg);
        _model = (ICharacterModel)_modelNode;
        _modelNode.Scale = Vector3.One * s;
        AddChild(_modelNode);

        float height = (Type.Beast ? 1.0f : 1.9f) * s;
        float radius = (Type.Beast ? 0.5f : 0.35f) * s;
        _capsule = new CapsuleShape3D { Radius = radius, Height = Mathf.Max(height, radius * 2f + 0.1f) };
        var col = new CollisionShape3D { Shape = _capsule, Position = new Vector3(0, _capsule.Height * 0.5f, 0) };
        AddChild(col);

        _bar = new HealthBar3D { Position = new Vector3(0, height + 0.4f, 0) };
        AddChild(_bar);

        _lastPos = GlobalPosition;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_dead) return;
        float dt = (float)delta;
        Vector3 vel = Velocity;
        if (!IsOnFloor()) vel.Y = Mathf.Max(vel.Y - Gravity * dt, -50f);
        else if (vel.Y < 0) vel.Y = 0;

        if (Type.BurnsInDaylight && DayFactor != null)
        {
            _burnT += dt;
            if (_burnT >= 1f)
            {
                _burnT = 0f;
                if (DayFactor() > 0.6f && !Sheltered())
                {
                    Fx.Burst(GlobalPosition + new Vector3(0, 1.0f, 0), FxKind.Poof, new Color(1f, 0.55f, 0.15f), 10);
                    TakeDamage(4f, null);
                    if (_dead) return;
                }
            }
        }

        bool canSee = Target != null && (Target is not IDamageable d || d.IsAlive);
        if (canSee && Type.AggroRange > 0f && Target.GlobalPosition.DistanceTo(GlobalPosition) > Type.AggroRange) canSee = false;
        if (canSee)
        {
            Vector3 to = Target.GlobalPosition - GlobalPosition;
            var flat = new Vector3(to.X, 0, to.Z);
            float dist = flat.Length();

            if (dist > 0.05f) RotationDegrees = new Vector3(0, Mathf.RadToDeg(Mathf.Atan2(flat.X, flat.Z) + Mathf.Pi), 0);

            if (dist > Type.AttackRange)
            {
                Vector3 dir = flat.Normalized();
                vel.X = dir.X * Type.Speed;
                vel.Z = dir.Z * Type.Speed;
            }
            else
            {
                vel.X = Mathf.MoveToward(vel.X, 0, Type.Speed);
                vel.Z = Mathf.MoveToward(vel.Z, 0, Type.Speed);
                _attackTimer -= dt;
                if (_attackTimer <= 0f)
                {
                    _attackTimer = Type.AttackCooldown;
                    _model.Attack();
                    if (Target is IDamageable dd && dd.IsAlive) dd.TakeDamage(Type.Damage, this);
                }
            }
        }
        else
        {
            vel.X = Mathf.MoveToward(vel.X, 0, Type.Speed);
            vel.Z = Mathf.MoveToward(vel.Z, 0, Type.Speed);
        }

        var desired = new Vector3(vel.X, 0, vel.Z);
        Velocity = vel;
        MoveAndSlide();

        if (IsOnFloor() && desired.Length() > 0.5f)
        {
            float moved = new Vector3(GlobalPosition.X - _lastPos.X, 0, GlobalPosition.Z - _lastPos.Z).Length();
            if (moved < 0.02f)
            {
                float climb = StepUpHeight(desired.Normalized());
                if (climb > 0f)
                    Velocity = new Vector3(Velocity.X, Mathf.Sqrt(2f * Gravity * (climb + 0.35f)), Velocity.Z);
            }
        }
        _lastPos = GlobalPosition;

        _model.Animate(new Vector3(Velocity.X, 0, Velocity.Z).Length(), dt);
    }

    private bool Sheltered()
    {
        if (World == null) return false;
        int x = Mathf.FloorToInt(GlobalPosition.X), z = Mathf.FloorToInt(GlobalPosition.Z);
        int y = Mathf.FloorToInt(GlobalPosition.Y) + 2;
        for (int i = 0; i < 12; i++)
            if (World.IsSolid(new Vector3I(x, y + i, z))) return true;
        return false;
    }

    private float StepUpHeight(Vector3 dir)
    {
        if (World == null) return 0f;
        Vector3 ahead = GlobalPosition + dir * (_capsule.Radius + 0.4f);
        int ax = Mathf.FloorToInt(ahead.X), az = Mathf.FloorToInt(ahead.Z);
        int feetY = Mathf.FloorToInt(GlobalPosition.Y + 0.05f);
        int maxClimb = Mathf.Max(1, Mathf.CeilToInt(_capsule.Height * 0.5f));
        int headroom = Mathf.Max(2, Mathf.CeilToInt(_capsule.Height));
        for (int h = 1; h <= maxClimb; h++)
        {
            var foot = new Vector3I(ax, feetY + h - 1, az);
            var stand = new Vector3I(ax, feetY + h, az);
            if (!World.IsSolid(foot) || World.IsSolid(stand)) continue;
            bool clear = true;
            for (int c = 1; c <= headroom; c++)
                if (World.IsSolid(stand + new Vector3I(0, c, 0))) { clear = false; break; }
            if (clear) return h;
        }
        return 0f;
    }

    public void TakeDamage(float amount, Node3D source)
    {
        if (_dead) return;
        Health = Mathf.Max(0, Health - amount);
        _bar.SetFraction(Health / MaxHealth);
        Flash();
        _model.Squash();
        if (Health > 0) AudioManager.Play("hit");
        if (Health <= 0) Defeat();
    }

    private async void Flash()
    {
        SetHitFlash(true);
        await ToSignal(GetTree().CreateTimer(0.09), SceneTreeTimer.SignalName.Timeout);
        if (!_dead) SetHitFlash(false);
    }

    private void SetHitFlash(bool on) => _model.SetFlash(on);

    private void Defeat()
    {
        if (_dead) return;
        _dead = true;
        _bar.Visible = false;
        SetCollisionLayerValue(1, false);
        Velocity = Vector3.Zero;
        EmitSignal(SignalName.Defeated);
        AudioManager.Play("defeat");

        Fx.Burst(GlobalPosition + new Vector3(0, _capsule.Height * 0.5f, 0),
            FxKind.Poof, new Color(0.95f, 0.92f, 0.8f), 40);
        Fx.HitStop(0.06f);

        var tween = CreateTween();
        tween.TweenProperty(_modelNode, "scale", Vector3.One * 0.01f, 0.45f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(QueueFree));
    }
}
