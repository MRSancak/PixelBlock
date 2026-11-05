using System;
using System.Collections.Generic;
using Godot;

namespace PixelBlock.Core;

public static class ChunkMesher
{
    public const int Pad = 1;
    public const int SnapSize = Chunk.Size + 2 * Pad;

    public static int SnapIndex(int x, int y, int z) =>
        ((y + Pad) * SnapSize + (z + Pad)) * SnapSize + (x + Pad);

    public sealed class Snapshot
    {
        public Vector3I Coord;
        public ushort[] Cells;
        public ushort Get(int x, int y, int z) => Cells[SnapIndex(x, y, z)];
    }

    public sealed class MeshData
    {
        public readonly Surface Opaque = new();
        public readonly Surface Water = new();
        public readonly List<Vector3> Collision = new();
        public readonly List<Vector3I> Vegetation = new();
        public bool IsEmpty => Opaque.Count == 0 && Water.Count == 0 && Collision.Count == 0;
        public int VertexCount => Opaque.Count + Water.Count;
    }

    public sealed class Surface
    {
        public readonly List<Vector3> Verts = new();
        public readonly List<Vector3> Normals = new();
        public readonly List<Plane> Tangents = new();
        public readonly List<Vector2> Uvs = new();
        public readonly List<Color> Custom = new();
        public readonly List<int> Indices = new();
        public int Count => Verts.Count;

        public void AddVertex(Vector3 pos, Vector3 normal, Plane tangent, Vector2 uv, Color custom)
        {
            Verts.Add(pos); Normals.Add(normal); Tangents.Add(tangent); Uvs.Add(uv); Custom.Add(custom);
        }
    }

    public static Snapshot Capture(VoxelWorld world, Chunk chunk)
    {
        var snap = new Snapshot
        {
            Coord = chunk.Coord,
            Cells = new ushort[SnapSize * SnapSize * SnapSize],
        };
        Vector3I baseW = chunk.Coord * Chunk.Size;

        for (int y = 0; y < Chunk.Size; y++)
        for (int z = 0; z < Chunk.Size; z++)
        for (int x = 0; x < Chunk.Size; x++)
            snap.Cells[SnapIndex(x, y, z)] = chunk.GetLocal(x, y, z);

        for (int y = -Pad; y < Chunk.Size + Pad; y++)
        for (int z = -Pad; z < Chunk.Size + Pad; z++)
        for (int x = -Pad; x < Chunk.Size + Pad; x++)
        {
            bool interior = x >= 0 && x < Chunk.Size && y >= 0 && y < Chunk.Size && z >= 0 && z < Chunk.Size;
            if (interior) continue;
            snap.Cells[SnapIndex(x, y, z)] = world.GetBlockId(baseW.X + x, baseW.Y + y, baseW.Z + z);
        }
        return snap;
    }

