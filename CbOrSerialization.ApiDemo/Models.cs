using CbOrSerialization;

namespace CbOrSerialization.ApiDemo;

// Simple user model for demo
public class SimpleUser
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
}

// CBOR context for the simple demo
[CbOrSerializable(typeof(SimpleUser))]
[CbOrSerializable(typeof(List<SimpleUser>))]
[CbOrSourceGenerationOptions(PropertyNamingPolicy = CbOrKnownNamingPolicy.CamelCase)]
public partial class SimpleCborContext : CbOrSerializerContext 
{
}