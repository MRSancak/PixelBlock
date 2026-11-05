using System.Collections.Generic;
using Godot;
using PixelBlock.Combat;
using PixelBlock.Core;
using PixelBlock.Dialogue;
using PixelBlock.NpcSys;
using PixelBlock.PlayerSys;
using PixelBlock.UI;

namespace PixelBlock;

public partial class GameSession : Node3D
{
    public enum Mode { Build, Adventure }

    public VoxelWorld World { get; private set; }
    public EnvironmentController Env { get; private set; }
    public Player Player { get; private set; }
    public GameHud Hud { get; private set; }
    public BlockInteractor Interactor { get; private set; }
    public BuildEditor Editor { get; private set; }
    public WeaponController Weapons { get; private set; }
    public FireController Fire { get; private set; }
    public AmbientLifeDirector Ambient { get; private set; }
    private HeldItem _held;
    public DialogueBox Dialogue { get; private set; }
    public Narrator Narrator { get; private set; }
    public PauseMenu Pause { get; private set; }
    public Mode CurrentMode { get; private set; }
    public bool InDialogue { get; private set; }

    public FreeCam FreeCam { get; private set; }
    private bool _freeCamOn;
    private Input.MouseModeEnum _preFreeCamMouse = Input.MouseModeEnum.Visible;
    public float InteractRange = 3.8f;
    public System.Action ReturnToMenuRequested;
    private bool _wasFocused = true;

    public string LessonId { get; set; }

    public Quests.QuestTracker Quest { get; private set; }

    [Signal] public delegate void NpcTalkedEventHandler(string npcName);
    [Signal] public delegate void EnemyDefeatedEventHandler(string enemyTypeName);
    [Signal] public delegate void PlayerReachedEventHandler(string triggerId);
    [Signal] public delegate void QuestCompletedEventHandler(string lessonId);

    public Inventory Inventory { get; private set; }
    public bool Survival { get; private set; }
    private CraftingMenu _craft;
    private ContainerMenu _container;
    private bool _crafting;
    private ChatBox _chat;
    private ChatCommands _cmds;
    private bool _chatOpen;
    private ulong _chatClosedFrame;
    private Input.MouseModeEnum _preChatMouse = Input.MouseModeEnum.Visible;
    private Input.MouseModeEnum _preCraftMouse = Input.MouseModeEnum.Visible;

    private TeacherPanel _teacher;
    private bool _teacherOpen;
    private bool _presentMode;
    private Input.MouseModeEnum _preTeacherMouse = Input.MouseModeEnum.Visible;
    private readonly List<(string name, Vector3 pos)> _waypoints = new();
    private readonly List<PixelBlock.World.Signpost> _signposts = new();

    public static readonly string[] DefaultPalette =
    {
        "grass", "dirt", "stone", "cobblestone", "planks", "oak_log", "mud_brick", "leaves", "lamp",
    };

