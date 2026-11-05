using System.Collections.Generic;
using Godot;

namespace PixelBlock.Core;

public sealed class Inventory
{
    public const int HotbarSize = 9;
    public const int TotalSlots = 36;
    public const int StackMax = 64;
    public const int Max = 999;

    private readonly ushort[] _id = new ushort[TotalSlots];
    private readonly int[] _count = new int[TotalSlots];
    private readonly List<ushort> _order = new();

    public event System.Action Changed;

    public IReadOnlyList<ushort> Order => _order;

    public ushort SlotId(int i) => i >= 0 && i < TotalSlots ? _id[i] : (ushort)0;
    public int SlotCount(int i) => i >= 0 && i < TotalSlots ? _count[i] : 0;

    public int Count(ushort id)
    {
        if (id == 0) return 0;
        int n = 0;
        for (int i = 0; i < TotalSlots; i++) if (_id[i] == id) n += _count[i];
        return n;
    }

    public bool Has(ushort id, int n = 1) => id != 0 && Count(id) >= n;

    public int Add(ushort id, int n = 1)
    {
        if (id == 0 || n <= 0) return 0;
        int left = n;

        for (int i = 0; i < TotalSlots && left > 0; i++)
        {
            if (_id[i] != id || _count[i] >= StackMax) continue;
            int add = Mathf.Min(left, StackMax - _count[i]);
            _count[i] += add;
            left -= add;
        }

        for (int i = 0; i < TotalSlots && left > 0; i++)
        {
            if (_id[i] != 0) continue;
            int add = Mathf.Min(left, StackMax);
            _id[i] = id;
            _count[i] = add;
            left -= add;
        }
        RebuildOrder();
        Changed?.Invoke();
        return left;
    }

    public bool TryConsume(ushort id, int n = 1)
    {
        if (!Has(id, n)) return false;
        int left = n;
        for (int i = TotalSlots - 1; i >= 0 && left > 0; i--)
        {
            if (_id[i] != id) continue;
            int take = Mathf.Min(left, _count[i]);
            _count[i] -= take;
            left -= take;
            if (_count[i] <= 0) { _count[i] = 0; _id[i] = 0; }
        }
        RebuildOrder();
        Changed?.Invoke();
        return true;
    }

    public bool TakeFromSlot(int slot, int n)
    {
        if (slot < 0 || slot >= TotalSlots || _id[slot] == 0 || n <= 0) return false;
        n = Mathf.Min(n, _count[slot]);
        _count[slot] -= n;
        if (_count[slot] <= 0) { _count[slot] = 0; _id[slot] = 0; }
        RebuildOrder();
        Changed?.Invoke();
        return true;
    }

    public void SetSlot(int slot, ushort id, int count)
    {
        if (slot < 0 || slot >= TotalSlots) return;
        if (id == 0 || count <= 0) { _id[slot] = 0; _count[slot] = 0; }
        else { _id[slot] = id; _count[slot] = Mathf.Min(count, StackMax); }
        RebuildOrder();
        Changed?.Invoke();
    }

    public void MoveStack(int from, int to)
    {
        if (from == to || from < 0 || to < 0 || from >= TotalSlots || to >= TotalSlots) return;
        if (_id[from] == 0) return;
        if (_id[to] == _id[from])
        {
            int room = StackMax - _count[to];
            int move = Mathf.Min(room, _count[from]);
            _count[to] += move;
            _count[from] -= move;
            if (_count[from] <= 0) { _count[from] = 0; _id[from] = 0; }
        }
        else
        {
            (_id[from], _id[to]) = (_id[to], _id[from]);
            (_count[from], _count[to]) = (_count[to], _count[from]);
        }
        RebuildOrder();
        Changed?.Invoke();
    }

    /// <summary>Eşyayı yalnızca [from..to] kutularına ekler; sığmayan miktarı döndürür.</summary>
    public int AddToRange(ushort id, int n, int from, int to)
    {
        if (id == 0 || n <= 0) return n;
        from = Mathf.Max(0, from); to = Mathf.Min(TotalSlots - 1, to);
        int left = n;
        for (int i = from; i <= to && left > 0; i++)
        {
            if (_id[i] != id || _count[i] >= StackMax) continue;
            int add = Mathf.Min(left, StackMax - _count[i]);
            _count[i] += add; left -= add;
        }
        for (int i = from; i <= to && left > 0; i++)
        {
            if (_id[i] != 0) continue;
            int add = Mathf.Min(left, StackMax);
            _id[i] = id; _count[i] = add; left -= add;
        }
        RebuildOrder();
        Changed?.Invoke();
        return left;
    }

    /// <summary>İki envanter arasında yığın taşır (boş kutuya ya da aynı eşyaya birleştirir).</summary>
    public static void Transfer(Inventory src, int from, Inventory dst, int to)
    {
        if (src == null || dst == null) return;
        if (src == dst) { src.MoveStack(from, to); return; }
        ushort id = src.SlotId(from);
        int n = src.SlotCount(from);
        if (id == 0 || n <= 0) return;
        ushort did = dst.SlotId(to);
        int dn = dst.SlotCount(to);
        if (did == 0) { dst.SetSlot(to, id, n); src.TakeFromSlot(from, n); }
        else if (did == id)
        {
            int mv = Mathf.Min(StackMax - dn, n);
            if (mv > 0) { dst.SetSlot(to, id, dn + mv); src.TakeFromSlot(from, mv); }
        }
    }

    public string Encode()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < TotalSlots; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(_id[i] == 0 ? "" : BlockRegistry.Get(_id[i]).Name).Append(':').Append(_count[i]);
        }
        return sb.ToString();
    }

    public void Decode(string s)
    {
        for (int i = 0; i < TotalSlots; i++) { _id[i] = 0; _count[i] = 0; }
        if (!string.IsNullOrEmpty(s))
        {
            var parts = s.Split(',');
            for (int i = 0; i < parts.Length && i < TotalSlots; i++)
            {
                var kv = parts[i].Split(':');
                if (kv.Length < 2) continue;
                ushort id = BlockRegistry.IdOf(kv[0]);
                if (id != 0 && int.TryParse(kv[1], out int n) && n > 0) { _id[i] = id; _count[i] = Mathf.Min(n, StackMax); }
            }
        }
        RebuildOrder();
        Changed?.Invoke();
    }

    public void Clear()
    {
        for (int i = 0; i < TotalSlots; i++) { _id[i] = 0; _count[i] = 0; }
        RebuildOrder();
        Changed?.Invoke();
    }

    private void RebuildOrder()
    {
        _order.Clear();
        for (int i = 0; i < TotalSlots; i++)
            if (_id[i] != 0 && !_order.Contains(_id[i])) _order.Add(_id[i]);
    }

    public bool CanAfford(Recipe r)
    {
        foreach (var (block, count) in r.Inputs)
            if (Count(BlockRegistry.IdOf(block)) < count) return false;
        return true;
    }

    public bool Craft(Recipe r)
    {
        if (!CanAfford(r)) return false;
        foreach (var (block, count) in r.Inputs)
            TryConsume(BlockRegistry.IdOf(block), count);
        Add(BlockRegistry.IdOf(r.Output.block), r.Output.count);
        return true;
    }
}
