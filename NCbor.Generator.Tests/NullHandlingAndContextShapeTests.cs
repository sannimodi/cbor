using Microsoft.CodeAnalysis;

namespace NCbor.Generator.Tests;

/// <summary>
/// Null reference-type properties (generated code must null-check them: no CS8604), and the
/// context-level generator fixes: collision-proof hint names, CBOR007's location,
/// <see cref="EquatableArray{T}"/>'s hash/equals contract, nested contexts (CBOR012 when a
/// containing type isn't partial) and preserved context accessibility.
/// </summary>
public class NullHandlingAndContextShapeTests
{
    // ---------- Null reference-type properties ----------

    [Fact]
    public void NullableAndNonNullableReferenceProperties_GenerateNoCS8604()
    {
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using System.Collections.Generic;
            using NCbor;

            namespace Models
            {
                public class Inner { public int X { get; set; } }

                public class Holder
                {
                    public string Plain { get; set; } = "";
                    public string? Name { get; set; }
                    public int[]? Nums { get; set; }
                    public int[] NonAnnotatedNums { get; set; } = new int[0];
                    public List<string>? Tags { get; set; }
                    public Dictionary<string, int>? Map { get; set; }
                    public Inner? Child { get; set; }
                    public Inner NonAnnotatedChild { get; set; } = new();
                    public byte[]? Bytes { get; set; }
                    public byte[] NonAnnotatedBytes { get; set; } = new byte[0];
                    public string? InitName { get; init; }
                }

                public record Rec(int Id, string? Name, Inner? Child);

                [NCborSerializable(typeof(Holder))]
                [NCborSerializable(typeof(Rec))]
                public partial class NullContext : NCborSerializerContext { }
            }
            """);

        result.GeneratedCodeWarnings.Where(d => d.Id == "CS8604").Should().BeEmpty(result.FormatDiagnostics(result.GeneratedCodeWarnings));
        result.GeneratedCodeWarnings.Should().BeEmpty(result.FormatDiagnostics(result.GeneratedCodeWarnings));
        // The T[]? property no longer bypasses the null check by taking the plain array branch.
        result.AllGeneratedText.Should().Contain("if (value.Nums is null) { writer.WriteNull(); }");
        result.AllGeneratedText.Should().Contain("if (value.Plain is null) { writer.WriteNull(); }");
    }

    // ---------- Hint names ----------

    [Fact]
    public void HintNames_GenericContextAndItsSanitizedLookalike_DoNotCollide()
    {
        var result = GeneratorHarness.Run("""
            using NCbor;

            namespace N1
            {
                public class M { public int X { get; set; } }

                [NCborSerializable(typeof(M))]
                public partial class Ctx<T> : NCborSerializerContext { }

                [NCborSerializable(typeof(M))]
                public partial class Ctx_T_ : NCborSerializerContext { }
            }
            """);

        result.GeneratorException.Should().BeNull();
        result.CompilationDiagnostics.Where(d => d.Id == "CS8785").Should().BeEmpty();
        result.GeneratedSources.Should().HaveCount(2);
        result.GeneratedSources.Keys.Should().OnlyHaveUniqueItems();
        result.GeneratedSources.Keys.Should().Contain("N1.Ctx_T_.g.cs", "a name that sanitizes losslessly stays readable");
        result.GeneratedSources.Keys.Should().ContainSingle(k => k.StartsWith("N1.Ctx_T_.", StringComparison.Ordinal) && k != "N1.Ctx_T_.g.cs");
        // The generic context compiles too: its type parameters are emitted, and the CS0693 its
        // `T` causes against GetTypeInfo<T> is suppressed in generated code.
        result.AssertCompilesCleanly();
        result.GeneratedCodeWarnings.Should().BeEmpty(result.FormatDiagnostics(result.GeneratedCodeWarnings));
    }

    [Fact]
    public void HintNames_ContextsDifferingOnlyByCase_DoNotCollide()
    {
        var result = GeneratorHarness.Run("""
            using NCbor;

            namespace N1
            {
                public class M { public int X { get; set; } }

                [NCborSerializable(typeof(M))]
                public partial class Ctx : NCborSerializerContext { }

                [NCborSerializable(typeof(M))]
                public partial class CTX : NCborSerializerContext { }

                [NCborSerializable(typeof(M))]
                public partial class Other : NCborSerializerContext { }
            }
            """);

        result.GeneratorException.Should().BeNull();
        result.CompilationDiagnostics.Where(d => d.Id == "CS8785").Should().BeEmpty();
        result.GeneratedSources.Should().HaveCount(3);
        result.GeneratedSources.Keys.Select(k => k.ToUpperInvariant()).Should().OnlyHaveUniqueItems();
        result.GeneratedSources.Keys.Should().Contain("N1.Other.g.cs", "only colliding contexts get a hash suffix");
        result.GeneratedSources.Keys.Should().NotContain("N1.Ctx.g.cs").And.NotContain("N1.CTX.g.cs");
        result.AssertCompilesCleanly();
    }

    [Fact]
    public void HintNames_AreStableAcrossRuns()
    {
        const string source = """
            using NCbor;
            namespace N1
            {
                public class M { public int X { get; set; } }
                [NCborSerializable(typeof(M))] public partial class Ctx : NCborSerializerContext { }
                [NCborSerializable(typeof(M))] public partial class CTX : NCborSerializerContext { }
            }
            """;

        GeneratorHarness.Run(source).GeneratedSources.Keys
            .Should().Equal(GeneratorHarness.Run(source).GeneratedSources.Keys);
    }

    // ---------- CBOR007 location ----------

    [Fact]
    public void CBOR007_IsReportedOnTheSecondMarkedConstructor()
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

        var cbor007 = result.DiagnosticsWithId("CBOR007").Should().ContainSingle().Subject;
        cbor007.Location.IsInSource.Should().BeTrue("Location.None can't be navigated to or suppressed with #pragma");
        cbor007.Location.SourceTree!.FilePath.Should().Be("Input0.cs");
        // 0-based line 10 is `public Ambiguous(int a, string b)`.
        cbor007.Location.GetLineSpan().StartLinePosition.Line.Should().Be(10);
        cbor007.Location.SourceTree.GetText().ToString(cbor007.Location.SourceSpan).Should().Be("Ambiguous");
    }

    // ---------- EquatableArray<T> ----------

    [Fact]
    public void EquatableArray_DefaultAndEmpty_AreEqualAndHashEqually()
    {
        var defaultInstance = default(EquatableArray<string>);
        var empty = new EquatableArray<string>(Array.Empty<string>());

        defaultInstance.Equals(empty).Should().BeTrue();
        empty.Equals(defaultInstance).Should().BeTrue();
        defaultInstance.GetHashCode().Should().Be(empty.GetHashCode());
    }

    [Fact]
    public void EquatableArray_CopiesTheCallersArray()
    {
        var items = new[] { "a", "b" };
        var array = new EquatableArray<string>(items);
        var hashBefore = array.GetHashCode();

        items[0] = "changed";

        array.Should().Equal("a", "b");
        array.GetHashCode().Should().Be(hashBefore);
        array.Equals(new EquatableArray<string>(new[] { "a", "b" })).Should().BeTrue();
    }

    // ---------- Nested contexts ----------

    [Fact]
    public void NestedContext_InPartialContainingTypes_CompilesCleanly()
    {
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using NCbor;

            namespace Models
            {
                public class M { public int X { get; set; } public string? S { get; set; } }

                public static partial class Outer
                {
                    internal partial struct Middle<TItem>
                    {
                        [NCborSerializable(typeof(M))]
                        private partial class InnerContext : NCborSerializerContext { }

                        public static byte[] RoundTrip(M m) =>
                            NCborSerializer.Serialize(m, InnerContext.Default.M);
                    }

                    public partial record Rec
                    {
                        [NCborSerializable(typeof(M))]
                        public partial class RecContext : NCborSerializerContext { }
                    }
                }
            }
            """);

