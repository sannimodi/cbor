using System;
using System.Formats.Cbor;

public class Person
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

class Program
{
    static void Main()
    {
        var person = new Person { Name = "Alice", Age = 30 };

        var writer = new CborWriter();
        writer.WriteStartMap(2);
        writer.WriteTextString("Name");
        writer.WriteTextString(person.Name);
        writer.WriteTextString("Age");
        writer.WriteInt32(person.Age);
        writer.WriteEndMap();
        byte[] bytes = writer.Encode();

        Console.WriteLine($"Serialized bytes: {BitConverter.ToString(bytes)}");

        var reader = new CborReader(bytes);
        reader.ReadStartMap();
        string? name = null;
        int age = 0;
        for (int i = 0; i < 2; i++)
        {
            string key = reader.ReadTextString();
            switch (key)
            {
                case "Name":
                    name = reader.ReadTextString();
                    break;
                case "Age":
                    age = reader.ReadInt32();
                    break;
            }
        }
        reader.ReadEndMap();

        var decoded = new Person { Name = name ?? string.Empty, Age = age };
        Console.WriteLine($"Decoded: Name={decoded.Name}, Age={decoded.Age}");
    }
}
