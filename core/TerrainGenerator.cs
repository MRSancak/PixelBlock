using Godot;

namespace PixelBlock.Core;

public sealed class ValueNoise2D
{
    private readonly int _seed;
    public ValueNoise2D(int seed) => _seed = seed;

    public static uint Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)seed * 0x9E3779B1u;
            h ^= (uint)x * 0x85EBCA77u;
            h = (h ^ (h >> 15)) * 0xC2B2AE3Du;
            h ^= (uint)y * 0x27D4EB2Fu;
            h = (h ^ (h >> 13)) * 0x165667B1u;
            return h ^ (h >> 16);
        }
    }

    private float Corner(int x, int y) => (Hash(x, y, _seed) & 0xFFFFFF) / (float)0xFFFFFF;
    private static float Fade(float t) => t * t * (3f - 2f * t);

    public float Noise(float x, float y)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = Fade(x - x0), fy = Fade(y - y0);
        float v00 = Corner(x0, y0), v10 = Corner(x0 + 1, y0);
        float v01 = Corner(x0, y0 + 1), v11 = Corner(x0 + 1, y0 + 1);
        return Mathf.Lerp(Mathf.Lerp(v00, v10, fx), Mathf.Lerp(v01, v11, fx), fy);
    }

    public float Fractal(float x, float y, int octaves, float lacunarity = 2f, float gain = 0.5f)
    {
        float sum = 0f, amp = 1f, freq = 1f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += amp * Noise(x * freq, y * freq);
            norm += amp;
            amp *= gain;
            freq *= lacunarity;
        }
        return norm > 0f ? sum / norm : 0f;
    }

    public static uint Hash(int x, int y, int z, int seed)
    {
        unchecked
        {
            uint h = (uint)seed * 0x9E3779B1u;
            h ^= (uint)x * 0x85EBCA77u;
            h = (h ^ (h >> 15)) * 0xC2B2AE3Du;
            h ^= (uint)y * 0x27D4EB2Fu;
            h = (h ^ (h >> 13)) * 0x165667B1u;
            h ^= (uint)z * 0x9E3779B1u;
            h = (h ^ (h >> 16)) * 0x85EBCA77u;
            return h ^ (h >> 15);
        }
    }

    private float Corner3(int x, int y, int z) => (Hash(x, y, z, _seed) & 0xFFFFFF) / (float)0xFFFFFF;

    public float Noise3(float x, float y, float z)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y), z0 = Mathf.FloorToInt(z);
        float fx = Fade(x - x0), fy = Fade(y - y0), fz = Fade(z - z0);
        float c000 = Corner3(x0, y0, z0), c100 = Corner3(x0 + 1, y0, z0);
        float c010 = Corner3(x0, y0 + 1, z0), c110 = Corner3(x0 + 1, y0 + 1, z0);
        float c001 = Corner3(x0, y0, z0 + 1), c101 = Corner3(x0 + 1, y0, z0 + 1);
        float c011 = Corner3(x0, y0 + 1, z0 + 1), c111 = Corner3(x0 + 1, y0 + 1, z0 + 1);
        float x00 = Mathf.Lerp(c000, c100, fx), x10 = Mathf.Lerp(c010, c110, fx);
        float x01 = Mathf.Lerp(c001, c101, fx), x11 = Mathf.Lerp(c011, c111, fx);
        float y0l = Mathf.Lerp(x00, x10, fy), y1l = Mathf.Lerp(x01, x11, fy);
        return Mathf.Lerp(y0l, y1l, fz);
    }

    public float Fractal3(float x, float y, float z, int octaves, float lacunarity = 2f, float gain = 0.5f)
    {
        float sum = 0f, amp = 1f, freq = 1f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += amp * Noise3(x * freq, y * freq, z * freq);
            norm += amp;
            amp *= gain;
            freq *= lacunarity;
        }
        return norm > 0f ? sum / norm : 0f;
    }
}

public enum Biome { Ocean, Beach, Plains, Forest, Desert, Snow, Mountains, Swamp, DenseForest, Meadow }

public sealed class TerrainGenerator : VoxelWorld.IChunkGenerator
{
    public int Seed { get; }

    public const int SeaLevel = 26;
    private const int BaseHeight = 16;
    private const float HeightRange = 46f;
    private const int SnowLine = SeaLevel + 24;
    private const float ElevFreq = 1f / 150f;
    private const float ClimateFreq = 1f / 320f;
    private const int TreeGrid = 5;

    private const int MinWorldY = -16;

    private readonly ValueNoise2D _elev, _detail, _warmth, _moisture, _river;
    private readonly ValueNoise2D _oreCoal, _oreCopper, _oreIron, _oreGold, _caveN;
    private readonly ushort _grass, _dirt, _stone, _sand, _sandstone, _gravel, _water, _snow, _log, _leaves;
    private readonly ushort _coal, _copper, _iron, _gold, _bedrock, _clay;

