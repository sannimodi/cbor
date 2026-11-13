namespace NCbor.Tests;

// ---------- Models ----------
// Every reference-type property has a non-null initializer, so a key that was written as CBOR
// null (property becomes null) and a key that was omitted (property keeps its initializer)
// deserialize differently.

public class NullRefInner
{
    public int X { get; set; }
}

public class NullRefModel
{
    public int Id { get; set; }
    public string Plain { get; set; } = "plain-default";
    public string? Annotated { get; set; } = "annotated-default";
    public int[]? Numbers { get; set; } = { -1 };
    public int[] NonAnnotatedNumbers { get; set; } = { -2 };
    public List<string>? Tags { get; set; } = new() { "default-tag" };
    public Dictionary<string, int>? Map { get; set; } = new() { ["default"] = -1 };
    public NullRefInner? Inner { get; set; } = new() { X = -1 };
    public NullRefInner NonAnnotatedInner { get; set; } = new() { X = -2 };
    public byte[]? Bytes { get; set; } = { 0xEE };
    public byte[] NonAnnotatedBytes { get; set; } = { 0xFF };
}

public record NullRefRecord(int Id, string? Name);

/// <summary>Goes through the constructor/initializer deserialization path (locals + has-flags).</summary>
public class NullRefInitModel
{
    public string? InitName { get; init; } = "init-default";
    public required string RequiredName { get; set; }
    public string SetterName { get; set; } = "setter-default";
}

public class NullRefWhenWritingNull
{
    [NCborIgnore(Condition = NCborIgnoreCondition.WhenWritingNull)]
    public string? Skipped { get; set; } = "skipped-default";

    [NCborIgnore(Condition = NCborIgnoreCondition.WhenWritingNull)]
    public NullRefInner? SkippedInner { get; set; } = new() { X = -1 };

    public string? Written { get; set; } = "written-default";
}

/// <summary>Encodes null as the text "<null>" so a test can tell the converter saw the null.</summary>
public sealed class NullSentinelStringConverter : INCborConverter<string?>
{
    public void Write(CborWriter writer, string? value) => writer.WriteTextString(value ?? "<null>");

    public string? Read(CborReader reader)
    {
        var text = reader.ReadTextString();
        return text == "<null>" ? null : text;
    }
}

public class NullRefConverterModel
{
    [NCborConverter(typeof(NullSentinelStringConverter))]
    public string? Name { get; set; } = "converter-default";
}

[NCborSerializable(typeof(NullRefModel))]
[NCborSerializable(typeof(NullRefRecord))]
[NCborSerializable(typeof(NullRefInitModel))]
[NCborSerializable(typeof(NullRefWhenWritingNull))]
[NCborSerializable(typeof(NullRefConverterModel))]
public partial class NullReferenceContext : NCborSerializerContext { }

// ---------- Tests ----------

/// <summary>
/// A null reference-type property is written as CBOR null and read back as null, whatever its
/// nullable annotation says (System.Text.Json parity). Previously a null string threw an
/// unwrapped ArgumentNullException from CborWriter.WriteTextString and a null array/collection/
/// POCO threw NullReferenceException inside the nested TypeInfo.
/// </summary>
public class NCborNullReferenceTests
{
    private static NullReferenceContext Context => NullReferenceContext.Default;

    private static NullRefModel AllNull() => new()
    {
        Id = 7,
        Plain = null!,
        Annotated = null,
        Numbers = null,
        NonAnnotatedNumbers = null!,
        Tags = null,
        Map = null,
        Inner = null,
        NonAnnotatedInner = null!,
        Bytes = null,
        NonAnnotatedBytes = null!,
    };