    public static MeshData BuildData(Snapshot snap)
    {
        var md = new MeshData();
        const int N = Chunk.Size;

        var has = new bool[N * N];
        var used = new bool[N * N];
        var layerArr = new int[N * N];
        var waterArr = new bool[N * N];
        var solidArr = new bool[N * N];
        var aoFlat = new int[N * N];
        var aoCorners = new int[N * N * 4];

        var cBArr = new float[N * N];
        var cAArr = new float[N * N];

        for (int f = 0; f < 6; f++)
        {
            Vector3I nrm = VoxelGeometry.Normals[f];
            Vector3I du = VoxelGeometry.Du[f], dv = VoxelGeometry.Dv[f];

            var naUnit = new Vector3I(Mathf.Abs(nrm.X), Mathf.Abs(nrm.Y), Mathf.Abs(nrm.Z));

            for (int s = 0; s < N; s++)
            {

                for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                {
                    int m = j * N + i;
                    has[m] = false;
                    used[m] = false;

                    Vector3I cell = naUnit * s + du * i + dv * j;
                    ushort id = snap.Get(cell.X, cell.Y, cell.Z);
                    if (id == 0) continue;
                    var self = BlockRegistry.Get(id);
                    Vector3I air = cell + nrm;
                    ushort nbId = snap.Get(air.X, air.Y, air.Z);
                    if (!ShouldDraw(self, id, BlockRegistry.Get(nbId), nbId)) continue;

                    has[m] = true;
                    bool water = self.Render == RenderType.Water;
                    layerArr[m] = self.FaceLayer[f];
                    waterArr[m] = water;
                    solidArr[m] = self.Solid;

                    float cB = 0f, cA = 0f;
                    if (water)
                    {
                        if (nrm.Y > 0)
                            ComputeFlow(snap, cell, out cB, out cA);
                        else if (nrm.Y == 0)
                        {
                            bool waterAbove = BlockRegistry.Get(snap.Get(cell.X, cell.Y + 1, cell.Z)).Render == RenderType.Water;

                            bool lip = snap.Get(cell.X + nrm.X, cell.Y - 1, cell.Z + nrm.Z) == 0;
                            if (waterAbove) cA = 1f;
                            else if (lip) cA = 0.5f;
                        }
                    }
                    cBArr[m] = cB;
                    cAArr[m] = cA;

                    int l0, l1, l2, l3;
                    if (water)
                    {
                        l0 = l1 = l2 = l3 = 3;
                    }
                    else
                    {
                        l0 = AoLevel(snap, air, du, dv, VoxelGeometry.Uvs[0]);
                        l1 = AoLevel(snap, air, du, dv, VoxelGeometry.Uvs[1]);
                        l2 = AoLevel(snap, air, du, dv, VoxelGeometry.Uvs[2]);
                        l3 = AoLevel(snap, air, du, dv, VoxelGeometry.Uvs[3]);
                    }
                    aoCorners[m * 4 + 0] = l0;
                    aoCorners[m * 4 + 1] = l1;
                    aoCorners[m * 4 + 2] = l2;
                    aoCorners[m * 4 + 3] = l3;
                    aoFlat[m] = (l0 == l1 && l1 == l2 && l2 == l3) ? l0 : -1;
                }

                for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                {
                    int m = j * N + i;
                    if (!has[m] || used[m]) continue;

                    Vector3I baseCell = naUnit * s + du * i + dv * j;

                    if (aoFlat[m] < 0)
                    {
                        EmitQuad(md, f, baseCell, 1, 1, layerArr[m], waterArr[m], solidArr[m],
                            aoCorners[m * 4], aoCorners[m * 4 + 1], aoCorners[m * 4 + 2], aoCorners[m * 4 + 3],
                            cBArr[m], cAArr[m]);
                        used[m] = true;
                        continue;
                    }

                    int kLayer = layerArr[m], kAo = aoFlat[m];
                    bool kWater = waterArr[m], kSolid = solidArr[m];
                    float kcB = cBArr[m], kcA = cAArr[m];

                    int w = 1;
                    while (i + w < N && Mergeable(j * N + i + w, has, used, aoFlat, layerArr, waterArr, solidArr,
                               cBArr, cAArr, kAo, kLayer, kWater, kSolid, kcB, kcA))
                        w++;

                    int h = 1;
                    bool stop = false;
                    while (j + h < N && !stop)
                    {
                        for (int k = 0; k < w; k++)
                            if (!Mergeable((j + h) * N + i + k, has, used, aoFlat, layerArr, waterArr, solidArr,
                                    cBArr, cAArr, kAo, kLayer, kWater, kSolid, kcB, kcA))
                            { stop = true; break; }
                        if (!stop) h++;
                    }

                    for (int jj = j; jj < j + h; jj++)
                    for (int ii = i; ii < i + w; ii++)
                        used[jj * N + ii] = true;

                    EmitQuad(md, f, baseCell, w, h, kLayer, kWater, kSolid, kAo, kAo, kAo, kAo, kcB, kcA);
                }
            }
        }

        ScatterVegetation(snap, md);
        return md;
    }

    private static void ScatterVegetation(Snapshot snap, MeshData md)
    {
        Vector3I baseW = snap.Coord * Chunk.Size;
        for (int y = 0; y < Chunk.Size; y++)
        for (int z = 0; z < Chunk.Size; z++)
        for (int x = 0; x < Chunk.Size; x++)
        {
            ushort id = snap.Get(x, y, z);
            if (id == 0 || !BlockRegistry.Get(id).SpawnsVegetation) continue;
            if (snap.Get(x, y + 1, z) != 0) continue;
            uint h = ValueNoise2D.Hash(baseW.X + x, baseW.Z + z, 7777);
            if ((h & 0xFFFF) / 65535f > 0.32f) continue;
            md.Vegetation.Add(new Vector3I(x, y, z));
        }
    }

