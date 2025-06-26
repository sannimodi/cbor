namespace NCbor.Tests;

[NCborSerializable(typeof(SimpleModel))]
[NCborSourceGenerationOptions(PropertyNamingPolicy = NCborNamingPolicy.KebabCaseUpper)]
public partial class KebabCaseUpperContext : NCborSerializerContext
{
}
