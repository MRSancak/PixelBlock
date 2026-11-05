using System.Collections.Generic;
using Godot;
using PixelBlock.Core;

namespace PixelBlock.World;

public static class ChurnWater
{
    private const int Sub = 4;
    private const float Step = 1f / Sub;
    private const float Jitter = 0.30f;

    private enum State { Falling = 0, Edge = 1, Impact = 2, Spread = 3, Spill = 4 }

    private struct Inst { public Vector3 Pos; public Vector3 Scale; public Color Data; }

    public static Node3D Build(VoxelWorld world)
    {
        var root = new Node3D { Name = "ChurnWater" };
        ushort waterId = BlockRegistry.IdOf("water");

        var water = new HashSet<Vector3I>();
        foreach (var kv in world.Chunks)
        {
            int ox = kv.Key.X * Chunk.Size, oy = kv.Key.Y * Chunk.Size, oz = kv.Key.Z * Chunk.Size;
            for (int lx = 0; lx < Chunk.Size; lx++)
            for (int ly = 0; ly < Chunk.Size; ly++)
            for (int lz = 0; lz < Chunk.Size; lz++)
                if (world.GetBlockId(ox + lx, oy + ly, oz + lz) == waterId)
                    water.Add(new Vector3I(ox + lx, oy + ly, oz + lz));
        }
        if (water.Count == 0) return root;

        bool IsWater(int x, int y, int z) => water.Contains(new Vector3I(x, y, z));
        bool IsAir(int x, int y, int z) => world.GetBlockId(x, y, z) == 0;
        (int dx, int dz)[] horiz = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        var curtains = new List<(Vector3I p, Vector3 outN)>();
        var curtainOut = new Dictionary<Vector3I, Vector3>();
        foreach (var p in water)
        {
            if (!IsWater(p.X, p.Y + 1, p.Z)) continue;
            Vector3I outDir = Vector3I.Zero;
            foreach (var (dx, dz) in horiz)
                if (IsAir(p.X + dx, p.Y, p.Z + dz)) outDir += new Vector3I(dx, 0, dz);
            if (outDir == Vector3I.Zero) continue;
            var outN = new Vector3(Mathf.Sign(outDir.X), 0, Mathf.Sign(outDir.Z));
            outN = outN.Length() > 0.001f ? outN.Normalized() : new Vector3(0, 0, 1);
            curtains.Add((p, outN));
            curtainOut[p] = outN;
        }

        Vector3 spill = Vector3.Zero;
        foreach (var (_, outN) in curtains) spill += outN;
        spill = spill.Length() > 0.001f ? spill.Normalized() : new Vector3(0, 0, 1);
        float spillAng = Angle01(spill.X, spill.Z);

        var surface = new HashSet<Vector3I>();
        foreach (var p in water)
            if (!IsWater(p.X, p.Y + 1, p.Z) && !curtainOut.ContainsKey(p))
                surface.Add(p);

        var lips = new Dictionary<Vector3I, Vector3>();
        foreach (var s in surface)
            if (curtainOut.TryGetValue(new Vector3I(s.X, s.Y - 1, s.Z), out var od))
                lips[s] = od;

        var flowAngle = new Dictionary<Vector3I, float>();
        var fq = new Queue<Vector3I>();
        foreach (var (lip, od) in lips) { flowAngle[lip] = Angle01(od.X, od.Z); fq.Enqueue(lip); }
        while (fq.Count > 0)
        {
            var c = fq.Dequeue();
            foreach (var (dx, dz) in horiz)
                for (int dy = -1; dy <= 1; dy++)
                {
                    var n = new Vector3I(c.X + dx, c.Y + dy, c.Z + dz);
                    if (!surface.Contains(n) || flowAngle.ContainsKey(n)) continue;
                    flowAngle[n] = Angle01(c.X - n.X, c.Z - n.Z);
                    fq.Enqueue(n);
                }
        }

        var turb = new Dictionary<Vector3I, float>();
        var impacts = new HashSet<Vector2I>();
        void Seed(Vector3I p, float t)
        {
            if (!surface.Contains(p)) return;
            turb[p] = Mathf.Max(turb.TryGetValue(p, out var e) ? e : 0f, t);
        }
        foreach (var (p, outN) in curtains)
        {
            foreach (var (dx, dz) in horiz)
                for (int dy = -1; dy <= 1; dy++)
                    Seed(new Vector3I(p.X + dx, p.Y + dy, p.Z + dz), 6f);

            int sx = (int)Mathf.Sign(outN.X), sz = (int)Mathf.Sign(outN.Z);
            for (int y = p.Y; y >= p.Y - 24; y--)
                if (IsWater(p.X + sx, y, p.Z + sz) && !IsWater(p.X + sx, y + 1, p.Z + sz))
                { Seed(new Vector3I(p.X + sx, y, p.Z + sz), 10f); impacts.Add(new Vector2I(p.X + sx, p.Z + sz)); break; }
        }

        var queue = new Queue<Vector3I>(turb.Keys);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            float t = turb[c];
            if (t <= 2f) continue;
            foreach (var (dx, dz) in horiz)
                for (int dy = -1; dy <= 1; dy++)
                {
                    var n = new Vector3I(c.X + dx, c.Y + dy, c.Z + dz);
                    if (!surface.Contains(n)) continue;
                    float nt = t - 2f;
                    if (nt > (turb.TryGetValue(n, out var e) ? e : 0f)) { turb[n] = nt; queue.Enqueue(n); }
                }
        }

