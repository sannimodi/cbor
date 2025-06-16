using System.Formats.Cbor;

public partial class Person
{
    public static void Serialize(CborWriter writer, Person value)
    {
        writer.WriteStartMap(2);
        writer.WriteTextString("Name");
        writer.WriteTextString(value.Name);
        writer.WriteTextString("Age");
        writer.WriteInt32(value.Age);
        writer.WriteEndMap();
    }

    public static Person Deserialize(CborReader reader)
    {
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
                default:
                    reader.SkipDataItem();
                    break;
            }
        }
        reader.ReadEndMap();
        return new Person { Name = name ?? string.Empty, Age = age };
    }
}
