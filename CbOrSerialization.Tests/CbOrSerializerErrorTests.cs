namespace CbOrSerialization.Tests;

public class CbOrSerializerErrorTests
{
    private readonly TestCbOrContext _context = TestCbOrContext.Default;

    [Fact]
    public void Serialize_NullTypeInfo_ThrowsArgumentNullException()
    {
        // Arrange
        var model = new SimpleModel { Name = "Test", Age = 30, IsActive = true };

        // Act & Assert
        var act = () => CbOrSerializer.Serialize(model, null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("typeInfo");
    }

    [Fact]
    public void Deserialize_NullData_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => CbOrSerializer.Deserialize(null!, _context.SimpleModel);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("data");
    }

    [Fact]
    public void Deserialize_NullTypeInfo_ThrowsArgumentNullException()
    {
        // Arrange
        var validData = new byte[] { 0xA0 }; // Empty CBOR map

        // Act & Assert
        var act = () => CbOrSerializer.Deserialize<SimpleModel>(validData, null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("typeInfo");
    }

    [Fact]
    public void Deserialize_EmptyData_ThrowsArgumentException()
    {
        // Arrange
        var emptyData = Array.Empty<byte>();

        // Act & Assert
        var act = () => CbOrSerializer.Deserialize(emptyData, _context.SimpleModel);
        act.Should().Throw<ArgumentException>()
           .WithParameterName("data")
           .WithMessage("CBOR data cannot be empty.*");
    }

    [Fact]
    public void Deserialize_InvalidCbOrData_ThrowsFormatException()
    {
        // Arrange
        var invalidData = new byte[] { 0xFF, 0xFE, 0xFD }; // Invalid CBOR

        // Act & Assert
        var act = () => CbOrSerializer.Deserialize(invalidData, _context.SimpleModel);
        act.Should().Throw<Exception>()
           .And.Message.Should().Contain("type SimpleModel");
    }

    [Fact]
    public void Deserialize_PartialCbOrData_ThrowsException()
    {
        // Arrange
        var partialData = new byte[] { 0xA1 }; // Start of map but incomplete

        // Act & Assert
        var act = () => CbOrSerializer.Deserialize(partialData, _context.SimpleModel);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Deserialize_WrongCbOrType_ThrowsException()
    {
        // Arrange
        var stringData = new byte[] { 0x64, 0x74, 0x65, 0x73, 0x74 }; // CBOR string "test"

        // Act & Assert
        var act = () => CbOrSerializer.Deserialize(stringData, _context.SimpleModel);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Serialize_VeryLargeObject_DoesNotThrow()
    {
        // Arrange
        var largeList = new List<SimpleModel>();
        for (int i = 0; i < 1000; i++)
        {
            largeList.Add(new SimpleModel 
            { 
                Name = $"Item {i}", 
                Age = i, 
                IsActive = i % 2 == 0 
            });
        }

        // Act & Assert - Should not throw for large but valid objects
        var act = () => CbOrSerializer.Serialize(largeList, _context.ListOfSimpleModel);
        act.Should().NotThrow();
        
        var result = act();
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(1000); // Should be substantial size
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1)]
    public void Serialize_EdgeCaseIntegers_HandlesCorrectly(int value)
    {
        // Arrange
        var model = new SimpleModel { Name = "Test", Age = value, IsActive = true };

        // Act
        var serialized = CbOrSerializer.Serialize(model, _context.SimpleModel);
        var deserialized = CbOrSerializer.Deserialize(serialized, _context.SimpleModel);

        // Assert
        deserialized.Age.Should().Be(value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A")]
    [InlineData("Unicode: 🔥🚀💯")]
    [InlineData("Multi\nLine\nString")]
    [InlineData("Tabs\tAnd\tSpaces   ")]
    public void Serialize_VariousStrings_HandlesCorrectly(string value)
    {
        // Arrange
        var model = new SimpleModel { Name = value, Age = 1, IsActive = true };

        // Act
        var serialized = CbOrSerializer.Serialize(model, _context.SimpleModel);
        var deserialized = CbOrSerializer.Deserialize(serialized, _context.SimpleModel);

        // Assert
        deserialized.Name.Should().Be(value);
    }
}

