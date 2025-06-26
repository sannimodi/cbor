namespace NCbor;

/// <summary>
/// Specifies the name of the property in the CBOR representation.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NCborPropertyNameAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NCborPropertyNameAttribute"/> class.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    public NCborPropertyNameAttribute(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}
