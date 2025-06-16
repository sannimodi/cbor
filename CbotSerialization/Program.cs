using System;
using System.Formats.Cbor;

class Program
{
    static void Main()
    {
        var person = new Person { Name = "Alice", Age = 30 };

        var writer = new CborWriter();
        Person.Serialize(writer, person);
        byte[] bytes = writer.Encode();

        Console.WriteLine($"Serialized bytes: {BitConverter.ToString(bytes)}");

        var reader = new CborReader(bytes);
        var decoded = Person.Deserialize(reader);
        Console.WriteLine($"Decoded: Name={decoded.Name}, Age={decoded.Age}");
    }
}
