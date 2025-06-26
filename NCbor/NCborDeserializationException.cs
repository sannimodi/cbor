namespace NCbor;

/// <summary>
/// The exception that is thrown when an error occurs during CBOR deserialization.
/// </summary>
public class NCborDeserializationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NCborDeserializationException"/> class.
    /// </summary>
    public NCborDeserializationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NCborDeserializationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public NCborDeserializationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NCborDeserializationException"/> class with a specified error message and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public NCborDeserializationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NCborDeserializationException"/> class with type information.
    /// </summary>
    /// <param name="type">The type that failed to deserialize.</param>
    /// <param name="message">The message that describes the error.</param>
    public NCborDeserializationException(Type type, string message) : base($"Failed to deserialize to type '{type?.Name ?? "unknown"}': {message}")
    {
        Type = type;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NCborDeserializationException"/> class with type information and inner exception.
    /// </summary>
    /// <param name="type">The type that failed to deserialize.</param>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public NCborDeserializationException(Type type, string message, Exception innerException) : base($"Failed to deserialize to type '{type?.Name ?? "unknown"}': {message}", innerException)
    {
        Type = type;
    }

    /// <summary>
    /// Gets the type that failed to deserialize, if available.
    /// </summary>
    public Type? Type { get; }
}