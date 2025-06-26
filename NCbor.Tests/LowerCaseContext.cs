namespace NCbor.Tests;

[NCborSerializable(typeof(SimpleModel))]
[NCborSourceGenerationOptions(PropertyNamingPolicy = NCborNamingPolicy.LowerCase)]
public partial class LowerCaseContext : NCborSerializerContext
{
}
