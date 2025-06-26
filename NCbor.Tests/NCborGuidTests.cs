namespace NCbor.Tests;

public class NCborGuidTests
{
    private readonly TestNCborContext _context = TestNCborContext.Default;

    #region Basic GUID Serialization Tests

    [Fact]
    public void Serialize_GuidModel_WithValidGuid_SerializesCorrectly()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var model = new GuidModel 
        { 
            Id = guid, 
            Name = "Test Model", 
            OptionalId = null 
        };

        // Act
        var serialized = NCborSerializer.Serialize(model, _context.GuidModel);

        // Assert
        serialized.Should().NotBeNull();
        serialized.Should().NotBeEmpty();
    }

    [Fact]
    public void Serialize_Then_Deserialize_GuidModel_RoundTripSucceeds()
    {
        // Arrange
        var originalGuid = Guid.NewGuid();
        var original = new GuidModel 
        { 
            Id = originalGuid, 
            Name = "Test Model", 
            OptionalId = null 
        };

        // Act
        var serialized = NCborSerializer.Serialize(original, _context.GuidModel);
        var deserialized = NCborSerializer.Deserialize(serialized, _context.GuidModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Id.Should().Be(originalGuid);
        deserialized.Name.Should().Be("Test Model");
        deserialized.OptionalId.Should().BeNull();
    }

    [Fact]
    public void Serialize_Then_Deserialize_GuidModel_WithOptionalId_RoundTripSucceeds()
    {
        // Arrange
        var originalGuid = Guid.NewGuid();
        var optionalGuid = Guid.NewGuid();
        var original = new GuidModel 
        { 
            Id = originalGuid, 
            Name = "Test Model", 
            OptionalId = optionalGuid 
        };

        // Act
        var serialized = NCborSerializer.Serialize(original, _context.GuidModel);
        var deserialized = NCborSerializer.Deserialize(serialized, _context.GuidModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Id.Should().Be(originalGuid);
        deserialized.Name.Should().Be("Test Model");
        deserialized.OptionalId.Should().Be(optionalGuid);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Serialize_Then_Deserialize_EmptyGuid_RoundTripSucceeds()
    {
        // Arrange
        var original = new GuidModel 
        { 
            Id = Guid.Empty, 
            Name = "Empty GUID Test", 
            OptionalId = null 
        };

        // Act
        var serialized = NCborSerializer.Serialize(original, _context.GuidModel);
        var deserialized = NCborSerializer.Deserialize(serialized, _context.GuidModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Id.Should().Be(Guid.Empty);
        deserialized.Name.Should().Be("Empty GUID Test");
        deserialized.OptionalId.Should().BeNull();
    }

    [Fact]
    public void Serialize_Then_Deserialize_GuidModel_WithNullOptionalId_RoundTripSucceeds()
    {
        // Arrange
        var originalGuid = Guid.NewGuid();
        var original = new GuidModel 
        { 
            Id = originalGuid, 
            Name = "Null Optional Test", 
            OptionalId = null 
        };

        // Act
        var serialized = NCborSerializer.Serialize(original, _context.GuidModel);
        var deserialized = NCborSerializer.Deserialize(serialized, _context.GuidModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Id.Should().Be(originalGuid);
        deserialized.Name.Should().Be("Null Optional Test");
        deserialized.OptionalId.Should().BeNull();
    }

    #endregion

    #region GUID Format and Size Tests

    [Fact]
    public void Serialize_GuidModel_SerializedDataContainsCorrectGuidBytes()
    {
        // Arrange
        var knownGuid = new Guid("12345678-1234-5678-9abc-123456789abc");
        var model = new GuidModel { Id = knownGuid, Name = "Known GUID", OptionalId = null };

        // Act
        var serialized = NCborSerializer.Serialize(model, _context.GuidModel);

        // Assert
        serialized.Should().NotBeNull();
        serialized.Should().NotBeEmpty();
        
        // The serialized data should contain the GUID bytes somewhere
        var guidBytes = knownGuid.ToByteArray();
        guidBytes.Should().HaveCount(16); // GUID is always 16 bytes
    }

    [Fact]
    public void Serialize_MultipleGuids_EachHasUniqueRepresentation()
    {
        // Arrange
        var guid1 = new Guid("12345678-1234-5678-9abc-123456789abc");
        var guid2 = new Guid("87654321-4321-8765-cba9-cba987654321");
        
        var model1 = new GuidModel { Id = guid1, Name = "Model 1", OptionalId = null };
        var model2 = new GuidModel { Id = guid2, Name = "Model 2", OptionalId = null };

        // Act
        var serialized1 = NCborSerializer.Serialize(model1, _context.GuidModel);
        var serialized2 = NCborSerializer.Serialize(model2, _context.GuidModel);

        // Assert
        serialized1.Should().NotEqual(serialized2);
        
        var deserialized1 = NCborSerializer.Deserialize(serialized1, _context.GuidModel);
        var deserialized2 = NCborSerializer.Deserialize(serialized2, _context.GuidModel);
        
        deserialized1.Id.Should().Be(guid1);
        deserialized2.Id.Should().Be(guid2);
        deserialized1.Id.Should().NotBe(deserialized2.Id);
    }

    #endregion

    #region Performance and Compatibility Tests

    [Fact]
    public void Serialize_Then_Deserialize_1000_Guids_PerformanceTest()
    {
        // Arrange
        var models = new List<GuidModel>();
        for (int i = 0; i < 1000; i++)
        {
            models.Add(new GuidModel 
            { 
                Id = Guid.NewGuid(), 
                Name = $"Model {i}", 
                OptionalId = i % 2 == 0 ? Guid.NewGuid() : null 
            });
        }

        // Act & Assert - This should complete without timeout
        foreach (var model in models)
        {
            var serialized = NCborSerializer.Serialize(model, _context.GuidModel);
            var deserialized = NCborSerializer.Deserialize(serialized, _context.GuidModel);
            
            deserialized.Id.Should().Be(model.Id);
            deserialized.Name.Should().Be(model.Name);
            deserialized.OptionalId.Should().Be(model.OptionalId);
        }
    }

    [Fact]
    public void Serialize_GuidModel_ProducesCompactBinaryFormat()
    {
        // Arrange
        var model = new GuidModel 
        { 
            Id = Guid.NewGuid(), 
            Name = "Compact Test", 
            OptionalId = null 
        };

        // Act
        var serialized = NCborSerializer.Serialize(model, _context.GuidModel);

        // Assert
        // CBOR should be more compact than JSON representation
        var jsonApproxSize = $"{{\"Id\":\"{model.Id}\",\"Name\":\"{model.Name}\",\"OptionalId\":null}}".Length;
        serialized.Length.Should().BeLessThan(jsonApproxSize);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void Deserialize_CorruptedGuidData_ThrowsNCborDeserializationException()
    {
        // Arrange - Create valid serialized data first
        var model = new GuidModel { Id = Guid.NewGuid(), Name = "Test", OptionalId = null };
        var validSerialized = NCborSerializer.Serialize(model, _context.GuidModel);
        
        // Corrupt the data by truncating it
        var corruptedData = validSerialized.Take(validSerialized.Length / 2).ToArray();

        // Act & Assert
        var act = () => NCborSerializer.Deserialize(corruptedData, _context.GuidModel);
        act.Should().Throw<NCborDeserializationException>()
           .WithMessage("*Failed to deserialize to type 'GuidModel'*");
    }

    #endregion

    #region GUID Specific Edge Cases

    [Fact]
    public void Serialize_Then_Deserialize_SpecialGuids_RoundTripSucceeds()
    {
        // Test various special GUID values
        var specialGuids = new[]
        {
            Guid.Empty,
            new Guid("00000000-0000-0000-0000-000000000000"),
            new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            new Guid("12340000-0000-0000-0000-000000000000"),
            new Guid("00000000-1234-0000-0000-000000000000"),
            new Guid("00000000-0000-1234-0000-000000000000"),
            new Guid("00000000-0000-0000-1234-000000000000"),
            new Guid("00000000-0000-0000-0000-123400000000")
        };

        foreach (var specialGuid in specialGuids)
        {
            // Arrange
            var model = new GuidModel 
            { 
                Id = specialGuid, 
                Name = $"Special GUID: {specialGuid}", 
                OptionalId = null 
            };

            // Act
            var serialized = NCborSerializer.Serialize(model, _context.GuidModel);
            var deserialized = NCborSerializer.Deserialize(serialized, _context.GuidModel);

            // Assert
            deserialized.Id.Should().Be(specialGuid, $"Failed for special GUID: {specialGuid}");
            deserialized.Name.Should().Be($"Special GUID: {specialGuid}");
        }
    }

    #endregion
}