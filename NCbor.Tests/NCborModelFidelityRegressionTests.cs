using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace NCbor.Tests.ModelFidelityRegression;

// Runtime regression tests for the wave-2 model-fidelity review (items 1, 3, 5, 6, 7, 9, 12).
// The CBOR014 (required field) case lives in NCbor.Generator.Tests, not here, so the solution
// build stays warning-free.

// ---------- Item 1: framework base classes are not walked ----------

public class C : Component
{
    public int Code { get; set; }
}

public class MyErr : Exception
{
    public int Code { get; set; }
}

public class Attr : Attribute
{
    public int Code { get; set; }
}

// ---------- Item 3: a `new static` property hides an instance one ----------

public class StatBase
{
    public int Id { get; set; }
}

public class Stat : StatBase
{
    public static new int Id { get; set; }
    public string Name { get; set; } = "";
}

// ---------- Item 5: constructor parameter defaults, required members ----------

public record Opt(int A = 42, string S = "def");

public enum OptColor { Red = 1, Green = 2, Blue = 3 }

public record OptMixed(OptColor Color = OptColor.Blue, Guid G = default, int? N = null, double D = 1.5, string? Text = null);

// The ignored property leaves `Ig` with no serialized property (CBOR006, intentional here).
#pragma warning disable CBOR006
public record OptIgnored([property: NCborIgnore] int Ig = 3, int B = 0);
#pragma warning restore CBOR006

// An ignore condition can omit the key on write; an absent key then means default(T), not 42.
public record OptGuarded([property: NCborIgnore(Condition = NCborIgnoreCondition.WhenWritingDefault)] int A = 42);

public class Req
{
    public required string Name { get; set; }
    public int X { get; set; }
}

public class ReqGuarded
{
    // WhenWritingNull omits a null Note, so the key may be absent without the payload being wrong.
    [NCborIgnore(Condition = NCborIgnoreCondition.WhenWritingNull)]
    public required string? Note { get; set; }

    [NCborDefaultValue(5)]
    public required int Level { get; set; }
}

// ---------- Item 6: [SetsRequiredMembers] constructor values survive ----------

public class Req2
{
    [SetsRequiredMembers]
    public Req2()
    {
        X = 1;
        S = "ctor";
    }

    public required int X { get; set; }
    public required string S { get; set; }
}

// ---------- Item 7: struct with a validating constructor but all-settable properties ----------

public struct SV
{
    public SV(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        B = 0;
    }

    public string Name { get; set; }
    public int B { get; set; }
}

// ---------- Item 9: property names differing only by case ----------

// `Value` has no setter and no exactly-matching parameter (CBOR005, intentional here).
#pragma warning disable CBOR005
public class CaseClash
{
    public CaseClash(int value)
    {
        this.value = value;
    }

    public int Value { get; }

#pragma warning disable IDE1006 // lower-case property name is the point of the repro
    public int value { get; set; }
#pragma warning restore IDE1006
}
#pragma warning restore CBOR005

// ---------- Item 12: overrides inherit NCbor attributes from the base declaration ----------

public class AttrBase
{
    [NCborIgnore]
    public virtual string Secret { get; set; } = "";

    [NCborPropertyName("n")]
    public virtual string Name { get; set; } = "";

    [NCborDefaultValue(5)]
    public virtual int Level { get; set; } = 5;
}

public class AttrDerived : AttrBase
{
    public override string Secret { get; set; } = "";
    public override string Name { get; set; } = "";
    public override int Level { get; set; } = 5;
}

public class AttrRedeclared : AttrBase
{
    // The most-derived declaration wins, per attribute type.
    [NCborPropertyName("m")]
    public override string Name { get; set; } = "";

    [NCborIgnore(Condition = NCborIgnoreCondition.Never)]
    public override string Secret { get; set; } = "";
}

[NCborSerializable(typeof(C))]
[NCborSerializable(typeof(MyErr))]
[NCborSerializable(typeof(Attr))]
[NCborSerializable(typeof(Stat))]
[NCborSerializable(typeof(Opt))]
[NCborSerializable(typeof(OptMixed))]
[NCborSerializable(typeof(OptIgnored))]
[NCborSerializable(typeof(OptGuarded))]
[NCborSerializable(typeof(Req))]
[NCborSerializable(typeof(ReqGuarded))]
[NCborSerializable(typeof(Req2))]
[NCborSerializable(typeof(SV))]
[NCborSerializable(typeof(CaseClash))]
[NCborSerializable(typeof(AttrBase))]
[NCborSerializable(typeof(AttrDerived))]
[NCborSerializable(typeof(AttrRedeclared))]
public partial class ModelFidelityRegressionContext : NCborSerializerContext
{
}

