using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using PixelBlock.Dialogue;

namespace PixelBlock.Lessons;

public sealed class LessonDoc
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Subtitle { get; set; } = "";
    public float[] Spawn { get; set; }
    public string Time { get; set; }
    public string Mood { get; set; }
    public string Mode { get; set; }
    public bool Disarm { get; set; }

    [JsonConverter(typeof(StringOrArrayConverter))]
    public string[] Intro { get; set; }

    public List<TerrainOp> Terrain { get; set; } = new();
    public List<NarrationDto> Narrations { get; set; } = new();
    public List<NpcDto> Npcs { get; set; } = new();
    public List<EnemyDto> Enemies { get; set; } = new();
    public QuestDto Quest { get; set; }
    public ChapterDto Chapter { get; set; }
}

public sealed class TerrainOp
{
    public string Op { get; set; }
    public float[] At { get; set; }
    public float[] From { get; set; }
    public float[] To { get; set; }
    public string Block { get; set; }
    public int X0 { get; set; } public int X1 { get; set; }
    public int Z0 { get; set; } public int Z1 { get; set; } public int Y { get; set; }
    public int X { get; set; } public int Z { get; set; } public int R { get; set; } public int H { get; set; }
    public int W { get; set; } public int D { get; set; }
    public int Height { get; set; }
    public string Top { get; set; } public string Fill { get; set; } public string Base { get; set; } public int Depth { get; set; }
    public string Wall { get; set; } public string Roof { get; set; } public string Cloth { get; set; }
}

public sealed class NarrationDto
{
    public string Id { get; set; }
    public float[] Pos { get; set; }
    public float[] Size { get; set; }
    public bool Once { get; set; } = true;

    [JsonConverter(typeof(StringOrArrayConverter))]
    public string[] Lines { get; set; }
}

public sealed class NpcDto
{
    public string Name { get; set; } = "Villager";
    public float[] Pos { get; set; }
    public bool Beast { get; set; }
    public float[] Skin { get; set; }
    public float[] Robe { get; set; }
    public float[] Accent { get; set; }

    [JsonConverter(typeof(StringOrArrayConverter))]
    public string[] Say { get; set; }
    public string DialogueId { get; set; }
    public DialogueData Dialogue { get; set; }
}

public sealed class EnemyDto
{
    public string Type { get; set; } = "Soldier";
    public string Name { get; set; }
    public float[] Pos { get; set; }
    public float? Health { get; set; }
    public float? Scale { get; set; }
    public bool Dormant { get; set; }
}

public sealed class QuestDto
{
    public List<ObjectiveDto> Objectives { get; set; } = new();
    public EffectDto OnComplete { get; set; }
}

public sealed class ObjectiveDto
{
    public string Kind { get; set; }
    public string Key { get; set; }
    public string Block { get; set; }
    public int Count { get; set; } = 1;
    public string Label { get; set; } = "";
    public bool Optional { get; set; }
    public EffectDto OnComplete { get; set; }
}

public sealed class EffectDto
{
    [JsonConverter(typeof(StringOrArrayConverter))]
    public string[] Narrate { get; set; }
    public string Center { get; set; }
    public string Banner { get; set; }
    public string Sound { get; set; }
    public string Mood { get; set; }
    public string Wake { get; set; }
    public FxDto Fx { get; set; }
}

public sealed class FxDto
{
    public string Kind { get; set; }
    public float[] At { get; set; }
    public float[] Tint { get; set; }
    public int Count { get; set; }
}

public sealed class ChapterDto
{
    public string[] Requires { get; set; }
    public int Order { get; set; }
}

public sealed class StringOrArrayConverter : JsonConverter<string[]>
{
    public override string[] Read(ref Utf8JsonReader reader, System.Type t, JsonSerializerOptions o)
    {
        if (reader.TokenType == JsonTokenType.String) return new[] { reader.GetString() };
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<string>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String) list.Add(reader.GetString());
                else reader.Skip();
            }
            return list.ToArray();
        }
        if (reader.TokenType == JsonTokenType.Null) return null;
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter w, string[] v, JsonSerializerOptions o)
    {
        w.WriteStartArray();
        if (v != null) foreach (string s in v) w.WriteStringValue(s);
        w.WriteEndArray();
    }
}
