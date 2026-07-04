namespace NCbor;

/// <summary>
/// Serializes the decorated enum property as its name (a CBOR text string) instead of its
/// underlying numeric value.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class NCborEnumAsStringAttribute : Attribute
{
}
