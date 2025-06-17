// Create a sample person
using CborSerialization;
using CborSerialization.Demo;

var person = new Person { Name = "John Doe", Age = 30, IsActive = true };

// Serialize to CBOR
var cborData = CborSerializer.Serialize(person, MyCborContext.Default.Person);

// Print the CBOR data
Console.WriteLine("CBOR Data (hex):");
Console.WriteLine(BitConverter.ToString(cborData));

// Deserialize back
var deserializedPerson = CborSerializer.Deserialize(cborData, MyCborContext.Default.Person);

// Print the deserialized data
Console.WriteLine("\nDeserialized Person:");
Console.WriteLine($"Name: {deserializedPerson.Name}");
Console.WriteLine($"Age: {deserializedPerson.Age}");
Console.WriteLine($"IsActive: {deserializedPerson.IsActive}");

// New test case: Serialize and deserialize a List<Person>
var people = new List<Person>
{
    new Person { Name = "Alice", Age = 25, IsActive = true },
    new Person { Name = "Bob", Age = 35, IsActive = false }
};
var listCborData = CborSerializer.Serialize(people, MyCborContext.Default.ListOfPerson);
Console.WriteLine("\nCBOR Data for List<Person> (hex):");
Console.WriteLine(BitConverter.ToString(listCborData));
var deserializedPeople = CborSerializer.Deserialize<List<Person>>(listCborData, MyCborContext.Default.ListOfPerson);
Console.WriteLine("\nDeserialized List<Person>:");
foreach (var p in deserializedPeople)
{
    Console.WriteLine($"Name: {p.Name}, Age: {p.Age}, IsActive: {p.IsActive}");
}