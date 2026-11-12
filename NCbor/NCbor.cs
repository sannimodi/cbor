using System.Buffers;

namespace NCbor;

/// <summary>
/// Provides static methods for CBOR serialization and deserialization.
/// </summary>
/// <remarks>
/// Every overload funnels through the same two cores (<c>WriteValue</c> / <c>ReadValue</c>), so the
/// exception mapping (<see cref="NCborSerializationException"/>, <see cref="NCborDeserializationException"/>,
/// <see cref="NCborValidationException"/>) and the empty-input check are identical regardless of whether
/// the data comes from a <see cref="T:byte[]"/>, <see cref="ReadOnlyMemory{T}"/>, <see cref="Stream"/>, or
/// <see cref="IBufferWriter{T}"/>. Stream I/O and cancellation happen outside the wrapped region, so
/// <see cref="IOException"/> and <see cref="OperationCanceledException"/> propagate unwrapped.
/// </remarks>
public static class NCborSerializer
{
    private const int StreamCopyBufferSize = 81920;

    // ------------------------------------------------------------------
    // Serialize
    // ------------------------------------------------------------------

    /// <summary>
    /// Serializes the specified value to CBOR format.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <param name="typeInfo">The type information for serialization.</param>
    /// <returns>The serialized CBOR data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="typeInfo"/> is null.</exception>
    /// <exception cref="NCborSerializationException">Thrown when serialization fails.</exception>
    public static byte[] Serialize<T>(T value, NCborTypeInfo<T> typeInfo)
    {
        if (typeInfo == null)
            throw new ArgumentNullException(nameof(typeInfo));

        return WriteValue(value, typeInfo).Encode();
    }

    /// <summary>
    /// Serializes the specified value as CBOR and writes it directly into <paramref name="writer"/>,
    /// without allocating an intermediate <see cref="T:byte[]"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="writer">The buffer writer to append the encoded CBOR data to.</param>
    /// <param name="value">The value to serialize.</param>
    /// <param name="typeInfo">The type information for serialization.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="writer"/> or <paramref name="typeInfo"/> is null.</exception>
    /// <exception cref="NCborSerializationException">Thrown when serialization fails.</exception>
    public static void Serialize<T>(IBufferWriter<byte> writer, T value, NCborTypeInfo<T> typeInfo)
    {
        if (writer == null)
            throw new ArgumentNullException(nameof(writer));
        if (typeInfo == null)
            throw new ArgumentNullException(nameof(typeInfo));

        var cborWriter = WriteValue(value, typeInfo);
        var span = writer.GetSpan(cborWriter.BytesWritten);
        int written = cborWriter.Encode(span);
        writer.Advance(written);
    }