        bool Interior(Vector3I c)
        {
            foreach (var (dx, dz) in horiz)
                if (!water.Contains(new Vector3I(c.X + dx, c.Y, c.Z + dz))) return false;
            return true;
        }
        float SpreadAngle(Vector3I cell)
        {
            float bestD = float.MaxValue; Vector2I best = default; bool found = false;
            foreach (var im in impacts)
            {
                float d = (im.X - cell.X) * (im.X - cell.X) + (im.Y - cell.Z) * (im.Y - cell.Z);
                if (d < bestD) { bestD = d; best = im; found = true; }
            }
            if (!found || bestD < 0.01f) return spillAng;
            var away = new Vector2(cell.X - best.X, cell.Z - best.Y);
            if (away.Length() < 0.01f) return spillAng;
            away = away.Normalized();
            var sp = new Vector2(spill.X, spill.Z);
            float along = away.Dot(sp);
            Vector2 res = (away - sp * along) + sp * Mathf.Max(along, 0f);
            return res.Length() < 0.05f ? spillAng : Angle01(res.X, res.Y);
        }

        var insts = new List<Inst>();
        var foamScale = new Vector3(Step * 0.9f, Step * 0.9f, Step * 0.9f);

        foreach (var (p, outN) in curtains)
            for (int i = 0; i < Sub; i++)
            for (int k = 0; k < Sub; k++)
                Emit(insts, new Vector3(p.X + (i + 0.5f) * Step, p.Y + 0.5f, p.Z + (k + 0.5f) * Step) + outN * 0.18f,
                    new Vector3(Step * 0.85f, Step * 2.0f, Step * 0.85f), 0.9f, State.Falling, 0f, p, i * Sub + k);

        foreach (var (lip, od) in lips)
        {
            float ang = Angle01(od.X, od.Z);
            for (int i = 0; i < Sub; i++)
            for (int k = 0; k < Sub; k++)
                Emit(insts, new Vector3(lip.X + (i + 0.5f) * Step, lip.Y + 1f, lip.Z + (k + 0.5f) * Step) + od * 0.4f,
                    foamScale, 0.9f, State.Spill, ang, lip, 90 + i * Sub + k);
        }

        foreach (var (c, t) in turb)
        {
            if (t <= 0f || lips.ContainsKey(c)) continue;
            bool nearLip = false;
            foreach (var (dx, dz) in horiz)
                if (lips.ContainsKey(new Vector3I(c.X + dx, c.Y, c.Z + dz))) { nearLip = true; break; }
            if (!nearLip && !Interior(c)) continue;
            State s = t >= 8f ? State.Impact : t >= 4f ? State.Edge : State.Spread;
            int layers = t >= 8f ? 2 : 1;
            float ang = flowAngle.TryGetValue(c, out var fa) ? fa : SpreadAngle(c);
            for (int L = 0; L < layers; L++)
            for (int i = 0; i < Sub; i++)
            for (int k = 0; k < Sub; k++)
                Emit(insts, new Vector3(c.X + (i + 0.5f) * Step, c.Y + 1f + L * Step, c.Z + (k + 0.5f) * Step),
                    foamScale, Mathf.Clamp(t / 10f, 0.15f, 1f), s, ang, c, L * 137 + i * Sub + k);
        }

        if (insts.Count == 0) return root;

        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = new BoxMesh { Size = Vector3.One },
            InstanceCount = insts.Count,
        };
        for (int i = 0; i < insts.Count; i++)
        {
            mm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(insts[i].Scale), insts[i].Pos));
            mm.SetInstanceCustomData(i, insts[i].Data);
        }

        var mat = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://assets/shaders/churn.gdshader"),
            RenderPriority = 1,
        };
        root.AddChild(new MultiMeshInstance3D { Name = "Churn", Multimesh = mm, MaterialOverride = mat });
        return root;
    }

    private static void Emit(List<Inst> list, Vector3 basePos, Vector3 baseScale, float turb, State s,
        float flowAngle01, Vector3I cell, int sub)
    {
        uint h = KeyOf(cell, sub);
        float drop = s == State.Falling ? 0.40f : 0.30f;
        if ((h & 0xFF) / 255f < drop) return;

        uint h2 = KeyOf(cell, sub * 31 + 7);
        float jx = (h >> 8 & 0xFF) / 255f - 0.5f;
        float jz = (h >> 16 & 0xFF) / 255f - 0.5f;
        float jy = (h >> 24 & 0xFF) / 255f - 0.5f;
        float sizef = 0.55f + (h2 >> 16 & 0xFF) / 255f * 0.55f;
        float seed = (h2 & 0xFFFF) / 65535f;

        list.Add(new Inst
        {
            Pos = basePos + new Vector3(jx, jy * 0.6f, jz) * Jitter,
            Scale = baseScale * sizef,
            Data = new Color(turb, (float)(int)s / 4f, seed, flowAngle01),
        });
    }

    private static float Angle01(float x, float z)
    {
        float a = Mathf.Atan2(z, x) / (Mathf.Pi * 2f) + 0.5f;
        return a - Mathf.Floor(a);
    }

    private static uint KeyOf(Vector3I p, int sub)
    {
        uint h = 2166136261u;
        h = (h ^ (uint)p.X) * 16777619u;
        h = (h ^ (uint)p.Y) * 16777619u;
        h = (h ^ (uint)p.Z) * 16777619u;
        h = (h ^ (uint)sub) * 16777619u;
        h ^= h >> 15;
        return h;
    }
}
