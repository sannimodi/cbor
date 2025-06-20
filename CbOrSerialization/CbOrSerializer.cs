using System.Formats.Cbor;

namespace CbOrSerialization;

/// <summary>
/// Provides static methods for CBOR serialization and deserialization.
/// </summary>
public static class CbOrSerializer
{
    /// <summary>
    /// Serializes the specified value to CBOR format.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <param name="typeInfo">The type information for serialization.</param>
    /// <returns>The serialized CBOR data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="typeInfo"/> is null.</exception>
    /// <exception cref="CbOrSerializationException">Thrown when serialization fails.</exception>
    public static byte[] Serialize<T>(T value, CbOrTypeInfo<T> typeInfo)
    {
        if (typeInfo == null)
            throw new ArgumentNullException(nameof(typeInfo));

        try
        {
            var writer = new CborWriter();
            typeInfo.Serialize(writer, value);
            return writer.Encode();
        }
        catch (CbOrSerializationException)
        {
            // Re-throw our custom exceptions as-is
            throw;
        }
        catch (InvalidOperationException ex)
        {
            throw new CbOrSerializationException(typeof(T), ex.Message, ex);
        }
        catch (Exception ex) when (!(ex is ArgumentNullException))
        {
            throw new CbOrSerializationException(typeof(T), $"Unexpected error during serialization: {ex.Message}", ex);
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
    /// <exception cref="CbOrDeserializationException">Thrown when deserialization fails.</exception>
    /// <exception cref="CbOrValidationException">Thrown when CBOR data validation fails.</exception>
    public static T Deserialize<T>(byte[] data, CbOrTypeInfo<T> typeInfo)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (typeInfo == null)
            throw new ArgumentNullException(nameof(typeInfo));
        
        if (data.Length == 0)
            throw new CbOrValidationException("CBOR data cannot be empty");

        try
        {
            var reader = new CborReader(data);
            return typeInfo.Deserialize(reader);
        }
        catch (CbOrDeserializationException)
        {
            // Re-throw our custom exceptions as-is
            throw;
        }
        catch (CbOrValidationException)
        {
            // Re-throw our custom exceptions as-is
            throw;
        }
        catch (FormatException ex)
        {
            throw new CbOrValidationException($"Invalid CBOR data format for type {typeof(T).Name}: {ex.Message}", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new CbOrDeserializationException(typeof(T), ex.Message, ex);
        }
        catch (Exception ex) when (!(ex is ArgumentNullException or ArgumentException))
        {
            throw new CbOrDeserializationException(typeof(T), $"Unexpected error during deserialization: {ex.Message}", ex);
        }
    }
} 

