using Godot;
using PixelBlock.Characters;
using PixelBlock.Core;

namespace PixelBlock.PlayerSys;

public partial class Player : CharacterBody3D, IDamageable
{
    [Signal] public delegate void HealthChangedEventHandler(float current, float max);
    [Signal] public delegate void AirChangedEventHandler(float current, float max);
    [Signal] public delegate void DiedEventHandler();
    [Signal] public delegate void HungerChangedEventHandler(float current, float max);
    [Signal] public delegate void ArmorChangedEventHandler();

    [Signal] public delegate void HurtEventHandler(float amount, string cause);

    public VoxelWorld World;
    public float MouseSensitivity = 0.0026f;

    private const float WalkSpeed = 4.6f, SprintSpeed = 7.2f, CrouchSpeed = 2.4f;
    private const float SwimSpeed = 3.6f, FlySpeed = 9f, SwimClimbVel = 7f;
    private const float Accel = 12f, AirAccel = 4f, Gravity = 24f, MaxFall = 45f;
    private const float JumpVel = 8.0f;
    private const float FallSafe = 5f, FallDamagePerBlock = 7f;

    private const float StandHeight = 1.75f, CrouchHeight = 1.15f;
    private const float EyeStand = 1.55f, EyeCrouch = 0.95f, Radius = 0.32f;

    public float MaxHealth = 100f, Health = 100f;
    public float MaxAir = 10f, Air = 10f;
    public float MaxHunger = 20f, Hunger = 20f;
    public bool HungerEnabled = false;

    public readonly ushort[] Armor = new ushort[4];

    public float ArmorPoints
    {
        get
        {
            float p = 0f;
            foreach (ushort id in Armor) if (id != 0) p += BlockRegistry.Get(id).ArmorValue;
            return p;
        }
    }

    public ushort SetArmor(int slot, ushort id)
    {
        if (slot < 0 || slot > 3) return 0;
        ushort old = Armor[slot];
        Armor[slot] = id;
        EmitSignal(SignalName.ArmorChanged);
        return old;
    }
    private float _starveTimer, _regenTimer;
    public bool Creative = false;
    public bool InputEnabled = true;

    public bool SafeMode = false;
    public bool IsDead { get; private set; }
    public bool InWater { get; private set; }
    public bool HeadUnderwater { get; private set; }

    public bool Sprinting { get; private set; }

    public float CollisionHeight => _crouching ? CrouchHeight : StandHeight;

    public bool ActionsSuppressed => _actionLock > 0f;

    private Node3D _head;
    private Camera3D _cam;
    private float _baseFov;
    private CollisionShape3D _shape;
    private CapsuleShape3D _capsule;
    private bool _crouching;
    private float _airApexY;
    private bool _wasOnFloor = true;
    private float _drownTimer;
    private float _actionLock;
    private Vector2 _mouseLook;
    private float _stepDist;
    private bool _stepFlip;
    private bool _wasInWater;
    private float _hurtCd;
    private GpuParticles3D _bubbles;

    public Camera3D Camera => _cam;
    public Node3D Head => _head;
    public bool Crouching => _crouching;

    public bool ThirdPerson { get; private set; }
    public float TpsDistance = 4f;
    public AvatarModel Avatar { get; private set; }
    private Camera3D _tpsCam;

    public void SetThirdPerson(bool on)
    {
        ThirdPerson = on;
        if (Avatar != null) Avatar.Visible = on;
        if (on && _tpsCam != null) _tpsCam.Current = true;
        else if (_cam != null) _cam.Current = true;
    }

