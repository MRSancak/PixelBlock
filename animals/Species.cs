using Godot;
using PixelBlock.Core;

namespace PixelBlock.Animals;

public sealed class SpeciesDef
{
    public string Key;
    public string Name;
    public Color Body;
    public Color Accent;
    public Vector3 Size;
    public float LegH;
    public float HeadS;
    public float Walk;
    public float Run;
    public float Hp;
    public float FleeDist;
    public float ChaseDist;
    public float Bite;
    public bool Predator;
    public bool Hops;
    public string[] Foods = System.Array.Empty<string>();
    public string[] Prey = System.Array.Empty<string>();
    public string Sound;
    public int Meat;
    public Biome[] Biomes = System.Array.Empty<Biome>();
    public bool Herd;
}

public static class Species
{
    public static readonly SpeciesDef[] All =
    {
        new SpeciesDef { Key = "inek", Name = "İnek", Body = new Color(0.93f, 0.93f, 0.9f), Accent = new Color(0.18f, 0.16f, 0.15f),
            Size = new Vector3(0.9f, 0.85f, 1.4f), LegH = 0.65f, HeadS = 0.5f, Walk = 1.2f, Run = 3.2f, Hp = 10, FleeDist = 4,
            Foods = new[] { "bugday" }, Sound = "moo", Meat = 2, Herd = true,
            Biomes = new[] { Biome.Plains, Biome.Meadow } },
        new SpeciesDef { Key = "koyun", Name = "Koyun", Body = new Color(0.92f, 0.9f, 0.84f), Accent = new Color(0.25f, 0.22f, 0.2f),
            Size = new Vector3(0.8f, 0.7f, 1.05f), LegH = 0.5f, HeadS = 0.38f, Walk = 1.1f, Run = 3.4f, Hp = 8, FleeDist = 5,
            Foods = new[] { "bugday", "havuc" }, Sound = "baa", Meat = 2, Herd = true,
            Biomes = new[] { Biome.Plains, Biome.Meadow, Biome.Mountains } },
        new SpeciesDef { Key = "tavuk", Name = "Tavuk", Body = new Color(0.97f, 0.96f, 0.93f), Accent = new Color(0.85f, 0.15f, 0.1f),
            Size = new Vector3(0.38f, 0.38f, 0.5f), LegH = 0.25f, HeadS = 0.22f, Walk = 1.0f, Run = 3.0f, Hp = 4, FleeDist = 4,
            Sound = "cluck", Meat = 1, Herd = true, Hops = true,
            Biomes = new[] { Biome.Plains, Biome.Meadow, Biome.Swamp } },
        new SpeciesDef { Key = "domuz", Name = "Domuz", Body = new Color(0.96f, 0.68f, 0.68f), Accent = new Color(0.9f, 0.5f, 0.52f),
            Size = new Vector3(0.8f, 0.7f, 1.1f), LegH = 0.4f, HeadS = 0.42f, Walk = 1.2f, Run = 3.2f, Hp = 10, FleeDist = 3,
            Foods = new[] { "havuc", "patates" }, Sound = "oink", Meat = 3, Herd = true,
            Biomes = new[] { Biome.Plains, Biome.Forest, Biome.Swamp } },
        new SpeciesDef { Key = "at", Name = "At", Body = new Color(0.50f, 0.32f, 0.18f), Accent = new Color(0.15f, 0.1f, 0.07f),
            Size = new Vector3(0.75f, 1.0f, 1.8f), LegH = 1.0f, HeadS = 0.55f, Walk = 1.8f, Run = 5.8f, Hp = 15, FleeDist = 6,
            Foods = new[] { "bugday", "havuc", "elma" }, Sound = "neigh", Meat = 3,
            Biomes = new[] { Biome.Plains, Biome.Meadow, Biome.Desert } },
        new SpeciesDef { Key = "tavsan", Name = "Tavşan", Body = new Color(0.72f, 0.62f, 0.5f), Accent = new Color(0.95f, 0.93f, 0.9f),
            Size = new Vector3(0.3f, 0.3f, 0.45f), LegH = 0.14f, HeadS = 0.2f, Walk = 1.5f, Run = 5.0f, Hp = 3, FleeDist = 9,
            Foods = new[] { "havuc" }, Sound = "squeak", Meat = 1, Hops = true, Herd = true,
            Biomes = new[] { Biome.Plains, Biome.Meadow, Biome.Forest, Biome.Snow, Biome.Desert } },
        new SpeciesDef { Key = "geyik", Name = "Geyik", Body = new Color(0.66f, 0.46f, 0.26f), Accent = new Color(0.9f, 0.85f, 0.7f),
            Size = new Vector3(0.6f, 0.85f, 1.3f), LegH = 0.95f, HeadS = 0.42f, Walk = 1.6f, Run = 6.5f, Hp = 10, FleeDist = 12,
            Sound = "baa", Meat = 3, Herd = true,
            Biomes = new[] { Biome.Forest, Biome.DenseForest } },
        new SpeciesDef { Key = "kurt", Name = "Kurt", Body = new Color(0.52f, 0.52f, 0.55f), Accent = new Color(0.25f, 0.25f, 0.28f),
            Size = new Vector3(0.5f, 0.6f, 1.1f), LegH = 0.55f, HeadS = 0.4f, Walk = 1.6f, Run = 5.2f, Hp = 12, ChaseDist = 8, Bite = 5,
            Predator = true, Prey = new[] { "tavsan", "koyun", "tavuk", "geyik" }, Sound = "howl", Meat = 2,
            Biomes = new[] { Biome.Forest, Biome.DenseForest, Biome.Snow } },
        new SpeciesDef { Key = "tilki", Name = "Tilki", Body = new Color(0.92f, 0.48f, 0.12f), Accent = new Color(0.97f, 0.95f, 0.9f),
            Size = new Vector3(0.38f, 0.42f, 0.8f), LegH = 0.35f, HeadS = 0.3f, Walk = 1.5f, Run = 5.0f, Hp = 7, FleeDist = 6, Bite = 0,
            Predator = true, Prey = new[] { "tavsan", "tavuk" }, Sound = "squeak", Meat = 1,
            Biomes = new[] { Biome.Forest, Biome.DenseForest, Biome.Snow, Biome.Plains } },
        new SpeciesDef { Key = "ayi", Name = "Ayı", Body = new Color(0.32f, 0.2f, 0.12f), Accent = new Color(0.2f, 0.12f, 0.08f),
            Size = new Vector3(1.0f, 1.05f, 1.6f), LegH = 0.6f, HeadS = 0.55f, Walk = 1.4f, Run = 4.2f, Hp = 30, ChaseDist = 7, Bite = 10,
            Predator = true, Prey = new string[0], Sound = "growl", Meat = 4,
            Biomes = new[] { Biome.Forest, Biome.DenseForest, Biome.Mountains } },
    };

    public static SpeciesDef ByKey(string key)
    {
        foreach (var s in All) if (s.Key == key) return s;
        return null;
    }

    public static bool Eats(SpeciesDef sp, string itemName)
    {
        foreach (var f in sp.Foods) if (f == itemName) return true;
        return sp.Key == "tavuk" && itemName != null && (itemName.StartsWith("tohum_") || itemName == ItemCatalog.WildSeedKey);
    }
}
