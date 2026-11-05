using System;
using System.Collections.Generic;
using Godot;

namespace PixelBlock.Core;

public static class SeasonBits
{
    public const int Spring = 1, Summer = 2, Autumn = 4, Winter = 8;
}

public sealed class CropDef
{
    public string Key;
    public string Name;
    public string SeedKey;
    public Color Color;
    public float GrowSeconds;
    public int SeasonMask;
    public bool Perennial;
    public float Height;
    public float Food;
    public bool IsFruit;
}

public static class ItemCatalog
{
    public static readonly CropDef[] Crops =
    {
        C("patates", "Patates", new Color(0.78f, 0.62f, 0.36f), 150, SeasonBits.Spring | SeasonBits.Autumn, false, 0.45f, 4, false),
        C("havuc", "Havuç", new Color(0.96f, 0.50f, 0.10f), 120, SeasonBits.Spring | SeasonBits.Autumn, false, 0.5f, 4, false),
        C("domates", "Domates", new Color(0.90f, 0.15f, 0.12f), 210, SeasonBits.Spring | SeasonBits.Summer, false, 0.8f, 5, false),
        C("sogan", "Soğan", new Color(0.86f, 0.74f, 0.52f), 150, SeasonBits.Spring | SeasonBits.Autumn, false, 0.45f, 3, false),
        C("lahana", "Lahana", new Color(0.55f, 0.82f, 0.45f), 180, SeasonBits.Autumn | SeasonBits.Winter | SeasonBits.Spring, false, 0.5f, 4, false),
        C("salatalik", "Salatalık", new Color(0.30f, 0.66f, 0.26f), 170, SeasonBits.Summer, false, 0.7f, 4, false),
        C("biber", "Biber", new Color(0.86f, 0.16f, 0.10f), 200, SeasonBits.Summer, false, 0.7f, 3, false),
        C("misir", "Mısır", new Color(0.96f, 0.86f, 0.25f), 240, SeasonBits.Summer, false, 1.3f, 5, false),
        C("elma", "Elma", new Color(0.85f, 0.12f, 0.10f), 300, SeasonBits.Spring | SeasonBits.Summer | SeasonBits.Autumn, true, 0.9f, 6, true),
        C("armut", "Armut", new Color(0.76f, 0.86f, 0.30f), 300, SeasonBits.Summer | SeasonBits.Autumn, true, 0.9f, 6, true),
        C("cilek", "Çilek", new Color(0.92f, 0.14f, 0.26f), 180, SeasonBits.Spring | SeasonBits.Summer, true, 0.4f, 4, true),
        C("uzum", "Üzüm", new Color(0.52f, 0.20f, 0.62f), 260, SeasonBits.Summer | SeasonBits.Autumn, true, 0.9f, 4, true),
        C("kiraz", "Kiraz", new Color(0.70f, 0.05f, 0.16f), 280, SeasonBits.Spring | SeasonBits.Summer, true, 0.9f, 4, true),
        C("portakal", "Portakal", new Color(1.00f, 0.60f, 0.10f), 320, SeasonBits.Autumn | SeasonBits.Winter, true, 0.9f, 6, true),
        C("bugday", "Buğday", new Color(0.92f, 0.78f, 0.30f), 200, SeasonBits.Spring | SeasonBits.Autumn, false, 0.9f, 0, false),
        C("pamuk", "Pamuk", new Color(0.97f, 0.97f, 0.95f), 260, SeasonBits.Summer | SeasonBits.Autumn, false, 0.8f, 0, false),
        C("aycicegi", "Ayçiçeği", new Color(1.00f, 0.80f, 0.10f), 230, SeasonBits.Summer, false, 1.4f, 0, false),
        C("kabak", "Kabak", new Color(0.95f, 0.55f, 0.10f), 250, SeasonBits.Autumn, false, 0.5f, 5, false),
        C("karpuz", "Karpuz", new Color(0.22f, 0.58f, 0.25f), 280, SeasonBits.Summer, false, 0.5f, 6, false),
        C("kavun", "Kavun", new Color(0.93f, 0.80f, 0.45f), 270, SeasonBits.Summer, false, 0.5f, 6, false),
        C("patlican", "Patlıcan", new Color(0.35f, 0.14f, 0.45f), 220, SeasonBits.Summer, false, 0.7f, 4, false),
    };

