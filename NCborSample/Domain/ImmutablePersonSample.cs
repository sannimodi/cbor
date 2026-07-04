namespace NCborSample.Domain;

/// <summary>
/// Manually reproduces what NCbor.Generator's [NCborConstructor] attribute automates:
/// deserializing into a type with get-only properties by reading fields into locals first, then
/// calling the constructor, instead of the default new-then-set-properties pattern. Validated
/// here before trusting the generator's own version.
/// </summary>
public class ImmutablePersonSample
{
    public ImmutablePersonSample(string name, int age)
    {
        Name = name;
        Age = age;
    }

    public string Name { get; }
    public int Age { get; }

    public static void Serialize(CborWriter writer, ImmutablePersonSample model)
    {
        writer.WriteStartMap(null);

        writer.WriteTextString("Name");
        writer.WriteTextString(model.Name);

        writer.WriteTextString("Age");
        writer.WriteInt32(model.Age);

        writer.WriteEndMap();
    }

    public static ImmutablePersonSample Deserialize(CborReader reader)
    {
        string name = "";
        int age = 0;

        reader.ReadStartMap();
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var propertyName = reader.ReadTextString();
            switch (propertyName)
            {
                case "Name":
                    name = reader.ReadTextString();
                    break;
                case "Age":
                    age = reader.ReadInt32();
                    break;
                default:
                    reader.SkipValue();
                    break;
            }
        }
        reader.ReadEndMap();

        // Get-only properties: must go through the constructor, not property setters.
        return new ImmutablePersonSample(name, age);
    }
}
