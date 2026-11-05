using Godot;

namespace PixelBlock.Core;

public static class Settings
{

    public enum MouseCapture { Off, ClickToCapture, Always }

    public static float MouseSensitivity = 1.0f;
    public static float MasterVolume = 0.8f;
    public static float MusicVolume = 0.5f;
    public static MouseCapture CaptureMode = MouseCapture.Always;
    public static float KeyboardLookSpeed = 110f;
    public static bool Loaded;

    public enum GfxQuality { Dusuk, Orta, Yuksek, Ozel }

    public static GfxQuality Quality = GfxQuality.Orta;
    public static bool Shadows = true;
    public static int ShadowQuality = 1;
    public static int ViewDistance = 6;
    public static int FpsLimit = 0;
    public static bool VSync = true;
    public static bool Fullscreen = false;
    public static int ResolutionIndex = 0;
    public static bool Ssao = true;
    public static bool Bloom = true;
    public static bool VolFog = true;
    public static int Aa = 1;
    public static int SimDistance = 6;

    public static int GfxVersion;

    public static readonly Vector2I[] Resolutions =
    {
        new(1280, 720), new(1366, 768), new(1600, 900), new(1920, 1080), new(2560, 1440),
    };

    public static void ApplyPreset(GfxQuality q)
    {
        Quality = q;
        switch (q)
        {
            case GfxQuality.Dusuk: Shadows = false; ShadowQuality = 0; ViewDistance = 4; Ssao = false; Bloom = false; VolFog = false; Aa = 0; break;
            case GfxQuality.Orta: Shadows = true; ShadowQuality = 1; ViewDistance = 6; Ssao = true; Bloom = true; VolFog = false; Aa = 1; break;
            case GfxQuality.Yuksek: Shadows = true; ShadowQuality = 2; ViewDistance = 10; Ssao = true; Bloom = true; VolFog = true; Aa = 2; break;
        }
    }

    private const string Path = "user://settings.cfg";

    public static void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(Path) == Error.Ok)
        {
            MouseSensitivity = (float)(double)cfg.GetValue("input", "mouse_sensitivity", 1.0);
            MasterVolume = (float)(double)cfg.GetValue("audio", "master_volume", 0.8);
            MusicVolume = (float)(double)cfg.GetValue("audio", "music_volume", 0.5);
            CaptureMode = (MouseCapture)cfg.GetValue("input", "mouse_capture_mode", (int)MouseCapture.Always).AsInt32();
            KeyboardLookSpeed = (float)(double)cfg.GetValue("input", "keyboard_look_speed", 110.0);
            Quality = (GfxQuality)cfg.GetValue("gfx", "quality", (int)GfxQuality.Orta).AsInt32();
            Shadows = cfg.GetValue("gfx", "shadows", true).AsBool();
            ShadowQuality = Mathf.Clamp(cfg.GetValue("gfx", "shadow_quality", 1).AsInt32(), 0, 2);
            ViewDistance = Mathf.Clamp(cfg.GetValue("gfx", "view_distance", 6).AsInt32(), 3, 12);
            FpsLimit = cfg.GetValue("gfx", "fps_limit", 0).AsInt32();
            VSync = cfg.GetValue("gfx", "vsync", true).AsBool();
            Fullscreen = cfg.GetValue("gfx", "fullscreen", false).AsBool();
            ResolutionIndex = Mathf.Clamp(cfg.GetValue("gfx", "resolution", 0).AsInt32(), 0, Resolutions.Length - 1);
            Ssao = cfg.GetValue("gfx", "ssao", true).AsBool();
            Bloom = cfg.GetValue("gfx", "bloom", true).AsBool();
            VolFog = cfg.GetValue("gfx", "volfog", true).AsBool();
            Aa = Mathf.Clamp(cfg.GetValue("gfx", "aa", 1).AsInt32(), 0, 3);
            SimDistance = Mathf.Clamp(cfg.GetValue("game", "sim_distance", 6).AsInt32(), 2, 12);
        }
        Loaded = true;
        Apply();
    }

    public static void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("input", "mouse_sensitivity", MouseSensitivity);
        cfg.SetValue("audio", "master_volume", MasterVolume);
        cfg.SetValue("audio", "music_volume", MusicVolume);
        cfg.SetValue("input", "mouse_capture_mode", (int)CaptureMode);
        cfg.SetValue("input", "keyboard_look_speed", KeyboardLookSpeed);
        cfg.SetValue("gfx", "quality", (int)Quality);
        cfg.SetValue("gfx", "shadows", Shadows);
        cfg.SetValue("gfx", "shadow_quality", ShadowQuality);
        cfg.SetValue("gfx", "view_distance", ViewDistance);
        cfg.SetValue("gfx", "fps_limit", FpsLimit);
        cfg.SetValue("gfx", "vsync", VSync);
        cfg.SetValue("gfx", "fullscreen", Fullscreen);
        cfg.SetValue("gfx", "resolution", ResolutionIndex);
        cfg.SetValue("gfx", "ssao", Ssao);
        cfg.SetValue("gfx", "bloom", Bloom);
        cfg.SetValue("gfx", "volfog", VolFog);
        cfg.SetValue("gfx", "aa", Aa);
        cfg.SetValue("game", "sim_distance", SimDistance);
        cfg.Save(Path);
        Apply();
    }

    public static void Apply()
    {
        SetBus("Master", MasterVolume);
        SetBus("Music", MusicVolume);
        ApplyGraphics();
    }

    private static bool _windowApplied, _appliedFull;
    private static int _appliedRes = -1;

    public static void ApplyGraphics()
    {
        Engine.MaxFps = FpsLimit;
        DisplayServer.WindowSetVsyncMode(VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
        int[] atlas = { 1024, 2048, 4096 };
        RenderingServer.DirectionalShadowAtlasSetSize(atlas[Mathf.Clamp(ShadowQuality, 0, 2)], true);

        if (!_windowApplied || _appliedFull != Fullscreen || _appliedRes != ResolutionIndex)
        {
            _windowApplied = true;
            _appliedFull = Fullscreen;
            _appliedRes = ResolutionIndex;
            if (Fullscreen)
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
            else
            {
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetSize(Resolutions[Mathf.Clamp(ResolutionIndex, 0, Resolutions.Length - 1)]);
            }
        }
        if (Engine.GetMainLoop() is SceneTree tree && tree.Root != null)
        {

            tree.Root.Set("screen_space_aa", Aa == 1 ? 1 : 0);
            tree.Root.Set("msaa_3d", Aa == 2 ? 1 : (Aa == 3 ? 2 : 0));
        }
        GfxVersion++;
    }

    private static void SetBus(string name, float linear)
    {
        int bus = AudioServer.GetBusIndex(name);
        if (bus >= 0)
            AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(Mathf.Clamp(linear, 0.0001f, 1f)));
    }
}