    public TerrainGenerator(int seed)
    {
        Seed = seed;
        BlockRegistry.EnsureInit();
        _elev = new ValueNoise2D(seed);
        _detail = new ValueNoise2D(seed + 17);
        _warmth = new ValueNoise2D(seed + 101);
        _moisture = new ValueNoise2D(seed + 211);
        _river = new ValueNoise2D(seed + 523);
        _oreCoal = new ValueNoise2D(seed + 331);
        _oreCopper = new ValueNoise2D(seed + 347);
        _oreIron = new ValueNoise2D(seed + 359);
        _oreGold = new ValueNoise2D(seed + 373);
        _caveN = new ValueNoise2D(seed + 401);

        _grass = BlockRegistry.IdOf("grass");
        _dirt = BlockRegistry.IdOf("dirt");
        _stone = BlockRegistry.IdOf("stone");
        _sand = BlockRegistry.IdOf("sand");
        _sandstone = BlockRegistry.IdOf("sandstone");
        _gravel = BlockRegistry.IdOf("gravel");
        _water = BlockRegistry.IdOf("water");
        _snow = BlockRegistry.IdOf("snow");
        _log = BlockRegistry.IdOf("oak_log");
        _leaves = BlockRegistry.IdOf("leaves");
        _coal = BlockRegistry.IdOf("coal_ore");
        _copper = BlockRegistry.IdOf("copper_ore");
        _iron = BlockRegistry.IdOf("iron_ore");
        _gold = BlockRegistry.IdOf("gold_ore");
        _bedrock = BlockRegistry.IdOf("bedrock");
        _clay = BlockRegistry.IdOf("clay");
    }

    public int SurfaceHeight(int worldX, int worldZ)
    {

        float e = _elev.Fractal(worldX * ElevFreq, worldZ * ElevFreq, 5);
        float rough = (_detail.Fractal(worldX * ElevFreq * 4f, worldZ * ElevFreq * 4f, 3) - 0.5f) * 4f;
        int h = BaseHeight + Mathf.RoundToInt(e * HeightRange + rough);

        float rv = Mathf.Abs(_river.Fractal(worldX * 0.0065f, worldZ * 0.0065f, 2) - 0.5f);
        const float RiverW = 0.016f;
        if (rv < RiverW && h > SeaLevel - 2 && h < SeaLevel + 14)
        {
            float t = rv / RiverW;
            h = Mathf.Min(h, SeaLevel - 2 + (int)(t * t * 8f));
        }
        return h;
    }

    public Biome BiomeAt(int worldX, int worldZ, int height)
    {
        if (height < SeaLevel) return Biome.Ocean;
        if (height <= SeaLevel + 1) return Biome.Beach;
        if (height >= SnowLine) return Biome.Mountains;

        float warmth = _warmth.Fractal(worldX * ClimateFreq, worldZ * ClimateFreq, 2);
        float moisture = _moisture.Fractal(worldX * ClimateFreq, worldZ * ClimateFreq, 2);
        if (warmth < 0.34f) return Biome.Snow;
        if (warmth > 0.62f && moisture < 0.45f) return Biome.Desert;
        if (moisture > 0.72f && height <= SeaLevel + 5) return Biome.Swamp;
        if (moisture > 0.66f) return Biome.DenseForest;
        if (moisture > 0.55f) return Biome.Forest;
        if (moisture > 0.47f) return Biome.Meadow;
        return Biome.Plains;
    }

    public void Generate(Vector3I coord, ushort[] blocks)
    {
        Vector3I baseW = coord * Chunk.Size;
        for (int lz = 0; lz < Chunk.Size; lz++)
        for (int lx = 0; lx < Chunk.Size; lx++)
        {
            int wx = baseW.X + lx, wz = baseW.Z + lz;
            int height = SurfaceHeight(wx, wz);
            Biome biome = BiomeAt(wx, wz, height);
            SurfaceBlocks(biome, height, out ushort top, out ushort sub);

            for (int ly = 0; ly < Chunk.Size; ly++)
            {
                int wy = baseW.Y + ly;
                ushort id;
                if (wy > height)
                    id = wy <= SeaLevel ? _water : (ushort)0;
                else if (wy == height)
                    id = top;
                else if (wy > height - 4)
                    id = sub;
                else
                    id = Underground(wx, wy, wz, height);
                blocks[Chunk.Index(lx, ly, lz)] = id;
            }
        }

        StampTrees(coord, baseW, blocks);
    }

