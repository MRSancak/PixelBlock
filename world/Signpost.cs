using Godot;

namespace PixelBlock.World;

public sealed partial class Signpost : Node3D
{
    public string Text = "";
    public string Title = "";

    public static Signpost Create(Vector3 position, string text, string title = null)
    {
        var s = new Signpost { Text = text, Title = title ?? FirstLine(text) };
        s.Position = position;
        return s;
    }

    private static string FirstLine(string t)
    {
        if (string.IsNullOrWhiteSpace(t)) return "Sign";
        int nl = t.IndexOf('\n');
        string s = (nl >= 0 ? t.Substring(0, nl) : t).Trim();
        return s.Length > 22 ? s.Substring(0, 21) + "…" : s;
    }

    public override void _Ready()
    {
        AddToGroup("signpost");

        var postMat = new StandardMaterial3D { AlbedoColor = new Color(0.42f, 0.28f, 0.15f), Roughness = 0.9f };
        var boardMat = new StandardMaterial3D { AlbedoColor = new Color(0.63f, 0.47f, 0.28f), Roughness = 0.85f };

        var post = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.12f, 1.6f, 0.12f) },
            MaterialOverride = postMat,
            Position = new Vector3(0, 0.8f, 0),
        };
        AddChild(post);

        var board = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.7f, 0.95f, 0.1f) },
            MaterialOverride = boardMat,
            Position = new Vector3(0, 1.95f, 0),
        };
        AddChild(board);

        if (!string.IsNullOrEmpty(Title))
        {

            var label = new Label3D
            {
                Text = Title,
                Position = new Vector3(0, 1.95f, 0.061f),
                FontSize = 48,
                PixelSize = 0.0024f,
                Modulate = new Color(0.16f, 0.09f, 0.03f),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 660,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            AddChild(label);
        }
    }
}
