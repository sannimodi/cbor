namespace NCborSample.Domain;

/// <summary>
/// Manually reproduces what NCbor.Generator emits for interface-typed and set-typed collection
/// properties: an <c>IReadOnlyList&lt;T&gt;</c> is written as a definite-length CBOR array (it has
/// a Count) and read back into a concrete <c>List&lt;T&gt;</c>; a <c>HashSet&lt;T&gt;</c> is written
/// as a definite-length CBOR array and read back into a <c>HashSet&lt;T&gt;</c>. Validated here
/// before trusting the generator's own version.
/// </summary>
public class CollectionInterfaceSample
{
    public string Name { get; set; } = "";
    public IReadOnlyList<int> Scores { get; set; } = new List<int>();
    public HashSet<string> Tags { get; set; } = new();
    public IReadOnlyList<int>? OptionalScores { get; set; }

    public static void Serialize(CborWriter writer, CollectionInterfaceSample model)
    {
        writer.WriteStartMap(null);

        writer.WriteTextString("Name");
        writer.WriteTextString(model.Name);

        writer.WriteTextString("Scores");
        WriteInt32List(writer, model.Scores);

        writer.WriteTextString("Tags");
        writer.WriteStartArray(model.Tags.Count);
        foreach (var item in model.Tags)
        {
            writer.WriteTextString(item);
        }
        writer.WriteEndArray();

        writer.WriteTextString("OptionalScores");
        if (model.OptionalScores != null) { WriteInt32List(writer, model.OptionalScores); } else { writer.WriteNull(); }

        writer.WriteEndMap();
    }

    private static void WriteInt32List(CborWriter writer, IReadOnlyList<int> value)
    {
        writer.WriteStartArray(value.Count);
        foreach (var item in value)
        {
            writer.WriteInt32(item);
        }
        writer.WriteEndArray();
    }

    private static IReadOnlyList<int> ReadInt32List(CborReader reader)
    {
        // The declared type is an interface: deserialize into the concrete List<T>.
        var list = new List<int>();
        int? length = reader.ReadStartArray();
        for (int i = 0; length == null || i < length; i++)
        {
            if (length == null && reader.PeekState() == CborReaderState.EndArray)
                break;
            list.Add(reader.ReadInt32());
        }
        reader.ReadEndArray();
        return list;
    }

    public static CollectionInterfaceSample Deserialize(CborReader reader)
    {
        var result = new CollectionInterfaceSample();
        reader.ReadStartMap();
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var propertyName = reader.ReadTextString();
            switch (propertyName)
            {
                case "Name":
                    result.Name = reader.ReadTextString();
                    break;
                case "Scores":
                    result.Scores = ReadInt32List(reader);
                    break;
                case "Tags":
                {
                    var set = new HashSet<string>();
                    int? length = reader.ReadStartArray();
                    for (int i = 0; length == null || i < length; i++)
                    {
                        if (length == null && reader.PeekState() == CborReaderState.EndArray)
                            break;
                        set.Add(reader.ReadTextString());
                    }
                    reader.ReadEndArray();
                    result.Tags = set;
                    break;
                }
                case "OptionalScores":
                    if (reader.PeekState() == CborReaderState.Null) { reader.ReadNull(); result.OptionalScores = null; }
                    else { result.OptionalScores = ReadInt32List(reader); }
                    break;
                default:
                    reader.SkipValue();
                    break;
            }
        }
        reader.ReadEndMap();
        return result;
    }
}
