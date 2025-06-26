namespace NCbor.Tests;

public class NCborDecimalTests
{
    private readonly TestNCborContext _context = TestNCborContext.Default;

    #region Basic Decimal Serialization Tests

    [Fact]
    public void Serialize_DecimalModel_WithValidDecimal_SerializesCorrectly()
    {
        // Arrange
        var model = new DecimalModel 
        { 
            Value = 123.456m,
            Name = "Test Model", 
            OptionalValue = null,
            Zero = 0m,
            Large = 99999999999999999999999999.99m,
            Small = 0.0001m,
            Negative = -456.789m
        };

        // Act
        var serialized = NCborSerializer.Serialize(model, _context.DecimalModel);

        // Assert
        serialized.Should().NotBeNull();
        serialized.Should().NotBeEmpty();
    }

    [Fact]
    public void Serialize_Then_Deserialize_DecimalModel_RoundTripSucceeds()
    {
        // Arrange
        var original = new DecimalModel 
        { 
            Value = 123.456m,
            Name = "Test Model", 
            OptionalValue = null,
            Zero = 0m,
            Large = 99999999999999999999999999.99m,
            Small = 0.0001m,
            Negative = -456.789m
        };

        // Act
        var serialized = NCborSerializer.Serialize(original, _context.DecimalModel);
        var deserialized = NCborSerializer.Deserialize(serialized, _context.DecimalModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Value.Should().Be(123.456m);
        deserialized.Name.Should().Be("Test Model");
        deserialized.OptionalValue.Should().BeNull();
        deserialized.Zero.Should().Be(0m);
        deserialized.Large.Should().Be(99999999999999999999999999.99m);
        deserialized.Small.Should().Be(0.0001m);
        deserialized.Negative.Should().Be(-456.789m);
    }

    [Fact]
    public void Serialize_Then_Deserialize_DecimalModel_WithNullableValue_RoundTripSucceeds()
    {
        // Arrange
        var original = new DecimalModel 
        { 
            Value = 999.99m,
            Name = "Test with Nullable", 
            OptionalValue = 42.42m,
            Zero = 0m,
            Large = 123.45m,
            Small = 0.01m,
            Negative = -0.99m
        };

        // Act
        var serialized = NCborSerializer.Serialize(original, _context.DecimalModel);
        var deserialized = NCborSerializer.Deserialize(serialized, _context.DecimalModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Value.Should().Be(999.99m);
        deserialized.Name.Should().Be("Test with Nullable");
        deserialized.OptionalValue.Should().Be(42.42m);
        deserialized.Zero.Should().Be(0m);
        deserialized.Large.Should().Be(123.45m);
        deserialized.Small.Should().Be(0.01m);
        deserialized.Negative.Should().Be(-0.99m);
    }

    #endregion

    #region Edge Cases and Special Values

    [Fact]
    public void Serialize_Then_Deserialize_DecimalModel_WithZero_RoundTripSucceeds()
    {
        // Arrange
        var original = new DecimalModel 
        { 
            Value = 0m,
            Name = "Zero Test", 
            OptionalValue = 0m,
            Zero = 0m,
            Large = 0m,
            Small = 0m,
            Negative = 0m
        };

        // Act
        var serialized = NCborSerializer.Serialize(original, _context.DecimalModel);
        var deserialized = NCborSerializer.Deserialize(serialized, _context.DecimalModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Value.Should().Be(0m);
        deserialized.Name.Should().Be("Zero Test");
        deserialized.OptionalValue.Should().Be(0m);
        deserialized.Zero.Should().Be(0m);
        deserialized.Large.Should().Be(0m);
        deserialized.Small.Should().Be(0m);
        deserialized.Negative.Should().Be(0m);
    }

    [Fact]
    public void Serialize_Then_Deserialize_DecimalModel_WithMaxValue_RoundTripSucceeds()
    {
        // Arrange
        var original = new DecimalModel 
        { 
            Value = decimal.MaxValue,
            Name = "Max Value Test", 
            OptionalValue = decimal.MaxValue,
            Zero = decimal.MaxValue,
            Large = decimal.MaxValue,
            Small = decimal.MaxValue,
            Negative = decimal.MaxValue
        };

        // Act
        var serialized = NCborSerializer.Serialize(original, _context.DecimalModel);
        var deserialized = NCborSerializer.Deserialize(serialized, _context.DecimalModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Value.Should().Be(decimal.MaxValue);
        deserialized.Name.Should().Be("Max Value Test");
        deserialized.OptionalValue.Should().Be(decimal.MaxValue);
        deserialized.Zero.Should().Be(decimal.MaxValue);
        deserialized.Large.Should().Be(decimal.MaxValue);
        deserialized.Small.Should().Be(decimal.MaxValue);
        deserialized.Negative.Should().Be(decimal.MaxValue);
    }

    [Fact]
    public void Serialize_Then_Deserialize_DecimalModel_WithMinValue_RoundTripSucceeds()
    {
        // Arrange
        var original = new DecimalModel 
        { 
            Value = decimal.MinValue,
            Name = "Min Value Test", 
            OptionalValue = decimal.MinValue,
            Zero = decimal.MinValue,
            Large = decimal.MinValue,
            Small = decimal.MinValue,
            Negative = decimal.MinValue
        };

        // Act
        var serialized = NCborSerializer.Serialize(original, _context.DecimalModel);
        var deserialized = NCborSerializer.Deserialize(serialized, _context.DecimalModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Value.Should().Be(decimal.MinValue);
        deserialized.Name.Should().Be("Min Value Test");
        deserialized.OptionalValue.Should().Be(decimal.MinValue);
        deserialized.Zero.Should().Be(decimal.MinValue);
        deserialized.Large.Should().Be(decimal.MinValue);
        deserialized.Small.Should().Be(decimal.MinValue);
        deserialized.Negative.Should().Be(decimal.MinValue);
    }

    [Fact]
    public void Serialize_Then_Deserialize_DecimalModel_WithMinusOne_RoundTripSucceeds()
    {
        // Arrange
        var original = new DecimalModel 
        { 
            Value = decimal.MinusOne,
            Name = "Minus One Test", 
            OptionalValue = decimal.MinusOne,
            Zero = decimal.MinusOne,
            Large = decimal.MinusOne,
            Small = decimal.MinusOne,
            Negative = decimal.MinusOne
        };

        // Act
        var serialized = NCborSerializer.Serialize(original, _context.DecimalModel);
        var deserialized = NCborSerializer.Deserialize(serialized, _context.DecimalModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Value.Should().Be(decimal.MinusOne);
        deserialized.Name.Should().Be("Minus One Test");
        deserialized.OptionalValue.Should().Be(decimal.MinusOne);
        deserialized.Zero.Should().Be(decimal.MinusOne);
        deserialized.Large.Should().Be(decimal.MinusOne);
        deserialized.Small.Should().Be(decimal.MinusOne);
        deserialized.Negative.Should().Be(decimal.MinusOne);
    }

    [Fact]
    public void Serialize_Then_Deserialize_DecimalModel_WithOne_RoundTripSucceeds()
    {
        // Arrange
        var original = new DecimalModel 
        { 
            Value = decimal.One,
            Name = "One Test", 
            OptionalValue = decimal.One,
            Zero = decimal.One,
            Large = decimal.One,
            Small = decimal.One,
            Negative = decimal.One
        };

        // Act
        var serialized = NCborSerializer.Serialize(original, _context.DecimalModel);
        var deserialized = NCborSerializer.Deserialize(serialized, _context.DecimalModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Value.Should().Be(decimal.One);
        deserialized.Name.Should().Be("One Test");
        deserialized.OptionalValue.Should().Be(decimal.One);
        deserialized.Zero.Should().Be(decimal.One);
        deserialized.Large.Should().Be(decimal.One);
        deserialized.Small.Should().Be(decimal.One);
        deserialized.Negative.Should().Be(decimal.One);
    }

    #endregion

    #region Precision Tests

    [Fact]
    public void Serialize_Then_Deserialize_DecimalModel_WithHighPrecision_PreservesPrecision()
    {
        // Arrange
        var original = new DecimalModel 
        { 
            Value = 1.2345678901234567890123456789m,
            Name = "High Precision Test", 
            OptionalValue = 0.0000000000000000000000000001m,
            Zero = 0m,
            Large = 123456789012345678901234567.89m,
            Small = 0.1234567890123456789012345678m,
            Negative = -9.8765432109876543210987654321m
        };

        // Act
        var serialized = NCborSerializer.Serialize(original, _context.DecimalModel);
        var deserialized = NCborSerializer.Deserialize(serialized, _context.DecimalModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Value.Should().Be(1.2345678901234567890123456789m);
        deserialized.Name.Should().Be("High Precision Test");
        deserialized.OptionalValue.Should().Be(0.0000000000000000000000000001m);
        deserialized.Zero.Should().Be(0m);
        deserialized.Large.Should().Be(123456789012345678901234567.89m);
        deserialized.Small.Should().Be(0.1234567890123456789012345678m);
        deserialized.Negative.Should().Be(-9.8765432109876543210987654321m);
    }

    [Fact]
    public void Serialize_Then_Deserialize_DecimalModel_WithTrailingZeros_PreservesValue()
    {
        // Arrange
        var original = new DecimalModel 
        { 
            Value = 1.2300m,    // Trailing zeros
            Name = "Trailing Zeros Test", 
            OptionalValue = 100.0000m,  // More trailing zeros
            Zero = 0.0000m,
            Large = 1000.000m,
            Small = 0.1000m,
            Negative = -50.00m
        };

        // Act
        var serialized = NCborSerializer.Serialize(original, _context.DecimalModel);
        var deserialized = NCborSerializer.Deserialize(serialized, _context.DecimalModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Value.Should().Be(1.23m);  // .NET decimal normalizes trailing zeros
        deserialized.Name.Should().Be("Trailing Zeros Test");
        deserialized.OptionalValue.Should().Be(100m);
        deserialized.Zero.Should().Be(0m);
        deserialized.Large.Should().Be(1000m);
        deserialized.Small.Should().Be(0.1m);
        deserialized.Negative.Should().Be(-50m);
    }

    #endregion

    #region Financial/Currency Scenarios

    [Fact]
    public void Serialize_Then_Deserialize_DecimalModel_WithCurrencyValues_RoundTripSucceeds()
    {
        // Arrange - Common currency scenarios
        var original = new DecimalModel 
        { 
            Value = 1234.56m,       // USD amount
            Name = "Currency Test", 
            OptionalValue = 999.99m, // Price
            Zero = 0.00m,           // Zero balance
            Large = 1000000.00m,    // Million dollars
            Small = 0.01m,          // One cent
            Negative = -50.25m      // Negative balance
        };

        // Act
        var serialized = NCborSerializer.Serialize(original, _context.DecimalModel);
        var deserialized = NCborSerializer.Deserialize(serialized, _context.DecimalModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Value.Should().Be(1234.56m);
        deserialized.Name.Should().Be("Currency Test");
        deserialized.OptionalValue.Should().Be(999.99m);
        deserialized.Zero.Should().Be(0.00m);
        deserialized.Large.Should().Be(1000000.00m);
        deserialized.Small.Should().Be(0.01m);
        deserialized.Negative.Should().Be(-50.25m);
    }

    #endregion

    #region Null Handling Tests

    [Fact]
    public void Serialize_DecimalModel_WithNullOptionalValue_SerializesCorrectly()
    {
        // Arrange
        var model = new DecimalModel 
        { 
            Value = 42.0m,
            Name = "Null Optional Test", 
            OptionalValue = null,   // Explicitly null
            Zero = 0m,
            Large = 100m,
            Small = 0.1m,
            Negative = -10m
        };

        // Act
        var serialized = NCborSerializer.Serialize(model, _context.DecimalModel);

        // Assert
        serialized.Should().NotBeNull();
        serialized.Should().NotBeEmpty();
    }

    [Fact]
    public void Serialize_Then_Deserialize_DecimalModel_WithNullOptionalValue_RoundTripSucceeds()
    {
        // Arrange
        var original = new DecimalModel 
        { 
            Value = 42.0m,
            Name = "Null Optional Test", 
            OptionalValue = null,   // Explicitly null
            Zero = 0m,
            Large = 100m,
            Small = 0.1m,
            Negative = -10m
        };

        // Act
        var serialized = NCborSerializer.Serialize(original, _context.DecimalModel);
        var deserialized = NCborSerializer.Deserialize(serialized, _context.DecimalModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Value.Should().Be(42.0m);
        deserialized.Name.Should().Be("Null Optional Test");
        deserialized.OptionalValue.Should().BeNull();
        deserialized.Zero.Should().Be(0m);
        deserialized.Large.Should().Be(100m);
        deserialized.Small.Should().Be(0.1m);
        deserialized.Negative.Should().Be(-10m);
    }

    #endregion
}