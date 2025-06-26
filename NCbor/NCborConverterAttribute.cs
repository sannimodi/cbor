namespace NCbor;

/// <summary>
/// Specifies a custom converter for a property or type.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class NCborConverterAttribute : Attribute
{
    /// <summary>
    /// Gets the type of the converter.
    /// </summary>
    public Type ConverterType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NCborConverterAttribute"/> class.
    /// </summary>
    /// <param name="converterType">The type of the converter.</param>
    public NCborConverterAttribute(Type converterType)
    {
        ConverterType = converterType ?? throw new ArgumentNullException(nameof(converterType));
    }
}

