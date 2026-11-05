using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace PixelBlock.Core;

public enum BlockChangeCause { Generation, LessonBuild, PlayerEdit, Script }

public sealed partial class VoxelWorld : Node3D
{
    public BlockTextures Textures { get; private set; }
    public Material WaterMaterial { get; private set; }
    private ushort _waterId;

    [Signal] public delegate void BlockChangedEventHandler(Vector3I pos, int oldId, int newId, int cause);

    public bool EmitBlockChanges;

    private readonly Dictionary<Vector3I, Chunk> _chunks = new();
    private readonly HashSet<Vector3I> _dirty = new();
    private readonly Queue<Vector3I> _dirtyQueue = new();

    private readonly HashSet<Vector3I> _meshing = new();
    private readonly ConcurrentQueue<MeshJobResult> _results = new();

    public int MaxConcurrentMeshes = 8;

    public int ApplyPerFrame = 6;

    private struct MeshJobResult
    {
        public Vector3I Coord;
        public int Version;
        public ChunkMesher.MeshData Data;
    }

    public IReadOnlyDictionary<Vector3I, Chunk> Chunks => _chunks;

    public override void _Ready()
    {
        BlockRegistry.EnsureInit();
        Textures = BlockTextures.Build();
        WaterMaterial = MakeWaterMaterial();
        _waterId = BlockRegistry.IdOf("water");
    }

    public override void _Process(double delta)
    {
        if (_streaming)
        {
            UpdateStreaming();
            PumpGeneration();
            PumpWaterFill(delta);
        }
        PumpMeshing();

        _fxTime += (float)delta;
        if (WaterMaterial is ShaderMaterial sm) sm.SetShaderParameter("u_time", _fxTime);
    }

    private void PumpMeshing()
    {

        while (_meshing.Count < MaxConcurrentMeshes && _dirtyQueue.Count > 0)
        {
            var coord = _dirtyQueue.Dequeue();
            _dirty.Remove(coord);
            if (!_chunks.TryGetValue(coord, out var chunk) || !chunk.Dirty) continue;
            if (_meshing.Contains(coord)) continue;
            if (_streaming && !MeshEligible(coord)) continue;

            var snap = ChunkMesher.Capture(this, chunk);
            int version = chunk.MeshVersion;
            chunk.Dirty = false;
            _meshing.Add(coord);
            Task.Run(() =>
            {
                ChunkMesher.MeshData data = null;
                try { data = ChunkMesher.BuildData(snap); }
                catch (Exception) { /* surfaced on apply; chunk will retry */ }
                _results.Enqueue(new MeshJobResult { Coord = coord, Version = version, Data = data });
            });
        }

        int applied = 0;
        while (applied < ApplyPerFrame && _results.TryDequeue(out var r))
        {
            _meshing.Remove(r.Coord);
            if (!_chunks.TryGetValue(r.Coord, out var chunk)) continue;
            if (r.Data == null || chunk.MeshVersion != r.Version) { MarkDirty(r.Coord); continue; }
            ChunkMesher.Apply(this, chunk, r.Data);
            chunk.Dirty = false;
            applied++;
        }
    }

    private static int FloorDiv(int a, int b)
    {
        int q = a / b;
        if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
        return q;
    }

    private static int Mod(int a, int b)
    {
        int r = a % b;
        return r < 0 ? r + b : r;
    }

    public static Vector3I ChunkCoord(int x, int y, int z) =>
        new(FloorDiv(x, Chunk.Size), FloorDiv(y, Chunk.Size), FloorDiv(z, Chunk.Size));

    public ushort GetBlockId(int x, int y, int z)
    {
        var c = ChunkCoord(x, y, z);
        return _chunks.TryGetValue(c, out var ch)
            ? ch.GetLocal(Mod(x, Chunk.Size), Mod(y, Chunk.Size), Mod(z, Chunk.Size))
            : (ushort)0;
    }

    public ushort GetBlockId(Vector3I p) => GetBlockId(p.X, p.Y, p.Z);

    public bool HasChunk(Vector3I coord) => _chunks.ContainsKey(coord);
    public BlockType GetBlock(int x, int y, int z) => BlockRegistry.Get(GetBlockId(x, y, z));
    public BlockType GetBlock(Vector3I p) => BlockRegistry.Get(GetBlockId(p.X, p.Y, p.Z));
    public bool IsOpaque(Vector3I p) => BlockRegistry.Get(GetBlockId(p.X, p.Y, p.Z)).Opaque;
    public bool IsSolid(Vector3I p) => BlockRegistry.Get(GetBlockId(p.X, p.Y, p.Z)).Solid;

