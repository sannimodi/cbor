namespace NCbor;

/// <summary>
/// Indicates that the constructor should be used for deserialization.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor)]
public sealed class NCborConstructorAttribute : Attribute
{
} 
