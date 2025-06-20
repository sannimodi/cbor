namespace CbOrSerialization.Tests;

[CbOrSerializable(typeof(SimpleModel))]
[CbOrSourceGenerationOptions(PropertyNamingPolicy = CbOrKnownNamingPolicy.LowerCase)]
public partial class LowerCaseContext : CbOrSerializerContext
{
}
