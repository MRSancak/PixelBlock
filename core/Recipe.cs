namespace PixelBlock.Core;

public sealed class Recipe
{
    public string Name;

    public string Display;

    public bool NeedsTable;
    public bool NeedsHeat;
    public (string block, int count)[] Inputs;
    public (string block, int count) Output;

    public Recipe(string name, (string, int) output, params (string block, int count)[] inputs)
    {
        Name = name;
        Output = output;
        Inputs = inputs;
    }
}

public static class CraftBook
{
    public static readonly Recipe[] All =
    {

        new("Wood Planks", ("planks", 4), ("oak_log", 1)) { Display = "Tahta" },
        new("Sticks", ("sopa", 4), ("planks", 2)) { Display = "Çubuk" },
        new("Crafting Table", ("crafting_table", 1), ("planks", 4)) { Display = "Üretim Masası" },
        new("Stone Bricks", ("stone_brick", 4), ("cobblestone", 4)) { Display = "Taş Tuğla" },
        new("Cobblestone", ("cobblestone", 1), ("stone", 1)) { Display = "Parke Taşı" },
        new("Sandstone", ("sandstone", 1), ("sand", 4)) { Display = "Kumtaşı" },
        new("Clay Bricks", ("brick", 4), ("clay", 4)) { Display = "Tuğla" },
        new("Mud Bricks", ("mud_brick", 4), ("dirt", 4)) { Display = "Kerpiç" },
        new("Thatch", ("thatch", 2), ("leaves", 4)) { Display = "Saz Çatı" },
        new("Lamp", ("lamp", 1), ("planks", 4), ("gold_block", 1)) { Display = "Lamba" },
        new("Bread", ("ekmek", 1), ("bugday", 3)) { Display = "Ekmek" },
        new("Chest", ("chest", 1), ("planks", 8)) { Display = "Sandık" },
        new("Furnace", ("furnace", 1), ("cobblestone", 8)) { Display = "Fırın" },
        new("Campfire", ("campfire", 1), ("oak_log", 2), ("cobblestone", 3)) { Display = "Kamp Ateşi" },
        new("Smelt Copper", ("bakir_kulce", 1), ("ham_bakir", 1), ("komur", 1)) { Display = "Bakır Külçe", NeedsHeat = true },
        new("Smelt Iron", ("demir_kulce", 1), ("ham_demir", 1), ("komur", 1)) { Display = "Demir Külçe", NeedsHeat = true },
        new("Smelt Gold", ("altin_kulce", 1), ("ham_altin", 1), ("komur", 1)) { Display = "Altın Külçe", NeedsHeat = true },
        new("Gold Block", ("gold_block", 1), ("altin_kulce", 4)) { Display = "Altın Blok" },
        new("Cooked Meat", ("pismis_et", 1), ("et", 1)) { Display = "Pişmiş Et", NeedsHeat = true },
        new("Fertilizer", ("gubre", 2), ("leaves", 4), ("dirt", 2)) { Display = "Gübre" },
        new("White Wool", ("wool_white", 1), ("yun", 4)) { Display = "Beyaz Yün Bloğu" },
        new("Red Wool", ("wool_red", 1), ("yun", 4), ("domates", 1)) { Display = "Kırmızı Yün Bloğu" },
        new("Blue Wool", ("wool_blue", 1), ("yun", 4), ("uzum", 1)) { Display = "Mavi Yün Bloğu" },

        new("Wooden Pickaxe", ("ahsap_kazma", 1), ("planks", 3), ("sopa", 2)) { Display = "Ahşap Kazma", NeedsTable = true },
        new("Wooden Axe", ("ahsap_balta", 1), ("planks", 3), ("sopa", 2)) { Display = "Ahşap Balta", NeedsTable = true },
        new("Wooden Shovel", ("ahsap_kurek", 1), ("planks", 1), ("sopa", 2)) { Display = "Ahşap Kürek", NeedsTable = true },
        new("Wooden Hoe", ("ahsap_capa", 1), ("planks", 2), ("sopa", 2)) { Display = "Ahşap Çapa", NeedsTable = true },
        new("Wooden Sword", ("ahsap_kilic", 1), ("planks", 2), ("sopa", 1)) { Display = "Ahşap Kılıç", NeedsTable = true },
        new("Stone Pickaxe", ("tas_kazma", 1), ("cobblestone", 3), ("sopa", 2)) { Display = "Taş Kazma", NeedsTable = true },
        new("Stone Axe", ("tas_balta", 1), ("cobblestone", 3), ("sopa", 2)) { Display = "Taş Balta", NeedsTable = true },
        new("Stone Shovel", ("tas_kurek", 1), ("cobblestone", 1), ("sopa", 2)) { Display = "Taş Kürek", NeedsTable = true },
        new("Stone Hoe", ("tas_capa", 1), ("cobblestone", 2), ("sopa", 2)) { Display = "Taş Çapa", NeedsTable = true },
        new("Stone Sword", ("tas_kilic", 1), ("cobblestone", 2), ("sopa", 1)) { Display = "Taş Kılıç", NeedsTable = true },
        new("Copper Pickaxe", ("bakir_kazma", 1), ("bakir_kulce", 3), ("sopa", 2)) { Display = "Bakır Kazma", NeedsTable = true },
        new("Copper Axe", ("bakir_balta", 1), ("bakir_kulce", 3), ("sopa", 2)) { Display = "Bakır Balta", NeedsTable = true },
        new("Copper Shovel", ("bakir_kurek", 1), ("bakir_kulce", 1), ("sopa", 2)) { Display = "Bakır Kürek", NeedsTable = true },
        new("Copper Hoe", ("bakir_capa", 1), ("bakir_kulce", 2), ("sopa", 2)) { Display = "Bakır Çapa", NeedsTable = true },
        new("Copper Sword", ("bakir_kilic", 1), ("bakir_kulce", 2), ("sopa", 1)) { Display = "Bakır Kılıç", NeedsTable = true },
        new("Iron Pickaxe", ("demir_kazma", 1), ("demir_kulce", 3), ("sopa", 2)) { Display = "Demir Kazma", NeedsTable = true },
        new("Iron Axe", ("demir_balta", 1), ("demir_kulce", 3), ("sopa", 2)) { Display = "Demir Balta", NeedsTable = true },
        new("Iron Shovel", ("demir_kurek", 1), ("demir_kulce", 1), ("sopa", 2)) { Display = "Demir Kürek", NeedsTable = true },
        new("Iron Hoe", ("demir_capa", 1), ("demir_kulce", 2), ("sopa", 2)) { Display = "Demir Çapa", NeedsTable = true },
        new("Iron Sword", ("demir_kilic", 1), ("demir_kulce", 2), ("sopa", 1)) { Display = "Demir Kılıç", NeedsTable = true },
        new("Bakır Miğfer", ("bakir_migfer", 1), ("bakir_kulce", 5)) { Display = "Bakır Miğfer", NeedsTable = true },
        new("Bakır Zırh", ("bakir_zirh", 1), ("bakir_kulce", 8)) { Display = "Bakır Zırh", NeedsTable = true },
        new("Bakır Pantolon", ("bakir_pantolon", 1), ("bakir_kulce", 7)) { Display = "Bakır Pantolon", NeedsTable = true },
        new("Bakır Bot", ("bakir_bot", 1), ("bakir_kulce", 4)) { Display = "Bakır Bot", NeedsTable = true },
        new("Demir Miğfer", ("demir_migfer", 1), ("demir_kulce", 5)) { Display = "Demir Miğfer", NeedsTable = true },
        new("Demir Zırh", ("demir_zirh", 1), ("demir_kulce", 8)) { Display = "Demir Zırh", NeedsTable = true },
        new("Demir Pantolon", ("demir_pantolon", 1), ("demir_kulce", 7)) { Display = "Demir Pantolon", NeedsTable = true },
        new("Demir Bot", ("demir_bot", 1), ("demir_kulce", 4)) { Display = "Demir Bot", NeedsTable = true },
        new("Bucket", ("kova", 1), ("demir_kulce", 3)) { Display = "Su Kovası", NeedsTable = true },
    };

    public static string DisplayOf(Recipe r) =>
        string.IsNullOrEmpty(r.Display) ? BlockRegistry.Get(r.Output.block).DisplayName : r.Display;
}
