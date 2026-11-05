using Godot;
using PixelBlock.Combat;
using PixelBlock.Core;
using PixelBlock.Dialogue;
using PixelBlock.NpcSys;
using PixelBlock.Quests;
using PixelBlock.World;

namespace PixelBlock.Lessons;

public sealed class DavidAndGoliath : ILesson
{
    public string Id => "david";
    public string Title => "David and Goliath";
    public string Subtitle => "The Valley of Elah — 1 Samuel 17";
    public Vector3 Spawn => new(32, 3, 52);
    public Core.MusicMood Mood => Core.MusicMood.Solemn;

    private Enemy _goliath;

    public void Build(GameSession session)
    {
        var w = session.World;

        BuildTerrain(w);
        w.MarkAllDirty();
        w.RebuildAllNow();

        session.Narrator.ShowMany(new[]
        {
            "The Philistines gathered their armies for battle in the Valley of Elah.",
            "Saul and the men of Israel pitched their camp on the far hill, and the giant defied them.",
        });

        var jesse = new Npc
        {
            NpcName = "Jesse",
            Robe = new Color(0.55f, 0.4f, 0.6f),
            Accent = new Color(0.7f, 0.62f, 0.45f),
            Dialogue = Dialogues.Load("jesse"),
        };
        w.AddChild(jesse);
        jesse.GlobalPosition = new Vector3(30, 1, 49);

        var eliab = new Npc
        {
            NpcName = "Eliab",
            Robe = new Color(0.4f, 0.45f, 0.5f),
            Accent = new Color(0.5f, 0.42f, 0.3f),
            Dialogue = Dialogues.Load("eliab"),
        };
        w.AddChild(eliab);
        eliab.GlobalPosition = new Vector3(36, 1, 44);

        var giantType = EnemyType.Giant();
        giantType.Health = 160f;
        _goliath = session.SpawnEnemy(giantType, new Vector3(32, 1, 14));
        _goliath.Target = null;

        var line = session.AddTrigger(new Vector3(32, 3, 26), new Vector3(64, 8, 3), "battle-line",
            "Then Goliath of Gath came forward, towering above the valley floor.",
            "\"Am I a dog, that you come at me with sticks? Choose a man, and let him fight me!\"");
        line.Once = true;

        var brook = NarrationTrigger.Create(new Vector3(32, 2, 33), new Vector3(64, 6, 2), session.Narrator,
            "David chose five smooth stones from the brook and put them in his shepherd's bag.");
        w.AddChild(brook);
    }

    public Quest BuildQuest(GameSession session) => new()
    {
        Objectives = new[]
        {

            Quest.Talk("Jesse", "Speak with your father Jesse",
                s => s.Narrator.Show("Carry the provisions across the valley to your brothers at the battle line."),
                optional: true),
            Quest.Reach("battle-line", "Cross the valley to the battle line",
                s => { if (GodotObject.IsInstanceValid(_goliath)) _goliath.Target = s.Player; }),
            Quest.Defeat("Goliath", "Defeat Goliath with your sling"),
        },
        OnComplete = s =>
        {
            s.Narrator.ShowMany(new[]
            {
                "The stone sank into the giant's forehead, and Goliath fell to the earth.",
                "\"The battle is the LORD's, and He saves not with sword and spear.\"",
            });
            s.Hud.ShowCenter("Victory!\nDavid has defeated Goliath", 0f);
            AudioManager.SetMusicMood(MusicMood.Hope);
            AudioManager.Play("fanfare");
        },
    };

    private static void BuildTerrain(VoxelWorld w)
    {
        WorldGen.FlatGround(w, 0, 63, 0, 63, 0);

        Mound(w, 32, 60, 10, 3);
        Mound(w, 32, 4, 12, 4);
        Mound(w, 6, 32, 8, 3);
        Mound(w, 58, 32, 8, 3);

        ushort water = BlockRegistry.IdOf("water");
        ushort sand = BlockRegistry.IdOf("sand");
        for (int x = 0; x <= 63; x++)
        {
            for (int z = 30; z <= 31; z++)
            {
                w.SetBlock(x, 0, z, water, false);
                w.SetBlock(x, -1, z, water, false);
            }
            w.SetBlock(x, 0, 29, sand, false);
            w.SetBlock(x, 0, 32, sand, false);
        }

        ushort cobble = BlockRegistry.IdOf("cobblestone");
        for (int i = 0; i < 5; i++) w.SetBlock(30 + i, 1, 34, cobble, false);

        Tent(w, new Vector3I(24, 1, 52), "cloth_red");
        Tent(w, new Vector3I(38, 1, 53), "cloth_blue");
        Tent(w, new Vector3I(31, 1, 57), "cloth_cream");
        Altar(w, new Vector3I(20, 1, 49));

        Tent(w, new Vector3I(24, 1, 10), "cloth_cream");
        Tent(w, new Vector3I(40, 1, 10), "mud_brick");
    }

    private static void Mound(VoxelWorld w, int cx, int cz, int radius, int height)
    {
        ushort grass = BlockRegistry.IdOf("grass");
        ushort dirt = BlockRegistry.IdOf("dirt");
        for (int x = -radius; x <= radius; x++)
        for (int z = -radius; z <= radius; z++)
        {
            float d = Mathf.Sqrt(x * x + z * z) / radius;
            if (d > 1f) continue;
            int h = (int)Mathf.Round((1f - d) * height);
            for (int y = 1; y <= h; y++)
                w.SetBlock(cx + x, y, cz + z, y == h ? grass : dirt, false);
        }
    }

    private static void Tent(VoxelWorld w, Vector3I o, string cloth)
    {
        ushort c = BlockRegistry.IdOf(cloth);

        Layer(w, o + new Vector3I(0, 0, 0), 5, c);
        Layer(w, o + new Vector3I(1, 1, 1), 3, c);
        w.SetBlock(o + new Vector3I(2, 2, 2), c, false);

        w.SetBlock(o + new Vector3I(2, 0, 0), 0, false);
    }

    private static void Layer(VoxelWorld w, Vector3I o, int size, ushort id)
    {
        for (int x = 0; x < size; x++)
        for (int z = 0; z < size; z++)
            w.SetBlock(o + new Vector3I(x, 0, z), id, false);
    }

    private static void Altar(VoxelWorld w, Vector3I o)
    {
        ushort stone = BlockRegistry.IdOf("stone");
        ushort fire = BlockRegistry.IdOf("altar_fire");
        for (int x = 0; x < 2; x++)
        for (int z = 0; z < 2; z++)
        {
            w.SetBlock(o + new Vector3I(x, 1, z), stone, false);
            w.SetBlock(o + new Vector3I(x, 2, z), stone, false);
        }
        w.SetBlock(o + new Vector3I(0, 3, 0), fire, false);
    }
}
