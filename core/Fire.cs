using Godot;

namespace PixelBlock.Core;

public enum FireKind { Candle, Torch, Campfire, Brazier, Altar, Forge }

public enum FirePalette { Normal, Holy, Forge }

public sealed partial class Fire : Node3D
{
    public FireKind Kind { get; private set; }
    public FirePalette Palette { get; private set; }

    public Vector3I? Cell { get; private set; }

    public float Seed { get; private set; }
    public float FlickerStr { get; private set; }
    public float SwellStr { get; private set; }
    public float SurgeStr { get; private set; }

    public float Dist;

    private static Shader _flameShaderRes, _smokeShaderRes, _glowShaderRes;
    private static Shader FlameShader => _flameShaderRes ??= GD.Load<Shader>("res://assets/shaders/flame.gdshader");
    private static Shader SmokeShader => _smokeShaderRes ??= GD.Load<Shader>("res://assets/shaders/smoke.gdshader");
    private static Shader GlowShader => _glowShaderRes ??= GD.Load<Shader>("res://assets/shaders/firebase.gdshader");

    private ShaderMaterial _mat;
    private ShaderMaterial _glowMat;
    private MeshInstance3D _glow;
    private OmniLight3D _light;
    private Vector3 _lightHome;
    private Color _lightLow, _lightHigh;
    private float _baseEnergy;
    private GpuParticles3D _embers, _smoke;
    private ParticleProcessMaterial _emberMat, _smokeMat;

