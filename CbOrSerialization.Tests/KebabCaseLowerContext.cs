namespace CbOrSerialization.Tests;

[CbOrSerializable(typeof(SimpleModel))]
[CbOrSourceGenerationOptions(PropertyNamingPolicy = CbOrKnownNamingPolicy.KebabCaseLower)]
public partial class KebabCaseLowerContext : CbOrSerializerContext
{
}
