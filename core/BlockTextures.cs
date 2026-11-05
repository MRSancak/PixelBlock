using System.Collections.Generic;
using Godot;

namespace PixelBlock.Core;

public sealed class BlockTextures
{
    public const int TexSize = 64;
    private const string Root = "res://assets/textures/blocks";

    public Texture2DArray Albedo;
    public Texture2DArray Normal;
    public Texture2DArray Orm;
    public Texture2DArray Emission;
    public ShaderMaterial Material;
    public int LayerCount { get; private set; }

    private readonly Dictionary<string, int> _layerOf = new();

    private static readonly HashSet<string> SeasonLayerNames = new() { "grass_top", "grass_side", "leaves", "olive_leaves" };
    private readonly Dictionary<string, Image> _seasonSrc = new();
    private int _appliedSeason = -1;

    public void ApplySeason(int season)
    {
        season = ((season % 4) + 4) % 4;
        if (season == _appliedSeason || Albedo == null) return;
        _appliedSeason = season;
        foreach (var kv in _seasonSrc)
        {
            if (!_layerOf.TryGetValue(kv.Key, out int layer)) continue;
            bool leafy = kv.Key.Contains("leaves");
            Color tint = SeasonTint(season, leafy);
            var img = (Image)kv.Value.Duplicate();
            for (int y = 0; y < img.GetHeight(); y++)
            for (int x = 0; x < img.GetWidth(); x++)
            {
                Color c = img.GetPixel(x, y);
                img.SetPixel(x, y, new Color(Mathf.Min(1f, c.R * tint.R), Mathf.Min(1f, c.G * tint.G), Mathf.Min(1f, c.B * tint.B), c.A));
            }
            img.GenerateMipmaps();
            Albedo.UpdateLayer(img, layer);
        }
    }

    private static Color SeasonTint(int season, bool leaves) => season switch
    {
        0 => leaves ? new Color(1.00f, 1.12f, 0.95f) : new Color(0.95f, 1.10f, 0.90f),
        2 => leaves ? new Color(1.70f, 0.85f, 0.35f) : new Color(1.25f, 0.95f, 0.55f),
        3 => leaves ? new Color(0.85f, 0.80f, 0.85f) : new Color(1.12f, 1.16f, 1.28f),
        _ => Colors.White,
    };

    public static BlockTextures Build()
    {
        BlockRegistry.EnsureInit();
        var bt = new BlockTextures();
        bt.Load();
        return bt;
    }

    private void Load()
    {

        var names = new List<string>();
        var seen = new HashSet<string>();
        foreach (var block in BlockRegistry.All)
        foreach (var t in block.FaceTex)
            if (!string.IsNullOrEmpty(t) && seen.Add(t))
                names.Add(t);
        names.Sort();

        var albedos = new Godot.Collections.Array<Image>();
        var normals = new Godot.Collections.Array<Image>();
        var orms = new Godot.Collections.Array<Image>();
        var emissions = new Godot.Collections.Array<Image>();

        for (int i = 0; i < names.Count; i++)
        {
            string n = names[i];
            _layerOf[n] = i;

            Image alb = ProcTex.TryAlbedo(n) ?? LoadOr($"{Root}/{n}/albedo.png", Image.Format.Rgba8, new Color(0.8f, 0.2f, 0.8f));
            if (SeasonLayerNames.Contains(n)) _seasonSrc[n] = (Image)alb.Duplicate();
            albedos.Add(WithMips(alb));
            normals.Add(WithMips(LoadOr($"{Root}/{n}/normal.png", Image.Format.Rgb8, new Color(0.5f, 0.5f, 1f))));
            orms.Add(WithMips(BuildOrm(n)));
            emissions.Add(WithMips(LoadOr($"{Root}/{n}/emission.png", Image.Format.Rgb8, Colors.Black)));
        }

        LayerCount = names.Count;
        Albedo = MakeArray(albedos);
        Normal = MakeArray(normals);
        Orm = MakeArray(orms);
        Emission = MakeArray(emissions);

        foreach (var block in BlockRegistry.All)
        for (int f = 0; f < 6; f++)
        {
            string t = block.FaceTex[f];
            block.FaceLayer[f] = (t != null && _layerOf.TryGetValue(t, out int l)) ? l : 0;
        }

        var shader = GD.Load<Shader>("res://assets/shaders/voxel.gdshader");
        Material = new ShaderMaterial { Shader = shader };
        Material.SetShaderParameter("albedo_tex", Albedo);
        Material.SetShaderParameter("normal_tex", Normal);
        Material.SetShaderParameter("orm_tex", Orm);
        Material.SetShaderParameter("emission_tex", Emission);

        GD.Print($"[Blocks] Built texture arrays: {LayerCount} layers @ {TexSize}px.");
    }

