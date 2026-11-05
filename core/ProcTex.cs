using System.Collections.Generic;
using Godot;

namespace PixelBlock.Core;

public static class ProcTex
{
    public enum Style
    {
        Noise, Farmland, Table, TableTop, Seed, Veg, Fruit, Grain, Pick, Axe, Shovel, Hoe, Sword,
        Bucket, Bread, Meat, Milk, Egg, Wool, Stick, Fert, Campfire, Cooked, Ingot, Chunk, Coal, Helmet, Chest, Legs, Boots, ChestTop, ChestSide, FurnaceTop, FurnaceSide,
    }

    private const int S = 64;
    private static readonly Dictionary<string, (Style style, Color color)> Defs = new();
    private static readonly Color Tile = new(0.17f, 0.19f, 0.25f, 1f);
    private static readonly Color Wood = new(0.55f, 0.38f, 0.20f, 1f);

    public static void Register(string name, Style style, Color color) => Defs[name] = (style, color);
    public static bool Has(string name) => name != null && Defs.ContainsKey(name);

    public static Image TryAlbedo(string name)
    {
        if (name == null || !Defs.TryGetValue(name, out var def)) return null;
        var img = Image.CreateEmpty(S, S, false, Image.Format.Rgba8);
        switch (def.style)
        {
            case Style.Noise: FillNoise(img, def.color, 0.14f); break;
            case Style.Wool: FillNoise(img, def.color, 0.08f); WeaveLines(img); break;
            case Style.Farmland: DrawFarmland(img, def.color); break;
            case Style.Campfire: DrawCampfire(img); break;
            case Style.ChestTop: DrawChest(img, true); break;
            case Style.ChestSide: DrawChest(img, false); break;
            case Style.FurnaceTop: DrawFurnace(img, true); break;
            case Style.FurnaceSide: DrawFurnace(img, false); break;
            case Style.Table: DrawTable(img, def.color, false); break;
            case Style.TableTop: DrawTable(img, def.color, true); break;
            default: DrawItem(img, def.style, def.color); break;
        }
        return img;
    }

