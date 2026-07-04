namespace NCbor.Tests;

public class NCborDefaultIgnoreConditionTests
{
    private readonly DefaultIgnoreConditionContext _context = DefaultIgnoreConditionContext.Default;

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
    public void ContextDefault_WhenWritingDefault_AppliesToPropertiesWithNoOwnIgnoreAttribute()
    {
        var model = new DefaultIgnoreConditionModel { Name = "a", Count = 0 };

        var data = NCborSerializer.Serialize(model, _context.DefaultIgnoreConditionModel);

        ReadPropertyNames(data).Should().NotContain("Count");
    }

    [Fact]
    public void ContextDefault_WhenWritingDefault_NonDefaultValueStillIncluded()
    {
        var model = new DefaultIgnoreConditionModel { Name = "a", Count = 5 };

        var data = NCborSerializer.Serialize(model, _context.DefaultIgnoreConditionModel);
        var result = NCborSerializer.Deserialize(data, _context.DefaultIgnoreConditionModel);

        ReadPropertyNames(data).Should().Contain("Count");
        result.Count.Should().Be(5);
    }

    [Fact]
    public void PropertyLevelNever_OverridesContextDefault()
    {
        var model = new DefaultIgnoreConditionModel { Name = "a", AlwaysIncluded = 0 };

        var data = NCborSerializer.Serialize(model, _context.DefaultIgnoreConditionModel);

        ReadPropertyNames(data).Should().Contain("AlwaysIncluded");
    }

    [Fact]
    public void NoDefaultIgnoreConditionSet_UnsetFallsBackToNever_NotAlways()
    {
        // Regression guard: NCborSourceGenerationOptionsAttribute.DefaultIgnoreCondition has no
        // field initializer, so its literal C# default is enum value 0 (Always) purely from
        // member ordering. If the generator ever naively read that as the "unset" fallback,
        // every property without its own [NCborIgnore] would vanish from every context that
        // doesn't explicitly set DefaultIgnoreCondition - which is every context in this repo
        // before this test existed.
        var model = new SimpleModel { Name = "x", Age = 0, IsActive = false };

        var data = NCborSerializer.Serialize(model, TestNCborContext.Default.SimpleModel);

        ReadPropertyNames(data).Should().Contain("Age");
        ReadPropertyNames(data).Should().Contain("IsActive");
    }
}
