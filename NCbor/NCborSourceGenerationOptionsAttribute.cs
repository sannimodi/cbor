namespace NCbor;

/// <summary>
/// Configures options for CBOR source generation.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class NCborSourceGenerationOptionsAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the property naming policy.
    /// </summary>
    public NCborNamingPolicy PropertyNamingPolicy { get; set; }

    /// <summary>
    /// Gets or sets the default ignore condition.
    /// </summary>
    public NCborIgnoreCondition DefaultIgnoreCondition { get; set; }

    /// <summary>
    /// Gets or sets the maximum depth for serialization.
    /// </summary>
    public int MaxDepth { get; set; } = 64;
}
