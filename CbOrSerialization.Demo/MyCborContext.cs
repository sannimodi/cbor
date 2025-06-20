namespace CbOrSerialization.Demo;

// Core models
[CbOrSerializable(typeof(Person))]
[CbOrSerializable(typeof(Address))]
[CbOrSerializable(typeof(Department))]

// Collections
[CbOrSerializable(typeof(List<Person>))]
[CbOrSerializable(typeof(List<string>))]

// Dictionary types - showcasing new Dictionary support
[CbOrSerializable(typeof(Dictionary<string, string>))]
[CbOrSerializable(typeof(Dictionary<string, int>))]
[CbOrSerializable(typeof(Dictionary<string, Person>))]
[CbOrSerializable(typeof(Dictionary<Guid, List<string>>))]

// Naming policy configuration
[CbOrSourceGenerationOptions(PropertyNamingPolicy = CbOrKnownNamingPolicy.CamelCase)]
public partial class MyCbOrContext : CbOrSerializerContext
{
} 

