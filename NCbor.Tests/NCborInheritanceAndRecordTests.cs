namespace NCbor.Tests;

// ---------- Models: inheritance ----------

public class InheritanceBase
{
    public int Id { get; set; }
    public string BaseNote { get; set; } = "";
}

public class InheritanceDerived : InheritanceBase
{
    public string Name { get; set; } = "";
}

public class ChainLevel1
{
    public int A { get; set; }
}

public class ChainLevel2 : ChainLevel1
{
    public int B { get; set; }
}

public class ChainLevel3 : ChainLevel2
{
    public int C { get; set; }
}

public class HidingBase
{
    public int Value { get; set; }
    public int Other { get; set; }
}

public class HidingDerived : HidingBase
{
    // Hides the base int Value; `value.Value` on HidingDerived binds to this one.
    public new string Value { get; set; } = "";
}

public class VirtualBase
{
    public int Other { get; set; }
    public virtual string Label { get; set; } = "base";
}

public class VirtualOverride : VirtualBase
{
    public override string Label { get; set; } = "derived";
}

public class GetterOnlyOverride : VirtualBase
{
    // Declares only a getter; the setter is inherited from VirtualBase.Label.
    public override string Label => base.Label;
}

public class RenamedVirtualBase
{
    [NCborPropertyName("lbl")]
    public virtual string Label { get; set; } = "";
}

public class RenamedVirtualOverride : RenamedVirtualBase
{
    // No attribute here: attributes are read from the most-derived declaration only.
    public override string Label { get; set; } = "";
}

// ---------- Models: init / required / records / structs ----------

public class InitOnlyModel
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
}

public class RequiredModel
{
    public required string Name { get; set; }
    public required int Count { get; init; }
    public int? Optional { get; set; }
}

public record PositionalPerson(string Name, int Age);

public record PositionalWithBody(string Name, int Age)
{
    public string Note { get; set; } = "default-note";
    public List<string> Tags { get; set; } = new();
}

public record AnimalRecord(string Name);

public record DogRecord(string Name, string Breed) : AnimalRecord(Name);

public readonly record struct ReadonlyPoint(int X, int Y);

public struct MutablePoint
{
    public int X { get; set; }
    public int Y { get; set; }
}

public record DefaultValueRecord([property: NCborDefaultValue(5)] int Level, string Name)
{
    [NCborDefaultValue("fallback")]
    public string Mode { get; init; } = "fallback";
}

[NCborSerializable(typeof(InheritanceDerived))]
[NCborSerializable(typeof(ChainLevel3))]
[NCborSerializable(typeof(HidingDerived))]
[NCborSerializable(typeof(VirtualOverride))]
[NCborSerializable(typeof(GetterOnlyOverride))]
[NCborSerializable(typeof(RenamedVirtualOverride))]
[NCborSerializable(typeof(InitOnlyModel))]
[NCborSerializable(typeof(RequiredModel))]
[NCborSerializable(typeof(PositionalPerson))]
[NCborSerializable(typeof(PositionalWithBody))]
[NCborSerializable(typeof(DogRecord))]
[NCborSerializable(typeof(ReadonlyPoint))]
[NCborSerializable(typeof(MutablePoint))]
[NCborSerializable(typeof(DefaultValueRecord))]
// Nested collection types must be registered explicitly until type auto-discovery lands.
[NCborSerializable(typeof(List<string>))]
public partial class InheritanceAndRecordContext : NCborSerializerContext
{
}

public class NCborInheritanceAndRecordTests
{
    private readonly InheritanceAndRecordContext _context = InheritanceAndRecordContext.Default;

