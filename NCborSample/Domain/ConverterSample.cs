namespace NCborSample.Domain;

/// <summary>
/// Manually reproduces what NCbor.Generator's [NCborConverter] attribute automates: a property
/// whose wire encoding is delegated to a dedicated converter type instead of the default
/// type-based dispatch. Validated here before trusting the generator's own version.
/// </summary>
public static class UnixSecondsConverter
{
    public static void Write(CborWriter writer, DateTime value)
    {
        var seconds = new DateTimeOffset(value.ToUniversalTime()).ToUnixTimeSeconds();
        writer.WriteInt64(seconds);
    }

    public static DateTime Read(CborReader reader)
    {
        var seconds = reader.ReadInt64();
        return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
    }
}

public class ConverterSampleModel
{
    public string Name { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    public static void Serialize(CborWriter writer, ConverterSampleModel model)
    {
        writer.WriteStartMap(null);

        writer.WriteTextString("Name");
        writer.WriteTextString(model.Name);

        // The converter, not the default DateTime-as-tagged-ISO-string encoding, owns this field.
        writer.WriteTextString("Timestamp");
        UnixSecondsConverter.Write(writer, model.Timestamp);

        writer.WriteEndMap();
    }

    public static ConverterSampleModel Deserialize(CborReader reader)
    {
        var result = new ConverterSampleModel();
        reader.ReadStartMap();
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var propertyName = reader.ReadTextString();
            switch (propertyName)
            {
                case "Name":
                    result.Name = reader.ReadTextString();
                    break;
                case "Timestamp":
                    result.Timestamp = UnixSecondsConverter.Read(reader);
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