    public void Setup(Vector3 spawn, bool creative, IEnumerable<string> palette = null, bool? captureMouse = null)
    {
        Env = Scenery.AddDaylight(this);

        World = new VoxelWorld { Name = "World" };
        AddChild(World);

        Player = new Player { Name = "Player", World = World };
        AddChild(Player);
        Player.SetCreative(creative);
        Player.GlobalPosition = spawn;
        Env.SetWeatherFollow(Player);

        FreeCam = new FreeCam { Name = "FreeCam" };
        AddChild(FreeCam);

        Hud = new GameHud { Name = "Hud" };
        AddChild(Hud);

        var ids = new List<ushort>();
        foreach (string n in palette ?? DefaultPalette)
            if (BlockRegistry.TryId(n, out ushort id)) ids.Add(id);
        Hud.Configure(World.Textures, ids);

        Interactor = new BlockInteractor { Name = "Interactor", World = World, Player = Player, Hotbar = Hud.Hotbar };
        AddChild(Interactor);

        Editor = new BuildEditor { Name = "Editor", World = World, Interactor = Interactor, Player = Player, Hotbar = Hud.Hotbar, Hud = Hud };
        AddChild(Editor);

        Weapons = new WeaponController { Name = "Weapons", Player = Player, ProjectileParent = this };
        Player.AddChild(Weapons);
        Weapons.AttachViewmodel();
        Weapons.Equip(Weapon.Sling());
        Weapons.WeaponChanged += Hud.SetWeapon;

        _held = new HeldItem { Name = "HeldItem", Player = Player, World = World };
        Player.AddChild(_held);
        _held.AttachViewmodel();
        Hud.Hotbar.SelectionChanged += _held.OnSelectionChanged;
        _held.OnSelectionChanged(Hud.Hotbar.SelectedBlockId);

        Narrator = new Narrator { Name = "Narrator" };
        AddChild(Narrator);
        Dialogue = new DialogueBox { Name = "DialogueBox" };
        AddChild(Dialogue);
        Dialogue.Finished += OnDialogueFinished;

        Pause = new PauseMenu { Name = "PauseMenu" };
        Pause.CanPause = () => !InDialogue && !_crafting && !ChatBusy && !_teacherOpen && !_presentMode && !_reading && !_freeCamOn;
        Pause.OnReturnToMenu = () => ReturnToMenuRequested?.Invoke();
        AddChild(Pause);

        Player.HungerChanged += Hud.SetHunger;
        Player.HealthChanged += Hud.SetHealth;
        Player.AirChanged += Hud.SetAir;
        Player.HealthChanged += (cur, max) => Hud.SetLowHealth(max > 0 ? cur / max : 0f);

        Hud.SetHealth(Player.Health, Player.MaxHealth);
        Hud.SetAir(Player.Air, Player.MaxAir);

        var screenFx = new ScreenFx { Name = "ScreenFx", Camera = Player.Camera };
        Player.AddChild(screenFx);
        Fx.OnShake = screenFx.AddTrauma;
        Fx.OnFlash = Hud.Flash;
        Player.Hurt += OnPlayerHurt;

        Fire = new FireController { Name = "FireController", Player = Player, Env = Env, World = World };
        AddChild(Fire);

        Ambient = new AmbientLifeDirector { Name = "AmbientLife", Player = Player, Env = Env, World = World };
        AddChild(Ambient);

        SetMode(creative ? Mode.Build : Mode.Adventure);

        Player.Camera.Current = true;
        bool grab = captureMouse ?? (Settings.CaptureMode == Settings.MouseCapture.Always);
        Input.MouseMode = grab ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;

        EnableTeacherTools();

        _chat = new ChatBox { Name = "Chat" };
        AddChild(_chat);
        _cmds = new ChatCommands(this, _chat);
        _chat.OnSubmit = _cmds.Submit;
        _chat.Complete = _cmds.Complete;
        _chat.OnClosed = OnChatClosed;
    }

    private bool ChatBusy => _chatOpen || Engine.GetProcessFrames() - _chatClosedFrame <= 1;

    private void OpenChat(string prefill)
    {
        if (_chat == null || _chatOpen) return;
        _chatOpen = true;
        _preChatMouse = Input.MouseMode;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Player.InputEnabled = false;
        Weapons.SetEnabled(false);
        Interactor.CanEdit = false;
        Hud.SetInteractPrompt("");
        Hud.SetMouseHint("");
        _chat.Open(prefill);
    }

    private void OnChatClosed()
    {
        if (!_chatOpen) return;
        _chatOpen = false;
        _chatClosedFrame = Engine.GetProcessFrames();
        Player.InputEnabled = true;
        Input.MouseMode = _preChatMouse;
        Player.SuppressActionsFor(0.2f);
        SetMode(CurrentMode);
    }

    public void SetCreativeMode(bool on)
    {
        Player.SetCreative(on);
        if (_crafting) _craft?.Open();
    }

    public void DropStack(ushort id, int n)
    {
        if (Player == null || id == 0 || n <= 0) return;
        Vector3 fwd = -Player.GlobalTransform.Basis.Z;
        Vector3 pos = Player.GlobalPosition + new Vector3(0f, 1.3f, 0f) + fwd * 0.6f;
        ItemDrop.Spawn(this, World, Player, Inventory, id, n, pos, fwd * 3f + new Vector3(0f, 2.5f, 0f));
    }

    private void CreativePick(ushort id, int n)
    {
        if (Inventory == null) return;
        int left = Inventory.Add(id, n);
        if (left >= n)
        {
            // envanter dolu: seçili hızlı erişim kutusunun yerine koy
            Inventory.SetSlot(Hud.Hotbar.SelectedSlot, id, n);
        }
        AudioManager.Play("select", 1.1f, -6f);
    }

