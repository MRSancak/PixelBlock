using Godot;
using PixelBlock.UI;

namespace PixelBlock.World;

public partial class NarrationTrigger : Area3D
{
    [Signal] public delegate void EnteredEventHandler();

    public string Id = null;

    public string[] Lines = System.Array.Empty<string>();
    public bool Once = true;
    public Narrator Narrator;
    private bool _fired;

    public static NarrationTrigger Create(Vector3 position, Vector3 size, Narrator narrator, params string[] lines)
    {
        var t = new NarrationTrigger { Lines = lines, Narrator = narrator };
        t.Position = position;
        var shape = new CollisionShape3D { Shape = new BoxShape3D { Size = size } };
        t.AddChild(shape);
        t.BodyEntered += t.OnBodyEntered;
        return t;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_fired && Once) return;
        if (body is not PlayerSys.Player) return;
        _fired = true;
        Narrator?.ShowMany(Lines);
        EmitSignal(SignalName.Entered);
    }
}
