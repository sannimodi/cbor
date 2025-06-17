namespace CborSerialization;

/// <summary>
/// Indicates that the type should be included in CBOR serialization source generation.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class CborSerializableAttribute : Attribute
{
    /// <summary>
    /// Gets the type to be included in source generation.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CborSerializableAttribute"/> class.
    /// </summary>
    /// <param name="type">The type to be included in source generation.</param>
    public CborSerializableAttribute(Type type)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
    }
} 