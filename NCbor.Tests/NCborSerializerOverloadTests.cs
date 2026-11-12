using System.Buffers;

namespace NCbor.Tests;

/// <summary>
/// Covers the Stream / IBufferWriter / ReadOnlyMemory overloads of <see cref="NCborSerializer"/>:
/// they must produce byte-for-byte the same CBOR as the byte[] overload, round-trip, and map errors
/// exactly like the byte[] overload does.
/// </summary>
public class NCborSerializerOverloadTests
{
    private readonly TestNCborContext _context = TestNCborContext.Default;

    private static SimpleModel CreateSimple() => new() { Name = "Ada", Age = 36, IsActive = true };

    private static ByteArrayModel CreateLargeByteArrayModel()
    {
        // > 4096 bytes so the chunked indefinite-length byte string path is exercised too.
        var data = new byte[10_000];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)(i * 31);
        return new ByteArrayModel { Data = data, OptionalData = [1, 2, 3], Name = "big" };
    }

    private static EnumModel CreateEnumModel() => new()
    {
        Role = UserRole.Admin,
        OptionalRole = UserRole.User,
        TaskPriority = Priority.High,
        UserPermissions = Permissions.Read | Permissions.Write,
        CurrentStatus = Status.Pending,
        Name = "enums",
    };

    private static NestedModel CreateNested() => new()
    {
        Title = "nested",
        Details = CreateSimple(),
        Tags = ["a", "b", "c"],
    };

    /// <summary>Serializes via every non-byte[] overload and checks each equals the byte[] overload's output.</summary>
    private static async Task AssertAllSerializeOverloadsMatchAsync<T>(T value, NCborTypeInfo<T> typeInfo)
    {
        var expected = NCborSerializer.Serialize(value, typeInfo);

        using (var ms = new MemoryStream())
        {
            NCborSerializer.Serialize(ms, value, typeInfo);
            ms.ToArray().Should().Equal(expected, "Serialize(Stream) must match Serialize(byte[])");
        }

        using (var ms = new MemoryStream())
        {
            await NCborSerializer.SerializeAsync(ms, value, typeInfo);
            ms.ToArray().Should().Equal(expected, "SerializeAsync(Stream) must match Serialize(byte[])");
        }

        var bufferWriter = new ArrayBufferWriter<byte>();
        NCborSerializer.Serialize(bufferWriter, value, typeInfo);
        bufferWriter.WrittenSpan.ToArray().Should().Equal(expected, "Serialize(IBufferWriter) must match Serialize(byte[])");
    }

    /// <summary>Deserializes the same bytes via every overload and returns the results for comparison.</summary>
    private static async Task<List<T>> DeserializeViaAllOverloadsAsync<T>(byte[] bytes, NCborTypeInfo<T> typeInfo)
    {
        var results = new List<T>
        {
            NCborSerializer.Deserialize(bytes, typeInfo),
            NCborSerializer.Deserialize(new ReadOnlyMemory<byte>(bytes), typeInfo),
            NCborSerializer.Deserialize(new MemoryStream(bytes), typeInfo),
            await NCborSerializer.DeserializeAsync(new MemoryStream(bytes), typeInfo),
        };
        return results;
    }

    // ------------------------------------------------------------------
    // Byte-for-byte equality with the byte[] overload
    // ------------------------------------------------------------------

    [Fact]
    public Task SerializeOverloads_SimpleModel_MatchByteArrayOverload() =>
        AssertAllSerializeOverloadsMatchAsync(CreateSimple(), _context.SimpleModel);

    [Fact]
    public Task SerializeOverloads_NestedModel_MatchByteArrayOverload() =>
        AssertAllSerializeOverloadsMatchAsync(CreateNested(), _context.NestedModel);

    [Fact]
    public Task SerializeOverloads_EnumModel_MatchByteArrayOverload() =>
        AssertAllSerializeOverloadsMatchAsync(CreateEnumModel(), _context.EnumModel);

    [Fact]
    public Task SerializeOverloads_LargeChunkedByteArray_MatchByteArrayOverload() =>
        AssertAllSerializeOverloadsMatchAsync(CreateLargeByteArrayModel(), _context.ByteArrayModel);

    [Fact]
    public Task SerializeOverloads_TopLevelByteArray_MatchByteArrayOverload() =>
        AssertAllSerializeOverloadsMatchAsync(new byte[] { 0, 1, 2, 254, 255 }, _context.ArrayOfByte);

    // ------------------------------------------------------------------
    // Round trips
    // ------------------------------------------------------------------

    [Fact]
    public async Task RoundTrip_SimpleModel_ThroughEveryDeserializeOverload()
    {
        var original = CreateSimple();
        var bytes = NCborSerializer.Serialize(original, _context.SimpleModel);

        foreach (var result in await DeserializeViaAllOverloadsAsync(bytes, _context.SimpleModel))
        {
            result.Should().BeEquivalentTo(original);
        }
    }

    [Fact]
    public async Task RoundTrip_NestedModel_ThroughEveryDeserializeOverload()
    {
        var original = CreateNested();
        var bytes = NCborSerializer.Serialize(original, _context.NestedModel);

        foreach (var result in await DeserializeViaAllOverloadsAsync(bytes, _context.NestedModel))
        {
            result.Should().BeEquivalentTo(original);
        }
    }

    [Fact]
    public async Task RoundTrip_LargeByteArrayModel_ThroughEveryDeserializeOverload()
    {
        var original = CreateLargeByteArrayModel();
        var bytes = NCborSerializer.Serialize(original, _context.ByteArrayModel);

        foreach (var result in await DeserializeViaAllOverloadsAsync(bytes, _context.ByteArrayModel))
        {
            result.Data.Should().Equal(original.Data);
            result.OptionalData.Should().Equal(original.OptionalData);
            result.Name.Should().Be(original.Name);
        }
    }

    [Fact]
    public void RoundTrip_StreamToStream()
    {
        var original = CreateEnumModel();
        using var ms = new MemoryStream();

        NCborSerializer.Serialize(ms, original, _context.EnumModel);
        ms.Position = 0;
        var result = NCborSerializer.Deserialize(ms, _context.EnumModel);

        result.Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task RoundTrip_StreamToStream_Async()
    {
        var original = CreateEnumModel();
        using var ms = new MemoryStream();

        await NCborSerializer.SerializeAsync(ms, original, _context.EnumModel);
        ms.Position = 0;
        var result = await NCborSerializer.DeserializeAsync(ms, _context.EnumModel);

        result.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void RoundTrip_BufferWriterToMemory()
    {
        var original = CreateNested();
        var bufferWriter = new ArrayBufferWriter<byte>();

        NCborSerializer.Serialize(bufferWriter, original, _context.NestedModel);
        var result = NCborSerializer.Deserialize(bufferWriter.WrittenMemory, _context.NestedModel);

        result.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void Serialize_BufferWriter_AppendsAfterExistingContent()
    {
        var bufferWriter = new ArrayBufferWriter<byte>();
        bufferWriter.Write(new byte[] { 0xDE, 0xAD });

        NCborSerializer.Serialize(bufferWriter, CreateSimple(), _context.SimpleModel);

        var expected = NCborSerializer.Serialize(CreateSimple(), _context.SimpleModel);
        bufferWriter.WrittenCount.Should().Be(2 + expected.Length);
        bufferWriter.WrittenSpan[..2].ToArray().Should().Equal(0xDE, 0xAD);
        bufferWriter.WrittenSpan[2..].ToArray().Should().Equal(expected);
    }

    [Fact]
    public void Serialize_Stream_AppendsAtCurrentPosition()
    {
        using var ms = new MemoryStream();
        ms.Write(new byte[] { 0xDE, 0xAD });

        NCborSerializer.Serialize(ms, CreateSimple(), _context.SimpleModel);

        var expected = NCborSerializer.Serialize(CreateSimple(), _context.SimpleModel);
        ms.ToArray().Should().Equal(new byte[] { 0xDE, 0xAD }.Concat(expected));
    }

    [Fact]
    public void Deserialize_Memory_SliceOfLargerBuffer()
    {
        var original = CreateSimple();
        var cbor = NCborSerializer.Serialize(original, _context.SimpleModel);
        var padded = new byte[cbor.Length + 6];
        cbor.CopyTo(padded, 3);

        var result = NCborSerializer.Deserialize(new ReadOnlyMemory<byte>(padded, 3, cbor.Length), _context.SimpleModel);

        result.Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task Deserialize_Stream_ReadsFromCurrentPosition()
    {
        var original = CreateSimple();
        var cbor = NCborSerializer.Serialize(original, _context.SimpleModel);
        var prefixed = new byte[] { 0xFF, 0xFF, 0xFF }.Concat(cbor).ToArray();

        using var sync = new MemoryStream(prefixed) { Position = 3 };
        NCborSerializer.Deserialize(sync, _context.SimpleModel).Should().BeEquivalentTo(original);

        using var async = new MemoryStream(prefixed) { Position = 3 };
        (await NCborSerializer.DeserializeAsync(async, _context.SimpleModel)).Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task Deserialize_NonSeekableStream_Works()
    {
        var original = CreateLargeByteArrayModel();
        var cbor = NCborSerializer.Serialize(original, _context.ByteArrayModel);

        var sync = NCborSerializer.Deserialize(new NonSeekableStream(cbor), _context.ByteArrayModel);
        var async = await NCborSerializer.DeserializeAsync(new NonSeekableStream(cbor), _context.ByteArrayModel);

        sync.Data.Should().Equal(original.Data);
        async.Data.Should().Equal(original.Data);
    }

    [Fact]
    public void Stream_IsLeftOpen()
    {
        using var ms = new MemoryStream();
        NCborSerializer.Serialize(ms, CreateSimple(), _context.SimpleModel);
        ms.Position = 0;
        NCborSerializer.Deserialize(ms, _context.SimpleModel);

        ms.CanRead.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // Empty-input check
    // ------------------------------------------------------------------

    [Fact]
    public void Deserialize_EmptyMemory_ThrowsNCborValidationException()
    {
        var act = () => NCborSerializer.Deserialize(ReadOnlyMemory<byte>.Empty, _context.SimpleModel);
        act.Should().Throw<NCborValidationException>().WithMessage("*CBOR data cannot be empty*");
    }

    [Fact]
    public void Deserialize_EmptyStream_ThrowsNCborValidationException()
    {
        var act = () => NCborSerializer.Deserialize(new MemoryStream(), _context.SimpleModel);
        act.Should().Throw<NCborValidationException>().WithMessage("*CBOR data cannot be empty*");
    }

    [Fact]
    public async Task DeserializeAsync_EmptyStream_ThrowsNCborValidationException()
    {
        var act = () => NCborSerializer.DeserializeAsync(new MemoryStream(), _context.SimpleModel).AsTask();
        await act.Should().ThrowAsync<NCborValidationException>().WithMessage("*CBOR data cannot be empty*");
    }

    [Fact]
    public void Deserialize_StreamPositionedAtEnd_ThrowsNCborValidationException()
    {
        var ms = new MemoryStream(new byte[] { 0xA0 }) { Position = 1 };
        var act = () => NCborSerializer.Deserialize(ms, _context.SimpleModel);
        act.Should().Throw<NCborValidationException>().WithMessage("*CBOR data cannot be empty*");
    }

    // ------------------------------------------------------------------
    // Error wrapping: every overload must throw the same exception type and
    // message as the byte[] overload for the same input.
    // ------------------------------------------------------------------

    public static TheoryData<byte[]> InvalidPayloads => new()
    {
        new byte[] { 0xFF, 0xFE, 0xFD },             // invalid structure
        new byte[] { 0xA1 },                         // truncated map
        new byte[] { 0x64, 0x74, 0x65, 0x73, 0x74 }, // text string where a map is expected
        new byte[] { 0x1C },                         // reserved additional-info value
    };

    [Theory]
    [MemberData(nameof(InvalidPayloads))]
    public async Task Deserialize_InvalidData_EveryOverloadThrowsSameWrappedExceptionAsByteArray(byte[] payload)
    {
        var baseline = Record.Exception(() => NCborSerializer.Deserialize(payload, _context.SimpleModel));
        baseline.Should().NotBeNull();
        baseline.Should().Match<Exception>(e =>
            e is NCborDeserializationException || e is NCborValidationException,
            "the byte[] overload wraps decode failures in NCbor exceptions");

        var memory = Record.Exception(() => NCborSerializer.Deserialize(new ReadOnlyMemory<byte>(payload), _context.SimpleModel));
        var stream = Record.Exception(() => NCborSerializer.Deserialize(new MemoryStream(payload), _context.SimpleModel));
        var asyncStream = await Record.ExceptionAsync(() => NCborSerializer.DeserializeAsync(new MemoryStream(payload), _context.SimpleModel).AsTask());

        foreach (var ex in new[] { memory, stream, asyncStream })
        {
            ex.Should().NotBeNull();
            ex!.GetType().Should().Be(baseline!.GetType());
            ex.Message.Should().Be(baseline.Message);
        }
    }

    [Fact]
    public void Deserialize_DepthLimitExceeded_StreamAndMemoryWrapLikeByteArray()
    {
        // Build a chain deeper than CircularRefContext.MaxDepth (5) using the deeper context.
        var head = new SelfRefNode { Name = "0" };
        var node = head;
        for (int i = 1; i < 10; i++) { node.Next = new SelfRefNode { Name = i.ToString() }; node = node.Next; }
        var bytes = NCborSerializer.Serialize(head, CircularRefDeepContext.Default.SelfRefNode);

        var shallow = CircularRefContext.Default.SelfRefNode;
        var byteArray = Record.Exception(() => NCborSerializer.Deserialize(bytes, shallow));
        var memory = Record.Exception(() => NCborSerializer.Deserialize(new ReadOnlyMemory<byte>(bytes), shallow));
        var stream = Record.Exception(() => NCborSerializer.Deserialize(new MemoryStream(bytes), shallow));

        byteArray.Should().BeOfType<NCborDeserializationException>();
        memory.Should().BeOfType<NCborDeserializationException>().Which.Message.Should().Be(byteArray!.Message);
        stream.Should().BeOfType<NCborDeserializationException>().Which.Message.Should().Be(byteArray.Message);
    }

    [Fact]
    public async Task Serialize_Failure_WrapsLikeByteArray_AndWritesNothing()
    {
        var a = new SelfRefNode { Name = "a" };
        a.Next = a; // true cycle -> MaxDepth exceeded
        var typeInfo = CircularRefContext.Default.SelfRefNode;

        var baseline = Record.Exception(() => NCborSerializer.Serialize(a, typeInfo));
        baseline.Should().BeOfType<NCborSerializationException>();

        using var ms = new MemoryStream();
        var stream = Record.Exception(() => NCborSerializer.Serialize(ms, a, typeInfo));
        stream.Should().BeOfType<NCborSerializationException>().Which.Message.Should().Be(baseline!.Message);
        ms.Length.Should().Be(0);

        using var asyncMs = new MemoryStream();
        var asyncStream = await Record.ExceptionAsync(() => NCborSerializer.SerializeAsync(asyncMs, a, typeInfo));
        asyncStream.Should().BeOfType<NCborSerializationException>().Which.Message.Should().Be(baseline.Message);
        asyncMs.Length.Should().Be(0);

        var bufferWriter = new ArrayBufferWriter<byte>();
        var buffer = Record.Exception(() => NCborSerializer.Serialize(bufferWriter, a, typeInfo));
        buffer.Should().BeOfType<NCborSerializationException>().Which.Message.Should().Be(baseline.Message);
        bufferWriter.WrittenCount.Should().Be(0);
    }

    [Fact]
    public void StreamIOException_IsNotWrapped()
    {
        var act = () => NCborSerializer.Deserialize(new ThrowingStream(), _context.SimpleModel);
        act.Should().ThrowExactly<IOException>();

        var actWrite = () => NCborSerializer.Serialize(new ThrowingStream(), CreateSimple(), _context.SimpleModel);
        actWrite.Should().ThrowExactly<IOException>();
    }

    // ------------------------------------------------------------------
    // Argument validation
    // ------------------------------------------------------------------

    [Fact]
    public void NullArguments_ThrowArgumentNullException_Synchronously()
    {
        var model = CreateSimple();
        var info = _context.SimpleModel;

        ((Action)(() => NCborSerializer.Serialize((Stream)null!, model, info))).Should().Throw<ArgumentNullException>().WithParameterName("stream");
        ((Action)(() => NCborSerializer.Serialize(new MemoryStream(), model, null!))).Should().Throw<ArgumentNullException>().WithParameterName("typeInfo");
        ((Action)(() => NCborSerializer.Serialize((IBufferWriter<byte>)null!, model, info))).Should().Throw<ArgumentNullException>().WithParameterName("writer");
        ((Action)(() => NCborSerializer.Serialize(new ArrayBufferWriter<byte>(), model, null!))).Should().Throw<ArgumentNullException>().WithParameterName("typeInfo");
        ((Action)(() => NCborSerializer.Deserialize(new ReadOnlyMemory<byte>(new byte[] { 0xA0 }), (NCborTypeInfo<SimpleModel>)null!))).Should().Throw<ArgumentNullException>().WithParameterName("typeInfo");
        ((Action)(() => NCborSerializer.Deserialize((Stream)null!, info))).Should().Throw<ArgumentNullException>().WithParameterName("stream");
        ((Action)(() => NCborSerializer.Deserialize(new MemoryStream(), (NCborTypeInfo<SimpleModel>)null!))).Should().Throw<ArgumentNullException>().WithParameterName("typeInfo");

        // Async APIs validate arguments eagerly (before returning a Task), like System.Text.Json.
        ((Action)(() => NCborSerializer.SerializeAsync((Stream)null!, model, info))).Should().Throw<ArgumentNullException>().WithParameterName("stream");
        ((Action)(() => NCborSerializer.SerializeAsync(new MemoryStream(), model, null!))).Should().Throw<ArgumentNullException>().WithParameterName("typeInfo");
        ((Action)(() => NCborSerializer.DeserializeAsync((Stream)null!, info))).Should().Throw<ArgumentNullException>().WithParameterName("stream");
        ((Action)(() => NCborSerializer.DeserializeAsync(new MemoryStream(), (NCborTypeInfo<SimpleModel>)null!))).Should().Throw<ArgumentNullException>().WithParameterName("typeInfo");
    }

    // ------------------------------------------------------------------
    // Cancellation
    // ------------------------------------------------------------------

    [Fact]
    public async Task SerializeAsync_PreCanceledToken_ThrowsOperationCanceled_AndWritesNothing()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var ms = new MemoryStream();

        var act = () => NCborSerializer.SerializeAsync(ms, CreateSimple(), _context.SimpleModel, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        ms.Length.Should().Be(0);
    }

    [Fact]
    public async Task DeserializeAsync_PreCanceledToken_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var bytes = NCborSerializer.Serialize(CreateSimple(), _context.SimpleModel);

        var act = () => NCborSerializer.DeserializeAsync(new MemoryStream(bytes), _context.SimpleModel, cts.Token).AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DeserializeAsync_CanceledDuringRead_ThrowsOperationCanceled_NotWrapped()
    {
        using var cts = new CancellationTokenSource();
        var bytes = NCborSerializer.Serialize(CreateSimple(), _context.SimpleModel);
        var stream = new CancelOnReadStream(bytes, cts);

        var act = () => NCborSerializer.DeserializeAsync(stream, _context.SimpleModel, cts.Token).AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
        stream.ObservedToken.Should().Be(cts.Token, "the caller's token must flow through to the stream read");
    }

    [Fact]
    public async Task SerializeAsync_PassesTokenToStreamWrite()
    {
        using var cts = new CancellationTokenSource();
        var stream = new RecordingWriteStream();

        await NCborSerializer.SerializeAsync(stream, CreateSimple(), _context.SimpleModel, cts.Token);

        stream.ObservedToken.Should().Be(cts.Token);
        stream.Written.ToArray().Should().Equal(NCborSerializer.Serialize(CreateSimple(), _context.SimpleModel));
    }

    // ------------------------------------------------------------------
    // Test streams
    // ------------------------------------------------------------------

    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();
    }

    private sealed class ThrowingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("boom");
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("boom");
    }

    /// <summary>Returns one byte per read, and cancels the token on the second read.</summary>
    private sealed class CancelOnReadStream(byte[] data, CancellationTokenSource cts) : MemoryStream(data)
    {
        private int _reads;
        public CancellationToken ObservedToken { get; private set; }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ObservedToken = cancellationToken;
            if (++_reads == 2) cts.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
        }
    }

    private sealed class RecordingWriteStream : MemoryStream
    {
        public CancellationToken ObservedToken { get; private set; }
        public MemoryStream Written => this;

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ObservedToken = cancellationToken;
            return base.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ObservedToken = cancellationToken;
            return base.WriteAsync(buffer, cancellationToken);
        }
    }
}
