namespace NCbor;

/// <summary>
/// The exception that is thrown when CBOR data validation fails.
/// </summary>
public class NCborValidationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NCborValidationException"/> class.
    /// </summary>
    public NCborValidationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NCborValidationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public NCborValidationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NCborValidationException"/> class with a specified error message and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public NCborValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NCborValidationException"/> class with property information.
    /// </summary>
    /// <param name="propertyName">The name of the property that failed validation.</param>
    /// <param name="message">The message that describes the error.</param>
    public NCborValidationException(string propertyName, string message) : base($"Validation failed for property '{propertyName}': {message}")
    {
        PropertyName = propertyName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NCborValidationException"/> class with type and property information.
    /// </summary>
    /// <param name="type">The type being validated.</param>
    /// <param name="propertyName">The name of the property that failed validation.</param>
    /// <param name="message">The message that describes the error.</param>
    public NCborValidationException(Type type, string propertyName, string message) : base($"Validation failed for property '{propertyName}' on type '{type?.Name ?? "unknown"}': {message}")
    {
        Type = type;
        PropertyName = propertyName;
    }

    /// <summary>
    /// Gets the type being validated, if available.
    /// </summary>
    public Type? Type { get; }

    /// <summary>
    /// Gets the name of the property that failed validation, if available.
    /// </summary>
    public string? PropertyName { get; }
}