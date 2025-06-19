namespace CbOrSerialization.Tests;

[CbOrSerializable(typeof(SimpleModel))]
[CbOrSourceGenerationOptions(PropertyNamingPolicy = CbOrKnownNamingPolicy.SnakeCaseUpper)]
public partial class SnakeCaseUpperContext : CbOrSerializerContext
{
}
