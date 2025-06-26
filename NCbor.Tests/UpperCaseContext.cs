namespace NCbor.Tests;

[NCborSerializable(typeof(SimpleModel))]
[NCborSourceGenerationOptions(PropertyNamingPolicy = NCborNamingPolicy.UpperCase)]
public partial class UpperCaseContext : NCborSerializerContext
{
}
