using Godot;

namespace PixelBlock.Core;

public enum FxKind { Poof, Debris, Splash, Sparkle, Dust }

public sealed partial class Fx : Node3D
{
    public static Fx Instance { get; private set; }

    public static System.Action<float> OnShake;

    public static System.Action<Color, float> OnFlash;

    private const int PoolSize = 16;
    private GpuParticles3D[] _pool;
    private int _next;
    private Mesh _quad, _cube;
    private bool _hitStopped;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        _quad = MakeQuad();
        _cube = MakeCube();

        _pool = new GpuParticles3D[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var p = new GpuParticles3D
            {
                Name = $"Fx{i}",
                OneShot = true,
                Emitting = false,
                Explosiveness = 1f,
                Amount = 16,
                Lifetime = 0.7,
                ProcessMaterial = new ParticleProcessMaterial(),
                DrawPass1 = _quad,
            };
            AddChild(p);
            _pool[i] = p;
        }
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public static void Burst(Vector3 pos, FxKind kind, Color tint, int count = 0) =>
        Instance?.DoBurst(pos, kind, tint, count);

    public static void Shake(float trauma) => OnShake?.Invoke(trauma);

    public static void Flash(Color color, float amount) => OnFlash?.Invoke(color, amount);

    public static void HitStop(float seconds = 0.06f) => Instance?.DoHitStop(seconds);

    private void DoBurst(Vector3 pos, FxKind kind, Color tint, int count)
    {
        var p = _pool[_next];
        _next = (_next + 1) % PoolSize;
        var m = (ParticleProcessMaterial)p.ProcessMaterial;

        switch (kind)
        {
            case FxKind.Debris:
                p.DrawPass1 = _cube;
                m.Direction = Vector3.Up; m.Spread = 70f;
                m.InitialVelocityMin = 2.0f; m.InitialVelocityMax = 4.5f;
                m.Gravity = new Vector3(0, -14f, 0);
                m.ScaleMin = 0.6f; m.ScaleMax = 1.2f;
                m.AngularVelocityMin = -320f; m.AngularVelocityMax = 320f;
                p.Lifetime = 0.7; p.Amount = count > 0 ? count : 14;
                break;
            case FxKind.Poof:
                p.DrawPass1 = _quad;
                m.Direction = Vector3.Up; m.Spread = 80f;
                m.InitialVelocityMin = 1.2f; m.InitialVelocityMax = 3.0f;
                m.Gravity = new Vector3(0, -2f, 0);
                m.ScaleMin = 0.9f; m.ScaleMax = 2.2f;
                m.AngularVelocityMin = -60f; m.AngularVelocityMax = 60f;
                p.Lifetime = 0.6; p.Amount = count > 0 ? count : 12;
                break;
            case FxKind.Splash:
                p.DrawPass1 = _quad;
                m.Direction = Vector3.Up; m.Spread = 35f;
                m.InitialVelocityMin = 3.0f; m.InitialVelocityMax = 6.5f;
                m.Gravity = new Vector3(0, -16f, 0);
                m.ScaleMin = 0.4f; m.ScaleMax = 1.1f;
                m.AngularVelocityMin = 0f; m.AngularVelocityMax = 0f;
                p.Lifetime = 0.8; p.Amount = count > 0 ? count : 24;
                break;
            case FxKind.Sparkle:
                p.DrawPass1 = _quad;
                m.Direction = Vector3.Up; m.Spread = 90f;
                m.InitialVelocityMin = 0.4f; m.InitialVelocityMax = 1.8f;
                m.Gravity = new Vector3(0, 0.5f, 0);
                m.ScaleMin = 0.5f; m.ScaleMax = 1.3f;
                m.AngularVelocityMin = -120f; m.AngularVelocityMax = 120f;
                p.Lifetime = 0.9; p.Amount = count > 0 ? count : 16;
                break;
            default:
                p.DrawPass1 = _quad;
                m.Direction = Vector3.Up; m.Spread = 50f;
                m.InitialVelocityMin = 0.3f; m.InitialVelocityMax = 1.0f;
                m.Gravity = new Vector3(0, -1.2f, 0);
                m.ScaleMin = 0.6f; m.ScaleMax = 1.4f;
                m.AngularVelocityMin = 0f; m.AngularVelocityMax = 0f;
                p.Lifetime = 0.6; p.Amount = count > 0 ? count : 8;
                break;
        }
        m.Color = tint;
        p.GlobalPosition = pos;
        p.Restart();
    }

    private void DoHitStop(float seconds)
    {
        if (_hitStopped) return;
        _hitStopped = true;
        Engine.TimeScale = 0.05f;

        GetTree().CreateTimer(seconds, processAlways: true, processInPhysics: false, ignoreTimeScale: true)
            .Timeout += () => { Engine.TimeScale = 1f; _hitStopped = false; };
    }

    private static Mesh MakeQuad()
    {
        var mesh = new QuadMesh { Size = new Vector2(0.16f, 0.16f) };
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
            AlbedoTexture = SoftDot(),
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            BillboardKeepScale = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        });
        return mesh;
    }

    public static Texture2D SoftDot()
    {
        var grad = new Gradient();
        grad.SetColor(0, new Color(1f, 1f, 1f, 1f));
        grad.SetColor(1, new Color(1f, 1f, 1f, 0f));
        return new GradientTexture2D
        {
            Gradient = grad,
            Width = 64,
            Height = 64,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
        };
    }

    private static Mesh MakeCube()
    {
        var mesh = new BoxMesh { Size = new Vector3(0.12f, 0.12f, 0.12f) };
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 0.9f,
        });
        return mesh;
    }
}
