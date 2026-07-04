namespace NCbor.Tests;

[NCborSerializable(typeof(AlwaysIgnoreDefaultValueModel))]
[NCborSourceGenerationOptions(DefaultIgnoreCondition = NCborIgnoreCondition.Always)]
public partial class AlwaysIgnoreContext : NCborSerializerContext
{
}
