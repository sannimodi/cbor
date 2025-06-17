// Create a sample person
using CborSerialization;
using CborSerialization.Demo;

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
var deserializedPerson = CborSerializer.Deserialize(cborData, MyCborContext.Default.Person);

// Print the deserialized data
Console.WriteLine("\nDeserialized Person:");
Console.WriteLine($"Name: {deserializedPerson.Name}");
Console.WriteLine($"Age: {deserializedPerson.Age}");
Console.WriteLine($"IsActive: {deserializedPerson.IsActive}");