using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace PixelBlock.Core;

public sealed partial class VoxelWorld : Node3D
{
    public interface IChunkGenerator
    {
        int Seed { get; }

        void Generate(Vector3I coord, ushort[] blocks);

        int SurfaceHeight(int worldX, int worldZ);
    }

    private bool _streaming;
    private IChunkGenerator _generator;
    private Node3D _streamTarget;
    private int _renderDistance = 8;
    private int _minChunkY = -1;
    private int _maxChunkY = 4;
    private Vector3I _lastTargetChunk = new(int.MinValue, 0, 0);

    private readonly HashSet<Vector3I> _generated = new();
    private readonly HashSet<Vector3I> _generating = new();
    private readonly HashSet<Vector3I> _genPending = new();
    private readonly Queue<Vector3I> _genQueue = new();
    private readonly ConcurrentQueue<GenResult> _genResults = new();
    private readonly List<Vector3I> _scratch = new();

    private readonly Dictionary<Vector3I, Dictionary<Vector3I, ushort>> _edits = new();
    public int MaxConcurrentGen = 8;
    public int GeneratePerFrame = 8;

    private struct GenResult { public Vector3I Coord; public ushort[] Blocks; }

    public bool IsStreaming => _streaming;
    public int GeneratedChunkCount => _generated.Count;
    public int Seed => _generator?.Seed ?? 0;
    public int EditCount { get { int n = 0; foreach (var d in _edits.Values) n += d.Count; return n; } }

    private void RecordEdit(Vector3I chunkCoord, int x, int y, int z, ushort id)
    {
        if (!_edits.TryGetValue(chunkCoord, out var d)) { d = new(); _edits[chunkCoord] = d; }
        d[new Vector3I(x, y, z)] = id;
    }

    private void ApplyEdits(Vector3I coord, Chunk chunk)
    {
        if (!_edits.TryGetValue(coord, out var d)) return;
        foreach (var (pos, id) in d)
            chunk.SetLocal(Mod(pos.X, Chunk.Size), Mod(pos.Y, Chunk.Size), Mod(pos.Z, Chunk.Size), id);
    }

    public void PreloadEdits(IEnumerable<(int x, int y, int z, ushort id)> edits)
    {
        foreach (var (x, y, z, id) in edits)
            RecordEdit(ChunkCoord(x, y, z), x, y, z, id);
    }

    public IEnumerable<(Vector3I pos, ushort id)> AllEdits()
    {
        foreach (var d in _edits.Values)
            foreach (var (pos, id) in d)
                yield return (pos, id);
    }

