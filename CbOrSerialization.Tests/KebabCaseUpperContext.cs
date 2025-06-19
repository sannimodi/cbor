namespace CbOrSerialization.Tests;

[CbOrSerializable(typeof(SimpleModel))]
[CbOrSourceGenerationOptions(PropertyNamingPolicy = CbOrKnownNamingPolicy.KebabCaseUpper)]
public partial class KebabCaseUpperContext : CbOrSerializerContext
{
}
