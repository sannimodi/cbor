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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="typeInfo"/> is null.</exception>
    public static byte[] Serialize<T>(T value, CborTypeInfo<T> typeInfo)
    {
        if (typeInfo == null)
            throw new ArgumentNullException(nameof(typeInfo));

        try
        {
            var writer = new CborWriter();
            typeInfo.Serialize(writer, value);
            return writer.Encode();
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"Failed to serialize object of type {typeof(T).Name}: {ex.Message}", ex);
        }
        catch (Exception ex) when (!(ex is ArgumentNullException))
        {
            throw new InvalidOperationException($"Unexpected error during serialization of type {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deserializes a value from CBOR format.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="data">The CBOR data to deserialize.</param>
    /// <param name="typeInfo">The type information for deserialization.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> or <paramref name="typeInfo"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="data"/> is empty.</exception>
    public static T Deserialize<T>(byte[] data, CborTypeInfo<T> typeInfo)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (typeInfo == null)
            throw new ArgumentNullException(nameof(typeInfo));
        
        if (data.Length == 0)
            throw new ArgumentException("CBOR data cannot be empty.", nameof(data));

        try
        {
            var reader = new CborReader(data);
            return typeInfo.Deserialize(reader);
        }
        catch (FormatException ex)
        {
            throw new FormatException($"Invalid CBOR data format for type {typeof(T).Name}: {ex.Message}", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"Failed to deserialize CBOR data to type {typeof(T).Name}: {ex.Message}", ex);
        }
        catch (Exception ex) when (!(ex is ArgumentNullException or ArgumentException))
        {
            throw new InvalidOperationException($"Unexpected error during deserialization to type {typeof(T).Name}: {ex.Message}", ex);
        }
    }
} 