    private static bool Mergeable(int m, bool[] has, bool[] used, int[] aoFlat,
        int[] layerArr, bool[] waterArr, bool[] solidArr, float[] cBArr, float[] cAArr,
        int kAo, int kLayer, bool kWater, bool kSolid, float kcB, float kcA) =>
        has[m] && !used[m] && aoFlat[m] == kAo && layerArr[m] == kLayer
        && waterArr[m] == kWater && solidArr[m] == kSolid

        && cBArr[m] == kcB && cAArr[m] == kcA;

    private static void ComputeFlow(Snapshot snap, Vector3I c, out float fx, out float fz)
    {
        fx = 0f; fz = 0f;
        AccumFlow(snap, c, 1, 0, ref fx, ref fz);
        AccumFlow(snap, c, -1, 0, ref fx, ref fz);
        AccumFlow(snap, c, 0, 1, ref fx, ref fz);
        AccumFlow(snap, c, 0, -1, ref fx, ref fz);
        float len = Mathf.Sqrt(fx * fx + fz * fz);
        if (len > 0.0001f)
        {
            float intensity = Mathf.Min(len / 2f, 1f);
            fx = fx / len * intensity;
            fz = fz / len * intensity;
        }
    }

    private static void AccumFlow(Snapshot snap, Vector3I c, int dx, int dz, ref float fx, ref float fz)
    {
        if (snap.Get(c.X + dx, c.Y, c.Z + dz) != 0) return;
        float w = snap.Get(c.X + dx, c.Y - 1, c.Z + dz) == 0 ? 2f : 1f;
        fx += dx * w; fz += dz * w;
    }

    private static void EmitQuad(MeshData md, int f, Vector3I baseCell, int w, int h,
        int layer, bool water, bool solid, int ao0, int ao1, int ao2, int ao3, float cB, float cA)
    {
        Surface surf = water ? md.Water : md.Opaque;
        int baseIdx = surf.Count;

        var baseLocal = new Vector3(baseCell.X, baseCell.Y, baseCell.Z);
        Vector3 o = VoxelGeometry.Corners[f][0];
        Vector3I dui = VoxelGeometry.Du[f], dvi = VoxelGeometry.Dv[f];
        var duv = new Vector3(dui.X, dui.Y, dui.Z) * w;
        var dvv = new Vector3(dvi.X, dvi.Y, dvi.Z) * h;
        Vector3 normal = VoxelGeometry.Normals[f];
        var tangent = new Plane(VoxelGeometry.Tangents[f], 1f);

        Vector3 p0 = baseLocal + o;
        Vector3 p1 = baseLocal + o + duv;
        Vector3 p2 = baseLocal + o + duv + dvv;
        Vector3 p3 = baseLocal + o + dvv;

        Vector2 uv0, uv1, uv2, uv3;
        if (f == 2 || f == 3)
        {

            uv0 = new Vector2(0, 0);
            uv1 = new Vector2(w, 0);
            uv2 = new Vector2(w, h);
            uv3 = new Vector2(0, h);
        }
        else
        {

            float maxY = Mathf.Max(Mathf.Max(p0.Y, p1.Y), Mathf.Max(p2.Y, p3.Y));
            float h0, h1, h2, h3, minH;
            if (f == 0 || f == 1)
            {
                h0 = p0.Z; h1 = p1.Z; h2 = p2.Z; h3 = p3.Z;
            }
            else
            {
                h0 = p0.X; h1 = p1.X; h2 = p2.X; h3 = p3.X;
            }
            minH = Mathf.Min(Mathf.Min(h0, h1), Mathf.Min(h2, h3));
            uv0 = new Vector2(h0 - minH, maxY - p0.Y);
            uv1 = new Vector2(h1 - minH, maxY - p1.Y);
            uv2 = new Vector2(h2 - minH, maxY - p2.Y);
            uv3 = new Vector2(h3 - minH, maxY - p3.Y);
        }

        surf.AddVertex(p0, normal, tangent, uv0, new Color(layer, ao0 / 3f, cB, cA));
        surf.AddVertex(p1, normal, tangent, uv1, new Color(layer, ao1 / 3f, cB, cA));
        surf.AddVertex(p2, normal, tangent, uv2, new Color(layer, ao2 / 3f, cB, cA));
        surf.AddVertex(p3, normal, tangent, uv3, new Color(layer, ao3 / 3f, cB, cA));
        foreach (int t in VoxelGeometry.TriOrder)
            surf.Indices.Add(baseIdx + t);

        if (solid)
        {
            Span<Vector3> c = stackalloc Vector3[4] { p0, p1, p2, p3 };
            foreach (int t in VoxelGeometry.TriOrder)
                md.Collision.Add(c[t]);
        }
    }

