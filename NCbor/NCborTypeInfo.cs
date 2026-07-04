namespace NCbor;

/// <summary>
/// Provides type information and serialization methods for a specific type.
/// </summary>
public abstract class NCborTypeInfo<T>
{
    /// <summary>
    /// Gets the type that this instance provides information for.
    /// </summary>
    public Type Type => typeof(T);

    /// <summary>
    /// Serializes the specified value to CBOR format.
    /// </summary>
    /// <param name="depth">
    /// Current object-graph nesting depth. Callers should not pass this explicitly; generated
    /// code increments it on each recursive call into a nested type's <see cref="NCborTypeInfo{T}"/>
    /// to detect circular references and enforce <c>NCborSourceGenerationOptionsAttribute.MaxDepth</c>.
    /// </param>
    public abstract void Serialize(CborWriter writer, T value, int depth = 0);

    /// <summary>
    /// Deserializes a value from CBOR format.
    /// </summary>
    /// <param name="depth">
    /// Current object-graph nesting depth. Callers should not pass this explicitly; generated
    /// code increments it on each recursive call into a nested type's <see cref="NCborTypeInfo{T}"/>
    /// to detect circular references and enforce <c>NCborSourceGenerationOptionsAttribute.MaxDepth</c>.
    /// </param>
    public abstract T Deserialize(CborReader reader, int depth = 0);
} 

