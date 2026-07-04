namespace NCbor.Tests;

public class NCborIgnoreConditionTests
{
    private readonly TestNCborContext _context = TestNCborContext.Default;

    private static List<string> ReadPropertyNames(byte[] data)
    {
        var reader = new CborReader(data);
        reader.ReadStartMap();

        var propertyNames = new List<string>();
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            propertyNames.Add(reader.ReadTextString());
            reader.SkipValue();
        }

        return propertyNames;
    }

    [Fact]
    public void Never_PropertyPresent_IsAlwaysIncludedEvenAtDefaultValue()
    {
        var model = new IgnoreConditionModel { Name = "a", AlwaysIncluded = 0 };

        var data = NCborSerializer.Serialize(model, _context.IgnoreConditionModel);

        ReadPropertyNames(data).Should().Contain("AlwaysIncluded");
    }

    [Fact]
    public void WhenWritingNull_ValueIsNull_PropertyOmittedFromMap()
    {
        var model = new IgnoreConditionModel { Name = "a", OptionalNote = null };

        var data = NCborSerializer.Serialize(model, _context.IgnoreConditionModel);

        ReadPropertyNames(data).Should().NotContain("OptionalNote");
    }

    [Fact]
    public void WhenWritingNull_ValueIsNotNull_PropertyIncludedAndRoundTrips()
    {
        var model = new IgnoreConditionModel { Name = "a", OptionalNote = "hello" };

        var data = NCborSerializer.Serialize(model, _context.IgnoreConditionModel);
        var result = NCborSerializer.Deserialize(data, _context.IgnoreConditionModel);

        ReadPropertyNames(data).Should().Contain("OptionalNote");
        result.OptionalNote.Should().Be("hello");
    }

    [Fact]
    public void WhenWritingDefault_ValueIsDefault_PropertyOmittedFromMap()
    {
        var model = new IgnoreConditionModel { Name = "a", Count = 0 };

        var data = NCborSerializer.Serialize(model, _context.IgnoreConditionModel);
        var result = NCborSerializer.Deserialize(data, _context.IgnoreConditionModel);

        ReadPropertyNames(data).Should().NotContain("Count");
        result.Count.Should().Be(0);
    }

    [Fact]
    public void WhenWritingDefault_ValueIsNonDefault_PropertyIncludedAndRoundTrips()
    {
        var model = new IgnoreConditionModel { Name = "a", Count = 7 };

        var data = NCborSerializer.Serialize(model, _context.IgnoreConditionModel);
        var result = NCborSerializer.Deserialize(data, _context.IgnoreConditionModel);

        ReadPropertyNames(data).Should().Contain("Count");
        result.Count.Should().Be(7);
    }

    [Fact]
    public void NCborDefaultValue_ValueMatchesDeclaredDefault_PropertyOmittedFromMap()
    {
        var model = new ModelWithAttributes { Name = "a", Age = 1, IsEnabled = true };

        var data = NCborSerializer.Serialize(model, _context.ModelWithAttributes);

        ReadPropertyNames(data).Should().NotContain("IsEnabled");
    }

    [Fact]
    public void NCborDefaultValue_ValueDiffersFromDeclaredDefault_PropertyIncluded()
    {
        var model = new ModelWithAttributes { Name = "a", Age = 1, IsEnabled = false };

        var data = NCborSerializer.Serialize(model, _context.ModelWithAttributes);

        ReadPropertyNames(data).Should().Contain("IsEnabled");
    }
}
