using System.Formats.Cbor;

namespace CborSerialization;

/// <summary>
/// Provides static methods for CBOR serialization and deserialization.
/// </summary>
public static class CborSerializer
{
    /// <summary>
    /// Serializes the specified value to CBOR format.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <param name="typeInfo">The type information for serialization.</param>
    /// <returns>The serialized CBOR data.</returns>
    public static byte[] Serialize<T>(T value, CborTypeInfo<T> typeInfo)
    {
        if (typeInfo == null)
            throw new ArgumentNullException(nameof(typeInfo));

        var writer = new CborWriter();
        typeInfo.Serialize(writer, value);
        return writer.Encode();
    }

    /// <summary>
    /// Deserializes a value from CBOR format.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="data">The CBOR data to deserialize.</param>
    /// <param name="typeInfo">The type information for deserialization.</param>
    /// <returns>The deserialized value.</returns>
    public static T Deserialize<T>(byte[] data, CborTypeInfo<T> typeInfo)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (typeInfo == null)
            throw new ArgumentNullException(nameof(typeInfo));

        var reader = new CborReader(data);
        return typeInfo.Deserialize(reader);
    }
} 