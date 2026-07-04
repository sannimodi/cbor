namespace NCbor.Tests;

[NCborSerializable(typeof(SelfRefNode))]
[NCborSourceGenerationOptions(MaxDepth = 5)]
public partial class CircularRefContext : NCborSerializerContext
{
}

[NCborSerializable(typeof(SelfRefNode))]
[NCborSourceGenerationOptions(MaxDepth = 20)]
public partial class CircularRefDeepContext : NCborSerializerContext
{
}
