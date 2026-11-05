using Godot;

namespace PixelBlock.Core;

public interface IDamageable
{
    void TakeDamage(float amount, Node3D source);
    bool IsAlive { get; }
}