    public int LayerFor(string name) => _layerOf.TryGetValue(name, out int l) ? l : 0;

    private readonly Dictionary<ushort, Color> _avgColor = new();

    public Color AverageColor(BlockType b)
    {
        if (_avgColor.TryGetValue(b.Id, out var cached)) return cached;
        var avg = new Color(0.7f, 0.7f, 0.7f);
        Image img = GetIcon(b)?.GetImage();
        if (img != null)
        {
            if (img.IsCompressed()) img.Decompress();
            int w = img.GetWidth(), h = img.GetHeight();
            int sx = Mathf.Max(1, w / 8), sy = Mathf.Max(1, h / 8);
            double r = 0, g = 0, bl = 0; int n = 0;
            for (int y = 0; y < h; y += sy)
            for (int x = 0; x < w; x += sx)
            {
                Color px = img.GetPixel(x, y);
                r += px.R; g += px.G; bl += px.B; n++;
            }
            if (n > 0) avg = new Color((float)(r / n), (float)(g / n), (float)(bl / n));
        }
        _avgColor[b.Id] = avg;
        return avg;
    }

    private readonly Dictionary<ushort, Texture2D> _icons = new();

    public Texture2D GetIcon(BlockType b)
    {
        if (_icons.TryGetValue(b.Id, out var cached)) return cached;
        string name = b.FaceTex[(int)Face.PosZ] ?? b.FaceTex[(int)Face.PosY];
        Texture2D tex = null;
        if (!string.IsNullOrEmpty(name))
        {
            var img = LoadPng($"{Root}/{name}/albedo.png") ?? ProcTex.TryAlbedo(name);
            if (img != null) tex = ImageTexture.CreateFromImage(img);
        }
        _icons[b.Id] = tex;
        return tex;
    }

    private static Texture2DArray MakeArray(Godot.Collections.Array<Image> imgs)
    {
        var arr = new Texture2DArray();
        arr.CreateFromImages(imgs);
        return arr;
    }

    private static Image WithMips(Image img)
    {
        img.GenerateMipmaps();
        return img;
    }

    private static Image LoadPng(string path)
    {
        if (!FileAccess.FileExists(path)) return null;
        byte[] bytes = FileAccess.GetFileAsBytes(path);
        if (bytes == null || bytes.Length == 0) return null;
        var img = new Image();
        return img.LoadPngFromBuffer(bytes) == Error.Ok ? img : null;
    }

    private static Image LoadOr(string path, Image.Format fmt, Color fallback)
    {
        Image img = LoadPng(path);
        if (img == null)
        {
            img = Image.CreateEmpty(TexSize, TexSize, false, fmt);
            img.Fill(fallback);
            return img;
        }
        if (img.GetWidth() != TexSize || img.GetHeight() != TexSize)
            img.Resize(TexSize, TexSize, Image.Interpolation.Lanczos);
        if (img.GetFormat() != fmt)
            img.Convert(fmt);
        return img;
    }

    private Image BuildOrm(string name)
    {
        Image ao = LoadGray($"{Root}/{name}/ao.png", 1f);
        Image rough = LoadGray($"{Root}/{name}/roughness.png", 0.85f);
        Image metal = LoadGray($"{Root}/{name}/metallic.png", 0f);

        var orm = Image.CreateEmpty(TexSize, TexSize, false, Image.Format.Rgb8);
        for (int y = 0; y < TexSize; y++)
        for (int x = 0; x < TexSize; x++)
            orm.SetPixel(x, y, new Color(ao.GetPixel(x, y).R, rough.GetPixel(x, y).R, metal.GetPixel(x, y).R));
        return orm;
    }

    private static Image LoadGray(string path, float fallback)
    {
        Image img = LoadPng(path);
        if (img == null)
        {
            img = Image.CreateEmpty(TexSize, TexSize, false, Image.Format.L8);
            img.Fill(new Color(fallback, fallback, fallback));
            return img;
        }
        if (img.GetWidth() != TexSize || img.GetHeight() != TexSize)
            img.Resize(TexSize, TexSize, Image.Interpolation.Lanczos);
        return img;
    }
}
