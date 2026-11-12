using Microsoft.CodeAnalysis;

namespace NCbor.Generator.Tests;

/// <summary>
/// Automated coverage for the generator's usage diagnostics CBOR005-CBOR008, which GAPS.md
/// previously recorded as "verified by manual build inspection" only. Assertions filter by
/// diagnostic ID and check severity plus the property/type names in the message, not exact
/// wording or locations.
/// </summary>
public class GeneratorDiagnosticTests
{
    // ---------- CBOR005: get-only property not covered by a constructor ----------

    [Fact]
    public void CBOR005_Fires_ForGetOnlyPropertyWithoutCoveringConstructor()
    {
        var result = GeneratorHarness.Run("""
            using NCbor;

            namespace Models
            {
                public class Widget
                {
                    public int Id { get; set; }
                    public string Computed { get; } = "x";
                }

                [NCborSerializable(typeof(Widget))]
                public partial class WidgetContext : NCborSerializerContext { }
            }
            """);

        var cbor005 = result.DiagnosticsWithId("CBOR005");
        cbor005.Should().ContainSingle(result.ToString());
        cbor005[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        var message = cbor005[0].GetMessage();
        message.Should().Contain("Computed").And.Contain("Widget");
        message.Should().NotContain("'Id'");
    }

    [Fact]
    public void CBOR005_DoesNotFire_WhenGetOnlyPropertyIsCoveredByNCborConstructor()
    {
        var result = GeneratorHarness.Run("""
            using NCbor;

            namespace Models
            {
                public class Widget
                {
                    [NCborConstructor]
                    public Widget(string name) { Name = name; }

                    public string Name { get; }
                    public int Count { get; set; }
                }

                [NCborSerializable(typeof(Widget))]
                public partial class WidgetContext : NCborSerializerContext { }
            }
            """);

        result.DiagnosticsWithId("CBOR005").Should().BeEmpty(result.ToString());
        result.GeneratedSources.Should().NotBeEmpty();
    }

    [Fact]
    public void CBOR005_DoesNotFire_ForListAndDictionaryRegistrations()
    {
        // Regression (GAPS.md GAP-14): CBOR005 must not misfire on List<T>/Dictionary<K,V>'s own
        // BCL get-only properties (Count, Keys, Values, ...). The plain model in the same context
        // is a positive control: it proves the check ran in this compilation.
        var result = GeneratorHarness.Run("""
            using System.Collections.Generic;
            using NCbor;

            namespace Models
            {
                public class Item
                {
                    public int Id { get; set; }
                    public string Label { get; } = "x";
                }

                [NCborSerializable(typeof(Item))]
                [NCborSerializable(typeof(List<Item>))]
                [NCborSerializable(typeof(List<int>))]
                [NCborSerializable(typeof(Dictionary<string, int>))]
                [NCborSerializable(typeof(Dictionary<string, Item>))]
                public partial class CollectionsContext : NCborSerializerContext { }
            }
            """);

        var cbor005 = result.DiagnosticsWithId("CBOR005");
        cbor005.Should().ContainSingle(result.ToString(), "only Item.Label is a real uncovered get-only property");
        cbor005[0].GetMessage().Should().Contain("Label").And.Contain("Item");
        result.GeneratedSources.Should().NotBeEmpty();
    }

    // ---------- CBOR006: [NCborConstructor] parameter with no matching property ----------

    [Fact]
    public void CBOR006_Fires_ForConstructorParameterWithNoMatchingProperty()
    {
        var result = GeneratorHarness.Run("""
            using NCbor;

            namespace Models
            {
                public class Account
                {
                    [NCborConstructor]
                    public Account(string name, int legacyCode) { Name = name; }

                    public string Name { get; }
                }

                [NCborSerializable(typeof(Account))]
                public partial class AccountContext : NCborSerializerContext { }
            }
            """);

        var cbor006 = result.DiagnosticsWithId("CBOR006");
        cbor006.Should().ContainSingle(result.ToString());
        cbor006[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        cbor006[0].GetMessage().Should().Contain("legacyCode").And.Contain("Account");
    }

    [Fact]
    public void CBOR006_DoesNotFire_WhenEveryParameterMatchesAProperty()
    {
        var result = GeneratorHarness.Run("""
            using NCbor;

            namespace Models
            {
                public class Account
                {
                    [NCborConstructor]
                    public Account(string name, int code) { Name = name; Code = code; }

                    public string Name { get; }
                    public int Code { get; }
                }

                [NCborSerializable(typeof(Account))]
                public partial class AccountContext : NCborSerializerContext { }
            }
            """);

        result.DiagnosticsWithId("CBOR006").Should().BeEmpty(result.ToString());
        result.GeneratedSources.Should().NotBeEmpty();
    }

    // ---------- CBOR007: more than one [NCborConstructor] ----------

    [Fact]
    public void CBOR007_IsAnError_ForTwoNCborConstructors()
    {
        var result = GeneratorHarness.Run("""
            using NCbor;

            namespace Models
            {
                public class Ambiguous
                {
                    [NCborConstructor]
                    public Ambiguous(int a) { A = a; }

                    [NCborConstructor]
                    public Ambiguous(int a, string b) { A = a; B = b; }

                    public int A { get; }
                    public string? B { get; }
                }

                [NCborSerializable(typeof(Ambiguous))]
                public partial class AmbiguousContext : NCborSerializerContext { }
            }
            """);

        var cbor007 = result.DiagnosticsWithId("CBOR007");
        cbor007.Should().ContainSingle(result.ToString());
        cbor007[0].Severity.Should().Be(DiagnosticSeverity.Error);
        cbor007[0].GetMessage().Should().Contain("Ambiguous");
    }

    [Fact]
    public void CBOR007_DoesNotFire_ForASingleNCborConstructor()
    {
        var result = GeneratorHarness.Run("""
            using NCbor;

            namespace Models
            {
                public class Single
                {
                    public Single() { }

                    [NCborConstructor]
                    public Single(int a) { A = a; }

                    public int A { get; }
                }

                [NCborSerializable(typeof(Single))]
                public partial class SingleContext : NCborSerializerContext { }
            }
            """);

        result.DiagnosticsWithId("CBOR007").Should().BeEmpty(result.ToString());
        result.GeneratedSources.Should().NotBeEmpty();
    }

    // ---------- CBOR008: [NCborEnumAsString] on a non-enum ----------

    [Fact]
    public void CBOR008_Fires_ForEnumAsStringOnNonEnumProperty()
    {
        var result = GeneratorHarness.Run("""
            using NCbor;

            namespace Models
            {
                public enum Color { Red, Green }

                public class Paint
                {
                    [NCborEnumAsString]
                    public Color Color { get; set; }

                    [NCborEnumAsString]
                    public Color? MaybeColor { get; set; }

                    [NCborEnumAsString]
                    public int NotAnEnum { get; set; }
                }

                [NCborSerializable(typeof(Paint))]
                public partial class PaintContext : NCborSerializerContext { }
            }
            """);

        var cbor008 = result.DiagnosticsWithId("CBOR008");
        cbor008.Should().ContainSingle(result.ToString(), "enum and nullable-enum properties are valid targets");
        cbor008[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        cbor008[0].GetMessage().Should().Contain("NotAnEnum").And.Contain("Paint");
    }
}
