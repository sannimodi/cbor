namespace NCbor.Tests;

[NCborSerializable(typeof(SimpleModel))]
[NCborSourceGenerationOptions(PropertyNamingPolicy = NCborNamingPolicy.SnakeCaseLower)]
public partial class SnakeCaseContext : NCborSerializerContext
{
}
