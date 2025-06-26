namespace NCbor.Tests;

[NCborSerializable(typeof(SimpleModel))]
[NCborSourceGenerationOptions(PropertyNamingPolicy = NCborNamingPolicy.KebabCaseLower)]
public partial class KebabCaseLowerContext : NCborSerializerContext
{
}
