// Runtime round-trips for names that used to break the generated code: global-namespace types
// named like a generated TypeInfo class, like a nested type of the context or like the context
// itself, and C# keywords used as property and type names. Generator-level coverage (including
// `T` under `Ctx<T>` and CBOR015) is in NCbor.Generator.Tests/IdentifierRobustnessGeneratorTests.cs.

// ---------- Global-namespace types ----------

// IdrFoo gets a generated nested `IdrFooTypeInfo` class; this one must stay the user's type.
public class IdrFooTypeInfo
{
    public int X { get; set; }
}

public class IdrFoo
{
    public int A { get; set; }
    public IdrFooTypeInfo? Info { get; set; }
}

// Same simple name as a type nested in the context below.
public class IdrItem
{
    public int A { get; set; }
}

// Same simple name as the context below.
public class IdentifierRobustnessContext
{
    public int Marker { get; set; }
}

namespace NCbor.Tests.IdentifierRobustness
{
    // ---------- Keyword-named types and properties ----------

    // A keyword type name is lower-case by necessity.
#pragma warning disable CS8981
    public class @event
    {
        public int @class { get; set; }
        public string? @params { get; set; }
    }

    public enum @enum { A = 1, B = 2 }
#pragma warning restore CS8981

    public class KeywordProps
    {
        public int @class { get; set; }
        public string? @params { get; set; }
        public List<int>? @event { get; set; }
        [NCborIgnore(Condition = NCborIgnoreCondition.WhenWritingNull)] public string? @null { get; set; }
        [NCborDefaultValue(7)] public int @int { get; set; }
        public int value { get; set; }
        public int var { get; set; }
        public @event? Nested { get; set; }
        public @enum Kind { get; set; }
        [NCborEnumAsString] public @enum? NamedKind { get; set; }
    }

    // Constructor arguments, an init-only and a required member (object initializer), and a
    // plain setter assigned after construction.
    public record KeywordRecord(int @class, string @event)
    {
        public int @fixed { get; init; }
        public required int @operator { get; set; }
        public int @this { get; set; }
    }

    [NCborSerializable(typeof(KeywordProps))]
    [NCborSerializable(typeof(KeywordRecord))]
    [NCborSerializable(typeof(@event))]
    [NCborSerializable(typeof(global::IdrFoo))]
    [NCborSerializable(typeof(global::IdrItem))]
    [NCborSerializable(typeof(IdentifierRobustnessContext.IdrItem))]
    [NCborSerializable(typeof(global::IdentifierRobustnessContext))]
    public partial class IdentifierRobustnessContext : NCborSerializerContext
    {
        public class IdrItem
        {
            public string? B { get; set; }
        }
    }

    public class NCborIdentifierRobustnessTests
    {
        private readonly IdentifierRobustnessContext _context = IdentifierRobustnessContext.Default;

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

        [Fact]
        public void KeywordNamedProperties_RoundTrip_UnderUnescapedWireKeys()
        {
            var model = new KeywordProps
            {
                @class = 1,
                @params = "p",
                @event = new List<int> { 2, 3 },
                @null = "n",
                @int = 8,
                value = 4,
                var = 5,
                Nested = new @event { @class = 6, @params = "q" },
                Kind = @enum.B,
                NamedKind = @enum.A,
            };

            var data = NCborSerializer.Serialize(model, _context.KeywordProps);

            ReadPropertyNames(data).Should().Equal("class", "params", "event", "null", "int", "value", "var", "Nested", "Kind", "NamedKind");
            NCborSerializer.Deserialize(data, _context.KeywordProps).Should().BeEquivalentTo(model);
        }

        [Fact]
        public void KeywordNamedProperties_WithWriteGuards_AreOmittedAndRestored()
        {
            var data = NCborSerializer.Serialize(new KeywordProps { @null = null, @int = 7 }, _context.KeywordProps);

            ReadPropertyNames(data).Should().NotContain("null").And.NotContain("int");
            NCborSerializer.Deserialize(data, _context.KeywordProps)!.@null.Should().BeNull();
        }

        [Fact]
        public void KeywordNamedRecordMembers_RoundTrip_ThroughConstructorInitializerAndSetter()
        {
            var model = new KeywordRecord(1, "e") { @fixed = 2, @operator = 3, @this = 4 };

            var data = NCborSerializer.Serialize(model, _context.KeywordRecord);

            ReadPropertyNames(data).Should().BeEquivalentTo("class", "event", "fixed", "operator", "this");
            NCborSerializer.Deserialize(data, _context.KeywordRecord).Should().Be(model);
        }

        [Fact]
        public void KeywordNamedType_HasAnEscapedContextProperty_AndRoundTrips()
        {
            var model = new @event { @class = 9, @params = "x" };

            var data = NCborSerializer.Serialize(model, _context.@event);

            ReadPropertyNames(data).Should().Equal("class", "params");
            NCborSerializer.Deserialize(data, _context.@event).Should().BeEquivalentTo(model);
        }

        [Fact]
        public void GlobalType_NamedLikeAGeneratedTypeInfoClass_RoundTrips()
        {
            var model = new IdrFoo { A = 1, Info = new IdrFooTypeInfo { X = 2 } };

            var data = NCborSerializer.Serialize(model, _context.GetTypeInfo<IdrFoo>());

            NCborSerializer.Deserialize(data, _context.GetTypeInfo<IdrFoo>()).Should().BeEquivalentTo(model);
        }

        [Fact]
        public void GlobalType_AndSameNamedNestedTypeOfTheContext_AreDistinct()
        {
            var global = _context.GetTypeInfo<global::IdrItem>();
            var nested = _context.GetTypeInfo<IdentifierRobustnessContext.IdrItem>();

            NCborSerializer.Deserialize(NCborSerializer.Serialize(new global::IdrItem { A = 3 }, global), global)!.A.Should().Be(3);
            NCborSerializer.Deserialize(NCborSerializer.Serialize(new IdentifierRobustnessContext.IdrItem { B = "b" }, nested), nested)!.B.Should().Be("b");
        }

        [Fact]
        public void ModelType_NamedLikeTheContext_RoundTrips()
        {
            var typeInfo = _context.GetTypeInfo<global::IdentifierRobustnessContext>();

            var data = NCborSerializer.Serialize(new global::IdentifierRobustnessContext { Marker = 11 }, typeInfo);

            ReadPropertyNames(data).Should().Equal("Marker");
            NCborSerializer.Deserialize(data, typeInfo)!.Marker.Should().Be(11);
        }
    }
}
