using System.Formats.Cbor;

namespace CbOrSerialization.Tests;

public class CbOrSerializerTests
{
    private readonly TestCbOrContext _context = TestCbOrContext.Default;

    [Fact]
    public void Serialize_SimpleModel_ReturnsValidCborData()
    {
        // Arrange
        var model = new SimpleModel
        {
            Name = "John Doe",
            Age = 30,
            IsActive = true
        };

        // Act
        var result = CbOrSerializer.Serialize(model, _context.SimpleModel);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        
        // Verify it's valid CBOR by attempting to read it
        var reader = new CborReader(result);
        reader.PeekState().Should().Be(CborReaderState.StartMap);
    }

    [Fact]
    public void Deserialize_ValidCborData_ReturnsCorrectModel()
    {
        // Arrange
        var original = new SimpleModel
        {
            Name = "Jane Smith",
            Age = 25,
            IsActive = false
        };
        var cborData = CbOrSerializer.Serialize(original, _context.SimpleModel);

        // Act
        var deserialized = CbOrSerializer.Deserialize(cborData, _context.SimpleModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Name.Should().Be(original.Name);
        deserialized.Age.Should().Be(original.Age);
        deserialized.IsActive.Should().Be(original.IsActive);
    }

    [Fact]
    public void RoundTrip_SimpleModel_PreservesAllData()
    {
        // Arrange
        var original = new SimpleModel
        {
            Name = "Test User",
            Age = 42,
            IsActive = true
        };

        // Act
        var serialized = CbOrSerializer.Serialize(original, _context.SimpleModel);
        var deserialized = CbOrSerializer.Deserialize(serialized, _context.SimpleModel);

        // Assert
        deserialized.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void Serialize_ModelWithAttributes_RespectsCustomPropertyNames()
    {
        // Arrange
        var model = new ModelWithAttributes
        {
            Name = "Test Name",
            Age = 35,
            InternalId = "should-be-ignored",
            IsEnabled = false
        };

        // Act
        var result = CbOrSerializer.Serialize(model, _context.ModelWithAttributes);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();

        // Verify the CBOR contains the custom property name "full_name"
        var reader = new CborReader(result);
        reader.ReadStartMap();
        
        var propertyNames = new List<string>();
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            propertyNames.Add(reader.ReadTextString());
            reader.SkipValue(); // Skip the property value
        }
        
        propertyNames.Should().Contain("full_name");
        propertyNames.Should().NotContain("Name");
        propertyNames.Should().NotContain("InternalId"); // Should be ignored
    }

    [Fact]
    public void Serialize_List_ReturnsValidCborArray()
    {
        // Arrange
        var models = new List<SimpleModel>
        {
            new() { Name = "Model 1", Age = 10, IsActive = true },
            new() { Name = "Model 2", Age = 20, IsActive = false }
        };

        // Act
        var result = CbOrSerializer.Serialize(models, _context.ListOfSimpleModel);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();

        // Verify it's a CBOR array
        var reader = new CborReader(result);
        reader.PeekState().Should().Be(CborReaderState.StartArray);
    }

    [Fact]
    public void RoundTrip_List_PreservesAllItems()
    {
        // Arrange
        var original = new List<SimpleModel>
        {
            new() { Name = "First", Age = 1, IsActive = true },
            new() { Name = "Second", Age = 2, IsActive = false },
            new() { Name = "Third", Age = 3, IsActive = true }
        };

        // Act
        var serialized = CbOrSerializer.Serialize(original, _context.ListOfSimpleModel);
        var deserialized = CbOrSerializer.Deserialize(serialized, _context.ListOfSimpleModel);

        // Assert
        deserialized.Should().HaveCount(original.Count);
        deserialized.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void Serialize_AllTypesModel_HandlesAllPrimitiveTypes()
    {
        // Arrange
        var model = new AllTypesModel
        {
            StringValue = "test string",
            IntValue = 123,
            LongValue = 456789L,
            DoubleValue = 123.456,
            FloatValue = 78.9f,
            BoolValue = true,
            ByteValue = 255,
            SByteValue = -128,
            ShortValue = -32768,
            UShortValue = 65535,
            UIntValue = 4294967295U,
            ULongValue = 18446744073709551615UL,
            NullableInt = 42,
            NullableBool = null
        };

        // Act & Assert - Should not throw
        var result = CbOrSerializer.Serialize(model, _context.AllTypesModel);
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();

        // Verify round trip works
        var deserialized = CbOrSerializer.Deserialize(result, _context.AllTypesModel);
        deserialized.StringValue.Should().Be(model.StringValue);
        deserialized.IntValue.Should().Be(model.IntValue);
        deserialized.BoolValue.Should().Be(model.BoolValue);
        // Add more assertions as needed
    }

    [Fact]
    public void Serialize_EmptyString_HandlesCorrectly()
    {
        // Arrange
        var model = new SimpleModel { Name = "", Age = 0, IsActive = false };

        // Act
        var result = CbOrSerializer.Serialize(model, _context.SimpleModel);
        var deserialized = CbOrSerializer.Deserialize(result, _context.SimpleModel);

        // Assert
        deserialized.Name.Should().Be("");
        deserialized.Age.Should().Be(0);
        deserialized.IsActive.Should().Be(false);
    }

    [Fact]
    public void Serialize_LargeString_HandlesCorrectly()
    {
        // Arrange
        var largeString = new string('A', 10000);
        var model = new SimpleModel { Name = largeString, Age = 1, IsActive = true };

        // Act
        var result = CbOrSerializer.Serialize(model, _context.SimpleModel);
        var deserialized = CbOrSerializer.Deserialize(result, _context.SimpleModel);

        // Assert
        deserialized.Name.Should().Be(largeString);
        deserialized.Name.Should().HaveLength(10000);
    }
}