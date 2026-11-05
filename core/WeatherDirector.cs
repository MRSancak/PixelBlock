using Godot;

namespace PixelBlock.Core;

public sealed partial class WeatherDirector : Node
{
    public TerrainGenerator Generator;
    public Node3D Player;
    public EnvironmentController Env;

    private float _clock;
    private readonly ValueNoise2D _schedule = new(4242);
    private readonly ValueNoise2D _fogSchedule = new(5151);

    public override void _Process(double delta)
    {
        if (Generator == null || Player == null || Env == null) return;
        _clock += (float)delta;
        if (_clock < 1.5f) return;
        _clock = 0f;

        Vector3 p = Player.GlobalPosition;
        int x = Mathf.FloorToInt(p.X), z = Mathf.FloorToInt(p.Z);
        Biome biome = Generator.BiomeAt(x, z, Generator.SurfaceHeight(x, z));
        int season = SeasonClock.Instance?.Season ?? 1;
        bool winter = season == 3;

        float t = (float)(Time.GetTicksMsec() / 1000.0);
        float wet = _schedule.Noise(t * 0.012f, 0f);
        float fogN = _fogSchedule.Noise(t * 0.015f, 7f);

        Weather w;
        switch (biome)
        {
            case Biome.Snow:
                w = wet > 0.35f ? Weather.Snow : Weather.Cloudy;
                break;
            case Biome.Mountains:
                w = wet > 0.5f ? Weather.Snow : (wet > 0.38f ? Weather.Cloudy : Weather.Clear);
                break;
            case Biome.Desert:
                w = wet > 0.8f ? Weather.Cloudy : Weather.Clear;
                break;
            case Biome.Swamp:
                w = fogN > 0.55f ? Weather.Fog : (wet > 0.6f ? Weather.Rain : Weather.Cloudy);
                break;
            default:
                if (wet > 0.86f) w = winter ? Weather.Snow : Weather.Storm;
                else if (wet > 0.64f) w = winter ? Weather.Snow : Weather.Rain;
                else if (wet > 0.52f) w = Weather.Cloudy;
                else if (fogN > 0.82f && (season == 0 || season == 2)) w = Weather.Fog;
                else w = Weather.Clear;
                break;
        }
        Env.SetWeather(w);
    }

    public static string NameOf(Weather w) => w switch
    {
        Weather.Rain => "Yağmur",
        Weather.Snow => "Kar",
        Weather.Cloudy => "Bulutlu",
        Weather.Storm => "Fırtına",
        Weather.Fog => "Sis",
        _ => "Güneşli",
    };
}
