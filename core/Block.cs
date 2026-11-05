using Godot;

namespace PixelBlock.Core;

public enum Face : byte { PosX = 0, NegX = 1, PosY = 2, NegY = 3, PosZ = 4, NegZ = 5 }

public enum RenderType : byte { None, Opaque, Water }

public enum MaterialSound : byte { Stone, Dirt, Grass, Sand, Wood, Snow, Metal, Cloth }

public enum ToolKind : byte { None, Pickaxe, Axe, Shovel, Hoe, Sword }

public enum ItemCategory : byte { Block, Natural, Building, Tool, Weapon, Farming, Food, Decoration, Armor }

public sealed class BlockType
{
    public ushort Id;
    public string Name;
    public string DisplayName;

    public readonly string[] FaceTex = new string[6];

    public readonly int[] FaceLayer = new int[6];

    public RenderType Render = RenderType.Opaque;
    public bool Solid = true;
    public bool Opaque = true;
    public bool Climbable = false;
    public bool Hazard = false;
    public float HazardDamage = 0f;
    public bool Emissive = false;

    public bool SpawnsVegetation = false;

    public float Hardness = 0.6f;

    public MaterialSound Material = MaterialSound.Stone;

    public bool Placeable = true;

    public string Drop;

    public ToolKind Preferred = ToolKind.None;

    public ToolKind Tool = ToolKind.None;
    public float ToolSpeed = 1f;

    public float Damage = 0f;

    public int Tier = 0;
    public int MinTier = 0;
    public int ArmorSlot = 0;
    public float ArmorValue = 0f;

    public float Food = 0f;
    public ItemCategory Category = ItemCategory.Block;

    public Color IconColor = new(0.8f, 0.8f, 0.8f);

    public bool IsAir => Render == RenderType.None;
    public bool IsLiquid => Render == RenderType.Water;

    public BlockType SetFaces(string all)
    {
        for (int i = 0; i < 6; i++) FaceTex[i] = all;
        return this;
    }

    public BlockType SetFaces(string top, string bottom, string side)
    {
        FaceTex[(int)Face.PosY] = top;
        FaceTex[(int)Face.NegY] = bottom;
        FaceTex[(int)Face.PosX] = FaceTex[(int)Face.NegX] =
            FaceTex[(int)Face.PosZ] = FaceTex[(int)Face.NegZ] = side;
        return this;
    }
}

public static class VoxelGeometry
{
    public static readonly Vector3I[] Normals =
    {
        new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1),
    };

    public static readonly Vector3[][] Corners =
    {
        new[] { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(1,0,1) },
        new[] { new Vector3(0,0,0), new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0) },
        new[] { new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,1,0) },
        new[] { new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1), new Vector3(0,0,1) },
        new[] { new Vector3(0,0,1), new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1) },
        new[] { new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,0,0) },
    };

    public static readonly Vector3[] Tangents =
    {
        new(0, 1, 0), new(0, 0, 1), new(0, 0, 1), new(1, 0, 0), new(1, 0, 0), new(0, 1, 0),
    };

    public static readonly Vector3I[] Du =
    {
        new(0, 1, 0), new(0, 0, 1), new(0, 0, 1), new(1, 0, 0), new(1, 0, 0), new(0, 1, 0),
    };

    public static readonly Vector3I[] Dv =
    {
        new(0, 0, 1), new(0, 1, 0), new(1, 0, 0), new(0, 0, 1), new(0, 1, 0), new(1, 0, 0),
    };

    public static readonly Vector2[] Uvs =
    {
        new(0, 0), new(1, 0), new(1, 1), new(0, 1),
    };

    public static readonly int[] TriOrder = { 0, 2, 1, 0, 3, 2 };
}
