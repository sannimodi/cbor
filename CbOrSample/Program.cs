// Create a new Person object.
var person = new Person { Name = "Alice", Age = 30 };

// Serialize the Person object to a CBOR byte array.
var writer = new CborWriter();
Person.Serialize(writer, person);
byte[] bytes = writer.Encode();

Console.WriteLine($"Serialized bytes: {BitConverter.ToString(bytes)}");

// Deserialize the CBOR byte array back to a Person object.
var reader = new CborReader(bytes);
var decoded = Person.Deserialize(reader);
Console.WriteLine($"Decoded: Name={decoded.Name}, Age={decoded.Age}");