public class NCborModelFidelityRegressionTests
{
    private readonly ModelFidelityRegressionContext _context = ModelFidelityRegressionContext.Default;

    private static readonly byte[] EmptyMap = WriteMap();

    private static byte[] WriteMap(params (string Key, Action<CborWriter> WriteValue)[] entries)
    {
        var writer = new CborWriter();
        writer.WriteStartMap(entries.Length);
        foreach (var (key, writeValue) in entries)
        {
            writer.WriteTextString(key);
            writeValue(writer);
        }
        writer.WriteEndMap();
        return writer.Encode();
    }

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

    // ---------- Item 1 ----------

    [Fact]
    public void ComponentSubclass_SerializesOnlyItsOwnProperties()
    {
        var data = NCborSerializer.Serialize(new C { Code = 7 }, _context.C);

        ReadPropertyNames(data).Should().Equal("Code");
        NCborSerializer.Deserialize(data, _context.C).Code.Should().Be(7);
    }

    [Fact]
    public void ExceptionSubclass_SerializesOnlyItsOwnProperties()
    {
        var data = NCborSerializer.Serialize(new MyErr { Code = 42 }, _context.MyErr);

        ReadPropertyNames(data).Should().Equal("Code");
        NCborSerializer.Deserialize(data, _context.MyErr).Code.Should().Be(42);
    }

    [Fact]
    public void AttributeSubclass_SerializesOnlyItsOwnProperties()
    {
        var data = NCborSerializer.Serialize(new Attr { Code = 3 }, _context.Attr);

        ReadPropertyNames(data).Should().Equal("Code");
        NCborSerializer.Deserialize(data, _context.Attr).Code.Should().Be(3);
    }

    // ---------- Item 3 ----------

    [Fact]
    public void NewStaticProperty_HidesInheritedInstanceProperty()
    {
        var model = new Stat { Name = "n" };
        ((StatBase)model).Id = 5;

        var data = NCborSerializer.Serialize(model, _context.Stat);

        ReadPropertyNames(data).Should().Equal("Name");
        NCborSerializer.Deserialize(data, _context.Stat).Name.Should().Be("n");
    }

    // ---------- Item 5 ----------

    [Fact]
    public void AbsentKeys_UseConstructorParameterDefaults()
    {
        var result = NCborSerializer.Deserialize(EmptyMap, _context.Opt);

        result.A.Should().Be(42);
        result.S.Should().Be("def");
    }

    [Fact]
    public void PresentKeys_OverrideConstructorParameterDefaults()
    {
        var data = NCborSerializer.Serialize(new Opt(1, "x"), _context.Opt);

        NCborSerializer.Deserialize(data, _context.Opt).Should().Be(new Opt(1, "x"));
    }

    [Fact]
    public void AbsentKeys_UseEnumGuidNullableAndDoubleParameterDefaults()
    {
        var result = NCborSerializer.Deserialize(EmptyMap, _context.OptMixed);

        result.Should().Be(new OptMixed(OptColor.Blue, Guid.Empty, null, 1.5, null));
    }

    [Fact]
    public void UnmatchedParameter_UsesItsDefault()
    {
        var data = NCborSerializer.Serialize(new OptIgnored(9, 4), _context.OptIgnored);

        ReadPropertyNames(data).Should().Equal("B");
        var result = NCborSerializer.Deserialize(data, _context.OptIgnored);
        result.Ig.Should().Be(3, "the ignored property's parameter falls back to its declared default");
        result.B.Should().Be(4);
    }

    [Fact]
    public void ParameterDefault_IsNotApplied_WhenAnIgnoreConditionOmittedTheKey()
    {
        var data = NCborSerializer.Serialize(new OptGuarded(0), _context.OptGuarded);

        ReadPropertyNames(data).Should().BeEmpty();
        NCborSerializer.Deserialize(data, _context.OptGuarded).A.Should().Be(0);
    }

    [Fact]
    public void MissingRequiredMember_Throws()
    {
        var data = WriteMap(("X", w => w.WriteInt32(1)));

        var act = () => NCborSerializer.Deserialize(data, _context.Req);

        act.Should().Throw<NCborDeserializationException>().WithMessage("*Name*");
    }

