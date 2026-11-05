using Godot;

namespace PixelBlock.Core;

public sealed partial class SeasonClock : Node
{
    public static SeasonClock Instance { get; private set; }
    public static readonly string[] SeasonNames = { "İlkbahar", "Yaz", "Sonbahar", "Kış" };

    public EnvironmentController Env;
    public VoxelWorld World;
    public int DayCount;
    public int DaysPerSeason = 4;

    private float _prevTime = -1f;
    private int _applied = -1;

    public int Season => (((DayCount / Mathf.Max(1, DaysPerSeason)) % 4) + 4) % 4;
    public int SeasonBit => 1 << Season;
    public string SeasonName => SeasonNames[Season];
    public string Label => $"{SeasonName} · {DayCount + 1}. gün";

    public override void _Ready() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public override void _Process(double delta)
    {
        if (Env == null) return;
        float t = Env.TimeOfDay;
        if (_prevTime >= 0f && t < _prevTime - 0.5f) DayCount++;
        _prevTime = t;
        if (World?.Textures != null && _applied != Season)
        {
            _applied = Season;
            World.Textures.ApplySeason(Season);
        }
    }
}
