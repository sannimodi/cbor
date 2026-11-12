using Microsoft.CodeAnalysis;

namespace NCbor.Generator.Tests;

/// <summary>
/// Generator-level coverage for inherited properties, init/required members, records/structs,
/// constructor auto-selection and CBOR010.
/// </summary>
public class ModelFidelityGeneratorTests
{
    private static string Wrap(string models, string registrations) => $$"""
        using System.Collections.Generic;
        using System.Diagnostics.CodeAnalysis;
        using NCbor;

        namespace Models
        {
        {{models}}

        {{registrations}}
            public partial class ShapeContext : NCborSerializerContext { }
        }
        """;

    public static TheoryData<string, string> CleanShapes => new()
    {
        {
            "derived class",
            Wrap("""
                public class Base { public int Id { get; set; } }
                public class Derived : Base { public string Name { get; set; } = ""; }
                """, "[NCborSerializable(typeof(Derived))]")
        },
        {
            "three-level chain",
            Wrap("""
                public class L1 { public int A { get; set; } }
                public class L2 : L1 { public int B { get; set; } }
                public class L3 : L2 { public int C { get; set; } }
                """, "[NCborSerializable(typeof(L3))]")
        },
        {
            "new-hidden property with a different type",
            Wrap("""
                public class Base { public int Value { get; set; } }
                public class Derived : Base { public new string Value { get; set; } = ""; }
                """, "[NCborSerializable(typeof(Derived))]")
        },
        {
            "virtual override and getter-only override",
            Wrap("""
                public class Base { public virtual string Label { get; set; } = ""; }
                public class Full : Base { public override string Label { get; set; } = ""; }
                public class GetterOnly : Base { public override string Label => base.Label; }
                """, "[NCborSerializable(typeof(Full))]\n[NCborSerializable(typeof(GetterOnly))]")
        },
        {
            "generic base class",
            Wrap("""
                public class Envelope<T> { public T Payload { get; set; } = default!; }
                public class IntEnvelope : Envelope<int> { public string Tag { get; set; } = ""; }
                """, "[NCborSerializable(typeof(IntEnvelope))]")
        },
        {
            "init-only properties",
            Wrap("""
                public class Settings { public int Id { get; init; } public string Name { get; init; } = ""; }
                """, "[NCborSerializable(typeof(Settings))]")
        },
        {
            // A required *field* is covered by CBOR014 in ModelFidelityRegressionGeneratorTests.
            "required members, including one on a base class",
            Wrap("""
                public class Base { public required string Id { get; set; } }
                public class Req : Base
                {
                    public required int Count { get; init; }
                    public int Plain { get; set; }
                }
                """, "[NCborSerializable(typeof(Req))]")
        },
        {
            "required member that is ignored",
            Wrap("""
                public class Req
                {
                    public required string Name { get; set; }
                    [NCborIgnore] public required string Secret { get; set; }
                }
                """, "[NCborSerializable(typeof(Req))]")
        },
        {
            "required members with a [SetsRequiredMembers] constructor",
            Wrap("""
                public class Req
                {
                    [SetsRequiredMembers]
                    public Req() { Name = ""; Secret = ""; }
                    public required string Name { get; set; }
                    [NCborIgnore] public required string Secret { get; set; }
                }
                """, "[NCborSerializable(typeof(Req))]")
        },
        {
            "positional record",
            Wrap("public record R(int A, string B);", "[NCborSerializable(typeof(R))]")
        },
        {
            "positional record with body properties",
            Wrap("""
                public record R(int A, string B)
                {
                    public string Note { get; set; } = "n";
                    public int Extra { get; init; }
                }
                """, "[NCborSerializable(typeof(R))]")
        },
        {
            "derived positional record",
            Wrap("""
                public record Animal(string Name);
                public record Dog(string Name, string Breed) : Animal(Name);
                """, "[NCborSerializable(typeof(Dog))]")
        },
        {
            "readonly record struct",
            Wrap("public readonly record struct P(int X, int Y);", "[NCborSerializable(typeof(P))]")
        },
        {
            "mutable struct",
            Wrap("public struct S { public int X { get; set; } public int Y { get; set; } }", "[NCborSerializable(typeof(S))]")
        },
        {
            "readonly struct with an explicit constructor",
            Wrap("""
                public readonly struct S
                {
                    public S(int x, int y) { X = x; Y = y; }
                    public int X { get; }
                    public int Y { get; }
                }
                """, "[NCborSerializable(typeof(S))]")
        },
        {
            "class with a primary constructor",
            Wrap("""
                public class Pc(int id, string name)
                {
                    public int Id { get; } = id;
                    public string Name { get; } = name;
                }
                """, "[NCborSerializable(typeof(Pc))]")
        },
        {
            "inaccessible members, an indexer and an explicit interface property are skipped",
            Wrap("""
                public interface IHasKey { int Key { get; } }
                public class M : IHasKey
                {
                    public int Id { get; set; }
                    internal int InternalValue { get; set; }
                    protected int ProtectedValue { get; set; }
                    private int PrivateValue { get; set; }
                    public int this[int i] => i;
                    int IHasKey.Key => Id;
                }
                """, "[NCborSerializable(typeof(M))]")
        },
    };

