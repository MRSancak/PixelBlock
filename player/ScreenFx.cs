using Godot;
using PixelBlock.Core;

namespace PixelBlock.PlayerSys;

public partial class ScreenFx : Node
{
    public Camera3D Camera;

    private const float MaxOffset = 0.16f;
    private const float MaxRoll = 0.045f;
    private const float Decay = 1.8f;

    private float _trauma;
    private float _t;
    private readonly ValueNoise2D _noise = new(1337);

    public void AddTrauma(float amount) => _trauma = Mathf.Clamp(_trauma + amount, 0f, 1f);

    public override void _Process(double delta)
    {
        if (Camera == null) return;
        if (_trauma <= 0f)
        {

            if (Camera.Position != Vector3.Zero) Camera.Position = Vector3.Zero;
            if (Camera.Rotation != Vector3.Zero) Camera.Rotation = Vector3.Zero;
            return;
        }

        float dt = (float)delta;
        _t += dt;
        float shake = _trauma * _trauma;

        float nx = _noise.Noise(_t * 30f, 0f) * 2f - 1f;
        float ny = _noise.Noise(0f, _t * 30f + 17f) * 2f - 1f;
        float nr = _noise.Noise(_t * 27f + 50f, _t * 27f) * 2f - 1f;

        Camera.Position = new Vector3(nx * MaxOffset * shake, ny * MaxOffset * shake, 0f);
        Camera.Rotation = new Vector3(0f, 0f, nr * MaxRoll * shake);

        _trauma = Mathf.MoveToward(_trauma, 0f, Decay * dt);
    }
}