    public void Configure(FireKind kind, FirePalette palette, float seed, Vector3I? cell)
    {
        Kind = kind; Seed = seed; Cell = cell;
        var spec = FireSpec.For(kind);
        FlickerStr = spec.FlickerStr; SwellStr = spec.SwellStr; SurgeStr = spec.SurgeStr;
        _baseEnergy = spec.LightEnergy;

        _mat = new ShaderMaterial { Shader = FlameShader };
        _mat.SetShaderParameter("emission_energy", spec.EmissionEnergy);
        _mat.SetShaderParameter("bands", spec.Bands);

        for (int i = 0; i < spec.Layers; i++)
        {
            float wf = 1f - i * 0.28f;
            float hf = 1f + i * 0.12f;
            var mi = new MeshInstance3D
            {
                Name = $"Flame{i}",
                Mesh = new QuadMesh { Size = Vector2.One },
                MaterialOverride = _mat,
                Scale = new Vector3(spec.Width * wf, spec.Height * hf, 1f),
                Position = new Vector3((i - (spec.Layers - 1) * 0.5f) * spec.Width * 0.14f,
                                       spec.Height * hf * 0.5f, 0f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                ExtraCullMargin = 4f,
            };
            AddChild(mi);
        }

        _light = new OmniLight3D
        {
            Name = "Light",
            LightEnergy = _baseEnergy,
            OmniRange = spec.LightRange,
            ShadowEnabled = false,
            Position = new Vector3(0f, spec.Height * 0.5f, 0f),
        };
        _lightHome = _light.Position;
        AddChild(_light);

        if (spec.GlowSize > 0f)
        {
            _glowMat = new ShaderMaterial { Shader = GlowShader };
            _glowMat.SetShaderParameter("emission_energy", spec.GlowEnergy);
            _glow = new MeshInstance3D
            {
                Name = "BaseGlow",
                Mesh = new PlaneMesh { Size = new Vector2(spec.GlowSize, spec.GlowSize) },
                MaterialOverride = _glowMat,
                Position = new Vector3(0f, 0.06f, 0f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                ExtraCullMargin = 2f,
            };
            AddChild(_glow);
        }

        if (spec.EmberAmount > 0) { _embers = BuildEmbers(spec); AddChild(_embers); }
        if (spec.SmokeAmount > 0) { _smoke = BuildSmoke(spec); AddChild(_smoke); }

        ApplyPalette(palette);
    }

    public void ApplyPalette(FirePalette palette)
    {
        if (_mat == null) return;
        Palette = palette;
        var pal = FirePal.For(palette);
        _mat.SetShaderParameter("col_dark", pal.Dark);
        _mat.SetShaderParameter("col_red", pal.Red);
        _mat.SetShaderParameter("col_orange", pal.Orange);
        _mat.SetShaderParameter("col_gold", pal.Gold);
        _mat.SetShaderParameter("col_white", pal.White);
        _lightLow = pal.LightLow; _lightHigh = pal.LightHigh;
        if (_light != null) _light.LightColor = pal.LightLow;
        if (_emberMat != null) _emberMat.Color = pal.Ember;
        if (_glowMat != null)
        {
            _glowMat.SetShaderParameter("col_pool", pal.Orange);
            _glowMat.SetShaderParameter("col_coals", pal.Gold);
        }
    }

    public void Tick(float t, float flicker, float slow, float nightBoost, bool lightOn, bool emit, Vector2 wind)
    {
        if (_mat == null) return;
        _mat.SetShaderParameter("flicker", flicker);
        if (_glowMat != null) _glowMat.SetShaderParameter("flicker", flicker);
        if (_light != null)
        {
            _light.Visible = lightOn;
            if (lightOn)
            {
                _light.LightEnergy = _baseEnergy * flicker * nightBoost;
                _light.LightColor = _lightLow.Lerp(_lightHigh, 0.5f + 0.5f * slow);
                _light.Position = _lightHome + new Vector3(
                    Mathf.Sin(t * 3.1f + Seed * 50f) * 0.05f,
                    Mathf.Sin(t * 4.3f + Seed * 25f) * 0.07f,
                    Mathf.Cos(t * 2.7f + Seed * 15f) * 0.05f);
            }
        }
        if (_embers != null) _embers.Emitting = emit;
        if (_smoke != null)
        {
            _smoke.Emitting = emit;
            if (emit && _smokeMat != null)
                _smokeMat.Gravity = new Vector3(wind.X * 0.35f, 0.6f, wind.Y * 0.35f);
        }
    }

    public void Sleep()
    {
        Visible = false;
        if (_light != null) _light.Visible = false;
        if (_embers != null) _embers.Emitting = false;
        if (_smoke != null) _smoke.Emitting = false;
    }

    private GpuParticles3D BuildEmbers(FireSpec spec)
    {
        var ramp = new Gradient
        {
            Offsets = new float[] { 0f, 0.25f, 1f },
            Colors = new Color[] { new(1, 1, 1, 0), new(1, 1, 1, 1), new(1, 1, 1, 0) },
        };
        _emberMat = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = spec.Width * 0.45f,
            Direction = Vector3.Up,
            Spread = 18f,
            Gravity = new Vector3(0f, 0.8f, 0f),
            InitialVelocityMin = 0.5f,
            InitialVelocityMax = 1.5f,
            ScaleMin = 0.5f,
            ScaleMax = 1.1f,
            ColorRamp = new GradientTexture1D { Gradient = ramp },
            TurbulenceEnabled = true,
            TurbulenceNoiseStrength = 1.3f,
            TurbulenceNoiseScale = 1.5f,
        };
        var mesh = new QuadMesh { Size = new Vector2(0.05f, 0.05f) };
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            VertexColorUseAsAlbedo = true,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        });
        return new GpuParticles3D
        {
            Name = "Embers",
            Amount = spec.EmberAmount,
            Lifetime = 1.7f,
            Emitting = true,
            Preprocess = 0.5f,
            LocalCoords = true,
            ProcessMaterial = _emberMat,
            DrawPass1 = mesh,
            Position = new Vector3(0f, spec.Height * 0.25f, 0f),
            VisibilityAabb = new Aabb(new Vector3(-3, -1, -3), new Vector3(6, spec.Height + 5f, 6)),
        };
    }

    private GpuParticles3D BuildSmoke(FireSpec spec)
    {

        var ramp = new Gradient
        {
            Offsets = new float[] { 0f, 0.25f, 1f },
            Colors = new Color[] { new(1, 1, 1, 0), new(1, 1, 1, 1), new(1, 1, 1, 0) },
        };
        var grow = new Curve();
        grow.AddPoint(new Vector2(0f, 0.7f));
        grow.AddPoint(new Vector2(1f, 1.4f));
        _smokeMat = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = spec.Width * 0.25f,
            Direction = Vector3.Up,
            Spread = 8f,
            Gravity = new Vector3(0f, 0.55f, 0f),
            InitialVelocityMin = 0.35f,
            InitialVelocityMax = 0.7f,
            ScaleMin = spec.Width * 0.5f,
            ScaleMax = spec.Width * 0.8f,
            ScaleCurve = new CurveTexture { Curve = grow },
            ColorRamp = new GradientTexture1D { Gradient = ramp },
            TurbulenceEnabled = true,
            TurbulenceNoiseStrength = 0.7f,
            TurbulenceNoiseScale = 1f,
        };
        var mesh = new QuadMesh { Size = Vector2.One };
        var drawMat = new ShaderMaterial { Shader = SmokeShader };
        drawMat.SetShaderParameter("opacity", 0.5f);
        mesh.SurfaceSetMaterial(0, drawMat);
        return new GpuParticles3D
        {
            Name = "Smoke",
            Amount = spec.SmokeAmount,
            Lifetime = 2.4f,
            Emitting = true,
            Preprocess = 1f,
            LocalCoords = true,
            ProcessMaterial = _smokeMat,
            DrawPass1 = mesh,
            Position = new Vector3(0f, spec.Height * 0.65f, 0f),
            VisibilityAabb = new Aabb(new Vector3(-3, -1, -3), new Vector3(6, spec.Height + 7f, 6)),
        };
    }
}

