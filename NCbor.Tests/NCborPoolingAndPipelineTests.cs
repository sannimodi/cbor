using System.Buffers;
using System.IO.Pipelines;

namespace NCbor.Tests;

public class PoolInner
{
    public int X { get; set; }
    public string Label { get; set; } = "";
}

/// <summary>Calls back into NCborSerializer from inside a write/read — exercises re-entrancy of the per-thread writer/reader reuse.</summary>
public class ReentrantInnerConverter : INCborConverter<PoolInner>
{
    public void Write(CborWriter writer, PoolInner value) =>
        writer.WriteByteString(NCborSerializer.Serialize(value, PoolingContext.Default.PoolInner));

    public PoolInner Read(CborReader reader) =>
        NCborSerializer.Deserialize(reader.ReadByteString(), PoolingContext.Default.PoolInner);
}

public class PoolOuter
{
    public int A { get; set; }

    [NCborConverter(typeof(ReentrantInnerConverter))]
    public PoolInner Inner { get; set; } = new();

    public string B { get; set; } = "";
}

[NCborSerializable(typeof(PoolOuter))]
[NCborSerializable(typeof(PoolInner))]
public partial class PoolingContext : NCborSerializerContext
{
}

public class NCborPoolingAndPipelineTests
{
    private static readonly TestNCborContext Ctx = TestNCborContext.Default;
    private static SimpleModel Sample() => new() { Name = "Ada", Age = 36, IsActive = true };

    // ---------- writer/reader reuse ----------

    [Fact]
    public void ReentrantConverter_GetsItsOwnWriterAndReader_OutputIsCorrect()
    {
        var value = new PoolOuter { A = 1, Inner = new PoolInner { X = 42, Label = "in" }, B = "after" };

        var bytes = NCborSerializer.Serialize(value, PoolingContext.Default.PoolOuter);
        var back = NCborSerializer.Deserialize(bytes, PoolingContext.Default.PoolOuter);

        back.A.Should().Be(1);
        back.Inner.X.Should().Be(42);
        back.Inner.Label.Should().Be("in");
        back.B.Should().Be("after");
        // Same bytes on a second run (a shared, half-written writer would corrupt the outer document).
        NCborSerializer.Serialize(value, PoolingContext.Default.PoolOuter).Should().Equal(bytes);
    }

    [Fact]
    public void FailedSerialization_DoesNotPoisonTheReusedWriter()
    {
        var expected = NCborSerializer.Serialize(Sample(), Ctx.SimpleModel);

        var a = new SelfRefNode { Name = "a" };
        a.Next = a;
        var act = () => NCborSerializer.Serialize(a, CircularRefContext.Default.SelfRefNode);
        act.Should().Throw<NCborSerializationException>();

        NCborSerializer.Serialize(Sample(), Ctx.SimpleModel).Should().Equal(expected);
    }

    [Fact]
    public void FailedDeserialization_DoesNotPoisonTheReusedReader()
    {
        var good = NCborSerializer.Serialize(Sample(), Ctx.SimpleModel);
        var act = () => NCborSerializer.Deserialize(new byte[] { 0xA1, 0x61 }, Ctx.SimpleModel); // truncated
        act.Should().Throw<Exception>();

        NCborSerializer.Deserialize(good, Ctx.SimpleModel).Should().BeEquivalentTo(Sample());
    }

    [Fact]
    public void LargePayload_ThenSmall_BothCorrect()
    {
        var big = new ByteArrayModel { Name = "big", Data = new byte[2 * 1024 * 1024] };
        new Random(1).NextBytes(big.Data);

        NCborSerializer.Deserialize(NCborSerializer.Serialize(big, Ctx.ByteArrayModel), Ctx.ByteArrayModel)
            .Data.Should().Equal(big.Data);
        NCborSerializer.Deserialize(NCborSerializer.Serialize(Sample(), Ctx.SimpleModel), Ctx.SimpleModel)
            .Should().BeEquivalentTo(Sample());
    }

