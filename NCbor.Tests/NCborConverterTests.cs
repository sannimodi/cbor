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

    [Fact]
    public void Converter_IsCachedNotAllocatedPerCall()
    {
        // Regression test: GeneratePropertySerialization/Deserialization previously emitted
        // `new {converterType}().Write(...)`/`.Read(...)` at every dispatch site, allocating a
        // fresh converter instance on every single call. The generator now caches one static
        // instance per distinct converter type on the TypeInfo class. CountingConverter's
        // static constructor counter proves it: regardless of how many times Serialize/
        // Deserialize run, and regardless of how many properties on the model share the same
        // converter type (two, here), the instance count must stay at exactly 1 (created once,
        // the first time the static field initializer runs — which count itself, not zero,
        // depends on test execution order across the assembly, so this only asserts it doesn't
        // grow across repeated calls, not that it's the assembly's very first use).
        var model = new ConverterCachingModel { A = "one", B = "two" };

        // Force at least one use before sampling, so the static field is guaranteed initialized.
        var bytes = NCborSerializer.Serialize(model, _context.ConverterCachingModel);
        NCborSerializer.Deserialize(bytes, _context.ConverterCachingModel);
        var countAfterFirstUse = CountingConverter.InstancesCreated;

        for (int i = 0; i < 10; i++)
        {
            var data = NCborSerializer.Serialize(model, _context.ConverterCachingModel);
            NCborSerializer.Deserialize(data, _context.ConverterCachingModel);
        }

        CountingConverter.InstancesCreated.Should().Be(countAfterFirstUse,
            "the converter should be cached once per converter type, not re-instantiated per call");
    }

    [Fact]
    public void RoundTrip_ConverterCachingModel_BothPropertiesSharingConverterType_RoundTrip()
    {
        var model = new ConverterCachingModel { A = "one", B = "two" };

        var data = NCborSerializer.Serialize(model, _context.ConverterCachingModel);
        var result = NCborSerializer.Deserialize(data, _context.ConverterCachingModel);

        result.A.Should().Be("one");
        result.B.Should().Be("two");
    }
}
