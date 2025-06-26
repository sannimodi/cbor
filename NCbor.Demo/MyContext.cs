using NCbor.Demo.Domain;

namespace NCbor.Demo;

// Core models
[NCborSerializable(typeof(Person))]
[NCborSerializable(typeof(Address))]
[NCborSerializable(typeof(Department))]

// Collections
[NCborSerializable(typeof(List<Person>))]
[NCborSerializable(typeof(List<string>))]

// Dictionary types - showcasing new Dictionary support
[NCborSerializable(typeof(Dictionary<string, string>))]
[NCborSerializable(typeof(Dictionary<string, int>))]
[NCborSerializable(typeof(Dictionary<string, Person>))]
[NCborSerializable(typeof(Dictionary<Guid, List<string>>))]

// Naming policy configuration
[NCborSourceGenerationOptions(PropertyNamingPolicy = NCborNamingPolicy.CamelCase)]
public partial class MyContext : NCborSerializerContext
{
}
