namespace NCbor.Tests;

public class NCborCircularReferenceTests
{
    private static SelfRefNode BuildAcyclicChain(int length)
    {
        var head = new SelfRefNode { Name = "node-0" };
        var current = head;
        for (int i = 1; i < length; i++)
        {
            var next = new SelfRefNode { Name = $"node-{i}" };
            current.Next = next;
            current = next;
        }
        return head;
    }

    [Fact]
    public void Serialize_TrueCircularReference_ThrowsNCborSerializationException()
    {
        var a = new SelfRefNode { Name = "a" };
        var b = new SelfRefNode { Name = "b" };
        a.Next = b;
        b.Next = a;

        Action act = () => NCborSerializer.Serialize(a, CircularRefContext.Default.SelfRefNode);

        act.Should().Throw<NCborSerializationException>()
            .WithMessage("*depth*");
    }

    [Fact]
    public void Serialize_ShallowAcyclicChain_WithinMaxDepth_RoundTrips()
    {
        var head = BuildAcyclicChain(3);

        var bytes = NCborSerializer.Serialize(head, CircularRefContext.Default.SelfRefNode);
        var result = NCborSerializer.Deserialize(bytes, CircularRefContext.Default.SelfRefNode);

        result.Name.Should().Be("node-0");
        result.Next!.Name.Should().Be("node-1");
        result.Next.Next!.Name.Should().Be("node-2");
        result.Next.Next.Next.Should().BeNull();
    }

    [Fact]
    public void Serialize_AcyclicChainExceedingMaxDepth_ThrowsNCborSerializationException()
    {
        var head = BuildAcyclicChain(10);

        Action act = () => NCborSerializer.Serialize(head, CircularRefContext.Default.SelfRefNode);

        act.Should().Throw<NCborSerializationException>()
            .WithMessage("*depth*");
    }

    [Fact]
    public void Deserialize_PayloadExceedingContextMaxDepth_ThrowsNCborDeserializationException()
    {
        var head = BuildAcyclicChain(8);
        var bytes = NCborSerializer.Serialize(head, CircularRefDeepContext.Default.SelfRefNode);

        Action act = () => NCborSerializer.Deserialize(bytes, CircularRefContext.Default.SelfRefNode);

        act.Should().Throw<NCborDeserializationException>()
            .WithMessage("*depth*");
    }

    [Fact]
    public void Serialize_ChainExactlyAtMaxDepth_Succeeds()
    {
        // CircularRefContext has MaxDepth = 5; a chain of exactly 5 nodes must be the last
        // length that still succeeds (depths 0..4 checked against `depth >= 5`).
        var head = BuildAcyclicChain(5);

        var bytes = NCborSerializer.Serialize(head, CircularRefContext.Default.SelfRefNode);
        var result = NCborSerializer.Deserialize(bytes, CircularRefContext.Default.SelfRefNode);

        result.Should().NotBeNull();
        result.Name.Should().Be("node-0");
    }

    [Fact]
    public void Serialize_ChainOneNodeOverMaxDepth_Throws()
    {
        // One node more than the exact-boundary case above must be the first length that throws.
        var head = BuildAcyclicChain(6);

        Action act = () => NCborSerializer.Serialize(head, CircularRefContext.Default.SelfRefNode);

        act.Should().Throw<NCborSerializationException>()
            .WithMessage("*depth*");
    }
}
