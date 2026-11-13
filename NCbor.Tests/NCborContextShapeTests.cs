namespace NCbor.Tests;

public class ContextShapeModel
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

/// <summary>A context nested in partial containing types (one generic) and declared internal.</summary>
public static partial class ContextShapeHost
{
    public partial class Middle<TTag>
    {
        [NCborSerializable(typeof(ContextShapeModel))]
        internal partial class NestedContext : NCborSerializerContext { }
    }
}

[NCborSerializable(typeof(ContextShapeModel))]
internal partial class InternalTopLevelContext : NCborSerializerContext { }

public class NCborContextShapeTests
{
    [Fact]
    public void NestedInternalContext_RoundTrips()
    {
        var info = ContextShapeHost.Middle<int>.NestedContext.Default.ContextShapeModel;
        var model = new ContextShapeModel { Id = 3, Name = "nested" };

        var result = NCborSerializer.Deserialize(NCborSerializer.Serialize(model, info), info);

        result.Should().BeEquivalentTo(model);
        typeof(ContextShapeHost.Middle<int>.NestedContext).IsNestedAssembly.Should().BeTrue("the declared internal accessibility is preserved");
    }

    [Fact]
    public void InternalTopLevelContext_RoundTrips_AndStaysInternal()
    {
        var info = InternalTopLevelContext.Default.ContextShapeModel;
        var model = new ContextShapeModel { Id = 4, Name = null };

        NCborSerializer.Deserialize(NCborSerializer.Serialize(model, info), info).Should().BeEquivalentTo(model);
        typeof(InternalTopLevelContext).IsPublic.Should().BeFalse();
    }
}