    private void EnableTeacherTools()
    {
        Hud.SetCompassEnabled(true);
        _teacher = new TeacherPanel { Name = "TeacherPanel" };
        AddChild(_teacher);
        _teacher.GetSafe = () => Player.SafeMode;
        _teacher.SetSafe = on =>
        {
            Player.SafeMode = on;
            Hud.ShowBanner(on ? "Güvenli mod AÇIK — hasar yok" : "Güvenli mod KAPALI", 1.6f);
        };
        _teacher.OnPresent = EnterPresentMode;
        _teacher.OnPlaceSignpost = PlaceSignpostAtLook;
        _teacher.OnAddWaypoint = AddWaypointHere;
        _teacher.GetWaypoints = () => _waypoints;
        _teacher.OnTeleport = TeleportToWaypoint;
        _teacher.OnClose = CloseTeacher;
    }

    private void ToggleTeacher()
    {
        if (_teacherOpen) CloseTeacher();
        else OpenTeacher();
    }

    private void OpenTeacher()
    {
        _teacherOpen = true;
        _preTeacherMouse = Input.MouseMode;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Player.InputEnabled = false;
        Hud.SetMouseHint("");
        _teacher.Open();
    }

    private void CloseTeacher()
    {
        _teacherOpen = false;
        _teacher.Visible = false;
        Input.MouseMode = _preTeacherMouse;
        Player.InputEnabled = true;
    }

    private void EnterPresentMode()
    {
        _presentMode = true;
        Hud.Visible = false;

        Input.MouseMode = Input.MouseModeEnum.Visible;
        Player.SuppressActionsFor(0.2f);
    }

    private void ExitPresentMode()
    {
        _presentMode = false;
        Hud.Visible = true;
    }

    private void AddWaypointHere()
    {
        _waypoints.Add(($"Waypoint {_waypoints.Count + 1}", Player.GlobalPosition));
        Hud.ShowBanner("Nokta işaretlendi", 1.4f);
    }

    private void TeleportToWaypoint(int index)
    {
        if (index < 0 || index >= _waypoints.Count) return;
        Player.Velocity = Vector3.Zero;
        Player.GlobalPosition = _waypoints[index].pos + Vector3.Up * 0.2f;
        Hud.ShowBanner($"Teleported to {_waypoints[index].name}", 1.6f);
    }

    private void PlaceSignpostAtLook(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) text = "…";