    public const string WildSeedKey = "yabani_tohum";

    /// <summary>Yabani tohumdan her bitkinin çıkma şansı (yüzde): 100 / bitki sayısı. Hepsi eşit.</summary>
    public static float WildChancePercent => 100f / Crops.Length;

    public static CropDef RollWildCrop(RandomNumberGenerator rng) => Crops[rng.RandiRange(0, Crops.Length - 1)];

    private static CropDef C(string key, string name, Color col, float grow, int mask, bool perennial, float h, float food, bool fruit)
        => new CropDef
        {
            Key = key, Name = name, Color = col, GrowSeconds = grow, SeasonMask = mask,
            Perennial = perennial, Height = h, Food = food, IsFruit = fruit,
            SeedKey = (fruit && key != "cilek" ? "fidan_" : "tohum_") + key,
        };

    private static readonly Dictionary<string, CropDef> BySeed = new();
    private static readonly Dictionary<string, CropDef> ByKey = new();

    public static CropDef CropForSeed(string seedItemName) => BySeed.TryGetValue(seedItemName, out var c) ? c : null;
    public static CropDef CropByKey(string key) => ByKey.TryGetValue(key, out var c) ? c : null;

    private static readonly Dictionary<string, string> TurkishNames = new()
    {
        { "air", "Hava" }, { "grass", "Çimen" }, { "dirt", "Toprak" }, { "stone", "Taş" },
        { "cobblestone", "Parke Taşı" }, { "sand", "Kum" }, { "sandstone", "Kumtaşı" },
        { "gravel", "Çakıl" }, { "clay", "Kil" }, { "snow", "Kar" },
        { "coal_ore", "Kömür Cevheri" }, { "copper_ore", "Bakır Cevheri" }, { "iron_ore", "Demir Cevheri" },
        { "gold_ore", "Altın Cevheri" }, { "bedrock", "Ana Kaya" }, { "water", "Su" },
        { "oak_log", "Meşe Kütüğü" }, { "planks", "Tahta" }, { "leaves", "Yaprak" }, { "olive_leaves", "Zeytin Yaprağı" },
        { "mud_brick", "Kerpiç" }, { "stone_brick", "Taş Tuğla" }, { "brick", "Tuğla" }, { "plaster", "Sıva" },
        { "thatch", "Saz Çatı" }, { "cloth_red", "Kırmızı Kumaş" }, { "cloth_blue", "Mavi Kumaş" },
        { "cloth_cream", "Krem Kumaş" }, { "gold_block", "Altın Blok" }, { "bronze_block", "Bronz Blok" },
        { "lamp", "Lamba" }, { "altar_fire", "Sunak Ateşi" },
    };