    [Theory]
    [MemberData(nameof(CleanShapes))]
    public void Shape_CompilesWithZeroDiagnostics(string shape, string source)
    {
        var result = GeneratorHarness.AssertCompilesCleanly(source);

        result.GeneratorDiagnostics.Should().BeEmpty($"{shape}: {result}");
        result.GeneratedCodeWarnings.Should().BeEmpty(
            $"{shape}: {result.FormatDiagnostics(result.GeneratedCodeWarnings)}\n{result.DumpGeneratedSources()}");
    }

    [Fact]
    public void Inheritance_BaseKeysComeFirst_AndOverrideAppearsOnce()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap("""
            public class Base { public int Id { get; set; } public virtual string Label { get; set; } = ""; }
            public class Derived : Base { public string Name { get; set; } = ""; public override string Label { get; set; } = ""; }
            """, "[NCborSerializable(typeof(Derived))]"));

        var text = result.AllGeneratedText;
        var id = text.IndexOf("writer.WriteTextString(\"Id\")", StringComparison.Ordinal);
        var label = text.IndexOf("writer.WriteTextString(\"Label\")", StringComparison.Ordinal);
        var name = text.IndexOf("writer.WriteTextString(\"Name\")", StringComparison.Ordinal);
        id.Should().BeGreaterThan(-1);
        label.Should().BeGreaterThan(id);
        name.Should().BeGreaterThan(label);
        text.Split("writer.WriteTextString(\"Label\")").Length.Should().Be(2, "an overridden property is one wire key");
    }

    [Fact]
    public void InaccessibleMembers_AreNotSerialized_InternalOnesAre()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap("""
            public class M
            {
                public int Id { get; set; }
                internal int InternalValue { get; set; }
                protected int ProtectedValue { get; set; }
                private int PrivateValue { get; set; }
            }
            """, "[NCborSerializable(typeof(M))]"));

        result.AllGeneratedText.Should().Contain("\"InternalValue\"");
        result.AllGeneratedText.Should().NotContain("\"ProtectedValue\"").And.NotContain("\"PrivateValue\"");
    }

    [Fact]
    public void FastPath_PlainSettablePoco_DeserializeBodyIsUnchanged()
    {
        // Pins the historical new-then-set output so the general construction path can't leak
        // into plain POCOs (and change generated code for every existing user). The deliberate
        // changes since: reference-type properties read CBOR null back as null (System.Text.Json
        // parity), so `Name` carries the null check; and type names are `global::`-qualified and locals
        // explicitly typed (a user type named `var` would otherwise hijack `var x = ...`).
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap("""
            public class Simple { public int Id { get; set; } public string Name { get; set; } = ""; }
            """, "[NCborSerializable(typeof(Simple))]"));

        var expected = """
            global::Models.Simple result = new global::Models.Simple();
            reader.ReadStartMap();
            while (reader.PeekState() != global::System.Formats.Cbor.CborReaderState.EndMap)
            {
                string propertyName = reader.ReadTextString();
                switch (propertyName)
                {
                    case "Id":
                        result.Id = reader.ReadInt32();
                        break;
                    case "Name":
                        result.Name = (reader.PeekState() == global::System.Formats.Cbor.CborReaderState.Null ? ReadNullValue<string>(reader)! : reader.ReadTextString());
                        break;
                    default:
                        reader.SkipValue();
                        break;
                }
            }
            reader.ReadEndMap();
            return result;
            """;
        result.AllGeneratedText.Replace("\r\n", "\n").Should().Contain(expected.Replace("\r\n", "\n"));
        result.AllGeneratedText.Should().NotContain("__v_").And.NotContain("__h_");
    }

    [Fact]
    public void GeneralPath_UsesObjectInitializerForInitAndConditionalAssignmentForSetters()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap("""
            public record R(int A)
            {
                public int Init { get; init; }
                public required int Req { get; set; }
                public int Plain { get; set; } = 7;
            }
            """, "[NCborSerializable(typeof(R))]"));

        var text = result.AllGeneratedText;
        text.Should().Contain("global::Models.R result = new global::Models.R(__v_A) { Init = __v_Init, Req = __v_Req };");
        text.Should().Contain("if (__h_Plain) result.Plain = __v_Plain;");
        text.Should().NotContain("__h_Init").And.NotContain("__h_A");
        // Required: a presence flag only to throw when the key is missing, never a post-assignment.
        text.Should().Contain("if (!__h_Req) throw new global::NCbor.NCborDeserializationException(").And.NotContain("result.Req =");
    }

    [Fact]
    public void ConstructorSelection_PrefersPrimaryConstructor_WhenSeveralMatch()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap("""
            public class P(int x, int y)
            {
                public P(int y) : this(0, y) { }
                public int X { get; } = x;
                public int Y { get; } = y;
            }
            """, "[NCborSerializable(typeof(P))]"));

        result.GeneratorDiagnostics.Should().BeEmpty(result.ToString());
        result.AllGeneratedText.Should().Contain("new global::Models.P(__v_X, __v_Y)");
    }

    [Fact]
    public void ConstructorSelection_StructPrefersExplicitMatchingConstructor_OverImplicitParameterless()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap(
            "public readonly record struct P(int X, int Y);", "[NCborSerializable(typeof(P))]"));

        result.AllGeneratedText.Should().Contain("new global::Models.P(__v_X, __v_Y)");
    }

    [Fact]
    public void ConstructorSelection_RequiresTypeMatch_NotJustName()
    {
        // The (string id) constructor matches Id by name only; with no other usable
        // constructor that's CBOR010, not a silently mistyped argument.
        var result = GeneratorHarness.Run(Wrap("""
            public class M
            {
                public M(string id) { Id = int.Parse(id); }
                public int Id { get; }
            }
            """, "[NCborSerializable(typeof(M))]"));

        result.DiagnosticsWithId("CBOR010").Should().ContainSingle(result.ToString());
    }

    // ---------- CBOR005 ----------

    [Theory]
    [InlineData("private set")]
    [InlineData("protected set")]
    [InlineData("private init")]
    public void CBOR005_Fires_ForPropertyWithInaccessibleSetter(string setter)
    {
        var result = GeneratorHarness.Run(Wrap($$"""
            public class Widget
            {
                public int Id { get; set; }
                public string Hidden { get; {{setter}}; } = "";
            }
            """, "[NCborSerializable(typeof(Widget))]"));

        result.AssertCompilesCleanly();
        var cbor005 = result.DiagnosticsWithId("CBOR005");
        cbor005.Should().ContainSingle(result.ToString());
        cbor005[0].GetMessage().Should().Contain("Hidden");
        result.AllGeneratedText.Should().NotContain("\"Hidden\"");
    }

    [Fact]
    public void CBOR005_Fires_ForGetOnlyPropertyDeclaredOnBaseClass()
    {
        var result = GeneratorHarness.Run(Wrap("""
            public class Base { public int Computed => 42; }
            public class Derived : Base { public int Id { get; set; } }
            """, "[NCborSerializable(typeof(Derived))]"));

        result.DiagnosticsWithId("CBOR005").Should().ContainSingle(result.ToString())
            .Which.GetMessage().Should().Contain("Computed");
    }

    [Fact]
    public void CBOR005_DoesNotFire_ForPositionalRecordOrRecordStruct()
    {
        var result = GeneratorHarness.Run(Wrap("""
            public record R(int A, string B);
            public readonly record struct P(int X, int Y);
            """, "[NCborSerializable(typeof(R))]\n[NCborSerializable(typeof(P))]"));

        result.DiagnosticsWithId("CBOR005").Should().BeEmpty(result.ToString());
    }

    // ---------- CBOR010 ----------

    [Fact]
    public void CBOR010_IsAnError_ForTwoNonMatchingConstructors_AndGeneratedCodeStillCompiles()
    {
        var result = GeneratorHarness.Run(Wrap("""
            public class Widget
            {
                public Widget(string label) { Name = label; }
                public Widget(int count) { Name = count.ToString(); }
                public string Name { get; set; }
            }
            """, "[NCborSerializable(typeof(Widget))]"));

        var cbor010 = result.DiagnosticsWithId("CBOR010");
        cbor010.Should().ContainSingle(result.ToString());
        cbor010[0].Severity.Should().Be(DiagnosticSeverity.Error);
        cbor010[0].GetMessage().Should().Contain("Widget");
        cbor010[0].Location.IsInSource.Should().BeTrue("the diagnostic points at the type");
        result.GeneratedCodeErrors.Should().BeEmpty("a throwing Deserialize stub is emitted instead of an uncompilable `new Widget()`");
        result.CompilationErrors.Should().BeEmpty();
        result.AllGeneratedText.Should().Contain("throw new global::NCbor.NCborDeserializationException(typeof(global::Models.Widget)");
    }

    [Fact]
    public void CBOR010_IsAnError_WhenSeveralConstructorsMatchAndNoneIsPrimary()
    {
        var result = GeneratorHarness.Run(Wrap("""
            public class M
            {
                public M(int a) { A = a; }
                public M(int a, int b) { A = a; B = b; }
                public int A { get; }
                public int B { get; }
            }
            """, "[NCborSerializable(typeof(M))]"));

        result.DiagnosticsWithId("CBOR010").Should().ContainSingle(result.ToString())
            .Which.GetMessage().Should().Contain("ambiguous");
        result.DiagnosticsWithId("CBOR005").Should().BeEmpty("CBOR010 already fails the build; per-property warnings would be noise");
        result.GeneratedCodeErrors.Should().BeEmpty();
    }

    [Fact]
    public void CBOR010_IsAnError_WhenOnlyConstructorIsPrivate()
    {
        var result = GeneratorHarness.Run(Wrap("""
            public class Singleton
            {
                private Singleton() { }
                public int Id { get; set; }
            }
            """, "[NCborSerializable(typeof(Singleton))]"));

        result.DiagnosticsWithId("CBOR010").Should().ContainSingle(result.ToString());
        result.GeneratedCodeErrors.Should().BeEmpty();
    }

    [Fact]
    public void CBOR010_DoesNotFire_ForAmbiguousNCborConstructor_WhichIsCBOR007()
    {
        var result = GeneratorHarness.Run(Wrap("""
            public class Widget
            {
                [NCborConstructor] public Widget(string name) { Name = name; }
                [NCborConstructor] public Widget(string name, int count) { Name = name; }
                public string Name { get; }
            }
            """, "[NCborSerializable(typeof(Widget))]"));

        result.DiagnosticsWithId("CBOR007").Should().ContainSingle();
        result.DiagnosticsWithId("CBOR010").Should().BeEmpty();
    }
}