    public override void _Process(double delta)
    {
        if (!ThirdPerson || _tpsCam == null || Avatar == null) return;
        float dt = (float)delta;

        Vector3 origin = _head.GlobalPosition;
        Vector3 back = _head.GlobalTransform.Basis.Z.Normalized();
        float dist = TpsDistance;
        if (World != null)
        {
            var hit = VoxelRay.Cast(World, origin, back, dist + 0.4f);
            if (hit.Ok) dist = Mathf.Clamp(hit.Distance - 0.35f, 0.3f, dist);
        }
        var t = _head.GlobalTransform;
        t.Origin = origin + back * dist;
        _tpsCam.GlobalTransform = t;
        Avatar.Visible = dist > 0.9f;

        bool mouse = Input.MouseMode == Input.MouseModeEnum.Captured;
        bool acting = InputEnabled && mouse && (Input.IsActionPressed(GameInput.Actions.Primary) || Input.IsActionPressed(GameInput.Actions.Secondary));
        float hs = new Vector2(Velocity.X, Velocity.Z).Length();
        Avatar.Tick(dt, hs, IsOnFloor() || Creative, _crouching, Input.IsActionPressed(GameInput.Actions.Sprint), acting);
    }

    public override void _Ready()
    {
        GameInput.Setup();
        AddToGroup("player");

        _capsule = new CapsuleShape3D { Radius = Radius, Height = StandHeight };
        _shape = new CollisionShape3D { Shape = _capsule, Position = new Vector3(0, StandHeight * 0.5f, 0) };
        AddChild(_shape);

        _head = new Node3D { Name = "Head", Position = new Vector3(0, EyeStand, 0) };
        AddChild(_head);
        _cam = new Camera3D { Name = "Camera", Fov = 75f };
        _head.AddChild(_cam);
        _baseFov = _cam.Fov;

        _tpsCam = new Camera3D { Name = "TpsCamera", Fov = 75f, TopLevel = true };
        AddChild(_tpsCam);
        Avatar = new AvatarModel { Name = "AvatarModel", Visible = false, Rotation = new Vector3(0f, Mathf.Pi, 0f) };
        AddChild(Avatar);
        Avatar.Build(CharacterProfile.Current);

        _bubbles = MakeBubbles();
        _head.AddChild(_bubbles);
        _bubbles.Position = new Vector3(0, 0.05f, -0.25f);

        _airApexY = GlobalPosition.Y;
        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
        EmitSignal(SignalName.AirChanged, Air, MaxAir);
        EmitSignal(SignalName.HungerChanged, Hunger, MaxHunger);
    }

    public void MakeCurrent()
    {
        if (ThirdPerson && _tpsCam != null) _tpsCam.Current = true; else _cam.Current = true;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public void SuppressActionsFor(float seconds)
    {
        if (seconds > _actionLock) _actionLock = seconds;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!InputEnabled) return;

        if (e is InputEventMouseMotion mm && Input.MouseMode == Input.MouseModeEnum.Captured)
        {

            _mouseLook += mm.Relative;
        }
        else if (e.IsActionPressed(GameInput.Actions.ToggleMode))
        {

            SetCreative(!Creative);
        }
        else if (e.IsActionPressed(GameInput.Actions.CameraToggle)) SetThirdPerson(!ThirdPerson);
        else if (e.IsActionPressed(GameInput.Actions.ZoomIn)) TpsDistance = Mathf.Max(1.5f, TpsDistance - 0.6f);
        else if (e.IsActionPressed(GameInput.Actions.ZoomOut)) TpsDistance = Mathf.Min(9f, TpsDistance + 0.6f);
    }

    private void ApplyMouseLook()
    {
        if (_mouseLook == Vector2.Zero) return;
        if (InputEnabled && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            float sens = MouseSensitivity * Core.Settings.MouseSensitivity;
            RotateY(-_mouseLook.X * sens);
            float pitch = Mathf.Clamp(_head.Rotation.X - _mouseLook.Y * sens, -1.5f, 1.5f);
            _head.Rotation = new Vector3(pitch, 0, 0);
        }
        _mouseLook = Vector2.Zero;
    }

