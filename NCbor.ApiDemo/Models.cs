namespace NCbor.ApiDemo;

// Simple user model for demo
public class SimpleUser
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
}

// CBOR context for the simple demo
[NCborSerializable(typeof(SimpleUser))]
[NCborSerializable(typeof(List<SimpleUser>))]
[NCborSourceGenerationOptions(PropertyNamingPolicy = NCborNamingPolicy.CamelCase)]
public partial class SimpleCborContext : NCborSerializerContext 
{
}