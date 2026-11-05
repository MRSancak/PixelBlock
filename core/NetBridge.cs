using Godot;

namespace PixelBlock.Core;

public static class NetBridge
{
    public static System.Action<Vector3I, ushort> LocalBlockChanged;
    public static void BlockChanged(Vector3I cell, ushort id) => LocalBlockChanged?.Invoke(cell, id);
}
