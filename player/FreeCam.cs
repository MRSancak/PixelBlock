using Godot;
using PixelBlock.Core;

namespace PixelBlock.PlayerSys;

public partial class FreeCam : Camera3D
{
    public float MouseSensitivity = 0.0026f;

    private const float MinSpeed = 0.5f, MaxSpeed = 80f, PitchLimit = 1.55f;
    private const float MoveStepCoarse = 2f, MoveStepFine = 0.5f;
    private static readonly float RotStepCoarse = Mathf.DegToRad(15f);
    private static readonly float RotStepFine = Mathf.DegToRad(5f);

    private float _speed = 8f;
    private Vector2 _mouseLook;

    public bool Active { get; private set; }

    public bool StepMode { get; private set; }

    public override void _Ready() => SetActive(false);

    public void SetActive(bool on)
    {
        Active = on;
        Current = on;
        _mouseLook = Vector2.Zero;
        SetProcess(on);
        SetProcessUnhandledInput(on);
    }

    [Export]
    public Vector3 AimAt
    {
        get => GlobalPosition;
        set
        {
            if (!IsInsideTree()) return;
            Vector3 dir = value - GlobalPosition;
            if (dir.LengthSquared() < 1e-6f) return;
            dir = dir.Normalized();
            if (Mathf.Abs(dir.Y) > 0.9995f)
            {
                Vector3 r = Rotation;
                r.X = Mathf.Asin(Mathf.Clamp(dir.Y, -1f, 1f));
                r.Z = 0f;
                Rotation = r;
            }
            else LookAt(value, Vector3.Up);
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Active) return;
        if (e is InputEventMouseMotion mm && Input.MouseMode == Input.MouseModeEnum.Captured)
            _mouseLook += mm.Relative;
        else if (e is InputEventMouseButton { Pressed: true } mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp) _speed = Mathf.Min(_speed * 1.12f, MaxSpeed);
            else if (mb.ButtonIndex == MouseButton.WheelDown) _speed = Mathf.Max(_speed / 1.12f, MinSpeed);
        }
    }

    public override void _Process(double delta)
    {
        if (!Active) return;
        float dt = (float)delta;
        if (Input.IsActionJustPressed(GameInput.Actions.FreeCamStep)) StepMode = !StepMode;
        ApplyMouseLook();
        if (StepMode) ApplyStep();
        else { ApplyContinuousLook(dt); ApplyContinuousMove(dt); }
    }

    private void RotateBy(float dyaw, float dpitch)
    {
        Vector3 rot = Rotation;
        rot.Y += dyaw;
        rot.X = Mathf.Clamp(rot.X + dpitch, -PitchLimit, PitchLimit);
        rot.Z = 0f;
        Rotation = rot;
    }

    private void ApplyMouseLook()
    {
        if (_mouseLook == Vector2.Zero) return;
        float sens = MouseSensitivity * Settings.MouseSensitivity;
        RotateBy(-_mouseLook.X * sens, -_mouseLook.Y * sens);
        _mouseLook = Vector2.Zero;
    }

    private void ApplyContinuousLook(float dt)
    {
        float rate = Mathf.DegToRad(Settings.KeyboardLookSpeed) * dt;
        float dyaw = (Input.GetActionStrength(GameInput.Actions.LookLeft)
                    - Input.GetActionStrength(GameInput.Actions.LookRight)) * rate;
        float dpitch = (Input.GetActionStrength(GameInput.Actions.LookUp)
                      - Input.GetActionStrength(GameInput.Actions.LookDown)) * rate;
        if (!Mathf.IsZeroApprox(dyaw) || !Mathf.IsZeroApprox(dpitch)) RotateBy(dyaw, dpitch);
    }

    private void ApplyContinuousMove(float dt)
    {
        Vector2 inp = Input.GetVector(GameInput.Actions.Left, GameInput.Actions.Right,
                                      GameInput.Actions.Forward, GameInput.Actions.Back);
        Vector3 dir = GlobalTransform.Basis * new Vector3(inp.X, 0, inp.Y);
        if (Input.IsActionPressed(GameInput.Actions.Jump)) dir.Y += 1f;
        if (Input.IsActionPressed(GameInput.Actions.Crouch)) dir.Y -= 1f;
        if (dir.LengthSquared() < 1e-6f) return;
        float speed = Input.IsActionPressed(GameInput.Actions.Sprint) ? _speed * 3f : _speed;
        GlobalPosition += dir.Normalized() * speed * dt;
    }

    private void ApplyStep()
    {
        bool fine = Input.IsActionPressed(GameInput.Actions.Sprint);
        float rs = fine ? RotStepFine : RotStepCoarse;
        float ms = fine ? MoveStepFine : MoveStepCoarse;

        float dyaw = 0f, dpitch = 0f;
        if (Input.IsActionJustPressed(GameInput.Actions.LookLeft)) dyaw += rs;
        if (Input.IsActionJustPressed(GameInput.Actions.LookRight)) dyaw -= rs;
        if (Input.IsActionJustPressed(GameInput.Actions.LookUp)) dpitch += rs;
        if (Input.IsActionJustPressed(GameInput.Actions.LookDown)) dpitch -= rs;
        if (dyaw != 0f || dpitch != 0f) RotateBy(dyaw, dpitch);

        Vector3 local = Vector3.Zero;
        if (Input.IsActionJustPressed(GameInput.Actions.Forward)) local.Z -= 1f;
        if (Input.IsActionJustPressed(GameInput.Actions.Back)) local.Z += 1f;
        if (Input.IsActionJustPressed(GameInput.Actions.Left)) local.X -= 1f;
        if (Input.IsActionJustPressed(GameInput.Actions.Right)) local.X += 1f;
        if (local != Vector3.Zero)
            GlobalPosition += (GlobalTransform.Basis * local).Normalized() * ms;
        if (Input.IsActionJustPressed(GameInput.Actions.Jump)) GlobalPosition += Vector3.Up * ms;
        if (Input.IsActionJustPressed(GameInput.Actions.Crouch)) GlobalPosition += Vector3.Down * ms;
    }

    public string StatusLine()
    {
        Vector3 p = GlobalPosition;
        float yaw = Mathf.Wrap(Mathf.RadToDeg(Rotation.Y), -180f, 180f);
        float pitch = Mathf.Wrap(Mathf.RadToDeg(Rotation.X), -180f, 180f);
        string mode = StepMode
            ? $"STEP {MoveStepCoarse:0.#}m / 15° (Shift: fine)"
            : $"fly  speed {_speed:0.#}";
        return $"pos {p.X:F1}, {p.Y:F1}, {p.Z:F1}    fov {Fov:F0}\n"
             + $"yaw {yaw:F0}°  pitch {pitch:F0}°    {mode}";
    }
}
