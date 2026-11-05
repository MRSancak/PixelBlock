using Godot;
using PixelBlock.Animals;
using PixelBlock.Core;
using PixelBlock.Farming;
using PixelBlock.UI;

namespace PixelBlock.PlayerSys;

public partial class BlockInteractor : Node3D
{
    public VoxelWorld World;
    public Player Player;
    public Hotbar Hotbar;

    public Inventory Inventory;
    public bool CanEdit = true;
    public System.Action<Vector3I, string> OpenContainer;
    public float Reach = 6f;

    private MeshInstance3D _highlight;
    private VoxelRay.Hit _target;
    private float _breakTimer, _placeTimer;
    private const float RepeatDelay = 0.18f;

    private MeshInstance3D _crack;
    private StandardMaterial3D _crackMat;
    private ImageTexture[] _crackStages;
    private Vector3I _miningCell;
    private bool _mining;
    private float _miningProgress;
    private int _crackStage = -1;
    private float _hitSfx;
    private float _meleeCd;

    public VoxelRay.Hit CurrentTarget => _target;

    public override void _Ready()
    {
        _highlight = new MeshInstance3D { Name = "Highlight", Mesh = BuildWireCube() };
        _highlight.MaterialOverride = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0, 0, 0, 0.85f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        _highlight.Visible = false;
        AddChild(_highlight);

        _crackMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Back,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
        };
        _crack = new MeshInstance3D
        {
            Name = "Crack",
            Mesh = new BoxMesh { Size = new Vector3(1.02f, 1.02f, 1.02f) },
            MaterialOverride = _crackMat,
            Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_crack);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (World == null || Player == null) return;
        UpdateTarget();

        if (!CanEdit || !Player.InputEnabled || Player.ActionsSuppressed)
        {
            _breakTimer = _placeTimer = 0f;
            ResetMining();
            return;
        }

