using System.Collections.Generic;
using Godot;

namespace PixelBlock.Core;

public static class PixelMat
{
    private static readonly Dictionary<int, StandardMaterial3D> Cache = new();

    public static StandardMaterial3D Get(Color c)
    {
        int key = ((int)(c.R * 255f) << 16) | ((int)(c.G * 255f) << 8) | (int)(c.B * 255f);
        if (Cache.TryGetValue(key, out var m)) return m;

        var img = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            float n = (ValueNoise2D.Hash(x, y, key) % 1000u) / 1000f;
            float k = 0.86f + n * 0.24f;
            img.SetPixel(x, y, new Color(Mathf.Min(1f, c.R * k), Mathf.Min(1f, c.G * k), Mathf.Min(1f, c.B * k), 1f));
        }
        m = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(img),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            Roughness = 1f,
        };
        Cache[key] = m;
        return m;
    }
}