    private void KeyboardLook(float dt)
    {
        if (!InputEnabled) return;
        float rate = Mathf.DegToRad(Core.Settings.KeyboardLookSpeed) * dt;
        float yaw = Input.GetActionStrength(GameInput.Actions.LookLeft)
                  - Input.GetActionStrength(GameInput.Actions.LookRight);
        float pitch = Input.GetActionStrength(GameInput.Actions.LookUp)
                    - Input.GetActionStrength(GameInput.Actions.LookDown);
        if (!Mathf.IsZeroApprox(yaw)) RotateY(yaw * rate);
        if (!Mathf.IsZeroApprox(pitch))
            _head.Rotation = new Vector3(
                Mathf.Clamp(_head.Rotation.X + pitch * rate, -1.5f, 1.5f), 0, 0);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (_actionLock > 0f) _actionLock -= dt;
        if (_hurtCd > 0f) _hurtCd -= dt;
        UpdateWaterState();
        UpdateCrouch();

        if (IsDead) { Velocity = Velocity.Lerp(Vector3.Zero, dt * 4f); MoveAndSlide(); return; }

        ApplyMouseLook();
        KeyboardLook(dt);

        if (!Creative && World != null && World.StreamingHold(GlobalPosition))
        {
            Velocity = Vector3.Zero;
            return;
        }

        if (Creative) FlyMove(dt);
        else if (InWater) SwimMove(dt);
        else GroundMove(dt);

        UpdateAir(dt);
        UpdateHunger(dt);
        CheckHazards(dt);
        UpdateFov(dt);
    }

    private void UpdateFov(float dt)
    {
        if (_cam == null) return;
        float target = Sprinting ? _baseFov + 8f : _baseFov;
        _cam.Fov = Mathf.MoveToward(_cam.Fov, target, 45f * dt);
    }

    private Vector3 WishDir()
    {
        if (!InputEnabled) return Vector3.Zero;
        Vector2 inp = Input.GetVector(GameInput.Actions.Left, GameInput.Actions.Right,
                                      GameInput.Actions.Forward, GameInput.Actions.Back);
        Vector3 dir = Transform.Basis * new Vector3(inp.X, 0, inp.Y);
        return dir.LengthSquared() > 0.0001f ? dir.Normalized() : Vector3.Zero;
    }

    private void GroundMove(float dt)
    {
        Vector3 vel = Velocity;
        if (!IsOnFloor()) vel.Y = Mathf.Max(vel.Y - Gravity * dt, -MaxFall);

        Vector3 dir = WishDir();
        float speed = _crouching ? CrouchSpeed
            : (InputEnabled && Input.IsActionPressed(GameInput.Actions.Sprint) ? SprintSpeed : WalkSpeed);
        float a = IsOnFloor() ? Accel : AirAccel;
        Sprinting = IsOnFloor() && !_crouching && dir != Vector3.Zero
                    && InputEnabled && Input.IsActionPressed(GameInput.Actions.Sprint);

        var horiz = new Vector3(vel.X, 0, vel.Z);
        horiz = horiz.MoveToward(dir * speed, a * dt * speed);
        vel.X = horiz.X;
        vel.Z = horiz.Z;

        if (IsOnFloor() && InputEnabled && Input.IsActionJustPressed(GameInput.Actions.Jump) && !_crouching)
        {
            vel.Y = JumpVel;
            AudioManager.Play("jump", 1f, -4f);
        }

        Velocity = vel;
        MoveAndSlide();
        Footsteps(dt);
        TrackFall();
    }

    private void Footsteps(float dt)
    {
        if (!IsOnFloor()) { _stepDist = 0f; return; }
        float hsp = new Vector2(Velocity.X, Velocity.Z).Length();
        if (hsp < 0.7f) { _stepDist = 0f; return; }
        _stepDist += hsp * dt;
        float stride = _crouching ? 1.4f : (hsp > 5.5f ? 2.4f : 1.9f);
        if (_stepDist >= stride)
        {
            _stepDist = 0f;
            _stepFlip = !_stepFlip;
            AudioManager.Play($"step_{GroundMaterial()}", _stepFlip ? 1.08f : 0.92f, -5f);

            if (World != null)
            {
                var gb = World.GetBlock(FloorV(GlobalPosition + new Vector3(0, -0.2f, 0)));
                if (!gb.IsAir && !gb.IsLiquid)
                    Fx.Burst(GlobalPosition, FxKind.Dust, DustTint(gb.Material), 4);
            }
        }
    }