    private static void FillNoise(Image img, Color c, float amount)
    {
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float n = (ValueNoise2D.Hash(x, y, 77) % 1000u) / 1000f - 0.5f;
            float k = 1f + n * amount * 2f;
            img.SetPixel(x, y, new Color(Mathf.Clamp(c.R * k, 0f, 1f), Mathf.Clamp(c.G * k, 0f, 1f), Mathf.Clamp(c.B * k, 0f, 1f), 1f));
        }
    }

    private static void WeaveLines(Image img)
    {
        for (int y = 0; y < S; y += 8)
        for (int x = 0; x < S; x++)
        {
            Color c = img.GetPixel(x, y);
            img.SetPixel(x, y, new Color(c.R * 0.88f, c.G * 0.88f, c.B * 0.88f, 1f));
        }
    }

    private static void DrawFarmland(Image img, Color c)
    {
        FillNoise(img, c, 0.10f);
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            bool groove = (y % 16) < 4;
            if (!groove) continue;
            Color p = img.GetPixel(x, y);
            img.SetPixel(x, y, new Color(p.R * 0.62f, p.G * 0.62f, p.B * 0.62f, 1f));
        }
    }

    private static void DrawChest(Image img, bool top)
    {
        Color wood = new(0.58f, 0.40f, 0.20f);
        FillNoise(img, wood, 0.08f);
        Color dark = new(0.30f, 0.19f, 0.09f);
        // çerçeve
        FillRect(img, 0, 0, S, 5, dark); FillRect(img, 0, S - 5, S, S, dark);
        FillRect(img, 0, 0, 5, S, dark); FillRect(img, S - 5, 0, S, S, dark);
        if (top)
        {
            FillRect(img, 5, 30, S - 5, 34, dark);
            return;
        }
        FillRect(img, 5, 26, S - 5, 31, dark);
        FillRect(img, 27, 24, 37, 38, new Color(0.88f, 0.78f, 0.32f));
        FillRect(img, 30, 29, 34, 35, new Color(0.18f, 0.14f, 0.05f));
    }

    private static void DrawFurnace(Image img, bool top)
    {
        FillNoise(img, new Color(0.50f, 0.50f, 0.52f), 0.10f);
        Color edge = new(0.32f, 0.32f, 0.35f);
        FillRect(img, 0, 0, S, 4, edge); FillRect(img, 0, S - 4, S, S, edge);
        FillRect(img, 0, 0, 4, S, edge); FillRect(img, S - 4, 0, S, S, edge);
        if (top)
        {
            FillRect(img, 18, 18, 46, 46, new Color(0.12f, 0.12f, 0.14f));
            FillRect(img, 22, 22, 42, 42, new Color(0.22f, 0.20f, 0.20f));
            return;
        }
        FillRect(img, 14, 28, 50, 54, new Color(0.10f, 0.10f, 0.12f));
        FillRect(img, 18, 42, 46, 54, new Color(0.95f, 0.45f, 0.08f));
        FillRect(img, 24, 46, 40, 54, new Color(1.00f, 0.80f, 0.25f));
        for (int x = 14; x < 50; x += 6) FillRect(img, x, 28, x + 2, 42, edge);
        FillRect(img, 14, 14, 50, 22, edge);
    }

    private static void DrawCampfire(Image img)
    {
        FillNoise(img, new Color(0.20f, 0.17f, 0.15f), 0.10f);
        Color log = new(0.42f, 0.28f, 0.14f);
        Color logLight = new(0.55f, 0.38f, 0.20f);
        Line(img, 6, 52, 58, 40, 9, log); Line(img, 6, 50, 58, 38, 2, logLight);
        Line(img, 8, 36, 56, 56, 9, log); Line(img, 8, 34, 56, 54, 2, logLight);
        FillCircle(img, 32, 40, 14, new Color(0.95f, 0.38f, 0.08f));
        FillCircle(img, 32, 36, 10, new Color(1.00f, 0.62f, 0.12f));
        FillCircle(img, 32, 32, 6, new Color(1.00f, 0.90f, 0.40f));
        Line(img, 32, 26, 32, 8, 7, new Color(1.00f, 0.55f, 0.10f));
        Line(img, 32, 24, 32, 14, 3, new Color(1.00f, 0.88f, 0.35f));
    }

    private static void DrawTable(Image img, Color c, bool top)
    {
        FillNoise(img, c, 0.08f);

        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            bool line = top ? (x % 32 == 0 || y % 32 == 0) : (y % 16 == 0);
            if (line)
            {
                Color p = img.GetPixel(x, y);
                img.SetPixel(x, y, new Color(p.R * 0.6f, p.G * 0.6f, p.B * 0.6f, 1f));
            }
        }
        if (!top) FillRect(img, 10, 22, 22, 30, new Color(0.35f, 0.35f, 0.4f));
        else FillRect(img, 20, 20, 44, 44, new Color(0.5f, 0.34f, 0.17f));
    }

    private static void DrawItem(Image img, Style style, Color c)
    {
        img.Fill(Tile);

        for (int i = 0; i < S; i++)
        {
            Color edge = new(0.3f, 0.33f, 0.4f, 1f);
            img.SetPixel(i, 0, edge); img.SetPixel(i, S - 1, edge);
            img.SetPixel(0, i, edge); img.SetPixel(S - 1, i, edge);
        }
        Color dark = new(c.R * 0.6f, c.G * 0.6f, c.B * 0.6f, 1f);
        Color light = new(Mathf.Min(1f, c.R * 1.25f + 0.05f), Mathf.Min(1f, c.G * 1.25f + 0.05f), Mathf.Min(1f, c.B * 1.25f + 0.05f), 1f);
        Color leaf = new(0.25f, 0.65f, 0.22f, 1f);
        switch (style)
        {
            case Style.Seed:
                int[,] dots = { { 18, 40 }, { 30, 28 }, { 42, 44 }, { 26, 48 }, { 44, 26 }, { 34, 38 } };
                for (int i = 0; i < dots.GetLength(0); i++) FillCircle(img, dots[i, 0], dots[i, 1], 4, c);
                Line(img, 32, 18, 32, 10, 2, leaf); FillCircle(img, 28, 10, 3, leaf); FillCircle(img, 36, 10, 3, leaf);
                break;
            case Style.Veg:
                FillCircle(img, 32, 36, 19, c); FillCircle(img, 26, 30, 6, light);
                Line(img, 32, 17, 32, 9, 3, leaf); FillCircle(img, 27, 12, 4, leaf); FillCircle(img, 37, 12, 4, leaf);
                break;
            case Style.Fruit:
                FillCircle(img, 32, 37, 18, c); FillCircle(img, 25, 31, 5, light);
                Line(img, 32, 20, 34, 10, 3, Wood); FillCircle(img, 40, 13, 5, leaf);
                break;
            case Style.Grain:
                for (int k = 0; k < 3; k++)
                {
                    int x = 18 + k * 14;
                    Line(img, x, 56, x, 22, 2, new Color(0.65f, 0.55f, 0.2f));
                    for (int j = 0; j < 5; j++) FillCircle(img, x + (j % 2 == 0 ? -3 : 3), 20 + j * 4, 3, c);
                }
                break;
            case Style.Stick:
                Line(img, 14, 52, 50, 14, 5, Wood); Line(img, 16, 50, 48, 16, 1, new Color(0.7f, 0.5f, 0.28f));
                break;
            case Style.Pick:
                Line(img, 16, 52, 44, 24, 5, Wood);
                Line(img, 20, 22, 54, 30, 7, c); Line(img, 22, 20, 50, 46, 7, c);
                break;
            case Style.Axe:
                Line(img, 16, 52, 44, 20, 5, Wood);
                FillRect(img, 34, 10, 56, 28, c); FillRect(img, 34, 10, 56, 14, light);
                break;
            case Style.Shovel:
                Line(img, 14, 54, 38, 24, 5, Wood);
                FillCircle(img, 46, 16, 12, c); FillCircle(img, 43, 13, 4, light);
                break;
            case Style.Hoe:
                Line(img, 14, 54, 40, 20, 5, Wood);
                Line(img, 36, 14, 56, 14, 6, c); Line(img, 54, 14, 54, 28, 5, c);
                break;
            case Style.Sword:
                Line(img, 18, 46, 52, 12, 7, c); Line(img, 20, 44, 50, 14, 2, light);
                Line(img, 12, 40, 26, 54, 5, Wood); Line(img, 18, 50, 12, 56, 5, Wood);
                break;
            case Style.Bucket:
                FillRect(img, 16, 24, 48, 52, c); FillRect(img, 16, 24, 48, 30, light);
                FillRect(img, 20, 30, 44, 36, new Color(0.25f, 0.45f, 0.9f));
                Line(img, 16, 24, 32, 10, 2, dark); Line(img, 48, 24, 32, 10, 2, dark);
                break;
            case Style.Bread:
                FillCircle(img, 32, 36, 20, c); FillRect(img, 12, 36, 52, 52, c);
                Line(img, 22, 28, 30, 36, 2, dark); Line(img, 34, 26, 42, 34, 2, dark);
                break;
            case Style.Meat:
                FillCircle(img, 28, 34, 16, c); FillCircle(img, 24, 30, 5, light);
                Line(img, 38, 44, 52, 54, 5, new Color(0.95f, 0.92f, 0.85f)); FillCircle(img, 54, 54, 4, new Color(0.95f, 0.92f, 0.85f));
                break;
            case Style.Cooked:
                FillCircle(img, 28, 34, 16, c); FillCircle(img, 24, 30, 5, light);
                Line(img, 18, 40, 38, 24, 2, dark); Line(img, 24, 46, 42, 30, 2, dark);
                Line(img, 38, 44, 52, 54, 5, new Color(0.95f, 0.92f, 0.85f)); FillCircle(img, 54, 54, 4, new Color(0.95f, 0.92f, 0.85f));
                break;
            case Style.Ingot:
                FillRect(img, 10, 26, 54, 46, c); FillRect(img, 10, 26, 54, 31, light); FillRect(img, 10, 42, 54, 46, dark);
                FillRect(img, 14, 20, 50, 26, dark); FillRect(img, 14, 20, 50, 23, c);
                break;
            case Style.Chunk:
            case Style.Coal:
                Color cc = style == Style.Coal ? new Color(0.12f, 0.12f, 0.14f) : c;
                Color cl = style == Style.Coal ? new Color(0.35f, 0.35f, 0.4f) : light;
                FillCircle(img, 26, 38, 12, cc); FillCircle(img, 40, 34, 13, cc); FillCircle(img, 34, 44, 10, cc);
                FillCircle(img, 28, 32, 4, cl); FillCircle(img, 42, 28, 3, cl);
                break;
            case Style.Helmet:
                FillRect(img, 14, 16, 50, 38, c); FillRect(img, 14, 38, 24, 52, c); FillRect(img, 40, 38, 50, 52, c);
                FillRect(img, 14, 16, 50, 21, light); FillRect(img, 24, 38, 40, 44, dark);
                break;
            case Style.Chest:
                FillRect(img, 12, 14, 52, 54, c); FillRect(img, 6, 14, 16, 36, c); FillRect(img, 48, 14, 58, 36, c);
                FillRect(img, 26, 14, 38, 22, Tile); FillRect(img, 12, 14, 52, 18, light); FillRect(img, 30, 22, 34, 54, dark);
                break;
            case Style.Legs:
                FillRect(img, 14, 12, 50, 26, c); FillRect(img, 14, 26, 29, 54, c); FillRect(img, 35, 26, 50, 54, c);
                FillRect(img, 14, 12, 50, 16, light);
                break;
            case Style.Boots:
                FillRect(img, 8, 26, 24, 46, c); FillRect(img, 8, 46, 30, 54, c);
                FillRect(img, 36, 26, 52, 46, c); FillRect(img, 36, 46, 58, 54, c);
                FillRect(img, 8, 26, 24, 30, light); FillRect(img, 36, 26, 52, 30, light);
                break;
            case Style.Milk:
                FillRect(img, 20, 20, 44, 54, c); FillRect(img, 20, 20, 44, 28, new Color(0.3f, 0.5f, 0.9f));
                break;
            case Style.Egg:
                FillCircle(img, 32, 36, 14, c); FillCircle(img, 32, 28, 12, c); FillCircle(img, 27, 28, 4, light);
                break;
            case Style.Wool:
                FillCircle(img, 24, 34, 12, c); FillCircle(img, 38, 30, 14, c); FillCircle(img, 34, 42, 12, c);
                break;
            case Style.Fert:
                FillRect(img, 16, 22, 48, 54, new Color(0.45f, 0.32f, 0.2f)); FillRect(img, 16, 22, 48, 30, c);
                FillCircle(img, 32, 40, 6, leaf);
                break;
            default:
                FillCircle(img, 32, 32, 18, c);
                break;
        }
    }

    private static void Put(Image img, int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= S || y >= S) return;
        img.SetPixel(x, y, c);
    }

    private static void FillRect(Image img, int x0, int y0, int x1, int y1, Color c)
    {
        for (int y = y0; y < y1; y++)
        for (int x = x0; x < x1; x++) Put(img, x, y, c);
    }

    private static void FillCircle(Image img, int cx, int cy, int r, Color c)
    {
        for (int y = -r; y <= r; y++)
        for (int x = -r; x <= r; x++)
            if (x * x + y * y <= r * r) Put(img, cx + x, cy + y, c);
    }

    private static void Line(Image img, int x0, int y0, int x1, int y1, int thick, Color c)
    {
        int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
        if (steps == 0) steps = 1;
        int half = thick / 2;
        for (int i = 0; i <= steps; i++)
        {
            int x = x0 + (x1 - x0) * i / steps;
            int y = y0 + (y1 - y0) * i / steps;
            for (int dy = -half; dy <= half; dy++)
            for (int dx = -half; dx <= half; dx++) Put(img, x + dx, y + dy, c);
        }
    }
}
