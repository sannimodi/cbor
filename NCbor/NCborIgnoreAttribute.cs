namespace NCbor;

/// <summary>
/// Indicates that the property should be ignored during serialization.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class NCborIgnoreAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the condition under which the property should be ignored.
    /// </summary>
    public NCborIgnoreCondition Condition { get; set; } = NCborIgnoreCondition.Always;
}
