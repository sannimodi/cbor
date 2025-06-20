using FluentAssertions;

namespace CbOrSerialization.Tests;

public class SimpleDecimalModelTest
{
    private static readonly TestCbOrContext _context = TestCbOrContext.Default;

    [Fact]
    public void SimpleDecimalModel_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var original = new SimpleDecimalModel
        {
            Value = 123.456m,
            Name = "Test Model"
        };

        // Act
        var serialized = CbOrSerializer.Serialize(original, _context.SimpleDecimalModel);
        var deserialized = CbOrSerializer.Deserialize<SimpleDecimalModel>(serialized, _context.SimpleDecimalModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Value.Should().Be(original.Value);
        deserialized.Name.Should().Be(original.Name);
    }

    [Fact]
    public void SimpleDecimalModel_ShouldHandleZeroAndNegativeValues()
    {
        // Arrange
        var original = new SimpleDecimalModel
        {
            Value = -0.00001m,
            Name = "Negative Test"
        };

        // Act
        var serialized = CbOrSerializer.Serialize(original, _context.SimpleDecimalModel);
        var deserialized = CbOrSerializer.Deserialize<SimpleDecimalModel>(serialized, _context.SimpleDecimalModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Value.Should().Be(original.Value);
        deserialized.Name.Should().Be(original.Name);
    }

    [Fact]
    public void SimpleDecimalModel_ShouldHandleLargeValues()
    {
        // Arrange
        var original = new SimpleDecimalModel
        {
            Value = decimal.MaxValue,
            Name = "Max Value Test"
        };

        // Act
        var serialized = CbOrSerializer.Serialize(original, _context.SimpleDecimalModel);
        var deserialized = CbOrSerializer.Deserialize<SimpleDecimalModel>(serialized, _context.SimpleDecimalModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Value.Should().Be(original.Value);
        deserialized.Name.Should().Be(original.Name);
    }
}