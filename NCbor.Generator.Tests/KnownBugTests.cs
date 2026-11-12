namespace NCbor.Generator.Tests;

/// <summary>
/// Executable records of generator bugs present today. Each test asserts the CORRECT behavior,
/// is skipped with the failure it currently produces, and should be un-skipped by whoever fixes
/// the corresponding bug.
/// </summary>
public class KnownBugTests
{
    [Fact]
    public void Inheritance_BaseClassPropertiesAreSerialized()
    {
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using NCbor;

            namespace Models
            {
                public class BaseEntity
                {
                    public int BaseMarkerValue { get; set; }
                }

                public class DerivedEntity : BaseEntity
                {
                    public string DerivedName { get; set; } = "";
                }

                [NCborSerializable(typeof(DerivedEntity))]
                public partial class InheritanceContext : NCborSerializerContext { }
            }
            """);

        result.AllGeneratedText.Should().Contain("\"DerivedName\"");
        result.AllGeneratedText.Should().Contain("\"BaseMarkerValue\"",
            "properties declared on a base class must be part of the derived type's CBOR map");
    }

    [Fact]
    public void PositionalRecord_CompilesCleanly()
    {
        GeneratorHarness.AssertCompilesCleanly("""
            using NCbor;

            namespace Models
            {
                public record R(int A, string B);

                [NCborSerializable(typeof(R))]
                public partial class RecordContext : NCborSerializerContext { }
            }
            """);
    }

    [Fact]
    public void InitOnlyProperty_CompilesCleanly()
    {
        GeneratorHarness.AssertCompilesCleanly("""
            using NCbor;

            namespace Models
            {
                public class Settings
                {
                    public int Id { get; init; }
                    public string Name { get; init; } = "";
                }

                [NCborSerializable(typeof(Settings))]
                public partial class SettingsContext : NCborSerializerContext { }
            }
            """);
    }

    [Fact(Skip = "Known bug: collection-typed properties dispatch to a context property (ListOfListOfInt32) that only exists if that exact collection type is registered on the context (CS1061).")]
    public void NestedListProperty_WithoutRegisteringNestedTypes_CompilesCleanly()
    {
        GeneratorHarness.AssertCompilesCleanly("""
            using System.Collections.Generic;
            using NCbor;

            namespace Models
            {
                public class Grid
                {
                    public List<List<int>> Cells { get; set; } = new();
                }

                [NCborSerializable(typeof(Grid))]
                public partial class GridContext : NCborSerializerContext { }
            }
            """);
    }

    [Fact(Skip = "Known bug: IReadOnlyList<T> is not recognized as a collection; even when registered, its TypeInfo is generated as a plain object and does `new IReadOnlyList<int>()` (CS0144).")]
    public void IReadOnlyListProperty_CompilesCleanly()
    {
        // IReadOnlyList<int> is registered explicitly so this isolates the interface-collection
        // bug from the separate "collection types must be registered" issue covered above.
        GeneratorHarness.AssertCompilesCleanly("""
            using System.Collections.Generic;
            using NCbor;

            namespace Models
            {
                public class Series
                {
                    public IReadOnlyList<int> Values { get; set; } = new List<int>();
                }

                [NCborSerializable(typeof(Series))]
                [NCborSerializable(typeof(IReadOnlyList<int>))]
                public partial class SeriesContext : NCborSerializerContext { }
            }
            """);
    }

    [Fact]
    public void Record_DoesNotGetCBOR005ForEqualityContract()
    {
        var result = GeneratorHarness.Run("""
            using NCbor;

            namespace Models
            {
                public record Point
                {
                    public int X { get; set; }
                    public int Y { get; set; }
                }

                [NCborSerializable(typeof(Point))]
                public partial class PointContext : NCborSerializerContext { }
            }
            """);

        result.GeneratedSources.Should().NotBeEmpty();
        result.DiagnosticsWithId("CBOR005").Should().NotContain(
            d => d.GetMessage().Contains("EqualityContract"),
            "the compiler-synthesized EqualityContract is not user data");
    }
}