    public static void Define(Func<string, string, BlockType> add)
    {

        ProcTex.Register("farmland_top", ProcTex.Style.Farmland, new Color(0.30f, 0.20f, 0.12f));
        var farm = add("farmland", "Sürülmüş Toprak").SetFaces("farmland_top", "dirt", "dirt");
        farm.Hardness = 0.5f; farm.Material = MaterialSound.Dirt; farm.Preferred = ToolKind.Shovel;
        farm.Drop = "dirt"; farm.Category = ItemCategory.Farming;

        ProcTex.Register("crafting_table_top", ProcTex.Style.TableTop, new Color(0.62f, 0.44f, 0.24f));
        ProcTex.Register("crafting_table_side", ProcTex.Style.Table, new Color(0.55f, 0.38f, 0.20f));
        var table = add("crafting_table", "Üretim Masası").SetFaces("crafting_table_top", "planks", "crafting_table_side");
        table.Hardness = 1.6f; table.Material = MaterialSound.Wood; table.Preferred = ToolKind.Axe;
        table.Category = ItemCategory.Building;

        (string key, string name, Color col)[] wools =
        {
            ("wool_white", "Beyaz Yün Bloğu", new Color(0.93f, 0.93f, 0.92f)),
            ("wool_red", "Kırmızı Yün Bloğu", new Color(0.80f, 0.18f, 0.18f)),
            ("wool_blue", "Mavi Yün Bloğu", new Color(0.20f, 0.35f, 0.80f)),
        };
        foreach (var (key, name, col) in wools)
        {
            ProcTex.Register(key, ProcTex.Style.Wool, col);
            var w = add(key, name).SetFaces(key);
            w.Hardness = 0.4f; w.Material = MaterialSound.Cloth; w.Category = ItemCategory.Decoration;
        }

        ProcTex.Register("chest_top", ProcTex.Style.ChestTop, new Color(0.58f, 0.40f, 0.20f));
        ProcTex.Register("chest_side", ProcTex.Style.ChestSide, new Color(0.58f, 0.40f, 0.20f));
        var chest = add("chest", "Sandık").SetFaces("chest_top", "chest_top", "chest_side");
        chest.Hardness = 1.4f; chest.Material = MaterialSound.Wood; chest.Preferred = ToolKind.Axe;
        chest.Category = ItemCategory.Building;
        ProcTex.Register("furnace_top", ProcTex.Style.FurnaceTop, new Color(0.5f, 0.5f, 0.52f));
        ProcTex.Register("furnace_side", ProcTex.Style.FurnaceSide, new Color(0.5f, 0.5f, 0.52f));
        var furnace = add("furnace", "Fırın").SetFaces("furnace_top", "furnace_top", "furnace_side");
        furnace.Hardness = 2.0f; furnace.Material = MaterialSound.Stone; furnace.Preferred = ToolKind.Pickaxe;
        furnace.Category = ItemCategory.Building;

        ProcTex.Register("campfire", ProcTex.Style.Campfire, new Color(1f, 0.5f, 0.1f));
        var camp = add("campfire", "Kamp Ateşi").SetFaces("campfire");
        camp.Hardness = 0.8f; camp.Material = MaterialSound.Wood; camp.Preferred = ToolKind.Axe;
        camp.Category = ItemCategory.Building;

        foreach (var c in Crops)
        {
            ProcTex.Register("item_" + c.SeedKey, ProcTex.Style.Seed, c.Color);
            var seed = add(c.SeedKey, c.Name + (c.SeedKey.StartsWith("fidan_") ? " Fidanı" : " Tohumu"));
            seed.SetFaces("item_" + c.SeedKey);
            MakeItem(seed, ItemCategory.Farming, c.Color);

            ProcTex.Style st = c.Key == "bugday" || c.Key == "pamuk" || c.Key == "aycicegi" ? ProcTex.Style.Grain
                : (c.IsFruit ? ProcTex.Style.Fruit : ProcTex.Style.Veg);
            ProcTex.Register("item_" + c.Key, st, c.Color);
            var prod = add(c.Key, c.Name);
            prod.SetFaces("item_" + c.Key);
            MakeItem(prod, c.Food > 0 ? ItemCategory.Food : ItemCategory.Farming, c.Color);
            prod.Food = c.Food;

            BySeed[c.SeedKey] = c;
            ByKey[c.Key] = c;
        }

        Item(add, WildSeedKey, "Yabani Tohum", ProcTex.Style.Seed, new Color(0.72f, 0.62f, 0.30f), ItemCategory.Farming);

        Item(add, "komur", "Kömür", ProcTex.Style.Coal, new Color(0.12f, 0.12f, 0.14f), ItemCategory.Natural);
        Item(add, "ham_bakir", "Ham Bakır", ProcTex.Style.Chunk, new Color(0.85f, 0.48f, 0.30f), ItemCategory.Natural);
        Item(add, "ham_demir", "Ham Demir", ProcTex.Style.Chunk, new Color(0.78f, 0.66f, 0.60f), ItemCategory.Natural);
        Item(add, "ham_altin", "Ham Altın", ProcTex.Style.Chunk, new Color(0.98f, 0.82f, 0.25f), ItemCategory.Natural);
        Item(add, "bakir_kulce", "Bakır Külçe", ProcTex.Style.Ingot, new Color(0.88f, 0.50f, 0.30f), ItemCategory.Natural);
        Item(add, "demir_kulce", "Demir Külçe", ProcTex.Style.Ingot, new Color(0.84f, 0.84f, 0.88f), ItemCategory.Natural);
        Item(add, "altin_kulce", "Altın Külçe", ProcTex.Style.Ingot, new Color(1.00f, 0.84f, 0.25f), ItemCategory.Natural);

        Item(add, "sopa", "Çubuk", ProcTex.Style.Stick, new Color(0.60f, 0.42f, 0.22f), ItemCategory.Natural);
        Item(add, "ekmek", "Ekmek", ProcTex.Style.Bread, new Color(0.82f, 0.60f, 0.28f), ItemCategory.Food).Food = 10;
        Item(add, "et", "Çiğ Et", ProcTex.Style.Meat, new Color(0.88f, 0.36f, 0.36f), ItemCategory.Food).Food = 3;
        Item(add, "pismis_et", "Pişmiş Et", ProcTex.Style.Cooked, new Color(0.55f, 0.28f, 0.14f), ItemCategory.Food).Food = 9;
        Item(add, "sut", "Süt", ProcTex.Style.Milk, new Color(0.96f, 0.96f, 0.98f), ItemCategory.Food).Food = 4;
        Item(add, "yumurta", "Yumurta", ProcTex.Style.Egg, new Color(0.96f, 0.92f, 0.82f), ItemCategory.Food).Food = 3;
        var yun = Item(add, "yun", "Yün", ProcTex.Style.Wool, new Color(0.92f, 0.92f, 0.90f), ItemCategory.Natural);
        yun.Placeable = true; yun.Solid = true; yun.Hardness = 0.4f; yun.Material = MaterialSound.Cloth;
        yun.Category = ItemCategory.Decoration;
        Item(add, "gubre", "Gübre", ProcTex.Style.Fert, new Color(0.45f, 0.70f, 0.30f), ItemCategory.Farming);
        Item(add, "kova", "Su Kovası", ProcTex.Style.Bucket, new Color(0.62f, 0.64f, 0.70f), ItemCategory.Farming);

        (string tier, string tr, float speed, float dmg, Color col, int level)[] tiers =
        {
            ("ahsap", "Ahşap", 2.0f, 3f, new Color(0.62f, 0.44f, 0.24f), 1),
            ("tas", "Taş", 3.5f, 5f, new Color(0.62f, 0.62f, 0.64f), 2),
            ("bakir", "Bakır", 4.5f, 6f, new Color(0.88f, 0.50f, 0.30f), 3),
            ("demir", "Demir", 5.5f, 7f, new Color(0.86f, 0.86f, 0.90f), 4),
        };
        foreach (var (tier, tr, speed, dmg, col, level) in tiers)
        {
            Tool(add, tier + "_kazma", tr + " Kazma", ProcTex.Style.Pick, col, ToolKind.Pickaxe, speed, dmg * 0.6f).Tier = level;
            Tool(add, tier + "_balta", tr + " Balta", ProcTex.Style.Axe, col, ToolKind.Axe, speed, dmg * 0.8f).Tier = level;
            Tool(add, tier + "_kurek", tr + " Kürek", ProcTex.Style.Shovel, col, ToolKind.Shovel, speed, dmg * 0.5f).Tier = level;
            Tool(add, tier + "_capa", tr + " Çapa", ProcTex.Style.Hoe, col, ToolKind.Hoe, speed, dmg * 0.4f).Tier = level;
            var sword = Tool(add, tier + "_kilic", tr + " Kılıç", ProcTex.Style.Sword, col, ToolKind.Sword, 1f, dmg + 2f);
            sword.Category = ItemCategory.Weapon;
            sword.Tier = level;
        }

        (string tier, string tr, Color col, float[] pts)[] armors =
        {
            ("bakir", "Bakır", new Color(0.88f, 0.50f, 0.30f), new[] { 1f, 3f, 2f, 1f }),
            ("demir", "Demir", new Color(0.86f, 0.86f, 0.90f), new[] { 2f, 5f, 4f, 2f }),
        };
        foreach (var (tier, tr, col, pts) in armors)
        {
            Armor(add, tier + "_migfer", tr + " Miğfer", ProcTex.Style.Helmet, col, 1, pts[0]);
            Armor(add, tier + "_zirh", tr + " Zırh", ProcTex.Style.Chest, col, 2, pts[1]);
            Armor(add, tier + "_pantolon", tr + " Pantolon", ProcTex.Style.Legs, col, 3, pts[2]);
            Armor(add, tier + "_bot", tr + " Bot", ProcTex.Style.Boots, col, 4, pts[3]);
        }
    }