internal struct FireSpec
{
    public float Width, Height, EmissionEnergy, Bands;
    public int Layers;
    public float LightEnergy, LightRange;
    public int EmberAmount, SmokeAmount;
    public float GlowSize, GlowEnergy;
    public float FlickerStr, SwellStr, SurgeStr;

    public static FireSpec For(FireKind k) => k switch
    {
        FireKind.Candle => new FireSpec
        {
            Width = 0.12f, Height = 0.22f, Layers = 1, EmissionEnergy = 2.6f, Bands = 5f,
            LightEnergy = 0.7f, LightRange = 4.5f, EmberAmount = 0, SmokeAmount = 0,
            GlowSize = 0f, GlowEnergy = 0f,
            FlickerStr = 0.10f, SwellStr = 0.10f, SurgeStr = 0.05f,
        },
        FireKind.Torch => new FireSpec
        {
            Width = 0.28f, Height = 0.5f, Layers = 2, EmissionEnergy = 3.0f, Bands = 5f,
            LightEnergy = 2.0f, LightRange = 9f, EmberAmount = 10, SmokeAmount = 0,
            GlowSize = 0.55f, GlowEnergy = 1.6f,
            FlickerStr = 0.22f, SwellStr = 0.16f, SurgeStr = 0.12f,
        },
        FireKind.Campfire => new FireSpec
        {
            Width = 0.7f, Height = 0.85f, Layers = 3, EmissionEnergy = 3.2f, Bands = 5f,
            LightEnergy = 3.0f, LightRange = 12f, EmberAmount = 28, SmokeAmount = 12,
            GlowSize = 1.7f, GlowEnergy = 2.2f,
            FlickerStr = 0.24f, SwellStr = 0.18f, SurgeStr = 0.16f,
        },
        FireKind.Brazier => new FireSpec
        {
            Width = 0.6f, Height = 1.0f, Layers = 3, EmissionEnergy = 3.4f, Bands = 5f,
            LightEnergy = 3.6f, LightRange = 14f, EmberAmount = 32, SmokeAmount = 10,
            GlowSize = 1.4f, GlowEnergy = 2.4f,
            FlickerStr = 0.22f, SwellStr = 0.18f, SurgeStr = 0.16f,
        },
        FireKind.Altar => new FireSpec
        {
            Width = 0.7f, Height = 1.25f, Layers = 3, EmissionEnergy = 3.6f, Bands = 6f,
            LightEnergy = 4.2f, LightRange = 16f, EmberAmount = 38, SmokeAmount = 14,
            GlowSize = 1.8f, GlowEnergy = 2.6f,
            FlickerStr = 0.20f, SwellStr = 0.18f, SurgeStr = 0.20f,
        },
        _ /* Forge */ => new FireSpec
        {
            Width = 0.55f, Height = 0.5f, Layers = 2, EmissionEnergy = 3.0f, Bands = 5f,
            LightEnergy = 2.6f, LightRange = 10f, EmberAmount = 22, SmokeAmount = 8,
            GlowSize = 1.3f, GlowEnergy = 2.2f,
            FlickerStr = 0.16f, SwellStr = 0.14f, SurgeStr = 0.10f,
        },
    };
}

internal struct FirePal
{
    public Color Dark, Red, Orange, Gold, White, Ember, LightLow, LightHigh;

    public static FirePal For(FirePalette p) => p switch
    {
        FirePalette.Holy => new FirePal
        {
            Dark = new(0.10f, 0.14f, 0.30f), Red = new(0.25f, 0.42f, 0.85f),
            Orange = new(0.55f, 0.78f, 1.0f), Gold = new(0.85f, 0.93f, 1.0f), White = new(1f, 1f, 1f),
            Ember = new(0.75f, 0.88f, 1.0f), LightLow = new(0.55f, 0.72f, 1.0f), LightHigh = new(0.85f, 0.92f, 1.0f),
        },
        FirePalette.Forge => new FirePal
        {
            Dark = new(0.18f, 0f, 0f), Red = new(0.70f, 0.06f, 0f),
            Orange = new(1.0f, 0.28f, 0.02f), Gold = new(1.0f, 0.52f, 0.10f), White = new(1.0f, 0.82f, 0.45f),
            Ember = new(1.0f, 0.45f, 0.12f), LightLow = new(0.9f, 0.30f, 0.08f), LightHigh = new(1.0f, 0.5f, 0.18f),
        },
        _ => new FirePal
        {
            Dark = new(0.25f, 0.01f, 0f), Red = new(0.85f, 0.10f, 0.01f),
            Orange = new(1.0f, 0.38f, 0.02f), Gold = new(1.0f, 0.75f, 0.18f), White = new(1.0f, 0.97f, 0.82f),
            Ember = new(1.0f, 0.6f, 0.2f), LightLow = new(1.0f, 0.55f, 0.18f), LightHigh = new(1.0f, 0.78f, 0.4f),
        },
    };
}
