using Godot;

namespace PixelBlock;

public partial class Game : Node3D
{
    private GameSession _session;
    private UI.MainMenu _menu;
    private UI.SaveMenu _saveMenu;
    private string _saveName;

    private static readonly (string block, int count)[] DefaultKit =
    {
        ("grass", 64), ("dirt", 64), ("stone", 64), ("cobblestone", 64),
        ("planks", 64), ("oak_log", 32), ("mud_brick", 64), ("leaves", 32), ("lamp", 8),
        ("ahsap_kazma", 1), ("ahsap_balta", 1), ("ahsap_kurek", 1), ("ahsap_capa", 1), ("ekmek", 5), ("crafting_table", 1),
    };

    public override void _Ready()
    {
        var version = (string)Engine.GetVersionInfo()["string"];
        GD.Print($"[RA] Game ready on Godot {version}");
        MatchPhysicsToRefresh();
        Core.Settings.Load();
        Core.CharacterProfile.Load();
        AddChild(new Core.AudioManager { Name = "Audio" });
        AddChild(new Core.Fx { Name = "Fx" });
        _net = new Network.NetworkManager { Name = "Net" };
        AddChild(_net);

        string mode = null;
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--")) { mode = arg; break; }

        switch (mode)
        {
            case "--smoke":
                GD.Print("[RA] smoke: C# assembly loaded OK");
                QuitSoon();
                break;
            case "--gen-textures":
                Tools.TextureForge.GenerateAll();
                QuitSoon();
                break;
            case "--gen-audio":
                GenAudio();
                QuitSoon();
                break;
            case "--test-blocks":
                Core.BlockRegistry.EnsureInit();
                var tex = Core.BlockTextures.Build();
                GD.Print($"[RA] test-blocks: blocks={Core.BlockRegistry.Count} layers={tex.LayerCount} " +
                         $"grass.top.layer={Core.BlockRegistry.Get("grass").FaceLayer[(int)Core.Face.PosY]}");
                QuitSoon();
                break;
            case "--test-world":
                RunWorldTest();
                break;
            case "--test-player":
                RunPlayerTest();
                break;
            case "--test-swim-exit":
                RunSwimExitTest();
                break;
            case "--test-controls":
                RunControlsTest();
                break;
            case "--test-stream":
                RunStreamTest();
                break;
            case "--test-greedy":
                RunGreedyTest();
                break;
            case "--test-biomes":
                RunBiomeTest();
                break;
            case "--test-underground":
                RunUndergroundTest();
                break;
            case "--test-daynight":
                RunDayNightTest();
                break;
            case "--test-sky":
                RunSkyTest();
                break;
            case "--test-water":
                RunWaterTest();
                break;
            case "--test-waterstep":
                RunWaterStepTest();
                break;
            case "--test-waterlane":
                RunWaterLaneTest();
                break;
            case "--test-waterdepth":
                RunWaterDepthTest();
                break;
            case "--test-mining":
                RunMiningTest();
                break;
            case "--test-strata":
                RunStrataTest();
                break;
            case "--test-waterfill":
                RunWaterFillTest();
                break;
            case "--test-swim":
                RunSwimTest();
                break;
            case "--test-underwater":
                RunUnderwaterTest();
                break;
            case "--test-fx":
                RunFxTest();
                break;
            case "--test-ambientlife":
                RunAmbientLifeTest();
                break;
            case "--test-ambient":
                RunAmbientTest();
                break;
            case "--test-craft":
                RunCraftTest();
                break;
            case "--test-persist":
                RunPersistTest();
                break;
            case "--test-teacher":
                RunTeacherTest();
                break;
            case "--test-hud":
                RunHudTest();
                break;
            case "--test-combat":
                RunCombatTest();
                break;
            case "--test-mob":
                RunMobTest();
                break;
            case "--test-spawn":
                RunSpawnTest();
                break;
            case "--test-climb":
                RunClimbTest();
                break;
            case "--test-rigged":
                RunRiggedTest();
                break;
            case "--test-npc":
                RunNpcTest();
                break;
            case "--test-save":
                RunSaveTest();
                break;
            case "--lesson-david":
                StartLesson(Lessons.LessonCatalog.Get("david"));
                break;
            case "--lesson-creation":
                StartLesson(Lessons.LessonCatalog.Get("creation"));
                break;
            case "--sandbox":
                StartSandbox();
                break;
            case "--showcase":
                StartShowcase();
                break;
            case "--test-creation":
                RunCreationTest();
                break;
            case "--test-lesson":
                RunLessonTest();
                break;
            case "--test-menu":
                RunMenuTest();
                break;
            case "--test-quest":
                RunQuestTest();
                break;
            case "--test-campaign":
                RunCampaignTest();
                break;
            case "--test-jsonlesson":
                RunJsonLessonTest();
                break;
            case "--lesson-jericho":
                StartLesson(Lessons.LessonCatalog.Get("jericho"));
                break;
            case "--menu":
            default:
                ShowMainMenu();
                break;
        }
    }

    private void QuitSoon() => GetTree().CreateTimer(0.1).Timeout += () => GetTree().Quit(0);

    private void MatchPhysicsToRefresh()
    {
        if (DisplayServer.GetName() == "headless") return;
        float hz = (float)DisplayServer.ScreenGetRefreshRate(DisplayServer.WindowGetCurrentScreen());
        if (hz <= 0f) { GD.Print("[RA] refresh rate unknown; keeping default physics tick rate"); return; }
        int ticks = Mathf.Clamp(Mathf.RoundToInt(hz), 60, 360);
        Engine.PhysicsTicksPerSecond = ticks;
        GD.Print($"[RA] physics tick rate = {ticks} Hz (panel reports {hz:F1} Hz)");
    }