        float dt = (float)delta;
        bool mouse = Input.MouseMode == Input.MouseModeEnum.Captured;
        if (_meleeCd > 0f) _meleeCd -= dt;
        UpdateMining(mouse, dt);
        StepAction(mouse, GameInput.Actions.Secondary, GameInput.Actions.KbPlace, ref _placeTimer, TryPlace, dt);
    }

    private void UpdateMining(bool mouse, float dt)
    {
        bool held = (mouse && Input.IsActionPressed(GameInput.Actions.Primary))
                    || Input.IsActionPressed(GameInput.Actions.KbBreak);

        if (HandlePlantPick()) return;
        if (HandleAnimalAttack(held)) return;
        if (HandleMeleeAttack(held)) return;

        if (!held || !_target.Ok) { ResetMining(); return; }

        if (!_mining || _target.Block != _miningCell)
        {
            _mining = true;
            _miningCell = _target.Block;
            _miningProgress = 0f;
            _hitSfx = 0f;
        }

        var b = World.GetBlock(_miningCell);
        if (b.IsAir) { ResetMining(); return; }

        if (b.Hardness <= 0f) { BreakAt(_miningCell); ResetMining(); return; }

        _miningProgress += dt * ToolMultiplier(b) / b.Hardness;

        _hitSfx -= dt;
        if (_hitSfx <= 0f)
        {
            float pitch = Mathf.Clamp(1.15f - b.Hardness * 0.12f, 0.6f, 1.15f);
            AudioManager.Play($"mine_{b.Material}", pitch, -7f);
            _hitSfx = 0.26f;
        }

        if (_miningProgress >= 1f)
        {
            BreakAt(_miningCell);

            _mining = false;
            _miningProgress = 0f;
        }
        UpdateCrackVisual();
    }

    private bool HandlePlantPick()
    {
        var wp = WildPlants.Instance;
        if (wp == null || Player?.Camera == null) return false;
        var cam = Player.Camera;
        float maxD = Mathf.Min(4.5f, _target.Ok ? _target.Distance : 4.5f);
        if (!wp.Probe(cam.GlobalPosition, -cam.GlobalTransform.Basis.Z, maxD, out var plant)) return false;
        ResetMining();
        bool just = Input.IsActionJustPressed(GameInput.Actions.Primary) || Input.IsActionJustPressed(GameInput.Actions.KbBreak);
        if (just) wp.Harvest(plant, Inventory, Player.GetParent());
        return true;
    }

    private bool HandleAnimalAttack(bool pressing)
    {
        var dir = AnimalDirector.Instance;
        if (dir == null || Player?.Camera == null || dir.All.Count == 0) return false;
        var cam = Player.Camera;
        var a = dir.FindInFront(cam.GlobalPosition, -cam.GlobalTransform.Basis.Z, 3.4f);
        if (a == null) return false;
        ResetMining();
        if (pressing && Hotbar != null)
        {
            var item = BlockRegistry.Get(Hotbar.SelectedBlockId);
            dir.Attack(a, Mathf.Max(3f, item.Damage), Player.GlobalPosition);
        }
        return true;
    }

    private bool HandleMeleeAttack(bool pressing)
    {
        var cam = Player?.Camera;
        if (cam == null) return false;
        Vector3 from = cam.GlobalPosition;
        Vector3 to = from + -cam.GlobalTransform.Basis.Z * 3.4f;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.Exclude = new Godot.Collections.Array<Rid> { Player.GetRid() };
        var hit = Player.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0 || hit["collider"].As<GodotObject>() is not IDamageable target || !target.IsAlive) return false;
        ResetMining();
        if (pressing && _meleeCd <= 0f)
        {
            _meleeCd = 0.45f;
            float dmg = Hotbar != null ? Mathf.Max(3f, BlockRegistry.Get(Hotbar.SelectedBlockId).Damage) : 3f;
            AudioManager.Play("swing", 1.0f, -6f);
            target.TakeDamage(dmg, Player);
        }
        return true;
    }

    private float ToolMultiplier(BlockType target)
    {
        if (Hotbar == null || Inventory == null) return 1f;
        var held = BlockRegistry.Get(Hotbar.SelectedBlockId);
        if (held.Tool != ToolKind.None && held.Tool == target.Preferred) return held.ToolSpeed;
        return 1f;
    }

    private void ResetMining()
    {
        if (!_mining && _miningProgress == 0f && (_crack == null || !_crack.Visible)) return;
        _mining = false;
        _miningProgress = 0f;
        _crackStage = -1;
        if (_crack != null) _crack.Visible = false;
    }

    private void UpdateCrackVisual()
    {
        if (_crack == null) return;
        if (!_mining || _miningProgress <= 0f) { _crack.Visible = false; return; }
        EnsureCrackStages();
        int stage = Mathf.Clamp((int)(_miningProgress * _crackStages.Length), 0, _crackStages.Length - 1);
        if (stage != _crackStage)
        {
            _crackStage = stage;
            _crackMat.AlbedoTexture = _crackStages[stage];

            var mb = World.GetBlock(_miningCell);
            Color chipTint = World.Textures?.AverageColor(mb) ?? new Color(0.6f, 0.6f, 0.6f);
            Fx.Burst((Vector3)_miningCell + new Vector3(0.5f, 0.5f, 0.5f), FxKind.Debris, chipTint, 4);
            Fx.Shake(0.035f);
        }
        _crack.GlobalPosition = (Vector3)_miningCell + new Vector3(0.5f, 0.5f, 0.5f);
        _crack.Visible = true;
    }

    private void EnsureCrackStages()
    {
        if (_crackStages != null) return;
        const int n = 8, s = 64;
        _crackStages = new ImageTexture[n];
        for (int stage = 0; stage < n; stage++)
        {
            var img = Image.CreateEmpty(s, s, false, Image.Format.Rgba8);
            img.Fill(new Color(0, 0, 0, 0));
            int cracks = 2 + stage * 2;
            for (int c = 0; c < cracks; c++)
            {
                uint h = ValueNoise2D.Hash(c, 7, 1234);
                float ang = (h % 360u) * Mathf.Pi / 180f;
                float len = s * (0.28f + ((h >> 9) % 100u) / 100f * 0.22f);
                DrawCrack(img, s / 2f, s / 2f, ang, len, h);
            }
            img.GenerateMipmaps();
            _crackStages[stage] = ImageTexture.CreateFromImage(img);
        }
    }

    private static void DrawCrack(Image img, float x, float y, float ang, float len, uint seed)
    {
        int s = img.GetWidth();
        int steps = (int)len;
        for (int i = 0; i < steps; i++)
        {
            uint hh = ValueNoise2D.Hash((int)(seed & 0xFFFF), i, 99);
            float jitter = ((hh % 100u) / 100f - 0.5f) * 0.7f;
            float a = ang + jitter;
            x += Mathf.Cos(a);
            y += Mathf.Sin(a);
            if (x < 1 || y < 1 || x > s - 2 || y > s - 2) break;
            PlotDark(img, (int)x, (int)y, 0.85f);
            PlotDark(img, (int)x + 1, (int)y, 0.4f);
            PlotDark(img, (int)x, (int)y + 1, 0.4f);
        }
    }

    private static void PlotDark(Image img, int x, int y, float a)
    {
        if (x < 0 || y < 0 || x >= img.GetWidth() || y >= img.GetHeight()) return;
        float na = Mathf.Max(img.GetPixel(x, y).A, a);
        img.SetPixel(x, y, new Color(0.04f, 0.03f, 0.02f, na));
    }

    private void StepAction(bool mouse, string mouseAct, string keyAct, ref float timer, System.Func<bool> act, float dt)
    {
        bool just = (mouse && Input.IsActionJustPressed(mouseAct)) || Input.IsActionJustPressed(keyAct);
        bool held = (mouse && Input.IsActionPressed(mouseAct)) || Input.IsActionPressed(keyAct);
        if (just) { act(); timer = RepeatDelay; return; }
        if (held)
        {
            timer -= dt;
            if (timer <= 0f) { act(); timer = RepeatDelay; }
        }
        else timer = 0f;
    }

    private void UpdateTarget()
    {

        if (!Player.InputEnabled)
        {
            _highlight.Visible = false;
            _target = default;
            return;
        }
        Camera3D cam = Player.Camera;
        Vector3 origin = cam.GlobalPosition;
        Vector3 dir = -cam.GlobalTransform.Basis.Z;
        _target = VoxelRay.Cast(World, origin, dir, Reach);
        if (_target.Ok)
        {
            _highlight.Visible = true;
            _highlight.GlobalPosition = _target.Block;
        }
        else _highlight.Visible = false;
    }

    public bool TryBreak()
    {
        if (!_target.Ok) return false;
        return BreakAt(_target.Block);
    }

    public bool TryPlace()
    {
        if (Hotbar == null) return false;
        var held = BlockRegistry.Get(Hotbar.SelectedBlockId);

        if (AnimalDirector.Instance != null && AnimalDirector.Instance.TryInteract(Player, held, Inventory)) return true;
        if (held.ArmorSlot > 0 && Hotbar.SelectedBlockId != 0) return EquipHeld(held);
        if (Hotbar.SelectedBlockId != 0 && !held.Placeable && held.Food > 0f)
        {
            if (_target.Ok && TryCook(held)) return true;
            return UseItem(held);
        }
        if (!_target.Ok) return false;

        if (OpenContainer != null && Containers.Instance != null)
        {
            string tn = World.GetBlock(_target.Block).Name;
            bool sneakPlace = Player.Crouching && held.Placeable && Hotbar.SelectedBlockId != 0;
            if (Containers.IsContainer(tn) && !sneakPlace)
            {
                OpenContainer(_target.Block, tn);
                return true;
            }
        }

        if (FarmManager.Instance != null && FarmManager.Instance.UseOn(_target.Block, held, Inventory, GetParent())) return true;
        if (Hotbar.SelectedBlockId != 0 && !held.Placeable) return UseItem(held);
        return PlaceAt(_target.Prev, Hotbar.SelectedBlockId);
    }

    private bool EquipHeld(BlockType item)
    {
        if (Inventory == null || !Inventory.TryConsume(item.Id)) return false;
        ushort old = Player.SetArmor(item.ArmorSlot - 1, item.Id);
        if (old != 0) Inventory.Add(old, 1);
        AudioManager.Play("select", 0.7f, -3f);
        return true;
    }

    private bool TryCook(BlockType item)
    {
        if (item.Name != "et" || Inventory == null) return false;
        string tn = World.GetBlock(_target.Block).Name;
        if (tn != "campfire" && tn != "altar_fire") return false;
        ushort cooked = BlockRegistry.IdOf("pismis_et");
        if (cooked == 0 || !Inventory.TryConsume(item.Id)) return false;
        Inventory.Add(cooked, 1);
        AudioManager.Play("select", 0.6f, -2f);
        Fx.Burst((Vector3)_target.Block + new Vector3(0.5f, 1.1f, 0.5f), FxKind.Poof, new Color(1f, 0.6f, 0.2f), 8);
        return true;
    }

    private bool UseItem(BlockType item)
    {
        if (item.Food > 0f)
        {
            if (Player.Creative) return false;
            if (Player.HungerEnabled)
            {
                if (Player.Hunger >= Player.MaxHunger - 0.01f) return false;
                Player.Eat(item.Food);
            }
            else
            {
                if (Player.Health >= Player.MaxHealth) return false;
                Player.Heal(item.Food);
            }
            Inventory?.TryConsume(item.Id);
            AudioManager.Play("select", 0.8f, -4f);
            return true;
        }
        if (item.Tool == ToolKind.Hoe && _target.Ok)
        {
            var tb = World.GetBlock(_target.Block);
            if (tb.Name == "grass" || tb.Name == "dirt")
            {
                World.SetBlock(_target.Block, BlockRegistry.IdOf("farmland"));
                NetBridge.BlockChanged(_target.Block, BlockRegistry.IdOf("farmland"));
                AudioManager.Play("break_Dirt", 1.2f, -4f);
                return true;
            }
        }
        return false;
    }

    public bool BreakAt(Vector3I cell)
    {
        var b = World.GetBlock(cell);
        if (b.IsAir) return false;
        FarmManager.Instance?.RemoveAt(cell);
        Containers.Instance?.OnBroken(cell);
        World.SetBlock(cell, 0);
        NetBridge.BlockChanged(cell, 0);
        if (!b.IsLiquid)
        {
            ushort dropId = string.IsNullOrEmpty(b.Drop) ? b.Id : BlockRegistry.IdOf(b.Drop);
            if (b.MinTier > 0 && !(Player != null && Player.Creative))
            {
                var tool = Hotbar != null ? BlockRegistry.Get(Hotbar.SelectedBlockId) : null;
                if (tool == null || tool.Tool != ToolKind.Pickaxe || tool.Tier < b.MinTier) dropId = 0;
            }
            if (!(Player != null && Player.Creative) && dropId != 0) Inventory?.Add(dropId);
        }
        AudioManager.Play($"break_{b.Material}");
        if (!b.IsLiquid)
        {

            Color tint = World.Textures?.AverageColor(b) ?? new Color(0.6f, 0.6f, 0.6f);
            Fx.Burst((Vector3)cell + new Vector3(0.5f, 0.5f, 0.5f), FxKind.Debris, tint, 16);
            Fx.Shake(0.05f);
        }
        return true;
    }

    public bool PlaceAt(Vector3I cell, ushort id)
    {
        if (id == 0) return false;
        if (!BlockRegistry.Get(id).Placeable) return false;
        bool free = Player != null && Player.Creative;
        if (!free && Inventory != null && !Inventory.Has(id)) return false;
        var existing = World.GetBlock(cell);
        if (!existing.IsAir && !existing.IsLiquid) return false;
        if (WouldHitPlayer(cell) && BlockRegistry.Get(id).Solid) return false;
        FarmManager.Instance?.RemoveAt(cell);
        World.SetBlock(cell, id);
        NetBridge.BlockChanged(cell, id);
        if (!free) Inventory?.TryConsume(id);
        AudioManager.Play("place");

        Color poof = (World.Textures?.AverageColor(BlockRegistry.Get(id)) ?? Colors.White).Lerp(Colors.White, 0.4f);
        Fx.Burst((Vector3)cell + new Vector3(0.5f, 0.5f, 0.5f), FxKind.Poof, poof, 10);
        return true;
    }

    private bool WouldHitPlayer(Vector3I cell)
    {
        if (Player == null) return false;
        Vector3 p = Player.GlobalPosition;
        const float r = 0.36f;
        float h = Player.CollisionHeight;
        var pMin = new Vector3(p.X - r, p.Y, p.Z - r);
        var pMax = new Vector3(p.X + r, p.Y + h, p.Z + r);
        var cMin = (Vector3)cell;
        var cMax = cMin + Vector3.One;
        return pMin.X < cMax.X && pMax.X > cMin.X
            && pMin.Y < cMax.Y && pMax.Y > cMin.Y
            && pMin.Z < cMax.Z && pMax.Z > cMin.Z;
    }

    private static ArrayMesh BuildWireCube()
    {

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Lines);
        float a = -0.002f, b = 1.002f;
        Vector3[] c =
        {
            new(a, a, a), new(b, a, a), new(b, a, b), new(a, a, b),
            new(a, b, a), new(b, b, a), new(b, b, b), new(a, b, b),
        };
        int[,] edges =
        {
            { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },
            { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },
            { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 },
        };
        for (int i = 0; i < 12; i++)
        {
            st.AddVertex(c[edges[i, 0]]);
            st.AddVertex(c[edges[i, 1]]);
        }
        return st.Commit();
    }
}
