namespace CbOrSerialization.Tests;

[CbOrSerializable(typeof(SimpleModel))]
[CbOrSourceGenerationOptions(PropertyNamingPolicy = CbOrKnownNamingPolicy.SnakeCaseLower)]
public partial class SnakeCaseContext : CbOrSerializerContext
{
}