    private static Color DustTint(MaterialSound m) => m switch
    {
        MaterialSound.Sand => new Color(0.85f, 0.74f, 0.50f, 0.5f),
        MaterialSound.Grass => new Color(0.45f, 0.58f, 0.30f, 0.5f),
        MaterialSound.Snow => new Color(0.92f, 0.95f, 1.00f, 0.5f),
        MaterialSound.Wood => new Color(0.55f, 0.43f, 0.28f, 0.5f),
        MaterialSound.Dirt => new Color(0.52f, 0.40f, 0.28f, 0.5f),
        MaterialSound.Metal => new Color(0.60f, 0.60f, 0.65f, 0.45f),
        MaterialSound.Cloth => new Color(0.70f, 0.66f, 0.60f, 0.45f),
        _ => new Color(0.62f, 0.62f, 0.64f, 0.5f),
    };

    private MaterialSound GroundMaterial()
    {
        if (World == null) return MaterialSound.Dirt;
        var b = World.GetBlock(FloorV(GlobalPosition + new Vector3(0, -0.2f, 0)));
        return b.IsAir || b.IsLiquid ? MaterialSound.Dirt : b.Material;
    }

    private void SwimMove(float dt)
    {
        Sprinting = false;
        Vector3 vel = Velocity;
        if (vel.Y < -5f) vel.Y = -5f;
        Vector3 dir = WishDir();

        float vy = 0;
        if (InputEnabled && Input.IsActionPressed(GameInput.Actions.Jump)) vy += 1;
        if (InputEnabled && Input.IsActionPressed(GameInput.Actions.Crouch)) vy -= 1;

        Vector3 target = dir * SwimSpeed + Vector3.Up * (vy * SwimSpeed);
        vel = vel.MoveToward(target, Accel * dt * SwimSpeed);
        if (Mathf.IsZeroApprox(vy))
        {

            float targetY = HeadUnderwater ? 0f : -0.4f;
            vel.Y = Mathf.MoveToward(vel.Y, targetY, Gravity * 0.4f * dt);
        }

        TrySwimClimb(dir, ref vel);

        Velocity = vel;
        MoveAndSlide();

        _airApexY = GlobalPosition.Y;
        _wasOnFloor = IsOnFloor();
    }

    private void TrySwimClimb(Vector3 wishDir, ref Vector3 vel)
    {
        if (World == null) return;
        var flat = new Vector3(wishDir.X, 0, wishDir.Z);
        if (flat.LengthSquared() < 0.04f) return;
        flat = flat.Normalized();
        Vector3 ahead = GlobalPosition + flat * (Radius + 0.25f);
        int ax = Mathf.FloorToInt(ahead.X), az = Mathf.FloorToInt(ahead.Z);
        int feetCell = Mathf.FloorToInt(GlobalPosition.Y + 0.1f);
        for (int y = feetCell; y <= feetCell + 1; y++)
        {
            var ledge = new Vector3I(ax, y, az);
            if (World.IsSolid(ledge)
                && !World.IsSolid(ledge + new Vector3I(0, 1, 0))
                && !World.IsSolid(ledge + new Vector3I(0, 2, 0)))
            {
                if (vel.Y < SwimClimbVel) vel.Y = SwimClimbVel;
                return;
            }
        }
    }

    private void FlyMove(float dt)
    {
        Sprinting = false;
        Vector3 dir = Vector3.Zero;
        if (InputEnabled)
        {
            Vector2 inp = Input.GetVector(GameInput.Actions.Left, GameInput.Actions.Right,
                                          GameInput.Actions.Forward, GameInput.Actions.Back);
            dir = _head.GlobalTransform.Basis * new Vector3(inp.X, 0, inp.Y);
            if (Input.IsActionPressed(GameInput.Actions.Jump)) dir.Y += 1;
            if (Input.IsActionPressed(GameInput.Actions.Crouch)) dir.Y -= 1;
        }
        if (dir.LengthSquared() > 0.0001f) dir = dir.Normalized();
        float speed = (InputEnabled && Input.IsActionPressed(GameInput.Actions.Sprint)) ? FlySpeed * 2f : FlySpeed;
        Velocity = Velocity.MoveToward(dir * speed, Accel * dt * speed);
        MoveAndSlide();
        _airApexY = GlobalPosition.Y;
        _wasOnFloor = true;
    }

