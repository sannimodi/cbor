namespace NCbor.Tests;

[NCborSerializable(typeof(DefaultIgnoreConditionModel))]
[NCborSourceGenerationOptions(DefaultIgnoreCondition = NCborIgnoreCondition.WhenWritingDefault)]
public partial class DefaultIgnoreConditionContext : NCborSerializerContext
{
}
