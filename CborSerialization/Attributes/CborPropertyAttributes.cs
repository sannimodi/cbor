namespace CborSerialization;

/// <summary>
/// Specifies the name of the property in the CBOR representation.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CborPropertyNameAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CborPropertyNameAttribute"/> class.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    public CborPropertyNameAttribute(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}

/// <summary>
/// Indicates that the property should be ignored during serialization.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class CborIgnoreAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the condition under which the property should be ignored.
    /// </summary>
    public CborIgnoreCondition Condition { get; set; } = CborIgnoreCondition.Always;
}

/// <summary>
/// Specifies a default value for a property.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class CborDefaultValueAttribute : Attribute
{
    /// <summary>
    /// Gets the default value.
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CborDefaultValueAttribute"/> class.
    /// </summary>
    /// <param name="value">The default value.</param>
    public CborDefaultValueAttribute(object? value)
    {
        Value = value;
    }
}

/// <summary>
/// Specifies a custom converter for a property or type.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class CborConverterAttribute : Attribute
{
    /// <summary>
    /// Gets the type of the converter.
    /// </summary>
    public Type ConverterType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CborConverterAttribute"/> class.
    /// </summary>
    /// <param name="converterType">The type of the converter.</param>
    public CborConverterAttribute(Type converterType)
    {
        ConverterType = converterType ?? throw new ArgumentNullException(nameof(converterType));
    }
}

/// <summary>
/// Indicates that the constructor should be used for deserialization.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor)]
public sealed class CborConstructorAttribute : Attribute
{
} 