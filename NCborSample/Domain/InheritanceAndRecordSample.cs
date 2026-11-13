namespace NCborSample.Domain;

/// <summary>
/// Manually reproduces what NCbor.Generator emits for a derived class: properties from the
/// whole base chain are written base-first, in declaration order within each level, and a
/// property overridden or hidden (<c>new</c>) in a derived class appears once, in the base's slot.
/// Validated here before trusting the generator's own version.
/// </summary>
public class BaseEntitySample
{
    public int Id { get; set; }
    public virtual string Kind { get; set; } = "base";
}

public class DerivedEntitySample : BaseEntitySample
{
    public string Name { get; set; } = "";
    // Override declares only a getter; the setter is inherited from the base virtual and is
    // still assignable through the derived type.
    public override string Kind => base.Kind;

    public static void Serialize(CborWriter writer, DerivedEntitySample value)
    {
        writer.WriteStartMap(null);
        // Base level first ...
        writer.WriteTextString("Id");
        writer.WriteInt32(value.Id);
        writer.WriteTextString("Kind"); // overridden: one key, base slot
        writer.WriteTextString(value.Kind);
        // ... then the derived level.
        writer.WriteTextString("Name");
        writer.WriteTextString(value.Name);
        writer.WriteEndMap();
    }

    public static DerivedEntitySample Deserialize(CborReader reader)
    {
        // Plain settable POCO (no init/required/constructor): the existing new-then-set fast
        // path is unchanged — inherited setters are assigned exactly like declared ones.
        var result = new DerivedEntitySample();
        reader.ReadStartMap();
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var propertyName = reader.ReadTextString();
            switch (propertyName)
            {
                case "Id":
                    result.Id = reader.ReadInt32();
                    break;
                case "Kind":
                    result.Kind = reader.ReadTextString();
                    break;
                case "Name":
                    result.Name = reader.ReadTextString();
                    break;
                default:
                    reader.SkipValue();
                    break;
            }
        }
        reader.ReadEndMap();
        return result;
    }
}

/// <summary>
/// Manually reproduces the generator's "general path" for a positional record with an extra
/// settable body property and an init-only property: every value is read into a local first,
/// then the primary constructor and an object initializer (for init-only members) build the
/// instance, and plain setters are assigned only when their key was present, so a property
/// initializer survives a payload that omits the key.
/// </summary>
public record PositionalRecordSample(string Name, int Age)
{
    public string Note { get; set; } = "default-note";
    public string Tag { get; init; } = "";

    public static void Serialize(CborWriter writer, PositionalRecordSample value)
    {
        writer.WriteStartMap(null);
        writer.WriteTextString("Name");
        writer.WriteTextString(value.Name);
        writer.WriteTextString("Age");
        writer.WriteInt32(value.Age);
        writer.WriteTextString("Note");
        writer.WriteTextString(value.Note);
        writer.WriteTextString("Tag");
        writer.WriteTextString(value.Tag);
        writer.WriteEndMap();
    }

    public static PositionalRecordSample Deserialize(CborReader reader)
    {
        string __v_Name = default!;
        int __v_Age = default!;
        string __v_Note = default!;
        bool __h_Note = false;
        string __v_Tag = default!;
        reader.ReadStartMap();
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var propertyName = reader.ReadTextString();
            switch (propertyName)
            {
                case "Name":
                    __v_Name = reader.ReadTextString();
                    break;
                case "Age":
                    __v_Age = reader.ReadInt32();
                    break;
                case "Note":
                    __v_Note = reader.ReadTextString();
                    __h_Note = true;
                    break;
                case "Tag":
                    __v_Tag = reader.ReadTextString();
                    break;
                default:
                    reader.SkipValue();
                    break;
            }
        }
        reader.ReadEndMap();
        var result = new PositionalRecordSample(__v_Name, __v_Age) { Tag = __v_Tag };
        if (__h_Note) result.Note = __v_Note;
        return result;
    }
}