        Camera3D cam = Player.Camera;
        var hit = Core.VoxelRay.Cast(World, cam.GlobalPosition, -cam.GlobalTransform.Basis.Z, 8f);
        Vector3 pos = hit.Ok ? (Vector3)hit.Prev + new Vector3(0.5f, 0f, 0.5f)
                             : Player.GlobalPosition - Player.GlobalTransform.Basis.Z * 2f;
        var sign = PixelBlock.World.Signpost.Create(pos, text);
        World.AddChild(sign);
        _signposts.Add(sign);
        Hud.ShowBanner("Tabela yerleştirildi", 1.6f);
    }

    public void RestoreTeacherState(
        IEnumerable<(Vector3 pos, string text)> signposts,
        IEnumerable<(string name, Vector3 pos)> waypoints)
    {
        foreach (var (pos, text) in signposts)
        {
            var s = PixelBlock.World.Signpost.Create(pos, text);
            World.AddChild(s);
            _signposts.Add(s);
        }
        _waypoints.AddRange(waypoints);
    }

    public IReadOnlyList<(string name, Vector3 pos)> Waypoints => _waypoints;
    public IEnumerable<(Vector3 pos, string text)> Signposts()
    {
        foreach (var s in _signposts) yield return (s.GlobalPosition, s.Text);
    }

    private async void TakeScreenshot()
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("user://screenshots"));
        var img = GetViewport().GetTexture().GetImage();
        string path = $"user://screenshots/shot_{Time.GetTicksMsec()}.png";
        img.SavePng(path);
        Hud.ShowBanner($"Saved {path}", 2f);
    }

    private void ToggleFreeCam()
    {
        if (_freeCamOn) ExitFreeCam();
        else EnterFreeCam();
    }

    private void EnterFreeCam()
    {
        _freeCamOn = true;
        FreeCam.GlobalTransform = Player.Camera.GlobalTransform;
        FreeCam.Fov = 75f;
        FreeCam.SetActive(true);
        Player.InputEnabled = false;
        _preFreeCamMouse = Input.MouseMode;
        Input.MouseMode = Input.MouseModeEnum.Captured;
        Hud.SetInteractPrompt("");
        Hud.SetMouseHint("");
        Hud.ShowBanner("Free camera — WASD fly · Space/Ctrl up/down · Shift boost · wheel speed · T precise steps · P to exit", 4f);
    }

    private void ExitFreeCam()
    {
        _freeCamOn = false;
        FreeCam.SetActive(false);
        Player.Camera.Current = true;
        Player.InputEnabled = true;
        Input.MouseMode = _preFreeCamMouse;
        Hud.SetFreeCam("");
    }

    public void EnableSurvival(params (string block, int count)[] startKit)
    {
        Inventory = new Inventory();
        Survival = true;
        foreach (var (block, count) in startKit)
            Inventory.Add(BlockRegistry.IdOf(block), count);

        Interactor.Inventory = Inventory;
        Hud.Hotbar.BindInventory(World.Textures, Inventory);

        Farming.FarmManager.OnMessage = m => Hud.ShowBanner(m, 3f);
        Player.HungerEnabled = true;
        Player.SetHunger(Player.MaxHunger);

        _craft = new CraftingMenu { Name = "Crafting" };
        _craft.Setup(Inventory, World.Textures);
        AddChild(_craft);
        _craft.OnClose = CloseCrafting;
        _craft.TableNearby = TableNearby;
        _craft.HeatNearby = HeatNearby;
        _craft.IsCreative = () => Player != null && Player.Creative;
        _craft.SelectedHotbar = () => Hud.Hotbar.SelectedSlot;
        _craft.OnDropItem = DropFromSlot;
        _craft.OnCreativePick = CreativePick;
        _craft.GetArmor = () => Player.Armor;
        _craft.OnUnequip = slot =>
        {
            ushort old = Player.SetArmor(slot, 0);
            if (old == 0) return;
            if (Inventory.Add(old, 1) > 0) DropStack(old, 1);
            AudioManager.Play("select", 0.8f, -4f);
        };
        _craft.OnEquip = (invSlot) =>
        {
            if (InvSlot.DragInv != null && InvSlot.DragInv != Inventory) return;
            ushort id = Inventory.SlotId(invSlot);
            var b = BlockRegistry.Get(id);
            if (id == 0 || b.ArmorSlot <= 0) return;
            Inventory.TakeFromSlot(invSlot, 1);
            ushort old = Player.SetArmor(b.ArmorSlot - 1, id);
            if (old != 0 && Inventory.Add(old, 1) > 0) DropStack(old, 1);
            AudioManager.Play("select", 0.7f, -3f);
        };
        _container = new ContainerMenu { Name = "ContainerMenu" };
        _container.Setup(Inventory, World.Textures);
        AddChild(_container);
        _container.OnClose = CloseCrafting;
        _container.OnDropItem = DropFromSlot;
        Interactor.OpenContainer = OpenContainer;

        Player.ArmorChanged += () => { Player.Avatar?.SetArmor(Player.Armor); _craft.RefreshArmor(); };

        SetMode(Mode.Build);
    }

    private bool TableNearby()
    {
        if (World == null || Player == null) return false;
        ushort table = BlockRegistry.IdOf("crafting_table");
        if (table == 0) return false;
        Vector3 p = Player.GlobalPosition;
        int px = Mathf.FloorToInt(p.X), py = Mathf.FloorToInt(p.Y), pz = Mathf.FloorToInt(p.Z);
        for (int dx = -5; dx <= 5; dx++)
        for (int dy = -3; dy <= 4; dy++)
        for (int dz = -5; dz <= 5; dz++)
            if (World.GetBlockId(px + dx, py + dy, pz + dz) == table) return true;
        return false;
    }

    private bool HeatNearby()
    {
        if (World == null || Player == null) return false;
        ushort camp = BlockRegistry.IdOf("campfire");
        ushort altar = BlockRegistry.IdOf("altar_fire");
        ushort furn = BlockRegistry.IdOf("furnace");
        Vector3 p = Player.GlobalPosition;
        int px = Mathf.FloorToInt(p.X), py = Mathf.FloorToInt(p.Y), pz = Mathf.FloorToInt(p.Z);
        for (int dx = -5; dx <= 5; dx++)
        for (int dy = -3; dy <= 4; dy++)
        for (int dz = -5; dz <= 5; dz++)
        {
            ushort id = World.GetBlockId(px + dx, py + dy, pz + dz);
            if (id != 0 && (id == camp || id == altar || id == furn)) return true;
        }
        return false;
    }

    public void DropFromSlot(int slot, int count)
    {
        if (Inventory == null || Player == null) return;
        ushort id = Inventory.SlotId(slot);
        int n = Mathf.Min(count, Inventory.SlotCount(slot));
        if (id == 0 || n <= 0) return;
        Inventory.TakeFromSlot(slot, n);
        Vector3 fwd = -Player.GlobalTransform.Basis.Z;
        Vector3 pos = Player.GlobalPosition + new Vector3(0f, 1.3f, 0f) + fwd * 0.6f;
        ItemDrop.Spawn(this, World, Player, Inventory, id, n, pos, fwd * 3f + new Vector3(0f, 2.5f, 0f));
        AudioManager.Play("place", 0.8f, -6f);
    }

    public SaveData CaptureSave(string name, long savedUnix)
    {
        var d = new SaveData
        {
            Name = name,
            Seed = World.Seed,
            SavedUnix = savedUnix,
            PlayerPos = Player.GlobalPosition,
            TimeOfDay = Env?.TimeOfDay ?? 0.4f,
        };
        if (Inventory != null)
            foreach (ushort id in Inventory.Order)
                d.Inventory[BlockRegistry.Get(id).Name] = Inventory.Count(id);
        if (Inventory != null)
            for (int i = 0; i < Inventory.TotalSlots; i++)
            {
                ushort sid = Inventory.SlotId(i);
                d.Slots.Add((sid == 0 ? "" : BlockRegistry.Get(sid).Name, Inventory.SlotCount(i)));
            }
        if (Survival) d.Hunger = Player.Hunger;
        if (PixelBlock.Core.Containers.Instance != null) d.Containers.AddRange(PixelBlock.Core.Containers.Instance.Export());
        for (int i = 0; i < 4; i++) d.Armor[i] = Player.Armor[i] == 0 ? "" : BlockRegistry.Get(Player.Armor[i]).Name;
        if (SeasonClock.Instance != null) d.DayCount = SeasonClock.Instance.DayCount;
        if (PixelBlock.Network.NetworkManager.Instance != null)
            foreach (var kv in PixelBlock.Network.NetworkManager.Instance.ExportPeerInv()) d.PeerInv[kv.Key] = kv.Value;
        if (PixelBlock.Farming.FarmManager.Instance != null) d.Crops.AddRange(PixelBlock.Farming.FarmManager.Instance.Export());
        if (PixelBlock.Animals.AnimalDirector.Instance != null) d.Animals.AddRange(PixelBlock.Animals.AnimalDirector.Instance.Export());
        foreach (var (pos, id) in World.AllEdits())
            d.Edits.Add((pos.X, pos.Y, pos.Z, BlockRegistry.Get(id).Name));
        foreach (var s in _signposts)
            d.Signposts.Add((s.GlobalPosition, s.Text));
        d.Waypoints.AddRange(_waypoints);
        return d;
    }

    private void ToggleCrafting()
    {
        if (_crafting) CloseCrafting();
        else OpenCrafting();
    }

    private void OpenCrafting()
    {
        _crafting = true;
        _preCraftMouse = Input.MouseMode;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Player.InputEnabled = false;
        Interactor.CanEdit = false;
        Hud.SetMouseHint("");
        _craft.Open();
    }

    private void OpenContainer(Vector3I cell, string kind)
    {
        if (_crafting || Containers.Instance == null || _container == null) return;
        var data = Containers.Instance.GetOrCreate(cell, kind);
        _crafting = true;
        _preCraftMouse = Input.MouseMode;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Player.InputEnabled = false;
        Interactor.CanEdit = false;
        Hud.SetMouseHint("");
        _container.Open(data);
        AudioManager.Play("select", 0.9f, -4f);
    }

    private void CloseCrafting()
    {
        _crafting = false;
        _craft.Visible = false;
        _container?.ForceHide();
        Input.MouseMode = _preCraftMouse;
        Player.InputEnabled = true;
        SetMode(CurrentMode);
    }

    public override void _Process(double delta)
    {
        if (Player == null) return;

        Hud.SetUnderwater(Player.HeadUnderwater ? 1f : 0f);
        Hud.SetAirDarken(Player.MaxAir > 0f ? 1f - Player.Air / Player.MaxAir : 0f);
        Hud.SetSprinting(Player.Sprinting);

        Vector3 fwd = -Player.GlobalTransform.Basis.Z;
        Hud.SetHeading(Mathf.RadToDeg(Mathf.Atan2(fwd.X, -fwd.Z)));

        if (Env != null)
        {
            string clockText = Env.ClockText();
            if (SeasonClock.Instance != null) clockText += "  ·  " + SeasonClock.Instance.Label;
            if (Survival) clockText += "  ·  " + WeatherDirector.NameOf(Env.Weather);
            Hud.SetClock(clockText);
        }

        if (Input.IsActionJustPressed(GameInput.Actions.Debug)) Hud.ToggleDebug();
        if (Hud.DebugVisible)
        {
            Vector3 pp = Player.GlobalPosition;
            long draws = (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame);
            Hud.SetDebug(
                $"FPS {Engine.GetFramesPerSecond()}   draws {draws}\n" +
                $"chunks {World.ChunkCount}   meshing {World.MeshingCount}   dirty {World.DirtyCount}\n" +
                $"pos  {pp.X:F0}, {pp.Y:F0}, {pp.Z:F0}");
        }

        Hud.SetHungerVisible(Survival && !Player.Creative);

        if (_chatOpen) return;
        if (!_freeCamOn && !InDialogue && !_crafting && !_teacherOpen && !_presentMode && !_reading
            && !(Pause?.IsPaused ?? false) && !ChatBusy
            && Input.IsActionJustPressed(GameInput.Actions.Chat))
        {
            OpenChat("");
            return;
        }

        if (_freeCamOn)
        {
            Hud.SetFreeCam("FREE CAM — P to exit\n" + FreeCam.StatusLine());
            if (Input.IsActionJustPressed(GameInput.Actions.FreeCam)) ExitFreeCam();
            return;
        }
        if (Input.IsActionJustPressed(GameInput.Actions.FreeCam)
            && !InDialogue && !_crafting && !_teacherOpen && !_presentMode && !_reading
            && !(Pause?.IsPaused ?? false))
        {
            EnterFreeCam();
            return;
        }

        if (_presentMode)
        {
            if (Input.IsActionJustPressed(GameInput.Actions.Screenshot)) TakeScreenshot();
            if (Input.IsActionJustPressed(GameInput.Actions.Pause)) ExitPresentMode();
            return;
        }

        if (_reading)
        {
            if (Input.IsActionJustPressed(GameInput.Actions.Interact)
                || Input.IsActionJustPressed(GameInput.Actions.Pause))
                CloseReader();
            return;
        }

        bool busy = InDialogue || _crafting || _teacherOpen || (Pause?.IsPaused ?? false);

        if (!InDialogue && !_crafting && !(Pause?.IsPaused ?? false)
            && Input.IsActionJustPressed(GameInput.Actions.Teacher))
        {
            ToggleTeacher();
            return;
        }
        if (_teacherOpen) return;

        if (Survival && !busy && Input.IsActionJustPressed(GameInput.Actions.DropItem))
            DropFromSlot(Hud.Hotbar.SelectedSlot, 1);

        if (Survival && !InDialogue && !(Pause?.IsPaused ?? false)
            && Input.IsActionJustPressed(GameInput.Actions.Inventory))
        {
            ToggleCrafting();
            return;
        }
        if (_crafting) return;

        bool focused = DisplayServer.WindowIsFocused(0);
        if (focused && !_wasFocused && !busy
            && Settings.CaptureMode == Settings.MouseCapture.Always)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
            Player.SuppressActionsFor(0.2f);
        }
        _wasFocused = focused;

        if (!busy && Input.IsActionJustPressed(GameInput.Actions.ToggleCapture))
        {
            bool wasCap = Input.MouseMode == Input.MouseModeEnum.Captured;
            Input.MouseMode = wasCap ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
            if (!wasCap) Player.SuppressActionsFor(0.2f);
            Hud.ShowBanner(wasCap ? "Fare serbest" : "Fare ile bakış açık", 1.2f);
        }

        if (!busy && Input.MouseMode != Input.MouseModeEnum.Captured
            && Settings.CaptureMode == Settings.MouseCapture.ClickToCapture)
            Hud.SetMouseHint("Bakmak için tıkla  ·  M: fareyi bırak  ·  oklar da bakış sağlar");
        else
            Hud.SetMouseHint("");

        if (InDialogue) return;

        if (Input.IsActionJustPressed(GameInput.Actions.ToggleBuild) && Player.InputEnabled)
        {
            SetMode(CurrentMode == Mode.Build ? Mode.Adventure : Mode.Build);
            Hud.ShowBanner(CurrentMode == Mode.Build ? "İnşa modu" : "Macera modu", 1.5f);
        }

        Npc best = null;
        float bestDist = InteractRange;
        foreach (Node n in GetTree().GetNodesInGroup("npc"))
        {
            if (n is not Npc npc || npc.Dialogue == null) continue;
            float d = npc.GlobalPosition.DistanceTo(Player.GlobalPosition);
            if (d < bestDist) { bestDist = d; best = npc; }
        }

        if (best != null)
        {
            Hud.SetInteractPrompt($"[E]  Talk to {best.NpcName}");
            if (Input.IsActionJustPressed(GameInput.Actions.Interact))
                StartDialogueWith(best);
        }
        else
        {

            PixelBlock.World.Signpost sign = null;
            float signDist = InteractRange;
            foreach (Node n in GetTree().GetNodesInGroup("signpost"))
            {
                if (n is not PixelBlock.World.Signpost sp) continue;
                float d = sp.GlobalPosition.DistanceTo(Player.GlobalPosition);
                if (d < signDist) { signDist = d; sign = sp; }
            }
            if (sign != null)
            {
                Hud.SetInteractPrompt("[E]  Read sign");
                if (Input.IsActionJustPressed(GameInput.Actions.Interact)) OpenReader(sign);
            }
            else
            {
                Hud.SetInteractPrompt("");
            }
        }
    }

    private bool _reading;
    private Input.MouseModeEnum _preReadMouse = Input.MouseModeEnum.Visible;

    private void OpenReader(PixelBlock.World.Signpost sign)
    {
        _reading = true;
        Hud.SetInteractPrompt("");
        Player.InputEnabled = false;
        Weapons.SetEnabled(false);
        Interactor.CanEdit = false;
        _preReadMouse = Input.MouseMode;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Hud.OpenReader(sign.Title, sign.Text);
    }

    private void CloseReader()
    {
        _reading = false;
        Hud.CloseReader();
        Player.InputEnabled = true;
        Input.MouseMode = _preReadMouse;
        SetMode(CurrentMode);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Unicode: '/' }
            && !_chatOpen && !ChatBusy && !_freeCamOn && !InDialogue && !_crafting && !_teacherOpen
            && !_presentMode && !_reading && !(Pause?.IsPaused ?? false) && Player.InputEnabled)
        {
            OpenChat("/");
            GetViewport().SetInputAsHandled();
            return;
        }
        if (Settings.CaptureMode == Settings.MouseCapture.ClickToCapture
            && e is InputEventMouseButton { Pressed: true }
            && Input.MouseMode != Input.MouseModeEnum.Captured
            && !InDialogue && !_crafting && !_teacherOpen && !_presentMode && !_chatOpen
            && !(Pause?.IsPaused ?? false))
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
            Player?.SuppressActionsFor(0.2f);
            GetViewport().SetInputAsHandled();
        }
    }

    private Npc _talkingNpc;
    private Input.MouseModeEnum _preDialogueMouse = Input.MouseModeEnum.Visible;

    public void StartDialogueWith(Npc npc)
    {
        StartDialogue(npc.Dialogue, npc);
    }

    public void StartDialogue(DialogueData data, Npc npc = null)
    {
        if (data == null || InDialogue) return;
        _talkingNpc = npc;
        InDialogue = true;
        Hud.SetInteractPrompt("");
        Hud.SetMouseHint("");
        Player.InputEnabled = false;
        Weapons.SetEnabled(false);
        Interactor.CanEdit = false;
        _preDialogueMouse = Input.MouseMode;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Dialogue.StartDialogue(data);
    }

    private void OnDialogueFinished()
    {
        InDialogue = false;
        Player.InputEnabled = true;
        SetMode(CurrentMode);

        Input.MouseMode = _preDialogueMouse;
        var npc = _talkingNpc;
        _talkingNpc = null;
        npc?.EmitSignal(Npc.SignalName.Talked);
        if (npc != null) EmitSignal(SignalName.NpcTalked, npc.NpcName);
    }

    public void SetMode(Mode mode)
    {
        CurrentMode = mode;
        bool build = mode == Mode.Build;
        Interactor.CanEdit = build;
        Editor.SetEnabled(build && !Survival);
        Weapons.SetEnabled(!build);
        _held?.SetShown(build);
        Hud.SetHotbarVisible(build);
        Hud.SetWeaponVisible(!build);
        Hud.SetWeapon(Weapons.Current?.Name ?? "");
    }

    public Enemy SpawnEnemy(EnemyType type, Vector3 position)
    {
        var e = new Enemy { Type = type, Target = Player, World = World, Name = $"Enemy_{type.Name}" };
        World.AddChild(e);
        e.GlobalPosition = GroundSnap(position);
        e.Defeated += () => EmitSignal(SignalName.EnemyDefeated, type.Name);
        return e;
    }

    private Vector3 GroundSnap(Vector3 position)
    {
        int x = Mathf.FloorToInt(position.X);
        int z = Mathf.FloorToInt(position.Z);
        for (int y = 128; y >= -32; y--)
            if (World.IsSolid(new Vector3I(x, y, z)))
                return new Vector3(position.X, y + 1, position.Z);
        return position;
    }

    public PixelBlock.World.NarrationTrigger AddTrigger(Vector3 pos, Vector3 size, string id, params string[] lines)
    {
        var t = PixelBlock.World.NarrationTrigger.Create(pos, size, Narrator, lines);
        t.Id = id;
        t.Entered += () => EmitSignal(SignalName.PlayerReached, id);
        World.AddChild(t);
        return t;
    }

    public void StartQuest(Quests.Quest quest)
    {
        if (quest == null || Quest != null) return;
        Quest = new Quests.QuestTracker(this, quest);
        ValidateQuest(quest);
        NpcTalked += name => Quest.OnTalk(name);
        EnemyDefeated += name => Quest.OnDefeat(name);
        PlayerReached += id => Quest.OnReach(id);
        World.BlockChanged += OnWorldBlockChanged;
        World.EmitBlockChanges = true;
        Quest.Begin();
    }

    internal void NotifyQuestComplete() => EmitSignal(SignalName.QuestCompleted, LessonId ?? "");

    private void OnWorldBlockChanged(Vector3I pos, int oldId, int newId, int cause)
        => Quest?.OnBlockChanged((ushort)oldId, (ushort)newId);

    private void ValidateQuest(Quests.Quest quest)
    {
        foreach (var o in quest.Objectives)
        {
            if (o.Kind == Quests.ObjectiveKind.Defeat && o.Key != null && !HasEnemyType(o.Key))
                GD.PushWarning($"[Quest] no spawned enemy of type '{o.Key}' for objective '{o.Label}'");
            if (o.Kind == Quests.ObjectiveKind.Reach && o.Key != null && !HasTrigger(o.Key))
                GD.PushWarning($"[Quest] no trigger with id '{o.Key}' for objective '{o.Label}'");
            if ((o.Kind == Quests.ObjectiveKind.Break || o.Kind == Quests.ObjectiveKind.Place
                 || o.Kind == Quests.ObjectiveKind.Collect)
                && (!BlockRegistry.TryId(o.Key, out ushort bid) || bid == 0))
                GD.PushWarning($"[Quest] block objective '{o.Label}' has no valid block name '{o.Key}'");
        }
    }

    private bool HasEnemyType(string typeName)
    {
        foreach (Node n in GetTree().GetNodesInGroup("enemy"))
            if (n is Enemy e && string.Equals(e.Type.Name, typeName, System.StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private bool HasTrigger(string id)
    {
        foreach (Node n in World.GetChildren())
            if (n is PixelBlock.World.NarrationTrigger t && t.Id == id) return true;
        return false;
    }

    private void OnPlayerHurt(float amount, string cause)
    {
        Hud.FlashHurt(Mathf.Clamp(0.35f + amount / 45f, 0.35f, 0.85f));
        Fx.Shake(Mathf.Clamp(amount / 25f, 0.12f, 0.5f));
    }

    public override void _ExitTree()
    {

        if (Fx.OnShake != null || Fx.OnFlash != null)
        {
            Fx.OnShake = null;
            Fx.OnFlash = null;
        }

        if (World != null && GodotObject.IsInstanceValid(World))
            World.BlockChanged -= OnWorldBlockChanged;
    }
}
