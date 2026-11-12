using Microsoft.CodeAnalysis;

namespace NCbor.Generator.Tests;

/// <summary>
/// Generator-level coverage for transitive type discovery, the collection-interface table,
/// unsupported-type reporting (CBOR009), disambiguation of colliding context-property names,
/// context property visibility, and registering built-in types directly on a context.
/// </summary>
public class DiscoveryAndCollectionGeneratorTests
{
    // ---------- Discovery ----------

    [Fact]
    public void UnregisteredNestedTypes_AreDiscovered_AndCompileCleanly()
    {
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using System;
            using System.Collections.Generic;
            using NCbor;

            namespace Models
            {
                public enum Kind { A, B }

                public class Child
                {
                    public string Name { get; set; } = "";
                    public Grandchild? Inner { get; set; }
                }

                public class Grandchild
                {
                    public int Value { get; set; }
                }

                public class Node
                {
                    public int Value { get; set; }
                    public Node? Next { get; set; }
                    public List<Node> Kids { get; set; } = new();
                }

                public class Root
                {
                    public List<List<int>> Grid { get; set; } = new();
                    public Dictionary<int, string> Names { get; set; } = new();
                    public Child Child { get; set; } = new();
                    public List<Child> Children { get; set; } = new();
                    public Child[] ChildArray { get; set; } = Array.Empty<Child>();
                    public Dictionary<string, List<Child>> ChildrenByKey { get; set; } = new();
                    public List<int?> MaybeInts { get; set; } = new();
                    public List<string?> MaybeStrings { get; set; } = new();
                    public List<Kind?> MaybeKinds { get; set; } = new();
                    public Dictionary<string, decimal?> MaybeDecimals { get; set; } = new();
                    public List<Child?> MaybeChildren { get; set; } = new();
                    public List<byte[]> Blobs { get; set; } = new();
                    public Node? Head { get; set; }
                }

                [NCborSerializable(typeof(Root))]
                public partial class RootContext : NCborSerializerContext { }
            }
            """);

        var text = result.AllGeneratedText;
        foreach (var property in new[]
                 {
                     "ListOfListOfInt32", "ListOfInt32", "DictionaryOfInt32AndString", "Child", "Grandchild",
                     "ListOfChild", "ArrayOfChild", "DictionaryOfStringAndListOfChild", "ListOfNullableOfInt32",
                     "ListOfString", "ListOfNullableOfKind", "DictionaryOfStringAndNullableOfDecimal",
                     "ListOfArrayOfByte", "ArrayOfByte", "Node", "ListOfNode",
                 })
        {
            text.Should().MatchRegex($@"public NCborTypeInfo<[^\n]*> {property} \{{ get; \}}",
                $"{property} should be discovered and, like every public model here, exposed as a public property");
        }

        // List<int?> must write the value or null, never dispatch to a NullableOfInt32 context property.
        text.Should().NotContain("_context.NullableOf");
        // Discovered types are reachable via GetTypeInfo<T>().
        text.Should().Contain("typeof(global::Models.Grandchild)");
        // Null-tolerant element code (shared by List<Child> and List<Child?>) stays warning-free.
        result.GeneratedCodeWarnings.Should().BeEmpty(result.FormatDiagnostics(result.GeneratedCodeWarnings));
    }

    [Fact]
    public void Discovery_RegisteredTypesComeFirst_InAttributeOrder_ThenDiscoveredInBfsOrder()
    {
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using System.Collections.Generic;
            using NCbor;

            namespace Models
            {
                public class Leaf { public int X { get; set; } }
                public class Mid { public Leaf Leaf { get; set; } = new(); }
                public class Top { public Mid Mid { get; set; } = new(); public List<int> Numbers { get; set; } = new(); }
                public class Second { public int Y { get; set; } }

                [NCborSerializable(typeof(Top))]
                [NCborSerializable(typeof(Second))]
                public partial class OrderContext : NCborSerializerContext { }
            }
            """);

