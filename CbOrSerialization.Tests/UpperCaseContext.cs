namespace CbOrSerialization.Tests;

[CbOrSerializable(typeof(SimpleModel))]
[CbOrSourceGenerationOptions(PropertyNamingPolicy = CbOrKnownNamingPolicy.UpperCase)]
public partial class UpperCaseContext : CbOrSerializerContext
{
}