    public void SetBlock(int x, int y, int z, ushort id, bool remesh = true,
        BlockChangeCause cause = BlockChangeCause.PlayerEdit)
    {
        var c = ChunkCoord(x, y, z);

        if (_streaming) RecordEdit(c, x, y, z, id);
        var ch = GetOrCreate(c);
        int lx = Mod(x, Chunk.Size), ly = Mod(y, Chunk.Size), lz = Mod(z, Chunk.Size);
        ushort prev = ch.GetLocal(lx, ly, lz);
        ch.SetLocal(lx, ly, lz, id);

        if (EmitBlockChanges && cause == BlockChangeCause.PlayerEdit && prev != id)
            EmitSignal(SignalName.BlockChanged, new Vector3I(x, y, z), (int)prev, (int)id, (int)cause);

        if (_streaming) EnqueueWaterArea(x, y, z);
        if (!remesh) return;

        MarkDirty(c);
        if (lx == 0) MarkDirty(c + new Vector3I(-1, 0, 0));
        if (lx == Chunk.Size - 1) MarkDirty(c + new Vector3I(1, 0, 0));
        if (ly == 0) MarkDirty(c + new Vector3I(0, -1, 0));
        if (ly == Chunk.Size - 1) MarkDirty(c + new Vector3I(0, 1, 0));
        if (lz == 0) MarkDirty(c + new Vector3I(0, 0, -1));
        if (lz == Chunk.Size - 1) MarkDirty(c + new Vector3I(0, 0, 1));
    }

    public void SetBlock(Vector3I p, ushort id, bool remesh = true,
        BlockChangeCause cause = BlockChangeCause.PlayerEdit) => SetBlock(p.X, p.Y, p.Z, id, remesh, cause);

    private Chunk GetOrCreate(Vector3I coord)
    {
        if (_chunks.TryGetValue(coord, out var ch)) return ch;
        ch = new Chunk { Coord = coord, Name = $"Chunk_{coord.X}_{coord.Y}_{coord.Z}" };
        _chunks[coord] = ch;
        AddChild(ch);
        return ch;
    }

    public void LoadChunk(Vector3I coord, ushort[] data)
    {
        var ch = GetOrCreate(coord);
        System.Array.Copy(data, ch.Blocks, Chunk.Volume);
        ch.RecomputeSolid();
        MarkDirty(coord);
    }

    private void MarkDirty(Vector3I coord)
    {
        if (!_chunks.TryGetValue(coord, out var ch)) return;
        ch.Dirty = true;
        if (_dirty.Add(coord)) _dirtyQueue.Enqueue(coord);
    }

    private void RebuildChunk(Chunk chunk)
    {
        var snap = ChunkMesher.Capture(this, chunk);
        var data = ChunkMesher.BuildData(snap);
        ChunkMesher.Apply(this, chunk, data);
        chunk.Dirty = false;
    }

    public void MarkAllDirty()
    {
        foreach (var c in _chunks.Keys) MarkDirty(c);
    }

    public void RebuildAllNow()
    {
        foreach (var ch in _chunks.Values)
            RebuildChunk(ch);
        _dirty.Clear();
        _dirtyQueue.Clear();
    }

    public int ChunkCount => _chunks.Count;

    public int MeshingCount => _meshing.Count;

    public int DirtyCount => _dirty.Count;

    public void Clear()
    {
        foreach (var ch in _chunks.Values) ch.QueueFree();
        _chunks.Clear();
        _dirty.Clear();
        _dirtyQueue.Clear();
        _meshing.Clear();
        while (_results.TryDequeue(out _)) { }
        ResetStreaming();
    }

    private static Material MakeWaterMaterial()
    {
        var shader = GD.Load<Shader>("res://assets/shaders/water.gdshader");
        if (shader == null)
        {

            return new StandardMaterial3D
            {
                AlbedoColor = new Color(0.18f, 0.42f, 0.62f, 0.72f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 0.12f,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            };
        }

        return new ShaderMaterial { Shader = shader, RenderPriority = -1 };
    }

    private const int RippleSlots = 12;
    private readonly Vector4[] _ripples = new Vector4[RippleSlots];
    private int _rippleNext;
    private float _fxTime;

    public void AddRipple(Vector3 pos, float size = 0.8f)
    {
        if (WaterMaterial is not ShaderMaterial sm) return;
        _ripples[_rippleNext] = new Vector4(pos.X, pos.Z, _fxTime, Mathf.Max(size, 0.05f));
        _rippleNext = (_rippleNext + 1) % RippleSlots;
        sm.SetShaderParameter("ripples", _ripples);
    }
}