    private static readonly Vector3I[] FaceNeighbours =
    {
        new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1),
    };

    public void StartStreaming(IChunkGenerator generator, Node3D target,
        int renderDistance = 8, int minChunkY = -1, int maxChunkY = 4)
    {
        _generator = generator;
        _streamTarget = target;
        _renderDistance = Mathf.Max(2, renderDistance);
        _minChunkY = minChunkY;
        _maxChunkY = maxChunkY;
        _lastTargetChunk = new Vector3I(int.MinValue, 0, 0);
        _streaming = true;
    }

    public void StopStreaming() => _streaming = false;

    private void ResetStreaming()
    {
        _generated.Clear();
        _generating.Clear();
        _genPending.Clear();
        _genQueue.Clear();
        while (_genResults.TryDequeue(out _)) { }
        _edits.Clear();
        _waterQueue.Clear();
        _waterQueued.Clear();
        _lastTargetChunk = new Vector3I(int.MinValue, 0, 0);
    }

    public void EnsureSpawnArea(Vector3 center, int radius = 2)
    {
        if (_generator == null) return;
        Vector3I tc = ChunkCoord(Mathf.FloorToInt(center.X), 0, Mathf.FloorToInt(center.Z));

        int genR = radius + 1;
        for (int dz = -genR; dz <= genR; dz++)
        for (int dx = -genR; dx <= genR; dx++)
        for (int cy = _minChunkY; cy <= _maxChunkY; cy++)
        {
            var coord = new Vector3I(tc.X + dx, cy, tc.Z + dz);
            if (_generated.Contains(coord)) continue;
            var ch = GetOrCreate(coord);
            _generator.Generate(coord, ch.Blocks);
            ApplyEdits(coord, ch);
            ch.RecomputeSolid();
            _generated.Add(coord);
        }

        for (int dz = -radius; dz <= radius; dz++)
        for (int dx = -radius; dx <= radius; dx++)
        for (int cy = _minChunkY; cy <= _maxChunkY; cy++)
        {
            var coord = new Vector3I(tc.X + dx, cy, tc.Z + dz);
            if (_chunks.TryGetValue(coord, out var ch)) RebuildChunk(ch);
        }
    }

    public bool StreamingHold(Vector3 worldPos)
    {
        if (!_streaming) return false;
        var c = ChunkCoord(Mathf.FloorToInt(worldPos.X),
            Mathf.FloorToInt(worldPos.Y - 0.1f), Mathf.FloorToInt(worldPos.Z));
        return !_generated.Contains(c);
    }

    private void UpdateStreaming()
    {
        if (_streamTarget == null || !GodotObject.IsInstanceValid(_streamTarget)) return;
        Vector3 p = _streamTarget.GlobalPosition;
        Vector3I tc = ChunkCoord(Mathf.FloorToInt(p.X), 0, Mathf.FloorToInt(p.Z));

        if (tc.X == _lastTargetChunk.X && tc.Z == _lastTargetChunk.Z) return;
        _lastTargetChunk = tc;

        int genR = _renderDistance + 1;
        int meshR = _renderDistance;
        int unloadR = _renderDistance + 2;

        _scratch.Clear();
        for (int dz = -genR; dz <= genR; dz++)
        for (int dx = -genR; dx <= genR; dx++)
        {
            if (dx * dx + dz * dz > genR * genR) continue;
            for (int cy = _minChunkY; cy <= _maxChunkY; cy++)
            {
                var coord = new Vector3I(tc.X + dx, cy, tc.Z + dz);
                if (_generated.Contains(coord) || _generating.Contains(coord) || _genPending.Contains(coord))
                    continue;
                _scratch.Add(coord);
            }
        }
        _scratch.Sort((a, b) => HDist2(a, tc).CompareTo(HDist2(b, tc)));
        foreach (var coord in _scratch)
        {
            _genPending.Add(coord);
            _genQueue.Enqueue(coord);
        }

        for (int dz = -meshR; dz <= meshR; dz++)
        for (int dx = -meshR; dx <= meshR; dx++)
        {
            if (dx * dx + dz * dz > meshR * meshR) continue;
            for (int cy = _minChunkY; cy <= _maxChunkY; cy++)
            {
                var coord = new Vector3I(tc.X + dx, cy, tc.Z + dz);
                if (_generated.Contains(coord) && _chunks.TryGetValue(coord, out var ch) && !ch.Meshed)
                    MarkDirty(coord);
            }
        }

        _scratch.Clear();
        foreach (var c in _chunks.Keys)
            if (HDist2(c, tc) > unloadR * unloadR) _scratch.Add(c);
        foreach (var c in _scratch) UnloadChunk(c);
    }

    private static int HDist2(Vector3I c, Vector3I tc)
    {
        int dx = c.X - tc.X, dz = c.Z - tc.Z;
        return dx * dx + dz * dz;
    }

    private void PumpGeneration()
    {

        while (_generating.Count < MaxConcurrentGen && _genQueue.Count > 0)
        {
            var coord = _genQueue.Dequeue();
            _genPending.Remove(coord);
            if (_generated.Contains(coord) || _generating.Contains(coord)) continue;
            GetOrCreate(coord);
            _generating.Add(coord);
            var gen = _generator;
            Task.Run(() =>
            {
                var buf = new ushort[Chunk.Volume];
                try { gen.Generate(coord, buf); }
                catch (Exception) { Array.Clear(buf, 0, buf.Length); }
                _genResults.Enqueue(new GenResult { Coord = coord, Blocks = buf });
            });
        }

        int applied = 0;
        while (applied < GeneratePerFrame && _genResults.TryDequeue(out var r))
        {
            _generating.Remove(r.Coord);
            if (!_chunks.TryGetValue(r.Coord, out var chunk)) continue;
            Array.Copy(r.Blocks, chunk.Blocks, Chunk.Volume);
            ApplyEdits(r.Coord, chunk);
            chunk.RecomputeSolid();
            _generated.Add(r.Coord);
            applied++;

            MarkDirty(r.Coord);
            foreach (var n in FaceNeighbours) MarkDirty(r.Coord + n);
        }
    }

    private bool MeshEligible(Vector3I coord)
    {
        if (!_generated.Contains(coord)) return false;
        foreach (var n in FaceNeighbours)
        {
            var nc = coord + n;
            if (nc.Y < _minChunkY || nc.Y > _maxChunkY) continue;
            if (!_generated.Contains(nc)) return false;
        }
        return true;
    }

    private void UnloadChunk(Vector3I coord)
    {
        if (_chunks.TryGetValue(coord, out var ch))
        {
            ch.QueueFree();
            _chunks.Remove(coord);
        }
        _generated.Remove(coord);
        _genPending.Remove(coord);
        _dirty.Remove(coord);
    }

    private readonly PriorityQueue<Vector3I, int> _waterQueue = new();
    private readonly HashSet<Vector3I> _waterQueued = new();

    public float WaterStepSeconds = 0.08f;
    public int WaterCellsPerStep = 1;
    private float _waterAccum;

    private void EnqueueWaterArea(int x, int y, int z)
    {

        TryQueueWater(x, y, z);
        TryQueueWater(x, y - 1, z);
        TryQueueWater(x + 1, y, z);
        TryQueueWater(x - 1, y, z);
        TryQueueWater(x, y + 1, z);
        TryQueueWater(x, y, z + 1);
        TryQueueWater(x, y, z - 1);
    }

    private void TryQueueWater(int x, int y, int z)
    {
        if (y > TerrainGenerator.SeaLevel) return;
        var p = new Vector3I(x, y, z);
        if (!_waterQueued.Add(p)) return;
        _waterQueue.Enqueue(p, y);
    }

    private void PumpWaterFill(double delta)
    {

        _waterAccum += (float)delta;
        if (_waterAccum < WaterStepSeconds) return;
        _waterAccum = 0f;
        DrainWater(WaterCellsPerStep);
    }

    public int DrainWaterForTest(int maxCells) => DrainWater(maxCells);

    private int DrainWater(int budget)
    {
        int placed = 0;
        while (placed < budget && _waterQueue.Count > 0)
        {
            var p = _waterQueue.Dequeue();
            _waterQueued.Remove(p);
            if (p.Y > TerrainGenerator.SeaLevel) continue;

            if (!_generated.Contains(ChunkCoord(p.X, p.Y, p.Z))) continue;
            if (GetBlockId(p.X, p.Y, p.Z) != 0) continue;
            if (!WaterReaches(p)) continue;

            SetBlock(p.X, p.Y, p.Z, _waterId, cause: BlockChangeCause.Script);
            placed++;
        }
        return placed;
    }

    private bool WaterReaches(Vector3I p) =>
        GetBlockId(p.X, p.Y + 1, p.Z) == _waterId
        || GetBlockId(p.X + 1, p.Y, p.Z) == _waterId
        || GetBlockId(p.X - 1, p.Y, p.Z) == _waterId
        || GetBlockId(p.X, p.Y, p.Z + 1) == _waterId
        || GetBlockId(p.X, p.Y, p.Z - 1) == _waterId;
}
