namespace NCbor;

/// <summary>
/// Defines when a property should be ignored during serialization.
/// </summary>
public enum NCborIgnoreCondition
{
    /// <summary>
    /// Always ignore the property.
    /// </summary>
    Always,

    /// <summary>
    /// Never ignore the property.
    /// </summary>
    Never,

    /// <summary>
    /// Ignore the property when writing default values.
    /// </summary>
    WhenWritingDefault,

    /// <summary>
    /// Ignore the property when writing null values.
    /// </summary>
    WhenWritingNull
} 