    /// <summary>
    /// Serializes the specified value as CBOR and writes it to <paramref name="stream"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="stream">The stream to write the encoded CBOR data to. The stream is not flushed or closed.</param>
    /// <param name="value">The value to serialize.</param>
    /// <param name="typeInfo">The type information for serialization.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> or <paramref name="typeInfo"/> is null.</exception>
    /// <exception cref="NCborSerializationException">Thrown when serialization fails.</exception>
    public static void Serialize<T>(Stream stream, T value, NCborTypeInfo<T> typeInfo)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));
        if (typeInfo == null)
            throw new ArgumentNullException(nameof(typeInfo));

        var cborWriter = WriteValue(value, typeInfo);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(cborWriter.BytesWritten);
        try
        {
            int written = cborWriter.Encode(buffer);
            stream.Write(buffer, 0, written);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    /// <summary>
    /// Asynchronously serializes the specified value as CBOR and writes it to <paramref name="stream"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="stream">The stream to write the encoded CBOR data to. The stream is not flushed or closed.</param>
    /// <param name="value">The value to serialize.</param>
    /// <param name="typeInfo">The type information for serialization.</param>
    /// <param name="cancellationToken">A token that may be used to cancel the write operation.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    /// <remarks>
    /// Encoding is performed synchronously into a pooled buffer (the underlying <see cref="CborWriter"/> is not
    /// streaming); only the write to <paramref name="stream"/> is asynchronous.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> or <paramref name="typeInfo"/> is null.</exception>
    /// <exception cref="NCborSerializationException">Thrown when serialization fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static Task SerializeAsync<T>(Stream stream, T value, NCborTypeInfo<T> typeInfo, CancellationToken cancellationToken = default)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));
        if (typeInfo == null)
            throw new ArgumentNullException(nameof(typeInfo));

        return SerializeAsyncCore(stream, value, typeInfo, cancellationToken);
    }

    private static async Task SerializeAsyncCore<T>(Stream stream, T value, NCborTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cborWriter = WriteValue(value, typeInfo);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(cborWriter.BytesWritten);
        try
        {
            int written = cborWriter.Encode(buffer);
            await stream.WriteAsync(buffer, 0, written, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    // ------------------------------------------------------------------
    // Deserialize
    // ------------------------------------------------------------------

    /// <summary>
    /// Deserializes a value from CBOR format.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="data">The CBOR data to deserialize.</param>
    /// <param name="typeInfo">The type information for deserialization.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> or <paramref name="typeInfo"/> is null.</exception>
    /// <exception cref="NCborValidationException">Thrown when <paramref name="data"/> is empty or CBOR data validation fails.</exception>
    /// <exception cref="NCborDeserializationException">Thrown when deserialization fails.</exception>
    public static T Deserialize<T>(byte[] data, NCborTypeInfo<T> typeInfo)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (typeInfo == null)
            throw new ArgumentNullException(nameof(typeInfo));

        return Deserialize(new ReadOnlyMemory<byte>(data), typeInfo);
    }

    /// <summary>
    /// Deserializes a value from CBOR data held in a <see cref="ReadOnlyMemory{T}"/>, without copying it.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="data">The CBOR data to deserialize.</param>
    /// <param name="typeInfo">The type information for deserialization.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="typeInfo"/> is null.</exception>
    /// <exception cref="NCborValidationException">Thrown when <paramref name="data"/> is empty or CBOR data validation fails.</exception>
    /// <exception cref="NCborDeserializationException">Thrown when deserialization fails.</exception>
    public static T Deserialize<T>(ReadOnlyMemory<byte> data, NCborTypeInfo<T> typeInfo)
    {
        if (typeInfo == null)
            throw new ArgumentNullException(nameof(typeInfo));

        return ReadValue(data, typeInfo);
    }

    /// <summary>
    /// Deserializes a value from CBOR data read from <paramref name="stream"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="stream">The stream to read from, starting at its current position. The
    /// stream is read to its end and is not closed.</param>
    /// <param name="typeInfo">The type information for deserialization.</param>
    /// <returns>The deserialized value.</returns>
    /// <remarks>
    /// The stream is read to its end into an in-memory buffer before decoding begins, because the underlying
    /// <see cref="CborReader"/> is not a streaming reader. This overload therefore does not reduce peak memory
    /// use compared to reading the stream into a <see cref="T:byte[]"/> yourself; it is a convenience.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> or <paramref name="typeInfo"/> is null.</exception>
    /// <exception cref="NCborValidationException">Thrown when the stream contains no data or CBOR data validation fails.</exception>
    /// <exception cref="NCborDeserializationException">Thrown when deserialization fails.</exception>
    public static T Deserialize<T>(Stream stream, NCborTypeInfo<T> typeInfo)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));
        if (typeInfo == null)
            throw new ArgumentNullException(nameof(typeInfo));

        using var buffer = CreateReadBuffer(stream);
        stream.CopyTo(buffer, StreamCopyBufferSize);
        return ReadValue(AsMemory(buffer), typeInfo);
    }

    /// <summary>
    /// Asynchronously deserializes a value from CBOR data read from <paramref name="stream"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="stream">The stream to read from, starting at its current position. The
    /// stream is read to its end and is not closed.</param>
    /// <param name="typeInfo">The type information for deserialization.</param>
    /// <param name="cancellationToken">A token that may be used to cancel the read operation.</param>
    /// <returns>A value task whose result is the deserialized value.</returns>
    /// <remarks>
    /// The stream is read asynchronously to its end into an in-memory buffer, then decoded synchronously,
    /// because the underlying <see cref="CborReader"/> is not a streaming reader. Only the I/O is asynchronous
    /// and cancelable; peak memory use is the full size of the payload.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> or <paramref name="typeInfo"/> is null.</exception>
    /// <exception cref="NCborValidationException">Thrown when the stream contains no data or CBOR data validation fails.</exception>
    /// <exception cref="NCborDeserializationException">Thrown when deserialization fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static ValueTask<T> DeserializeAsync<T>(Stream stream, NCborTypeInfo<T> typeInfo, CancellationToken cancellationToken = default)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));
        if (typeInfo == null)
            throw new ArgumentNullException(nameof(typeInfo));

        return new ValueTask<T>(DeserializeAsyncCore(stream, typeInfo, cancellationToken));
    }

    private static async Task<T> DeserializeAsyncCore<T>(Stream stream, NCborTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var buffer = CreateReadBuffer(stream);
        await stream.CopyToAsync(buffer, StreamCopyBufferSize, cancellationToken).ConfigureAwait(false);
        return ReadValue(AsMemory(buffer), typeInfo);
    }

    // ------------------------------------------------------------------
    // Shared cores: all exception mapping lives here.
    // ------------------------------------------------------------------

    /// <summary>
    /// Runs the generated serializer into a fresh <see cref="CborWriter"/> and guarantees the returned writer
    /// holds a complete CBOR document, so callers can encode it (to an array, span, or stream) outside the
    /// exception-mapping region.
    /// </summary>
    private static CborWriter WriteValue<T>(T value, NCborTypeInfo<T> typeInfo)
    {
        try
        {
            var writer = new CborWriter();
            typeInfo.Serialize(writer, value);
            if (!writer.IsWriteCompleted)
            {
                // Throws CborWriter's own InvalidOperationException ("incomplete CBOR document"), which is
                // mapped below exactly as it was when Encode() lived inside this try block.
                writer.Encode();
            }
            return writer;
        }
        catch (NCborSerializationException)
        {
            // Re-throw our custom exceptions as-is
            throw;
        }
        catch (InvalidOperationException ex)
        {
            throw new NCborSerializationException(typeof(T), ex.Message, ex);
        }
        catch (Exception ex) when (!(ex is ArgumentNullException))
        {
            throw new NCborSerializationException(typeof(T), $"Unexpected error during serialization: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates that <paramref name="data"/> is non-empty and runs the generated deserializer over it.
    /// </summary>
    private static T ReadValue<T>(ReadOnlyMemory<byte> data, NCborTypeInfo<T> typeInfo)
    {
        if (data.Length == 0)
            throw new NCborValidationException("CBOR data cannot be empty");

        try
        {
            var reader = new CborReader(data);
            var result = typeInfo.Deserialize(reader);
            if (reader.BytesRemaining > 0)
            {
                // Matches System.Text.Json: a payload must contain exactly one top-level value. Silently
                // ignoring trailing bytes would hide truncated/concatenated documents and garbage input.
                throw new NCborValidationException(
                    $"Invalid CBOR data for type {typeof(T).Name}: {reader.BytesRemaining} unexpected trailing byte(s) after the top-level value.");
            }
            return result;
        }
        catch (NCborDeserializationException)
        {
            // Re-throw our custom exceptions as-is
            throw;
        }
        catch (NCborValidationException)
        {
            // Re-throw our custom exceptions as-is
            throw;
        }
        catch (FormatException ex)
        {
            throw new NCborValidationException($"Invalid CBOR data format for type {typeof(T).Name}: {ex.Message}", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new NCborDeserializationException(typeof(T), ex.Message, ex);
        }
        catch (Exception ex) when (!(ex is ArgumentNullException or ArgumentException))
        {
            throw new NCborDeserializationException(typeof(T), $"Unexpected error during deserialization: {ex.Message}", ex);
        }
    }

    private static MemoryStream CreateReadBuffer(Stream stream)
    {
        // Pre-size when the remaining length is knowable; never touch Length/Position on non-seekable streams.
        if (stream.CanSeek)
        {
            long remaining = stream.Length - stream.Position;
            if (remaining > 0 && remaining <= int.MaxValue)
                return new MemoryStream((int)remaining);
        }
        return new MemoryStream();
    }

    // MemoryStream we created ourselves, so GetBuffer() is always permitted; avoids the ToArray() copy.
    private static ReadOnlyMemory<byte> AsMemory(MemoryStream buffer) =>
        new ReadOnlyMemory<byte>(buffer.GetBuffer(), 0, (int)buffer.Length);
}
