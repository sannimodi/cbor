namespace NCbor.Tests;

/// <summary>
/// A payload must contain exactly one top-level CBOR value (same contract as System.Text.Json).
/// Previously trailing bytes were silently ignored, so a Stream holding two concatenated documents
/// lost the second one with no error.
/// </summary>
public class NCborTrailingDataTests
{
    private readonly TestNCborContext _context = TestNCborContext.Default;

    private byte[] TwoDocuments()
    {
        var one = NCborSerializer.Serialize(new SimpleModel { Name = "a", Age = 1 }, _context.SimpleModel);
        var two = NCborSerializer.Serialize(new SimpleModel { Name = "b", Age = 2 }, _context.SimpleModel);
        return one.Concat(two).ToArray();
    }

    [Fact]
    public void ByteArray_WithTrailingDocument_Throws()
    {
        var act = () => NCborSerializer.Deserialize(TwoDocuments(), _context.SimpleModel);
        act.Should().Throw<NCborValidationException>().WithMessage("*trailing*");
    }

    [Fact]
    public void ByteArray_WithTrailingGarbageByte_Throws()
    {
        var bytes = NCborSerializer.Serialize(new SimpleModel { Name = "a" }, _context.SimpleModel).Append((byte)0x00).ToArray();
        var act = () => NCborSerializer.Deserialize(bytes, _context.SimpleModel);
        act.Should().Throw<NCborValidationException>().WithMessage("*1 unexpected trailing byte*");
    }

    [Fact]
    public void Memory_WithTrailingDocument_Throws()
    {
        var act = () => NCborSerializer.Deserialize(new ReadOnlyMemory<byte>(TwoDocuments()), _context.SimpleModel);
        act.Should().Throw<NCborValidationException>().WithMessage("*trailing*");
    }

    [Fact]
    public void Stream_WithTrailingDocument_Throws()
    {
        var act = () => NCborSerializer.Deserialize(new MemoryStream(TwoDocuments()), _context.SimpleModel);
        act.Should().Throw<NCborValidationException>().WithMessage("*trailing*");
    }

    [Fact]
    public async Task StreamAsync_WithTrailingDocument_Throws()
    {
        var act = () => NCborSerializer.DeserializeAsync(new MemoryStream(TwoDocuments()), _context.SimpleModel).AsTask();
        await act.Should().ThrowAsync<NCborValidationException>().WithMessage("*trailing*");
    }

    [Fact]
    public void ExactSingleDocument_StillSucceeds()
    {
        var bytes = NCborSerializer.Serialize(new SimpleModel { Name = "a", Age = 5 }, _context.SimpleModel);
        NCborSerializer.Deserialize(bytes, _context.SimpleModel).Age.Should().Be(5);
    }
}

public class NCborMalformedPayloadTests
{
    [Fact]
    public void DuplicateMapKey_IsReportedAsNCborException_NotRawArgumentException()
    {
        // {"a": 1, "a": 2} — hand-encoded, since CborWriter refuses to produce it.
        var bytes = new byte[] { 0xA2, 0x61, 0x61, 0x01, 0x61, 0x61, 0x02 };

        var ex = Record.Exception(() => NCborSerializer.Deserialize(bytes, TestNCborContext.Default.DictionaryOfStringAndInt32));

        ex.Should().NotBeNull();
        ex.Should().Match(e => e is NCborDeserializationException || e is NCborValidationException);
    }

    [Fact]
    public void UnknownEnumName_ThrowsNCborDeserializationException_NotRawArgumentException()
    {
        var writer = new System.Formats.Cbor.CborWriter();
        writer.WriteStartMap(1);
        writer.WriteTextString("Role");
        writer.WriteTextString("NoSuchRole");
        writer.WriteEndMap();

        var act = () => NCborSerializer.Deserialize(writer.Encode(), TestNCborContext.Default.EnumAsStringModel);
        act.Should().Throw<NCborDeserializationException>().WithInnerException<ArgumentException>();
    }
}