    private void SurfaceBlocks(Biome biome, int height, out ushort top, out ushort sub)
    {
        switch (biome)
        {
            case Biome.Ocean:
                top = height > SeaLevel - 3 ? _sand : _gravel; sub = _sand; break;
            case Biome.Beach:
                top = _sand; sub = _sand; break;
            case Biome.Desert:
                top = _sand; sub = _sandstone; break;
            case Biome.Snow:
                top = _snow; sub = _dirt; break;
            case Biome.Mountains:
                top = height >= SnowLine + 3 ? _snow : _stone; sub = _stone; break;
            case Biome.Swamp:
                top = _grass; sub = _clay; break;
            default:
                top = _grass; sub = _dirt; break;
        }
    }

    private ushort Underground(int wx, int wy, int wz, int height)
    {
        if (wy <= MinWorldY + 1) return _bedrock;

        if (height > SeaLevel + 2 && wy < height - 6 && wy > MinWorldY + 1)
        {
            float cave = _caveN.Fractal3(wx * 0.045f, wy * 0.07f, wz * 0.045f, 3);
            if (Mathf.Abs(cave - 0.5f) < 0.052f) return 0;
        }

        ushort ore = PickOre(wx, wy, wz);
        return ore != 0 ? ore : _stone;
    }

    private ushort PickOre(int wx, int wy, int wz)
    {
        if (wy <= 2 && _oreGold.Fractal3(wx * 0.11f, wy * 0.11f, wz * 0.11f, 2) > 0.82f) return _gold;
        if (wy >= -12 && wy <= 20 && _oreIron.Fractal3(wx * 0.10f, wy * 0.10f, wz * 0.10f, 2) > 0.80f) return _iron;
        if (wy >= -8 && wy <= 32 && _oreCopper.Fractal3(wx * 0.10f, wy * 0.10f, wz * 0.10f, 2) > 0.79f) return _copper;
        if (wy >= -2 && wy <= 48 && _oreCoal.Fractal3(wx * 0.09f, wy * 0.09f, wz * 0.09f, 2) > 0.76f) return _coal;
        return 0;
    }

    private static int FloorDiv(int a, int b)
    {
        int q = a / b;
        if (a % b != 0 && (a < 0) != (b < 0)) q--;
        return q;
    }

    private void StampTrees(Vector3I coord, Vector3I baseW, ushort[] blocks)
    {
        const int reach = 3;
        int gx0 = FloorDiv(baseW.X - reach, TreeGrid);
        int gx1 = FloorDiv(baseW.X + Chunk.Size - 1 + reach, TreeGrid);
        int gz0 = FloorDiv(baseW.Z - reach, TreeGrid);
        int gz1 = FloorDiv(baseW.Z + Chunk.Size - 1 + reach, TreeGrid);

        for (int gx = gx0; gx <= gx1; gx++)
        for (int gz = gz0; gz <= gz1; gz++)
        {
            uint h = ValueNoise2D.Hash(gx, gz, Seed ^ 0x7EE5);
            int wx = gx * TreeGrid + (int)(h % TreeGrid);
            int wz = gz * TreeGrid + (int)((h / 7u) % TreeGrid);

            int surface = SurfaceHeight(wx, wz);
            if (surface <= SeaLevel + 1) continue;
            Biome biome = BiomeAt(wx, wz, surface);
            float density = biome switch
            {
                Biome.DenseForest => 0.9f, Biome.Forest => 0.55f, Biome.Swamp => 0.25f,
                Biome.Plains => 0.08f, Biome.Meadow => 0.02f, _ => 0f,
            };
            if (density <= 0f) continue;

            float roll = ((h >> 16) & 0xFFFF) / 65535f;
            if (roll >= density) continue;

            int trunk = 4 + (int)((h >> 8) % 3u);
            StampTree(blocks, baseW, wx, surface + 1, wz, trunk);
        }
    }

    private void StampTree(ushort[] blocks, Vector3I baseW, int bx, int by, int bz, int trunkHeight)
    {
        for (int t = 0; t < trunkHeight; t++)
            SetCell(blocks, baseW, bx, by + t, bz, _log, overwrite: true);

        int topY = by + trunkHeight;
        for (int dx = -2; dx <= 2; dx++)
        for (int dz = -2; dz <= 2; dz++)
        for (int dy = -2; dy <= 1; dy++)
        {
            float d = Mathf.Sqrt(dx * dx + dz * dz + dy * dy * 1.3f);
            if (d <= 2.4f)
                SetCell(blocks, baseW, bx + dx, topY + dy, bz + dz, _leaves, overwrite: false);
        }
    }

    private static void SetCell(ushort[] blocks, Vector3I baseW, int wx, int wy, int wz, ushort id, bool overwrite)
    {
        int lx = wx - baseW.X, ly = wy - baseW.Y, lz = wz - baseW.Z;
        if (lx < 0 || lx >= Chunk.Size || ly < 0 || ly >= Chunk.Size || lz < 0 || lz >= Chunk.Size) return;
        int idx = Chunk.Index(lx, ly, lz);
        if (!overwrite && blocks[idx] != 0) return;
        blocks[idx] = id;
    }
}
