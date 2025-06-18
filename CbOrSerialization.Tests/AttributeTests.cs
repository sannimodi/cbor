using System.Formats.Cbor;

namespace CbOrSerialization.Tests;

public class AttributeTests
{
    private readonly TestCbOrContext _context = TestCbOrContext.Default;

    [Fact]
    public void CborPropertyName_CustomName_IsUsedInSerialization()
    {
        // Arrange
        var model = new ModelWithAttributes
        {
            Name = "Test Name",
            Age = 30
        };

        // Act
        var data = CbOrSerializer.Serialize(model, _context.ModelWithAttributes);

        // Assert
        var reader = new CborReader(data);
        reader.ReadStartMap();
        
        var propertyNames = new List<string>();
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            propertyNames.Add(reader.ReadTextString());
            reader.SkipValue();
        }

        propertyNames.Should().Contain("full_name"); // Custom name from attribute
        propertyNames.Should().NotContain("Name"); // Original name should not be present
    }

    [Fact]
    public void CborIgnore_IgnoredProperty_NotIncludedInSerialization()
    {
        // Arrange
        var model = new ModelWithAttributes
        {
            Name = "Test",
            Age = 25,
            InternalId = "should-be-ignored"
        };

        // Act
        var data = CbOrSerializer.Serialize(model, _context.ModelWithAttributes);

        // Assert
        var reader = new CborReader(data);
        reader.ReadStartMap();
        
        var propertyNames = new List<string>();
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            propertyNames.Add(reader.ReadTextString());
            reader.SkipValue();
        }

        propertyNames.Should().NotContain("InternalId");
    }

    [Fact]
    public void CborIgnore_RoundTrip_IgnoredPropertyNotAffected()
    {
        // Arrange
        var original = new ModelWithAttributes
        {
            Name = "Test",
            Age = 25,
            InternalId = "original-value"
        };

        // Act
        var data = CbOrSerializer.Serialize(original, _context.ModelWithAttributes);
        var deserialized = CbOrSerializer.Deserialize(data, _context.ModelWithAttributes);

        // Assert
        deserialized.Name.Should().Be(original.Name);
        deserialized.Age.Should().Be(original.Age);
        deserialized.InternalId.Should().Be(string.Empty); // Default value, not serialized
    }

    [Fact]
    public void CborDefaultValue_DefaultValue_HandledCorrectly()
    {
        // Arrange
        var model1 = new ModelWithAttributes
        {
            Name = "Test1",
            Age = 30,
            IsEnabled = true // Default value
        };

        var model2 = new ModelWithAttributes
        {
            Name = "Test2", 
            Age = 35,
            IsEnabled = false // Non-default value
        };

        // Act
        var data1 = CbOrSerializer.Serialize(model1, _context.ModelWithAttributes);
        var data2 = CbOrSerializer.Serialize(model2, _context.ModelWithAttributes);

        var deserialized1 = CbOrSerializer.Deserialize(data1, _context.ModelWithAttributes);
        var deserialized2 = CbOrSerializer.Deserialize(data2, _context.ModelWithAttributes);

        // Assert
        deserialized1.IsEnabled.Should().Be(true);
        deserialized2.IsEnabled.Should().Be(false);
    }

    [Fact]
    public void CborSerializable_RegisteredTypes_CanBeUsed()
    {
        // Act & Assert - If these don't throw, the types are properly registered
        var act1 = () => _context.GetTypeInfo<SimpleModel>();
        var act2 = () => _context.GetTypeInfo<ModelWithAttributes>();
        var act3 = () => _context.GetTypeInfo<List<SimpleModel>>();

        act1.Should().NotThrow();
        act2.Should().NotThrow();
        act3.Should().NotThrow();
    }

    [Fact]
    public void CborSerializable_NonRegisteredType_ThrowsException()
    {
        // Act & Assert
        var act = () => _context.GetTypeInfo<Dictionary<string, int>>(); // Not registered
        act.Should().Throw<ArgumentException>()
           .WithMessage("*not registered for serialization*");
    }

    [Fact]
    public void Attributes_MultipleModels_WorkIndependently()
    {
        // Arrange
        var simple = new SimpleModel { Name = "Simple", Age = 10, IsActive = true };
        var attributed = new ModelWithAttributes { Name = "Attributed", Age = 20 };

        // Act
        var simpleData = CbOrSerializer.Serialize(simple, _context.SimpleModel);
        var attributedData = CbOrSerializer.Serialize(attributed, _context.ModelWithAttributes);

        var deserializedSimple = CbOrSerializer.Deserialize(simpleData, _context.SimpleModel);
        var deserializedAttributed = CbOrSerializer.Deserialize(attributedData, _context.ModelWithAttributes);

        // Assert
        deserializedSimple.Name.Should().Be("Simple");
        deserializedAttributed.Name.Should().Be("Attributed");

        // Verify different property names are used
        simpleData.Should().NotEqual(attributedData);
    }

    [Fact]
    public void PropertyName_SpecialCharacters_HandledCorrectly()
    {
        // This test verifies that property names with special characters work
        // The ModelWithAttributes uses "full_name" which contains an underscore

        // Arrange
        var model = new ModelWithAttributes { Name = "Test", Age = 25 };

        // Act & Assert - Should not throw
        var act = () =>
        {
            var data = CbOrSerializer.Serialize(model, _context.ModelWithAttributes);
            return CbOrSerializer.Deserialize(data, _context.ModelWithAttributes);
        };

        act.Should().NotThrow();
        var result = act();
        result.Name.Should().Be("Test");
    }

    [Fact]
    public void Attributes_CaseInsensitive_PropertiesHandledCorrectly()
    {
        // Arrange
        var model = new ModelWithAttributes
        {
            Name = "Case Test",
            Age = 42
        };

        // Act
        var data = CbOrSerializer.Serialize(model, _context.ModelWithAttributes);
        var deserialized = CbOrSerializer.Deserialize(data, _context.ModelWithAttributes);

        // Assert
        deserialized.Name.Should().Be("Case Test");
        deserialized.Age.Should().Be(42);
    }
}