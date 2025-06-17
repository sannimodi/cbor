using System.Formats.Cbor;

namespace CborSerialization;

/// <summary>
/// Provides type information and serialization methods for a specific type.
/// </summary>
public abstract class CborTypeInfo<T>
{
    /// <summary>
    /// Gets the type that this instance provides information for.
    /// </summary>
    public Type Type => typeof(T);

    /// <summary>
    /// Serializes the specified value to CBOR format.
    /// </summary>
    public abstract void Serialize(CborWriter writer, T value);

    /// <summary>
    /// Deserializes a value from CBOR format.
    /// </summary>
    public abstract T Deserialize(CborReader reader);
} 