    /// <summary>Reads a top-level CBOR map into key → "is the value CBOR null".</summary>
    private static Dictionary<string, bool> ReadTopLevelKeys(byte[] bytes)
    {
        var reader = new CborReader(bytes);
        var keys = new Dictionary<string, bool>();
        reader.ReadStartMap();
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var key = reader.ReadTextString();
            keys[key] = reader.PeekState() == CborReaderState.Null;
            reader.SkipValue();
        }
        reader.ReadEndMap();
        return keys;
    }

    [Fact]
    public void AllReferenceProperties_Null_SerializeAsCborNull()
    {
        var bytes = NCborSerializer.Serialize(AllNull(), Context.NullRefModel);

        var keys = ReadTopLevelKeys(bytes);
        keys.Should().HaveCount(11);
        keys["Id"].Should().BeFalse();
        keys.Where(k => k.Key != "Id").Should().OnlyContain(k => k.Value, "every null reference property is written as CBOR null");
    }

    [Fact]
    public void AllReferenceProperties_Null_RoundTripAsNull()
    {
        var bytes = NCborSerializer.Serialize(AllNull(), Context.NullRefModel);
        var result = NCborSerializer.Deserialize(bytes, Context.NullRefModel);

        result.Id.Should().Be(7);
        result.Plain.Should().BeNull("a non-annotated string that was null at runtime round-trips as null");
        result.Annotated.Should().BeNull();
        result.Numbers.Should().BeNull();
        result.NonAnnotatedNumbers.Should().BeNull();
        result.Tags.Should().BeNull();
        result.Map.Should().BeNull();
        result.Inner.Should().BeNull();
        result.NonAnnotatedInner.Should().BeNull();
        result.Bytes.Should().BeNull();
        result.NonAnnotatedBytes.Should().BeNull();
    }

    [Theory]
    [InlineData(nameof(NullRefModel.Plain))]
    [InlineData(nameof(NullRefModel.Annotated))]
    [InlineData(nameof(NullRefModel.Numbers))]
    [InlineData(nameof(NullRefModel.NonAnnotatedNumbers))]
    [InlineData(nameof(NullRefModel.Tags))]
    [InlineData(nameof(NullRefModel.Map))]
    [InlineData(nameof(NullRefModel.Inner))]
    [InlineData(nameof(NullRefModel.NonAnnotatedInner))]
    [InlineData(nameof(NullRefModel.Bytes))]
    [InlineData(nameof(NullRefModel.NonAnnotatedBytes))]
    public void SingleNullProperty_DoesNotThrow_AndOnlyThatPropertyIsNull(string propertyName)
    {
        var model = NonNull();
        typeof(NullRefModel).GetProperty(propertyName)!.SetValue(model, null);

        var bytes = NCborSerializer.Serialize(model, Context.NullRefModel);
        var result = NCborSerializer.Deserialize(bytes, Context.NullRefModel);

        typeof(NullRefModel).GetProperty(propertyName)!.GetValue(result).Should().BeNull();
        foreach (var other in typeof(NullRefModel).GetProperties().Where(p => p.Name != propertyName))
            other.GetValue(result).Should().NotBeNull($"{other.Name} was not null");
    }

    private static NullRefModel NonNull() => new()
    {
        Id = 42,
        Plain = "plain",
        Annotated = "annotated",
        Numbers = new[] { 1, 2, 3 },
        NonAnnotatedNumbers = new[] { 4 },
        Tags = new() { "a", "b" },
        Map = new() { ["x"] = 1, ["y"] = 2 },
        Inner = new() { X = 5 },
        NonAnnotatedInner = new() { X = 6 },
        Bytes = new byte[] { 1, 2 },
        NonAnnotatedBytes = new byte[] { 3 },
    };

    [Fact]
    public void AllReferenceProperties_NonNull_RoundTrip()
    {
        var model = NonNull();
        var result = NCborSerializer.Deserialize(NCborSerializer.Serialize(model, Context.NullRefModel), Context.NullRefModel);

        result.Should().BeEquivalentTo(model);
    }

    [Fact]
    public void ExactCase_FromBugReport_NullStringAndNullIntArray()
    {
        var model = new NullRefModel { Plain = "x", Annotated = null, Numbers = null };
        var act = () => NCborSerializer.Serialize(model, Context.NullRefModel);
        act.Should().NotThrow();

        var result = NCborSerializer.Deserialize(act(), Context.NullRefModel);
        result.Plain.Should().Be("x");
        result.Annotated.Should().BeNull();
        result.Numbers.Should().BeNull();
    }

    [Fact]
    public void PositionalRecord_NullableStringParameter_RoundTripsNull()
    {
        var bytes = NCborSerializer.Serialize(new NullRefRecord(1, null), Context.NullRefRecord);

        ReadTopLevelKeys(bytes)["Name"].Should().BeTrue();
        NCborSerializer.Deserialize(bytes, Context.NullRefRecord).Should().Be(new NullRefRecord(1, null));
    }

    [Fact]
    public void PositionalRecord_NullableStringParameter_RoundTripsNonNull()
    {
        var bytes = NCborSerializer.Serialize(new NullRefRecord(2, "two"), Context.NullRefRecord);
        NCborSerializer.Deserialize(bytes, Context.NullRefRecord).Should().Be(new NullRefRecord(2, "two"));
    }

    [Fact]
    public void InitAndRequiredPath_ExplicitNull_OverridesInitializers()
    {
        var model = new NullRefInitModel { InitName = null, RequiredName = null!, SetterName = null! };
        var result = NCborSerializer.Deserialize(NCborSerializer.Serialize(model, Context.NullRefInitModel), Context.NullRefInitModel);

        result.InitName.Should().BeNull();
        result.RequiredName.Should().BeNull();
        result.SetterName.Should().BeNull("an explicit CBOR null is assigned, not treated as an omitted key");
    }

    [Fact]
    public void InitAndRequiredPath_NonNull_RoundTrips()
    {
        var model = new NullRefInitModel { InitName = "i", RequiredName = "r", SetterName = "s" };
        var result = NCborSerializer.Deserialize(NCborSerializer.Serialize(model, Context.NullRefInitModel), Context.NullRefInitModel);

        result.Should().BeEquivalentTo(model);
    }

    [Fact]
    public void WhenWritingNull_StillOmitsTheKey_WhileUnguardedNullIsWrittenAsCborNull()
    {
        var model = new NullRefWhenWritingNull { Skipped = null, SkippedInner = null, Written = null };
        var bytes = NCborSerializer.Serialize(model, Context.NullRefWhenWritingNull);

        var keys = ReadTopLevelKeys(bytes);
        keys.Should().NotContainKey("Skipped").And.NotContainKey("SkippedInner");
        keys.Should().ContainKey("Written").WhoseValue.Should().BeTrue();

        var result = NCborSerializer.Deserialize(bytes, Context.NullRefWhenWritingNull);
        result.Skipped.Should().Be("skipped-default", "an omitted key leaves the initializer in place");
        result.SkippedInner!.X.Should().Be(-1);
        result.Written.Should().BeNull("an explicit CBOR null is assigned");
    }

    [Fact]
    public void WhenWritingNull_NonNull_IsWrittenAndRoundTrips()
    {
        var model = new NullRefWhenWritingNull { Skipped = "s", SkippedInner = new() { X = 3 }, Written = "w" };
        var bytes = NCborSerializer.Serialize(model, Context.NullRefWhenWritingNull);

        ReadTopLevelKeys(bytes).Keys.Should().BeEquivalentTo(new[] { "Skipped", "SkippedInner", "Written" });
        NCborSerializer.Deserialize(bytes, Context.NullRefWhenWritingNull).Should().BeEquivalentTo(model);
    }

    [Fact]
    public void ConverterProperty_NullIsLeftToTheConverter()
    {
        var bytes = NCborSerializer.Serialize(new NullRefConverterModel { Name = null }, Context.NullRefConverterModel);

        var reader = new CborReader(bytes);
        reader.ReadStartMap();
        reader.ReadTextString().Should().Be("Name");
        reader.ReadTextString().Should().Be("<null>", "the converter, not the generator, decides how null is encoded");

        NCborSerializer.Deserialize(bytes, Context.NullRefConverterModel).Name.Should().BeNull();
    }
}
