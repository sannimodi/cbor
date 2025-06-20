namespace CbOrSerialization;

/// <summary>
/// Configures options for CBOR source generation.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CbOrSourceGenerationOptionsAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the property naming policy.
    /// </summary>
    public CbOrKnownNamingPolicy PropertyNamingPolicy { get; set; }

    /// <summary>
    /// Gets or sets the default ignore condition.
    /// </summary>
    public CbOrIgnoreCondition DefaultIgnoreCondition { get; set; }

    /// <summary>
    /// Gets or sets the maximum depth for serialization.
    /// </summary>
    public int MaxDepth { get; set; } = 64;
}

/// <summary>
/// Defines the available property naming policies.
/// </summary>
public enum CbOrKnownNamingPolicy
{
    /// <summary>
    /// No specific naming policy.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Camel case naming policy (e.g., "propertyName").
    /// </summary>
    CamelCase = 1,

    /// <summary>
    /// Snake case naming policy with lowercase (e.g., "property_name").
    /// </summary>
    SnakeCaseLower = 2,

    /// <summary>
    /// Snake case naming policy with uppercase (e.g., "PROPERTY_NAME").
    /// </summary>
    SnakeCaseUpper = 3,

    /// <summary>
    /// Kebab case naming policy with lowercase (e.g., "property-name").
    /// </summary>
    KebabCaseLower = 4,

    /// <summary>
    /// Kebab case naming policy with uppercase (e.g., "PROPERTY-NAME").
    /// </summary>
    KebabCaseUpper = 5,

    /// <summary>
    /// Uppercase naming policy (e.g., "PROPERTYNAME").
    /// </summary>
    UpperCase = 6,

    /// <summary>
    /// Lowercase naming policy (e.g., "propertyname").
    /// </summary>
    LowerCase = 7
}

/// <summary>
/// Defines when a property should be ignored during serialization.
/// </summary>
public enum CbOrIgnoreCondition
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

