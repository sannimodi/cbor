namespace NCbor.Tests;

public class NCborConverterTests
{
    private readonly TestNCborContext _context = TestNCborContext.Default;

    [Fact]
    public void Serialize_PropertyWithConverter_UsesConverterInsteadOfDefaultEncoding()
    {
        var model = new ConverterModel { Name = "a", Timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };

        var data = NCborSerializer.Serialize(model, _context.ConverterModel);

        var reader = new CborReader(data);
        reader.ReadStartMap();
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var key = reader.ReadTextString();
            if (key == "Timestamp")
            {
                // The default DateTime encoding is a tagged text string (CborReaderState.Tag).
                // The converter writes a plain signed integer (Unix seconds) instead.
                reader.PeekState().Should().Be(CborReaderState.UnsignedInteger);
                return;
            }
            reader.SkipValue();
        }

        Assert.Fail("Timestamp key not found in serialized data");
    }

    [Fact]
    public void RoundTrip_PropertyWithConverter_PreservesValue()
    {
        var timestamp = new DateTime(2026, 6, 15, 12, 30, 0, DateTimeKind.Utc);
        var model = new ConverterModel { Name = "b", Timestamp = timestamp };

        var data = NCborSerializer.Serialize(model, _context.ConverterModel);
        var result = NCborSerializer.Deserialize(data, _context.ConverterModel);

        result.Name.Should().Be("b");
        result.Timestamp.Should().Be(timestamp);
    }
}
