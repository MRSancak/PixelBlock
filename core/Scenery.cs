using Godot;

namespace PixelBlock.Core;

public static class Scenery
{
    public static EnvironmentController AddDaylight(Node parent)
    {
        var env = new EnvironmentController { Name = "Environment" };
        parent.AddChild(env);
        return env;
    }
}
