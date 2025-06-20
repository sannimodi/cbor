namespace CbOrSerialization;

/// <summary>
/// Indicates that the type should be included in CBOR serialization source generation.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class CbOrSerializableAttribute : Attribute
{
    /// <summary>
    /// Gets the type to be included in source generation.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CbOrSerializableAttribute"/> class.
    /// </summary>
    /// <param name="type">The type to be included in source generation.</param>
    public CbOrSerializableAttribute(Type type)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
    }
} 

