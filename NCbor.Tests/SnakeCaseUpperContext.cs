namespace NCbor.Tests;

[NCborSerializable(typeof(SimpleModel))]
[NCborSourceGenerationOptions(PropertyNamingPolicy = NCborNamingPolicy.SnakeCaseUpper)]
public partial class SnakeCaseUpperContext : NCborSerializerContext
{
}
