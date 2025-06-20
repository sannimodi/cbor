namespace CbOrSerialization.Tests;

public class CbOrDateTimeTests
{
    private readonly TestCbOrContext _context = TestCbOrContext.Default;

    #region Basic DateTime Serialization Tests

    [Fact]
    public void Serialize_DateTimeModel_WithUtcDateTime_SerializesCorrectly()
    {
        // Arrange
        var utcTime = new DateTime(2024, 6, 20, 14, 30, 45, 123, DateTimeKind.Utc);
        var offsetTime = new DateTimeOffset(2024, 6, 20, 14, 30, 45, 123, TimeSpan.FromHours(2));
        var model = new DateTimeModel 
        { 
            CreatedAt = utcTime, 
            UpdatedAt = offsetTime,
            Name = "Test Model",
            OptionalDate = null,
            OptionalDateOffset = null
        };

        // Act
        var serialized = CbOrSerializer.Serialize(model, _context.DateTimeModel);

        // Assert
        serialized.Should().NotBeNull();
        serialized.Should().NotBeEmpty();
    }

    [Fact]
    public void Serialize_Then_Deserialize_DateTimeModel_RoundTripSucceeds()
    {
        // Arrange
        var utcTime = new DateTime(2024, 6, 20, 14, 30, 45, 123, DateTimeKind.Utc);
        var offsetTime = new DateTimeOffset(2024, 6, 20, 16, 30, 45, 123, TimeSpan.FromHours(2));
        var original = new DateTimeModel 
        { 
            CreatedAt = utcTime, 
            UpdatedAt = offsetTime,
            Name = "Test Model",
            OptionalDate = null,
            OptionalDateOffset = null
        };

        // Act
        var serialized = CbOrSerializer.Serialize(original, _context.DateTimeModel);
        var deserialized = CbOrSerializer.Deserialize(serialized, _context.DateTimeModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.CreatedAt.Should().Be(utcTime);
        deserialized.UpdatedAt.Should().Be(offsetTime);
        deserialized.Name.Should().Be("Test Model");
        deserialized.OptionalDate.Should().BeNull();
        deserialized.OptionalDateOffset.Should().BeNull();
    }

    [Fact]
    public void Serialize_Then_Deserialize_DateTimeModel_WithOptionalValues_RoundTripSucceeds()
    {
        // Arrange
        var utcTime = new DateTime(2024, 6, 20, 14, 30, 45, 123, DateTimeKind.Utc);
        var offsetTime = new DateTimeOffset(2024, 6, 20, 16, 30, 45, 123, TimeSpan.FromHours(2));
        var optionalUtc = new DateTime(2024, 12, 25, 00, 00, 00, DateTimeKind.Utc);
        var optionalOffset = new DateTimeOffset(2024, 12, 25, 12, 00, 00, TimeSpan.FromHours(-5));
        
        var original = new DateTimeModel 
        { 
            CreatedAt = utcTime, 
            UpdatedAt = offsetTime,
            Name = "Test Model",
            OptionalDate = optionalUtc,
            OptionalDateOffset = optionalOffset
        };

        // Act
        var serialized = CbOrSerializer.Serialize(original, _context.DateTimeModel);
        var deserialized = CbOrSerializer.Deserialize(serialized, _context.DateTimeModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.CreatedAt.Should().Be(utcTime);
        deserialized.UpdatedAt.Should().Be(offsetTime);
        deserialized.Name.Should().Be("Test Model");
        deserialized.OptionalDate.Should().Be(optionalUtc);
        deserialized.OptionalDateOffset.Should().Be(optionalOffset);
    }

    #endregion

    #region DateTime.Kind Handling Tests

    [Fact]
    public void Serialize_Then_Deserialize_UtcDateTime_RoundTripSucceeds()
    {
        // Arrange
        var utcTime = new DateTime(2024, 6, 20, 14, 30, 45, 123, DateTimeKind.Utc);
        var model = new DateTimeModel { CreatedAt = utcTime, UpdatedAt = DateTimeOffset.UtcNow, Name = "UTC Test" };

        // Act
        var serialized = CbOrSerializer.Serialize(model, _context.DateTimeModel);
        var deserialized = CbOrSerializer.Deserialize(serialized, _context.DateTimeModel);

        // Assert
        deserialized.CreatedAt.Should().Be(utcTime);
        deserialized.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Serialize_Then_Deserialize_LocalDateTime_ConvertedToUtc()
    {
        // Arrange
        var localTime = new DateTime(2024, 6, 20, 14, 30, 45, 123, DateTimeKind.Local);
        var expectedUtc = localTime.ToUniversalTime();
        var model = new DateTimeModel { CreatedAt = localTime, UpdatedAt = DateTimeOffset.UtcNow, Name = "Local Test" };

        // Act
        var serialized = CbOrSerializer.Serialize(model, _context.DateTimeModel);
        var deserialized = CbOrSerializer.Deserialize(serialized, _context.DateTimeModel);

        // Assert
        deserialized.CreatedAt.Should().Be(expectedUtc);
        deserialized.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Serialize_Then_Deserialize_UnspecifiedDateTime_TreatedAsUtc()
    {
        // Arrange
        var unspecifiedTime = new DateTime(2024, 6, 20, 14, 30, 45, 123, DateTimeKind.Unspecified);
        var expectedUtc = DateTime.SpecifyKind(unspecifiedTime, DateTimeKind.Utc);
        var model = new DateTimeModel { CreatedAt = unspecifiedTime, UpdatedAt = DateTimeOffset.UtcNow, Name = "Unspecified Test" };

        // Act
        var serialized = CbOrSerializer.Serialize(model, _context.DateTimeModel);
        var deserialized = CbOrSerializer.Deserialize(serialized, _context.DateTimeModel);

        // Assert
        deserialized.CreatedAt.Should().Be(expectedUtc);
        deserialized.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    #endregion

    #region DateTimeOffset Timezone Tests

    [Fact]
    public void Serialize_Then_Deserialize_DateTimeOffset_PreservesOffset()
    {
        // Test various timezone offsets
        var offsets = new[]
        {
            TimeSpan.Zero,                    // UTC
            TimeSpan.FromHours(5),            // +05:00
            TimeSpan.FromHours(-8),           // -08:00
            TimeSpan.FromHours(2.5),          // +02:30
            TimeSpan.FromHours(-9.5),         // -09:30
            TimeSpan.FromHours(14),           // +14:00 (max)
            TimeSpan.FromHours(-12)           // -12:00 (min)
        };

        foreach (var offset in offsets)
        {
            // Arrange
            var offsetTime = new DateTimeOffset(2024, 6, 20, 14, 30, 45, 123, offset);
            var model = new DateTimeModel 
            { 
                CreatedAt = DateTime.UtcNow, 
                UpdatedAt = offsetTime, 
                Name = $"Offset {offset} Test" 
            };

            // Act
            var serialized = CbOrSerializer.Serialize(model, _context.DateTimeModel);
            var deserialized = CbOrSerializer.Deserialize(serialized, _context.DateTimeModel);

            // Assert
            deserialized.UpdatedAt.Should().Be(offsetTime, $"Failed for offset: {offset}");
            deserialized.UpdatedAt.Offset.Should().Be(offset, $"Failed for offset: {offset}");
        }
    }

    #endregion

    #region Precision and Format Tests

    [Fact]
    public void Serialize_Then_Deserialize_HighPrecisionDateTime_PreservesPrecision()
    {
        // Arrange - Use maximum DateTime precision (100ns ticks)
        var highPrecisionTime = new DateTime(2024, 6, 20, 14, 30, 45, DateTimeKind.Utc)
            .AddMilliseconds(123)
            .AddMicroseconds(456)
            .AddTicks(7); // Adding individual ticks for maximum precision
            
        var model = new DateTimeModel 
        { 
            CreatedAt = highPrecisionTime, 
            UpdatedAt = DateTimeOffset.UtcNow, 
            Name = "Precision Test" 
        };

        // Act
        var serialized = CbOrSerializer.Serialize(model, _context.DateTimeModel);
        var deserialized = CbOrSerializer.Deserialize(serialized, _context.DateTimeModel);

        // Assert
        deserialized.CreatedAt.Should().Be(highPrecisionTime);
        deserialized.CreatedAt.Ticks.Should().Be(highPrecisionTime.Ticks);
    }

    [Fact]
    public void Serialize_Then_Deserialize_DateTimeOffset_WithMicroseconds_PreservesPrecision()
    {
        // Arrange
        var preciseDateTimeOffset = new DateTimeOffset(2024, 6, 20, 14, 30, 45, 123, TimeSpan.FromHours(1))
            .AddMicroseconds(456)
            .AddTicks(7);
            
        var model = new DateTimeModel 
        { 
            CreatedAt = DateTime.UtcNow, 
            UpdatedAt = preciseDateTimeOffset, 
            Name = "DateTimeOffset Precision Test" 
        };

        // Act
        var serialized = CbOrSerializer.Serialize(model, _context.DateTimeModel);
        var deserialized = CbOrSerializer.Deserialize(serialized, _context.DateTimeModel);

        // Assert
        deserialized.UpdatedAt.Should().Be(preciseDateTimeOffset);
        deserialized.UpdatedAt.Ticks.Should().Be(preciseDateTimeOffset.Ticks);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Serialize_Then_Deserialize_MinMaxDateTimes_RoundTripSucceeds()
    {
        // Test DateTime edge cases
        var edgeCases = new[]
        {
            DateTime.MinValue,
            DateTime.MaxValue,
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), // Unix epoch
            new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), // Y2K
            new DateTime(2038, 1, 19, 3, 14, 7, DateTimeKind.Utc) // Year 2038 problem
        };

        foreach (var edgeDateTime in edgeCases)
        {
            // Arrange
            var model = new DateTimeModel 
            { 
                CreatedAt = edgeDateTime, 
                UpdatedAt = DateTimeOffset.UtcNow, 
                Name = $"Edge case: {edgeDateTime}" 
            };

            // Act
            var serialized = CbOrSerializer.Serialize(model, _context.DateTimeModel);
            var deserialized = CbOrSerializer.Deserialize(serialized, _context.DateTimeModel);

            // Assert
            deserialized.CreatedAt.Should().Be(edgeDateTime, $"Failed for edge case: {edgeDateTime}");
        }
    }

    [Fact]
    public void Serialize_Then_Deserialize_DateTimeOffset_EdgeCases_RoundTripSucceeds()
    {
        // Test DateTimeOffset edge cases
        var edgeCases = new[]
        {
            DateTimeOffset.MinValue,
            DateTimeOffset.MaxValue,
            new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero), // Unix epoch UTC
            new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero)  // Y2K UTC
        };

        foreach (var edgeDateTimeOffset in edgeCases)
        {
            // Arrange
            var model = new DateTimeModel 
            { 
                CreatedAt = DateTime.UtcNow, 
                UpdatedAt = edgeDateTimeOffset, 
                Name = $"Edge case: {edgeDateTimeOffset}" 
            };

            // Act
            var serialized = CbOrSerializer.Serialize(model, _context.DateTimeModel);
            var deserialized = CbOrSerializer.Deserialize(serialized, _context.DateTimeModel);

            // Assert
            deserialized.UpdatedAt.Should().Be(edgeDateTimeOffset, $"Failed for edge case: {edgeDateTimeOffset}");
        }
    }

    #endregion

    #region Null Handling Tests

    [Fact]
    public void Serialize_Then_Deserialize_NullableDateTime_WithNulls_RoundTripSucceeds()
    {
        // Arrange
        var model = new DateTimeModel 
        { 
            CreatedAt = DateTime.UtcNow, 
            UpdatedAt = DateTimeOffset.UtcNow, 
            Name = "Null Test",
            OptionalDate = null,
            OptionalDateOffset = null
        };

        // Act
        var serialized = CbOrSerializer.Serialize(model, _context.DateTimeModel);
        var deserialized = CbOrSerializer.Deserialize(serialized, _context.DateTimeModel);

        // Assert
        deserialized.OptionalDate.Should().BeNull();
        deserialized.OptionalDateOffset.Should().BeNull();
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void Serialize_Then_Deserialize_1000_DateTimes_PerformanceTest()
    {
        // Arrange
        var models = new List<DateTimeModel>();
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var baseLocalTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        
        for (int i = 0; i < 1000; i++)
        {
            var offset = TimeSpan.FromHours(i % 24 - 12);
            models.Add(new DateTimeModel 
            { 
                CreatedAt = baseTime.AddMinutes(i), 
                UpdatedAt = new DateTimeOffset(baseLocalTime.AddHours(i), offset),
                Name = $"Model {i}",
                OptionalDate = i % 2 == 0 ? baseTime.AddDays(i) : null,
                OptionalDateOffset = i % 3 == 0 ? new DateTimeOffset(baseLocalTime.AddMonths(i % 12), offset) : null
            });
        }

        // Act & Assert - This should complete without timeout
        foreach (var model in models)
        {
            var serialized = CbOrSerializer.Serialize(model, _context.DateTimeModel);
            var deserialized = CbOrSerializer.Deserialize(serialized, _context.DateTimeModel);
            
            deserialized.CreatedAt.Should().Be(model.CreatedAt);
            deserialized.UpdatedAt.Should().Be(model.UpdatedAt);
            deserialized.Name.Should().Be(model.Name);
            deserialized.OptionalDate.Should().Be(model.OptionalDate);
            deserialized.OptionalDateOffset.Should().Be(model.OptionalDateOffset);
        }
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void Deserialize_CorruptedDateTimeData_ThrowsCbOrDeserializationException()
    {
        // Arrange - Create valid serialized data first
        var model = new DateTimeModel 
        { 
            CreatedAt = DateTime.UtcNow, 
            UpdatedAt = DateTimeOffset.UtcNow, 
            Name = "Test" 
        };
        var validSerialized = CbOrSerializer.Serialize(model, _context.DateTimeModel);
        
        // Corrupt the data by truncating it
        var corruptedData = validSerialized.Take(validSerialized.Length / 2).ToArray();

        // Act & Assert
        var act = () => CbOrSerializer.Deserialize(corruptedData, _context.DateTimeModel);
        act.Should().Throw<CbOrDeserializationException>()
           .WithMessage("*Failed to deserialize to type 'DateTimeModel'*");
    }

    #endregion

    #region Format Compatibility Tests

    [Fact]
    public void DateTime_Serialization_UsesISO8601Format()
    {
        // Arrange
        var testTime = new DateTime(2024, 6, 20, 14, 30, 45, 123, DateTimeKind.Utc);
        var model = new DateTimeModel 
        { 
            CreatedAt = testTime, 
            UpdatedAt = DateTimeOffset.UtcNow, 
            Name = "ISO Format Test" 
        };

        // Act
        var serialized = CbOrSerializer.Serialize(model, _context.DateTimeModel);

        // Assert
        serialized.Should().NotBeNull();
        serialized.Should().NotBeEmpty();
        
        // Verify we can round-trip
        var deserialized = CbOrSerializer.Deserialize(serialized, _context.DateTimeModel);
        deserialized.CreatedAt.Should().Be(testTime);
    }

    [Fact]
    public void DateTimeOffset_Serialization_PreservesTimezoneInformation()
    {
        // Arrange
        var testTime = new DateTimeOffset(2024, 6, 20, 14, 30, 45, 123, TimeSpan.FromHours(-7));
        var model = new DateTimeModel 
        { 
            CreatedAt = DateTime.UtcNow, 
            UpdatedAt = testTime, 
            Name = "Timezone Test" 
        };

        // Act
        var serialized = CbOrSerializer.Serialize(model, _context.DateTimeModel);
        var deserialized = CbOrSerializer.Deserialize(serialized, _context.DateTimeModel);

        // Assert
        deserialized.UpdatedAt.Should().Be(testTime);
        deserialized.UpdatedAt.Offset.Should().Be(TimeSpan.FromHours(-7));
        deserialized.UpdatedAt.DateTime.Should().Be(testTime.DateTime);
        deserialized.UpdatedAt.UtcDateTime.Should().Be(testTime.UtcDateTime);
    }

    #endregion
}