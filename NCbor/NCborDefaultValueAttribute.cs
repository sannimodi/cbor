namespace NCbor;

/// <summary>
/// Specifies a default value for a property.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class NCborDefaultValueAttribute : Attribute
{
    /// <summary>
    /// Gets the default value.
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NCborDefaultValueAttribute"/> class.
    /// </summary>
    /// <param name="value">The default value.</param>
    public NCborDefaultValueAttribute(object? value)
    {
        Value = value;
    }
}
