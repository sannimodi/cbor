namespace CborSerialization.Demo;

// Define a simple model
public class Person
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool IsActive { get; set; }
}

// Create a context for serialization
[CborSerializable(typeof(Person))]
public partial class MyCborContext : CborSerializerContext
{

}

class Program
{
    static void Main(string[] args)
    {
        // Create a sample person
        var person = new Person
        {
            Name = "John Doe",
            Age = 30,
            IsActive = true
        };

        // Serialize to CBOR
        var cborData = CborSerializer.Serialize(person, MyCborContext.Default.Person);

        // Print the CBOR data
        Console.WriteLine("CBOR Data (hex):");
        Console.WriteLine(BitConverter.ToString(cborData));

        // Deserialize back
        var deserializedPerson = CborSerializer.Deserialize<Person>(cborData, MyCborContext.Default.Person);

        // Print the deserialized data
        Console.WriteLine("\nDeserialized Person:");
        Console.WriteLine($"Name: {deserializedPerson.Name}");
        Console.WriteLine($"Age: {deserializedPerson.Age}");
        Console.WriteLine($"IsActive: {deserializedPerson.IsActive}");
    }
}