    private static bool ShouldDraw(BlockType self, ushort selfId, BlockType nb, ushort nbId)
    {
        if (nb.IsAir) return true;
        if (nb.Opaque) return false;
        if (nbId == selfId) return false;
        return true;
    }

    private static int AoLevel(Snapshot snap, Vector3I airCell, Vector3I du, Vector3I dv, Vector2 uv)
    {
        int su = uv.X > 0.5f ? 1 : -1;
        int sv = uv.Y > 0.5f ? 1 : -1;
        bool s1 = IsOpaque(snap, airCell + du * su);
        bool s2 = IsOpaque(snap, airCell + dv * sv);
        bool corner = IsOpaque(snap, airCell + du * su + dv * sv);
        return (s1 && s2) ? 0 : 3 - ((s1 ? 1 : 0) + (s2 ? 1 : 0) + (corner ? 1 : 0));
    }

    private static bool IsOpaque(Snapshot snap, Vector3I c) =>
        BlockRegistry.Get(snap.Get(c.X, c.Y, c.Z)).Opaque;

    public static void Apply(VoxelWorld world, Chunk chunk, MeshData md)
    {
        ArrayMesh opaque = Commit(md.Opaque);
        ArrayMesh water = Commit(md.Water);
        Shape3D col = null;
        if (md.Collision.Count > 0)
        {
            var shape = new ConcavePolygonShape3D();
            shape.SetFaces(md.Collision.ToArray());
            col = shape;
        }
        chunk.ApplyMesh(opaque, world.Textures.Material, water, world.WaterMaterial, col);
        chunk.SetVegetation(BuildVegetation(chunk.Coord, md));
    }

    private static MultiMeshInstance3D BuildVegetation(Vector3I coord, MeshData md)
    {
        if (md.Vegetation.Count == 0) return null;
        Vector3I baseW = coord * Chunk.Size;
        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = Vegetation.CrossMesh,
            InstanceCount = md.Vegetation.Count,
        };
        for (int i = 0; i < md.Vegetation.Count; i++)
        {
            Vector3I c = md.Vegetation[i];
            uint h = ValueNoise2D.Hash(baseW.X + c.X, baseW.Z + c.Z, 31337);
            float yaw = (h & 0xFF) / 255f * Mathf.Tau;
            float scale = 0.8f + ((h >> 8) & 0xFF) / 255f * 0.5f;
            float jx = (((h >> 16) & 0xFF) / 255f - 0.5f) * 0.5f;
            float jz = (((h >> 24) & 0xFF) / 255f - 0.5f) * 0.5f;
            var basis = new Basis(Vector3.Up, yaw).Scaled(new Vector3(scale, scale, scale));
            var origin = new Vector3(c.X + 0.5f + jx, c.Y + 1f, c.Z + 0.5f + jz);
            mm.SetInstanceTransform(i, new Transform3D(basis, origin));
        }
        return new MultiMeshInstance3D
        {
            Name = "Vegetation",
            Multimesh = mm,
            MaterialOverride = Vegetation.Material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
    }

    private static ArrayMesh Commit(Surface s)
    {
        if (s.Count == 0) return null;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetCustomFormat(0, SurfaceTool.CustomFormat.RgbaFloat);
        for (int i = 0; i < s.Count; i++)
        {
            st.SetNormal(s.Normals[i]);
            st.SetTangent(s.Tangents[i]);
            st.SetUV(s.Uvs[i]);
            st.SetCustom(0, s.Custom[i]);
            st.AddVertex(s.Verts[i]);
        }
        foreach (int idx in s.Indices)
            st.AddIndex(idx);
        return st.Commit();
    }
}
