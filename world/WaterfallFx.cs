using Godot;

namespace PixelBlock.World;

public static class WaterfallFx
{
    public static Node3D Build(Vector3 lipCenter, Vector3 baseCenter, float width, float fallHeight, Vector3 spillDir)
    {
        spillDir = spillDir.Normalized();
        float hw = width * 0.5f;
        var root = new Node3D { Name = "WaterfallFx" };

        root.AddChild(Emitter("LipSpray", lipCenter,
            box: new Vector3(hw, 0.15f, 0.25f),
            dir: (spillDir * 0.7f + Vector3.Down * 0.5f).Normalized(), spread: 30f,
            vMin: 1.4f, vMax: 3.2f, gravity: -9f, spin: 220f,
            amount: 40, life: 0.9f, sMin: 0.5f, sMax: 1.0f, peak: 0.9f,
            tint: new Color(0.95f, 0.98f, 1f)));

        root.AddChild(Emitter("FallSheet", lipCenter.Lerp(baseCenter, 0.5f),
            box: new Vector3(hw, fallHeight * 0.5f, 0.16f),
            dir: Vector3.Down, spread: 9f, vMin: 3f, vMax: 6f, gravity: -12f, spin: 160f,
            amount: 85, life: Mathf.Clamp(fallHeight / 6f, 0.7f, 1.5f),
            sMin: 0.45f, sMax: 1.1f, peak: 0.85f, tint: new Color(0.86f, 0.93f, 1f)));

        root.AddChild(Emitter("BaseSplash", baseCenter,
            box: new Vector3(hw + 0.6f, 0.12f, 0.7f),
            dir: Vector3.Up, spread: 58f, vMin: 2.6f, vMax: 5.4f, gravity: -8f, spin: 260f,
            amount: 110, life: 0.95f, sMin: 0.6f, sMax: 1.7f, peak: 0.95f,
            tint: new Color(0.97f, 0.99f, 1f)));

        root.AddChild(Emitter("FoamPool", baseCenter + spillDir * 0.8f,
            box: new Vector3(hw + 1.0f, 0.12f, 1.6f),
            dir: Vector3.Up, spread: 80f, vMin: 0.3f, vMax: 1.2f, gravity: -1.5f, spin: 90f,
            amount: 130, life: 1.5f, sMin: 0.5f, sMax: 1.2f, peak: 0.9f,
            tint: new Color(0.95f, 0.98f, 1f)));

        root.AddChild(Emitter("Bubbles", baseCenter + spillDir * 1.6f + Vector3.Down * 0.2f,
            box: new Vector3(hw + 1.0f, 0.1f, 0.6f),
            dir: spillDir, spread: 34f, vMin: 0.5f, vMax: 1.4f, gravity: 0f, spin: 50f,
            amount: 72, life: 2.8f, sMin: 0.35f, sMax: 0.7f, peak: 0.5f,
            tint: new Color(0.88f, 0.95f, 1f)));
        return root;
    }

    private static GpuParticles3D Emitter(string name, Vector3 pos, Vector3 box, Vector3 dir,
        float spread, float vMin, float vMax, float gravity, float spin, int amount, float life,
        float sMin, float sMax, float peak, Color tint)
    {
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(tint, 0f));
        ramp.AddPoint(0.18f, new Color(tint, peak));
        ramp.SetColor(1, new Color(tint, 0f));
        return new GpuParticles3D
        {
            Name = name,
            Position = pos,
            Amount = amount,
            Lifetime = life,
            DrawPass1 = Cube(),
            ProcessMaterial = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
                EmissionBoxExtents = box,
                Direction = dir,
                Spread = spread,
                InitialVelocityMin = vMin,
                InitialVelocityMax = vMax,
                Gravity = new Vector3(0, gravity, 0),
                ScaleMin = sMin,
                ScaleMax = sMax,
                AngularVelocityMin = -spin,
                AngularVelocityMax = spin,
                TurbulenceEnabled = true,
                TurbulenceNoiseStrength = 1.1f,
                TurbulenceNoiseScale = 1.4f,
                ColorRamp = new GradientTexture1D { Gradient = ramp },
            },
        };
    }

    private static Mesh Cube()
    {
        var mesh = new BoxMesh { Size = new Vector3(0.22f, 0.22f, 0.22f) };
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.75f,
            RenderPriority = 2,
        });
        return mesh;
    }
}