    private void GenAudio()
    {
        const string dir = "res://assets/audio";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(dir));
        int n = 0;
        foreach (var kv in Core.SoundBank.BuildAll())
        {
            byte[] pcm = Core.SoundBank.ToPcm16(kv.Value.Samples);
            using var f = Godot.FileAccess.Open($"{dir}/{kv.Key}.wav", Godot.FileAccess.ModeFlags.Write);
            if (f == null) continue;
            f.StoreBuffer(WavFile(pcm, Core.SoundBank.Rate));
            n++;
        }
        GD.Print($"[RA] gen-audio: wrote {n} wav files to {dir}");
    }

    private static byte[] WavFile(byte[] pcm, int rate)
    {
        using var ms = new System.IO.MemoryStream();
        using var w = new System.IO.BinaryWriter(ms);
        const int channels = 1, bits = 16;
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + pcm.Length);
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write((short)1);
        w.Write((short)channels);
        w.Write(rate);
        w.Write(rate * channels * bits / 8);
        w.Write((short)(channels * bits / 8));
        w.Write((short)bits);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        w.Write(pcm.Length);
        w.Write(pcm);
        return ms.ToArray();
    }

    private void ShowMainMenu()
    {
        GetTree().Paused = false;
        _net?.Disconnect();
        _hostMode = false;
        if (_session != null) { _session.QueueFree(); _session = null; }
        _saveName = null;
        ClearMenu();
        _menu = new UI.MainMenu { Name = "MainMenu", Progress = Core.CampaignStore.Load() };
        _menu.OnPlayLesson = id => StartLesson(Lessons.LessonCatalog.Get(id));
        _menu.OnSandbox = ShowSaveMenu;
        _menu.OnShowcase = LaunchShowcase;
        _menu.OnSingleplayer = ShowSaveMenu;
        _menu.OnMultiplayer = ShowMultiplayerMenu;
        _menu.OnCharacter = ShowCharacterMenu;
        _menu.OnQuit = () => GetTree().Quit();
        AddChild(_menu);
        Core.AudioManager.StartMusic();
        Core.AudioManager.SetMusicMood(Core.MusicMood.Calm);
        Core.AudioManager.StopAmbience();
    }

    private UI.PlaceholderScreen _info;

    private void ShowInfoScreen(string title, string body, System.Action back = null)
    {
        ClearMenu();
        _info = new UI.PlaceholderScreen { Name = "Info", TitleText = title, BodyText = body, OnBack = back ?? new System.Action(ShowMainMenu) };
        AddChild(_info);
    }

    private void ShowNewWorldMenu()
    {
        ClearMenu();
        var nw = new UI.NewWorldMenu { Name = "NewWorld", DefaultName = UniqueWorldName() };
        nw.OnBack = ShowSaveMenu;
        nw.OnCreate = (name, seed) => StartSandbox(NewWorldSave(name, seed));
        _info = null;
        _newWorld = nw;
        AddChild(nw);
    }

    private UI.NewWorldMenu _newWorld;
    private UI.CharacterMenu _charMenu;
    private UI.MultiplayerMenu _mpMenu;
    private Network.NetworkManager _net;
    private bool _hostMode;

    private void ShowMultiplayerMenu()
    {
        ClearMenu();
        _hostMode = false;
        _mpMenu = new UI.MultiplayerMenu { Name = "MultiplayerMenu", Lan = _net.Lan, OnBack = ShowMainMenu };
        _mpMenu.OnHost = () => { _hostMode = true; ShowSaveMenu(); };
        _mpMenu.OnJoin = JoinGame;
        AddChild(_mpMenu);
    }

    private void JoinGame(string ip)
    {
        UI.MultiplayerMenu.LastIp = ip;
        var err = _net.Join(ip);
        if (err != Error.Ok)
        {
            ShowInfoScreen("Bağlantı Hatası", "Bu adrese bağlanılamadı. IP adresini kontrol et.", ShowMultiplayerMenu);
            return;
        }
        _net.OnConnectFailed = () => ShowInfoScreen("Bağlantı Başarısız",
            "Sunucuya ulaşılamadı. Host'un oyunu kurduğundan, aynı ağda olduğunuzdan ve 24565 (UDP) portunun güvenlik duvarında açık olduğundan emin ol.", ShowMultiplayerMenu);
        _net.OnWorldInfo = (seed, time, day, hostName, inv) => StartClientSession(seed, time, day, hostName, inv);
        _net.OnServerLost = () =>
        {
            ShowMainMenu();
            ShowInfoScreen("Bağlantı Koptu", "Sunucuyla bağlantı kesildi. Çok Oyunculu menüsünden aynı adrese tekrar katılabilirsin; dünya host'ta korunuyor.", ShowMultiplayerMenu);
        };
        ShowInfoScreen("Bağlanılıyor…", $"{ip} adresine bağlanılıyor…", () => { _net.Disconnect(); ShowMultiplayerMenu(); });
    }

    private string _clientInv = "";
    private System.Collections.Generic.Dictionary<string, string> _peerInvLoaded = new();

    private void StartClientSession(int seed, float time, int day, string hostName, string savedInv)
    {
        _clientInv = savedInv ?? "";
        var cs = NewWorldSave(hostName + " (Çok Oyunculu)", seed);
        cs.TimeOfDay = time;
        cs.DayCount = day;
        StartSandbox(cs, client: true);
    }

    private void HostNow()
    {
        var err = _net.StartHost(_saveName ?? "Dünya");
        if (err != Error.Ok)
        {
            _session.Hud.ShowBanner("Sunucu başlatılamadı (24565 portu kullanımda olabilir).", 5f);
            return;
        }
        _net.AttachSession(_session);
        _net.ImportPeerInv(_peerInvLoaded);
        _net.OnMessage = msg => _session?.Hud.ShowBanner(msg, 3f);
        var ips = Network.LanDiscovery.LocalAddresses();
        string addr = ips.Count > 0 ? string.Join(" / ", ips) : "IP bulunamadı";
        _session.Hud.ShowBanner($"Çok oyunculu açık — arkadaşların bu adrese katılabilir: {addr}", 9f);
    }

    private void ShowCharacterMenu()
    {
        ClearMenu();
        _charMenu = new UI.CharacterMenu { Name = "CharacterMenu", OnBack = ShowMainMenu };
        AddChild(_charMenu);
    }

    private void ClearMenu()
    {
        if (_info != null) { _info.QueueFree(); _info = null; }
        if (_newWorld != null) { _newWorld.QueueFree(); _newWorld = null; }
        if (_charMenu != null) { _charMenu.QueueFree(); _charMenu = null; }
        if (_mpMenu != null) { _mpMenu.QueueFree(); _mpMenu = null; }
        if (_menu != null) { _menu.QueueFree(); _menu = null; }
        if (_saveMenu != null) { _saveMenu.QueueFree(); _saveMenu = null; }
    }

    private void ShowSaveMenu()
    {
        ClearMenu();
        _saveMenu = new UI.SaveMenu { Name = "SaveMenu" };
        _saveMenu.OnBack = ShowMainMenu;
        _saveMenu.OnNewWorld = ShowNewWorldMenu;
        _saveMenu.OnLoad = name =>
        {
            var save = Core.SaveSystem.Load(name);
            if (save != null) StartSandbox(save);
            else StartSandbox(NewWorldSave());
        };
        AddChild(_saveMenu);
    }

    private Core.SaveData NewWorldSave() => NewWorldSave(UniqueWorldName(), (int)((long)Time.GetUnixTimeFromSystem() & 0x7FFFFFFF));

    private Core.SaveData NewWorldSave(string name, int seed)
    {
        long now = (long)Time.GetUnixTimeFromSystem();
        var save = new Core.SaveData
        {
            Name = name,
            Seed = seed,
            SavedUnix = now,
            TimeOfDay = Core.EnvironmentController.Morning,
        };
        foreach (var (block, count) in DefaultKit) save.Inventory[block] = count;
        return save;
    }

    private static string UniqueWorldName()
    {
        var taken = new System.Collections.Generic.HashSet<string>();
        foreach (var s in Core.SaveSystem.List()) taken.Add(s.Name);
        if (!taken.Contains("Dünya")) return "Dünya";
        for (int i = 2; ; i++)
            if (!taken.Contains($"Dünya {i}")) return $"Dünya {i}";
    }

    private void SaveCurrent()
    {
        if (_session == null || string.IsNullOrEmpty(_saveName)) return;
        Core.SaveSystem.Save(_session.CaptureSave(_saveName, (long)Time.GetUnixTimeFromSystem()));
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) SaveCurrent();
    }

    private void StartSandbox() => StartSandbox(NewWorldSave());

    private void LaunchShowcase(string id)
    {
        if (id == "blocks") StartBlockGallery();
        else StartShowcase();
    }

    private void BeginShowcaseSession(Vector3 spawn)
    {
        ClearMenu();
        _saveName = null;
        _session = new GameSession { Name = "Session", ReturnToMenuRequested = ShowMainMenu };
        AddChild(_session);
        _session.Setup(spawn, creative: false);
        _session.Env.SetCycle(true, 240f);
        _session.Env.TimeOfDay = Core.EnvironmentController.Morning;
        _session.Env.SetWeatherFollow(_session.Player);
        _session.Player.SafeMode = true;
    }

    private void StartShowcase()
    {
        BeginShowcaseSession(new Vector3(20, 2, 112));
        var world = _session.World;
        Core.WorldGen.FxShowcase(world);
        world.MarkAllDirty();
        world.RebuildAllNow();

        var fire = _session.Fire;
        fire.AddFire(new Vector3(22.5f, 2f, 102.5f), Core.FireKind.Candle);
        fire.AddFire(new Vector3(22.5f, 2f, 106.5f), Core.FireKind.Torch);
        fire.AddFire(new Vector3(27.5f, 1f, 104.5f), Core.FireKind.Campfire);
        fire.AddFire(new Vector3(24.5f, 2f, 104.5f), Core.FireKind.Forge, Core.FirePalette.Forge);
        fire.AddFire(new Vector3(31.5f, 3f, 102.5f), Core.FireKind.Brazier);
        fire.AddFire(new Vector3(31.5f, 4f, 106.5f), Core.FireKind.Altar);

        world.AddChild(PixelBlock.World.ChurnWater.Build(world));

        void Sign(float x, float y, float z, string title, string body) =>
            world.AddChild(PixelBlock.World.Signpost.Create(new Vector3(x, y, z), body, title));

        Sign(20, 1, 104, "FX Showcase",
            """
            Welcome! This world demonstrates the engine's visual effects.

            Controls:
              [F5]  cycle weather (clear / rain / snow)
              [F6]  cycle time of day
              [F7]  cycle the bloom / glow mood
              [H]   bless the fires (holy / forge / normal)
              [L]   cycle ambient-life density (off / sparse / normal / lush)
              V       toggle fly
              Shift   sprint

            Walk north and read each sign with E.
            """);
        Sign(27, 1, 100, "Fire & Light",
            """
            Living fire, one of every size: a candle, a torch, a campfire, a forge,
            a brazier and a tall altar fire. Each flickers, breathes its warm light,
            and throws drifting embers; the bigger ones trail wind-bent smoke.

            Press [H] to bless the flames into holy fire (white-gold), again for
            forge-red, again to return. Fire reads best at dusk or night — [F6].
            """);
        Sign(20, 1, 95, "Footstep Dust",
            "Walk north across the material bands — dirt, sand, stone, snow, planks, cloth. "
            + "Each footstep kicks up a little dust tinted to that material, with its own sound.");
        Sign(17, 1, 79, "Landing Dust",
            "Climb the four steps onto the platform, then jump off the north edge — "
            + "you land in a puff of dust scaled to how far you fell.");
        Sign(15, 1, 106, "Rivers & Waterfall",
            """
            Water that moves. The source pool spills over the lip and pours down a
            falling curtain into the catch pool below, throwing up a fine mist.

            Watch the lip: the surface streams in the direction it flows (foam crests
            ride the current). Jump into the catch pool for a splash and a ripple ring.
            """);
        Sign(16, 1, 49, "Flowing River",
            """
            A stepped mountain stream descending toward you. Watch the surface — the
            current streams downhill (the highlights and foam ride the flow direction),
            and each little drop churns into the pool at the foot.
            """);
        Sign(6, 1, 66, "Water", "Jump into the pool for a splash and a ripple.");
        Sign(33, 1, 61, "Bloom / Glow",
            "These lamps emit light that blooms. Press [F7] to cycle the glow mood — Divine makes "
            + "the whole scene radiant, Plague crushes it flat. Try it at night with [F6].");
        Sign(20, 1, 50, "Wind & Speed",
            "The grass leans and shimmers with the wind. Hold Shift to sprint — the view widens "
            + "(a FOV kick) and the vignette tightens for a sense of speed.");
        Sign(14, 1, 32, "Ambient Life",
            """
            A living world. By day, butterflies flit over the meadow while pollen and
            dandelion fluff drift on the breeze and birds cross the sky; leaves and blossom
            shed near the trees; fish dart in the pond and now and then leap with a splash.
            It all eases with the time of day, the weather and the wind.

            Press [L] to cycle the density: sparse / normal / lush / off.
            """);
        Sign(20, 1, 30, "Depth Haze",
            "Distant terrain fades into a soft atmospheric haze, giving the world a sense of scale. "
            + "Look north to the far ridge and compare it with the grass at your feet.");

        _session.AddChild(new PixelBlock.World.ShowcaseController
        {
            Name = "ShowcaseController", Env = _session.Env, Hud = _session.Hud, Fire = _session.Fire, Ambient = _session.Ambient,
        });

        _session.Hud.ShowBanner("FX Showcase — walk to each sign and press E to read.   [F5] weather  [F6] time  [F7] glow  [L] life", 8f);
    }

    private void StartBlockGallery()
    {
        BeginShowcaseSession(new Vector3(24, 2, 18));
        var world = _session.World;
        Core.WorldGen.Showcase(world);
        world.MarkAllDirty();
        world.RebuildAllNow();
        world.AddChild(PixelBlock.World.Signpost.Create(new Vector3(24, 1, 16),
            "Block Gallery — one pillar of every block type"));
        _session.Hud.ShowBanner("Block Gallery — every block type.   Esc to pause / return to menu.", 6f);
    }

    private void StartSandbox(Core.SaveData save, bool client = false)
    {
        ClearMenu();
        _saveName = client ? null : save.Name;
        _peerInvLoaded = new System.Collections.Generic.Dictionary<string, string>(save.PeerInv);
        _session = new GameSession { Name = "Session" };
        _session.ReturnToMenuRequested = () => { SaveCurrent(); ShowMainMenu(); };
        AddChild(_session);

        var gen = new Core.TerrainGenerator(save.Seed);
        Vector3 spawn = save.PlayerPos == Vector3.Zero
            ? new Vector3(24.5f, gen.SurfaceHeight(24, 24) + 3f, 24.5f)
            : save.PlayerPos;

        _session.Setup(spawn, creative: false);
        _session.Env.TimeOfDay = save.TimeOfDay;
        _session.Env.SetCycle(true);

        var world = _session.World;
        var edits = new System.Collections.Generic.List<(int, int, int, ushort)>();
        foreach (var (x, y, z, block) in save.Edits)
            edits.Add((x, y, z, Core.BlockRegistry.IdOf(block)));
        world.PreloadEdits(edits);
        world.StartStreaming(gen, _session.Player, renderDistance: Core.Settings.ViewDistance, minChunkY: -1, maxChunkY: 3);
        world.EnsureSpawnArea(spawn, radius: 2);

        _session.Env.SetWeatherFollow(_session.Player);
        _session.Ambient?.SetGenerator(gen);
        if (!client)
            _session.AddChild(new Core.WeatherDirector
            {
                Name = "Weather", Generator = gen, Player = _session.Player, Env = _session.Env,
            });

        var kit = new System.Collections.Generic.List<(string, int)>();
        foreach (var (block, count) in save.Inventory) kit.Add((block, count));
        _session.EnableSurvival(kit.ToArray());
        _session.RestoreTeacherState(save.Signposts, save.Waypoints);
        if (save.Hunger >= 0f) _session.Player.SetHunger(save.Hunger);
        for (int i = 0; i < 4; i++)
            if (!string.IsNullOrEmpty(save.Armor[i])) _session.Player.SetArmor(i, Core.BlockRegistry.IdOf(save.Armor[i]));

        if (save.Slots.Count > 0 && _session.Inventory != null)
        {
            _session.Inventory.Clear();
            for (int i = 0; i < save.Slots.Count && i < Core.Inventory.TotalSlots; i++)
            {
                var (sname, scount) = save.Slots[i];
                if (!string.IsNullOrEmpty(sname)) _session.Inventory.SetSlot(i, Core.BlockRegistry.IdOf(sname), scount);
            }
        }

        var clock = new Core.SeasonClock { Name = "Seasons", Env = _session.Env, World = world, DayCount = save.DayCount };
        _session.AddChild(clock);

        var farm = new Farming.FarmManager { Name = "Farm", World = world, Player = _session.Player, Env = _session.Env, ClientMode = client };
        _session.AddChild(farm);
        var animals = new Animals.AnimalDirector
        {
            Name = "Animals", World = world, Player = _session.Player, Env = _session.Env, Gen = gen,
            Inv = _session.Inventory, Hotbar = _session.Hud.Hotbar, PuppetMode = client,
        };
        _session.AddChild(animals);
        if (!client)
            _session.AddChild(new Combat.NightSpawner
            {
                Name = "NightSpawner", World = world, Player = _session.Player, Env = _session.Env,
                Inv = _session.Inventory, SpawnParent = _session, Notify = m => _session.Hud.ShowBanner(m, 3f),
            });
        var containers = new Core.Containers { Name = "Containers", World = world, Player = _session.Player, DropParent = _session, PlayerInv = _session.Inventory };
        _session.AddChild(containers);
        if (!client) containers.Import(save.Containers);
        _session.AddChild(new Core.WildPlants
        {
            Name = "WildPlants", World = world, Player = _session.Player, Inv = _session.Inventory, Seed = save.Seed,
        });
        if (!client)
        {
            farm.Import(save.Crops);
            animals.Import(save.Animals);
        }
        else if (!string.IsNullOrEmpty(_clientInv) && _session.Inventory != null)
        {
            _session.Inventory.Decode(_clientInv);
        }

        if (!client)
        {
            var autosave = new Timer { Name = "AutoSave", WaitTime = 60, Autostart = true, OneShot = false };
            _session.AddChild(autosave);
            autosave.Timeout += SaveCurrent;
        }

        _session.Hud.ShowBanner($"{save.Name} — Seed: {save.Seed}  (WASD hareket · +/- yerleştir/kır · G uç · Tab envanter · Enter sohbet · / komut)", 6f);
        Core.AudioManager.StartMusic();
        Core.AudioManager.StartAmbience();
        Core.AudioManager.SetMusicMood(Core.MusicMood.Calm);
        if (client)
        {
            _net.AttachSession(_session);
            if (_session.Inventory != null) _session.Inventory.Changed += _net.MarkInventoryDirty;
            _net.OnMessage = msg => _session?.Hud.ShowBanner(msg, 3f);
            _session.Hud.ShowBanner("Çok oyunculu dünyaya katıldın", 4f);
            _net.SendReady();
            return;
        }
        SaveCurrent();
        if (_hostMode) { _hostMode = false; HostNow(); }
    }

    private GameSession StartLesson(Lessons.ILesson lesson)
    {
        ClearMenu();
        _session = new GameSession { Name = "Session", ReturnToMenuRequested = ShowMainMenu, LessonId = lesson.Id };
        AddChild(_session);
        _session.Setup(lesson.Spawn, creative: false);
        if (lesson.TimeOfDay is float tod) _session.Env.SetFixedTime(tod);
        else _session.Env.SetCycle(true);
        lesson.Build(_session);
        var quest = lesson.BuildQuest(_session);
        if (quest != null) _session.StartQuest(quest);
        _session.QuestCompleted += id => Core.CampaignStore.MarkComplete(id);
        _session.Hud.ShowBanner($"{lesson.Title}", 4f);
        Core.AudioManager.StartMusic();
        Core.AudioManager.StartAmbience();
        Core.AudioManager.SetMusicMood(lesson.Mood);
        return _session;
    }

    private async void RunLessonTest()
    {
        var lesson = Lessons.LessonCatalog.Get("david");
        var session = new GameSession { Name = "Session", LessonId = "david" };
        AddChild(session);
        session.Setup(lesson.Spawn, creative: false, captureMouse: false);
        lesson.Build(session);
        var quest = lesson.BuildQuest(session);
        if (quest != null) session.StartQuest(quest);
        session.Player.InputEnabled = false;

        await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
        await Capture("res://_lesson.png", 0.3);

        NpcSys.Npc jesse = null;
        foreach (Node n in GetTree().GetNodesInGroup("npc"))
            if (n is NpcSys.Npc npc && npc.NpcName == "Jesse") jesse = npc;
        bool talked = false;
        if (jesse != null) jesse.Talked += () => talked = true;
        session.StartDialogueWith(jesse);
        int g = 0;
        while (session.InDialogue && g++ < 20)
        {
            if (session.Dialogue.HasChoices) session.Dialogue.Choose(0);
            else session.Dialogue.Advance();
            await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
        }

        Combat.Enemy goliath = null;
        foreach (Node n in GetTree().GetNodesInGroup("enemy"))
            if (n is Combat.Enemy e && e.Type.Name == "Goliath") goliath = e;
        bool defeated = false;
        if (goliath != null) goliath.Defeated += () => defeated = true;
        session.Player.GlobalPosition = new Vector3(32, 2, 26);
        await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
        bool woke = goliath != null && goliath.Target != null;

        goliath?.TakeDamage(9999, session.Player);
        await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);

        GD.Print($"[RA] lesson-test: jesseTalked={talked} goliathWoke={woke} goliathDefeated={defeated}");
        await Capture("res://_lesson_victory.png", 0.3);
        GetTree().Quit(0);
    }

    private async void RunCreationTest()
    {
        var lesson = Lessons.LessonCatalog.Get("creation");
        var session = new GameSession { Name = "Session", LessonId = "creation" };
        AddChild(session);
        session.Setup(lesson.Spawn, creative: false, captureMouse: false);
        lesson.Build(session);
        var quest = lesson.BuildQuest(session);
        if (quest != null) session.StartQuest(quest);
        session.Player.InputEnabled = false;

        await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
        await Capture("res://_creation.png", 0.3);

        var animals = new System.Collections.Generic.List<NpcSys.Npc>();
        foreach (Node n in GetTree().GetNodesInGroup("npc"))
            if (n is NpcSys.Npc a) animals.Add(a);
        int talked = 0;
        foreach (var a in animals) a.Talked += () => talked++;

        foreach (var a in animals)
        {
            session.StartDialogueWith(a);
            int g = 0;
            while (session.InDialogue && g++ < 12)
            {
                if (session.Dialogue.HasChoices) session.Dialogue.Choose(0);
                else session.Dialogue.Advance();
                await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
            }
        }

        session.Player.GlobalPosition = new Vector3(32, 2, 12);
        await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
        GD.Print($"[RA] creation-test: animals={animals.Count} named={talked}");
        await Capture("res://_creation2.png", 0.3);
        GetTree().Quit(0);
    }

    private void RunQuestTest()
    {
        Core.BlockRegistry.EnsureInit();
        var session = new GameSession { Name = "Session", LessonId = "quest-test" };
        AddChild(session);
        session.Setup(new Vector3(8, 5, 8), creative: false, captureMouse: false);
        Core.WorldGen.FlatGround(session.World, 0, 16, 0, 16, 0);
        session.World.MarkAllDirty();
        session.World.RebuildAllNow();
        session.Player.InputEnabled = false;

        int preArmEmits = 0;
        session.World.BlockChanged += (p, o, n, c) => preArmEmits++;
        ushort stone = Core.BlockRegistry.IdOf("stone");
        session.World.SetBlock(2, 1, 2, stone);
        bool preArmSilent = preArmEmits == 0;

        var quest = new Quests.Quest
        {
            Objectives = new[]
            {
                Quests.Quest.Talk("Jesse", "Talk to Jesse"),
                Quests.Quest.TalkAny(3, "Name the animals"),
                Quests.Quest.Defeat("Goliath", "Defeat Goliath"),
                Quests.Quest.Reach("zone", "Reach the zone"),
                Quests.Quest.Break("stone", 2, "Mine two stone"),
                Quests.Quest.Place("definitely_not_a_block", 1, "Bad block key", optional: true),
            },
        };
        int completedFires = 0;
        session.QuestCompleted += _ => completedFires++;
        session.StartQuest(quest);

        session.Quest.OnTalk("Jesse");
        bool talkOk = session.Quest.IsDone(0);

        session.Quest.OnTalk("Lion");
        session.Quest.OnTalk("Lion");
        session.Quest.OnTalk("Ox");
        session.Quest.OnTalk("Dove");
        bool talkAnyOk = session.Quest.IsDone(1) && session.Quest.Progress(1) == 3;

        session.Quest.OnDefeat("Soldier");
        session.Quest.OnDefeat("Goliath");
        bool defeatOk = session.Quest.IsDone(2);

        session.Quest.OnReach("elsewhere");
        session.Quest.OnReach("zone");
        bool reachOk = session.Quest.IsDone(3);

        session.World.SetBlock(3, 1, 3, stone, cause: Core.BlockChangeCause.LessonBuild);
        session.World.SetBlock(3, 1, 3, 0);
        session.World.SetBlock(4, 1, 4, stone, cause: Core.BlockChangeCause.LessonBuild);
        session.World.SetBlock(4, 1, 4, 0);
        bool breakOk = session.Quest.IsDone(4) && session.Quest.Progress(4) >= 2;

        ushort dirt = Core.BlockRegistry.IdOf("dirt");
        session.World.SetBlock(5, 1, 5, dirt, cause: Core.BlockChangeCause.LessonBuild);
        session.World.SetBlock(5, 1, 5, 0);
        bool badKeyGuarded = !session.Quest.IsDone(5);

        bool allDone = session.Quest.AllDone;

        var session2 = new GameSession { Name = "Session2", LessonId = "quest-test-2" };
        AddChild(session2);
        session2.Setup(new Vector3(8, 5, 8), creative: false, captureMouse: false);
        Core.WorldGen.FlatGround(session2.World, 0, 16, 0, 16, 0);
        session2.World.MarkAllDirty();
        session2.World.RebuildAllNow();
        session2.Player.InputEnabled = false;

        int completedFires2 = 0;
        session2.QuestCompleted += _ => completedFires2++;
        session2.StartQuest(new Quests.Quest
        {
            Objectives = new[]
            {
                Quests.Quest.TalkAny(2, "Name two"),
                Quests.Quest.Talk("Jesse", "Talk to Jesse"),
                Quests.Quest.Defeat("Goliath", "Optional foe", optional: true),
            },
        });
        session2.Quest.OnTalk("Jesse");
        session2.Quest.OnTalk("Jesse");
        bool continueFix = session2.Quest.IsDone(1);
        session2.Quest.OnTalk("Lion");
        bool optionalOk = session2.Quest.AllDone && !session2.Quest.IsDone(2);

        GD.Print($"[RA] quest-test: preArmSilent={preArmSilent} talk={talkOk} talkAny={talkAnyOk} " +
                 $"defeat={defeatOk} reach={reachOk} break={breakOk} badKeyGuarded={badKeyGuarded} " +
                 $"allDone={allDone} completedFires={completedFires} continueFix={continueFix} " +
                 $"optionalOk={optionalOk} completedFires2={completedFires2}");
        QuitSoon();
    }

    private void RunCampaignTest()
    {
        Core.CampaignStore.DeleteAll();
        var fresh = Core.CampaignStore.Load();
        bool creationUnlocked = fresh.IsUnlocked("creation");
        bool davidLocked = !fresh.IsUnlocked("david");

        fresh.MarkComplete("creation");
        Core.CampaignStore.Save(fresh);

        var reloaded = Core.CampaignStore.Load();
        bool creationDone = reloaded.IsComplete("creation");
        bool davidUnlocked = reloaded.IsUnlocked("david");
        bool nextOk = Core.Campaign.NextAfter("creation") == "david";

        Core.CampaignStore.DeleteAll();

        GD.Print($"[RA] campaign-test: creationUnlocked={creationUnlocked} davidLocked={davidLocked} " +
                 $"creationDone={creationDone} davidUnlocked={davidUnlocked} nextOk={nextOk}");
        QuitSoon();
    }

    private void RunJsonLessonTest()
    {
        Core.BlockRegistry.EnsureInit();
        var lesson = Lessons.LessonCatalog.Get("jericho");
        bool inCatalog = lesson is Lessons.JsonLesson && lesson.Id == "jericho";

        var session = new GameSession { Name = "Session", LessonId = "jericho" };
        AddChild(session);
        session.Setup(lesson.Spawn, creative: false, captureMouse: false);
        lesson.Build(session);
        var quest = lesson.BuildQuest(session);
        if (quest != null) session.StartQuest(quest);
        session.Player.InputEnabled = false;

        ushort mud = Core.BlockRegistry.IdOf("mud_brick");
        bool wallBuilt = session.World.GetBlockId(22, 3, 6) == mud;
        bool gateOpen = session.World.GetBlockId(32, 1, 18) == 0;

        bool joshua = false;
        foreach (Node n in GetTree().GetNodesInGroup("npc"))
            if (n is NpcSys.Npc npc && npc.NpcName == "Joshua") joshua = true;

        bool completed = false;
        session.QuestCompleted += _ => completed = true;

        session.Quest.OnTalk("Joshua");
        ushort gold = Core.BlockRegistry.IdOf("gold_block");
        foreach (var p in new[] { new Vector3I(28, 1, 40), new Vector3I(32, 1, 38), new Vector3I(36, 1, 40) })
            if (session.World.GetBlockId(p) == gold) session.World.SetBlock(p, 0);
        bool collectOk = session.Quest.IsDone(1);
        session.Quest.OnReach("the-gate");
        bool allDone = session.Quest.AllDone;

        GD.Print($"[RA] jsonlesson-test: inCatalog={inCatalog} wall={wallBuilt} gate={gateOpen} " +
                 $"npc={joshua} collect={collectOk} allDone={allDone} completed={completed}");

        const string inline = """
            {
              "id": "_enemytest", "title": "Enemy Test", "spawn": [8, 3, 8],
              "terrain": [ { "op": "flat", "x0": 0, "x1": 16, "z0": 0, "z1": 16, "y": 0 } ],
              "enemies": [
                { "type": "soldier", "pos": [4, 1, 4], "dormant": true },
                { "type": "soldier", "pos": [6, 1, 4], "dormant": true }
              ],
              "narrations": [ { "id": "edge", "pos": [8, 3, 8], "size": [6, 6, 6], "lines": ["Here they come."] } ],
              "quest": {
                "objectives": [
                  { "kind": "reach", "key": "edge", "label": "Hold the line", "onComplete": { "wake": "*" } },
                  { "kind": "defeat", "key": "Soldier", "count": 2, "label": "Rout the guards" }
                ]
              }
            }
            """;
        var lesson2 = Lessons.JsonLesson.FromJson(inline, "inline-enemytest");
        var s2 = new GameSession { Name = "Session2", LessonId = "_enemytest" };
        AddChild(s2);
        s2.Setup(lesson2.Spawn, creative: false, captureMouse: false);
        lesson2.Build(s2);
        var q2 = lesson2.BuildQuest(s2);
        if (q2 != null) s2.StartQuest(q2);
        s2.Player.InputEnabled = false;

        var foes = new System.Collections.Generic.List<Combat.Enemy>();
        foreach (Node n in s2.World.GetChildren())
            if (n is Combat.Enemy en) foes.Add(en);
        bool twoFoes = foes.Count == 2;
        bool dormant = twoFoes && foes.TrueForAll(f => f.Target == null);

        s2.Quest.OnReach("edge");
        bool wokeAll = twoFoes && foes.TrueForAll(f => f.Target != null);

        foreach (var f in foes) f.TakeDamage(9999, s2.Player);
        bool defeatedAll = s2.Quest.IsDone(1) && s2.Quest.AllDone;

        GD.Print($"[RA] jsonlesson-enemies: twoFoes={twoFoes} dormant={dormant} wokeAll={wokeAll} defeatedAll={defeatedAll}");
        QuitSoon();
    }

    private async void RunMenuTest()
    {
        ShowMainMenu();
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        await Capture("res://_menu.png", 0.2);

        _menu.OnPlayLesson("david");
        await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
        await Capture("res://_menu_lesson.png", 0.2);
        GD.Print($"[RA] menu-test: sessionStarted={_session != null} menuCleared={_menu == null}");
        GetTree().Quit(0);
    }

    private void RunSaveTest()
    {
        var world = new Core.VoxelWorld { Name = "World" };
        AddChild(world);
        Core.WorldGen.Showcase(world);
        world.MarkAllDirty();
        world.RebuildAllNow();

        var snap = new System.Collections.Generic.Dictionary<Vector3I, ushort>();
        for (int x = -2; x <= 24; x++)
        for (int y = -3; y <= 6; y++)
        for (int z = -2; z <= 18; z++)
        {
            ushort id = world.GetBlockId(x, y, z);
            if (id != 0) snap[new Vector3I(x, y, z)] = id;
        }

        const string path = "user://worlds/test.rworld";
        Core.WorldIO.SaveWorld(world, path);

        var prefab = Core.WorldIO.Capture(world, new Vector3I(0, 0, 0), new Vector3I(2, 2, 2));
        world.Clear();
        int afterClear = world.GetBlockId(0, 0, 0);
        Core.WorldIO.LoadWorld(world, path);

        int mismatches = 0, chec_d = 0;
        foreach (var kv in snap)
        {
            chec_d++;
            if (world.GetBlockId(kv.Key) != kv.Value) mismatches++;
        }

        Core.WorldIO.Stamp(world, prefab, new Vector3I(60, 1, 60));
        ushort expectedCorner = Core.BlockRegistry.IdOf(prefab.Palette[prefab.Cells[prefab.Index(0, 0, 0)]]);
        bool stampOk = world.GetBlockId(60, 1, 60) == expectedCorner;

        GD.Print($"[RA] save-test: snapshot={chec_d} mismatches={mismatches} clearedTo={afterClear} " +
                 $"prefab={prefab.Size.X}x{prefab.Size.Y}x{prefab.Size.Z} stampOk={stampOk}");
        QuitSoon();
    }

    private async void RunNpcTest()
    {
        var session = new GameSession { Name = "Session" };
        AddChild(session);
        session.Setup(new Vector3(24, 1, 24), creative: false);
        Core.WorldGen.FlatGround(session.World, 0, 48, 0, 48, 0);
        session.World.MarkAllDirty();
        session.World.RebuildAllNow();
        session.Player.InputEnabled = false;

        var npc = new NpcSys.Npc
        {
            NpcName = "Jesse",
            Robe = new Color(0.5f, 0.35f, 0.55f),
            Dialogue = Dialogue.DialogueData.Linear("Jesse",
                "Welcome, young shepherd.",
                "The Philistines have gathered for battle in the Valley of Elah.",
                "Will you carry bread to your brothers at the camp?"),
        };
        session.World.AddChild(npc);
        npc.GlobalPosition = new Vector3(24, 1, 19);

        var trig = World.NarrationTrigger.Create(new Vector3(24, 1.5f, 24), new Vector3(3, 3, 3),
            session.Narrator, "And David rose early in the morning...", "...and went, as Jesse had commanded him.");
        session.World.AddChild(trig);

        session.Player.Head.Rotation = new Vector3(-0.1f, 0, 0);
        await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
        await Capture("res://_npc.png", 0.2);

        session.StartDialogue(npc.Dialogue);
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        bool boxShown = session.Dialogue.Active;
        await Capture("res://_dialogue.png", 0.2);

        int guard = 0;
        while (session.InDialogue && guard++ < 12)
        {
            session.Dialogue.Advance();
            await ToSignal(GetTree().CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
        }
        GD.Print($"[RA] npc-test: dialogueShown={boxShown} finished={!session.InDialogue} steps={guard} " +
                 $"inputRestored={session.Player.InputEnabled}");

        GetTree().Quit(0);
    }

    private void RunMobTest()
    {
        var container = new Node3D { Name = "MobTest" };
        AddChild(container);

        var human = Combat.MobModel.BuildHumanoid(
            new Color(0.8f, 0.66f, 0.52f), new Color(0.4f, 0.4f, 0.5f), new Color(0.6f, 0.4f, 0.3f));
        container.AddChild(human);
        var beast = Combat.MobModel.BuildBeast(new Color(0.4f, 0.36f, 0.32f), new Color(0.6f, 0.56f, 0.5f));
        container.AddChild(beast);

        bool humanPivots = !human.Beast && human.LeftLeg != null && human.RightLeg != null
                           && human.LeftArm != null && human.RightArm != null && human.Head != null;
        bool beastPivots = beast.Beast && beast.Legs != null && beast.Legs.Length == 4
                           && beast.Legs[0] != null && beast.Legs[3] != null
                           && beast.Tail != null && beast.Head != null;

        for (int i = 0; i < 8; i++) human.Animate(3.5f, 0.05f);
        float lLeg = human.LeftLeg.Rotation.X, rLeg = human.RightLeg.Rotation.X;
        bool walks = Mathf.Abs(lLeg) > 0.01f && Mathf.Sign(lLeg) != Mathf.Sign(rLeg);

        for (int i = 0; i < 8; i++) beast.Animate(4f, 0.05f);
        float bx = beast.Legs[0].Rotation.X;
        bool beastGait = Mathf.Abs(bx) > 0.01f
                         && Mathf.Sign(bx) == Mathf.Sign(beast.Legs[3].Rotation.X)
                         && Mathf.Sign(bx) != Mathf.Sign(beast.Legs[1].Rotation.X);

        for (int i = 0; i < 20; i++) human.Animate(0f, 0.05f);
        bool idleSettles = Mathf.Abs(human.LeftLeg.Rotation.X) < 0.01f;

        human.SetFlash(true);
        bool flashOn = FirstMesh(human)?.MaterialOverride is StandardMaterial3D mOn && mOn.EmissionEnabled;
        human.SetFlash(false);
        bool flashOff = FirstMesh(human)?.MaterialOverride is StandardMaterial3D mOff && !mOff.EmissionEnabled;

        bool fxOk = true;
        try { human.Attack(); human.Squash(); beast.Attack(); beast.Squash(); }
        catch (System.Exception ex) { fxOk = false; GD.PrintErr("[RA] mob attack/squash threw: " + ex.Message); }

        bool enemyOk = true, defeated = false;
        try
        {
            var giant = new Combat.Enemy { Type = Combat.EnemyType.Giant(), Target = null, Name = "MobTestGiant" };
            container.AddChild(giant);
            giant.TakeDamage(10f, null);
            giant.TakeDamage(99999f, null);
            defeated = !giant.IsAlive;
        }
        catch (System.Exception ex) { enemyOk = false; GD.PrintErr("[RA] enemy lifecycle threw: " + ex.Message); }

        bool allPass = humanPivots && beastPivots && walks && beastGait && idleSettles
                       && flashOn && flashOff && fxOk && enemyOk && defeated;
        string summary = $"humanPivots={humanPivots} beastPivots={beastPivots} walks={walks} " +
                         $"beastGait={beastGait} idleSettles={idleSettles} flashOn={flashOn} flashOff={flashOff} " +
                         $"attackSquashOk={fxOk} enemyLifecycle={enemyOk} defeated={defeated} ALLPASS={allPass}";
        GD.Print("[RA] mob-test: " + summary);

        using (var f = FileAccess.Open("res://_mob_test.txt", FileAccess.ModeFlags.Write))
            f?.StoreString(summary);
        QuitSoon();
    }

    private async void RunClimbTest()
    {
        Core.BlockRegistry.EnsureInit();
        var session = new GameSession { Name = "Session", LessonId = "climb-test" };
        AddChild(session);
        session.Setup(new Vector3(2, 5, 2), creative: false, captureMouse: false);
        Core.WorldGen.FlatGround(session.World, 0, 32, 0, 32, 0);
        session.Player.InputEnabled = false;

        ushort stone = Core.BlockRegistry.IdOf("stone");
        for (int x = 12; x <= 20; x++)
        for (int z = 4; z <= 12; z++)
        {
            session.World.SetBlock(x, 1, z, stone, false);
            session.World.SetBlock(x, 2, z, stone, false);
        }
        session.World.MarkAllDirty();
        session.World.RebuildAllNow();

        var goal = new Node3D { Name = "Goal" };
        session.World.AddChild(goal);
        goal.GlobalPosition = new Vector3(16, 3, 8);

        var giant = session.SpawnEnemy(Combat.EnemyType.Giant(), new Vector3(8, 1, 8));
        giant.Target = goal;
        float startY = giant.GlobalPosition.Y;

        await ToSignal(GetTree().CreateTimer(4.5), SceneTreeTimer.SignalName.Timeout);

        bool valid = GodotObject.IsInstanceValid(giant);
        float endY = valid ? giant.GlobalPosition.Y : -99f;
        float endX = valid ? giant.GlobalPosition.X : -99f;
        bool climbed = endY >= 2.5f;
        bool advanced = endX >= 11.5f;
        bool allPass = climbed && advanced;
        string summary = $"startY={startY:F2} endY={endY:F2} endX={endX:F2} " +
                         $"climbed={climbed} advanced={advanced} ALLPASS={allPass}";
        GD.Print("[RA] climb-test: " + summary);
        using (var f = FileAccess.Open("res://_climb_test.txt", FileAccess.ModeFlags.Write))
            f?.StoreString(summary);
        GetTree().Quit(0);
    }

    private void RunSpawnTest()
    {
        Core.BlockRegistry.EnsureInit();
        var session = new GameSession { Name = "Session", LessonId = "spawn-test" };
        AddChild(session);
        session.Setup(new Vector3(8, 5, 8), creative: false, captureMouse: false);
        Core.WorldGen.FlatGround(session.World, 0, 16, 0, 16, 0);
        session.World.SetBlock(5, 1, 5, Core.BlockRegistry.IdOf("grass"), false);
        session.World.SetBlock(11, 1, 11, Core.BlockRegistry.IdOf("stone"), false);
        session.World.SetBlock(11, 2, 11, Core.BlockRegistry.IdOf("stone"), false);
        session.World.SetBlock(11, 3, 11, Core.BlockRegistry.IdOf("stone"), false);
        session.World.MarkAllDirty();
        session.World.RebuildAllNow();

        var onHill = session.SpawnEnemy(Combat.EnemyType.Soldier(), new Vector3(5, 1, 5));
        bool hillClear = onHill.GlobalPosition.Y >= 2f - 0.001f;

        var onFlat = session.SpawnEnemy(Combat.EnemyType.Soldier(), new Vector3(8, 1, 8));
        bool flatOk = Mathf.Abs(onFlat.GlobalPosition.Y - 1f) < 0.001f;

        var onPillar = session.SpawnEnemy(Combat.EnemyType.Giant(), new Vector3(11, 1, 11));
        bool pillarOk = onPillar.GlobalPosition.Y >= 4f - 0.001f;

        bool allPass = hillClear && flatOk && pillarOk;
        string summary = $"hillClear={hillClear}(y={onHill.GlobalPosition.Y:F2}) " +
                         $"flatOk={flatOk}(y={onFlat.GlobalPosition.Y:F2}) " +
                         $"pillarOk={pillarOk}(y={onPillar.GlobalPosition.Y:F2}) ALLPASS={allPass}";
        GD.Print("[RA] spawn-test: " + summary);
        using (var f = FileAccess.Open("res://_spawn_test.txt", FileAccess.ModeFlags.Write))
            f?.StoreString(summary);
        QuitSoon();
    }

    private void RunRiggedTest()
    {
        var container = new Node3D { Name = "RiggedTest" };
        AddChild(container);

        var c = new Color(0.7f, 0.6f, 0.5f);
        bool fallbackNull = Combat.CharacterModel.BuildHumanoid(c, c, c, null) is Combat.MobRig;
        bool fallbackBad = Combat.CharacterModel.BuildHumanoid(c, c, c, "res://does_not_exist.glb") is Combat.MobRig;

        var root = new Node3D { Name = "Char" };
        var mesh = new MeshInstance3D { Mesh = new BoxMesh { Material = new StandardMaterial3D() } };
        root.AddChild(mesh);
        var ap = new AnimationPlayer { Name = "AnimationPlayer" };
        root.AddChild(ap);
        var lib = new AnimationLibrary();
        lib.AddAnimation("Idle", new Animation { Length = 0.5f });
        lib.AddAnimation("Walk_A", new Animation { Length = 0.5f });
        lib.AddAnimation("1H_Melee_Attack", new Animation { Length = 0.4f });
        ap.AddAnimationLibrary("", lib);

        var rm = new Combat.RiggedModel { Name = "Rig" };
        container.AddChild(rm);
        bool inited = rm.Init(root);

        rm.Animate(3.0f, 0.016f); bool walks = ap.CurrentAnimation == "Walk_A";
        rm.Animate(0.0f, 0.016f); bool idles = ap.CurrentAnimation == "Idle";
        rm.Attack();              bool attacks = ap.CurrentAnimation == "1H_Melee_Attack";

        rm.SetFlash(true);
        bool flashOn = mesh.GetSurfaceOverrideMaterial(0) is StandardMaterial3D fm && fm.EmissionEnabled;
        rm.SetFlash(false);
        bool flashOff = mesh.GetSurfaceOverrideMaterial(0) is StandardMaterial3D fm2 && !fm2.EmissionEnabled;

        bool squashOk = true;
        try { rm.Squash(); }
        catch (System.Exception ex) { squashOk = false; GD.PrintErr("[RA] rigged squash threw: " + ex.Message); }

        bool allPass = fallbackNull && fallbackBad && inited && walks && idles && attacks
                       && flashOn && flashOff && squashOk;
        string summary = $"fallbackNull={fallbackNull} fallbackBad={fallbackBad} inited={inited} " +
                         $"walks={walks} idles={idles} attacks={attacks} flashOn={flashOn} " +
                         $"flashOff={flashOff} squashOk={squashOk} ALLPASS={allPass}";
        GD.Print("[RA] rigged-test: " + summary);
        using (var f = FileAccess.Open("res://_rigged_test.txt", FileAccess.ModeFlags.Write))
            f?.StoreString(summary);
        QuitSoon();
    }

    private static MeshInstance3D FirstMesh(Node n)
    {
        foreach (Node c in n.GetChildren())
        {
            if (c is MeshInstance3D mi) return mi;
            var found = FirstMesh(c);
            if (found != null) return found;
        }
        return null;
    }

    private async void RunCombatTest()
    {
        var session = new GameSession { Name = "Session" };
        AddChild(session);
        session.Setup(new Vector3(24, 1, 24), creative: false);
        Core.WorldGen.FlatGround(session.World, 0, 48, 0, 48, 0);
        session.World.MarkAllDirty();
        session.World.RebuildAllNow();
        session.Player.InputEnabled = false;

        var soldier = session.SpawnEnemy(Combat.EnemyType.Soldier(), new Vector3(24, 1, 16));
        var giant = session.SpawnEnemy(Combat.EnemyType.Giant(), new Vector3(29, 1, 14));
        var wolf = session.SpawnEnemy(Combat.EnemyType.Wolf(), new Vector3(34, 1, 24));
        wolf.Target = null;
        session.Hud.ShowBanner("Combat test", 5f);

        await ToSignal(GetTree().CreateTimer(0.7), SceneTreeTimer.SignalName.Timeout);
        await Capture("res://_combat.png", 0.2);

        float wolfBefore = wolf.Health;
        Vector3 camPos = session.Player.Camera.GlobalPosition;
        Vector3 wolfCenter = wolf.GlobalPosition + Vector3.Up * 1.0f;
        Vector3 dir = (wolfCenter - camPos).Normalized();
        Combat.Projectile.Spawn(session, camPos, dir * 45f, 22f, session.Player);
        await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
        GD.Print($"[RA] combat sling: wolf {wolfBefore:F0} -> {(GodotObject.IsInstanceValid(wolf) ? wolf.Health : 0):F0}");

        await ToSignal(GetTree().CreateTimer(2.6), SceneTreeTimer.SignalName.Timeout);
        float sdist = GodotObject.IsInstanceValid(soldier)
            ? (soldier.GlobalPosition - session.Player.GlobalPosition).Length() : -1;
        GD.Print($"[RA] combat chase: soldierDist={sdist:F1} playerHp={session.Player.Health:F0}");

        giant.TakeDamage(9999, session.Player);
        await ToSignal(GetTree().CreateTimer(0.7), SceneTreeTimer.SignalName.Timeout);
        GD.Print($"[RA] combat defeat: giantValid={GodotObject.IsInstanceValid(giant)}");

        GetTree().Quit(0);
    }

    private async void RunHudTest()
    {
        var session = new GameSession { Name = "Session" };
        AddChild(session);
        session.Setup(new Vector3(24, 1, 24), creative: true);
        Core.WorldGen.FlatGround(session.World, 0, 48, 0, 48, 0);

        for (int y = 1; y <= 3; y++)
            session.World.SetBlock(24, y, 18, Core.BlockRegistry.IdOf("stone"), false);
        session.World.MarkAllDirty();
        session.World.RebuildAllNow();

        session.Player.Head.Rotation = new Vector3(-0.25f, 0, 0);
        session.Hud.ShowBanner("Block interaction test", 5f);

        await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);

        bool placed = session.Interactor.PlaceAt(new Vector3I(24, 4, 18), Core.BlockRegistry.IdOf("gold_block"));
        bool broke = session.Interactor.BreakAt(new Vector3I(24, 1, 18));
        var hit = Core.VoxelRay.Cast(session.World, session.Player.Camera.GlobalPosition,
            -session.Player.Camera.GlobalTransform.Basis.Z, 6f);
        GD.Print($"[RA] hud-test: placed={placed} broke={broke} rayHit={hit.Ok} hitBlock={hit.Block} " +
                 $"afterPlace={Core.BlockRegistry.Get(session.World.GetBlockId(24, 4, 18)).Name} " +
                 $"afterBreak={Core.BlockRegistry.Get(session.World.GetBlockId(24, 1, 18)).Name}");

        await Capture("res://_hud.png", 0.4);
        GetTree().Quit(0);
    }

    private async void RunPlayerTest()
    {
        Core.Scenery.AddDaylight(this);
        var world = new Core.VoxelWorld { Name = "World" };
        AddChild(world);
        Core.WorldGen.FlatGround(world, 0, 32, 0, 32, 0);

        for (int x = 4; x <= 10; x++)
        for (int z = 4; z <= 10; z++)
        for (int y = -8; y <= 0; y++)
            world.SetBlock(x, y, z, Core.BlockRegistry.IdOf("stone"), false);
        for (int x = 5; x <= 9; x++)
        for (int z = 5; z <= 9; z++)
        for (int y = -7; y <= 0; y++)
            world.SetBlock(x, y, z, Core.BlockRegistry.IdOf("water"), false);
        world.MarkAllDirty();
        world.RebuildAllNow();

        var player = new PlayerSys.Player { Name = "Player", World = world, InputEnabled = false };
        AddChild(player);
        player.GlobalPosition = new Vector3(16, 8, 16);
        player.Camera.Current = true;

        await ToSignal(GetTree().CreateTimer(1.6), SceneTreeTimer.SignalName.Timeout);
        GD.Print($"[RA] player ground: y={player.GlobalPosition.Y:F2} onFloor={player.IsOnFloor()} " +
                 $"hp={player.Health:F1} inWater={player.InWater}");
        await Capture("res://_player_ground.png", 0.3);

        player.Respawn(new Vector3(7, 6, 7));
        await ToSignal(GetTree().CreateTimer(4.0), SceneTreeTimer.SignalName.Timeout);
        GD.Print($"[RA] player water: y={player.GlobalPosition.Y:F2} inWater={player.InWater} " +
                 $"headUnder={player.HeadUnderwater} air={player.Air:F1} hp={player.Health:F1}");
        await Capture("res://_player_water.png", 0.3);

        GetTree().Quit(0);
    }

    private async void RunSwimExitTest()
    {
        Core.Scenery.AddDaylight(this);
        var world = new Core.VoxelWorld { Name = "World" };
        AddChild(world);
        Core.WorldGen.FlatGround(world, 0, 32, 0, 32, 0);

        ushort water = Core.BlockRegistry.IdOf("water");
        ushort stone = Core.BlockRegistry.IdOf("stone");
        for (int x = 8; x <= 24; x++)
        for (int z = 8; z <= 16; z++)
        {
            for (int y = 0; y >= -2; y--) world.SetBlock(x, y, z, water, false);
            world.SetBlock(x, -3, z, stone, false);
        }
        world.MarkAllDirty();
        world.RebuildAllNow();

        var player = new PlayerSys.Player { Name = "Player", World = world };
        AddChild(player);
        player.GlobalPosition = new Vector3(16, 3, 12);
        player.Camera.Current = true;

        await ToSignal(GetTree().CreateTimer(1.6), SceneTreeTimer.SignalName.Timeout);
        float floatY = player.GlobalPosition.Y;
        bool floatIn = player.InWater;

        Input.ActionPress(Core.GameInput.Actions.Forward);
        bool exited = false;
        Vector3 exitPos = player.GlobalPosition;
        for (int i = 0; i < 50 && !exited; i++)
        {
            await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
            Vector3 q = player.GlobalPosition;

            if (!player.InWater && player.IsOnFloor() && q.Y > 0.9f && q.Z < 7.8f)
            {
                exited = true;
                exitPos = q;
            }
        }
        Input.ActionRelease(Core.GameInput.Actions.Forward);

        GD.Print($"[RA] swim-exit: floated y={floatY:F2} inWater={floatIn} -> " +
                 $"exitPos=({exitPos.X:F1},{exitPos.Y:F2},{exitPos.Z:F1}) EXITED_ONTO_BANK={exited}");
        GetTree().Quit(0);
    }

    private async void RunControlsTest()
    {
        Core.Settings.CaptureMode = Core.Settings.MouseCapture.ClickToCapture;
        var session = new GameSession { Name = "Session" };
        AddChild(session);
        session.Setup(new Vector3(24, 1, 22), creative: true);

        Core.WorldGen.FlatGround(session.World, 0, 48, 0, 48, 0);
        for (int y = 1; y <= 3; y++)
            session.World.SetBlock(24, y, 18, Core.BlockRegistry.IdOf("stone"), false);
        session.World.MarkAllDirty();
        session.World.RebuildAllNow();

        bool initFree = Input.MouseMode != Input.MouseModeEnum.Captured;

        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        var tgt = session.Interactor.CurrentTarget;
        Vector3I placeCell = tgt.Prev;

        Input.ActionPress(Core.GameInput.Actions.KbPlace);
        await ToSignal(GetTree().CreateTimer(0.12), SceneTreeTimer.SignalName.Timeout);
        Input.ActionRelease(Core.GameInput.Actions.KbPlace);
        bool placed = tgt.Ok && session.World.GetBlockId(placeCell) != 0;

        await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
        Vector3I breakCell = session.Interactor.CurrentTarget.Block;
        Input.ActionPress(Core.GameInput.Actions.KbBreak);
        bool broke = false;
        for (int i = 0; i < 40 && !broke; i++)
        {
            await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
            if (session.World.GetBlockId(breakCell) == 0) broke = true;
        }
        Input.ActionRelease(Core.GameInput.Actions.KbBreak);

        float yaw0 = session.Player.Rotation.Y, pitch0 = session.Player.Head.Rotation.X;
        Input.ActionPress(Core.GameInput.Actions.LookLeft);
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        Input.ActionRelease(Core.GameInput.Actions.LookLeft);
        Input.ActionPress(Core.GameInput.Actions.LookUp);
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        Input.ActionRelease(Core.GameInput.Actions.LookUp);
        float yawDelta = Mathf.Abs(session.Player.Rotation.Y - yaw0);
        float pitchDelta = session.Player.Head.Rotation.X - pitch0;

        Input.ActionPress(Core.GameInput.Actions.ToggleCapture);
        await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
        Input.ActionRelease(Core.GameInput.Actions.ToggleCapture);
        bool toggledCaptured = Input.MouseMode == Input.MouseModeEnum.Captured;

        GD.Print($"[RA] controls-test: initFree={initFree} placed={placed} broke={broke} " +
                 $"yawDelta={yawDelta:F2} pitchDelta={pitchDelta:F2} mToggleCaptured={toggledCaptured}");
        GetTree().Quit(0);
    }

    private void RunTeacherTest()
    {
        Core.BlockRegistry.EnsureInit();
        var player = new PlayerSys.Player { Name = "P", InputEnabled = false };
        AddChild(player);

        player.SafeMode = true;
        float hp0 = player.Health;
        player.Damage(50, "hit");
        bool safeBlocks = Mathf.IsEqualApprox(player.Health, hp0);
        player.SafeMode = false;
        player.Damage(10, "hit");
        bool unsafeHurts = player.Health < hp0;

        var d = new Core.SaveData { Name = "__teacher_test__", Seed = 1 };
        d.Signposts.Add((new Vector3(1, 2, 3), "John 3:16"));
        d.Waypoints.Add(("Camp", new Vector3(4, 5, 6)));
        Core.SaveSystem.Save(d);
        var loaded = Core.SaveSystem.Load("__teacher_test__");
        bool signOk = loaded != null && loaded.Signposts.Count == 1
            && loaded.Signposts[0].text == "John 3:16" && loaded.Signposts[0].pos == new Vector3(1, 2, 3);
        bool wpOk = loaded != null && loaded.Waypoints.Count == 1
            && loaded.Waypoints[0].name == "Camp" && loaded.Waypoints[0].pos == new Vector3(4, 5, 6);
        Core.SaveSystem.Delete("__teacher_test__");

        GD.Print($"[RA] teacher-test: safeBlocks={safeBlocks} unsafeHurts={unsafeHurts} signOk={signOk} wpOk={wpOk}");
        QuitSoon();
    }

    private void RunPersistTest()
    {
        Core.BlockRegistry.EnsureInit();
        var d = new Core.SaveData
        {
            Name = "__persist_test__", Seed = 4321, SavedUnix = 123,
            PlayerPos = new Vector3(10, 20, 30), TimeOfDay = 0.6f,
        };
        d.Inventory["stone"] = 12;
        d.Inventory["planks"] = 5;
        d.Edits.Add((1, 2, 3, "gold_block"));
        Core.SaveSystem.Save(d);

        var loaded = Core.SaveSystem.Load("__persist_test__");
        bool basicsOk = loaded != null && loaded.Seed == 4321
            && loaded.PlayerPos == new Vector3(10, 20, 30) && Mathf.IsEqualApprox(loaded.TimeOfDay, 0.6f);
        bool invOk = loaded != null && loaded.Inventory.Count == 2
            && loaded.Inventory.TryGetValue("stone", out int sc) && sc == 12;
        bool editsOk = loaded != null && loaded.Edits.Count == 1 && loaded.Edits[0].block == "gold_block";
        Core.SaveSystem.Delete("__persist_test__");
        bool deleted = !Core.SaveSystem.Exists("__persist_test__");

        var world = new Core.VoxelWorld { Name = "W" };
        AddChild(world);
        var gen = new Core.TerrainGenerator(4321);
        int sh = gen.SurfaceHeight(5, 5);
        ushort gold = Core.BlockRegistry.IdOf("gold_block");
        world.PreloadEdits(new (int, int, int, ushort)[] { (5, sh + 1, 5, gold) });
        var target = new Node3D { Name = "T" };
        AddChild(target);
        target.GlobalPosition = new Vector3(5, sh + 2, 5);
        world.StartStreaming(gen, target, 3, -1, 3);
        world.EnsureSpawnArea(new Vector3(5, 0, 5), 1);
        bool editApplied = world.GetBlockId(5, sh + 1, 5) == gold;

        GD.Print($"[RA] persist-test: basicsOk={basicsOk} invOk={invOk} editsOk={editsOk} " +
                 $"deleted={deleted} editApplied={editApplied}");
        QuitSoon();
    }

    private void RunCraftTest()
    {
        Core.BlockRegistry.EnsureInit();
        var inv = new Core.Inventory();
        ushort log = Core.BlockRegistry.IdOf("oak_log");
        ushort planks = Core.BlockRegistry.IdOf("planks");
        ushort stone = Core.BlockRegistry.IdOf("stone");

        inv.Add(log, 2);
        var planksRecipe = System.Array.Find(Core.CraftBook.All, r => r.Name == "Wood Planks");
        bool crafted = inv.Craft(planksRecipe);
        int logsAfter = inv.Count(log), planksAfter = inv.Count(planks);

        var sandstone = System.Array.Find(Core.CraftBook.All, r => r.Name == "Sandstone");
        bool cantAfford = !inv.CanAfford(sandstone);

        inv.Add(stone, 1);
        bool consumed = inv.TryConsume(stone, 1);
        bool emptyDropped = inv.Count(stone) == 0 && !new System.Collections.Generic.List<ushort>(inv.Order).Contains(stone);

        GD.Print($"[RA] craft-test: crafted={crafted} logsAfter={logsAfter} planks={planksAfter} " +
                 $"cantAfford={cantAfford} consumed={consumed} emptyDropped={emptyDropped}");
        QuitSoon();
    }

    private void RunDayNightTest()
    {
        var env = new Core.EnvironmentController { Name = "Env" };
        AddChild(env);

        env.SetFixedTime(Core.EnvironmentController.Noon);
        bool noonOk = env.Sun.Visible && env.Sun.LightEnergy > 1.0f && !env.Moon.Visible;
        float noonSun = env.Sun.LightEnergy;

        env.SetFixedTime(Core.EnvironmentController.Night);
        bool nightOk = !env.Sun.Visible && env.Moon.Visible && env.Moon.LightEnergy > 0.05f;
        float midnightMoon = env.Moon.LightEnergy;

        env.SetFixedTime(Core.EnvironmentController.Dawn);
        bool dawnOk = env.Sun.Visible && env.Sun.LightEnergy < noonSun;

        GD.Print($"[RA] daynight-test: noonOk={noonOk} nightOk={nightOk} dawnOk={dawnOk} " +
                 $"noonSun={noonSun:F2} midnightMoon={midnightMoon:F2}");
        QuitSoon();
    }

    private async void RunWaterFillTest()
    {
        Core.Scenery.AddDaylight(this);
        var world = new Core.VoxelWorld { Name = "World" };
        AddChild(world);
        var gen = new Core.TerrainGenerator(2024);
        var target = new Node3D { Name = "T" };
        AddChild(target);
        target.GlobalPosition = new Vector3(8, 40, 8);
        world.StartStreaming(gen, target, 3, -1, 3);
        world.EnsureSpawnArea(new Vector3(8, 0, 8), 2);
        await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);

        ushort stone = Core.BlockRegistry.IdOf("stone");
        ushort water = Core.BlockRegistry.IdOf("water");
        int sea = Core.TerrainGenerator.SeaLevel;
        const int x0 = 6, x1 = 10, z0 = 6, z1 = 10;
        for (int x = x0; x <= x1; x++)
        for (int z = z0; z <= z1; z++)
        {
            world.SetBlock(x, 23, z, stone);
            for (int y = 24; y <= 27; y++)
            {
                bool wall = x == x0 || x == x1 || z == z0 || z == z1;
                world.SetBlock(x, y, z, wall ? stone : (ushort)0);
            }
        }
        world.SetBlock(7, sea, 7, water);

        await ToSignal(GetTree().CreateTimer(3.5), SceneTreeTimer.SignalName.Timeout);

        int filled = 0, interior = 0;
        for (int x = 7; x <= 9; x++)
        for (int z = 7; z <= 9; z++)
        for (int y = 24; y <= sea; y++)
        {
            interior++;
            if (world.GetBlockId(x, y, z) == water) filled++;
        }
        bool noLeak = world.GetBlockId(8, sea + 1, 8) != water;
        GD.Print($"[RA] waterfill-test: interior={interior} filled={filled} " +
                 $"allFilled={filled == interior} noLeakAboveSea={noLeak}");
        GetTree().Quit(0);
    }

    private async void RunWaterLaneTest()
    {
        Core.Scenery.AddDaylight(this);
        var world = new Core.VoxelWorld { Name = "World" };
        AddChild(world);
        var gen = new Core.TerrainGenerator(2024);
        var target = new Node3D { Name = "T" };
        AddChild(target);
        target.GlobalPosition = new Vector3(8, 40, 8);
        world.StartStreaming(gen, target, 3, -1, 3);
        world.EnsureSpawnArea(new Vector3(8, 0, 8), 2);
        await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);

        ushort stone = Core.BlockRegistry.IdOf("stone");
        ushort water = Core.BlockRegistry.IdOf("water");
        int sea = Core.TerrainGenerator.SeaLevel;
        const int z = 6;

        for (int x = 4; x <= 11; x++)
        {
            world.SetBlock(x, sea - 1, z, stone);
            world.SetBlock(x, sea, z, stone);
            world.SetBlock(x, sea, z - 1, stone);
            world.SetBlock(x, sea, z + 1, stone);
        }
        world.SetBlock(5, sea, z, water);
        for (int x = 7; x <= 10; x++) world.SetBlock(x, sea, z, 0);

        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        int before = 0;
        for (int x = 6; x <= 10; x++) if (world.GetBlockId(x, sea, z) == water) before++;

        world.SetBlock(6, sea, z, 0);
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

        int after = 0;
        for (int x = 6; x <= 10; x++) if (world.GetBlockId(x, sea, z) == water) after++;
        GD.Print($"[RA] waterlane-test: filledBeforeBreak={before} filledAfterSingleBreak={after} " +
                 $"expected=5 pass={before == 0 && after == 5}");
        GetTree().Quit(0);
    }

    private async void RunWaterDepthTest()
    {
        Core.Scenery.AddDaylight(this);
        var world = new Core.VoxelWorld { Name = "World" };
        AddChild(world);
        var gen = new Core.TerrainGenerator(2024);
        var target = new Node3D { Name = "T" };
        AddChild(target);
        target.GlobalPosition = new Vector3(8, 40, 8);
        world.StartStreaming(gen, target, 3, -1, 3);
        world.EnsureSpawnArea(new Vector3(8, 0, 8), 2);
        await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);

        ushort stone = Core.BlockRegistry.IdOf("stone");
        ushort water = Core.BlockRegistry.IdOf("water");
        int sea = Core.TerrainGenerator.SeaLevel;

        for (int x = 6; x <= 10; x++)
        for (int z = 6; z <= 10; z++)
        for (int y = sea - 3; y <= sea + 1; y++)
            world.SetBlock(x, y, z, stone);
        world.SetBlock(8, sea - 1, 8, 0);
        world.SetBlock(8, sea - 2, 8, 0);
        world.SetBlock(9, sea, 8, 0);
        world.SetBlock(8, sea, 8, water);

        int n1 = world.DrainWaterForTest(1);
        bool step1 = world.GetBlockId(8, sea - 1, 8) == water
                  && world.GetBlockId(8, sea - 2, 8) != water
                  && world.GetBlockId(9, sea, 8) != water;

        int n2 = world.DrainWaterForTest(1);
        bool step2 = world.GetBlockId(8, sea - 2, 8) == water
                  && world.GetBlockId(9, sea, 8) != water;

        int n3 = world.DrainWaterForTest(1);
        bool step3 = world.GetBlockId(9, sea, 8) == water;

        bool pass = n1 == 1 && n2 == 1 && n3 == 1 && step1 && step2 && step3;
        GD.Print($"[RA] waterdepth-test: step1={step1} step2={step2} step3={step3} " +
                 $"placed=({n1},{n2},{n3}) bottomUp={pass}");
        GetTree().Quit(0);
    }

    private async void RunUnderwaterTest()
    {
        var session = new GameSession { Name = "Session" };
        AddChild(session);
        session.Setup(new Vector3(8, 5, 8), creative: true, captureMouse: false);
        ushort water = Core.BlockRegistry.IdOf("water");
        ushort stone = Core.BlockRegistry.IdOf("stone");
        ushort gold = Core.BlockRegistry.IdOf("gold_block");
        ushort grass = Core.BlockRegistry.IdOf("grass");
        ushort brick = Core.BlockRegistry.IdOf("brick");
        for (int x = 0; x <= 16; x++)
        for (int z = 0; z <= 16; z++)
        {
            session.World.SetBlock(x, 0, z, stone, false);
            for (int y = 1; y <= 9; y++)
            {
                bool wall = x == 0 || x == 16 || z == 0 || z == 16;
                session.World.SetBlock(x, y, z, wall ? stone : water, false);
            }
        }

        for (int x = 4; x <= 12; x++)
        for (int y = 1; y <= 7; y++)
        {
            ushort b = ((x + y) % 3 == 0) ? gold : ((x + y) % 3 == 1) ? grass : brick;
            session.World.SetBlock(x, y, 3, b, false);
        }
        session.World.MarkAllDirty();
        session.World.RebuildAllNow();
        session.Env.SetFixedTime(Core.EnvironmentController.Noon);

        session.Player.GlobalPosition = new Vector3(8, 5, 10);
        session.Player.Head.Rotation = new Vector3(-0.12f, 0, 0);
        await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
        bool headUnder = session.Player.HeadUnderwater;
        await Capture("res://_underwater.png", 0.4);
        GD.Print($"[RA] underwater-test: headUnder={headUnder}");
        GetTree().Quit(0);
    }

    private async void RunFxTest()
    {
        var session = new GameSession { Name = "Session" };
        AddChild(session);
        session.Setup(new Vector3(24, 1, 24), creative: true, captureMouse: false);
        Core.WorldGen.FlatGround(session.World, 0, 48, 0, 48, 0);
        ushort dirt = Core.BlockRegistry.IdOf("dirt");
        for (int y = 1; y <= 3; y++) session.World.SetBlock(24, y, 20, dirt, false);
        session.World.MarkAllDirty();
        session.World.RebuildAllNow();
        session.Env.SetFixedTime(Core.EnvironmentController.Noon);
        session.Player.Head.Rotation = new Vector3(-0.12f, 0, 0);

        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);

        Input.ActionPress(Core.GameInput.Actions.KbBreak);
        await ToSignal(GetTree().CreateTimer(0.7), SceneTreeTimer.SignalName.Timeout);
        await Capture("res://_fx_debris.png", 0.0);
        Input.ActionRelease(Core.GameInput.Actions.KbBreak);

        session.Player.SetCreative(false);
        session.Player.Damage(20f, "test");
        await Capture("res://_fx_hurt.png", 0.05);

        GD.Print($"[RA] fx-test: captured debris + hurt flash (health={session.Player.Health:F0})");
        GetTree().Quit(0);
    }

    private async void RunAmbientLifeTest()
    {
        var session = new GameSession { Name = "Session" };
        AddChild(session);

        session.Setup(new Vector3(6, 3, 40), creative: false, captureMouse: false);
        Core.WorldGen.FxShowcase(session.World);
        session.World.MarkAllDirty();
        session.World.RebuildAllNow();
        session.Ambient.SetDensityScale(1.6f);
        session.Player.Head.Rotation = new Vector3(-0.32f, 0, 0);

        session.Env.SetFixedTime(Core.EnvironmentController.Noon);
        await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);
        await Capture("res://_ambientlife_day.png", 0.0);

        session.Env.SetFixedTime(Core.EnvironmentController.Dusk);
        await ToSignal(GetTree().CreateTimer(2.5), SceneTreeTimer.SignalName.Timeout);
        await Capture("res://_ambientlife_dusk.png", 0.0);

        GD.Print("[RA] ambientlife-test: captured day + dusk ambient life");
        GetTree().Quit(0);
    }

    private async void RunAmbientTest()
    {
        var session = new GameSession { Name = "Session" };
        AddChild(session);
        session.Setup(new Vector3(24, 1, 24), creative: true, captureMouse: false);
        Core.WorldGen.FlatGround(session.World, 0, 48, 0, 48, 0);
        session.World.MarkAllDirty();
        session.World.RebuildAllNow();
        session.Player.Head.Rotation = new Vector3(-0.1f, 0, 0);

        session.Env.SetFixedTime(Core.EnvironmentController.Night);
        await ToSignal(GetTree().CreateTimer(2.5), SceneTreeTimer.SignalName.Timeout);
        await Capture("res://_ambient_night.png", 0.0);

        session.Env.SetFixedTime(Core.EnvironmentController.Noon);
        await ToSignal(GetTree().CreateTimer(2.5), SceneTreeTimer.SignalName.Timeout);
        await Capture("res://_ambient_day.png", 0.0);

        GD.Print("[RA] ambient-test: captured night fireflies + day motes");
        GetTree().Quit(0);
    }

    private async void RunSwimTest()
    {
        Core.Scenery.AddDaylight(this);
        var world = new Core.VoxelWorld { Name = "World" };
        AddChild(world);
        ushort stone = Core.BlockRegistry.IdOf("stone");
        ushort water = Core.BlockRegistry.IdOf("water");
        for (int x = 4; x <= 10; x++)
        for (int z = 4; z <= 10; z++)
        for (int y = -11; y <= 1; y++)
            world.SetBlock(x, y, z, stone, false);
        for (int x = 5; x <= 9; x++)
        for (int z = 5; z <= 9; z++)
        for (int y = -10; y <= 0; y++)
            world.SetBlock(x, y, z, water, false);
        world.MarkAllDirty();
        world.RebuildAllNow();

        var player = new PlayerSys.Player { Name = "Player", World = world, InputEnabled = true };
        AddChild(player);
        player.GlobalPosition = new Vector3(7, -1, 7);
        player.Camera.Current = true;
        await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
        bool inWater = player.InWater, headUnder = player.HeadUnderwater;
        float yStart = player.GlobalPosition.Y;

        Input.ActionPress(Core.GameInput.Actions.Crouch);
        await ToSignal(GetTree().CreateTimer(0.9), SceneTreeTimer.SignalName.Timeout);
        float yDive = player.GlobalPosition.Y;
        Input.ActionRelease(Core.GameInput.Actions.Crouch);

        Input.ActionPress(Core.GameInput.Actions.Jump);
        await ToSignal(GetTree().CreateTimer(0.9), SceneTreeTimer.SignalName.Timeout);
        float yRise = player.GlobalPosition.Y;
        Input.ActionRelease(Core.GameInput.Actions.Jump);

        bool dived = yDive < yStart - 0.5f;
        bool rose = yRise > yDive + 0.5f;
        GD.Print($"[RA] swim-test: inWater={inWater} headUnder={headUnder} " +
                 $"yStart={yStart:F1} yDive={yDive:F1} yRise={yRise:F1} dived={dived} rose={rose}");
        GetTree().Quit(0);
    }

    private async void RunMiningTest()
    {
        var session = new GameSession { Name = "Session" };
        AddChild(session);
        session.Setup(new Vector3(24, 1, 24), creative: true, captureMouse: false);
        Core.WorldGen.FlatGround(session.World, 0, 48, 0, 48, 0);
        for (int y = 1; y <= 3; y++)
            session.World.SetBlock(24, y, 20, Core.BlockRegistry.IdOf("stone"), false);
        session.World.MarkAllDirty();
        session.World.RebuildAllNow();
        session.Env.SetFixedTime(Core.EnvironmentController.Noon);
        session.Player.Head.Rotation = new Vector3(-0.12f, 0, 0);

        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);

        Input.ActionPress(Core.GameInput.Actions.KbBreak);
        await ToSignal(GetTree().CreateTimer(0.95), SceneTreeTimer.SignalName.Timeout);
        await Capture("res://_mining.png", 0.0);
        Input.ActionRelease(Core.GameInput.Actions.KbBreak);
        GD.Print("[RA] mining-test: captured mid-mine");
        GetTree().Quit(0);
    }

    private async void RunStrataTest()
    {
        Core.Scenery.AddDaylight(this);
        var world = new Core.VoxelWorld { Name = "World" };
        AddChild(world);
        var gen = new Core.TerrainGenerator(1337);
        var buf = new ushort[Core.Chunk.Volume];
        for (int cx = 0; cx < 3; cx++)
        for (int cy = -1; cy <= 3; cy++)
        for (int cz = 0; cz < 3; cz++)
        {
            gen.Generate(new Vector3I(cx, cy, cz), buf);
            world.LoadChunk(new Vector3I(cx, cy, cz), buf);
        }
        world.RebuildAllNow();

        for (int x = 24; x < 48; x++)
        for (int y = -16; y < 48; y++)
        for (int z = 0; z < 48; z++)
            world.SetBlock(x, y, z, 0, false);
        world.MarkAllDirty();
        world.RebuildAllNow();

        var cam = new Camera3D { Name = "Cam", Fov = 60f };
        AddChild(cam);
        cam.Current = true;
        cam.Position = new Vector3(31, 9, 24);
        cam.LookAt(new Vector3(23, 5, 24), Vector3.Up);
        await Capture("res://_strata.png", 0.8);
        GD.Print("[RA] strata-test: captured cross-section");
        GetTree().Quit(0);
    }

    private async void RunWaterTest()
    {
        var session = new GameSession { Name = "Session" };
        AddChild(session);
        session.Setup(new Vector3(24, 6, 44), creative: true, captureMouse: false);
        ushort sand = Core.BlockRegistry.IdOf("sand");
        ushort water = Core.BlockRegistry.IdOf("water");
        for (int x = -8; x <= 56; x++)
        for (int z = -8; z <= 56; z++)
        {
            session.World.SetBlock(x, -2, z, sand, false);
            session.World.SetBlock(x, -1, z, water, false);
            session.World.SetBlock(x, 0, z, water, false);
        }
        session.World.MarkAllDirty();
        session.World.RebuildAllNow();
        session.Env.SetFixedTime(Core.EnvironmentController.Noon);
        session.Player.Head.Rotation = new Vector3(-0.16f, 0, 0);
        await Capture("res://_water.png", 0.8);
        GetTree().Quit(0);
    }

    private async void RunWaterStepTest()
    {
        var session = new GameSession { Name = "Session" };
        AddChild(session);
        session.Setup(new Vector3(7, 6, 1), creative: true, captureMouse: false);
        ushort sand = Core.BlockRegistry.IdOf("sand");
        ushort water = Core.BlockRegistry.IdOf("water");
        const int W = 14, N = 10;

        for (int x = -2; x <= W + 2; x++)
        for (int z = -2; z <= N + 3; z++)
        for (int y = -N - 1; y <= 0; y++)
            session.World.SetBlock(x, y, z, sand, false);

        for (int i = 0; i < N; i++)
        {
            int y = -i;
            for (int x = 0; x <= W; x++)
            for (int z = i + 1; z <= N + 3; z++)
                for (int yy = 0; yy >= y; yy--)
                    session.World.SetBlock(x, yy, z, 0, false);
            for (int x = 0; x <= W; x++)
                session.World.SetBlock(x, y, i + 1, water, false);
        }
        session.World.MarkAllDirty();
        session.World.RebuildAllNow();
        session.Env.SetFixedTime(Core.EnvironmentController.Noon);

        session.Player.GlobalPosition = new Vector3(7, 4, -1);
        session.Player.Rotation = new Vector3(0, Mathf.Pi, 0);
        session.Player.Head.Rotation = new Vector3(-0.6f, 0, 0);
        await Capture("res://_waterstep.png", 0.8);
        GetTree().Quit(0);
    }

    private async void RunSkyTest()
    {
        var session = new GameSession { Name = "Session" };
        AddChild(session);
        session.Setup(new Vector3(24, 8, 24), creative: true, captureMouse: false);
        Core.WorldGen.FlatGround(session.World, -8, 56, -8, 56, 0);
        for (int i = 0; i < 6; i++)
            Core.WorldGen.Tree(session.World, new Vector3I(12 + i * 6, 1, 30));
        session.World.MarkAllDirty();
        session.World.RebuildAllNow();

        session.Player.Rotation = new Vector3(0, 0, 0);
        session.Player.Head.Rotation = new Vector3(0.55f, 0, 0);
        session.Env.SetFixedTime(Core.EnvironmentController.Morning);
        await Capture("res://_sky_sun.png", 0.6);

        session.Player.Head.Rotation = new Vector3(0.95f, 0, 0);
        session.Env.SetFixedTime(Core.EnvironmentController.Noon);
        await Capture("res://_sky_clouds.png", 0.6);

        session.Player.Rotation = new Vector3(0, Mathf.Pi, 0);
        session.Player.Head.Rotation = new Vector3(0.55f, 0, 0);
        session.Env.SetFixedTime(0.85f);
        await Capture("res://_sky_moon.png", 0.6);

        session.Player.Head.Rotation = new Vector3(1.1f, 0, 0);
        session.Env.SetFixedTime(Core.EnvironmentController.Night);
        session.Env.SkyMaterial?.SetShaderParameter("cloud_coverage", -2.0f);
        await Capture("res://_sky_stars.png", 0.6);

        GD.Print("[RA] sky-test: done");
        GetTree().Quit(0);
    }

    private void RunBiomeTest()
    {
        var gen = new Core.TerrainGenerator(1337);
        var counts = new System.Collections.Generic.Dictionary<Core.Biome, int>();
        const int step = 8, range = 2400;
        for (int x = -range; x <= range; x += step)
        for (int z = -range; z <= range; z += step)
        {
            var b = gen.BiomeAt(x, z, gen.SurfaceHeight(x, z));
            counts.TryGetValue(b, out int c);
            counts[b] = c + 1;
        }

        ushort log = Core.BlockRegistry.IdOf("oak_log");
        ushort leaves = Core.BlockRegistry.IdOf("leaves");
        int logCount = 0, leafCount = 0;
        var buf = new ushort[Core.Chunk.Volume];
        for (int cx = 0; cx < 40; cx++)
        for (int cy = 0; cy <= 3; cy++)
        {
            gen.Generate(new Vector3I(cx, cy, 0), buf);
            foreach (var id in buf)
            {
                if (id == log) logCount++;
                else if (id == leaves) leafCount++;
            }
        }

        var summary = new System.Text.StringBuilder();
        foreach (var kv in counts) summary.Append($"{kv.Key}:{kv.Value} ");
        GD.Print($"[RA] biome-test: distinct={counts.Count} logs={logCount} leaves={leafCount} | {summary}");
        QuitSoon();
    }

    private void RunUndergroundTest()
    {
        var gen = new Core.TerrainGenerator(1337);
        ushort stone = Core.BlockRegistry.IdOf("stone");
        ushort bedrock = Core.BlockRegistry.IdOf("bedrock");
        ushort coal = Core.BlockRegistry.IdOf("coal_ore");
        ushort copper = Core.BlockRegistry.IdOf("copper_ore");
        ushort iron = Core.BlockRegistry.IdOf("iron_ore");
        ushort gold = Core.BlockRegistry.IdOf("gold_ore");

        int stoneN = 0, bedrockN = 0, oreN = 0, caveN = 0, bottomCells = 0, bottomBedrock = 0;
        var buf = new ushort[Core.Chunk.Volume];
        for (int cx = 0; cx < 16; cx++)
        for (int cz = 0; cz < 3; cz++)
        for (int cy = -1; cy <= 3; cy++)
        {
            gen.Generate(new Vector3I(cx, cy, cz), buf);
            for (int ly = 0; ly < Core.Chunk.Size; ly++)
            for (int lz = 0; lz < Core.Chunk.Size; lz++)
            for (int lx = 0; lx < Core.Chunk.Size; lx++)
            {
                ushort id = buf[Core.Chunk.Index(lx, ly, lz)];
                int wy = cy * Core.Chunk.Size + ly;
                if (id == stone) stoneN++;
                else if (id == bedrock) bedrockN++;
                else if (id == coal || id == copper || id == iron || id == gold) oreN++;
                else if (id == 0 && wy >= -13 && wy <= 8) caveN++;
                if (wy == -16) { bottomCells++; if (id == bedrock) bottomBedrock++; }
            }
        }

        var n = new Core.ValueNoise2D(99);
        bool det = Mathf.IsEqualApprox(n.Noise3(1.5f, 2.5f, 3.5f), n.Noise3(1.5f, 2.5f, 3.5f))
            && n.Fractal3(4f, 5f, 6f, 3) >= 0f && n.Fractal3(4f, 5f, 6f, 3) <= 1f;

        GD.Print($"[RA] underground-test: stone={stoneN} ores={oreN} bedrock={bedrockN} caves={caveN} " +
                 $"bottomAllBedrock={bottomBedrock == bottomCells && bottomCells > 0} noise3Det={det}");
        QuitSoon();
    }

    private void RunGreedyTest()
    {
        var world = new Core.VoxelWorld { Name = "World" };
        AddChild(world);
        ushort grass = Core.BlockRegistry.IdOf("grass");
        for (int x = 0; x < Core.Chunk.Size; x++)
        for (int z = 0; z < Core.Chunk.Size; z++)
            world.SetBlock(x, 0, z, grass, false);

        var chunk = world.Chunks[new Vector3I(0, 0, 0)];
        var snap = Core.ChunkMesher.Capture(world, chunk);
        var data = Core.ChunkMesher.BuildData(snap);
        int verts = data.VertexCount;
        int tris = (data.Opaque.Indices.Count + data.Water.Indices.Count) / 3;

        bool merged = verts <= 32;
        GD.Print($"[RA] greedy-test: verts={verts} tris={tris} collisionVerts={data.Collision.Count} merged={merged}");
        QuitSoon();
    }

    private async void RunStreamTest()
    {
        Core.Scenery.AddDaylight(this);
        var world = new Core.VoxelWorld { Name = "World" };
        AddChild(world);

        var gen = new Core.TerrainGenerator(1337);
        var target = new Node3D { Name = "Target" };
        AddChild(target);
        int surf0 = gen.SurfaceHeight(8, 8);
        target.GlobalPosition = new Vector3(8, surf0 + 2, 8);
        world.StartStreaming(gen, target, renderDistance: 4, minChunkY: -1, maxChunkY: 3);

        await ToSignal(GetTree().CreateTimer(2.5), SceneTreeTimer.SignalName.Timeout);
        int genNear = world.GeneratedChunkCount;
        int chunksNear = world.ChunkCount;
        int sh = gen.SurfaceHeight(8, 8);
        bool groundExists = world.GetBlockId(8, sh, 8) != 0;
        bool airAbove = world.GetBlockId(8, sh + 6, 8) == 0;
        bool holdClear = !world.StreamingHold(new Vector3(8, sh + 1, 8));
        int meshedNear = 0;
        foreach (var kv in world.Chunks) if (kv.Value.Meshed) meshedNear++;
        var originChunk = Core.VoxelWorld.ChunkCoord(8, sh, 8);

        int fx = 8 + 16 * 48;
        target.GlobalPosition = new Vector3(fx, gen.SurfaceHeight(fx, 8) + 2, 8);
        await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);
        bool originUnloaded = !world.Chunks.ContainsKey(originChunk);
        bool farGround = world.GetBlockId(fx, gen.SurfaceHeight(fx, 8), 8) != 0;

        GD.Print($"[RA] stream-test: genNear={genNear} chunksNear={chunksNear} meshedNear={meshedNear} " +
                 $"groundExists={groundExists} airAbove={airAbove} holdClear={holdClear} " +
                 $"originUnloaded={originUnloaded} farGround={farGround}");
        GetTree().Quit(0);
    }

    private void RunWorldTest()
    {
        Core.Scenery.AddDaylight(this);

        var world = new Core.VoxelWorld { Name = "World" };
        AddChild(world);
        Core.WorldGen.Showcase(world);
        world.MarkAllDirty();
        world.RebuildAllNow();
        GD.Print($"[RA] test-world: chunks={world.ChunkCount}");

        var cam = new Camera3D { Name = "TestCam", Fov = 70f };
        AddChild(cam);
        CaptureSequence(cam);
    }

    private async void CaptureSequence(Camera3D cam)
    {
        cam.Current = true;

        cam.Position = new Vector3(26, 16, 40);
        cam.LookAt(new Vector3(24, 2, 6), Vector3.Up);
        await Capture("res://_world_test.png", 1.0);

        cam.Position = new Vector3(10, 5, 12);
        cam.LookAt(new Vector3(18, 2.5f, 6), Vector3.Up);
        await Capture("res://_world_close.png", 0.4);
        GetTree().Quit(0);
    }

    private async System.Threading.Tasks.Task Capture(string path, double afterSeconds)
    {
        await ToSignal(GetTree().CreateTimer(afterSeconds), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var img = GetViewport().GetTexture().GetImage();
        Error e = img.SavePng(path);
        GD.Print($"[RA] screenshot {path} -> {e}");
    }
}
