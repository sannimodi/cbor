var person = new Person
{
    FirstName = "John",
    LastName = "Doe",
    Age = 30
};

var writer = new CborWriter();
var cborData = CborSerializer.Serialize(person, MyCborContext.Person);

Console.WriteLine("CBOR Data (hex):");
Console.WriteLine(BitConverter.ToString(cborData));

var deserialized = CborSerializer.Deserialize<Person>(cborData, MyCborContext.Person);
Console.WriteLine("\nDeserialized Person:");
Console.WriteLine($"First Name: {deserialized.FirstName}");
Console.WriteLine($"Last Name: {deserialized.LastName}");
Console.WriteLine($"Age: {deserialized.Age}");