        result.GeneratedSources.Keys.Should().Contain("Models.Outer.Rec.RecContext.g.cs");
        result.AllGeneratedText.Should().Contain("partial struct Middle<TItem>").And.Contain("partial record Rec");
        result.AllGeneratedText.Should().Contain("private partial class InnerContext");
        result.GeneratedCodeWarnings.Should().BeEmpty(result.FormatDiagnostics(result.GeneratedCodeWarnings));
    }

    [Fact]
    public void NestedContext_InNonPartialContainingType_ReportsCBOR012()
    {
        var result = GeneratorHarness.Run("""
            using NCbor;

            namespace Models
            {
                public class M { public int X { get; set; } }

                public partial class Good
                {
                    [NCborSerializable(typeof(M))]
                    public partial class GoodContext : NCborSerializerContext { }
                }

                public class NotPartial
                {
                    [NCborSerializable(typeof(M))]
                    public partial class BadContext : NCborSerializerContext { }
                }
            }
            """);

        result.GeneratorException.Should().BeNull();
        var cbor012 = result.DiagnosticsWithId("CBOR012").Should().ContainSingle().Subject;
        cbor012.Severity.Should().Be(DiagnosticSeverity.Error);
        cbor012.GetMessage().Should().Contain("BadContext").And.Contain("NotPartial");
        cbor012.Location.IsInSource.Should().BeTrue();
        cbor012.Location.SourceTree!.GetText().ToString(cbor012.Location.SourceSpan).Should().Be("BadContext");

        // The other context is unaffected; nothing is generated for the broken one, so the only
        // compiler error is CS0534 (abstract GetTypeInfo<T> not implemented) on BadContext itself.
        result.GeneratedSources.Keys.Should().Equal("Models.Good.GoodContext.g.cs");
        result.GeneratedCodeErrors.Should().BeEmpty();
        result.CompilationErrors.Select(d => d.Id).Should().Equal("CS0534");
    }

    // ---------- Context accessibility ----------

    [Theory]
    [InlineData("internal partial class", "internal partial class Ctx")]
    [InlineData("partial class", "internal partial class Ctx")]
    [InlineData("public partial class", "public partial class Ctx")]
    [InlineData("public sealed partial class", "public partial class Ctx")]
    public void ContextAccessibility_IsPreserved(string declaration, string expectedGenerated)
    {
        var result = GeneratorHarness.AssertCompilesCleanly($$"""
            using NCbor;

            namespace Models
            {
                internal class M { public int X { get; set; } }

                [NCborSerializable(typeof(M))]
                {{declaration}} Ctx : NCborSerializerContext { }
            }
            """);

        result.AllGeneratedText.Should().Contain(expectedGenerated);
        result.CompilationDiagnostics.Where(d => d.Id == "CS0262").Should().BeEmpty();
    }
}