    private void TrackFall()
    {
        bool onFloor = IsOnFloor();
        if (!onFloor)
            _airApexY = Mathf.Max(_airApexY, GlobalPosition.Y);
        else if (!_wasOnFloor)
        {
            float fall = _airApexY - GlobalPosition.Y;
            if (!InWater && fall > 0.4f)
                AudioManager.Play("land", 1f, Mathf.Lerp(-8f, 0f, Mathf.Min(fall / 6f, 1f)));

            if (!InWater && fall > 1.2f)
                Fx.Shake(Mathf.Clamp((fall - 1.2f) / 8f, 0.06f, 0.5f));

            if (!InWater && fall > 0.6f && World != null)
            {
                var lb = World.GetBlock(FloorV(GlobalPosition + new Vector3(0, -0.2f, 0)));
                if (!lb.IsAir && !lb.IsLiquid)
                    Fx.Burst(GlobalPosition, FxKind.Poof, DustTint(lb.Material),
                        Mathf.Clamp((int)(6 + fall * 2f), 6, 18));
            }
            if (!InWater && fall > FallSafe)
                Damage((fall - FallSafe) * FallDamagePerBlock, "fall");
            _airApexY = GlobalPosition.Y;
        }
        if (onFloor) _airApexY = GlobalPosition.Y;
        _wasOnFloor = onFloor;
    }

    private void UpdateWaterState()
    {
        if (World == null) { InWater = HeadUnderwater = false; return; }
        Vector3 p = GlobalPosition;
        float eye = _crouching ? EyeCrouch : EyeStand;

        InWater = World.GetBlock(FloorV(p + new Vector3(0, 0.1f, 0))).IsLiquid;
        HeadUnderwater = World.GetBlock(FloorV(p + new Vector3(0, eye, 0))).IsLiquid;
        if (InWater && !_wasInWater)
        {
            AudioManager.Play("splash");
            Fx.Burst(GlobalPosition, FxKind.Splash, new Color(0.72f, 0.86f, 1f), 24);
            World.AddRipple(GlobalPosition, 1.0f);
            Fx.Shake(0.08f);
        }
        if (_bubbles != null) _bubbles.Emitting = HeadUnderwater;
        _wasInWater = InWater;
    }