    private static void MakeItem(BlockType b, ItemCategory cat, Color col)
    {
        b.Placeable = false;
        b.Solid = false;
        b.Category = cat;
        b.IconColor = col;
        b.Hardness = 0.2f;
    }

    private static BlockType Item(Func<string, string, BlockType> add, string key, string name, ProcTex.Style style, Color col, ItemCategory cat)
    {
        ProcTex.Register("item_" + key, style, col);
        var b = add(key, name);
        b.SetFaces("item_" + key);
        MakeItem(b, cat, col);
        return b;
    }

    private static BlockType Armor(Func<string, string, BlockType> add, string key, string name, ProcTex.Style style, Color col, int slot, float points)
    {
        var b = Item(add, key, name, style, col, ItemCategory.Armor);
        b.ArmorSlot = slot;
        b.ArmorValue = points;
        return b;
    }

    private static BlockType Tool(Func<string, string, BlockType> add, string key, string name, ProcTex.Style style, Color col,
        ToolKind kind, float speed, float dmg)
    {
        var b = Item(add, key, name, style, col, ItemCategory.Tool);
        b.Tool = kind;
        b.ToolSpeed = speed;
        b.Damage = dmg;
        return b;
    }

    public static void Finish(Func<string, BlockType> get)
    {
        foreach (var kv in TurkishNames)
        {
            var b = get(kv.Key);
            if (b != null) b.DisplayName = kv.Value;
        }

        SetPreferred(get, ToolKind.Pickaxe, "stone", "cobblestone", "sandstone", "coal_ore", "copper_ore", "iron_ore",
            "gold_ore", "stone_brick", "brick", "bronze_block", "gold_block", "plaster", "mud_brick");
        SetPreferred(get, ToolKind.Axe, "oak_log", "planks", "crafting_table", "thatch");
        SetPreferred(get, ToolKind.Shovel, "dirt", "grass", "sand", "gravel", "clay", "snow", "farmland");

        Drop(get, "grass", "dirt");
        Drop(get, "coal_ore", "komur");
        Drop(get, "copper_ore", "ham_bakir");
        Drop(get, "iron_ore", "ham_demir");
        Drop(get, "gold_ore", "ham_altin");
        MinTier(get, 1, "coal_ore");
        MinTier(get, 2, "copper_ore", "iron_ore");
        MinTier(get, 4, "gold_ore");
        Drop(get, "stone", "cobblestone");

        SetCat(get, ItemCategory.Natural, "grass", "dirt", "stone", "sand", "gravel", "clay", "snow", "coal_ore",
            "copper_ore", "iron_ore", "gold_ore", "oak_log", "leaves", "olive_leaves", "bedrock");
        SetCat(get, ItemCategory.Building, "cobblestone", "sandstone", "planks", "mud_brick", "stone_brick", "brick",
            "plaster", "thatch", "crafting_table");
        SetCat(get, ItemCategory.Decoration, "cloth_red", "cloth_blue", "cloth_cream", "gold_block", "bronze_block",
            "lamp", "altar_fire", "wool_white", "wool_red", "wool_blue");
    }

    private static void SetPreferred(Func<string, BlockType> get, ToolKind k, params string[] names)
    {
        foreach (var n in names) { var b = get(n); if (b != null) b.Preferred = k; }
    }

    private static void MinTier(Func<string, BlockType> get, int tier, params string[] names)
    {
        foreach (var n in names) { var b = get(n); if (b != null) b.MinTier = tier; }
    }

    private static void Drop(Func<string, BlockType> get, string name, string drop)
    {
        var b = get(name);
        if (b != null) b.Drop = drop;
    }

    private static void SetCat(Func<string, BlockType> get, ItemCategory cat, params string[] names)
    {
        foreach (var n in names) { var b = get(n); if (b != null) b.Category = cat; }
    }
}