        var text = result.AllGeneratedText;
        var order = new[] { "> Top {", "> Second {", "> Mid {", "> ListOfInt32 {", "> Leaf {" }
            .Select(marker => text.IndexOf(marker, StringComparison.Ordinal))
            .ToArray();
        order.Should().NotContain(-1, result.DumpGeneratedSources());
        order.Should().BeInAscendingOrder("registered types come first in attribute order, then discovered types in BFS order");
    }

    [Fact]
    public void DuplicateRegistration_OfADiscoveredType_IsDeduplicated()
    {
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using System.Collections.Generic;
            using NCbor;

            namespace Models
            {
                public class Holder { public List<string> Tags { get; set; } = new(); }

                [NCborSerializable(typeof(Holder))]
                [NCborSerializable(typeof(List<string>))]
                [NCborSerializable(typeof(List<string>))]
                public partial class DedupeContext : NCborSerializerContext { }
            }
            """);

        CountOccurrences(result.AllGeneratedText, "> ListOfString { get; }").Should().Be(1);
    }

    [Fact]
    public void CBOR005_Fires_ForDiscoveredNestedType()
    {
        // Diagnostics iterate the discovered closure, not only the registered types.
        var result = GeneratorHarness.Run("""
            using NCbor;

            namespace Models
            {
                public class Inner
                {
                    public int Id { get; set; }
                    public string Computed { get; } = "x";
                }

                public class Outer { public Inner Inner { get; set; } = new(); }

                [NCborSerializable(typeof(Outer))]
                public partial class OuterContext : NCborSerializerContext { }
            }
            """);

        result.DiagnosticsWithId("CBOR005").Should().ContainSingle(result.ToString())
            .Which.GetMessage().Should().Contain("Computed").And.Contain("Inner");
        result.CompilationErrors.Should().BeEmpty(result.ToString());
    }

    // ---------- Collection table ----------

    [Fact]
    public void AllCollectionInterfaces_CompileCleanly_WithoutRegistration()
    {
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using System.Collections.Generic;
            using NCbor;

            namespace Models
            {
                public class Collections
                {
                    public IList<int> AList { get; set; } = new List<int>();
                    public ICollection<string> ACollection { get; set; } = new List<string>();
                    public IReadOnlyList<int> ReadOnlyList { get; set; } = new List<int>();
                    public IReadOnlyCollection<string> ReadOnlyCollection { get; set; } = new List<string>();
                    public IEnumerable<int> Sequence { get; set; } = new List<int>();
                    public HashSet<string> Set { get; set; } = new();
                    public ISet<int> ISet { get; set; } = new HashSet<int>();
                    public IReadOnlySet<string> ReadOnlySet { get; set; } = new HashSet<string>();
                    public IDictionary<string, int> IDict { get; set; } = new Dictionary<string, int>();
                    public IReadOnlyDictionary<string, int> ReadOnlyDict { get; set; } = new Dictionary<string, int>();
                    public IReadOnlyList<int>? MaybeList { get; set; }
                }

                [NCborSerializable(typeof(Collections))]
                public partial class CollectionsContext : NCborSerializerContext { }
            }
            """);

        var text = result.AllGeneratedText;
        text.Should().Contain("new System.Collections.Generic.HashSet<string>()");
        text.Should().Contain("writer.WriteStartArray(null);", "IEnumerable<T> has no Count and is written as an indefinite-length array");
        result.GeneratorDiagnostics.Should().BeEmpty(result.ToString());
    }

    [Fact]
    public void HashSetAndSetInterfaces_DoNotGetCBOR005()
    {
        var result = GeneratorHarness.Run("""
            using System.Collections.Generic;
            using NCbor;

            namespace Models
            {
                public class Tagged { public HashSet<string> Tags { get; set; } = new(); }

                [NCborSerializable(typeof(Tagged))]
                [NCborSerializable(typeof(HashSet<int>))]
                [NCborSerializable(typeof(ISet<int>))]
                [NCborSerializable(typeof(IReadOnlySet<int>))]
                public partial class SetContext : NCborSerializerContext { }
            }
            """);

        result.AssertCompilesCleanly();
        result.DiagnosticsWithId("CBOR005").Should().BeEmpty(result.ToString());
    }

    [Fact]
    public void UserTypeNamedList_InAnotherNamespace_IsNotTreatedAsACollection()
    {
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using NCbor;

            namespace Models
            {
                public class List<T> { public int Marker { get; set; } }

                public class Holder { public List<int> Thing { get; set; } = new(); }

                [NCborSerializable(typeof(Holder))]
                public partial class FakeListContext : NCborSerializerContext { }
            }
            """);

        result.AllGeneratedText.Should().Contain("\"Marker\"", "Models.List<T> is a plain object, not System.Collections.Generic.List<T>");
    }

    // ---------- CBOR009: unsupported types ----------

    [Theory]
    [InlineData("System.TimeSpan")]
    [InlineData("System.TimeSpan?")]
    [InlineData("System.Uri")]
    [InlineData("System.DateOnly")]
    [InlineData("System.TimeOnly")]
    [InlineData("char")]
    [InlineData("object")]
    [InlineData("System.Action")]
    [InlineData("System.Func<int>")]
    [InlineData("(int, string)")]
    [InlineData("System.Tuple<int, string>")]
    [InlineData("int[,]")]
    [InlineData("Models.AbstractThing")]
    [InlineData("Models.IThing")]
    [InlineData("System.Collections.Generic.Queue<int>")]
    [InlineData("System.Collections.Generic.SortedSet<int>")]
    [InlineData("System.Collections.Generic.KeyValuePair<string, int>")]
    [InlineData("System.Collections.Generic.Stack<int>")]
    [InlineData("System.Collections.Generic.LinkedList<int>")]
    [InlineData("System.Collections.Concurrent.ConcurrentQueue<int>")]
    [InlineData("Models.StructPoco?")]
    [InlineData("dynamic")]
    [InlineData("System.Collections.Generic.List<dynamic>")]
    [InlineData("dynamic[]")]
    [InlineData("Models.Wrapper<dynamic>")]
    [InlineData("System.Collections.Generic.List<System.TimeSpan>")]
    [InlineData("System.Collections.Generic.Dictionary<string, object>")]
    public void UnsupportedPropertyType_ReportsCBOR009_WithNoCompilerErrors(string propertyType)
    {
        var result = GeneratorHarness.Run($$"""
            using System.Collections.Generic;
            using NCbor;

            namespace Models
            {
                public abstract class AbstractThing { public int X { get; set; } }
                public interface IThing { int X { get; } }
                public struct StructPoco { public int X { get; set; } }
                public class Wrapper<T> { public T Item { get; set; } = default!; }

                public class Holder
                {
                    public int Id { get; set; }
                    public {{propertyType}} Value { get; set; } = default!;
                }

                [NCborSerializable(typeof(Holder))]
                public partial class UnsupportedContext : NCborSerializerContext { }
            }
            """);

        result.GeneratorException.Should().BeNull();
        result.GeneratedSources.Should().NotBeEmpty();
        var cbor009 = result.DiagnosticsWithId("CBOR009");
        cbor009.Should().NotBeEmpty(result.ToString());
        cbor009.Should().OnlyContain(d => d.Severity == DiagnosticSeverity.Error);
        cbor009[0].GetMessage().Should().Contain("Holder.Value");
        // An unsupported type is reported once, as CBOR009 — never also CBOR005/006/008/010.
        result.GeneratorDiagnostics.Should().OnlyContain(d => d.Id == "CBOR009", result.ToString());
        // The stub TypeInfo keeps every reference site resolvable: CBOR009 is the only error.
        result.CompilationErrors.Should().BeEmpty(result.ToString() + "\n" + result.DumpGeneratedSources());
    }

    [Fact]
    public void UnsupportedRegisteredType_ReportsCBOR009_AtTheAttribute()
    {
        var result = GeneratorHarness.Run("""
            using NCbor;

            namespace Models
            {
                [NCborSerializable(typeof(System.TimeSpan))]
                public partial class TimeSpanContext : NCborSerializerContext { }
            }
            """);

        var cbor009 = result.DiagnosticsWithId("CBOR009");
        cbor009.Should().ContainSingle(result.ToString());
        cbor009[0].Location.SourceTree.Should().NotBeNull("the error points at the [NCborSerializable] attribute");
        result.CompilationErrors.Should().BeEmpty(result.ToString());
    }

    [Fact]
    public void ConverterCoveredTimeSpan_DoesNotReportCBOR009()
    {
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using System;
            using System.Formats.Cbor;
            using NCbor;

            namespace Models
            {
                public class TimeSpanTicksConverter : INCborConverter<TimeSpan>
                {
                    public void Write(CborWriter writer, TimeSpan value) => writer.WriteInt64(value.Ticks);
                    public TimeSpan Read(CborReader reader) => TimeSpan.FromTicks(reader.ReadInt64());
                }

                public class Timed
                {
                    [NCborConverter(typeof(TimeSpanTicksConverter))]
                    public TimeSpan Duration { get; set; }
                }

                [NCborSerializable(typeof(Timed))]
                public partial class TimedContext : NCborSerializerContext { }
            }
            """);

        result.DiagnosticsWithId("CBOR009").Should().BeEmpty();
        result.AllGeneratedText.Should().NotContain("TimeSpanTypeInfo");
    }

    [Fact]
    public void IgnoredUnsupportedProperty_DoesNotReportCBOR009()
    {
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using System;
            using NCbor;

            namespace Models
            {
                public class Holder
                {
                    public int Id { get; set; }
                    [NCborIgnore]
                    public Uri? Link { get; set; }
                }

                [NCborSerializable(typeof(Holder))]
                public partial class IgnoredContext : NCborSerializerContext { }
            }
            """);

        result.DiagnosticsWithId("CBOR009").Should().BeEmpty();
    }

    // ---------- Context property name collisions (auto-disambiguated; CBOR011 is retired) ----------

    [Fact]
    public void SameSimpleNameInTwoNamespaces_BothReferenced_AreDisambiguated_AndCompileCleanly()
    {
        // Both colliding types are reached through properties (and through List<T> / T[] element
        // types), so every reference site has to use the disambiguated name. This used to report
        // CBOR011, drop B.Item, and still reference it — CS1503/CS0029 on top of the error.
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using System.Collections.Generic;
            using NCbor;

            namespace A { public class Item { public int X { get; set; } } }
            namespace B { public class Item { public string Y { get; set; } = ""; } }

            namespace Models
            {
                public class Root
                {
                    public A.Item First { get; set; } = new();
                    public B.Item Second { get; set; } = new();
                    public List<A.Item> FirstList { get; set; } = new();
                    public List<B.Item> SecondList { get; set; } = new();
                    public A.Item[] FirstArray { get; set; } = new A.Item[0];
                    public B.Item[] SecondArray { get; set; } = new B.Item[0];
                    public Dictionary<string, B.Item> SecondByKey { get; set; } = new();
                }

                [NCborSerializable(typeof(Root))]
                public partial class CollisionContext : NCborSerializerContext { }
            }
            """);

        result.GeneratorDiagnostics.Should().BeEmpty(result.ToString());
        result.GeneratedCodeWarnings.Should().BeEmpty(result.ToString());
        var text = result.AllGeneratedText;
        text.Should().Contain("NCborTypeInfo<global::A.Item> Item { get; }", "the first claimant keeps the simple name");
        text.Should().Contain("NCborTypeInfo<global::B.Item> B_Item { get; }");
        text.Should().Contain("NCborTypeInfo<global::System.Collections.Generic.List<global::B.Item>> ListOfB_Item { get; }");
        text.Should().Contain("NCborTypeInfo<global::B.Item[]> ArrayOfB_Item { get; }");
        text.Should().Contain("_context.B_Item.Serialize(writer, value.Second, depth + 1);");
        text.Should().Contain("_context.B_Item.Deserialize(reader, depth + 1)");
        text.Should().Contain("Type t when t == typeof(global::B.Item) => (NCborTypeInfo<T>)(object)B_Item,");
        text.Should().Contain("private sealed class B_ItemTypeInfo : NCborTypeInfo<global::B.Item>");
    }

    [Fact]
    public void TypeNamedLikeAContextMember_IsDisambiguated_AndCompilesCleanly()
    {
        // `Default` (generated), `Equals` (inherited from object) and `Helper` (written by the
        // user in the context's own partial declaration) are all taken.
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using NCbor;

            namespace Models
            {
                public class Default { public int X { get; set; } }
                public class Equals { public int Y { get; set; } }
                public class Helper { public int Z { get; set; } }

                [NCborSerializable(typeof(Default))]
                [NCborSerializable(typeof(Equals))]
                [NCborSerializable(typeof(Helper))]
                public partial class ReservedContext : NCborSerializerContext
                {
                    public int Helper() => 0;
                }
            }
            """);

        result.GeneratorDiagnostics.Should().BeEmpty(result.ToString());
        result.GeneratedCodeWarnings.Should().BeEmpty(result.ToString());
        var text = result.AllGeneratedText;
        text.Should().Contain("NCborTypeInfo<global::Models.Default> Models_Default { get; }");
        text.Should().Contain("NCborTypeInfo<global::Models.Equals> Models_Equals { get; }");
        text.Should().Contain("NCborTypeInfo<global::Models.Helper> Models_Helper { get; }");
    }

    [Fact]
    public void UserTypesNamedLikeGeneratedNames_AreDisambiguated_AndCompileCleanly()
    {
        // A user class `Int32` next to a registered int, and a user class `ListOfString` next to
        // a List<string> property: same context property names, different types.
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using System.Collections.Generic;
            using NCbor;

            namespace Models
            {
                public class Int32 { public int A { get; set; } }
                public class ListOfString { public int B { get; set; } }

                public class Holder
                {
                    public List<string> Names { get; set; } = new();
                    public ListOfString Fake { get; set; } = new();
                    public Int32 Custom { get; set; } = new();
                }

                [NCborSerializable(typeof(int))]
                [NCborSerializable(typeof(Holder))]
                public partial class LookalikeContext : NCborSerializerContext { }
            }
            """);

        result.GeneratorDiagnostics.Should().BeEmpty(result.ToString());
        var text = result.AllGeneratedText;
        text.Should().Contain("NCborTypeInfo<int> Int32 { get; }");
        text.Should().Contain("NCborTypeInfo<global::Models.Int32> Models_Int32 { get; }");
        text.Should().Contain("NCborTypeInfo<global::System.Collections.Generic.List<string>> ListOfString { get; }");
        text.Should().Contain("NCborTypeInfo<global::Models.ListOfString> Models_ListOfString { get; }");
    }

    [Fact]
    public void TypesNestedInDifferentGenericInstantiations_AreDisambiguated_AndCompileCleanly()
    {
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using NCbor;

            namespace Models
            {
                public class Outer<T>
                {
                    public class Inner { public T Value { get; set; } = default!; }
                }

                public class Holder
                {
                    public Outer<int>.Inner Ints { get; set; } = new();
                    public Outer<string>.Inner Strings { get; set; } = new();
                }

                [NCborSerializable(typeof(Holder))]
                public partial class NestedGenericContext : NCborSerializerContext { }
            }
            """);

        result.GeneratorDiagnostics.Should().BeEmpty(result.ToString());
        var text = result.AllGeneratedText;
        text.Should().NotContain("InnerOf", "a type nested in a generic type has no type arguments of its own");
        text.Should().Contain("NCborTypeInfo<global::Models.Outer<int>.Inner> Inner { get; }");
        text.Should().Contain("NCborTypeInfo<global::Models.Outer<string>.Inner> OuterOfString_Inner { get; }");
    }

    // ---------- Visibility ----------

    [Fact]
    public void InternalModels_GetInternalContextProperties_NoCS0053()
    {
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using System.Collections.Generic;
            using NCbor;

            namespace Models
            {
                internal class InternalChild { public int X { get; set; } }

                public class Outer
                {
                    internal class NestedInternal { public int Y { get; set; } }
                }

                internal class InternalRoot
                {
                    public InternalChild Child { get; set; } = new();
                    public List<InternalChild> Children { get; set; } = new();
                    public List<Outer.NestedInternal> Nested { get; set; } = new();
                }

                public class PublicRoot { public int Z { get; set; } }

                [NCborSerializable(typeof(InternalRoot))]
                [NCborSerializable(typeof(PublicRoot))]
                public partial class VisibilityContext : NCborSerializerContext { }
            }
            """);

        var text = result.AllGeneratedText;
        text.Should().Contain("internal NCborTypeInfo<global::Models.InternalRoot> InternalRoot");
        text.Should().Contain("internal NCborTypeInfo<global::Models.InternalChild> InternalChild");
        text.Should().Contain("internal NCborTypeInfo<global::System.Collections.Generic.List<global::Models.InternalChild>> ListOfInternalChild");
        text.Should().Contain("internal NCborTypeInfo<global::Models.Outer.NestedInternal> NestedInternal");
        text.Should().Contain("public NCborTypeInfo<global::Models.PublicRoot> PublicRoot");
    }

    // ---------- Built-in registrations ----------

    [Fact]
    public void RegisteringBuiltInTypes_CompilesCleanly()
    {
        // Previously generated `(NCborTypeInfo<T>)(object)Int32` with no Int32 property (CS0119).
        // Generated properties named String/Guid/DateTime/Decimal must also not break the
        // context's own references to those type names (nullable helpers, POCO codegen).
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using System;
            using NCbor;

            namespace Models
            {
                public enum Color { Red, Green }

                public class AllNullable
                {
                    public int? I { get; set; }
                    public string? S { get; set; }
                    public Guid? G { get; set; }
                    public DateTime? D { get; set; }
                    public DateTimeOffset? O { get; set; }
                    public decimal? M { get; set; }
                    public Color? C { get; set; }
                    public Guid G2 { get; set; }
                    public DateTime D2 { get; set; }
                }

                [NCborSerializable(typeof(int))]
                [NCborSerializable(typeof(string))]
                [NCborSerializable(typeof(Guid))]
                [NCborSerializable(typeof(DateTime))]
                [NCborSerializable(typeof(DateTimeOffset))]
                [NCborSerializable(typeof(decimal))]
                [NCborSerializable(typeof(Color))]
                [NCborSerializable(typeof(int?))]
                [NCborSerializable(typeof(AllNullable))]
                public partial class PrimitiveContext : NCborSerializerContext { }
            }
            """);

        var text = result.AllGeneratedText;
        text.Should().Contain("public NCborTypeInfo<int> Int32 { get; }");
        text.Should().Contain("public NCborTypeInfo<int?> NullableOfInt32 { get; }");
        text.Should().Contain("Type t when t == typeof(int) => (NCborTypeInfo<T>)(object)Int32,");
        // Registered Nullable<int> must not be treated as a POCO (CBOR005 on HasValue/Value).
        result.GeneratorDiagnostics.Should().BeEmpty(result.ToString());
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
            count++;
        return count;
    }
}
