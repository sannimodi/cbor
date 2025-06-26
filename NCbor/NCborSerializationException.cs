namespace NCbor;

/// <summary>
/// The exception that is thrown when an error occurs during CBOR serialization.
/// </summary>
public class NCborSerializationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NCborSerializationException"/> class.
    /// </summary>
    public NCborSerializationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NCborSerializationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public NCborSerializationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NCborSerializationException"/> class with a specified error message and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public NCborSerializationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NCborSerializationException"/> class with type information.
    /// </summary>
    /// <param name="type">The type that failed to serialize.</param>
    /// <param name="message">The message that describes the error.</param>
    public NCborSerializationException(Type type, string message) : base($"Failed to serialize type '{type?.Name ?? "unknown"}': {message}")
    {
        Type = type;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NCborSerializationException"/> class with type information and inner exception.
    /// </summary>
    /// <param name="type">The type that failed to serialize.</param>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public NCborSerializationException(Type type, string message, Exception innerException) : base($"Failed to serialize type '{type?.Name ?? "unknown"}': {message}", innerException)
    {
        Type = type;
    }

    /// <summary>
    /// Gets the type that failed to serialize, if available.
    /// </summary>
    public Type? Type { get; }
}