    private static GpuParticles3D MakeBubbles()
    {
        var mat = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.22f,
            Direction = Vector3.Up,
            Spread = 22f,
            Gravity = new Vector3(0, 1.3f, 0),
            InitialVelocityMin = 0.4f,
            InitialVelocityMax = 1.0f,
            ScaleMin = 0.4f,
            ScaleMax = 1.0f,
            Color = new Color(0.85f, 0.95f, 1f, 0.7f),
        };
        var mesh = new QuadMesh { Size = new Vector2(0.05f, 0.05f) };
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        });
        return new GpuParticles3D
        {
            Name = "Bubbles",
            Amount = 16,
            Lifetime = 1.6f,
            Emitting = false,
            LocalCoords = false,
            ProcessMaterial = mat,
            DrawPass1 = mesh,
        };
    }

    private void UpdateCrouch()
    {
        bool want = InputEnabled && !Creative && !InWater && Input.IsActionPressed(GameInput.Actions.Crouch);

        if (!want && _crouching && World != null && World.IsSolid(FloorV(GlobalPosition + new Vector3(0, StandHeight, 0))))
            want = true;
        if (want == _crouching) return;
        _crouching = want;
        float h = _crouching ? CrouchHeight : StandHeight;
        _capsule.Height = h;
        _shape.Position = new Vector3(0, h * 0.5f, 0);
        _head.Position = new Vector3(0, _crouching ? EyeCrouch : EyeStand, 0);
    }

    private void UpdateAir(float dt)
    {
        float before = Air;
        if (HeadUnderwater)
        {
            Air = Mathf.Max(0, Air - dt);
            if (Air <= 0)
            {
                _drownTimer += dt;
                if (_drownTimer >= 1f) { Damage(4f, "drown"); _drownTimer -= 1f; }
            }
        }
        else
        {
            Air = Mathf.Min(MaxAir, Air + dt * 4f);
            _drownTimer = 0;
        }
        if (!Mathf.IsEqualApprox(before, Air)) EmitSignal(SignalName.AirChanged, Air, MaxAir);
    }

    private void UpdateHunger(float dt)
    {
        if (!HungerEnabled || Creative || SafeMode || IsDead) return;
        float before = Hunger;
        float rate = 0.0125f * (Sprinting ? 2.6f : 1f);
        Hunger = Mathf.Max(0f, Hunger - rate * dt);

        if (Hunger >= MaxHunger * 0.7f && Health < MaxHealth)
        {
            _regenTimer += dt;
            if (_regenTimer >= 2f)
            {
                _regenTimer = 0f;
                Heal(1f);
                Hunger = Mathf.Max(0f, Hunger - 0.25f);
            }
        }
        else _regenTimer = 0f;

        if (Hunger <= 0f)
        {
            _starveTimer += dt;
            if (_starveTimer >= 4f)
            {
                _starveTimer = 0f;
                if (Health > 2f) Damage(1.5f, "starve");
            }
        }
        else _starveTimer = 0f;

        if (!Mathf.IsEqualApprox(before, Hunger)) EmitSignal(SignalName.HungerChanged, Hunger, MaxHunger);
    }

    public void Eat(float amount)
    {
        if (IsDead) return;
        Hunger = Mathf.Min(MaxHunger, Hunger + amount);
        EmitSignal(SignalName.HungerChanged, Hunger, MaxHunger);
    }

    public void SetHunger(float value)
    {
        Hunger = Mathf.Clamp(value, 0f, MaxHunger);
        EmitSignal(SignalName.HungerChanged, Hunger, MaxHunger);
    }

    private void CheckHazards(float dt)
    {
        if (World == null || Creative) return;
        var b = World.GetBlock(FloorV(GlobalPosition + new Vector3(0, 0.5f, 0)));
        if (b.Hazard) Damage(b.HazardDamage * dt, "hazard");
    }

    private static Vector3I FloorV(Vector3 p) =>
        new(Mathf.FloorToInt(p.X), Mathf.FloorToInt(p.Y), Mathf.FloorToInt(p.Z));

    public bool IsAlive => !IsDead;
    public void TakeDamage(float amount, Node3D source) => Damage(amount, "hit");

    public void Damage(float amount, string cause = "")
    {
        if (IsDead || Creative || SafeMode || amount <= 0) return;
        if (cause != "drown" && cause != "starve")
            amount *= 1f - Mathf.Min(0.8f, ArmorPoints * 0.04f);
        Health = Mathf.Max(0, Health - amount);
        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
        EmitSignal(SignalName.Hurt, amount, cause);

        if (amount > 1.5f && _hurtCd <= 0f) { AudioManager.Play("hurt"); _hurtCd = 0.25f; }
        if (Health <= 0) Die();
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        Health = Mathf.Min(MaxHealth, Health + amount);
        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
    }

    public void Respawn(Vector3 at)
    {
        IsDead = false;
        Health = MaxHealth;
        Air = MaxAir;
        Hunger = MaxHunger;
        EmitSignal(SignalName.HungerChanged, Hunger, MaxHunger);
        _drownTimer = 0;
        Velocity = Vector3.Zero;
        GlobalPosition = at;
        _airApexY = at.Y;
        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
        EmitSignal(SignalName.AirChanged, Air, MaxAir);
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;
        EmitSignal(SignalName.Died);
    }

    public void SetCreative(bool on)
    {
        Creative = on;
        if (on) Velocity = new Vector3(Velocity.X, 0, Velocity.Z);
    }
}