    [Fact]
    public void RequiredMember_RoundTrips()
    {
        var data = NCborSerializer.Serialize(new Req { Name = "a", X = 2 }, _context.Req);

        var result = NCborSerializer.Deserialize(data, _context.Req);
        result.Name.Should().Be("a");
        result.X.Should().Be(2);
    }

    [Fact]
    public void RequiredMember_WhoseKeyNCborMayOmit_DoesNotThrowWhenAbsent()
    {
        var data = NCborSerializer.Serialize(new ReqGuarded { Note = null, Level = 5 }, _context.ReqGuarded);

        ReadPropertyNames(data).Should().BeEmpty();
        var result = NCborSerializer.Deserialize(data, _context.ReqGuarded);
        result.Note.Should().BeNull();
        result.Level.Should().Be(5);
    }

    // ---------- Item 6 ----------

    [Fact]
    public void SetsRequiredMembersConstructor_ValuesSurviveAbsentKeys()
    {
        var result = NCborSerializer.Deserialize(EmptyMap, _context.Req2);

        result.X.Should().Be(1);
        result.S.Should().Be("ctor");
    }

    [Fact]
    public void SetsRequiredMembersConstructor_PresentKeysStillApply()
    {
        var result = NCborSerializer.Deserialize(WriteMap(("X", w => w.WriteInt32(5))), _context.Req2);

        result.X.Should().Be(5);
        result.S.Should().Be("ctor");
    }

    // ---------- Item 7 ----------

    [Fact]
    public void Struct_WithAllSettableProperties_UsesParameterlessConstruction()
    {
        // The (string name) constructor throws on null; with Name absent it must not be called.
        var result = NCborSerializer.Deserialize(WriteMap(("B", w => w.WriteInt32(3))), _context.SV);

        result.B.Should().Be(3);
        result.Name.Should().BeNull();
    }

    [Fact]
    public void Struct_WithAllSettableProperties_RoundTrips()
    {
        var data = NCborSerializer.Serialize(new SV("x") { B = 2 }, _context.SV);

        var result = NCborSerializer.Deserialize(data, _context.SV);
        result.Name.Should().Be("x");
        result.B.Should().Be(2);
    }

    // ---------- Item 9 ----------

    [Fact]
    public void ConstructorParameter_MatchesExactCasePropertyFirst()
    {
        var data = NCborSerializer.Serialize(new CaseClash(5), _context.CaseClash);

        ReadPropertyNames(data).Should().Equal("value");
        NCborSerializer.Deserialize(data, _context.CaseClash).value.Should().Be(5);
    }

    // ---------- Item 12 ----------

    [Fact]
    public void Override_InheritsIgnorePropertyNameAndDefaultValueFromBase()
    {
        var model = new AttrDerived { Secret = "s", Name = "x", Level = 5 };

        var data = NCborSerializer.Serialize(model, _context.AttrDerived);

        ReadPropertyNames(data).Should().Equal("n");
        var result = NCborSerializer.Deserialize(data, _context.AttrDerived);
        result.Name.Should().Be("x");
        result.Secret.Should().BeEmpty();
        result.Level.Should().Be(5);
    }

    [Fact]
    public void BaseAndDerived_AgreeOnTheWire()
    {
        var baseData = NCborSerializer.Serialize(new AttrBase { Secret = "s", Name = "x", Level = 9 }, _context.AttrBase);
        var derivedData = NCborSerializer.Serialize(new AttrDerived { Secret = "s", Name = "x", Level = 9 }, _context.AttrDerived);

        derivedData.Should().Equal(baseData);
        ReadPropertyNames(derivedData).Should().Equal("n", "Level");
    }

    [Fact]
    public void Override_RedeclaringAnAttribute_WinsOverTheBase()
    {
        var model = new AttrRedeclared { Secret = "s", Name = "x", Level = 5 };

        var data = NCborSerializer.Serialize(model, _context.AttrRedeclared);

        // Secret: [NCborIgnore(Never)] overrides the base's Always; Name: "m" overrides "n";
        // Level still inherits [NCborDefaultValue(5)].
        ReadPropertyNames(data).Should().Equal("Secret", "m");
        var result = NCborSerializer.Deserialize(data, _context.AttrRedeclared);
        result.Secret.Should().Be("s");
        result.Name.Should().Be("x");
        result.Level.Should().Be(5);
    }
}