    [Fact]
    public async Task ParallelSerialization_AcrossThreads_IsCorrect()
    {
        var expected = NCborSerializer.Serialize(Sample(), Ctx.SimpleModel);
        var results = await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 200; i++)
            {
                var bytes = NCborSerializer.Serialize(Sample(), Ctx.SimpleModel);
                if (!bytes.AsSpan().SequenceEqual(expected) ||
                    NCborSerializer.Deserialize(bytes, Ctx.SimpleModel).Age != 36)
                    return false;
            }
            return true;
        })));
        results.Should().AllSatisfy(ok => ok.Should().BeTrue());
    }

    // ---------- allocation bounds (sync paths, after warm-up) ----------

    [Fact]
    public void Serialize_ToBufferWriter_SteadyState_AllocatesAlmostNothing()
    {
        var model = Sample();
        var buffer = new ArrayBufferWriter<byte>(4096);
        for (int i = 0; i < 50; i++) { buffer.Clear(); NCborSerializer.Serialize(buffer, model, Ctx.SimpleModel); }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { buffer.Clear(); NCborSerializer.Serialize(buffer, model, Ctx.SimpleModel); }
        long perCall = (GC.GetAllocatedBytesForCurrentThread() - before) / 1000;

        // A fresh CborWriter per call cost ~1.8 KB; reuse should leave essentially nothing.
        perCall.Should().BeLessThan(128);
    }

    [Fact]
    public void Deserialize_FromMemory_SteadyState_AllocatesOnlyTheResult()
    {
        var data = new ReadOnlyMemory<byte>(NCborSerializer.Serialize(Sample(), Ctx.SimpleModel));
        for (int i = 0; i < 50; i++) NCborSerializer.Deserialize(data, Ctx.SimpleModel);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) NCborSerializer.Deserialize(data, Ctx.SimpleModel);
        long perCall = (GC.GetAllocatedBytesForCurrentThread() - before) / 1000;

        // Result object + strings only; a fresh CborReader per call added ~0.7 KB on top.
        perCall.Should().BeLessThan(400);
    }

    private static long AllocatedBySmallSerialize()
    {
        var buffer = new ArrayBufferWriter<byte>(4096);
        long before = GC.GetAllocatedBytesForCurrentThread();
        NCborSerializer.Serialize(buffer, Sample(), Ctx.SimpleModel);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static void SerializeBytePayload(int size) =>
        NCborSerializer.Serialize(new ArrayBufferWriter<byte>(size + 1024),
            new ByteArrayModel { Name = "p", Data = new byte[size] }, Ctx.ByteArrayModel);

    [Fact]
    public void WriterUnderTheCap_IsRetained_NextSmallCallReusesIt()
    {
        SerializeBytePayload(512 * 1024);
        // Retained writer: the next call allocates no new CborWriter (~1.7 KB).
        AllocatedBySmallSerialize().Should().BeLessThan(512);
    }

    [Fact]
    public void WriterOverTheCap_IsDropped_NextSmallCallAllocatesAFreshOne()
    {
        SerializeBytePayload(2 * 1024 * 1024);
        // The 2 MB writer must not be retained for the thread's lifetime: a fresh writer is created next.
        AllocatedBySmallSerialize().Should().BeGreaterThan(1024);
    }

    [Fact]
    public void ReaderIsResetOnReturn_InputBufferIsCollectable()
    {
        static WeakReference DeserializeAndDrop()
        {
            byte[] data = NCborSerializer.Serialize(Sample(), Ctx.SimpleModel);
            NCborSerializer.Deserialize(new ReadOnlyMemory<byte>(data), Ctx.SimpleModel);
            return new WeakReference(data);
        }

        WeakReference weak = DeserializeAndDrop();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        weak.IsAlive.Should().BeFalse("the per-thread CborReader must not keep the caller's data alive");
    }

    [Fact]
    public async Task PipeWriter_FlushCanceledWhileBackpressured_ThrowsOperationCanceled()
    {
        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 100, resumeWriterThreshold: 50));
        var big = new ByteArrayModel { Name = "bp", Data = new byte[1000] };

        Task writing = NCborSerializer.SerializeAsync(pipe.Writer, big, Ctx.ByteArrayModel);
        writing.IsCompleted.Should().BeFalse("the flush should be waiting on backpressure");
        pipe.Writer.CancelPendingFlush();

        await writing.Invoking(t => t).Should().ThrowAsync<OperationCanceledException>();
    }

    // ---------- PipeWriter / PipeReader ----------

    [Fact]
    public async Task Pipe_RoundTrip_BytesMatchByteArrayOverload()
    {
        var pipe = new Pipe();
        await NCborSerializer.SerializeAsync(pipe.Writer, Sample(), Ctx.SimpleModel);
        await pipe.Writer.CompleteAsync();

        ReadResult raw = await pipe.Reader.ReadAsync();
        raw.Buffer.ToArray().Should().Equal(NCborSerializer.Serialize(Sample(), Ctx.SimpleModel));
        pipe.Reader.AdvanceTo(raw.Buffer.Start); // leave it unread for the deserializer

        (await NCborSerializer.DeserializeAsync(pipe.Reader, Ctx.SimpleModel)).Should().BeEquivalentTo(Sample());
    }

    [Fact]
    public async Task PipeReader_MultiSegmentPayload_WrittenInChunks_RoundTrips()
    {
        var big = new ByteArrayModel { Name = "chunked", Data = new byte[200_000] };
        new Random(7).NextBytes(big.Data);
        var bytes = NCborSerializer.Serialize(big, Ctx.ByteArrayModel);

        var pipe = new Pipe(new PipeOptions(minimumSegmentSize: 512, pauseWriterThreshold: 0));
        var reading = NCborSerializer.DeserializeAsync(pipe.Reader, Ctx.ByteArrayModel).AsTask();
        for (int offset = 0; offset < bytes.Length; offset += 1000)
        {
            await pipe.Writer.WriteAsync(bytes.AsMemory(offset, Math.Min(1000, bytes.Length - offset)));
        }
        await pipe.Writer.CompleteAsync();

        var result = await reading;
        result.Name.Should().Be("chunked");
        result.Data.Should().Equal(big.Data);
    }

    [Fact]
    public async Task PipeReader_Empty_ThrowsValidation()
    {
        var pipe = new Pipe();
        await pipe.Writer.CompleteAsync();
        var act = () => NCborSerializer.DeserializeAsync(pipe.Reader, Ctx.SimpleModel).AsTask();
        await act.Should().ThrowAsync<NCborValidationException>().WithMessage("*cannot be empty*");
    }

    [Fact]
    public async Task PipeReader_TrailingData_ThrowsValidation()
    {
        var one = NCborSerializer.Serialize(Sample(), Ctx.SimpleModel);
        var pipe = new Pipe();
        await pipe.Writer.WriteAsync(one.Concat(one).ToArray());
        await pipe.Writer.CompleteAsync();
        var act = () => NCborSerializer.DeserializeAsync(pipe.Reader, Ctx.SimpleModel).AsTask();
        await act.Should().ThrowAsync<NCborValidationException>().WithMessage("*trailing*");
    }

    [Fact]
    public async Task PipeReader_CancelPendingRead_ThrowsOperationCanceled()
    {
        var pipe = new Pipe();
        var reading = NCborSerializer.DeserializeAsync(pipe.Reader, Ctx.SimpleModel).AsTask();
        pipe.Reader.CancelPendingRead();
        await reading.Invoking(t => t).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PipeReader_PreCanceledToken_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => NCborSerializer.DeserializeAsync(new Pipe().Reader, Ctx.SimpleModel, cts.Token).AsTask();
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PipeWriter_PreCanceledToken_ThrowsBeforeEncoding_WritesNothing()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var pipe = new Pipe();
        var act = () => NCborSerializer.SerializeAsync(pipe.Writer, Sample(), Ctx.SimpleModel, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();

        await pipe.Writer.CompleteAsync();
        (await pipe.Reader.ReadAsync()).Buffer.Length.Should().Be(0);
    }

    [Fact]
    public void PipeOverloads_NullArguments_Throw()
    {
        ((Action)(() => NCborSerializer.SerializeAsync((PipeWriter)null!, Sample(), Ctx.SimpleModel)))
            .Should().Throw<ArgumentNullException>().WithParameterName("pipeWriter");
        ((Action)(() => NCborSerializer.DeserializeAsync((PipeReader)null!, Ctx.SimpleModel)))
            .Should().Throw<ArgumentNullException>().WithParameterName("pipeReader");
        ((Action)(() => NCborSerializer.SerializeAsync(new Pipe().Writer, Sample(), (NCborTypeInfo<SimpleModel>)null!)))
            .Should().Throw<ArgumentNullException>().WithParameterName("typeInfo");
    }

    [Fact]
    public async Task PipeWriter_SerializationFailure_IsWrappedAndNothingIsAdvanced()
    {
        var a = new SelfRefNode { Name = "a" };
        a.Next = a;
        var pipe = new Pipe();
        var act = () => NCborSerializer.SerializeAsync(pipe.Writer, a, CircularRefContext.Default.SelfRefNode);
        await act.Should().ThrowAsync<NCborSerializationException>();

        await pipe.Writer.CompleteAsync();
        (await pipe.Reader.ReadAsync()).Buffer.Length.Should().Be(0);
    }
}