    private static List<string> ReadPropertyNames(byte[] data)
    {
        var reader = new CborReader(data);
        reader.ReadStartMap();
        var names = new List<string>();
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            names.Add(reader.ReadTextString());
            reader.SkipValue();
        }
        return names;
    }

    // ---------- Inheritance ----------

    [Fact]
    public void Derived_RoundTrip_IncludesBaseProperties()
    {
        var model = new InheritanceDerived { Id = 42, BaseNote = "from base", Name = "derived" };

        var data = NCborSerializer.Serialize(model, _context.InheritanceDerived);
        var result = NCborSerializer.Deserialize(data, _context.InheritanceDerived);

        result.Id.Should().Be(42);
        result.BaseNote.Should().Be("from base");
        result.Name.Should().Be("derived");
    }

    [Fact]
    public void Derived_WireKeys_AreBaseFirst()
    {
        var data = NCborSerializer.Serialize(new InheritanceDerived(), _context.InheritanceDerived);

        ReadPropertyNames(data).Should().Equal("Id", "BaseNote", "Name");
    }

    [Fact]
    public void ThreeLevelChain_RoundTrip_IncludesEveryLevel()
    {
        var model = new ChainLevel3 { A = 1, B = 2, C = 3 };

        var data = NCborSerializer.Serialize(model, _context.ChainLevel3);
        var result = NCborSerializer.Deserialize(data, _context.ChainLevel3);

        ReadPropertyNames(data).Should().Equal("A", "B", "C");
        result.A.Should().Be(1);
        result.B.Should().Be(2);
        result.C.Should().Be(3);
    }

    [Fact]
    public void NewHiddenProperty_SerializesOnlyTheDerivedMember_InTheBaseSlot()
    {
        var model = new HidingDerived { Value = "hidden-wins", Other = 7 };
        ((HidingBase)model).Value = 99;

        var data = NCborSerializer.Serialize(model, _context.HidingDerived);
        var result = NCborSerializer.Deserialize(data, _context.HidingDerived);

        ReadPropertyNames(data).Should().Equal("Value", "Other");
        result.Value.Should().Be("hidden-wins");
        result.Other.Should().Be(7);
        ((HidingBase)result).Value.Should().Be(0, "the hidden base member is not part of the derived type's map");
    }

    [Fact]
    public void VirtualOverride_HasOneWireKey_AndRoundTrips()
    {
        var model = new VirtualOverride { Label = "custom", Other = 3 };

        var data = NCborSerializer.Serialize(model, _context.VirtualOverride);
        var result = NCborSerializer.Deserialize(data, _context.VirtualOverride);

        ReadPropertyNames(data).Should().Equal("Other", "Label");
        result.Label.Should().Be("custom");
        result.Other.Should().Be(3);
    }

    [Fact]
    public void GetterOnlyOverride_UsesInheritedSetter_AndRoundTrips()
    {
        var model = new GetterOnlyOverride { Label = "via-base-setter", Other = 4 };

        var data = NCborSerializer.Serialize(model, _context.GetterOnlyOverride);
        var result = NCborSerializer.Deserialize(data, _context.GetterOnlyOverride);

        ReadPropertyNames(data).Should().Equal("Other", "Label");
        result.Label.Should().Be("via-base-setter");
    }

    [Fact]
    public void OverrideWithoutAttribute_DoesNotInheritBasePropertyName()
    {
        // Documented behavior: NCbor attributes are read from the most-derived declaration of an
        // overridden property only (re-declare them on the override to keep them).
        var data = NCborSerializer.Serialize(new RenamedVirtualOverride { Label = "x" }, _context.RenamedVirtualOverride);

        ReadPropertyNames(data).Should().Equal("Label");
    }

    // ---------- init / required ----------

    [Fact]
    public void InitOnlyProperties_RoundTrip()
    {
        var model = new InitOnlyModel { Id = 5, Name = "init" };

        var data = NCborSerializer.Serialize(model, _context.InitOnlyModel);
        var result = NCborSerializer.Deserialize(data, _context.InitOnlyModel);

        result.Id.Should().Be(5);
        result.Name.Should().Be("init");
    }

    [Fact]
    public void RequiredProperties_RoundTrip()
    {
        var model = new RequiredModel { Name = "req", Count = 9, Optional = 3 };

        var data = NCborSerializer.Serialize(model, _context.RequiredModel);
        var result = NCborSerializer.Deserialize(data, _context.RequiredModel);

        result.Name.Should().Be("req");
        result.Count.Should().Be(9);
        result.Optional.Should().Be(3);
    }

    // ---------- records ----------

    [Fact]
    public void PositionalRecord_RoundTrip()
    {
        var model = new PositionalPerson("Grace Hopper", 85);

        var data = NCborSerializer.Serialize(model, _context.PositionalPerson);
        var result = NCborSerializer.Deserialize(data, _context.PositionalPerson);

        result.Should().Be(model);
        ReadPropertyNames(data).Should().Equal("Name", "Age");
    }

    [Fact]
    public void PositionalRecordWithBodyProperties_RoundTrip()
    {
        var model = new PositionalWithBody("Ada", 36) { Note = "analyst", Tags = new List<string> { "a", "b" } };

        var data = NCborSerializer.Serialize(model, _context.PositionalWithBody);
        var result = NCborSerializer.Deserialize(data, _context.PositionalWithBody);

        result.Name.Should().Be("Ada");
        result.Age.Should().Be(36);
        result.Note.Should().Be("analyst");
        result.Tags.Should().Equal("a", "b");
    }

    [Fact]
    public void AbsentKey_PreservesSetterPropertyInitializer_OnConstructorPath()
    {
        // Hand-written payload with no "Note"/"Tags" keys (e.g. from an older writer): the
        // general construction path must leave the property initializers in place rather than
        // assigning default(T) over them.
        var writer = new CborWriter();
        writer.WriteStartMap(null);
        writer.WriteTextString("Name");
        writer.WriteTextString("Ada");
        writer.WriteTextString("Age");
        writer.WriteInt32(36);
        writer.WriteEndMap();

        var result = NCborSerializer.Deserialize(writer.Encode(), _context.PositionalWithBody);

        result.Name.Should().Be("Ada");
        result.Note.Should().Be("default-note");
        result.Tags.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void DerivedPositionalRecord_RoundTrip()
    {
        var model = new DogRecord("Rex", "Collie");

        var data = NCborSerializer.Serialize(model, _context.DogRecord);
        var result = NCborSerializer.Deserialize(data, _context.DogRecord);

        result.Should().Be(model);
        ReadPropertyNames(data).Should().Equal("Name", "Breed");
    }

    [Fact]
    public void ReadonlyRecordStruct_RoundTrip()
    {
        var model = new ReadonlyPoint(3, -4);

        var data = NCborSerializer.Serialize(model, _context.ReadonlyPoint);
        var result = NCborSerializer.Deserialize(data, _context.ReadonlyPoint);

        result.Should().Be(model);
    }

    [Fact]
    public void MutableStruct_RoundTrip()
    {
        var model = new MutablePoint { X = 10, Y = 20 };

        var data = NCborSerializer.Serialize(model, _context.MutablePoint);
        var result = NCborSerializer.Deserialize(data, _context.MutablePoint);

        result.X.Should().Be(10);
        result.Y.Should().Be(20);
    }

    // ---------- [NCborDefaultValue] on constructor / init members ----------

    [Fact]
    public void DefaultValue_OnConstructorAndInitMembers_IsRestoredWhenKeyOmitted()
    {
        // Both values equal their declared [NCborDefaultValue], so the writer omits their keys;
        // the reader has to seed the constructor argument / init value with that default rather
        // than default(T) (0 / null).
        var model = new DefaultValueRecord(5, "n");

        var data = NCborSerializer.Serialize(model, _context.DefaultValueRecord);
        var result = NCborSerializer.Deserialize(data, _context.DefaultValueRecord);

        ReadPropertyNames(data).Should().Equal("Name");
        result.Level.Should().Be(5);
        result.Mode.Should().Be("fallback");
        result.Name.Should().Be("n");
    }

    [Fact]
    public void DefaultValue_OnConstructorAndInitMembers_NonDefaultValuesRoundTrip()
    {
        var model = new DefaultValueRecord(8, "n") { Mode = "custom" };

        var data = NCborSerializer.Serialize(model, _context.DefaultValueRecord);
        var result = NCborSerializer.Deserialize(data, _context.DefaultValueRecord);

        result.Should().Be(model);
    }
}
