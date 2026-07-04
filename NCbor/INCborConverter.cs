namespace NCbor;

/// <summary>
/// Provides custom CBOR serialization and deserialization logic for a specific type, used when a
/// property is decorated with <see cref="NCborConverterAttribute"/> to override the generator's
/// default handling for that property's type.
/// </summary>
public interface INCborConverter<T>
{
    /// <summary>
    /// Writes the value to CBOR format.
    /// </summary>
    void Write(CborWriter writer, T value);

    /// <summary>
    /// Reads a value from CBOR format.
    /// </summary>
    T Read(CborReader reader);
}
