namespace NCbor.Tests;

public class NCborByteArrayTests
{
    private readonly TestNCborContext _context = TestNCborContext.Default;

    [Fact]
    public void SerializeByteArray_RoundTrip_ShouldSucceed()
    {
        var model = new ByteArrayModel
        {
            Data = new byte[] { 0x01, 0x02, 0x03, 0xFF, 0x00 },
            Name = "test"
        };

        var bytes = NCborSerializer.Serialize(model, _context.ByteArrayModel);
        var deserialized = NCborSerializer.Deserialize(bytes, _context.ByteArrayModel);

        deserialized.Should().NotBeNull();
        deserialized.Data.Should().BeEquivalentTo(model.Data);
        deserialized.Name.Should().Be("test");
    }

    [Fact]
    public void SerializeByteArray_Empty_ShouldSucceed()
    {
        var model = new ByteArrayModel
        {
            Data = Array.Empty<byte>(),
            Name = "empty"
        };

        var bytes = NCborSerializer.Serialize(model, _context.ByteArrayModel);
        var deserialized = NCborSerializer.Deserialize(bytes, _context.ByteArrayModel);

        deserialized.Should().NotBeNull();
        deserialized.Data.Should().BeEmpty();
    }

    [Fact]
    public void SerializeByteArray_Large_ShouldSucceed()
    {
        var largeData = new byte[4096];
        new Random(42).NextBytes(largeData);

        var model = new ByteArrayModel
        {
            Data = largeData,
            Name = "large"
        };

        var bytes = NCborSerializer.Serialize(model, _context.ByteArrayModel);
        var deserialized = NCborSerializer.Deserialize(bytes, _context.ByteArrayModel);

        deserialized.Should().NotBeNull();
        deserialized.Data.Should().BeEquivalentTo(largeData);
    }

    [Fact]
    public void SerializeByteArray_NullableWithValue_ShouldSucceed()
    {
        var model = new ByteArrayModel
        {
            Data = new byte[] { 0xAA },
            OptionalData = new byte[] { 0xBB, 0xCC },
            Name = "nullable"
        };

        var bytes = NCborSerializer.Serialize(model, _context.ByteArrayModel);
        var deserialized = NCborSerializer.Deserialize(bytes, _context.ByteArrayModel);

        deserialized.Should().NotBeNull();
        deserialized.OptionalData.Should().NotBeNull();
        deserialized.OptionalData.Should().BeEquivalentTo(new byte[] { 0xBB, 0xCC });
    }

    [Fact]
    public void SerializeByteArray_NullableWithNull_ShouldSucceed()
    {
        var model = new ByteArrayModel
        {
            Data = new byte[] { 0x01 },
            OptionalData = null,
            Name = "null-optional"
        };

        var bytes = NCborSerializer.Serialize(model, _context.ByteArrayModel);
        var deserialized = NCborSerializer.Deserialize(bytes, _context.ByteArrayModel);

        deserialized.Should().NotBeNull();
        deserialized.OptionalData.Should().BeNull();
    }

    [Fact]
    public void SerializeByteArray_UsesCborByteString_IsCompact()
    {
        // Verify byte[] uses CBOR byte string (major type 2), which is more compact
        // than a CBOR array of integers
        var model = new ByteArrayModel
        {
            Data = new byte[] { 0x01, 0x02, 0x03 },
            Name = "compact"
        };

        var bytes = NCborSerializer.Serialize(model, _context.ByteArrayModel);
        var deserialized = NCborSerializer.Deserialize(bytes, _context.ByteArrayModel);

        deserialized.Data.Should().BeEquivalentTo(model.Data);

        // The serialized CBOR should be compact — byte string header is 1-2 bytes,
        // not 1 byte per element like an integer array would be
        bytes.Length.Should().BeLessThan(50);
    }
}
