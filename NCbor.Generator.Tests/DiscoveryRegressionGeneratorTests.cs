using Microsoft.CodeAnalysis;

namespace NCbor.Generator.Tests;

/// <summary>
/// Generator-level coverage for the wave-2 discovery regressions: IEnumerable POCOs (CBOR013),
/// open generic registrations, <c>dynamic</c>, de-duplicated diagnostics on base members, jagged
/// arrays and dictionary-key codegen. Runtime round-trips live in NCbor.Tests/NCborDiscoveryRegressionTests.cs.
/// </summary>
public class DiscoveryRegressionGeneratorTests
{
    // ---------- CBOR013: user type implementing IEnumerable ----------

    [Theory]
    [InlineData("public class Bag : IEnumerable<int> { public int Code { get; set; } public IEnumerator<int> GetEnumerator() => new List<int>().GetEnumerator(); IEnumerator IEnumerable.GetEnumerator() => GetEnumerator(); }", "Bag")]
    [InlineData("public class MyList : List<int> { public int Code { get; set; } }", "MyList")]
    public void UserEnumerableType_ReportsCBOR013_AndSerializesAsObject(string declaration, string typeName)
    {
        var result = GeneratorHarness.AssertCompilesCleanly($$"""
            using System.Collections;
            using System.Collections.Generic;
            using NCbor;

            namespace Models
            {
                {{declaration}}

                public class Holder { public {{typeName}} Value { get; set; } = new(); }

                [NCborSerializable(typeof(Holder))]
                public partial class EnumerableContext : NCborSerializerContext { }
            }
            """);

        var cbor013 = result.DiagnosticsWithId("CBOR013");
        cbor013.Should().ContainSingle(result.ToString());
        cbor013[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        cbor013[0].GetMessage().Should().Contain($"Models.{typeName}").And.Contain("IEnumerable");
        cbor013[0].Location.SourceTree.Should().NotBeNull("the warning points at the type declaration, so #pragma can suppress it");
        result.GeneratorDiagnostics.Should().OnlyContain(d => d.Id == "CBOR013", "no CBOR009, and no CBOR005 for List<int>.Count");
        result.AllGeneratedText.Should().Contain("\"Code\"");
        result.AllGeneratedText.Should().NotContain("\"Capacity\"", "framework base members are not serialized");
    }

    [Fact]
    public void FrameworkCollectionOutsideTheTable_StillReportsCBOR009_NotCBOR013()
    {
        var result = GeneratorHarness.Run("""
            using System.Collections.Generic;
            using NCbor;

            namespace Models
            {
                public class Holder { public Queue<int> Pending { get; set; } = new(); }

                [NCborSerializable(typeof(Holder))]
                public partial class QueueContext : NCborSerializerContext { }
            }
            """);

        result.DiagnosticsWithId("CBOR009").Should().ContainSingle(result.ToString());
        result.DiagnosticsWithId("CBOR013").Should().BeEmpty();
        result.CompilationErrors.Should().BeEmpty(result.ToString());
    }

    // ---------- Open generic registrations ----------

    [Theory]
    [InlineData("typeof(Wrapper<>)")]
    [InlineData("typeof(Outer<>.Inner)")]
    public void OpenGenericRegistration_ReportsCBOR009Only_WithNoCompilerErrors(string typeofExpression)
    {
        var result = GeneratorHarness.Run($$"""
            using NCbor;

            namespace Models
            {
                public class Wrapper<T> { public T Value { get; set; } = default!; }
                public class Outer<T> { public class Inner { public T Value { get; set; } = default!; } }

                [NCborSerializable({{typeofExpression}})]
                public partial class OpenGenericContext : NCborSerializerContext { }
            }
            """);

        result.GeneratorException.Should().BeNull();
        var cbor009 = result.DiagnosticsWithId("CBOR009");
        cbor009.Should().ContainSingle(result.ToString());
        cbor009[0].GetMessage().Should().Contain("open generic types cannot be registered");
        cbor009[0].Location.SourceTree.Should().NotBeNull("the error points at the [NCborSerializable] attribute");
        result.DiagnosticsWithId("CBOR010").Should().BeEmpty("an open generic type gets no other checks");
        result.GeneratorDiagnostics.Should().OnlyContain(d => d.Id == "CBOR009", result.ToString());
        result.CompilationErrors.Should().BeEmpty(result.ToString() + "\n" + result.DumpGeneratedSources());
    }

    // ---------- dynamic ----------

    [Fact]
    public void DynamicProperty_ReportsCBOR009Only_WithNoCascadingErrors()
    {
        var result = GeneratorHarness.Run("""
            using System.Collections.Generic;
            using NCbor;

            namespace Models
            {
                public class Holder
                {
                    public int Id { get; set; }
                    public dynamic Anything { get; set; } = 0;
                    public List<dynamic> Many { get; set; } = new();
                }

                [NCborSerializable(typeof(Holder))]
                public partial class DynamicContext : NCborSerializerContext { }
            }
            """);

        result.GeneratorException.Should().BeNull();
        result.DiagnosticsWithId("CBOR009").Should().HaveCount(2, result.ToString());
        result.GeneratorDiagnostics.Should().OnlyContain(d => d.Id == "CBOR009", result.ToString());
        result.CompilationErrors.Should().BeEmpty(result.ToString() + "\n" + result.DumpGeneratedSources());
        result.GeneratedCodeWarnings.Should().BeEmpty(result.ToString());
        result.AllGeneratedText.Should().Contain("ThrowUnsupportedTypeOnWrite(\"dynamic\");");
        result.AllGeneratedText.Should().Contain("ThrowUnsupportedTypeOnRead<dynamic>(\"dynamic\")");
    }

    // ---------- Diagnostic de-duplication ----------

    [Fact]
    public void DiagnosticOnBaseMember_IsReportedOnce_ForSeveralDerivedTypes()
    {
        var result = GeneratorHarness.Run("""
            using System;
            using NCbor;

            namespace Models
            {
                public class Base
                {
                    public TimeSpan T { get; set; }
                    public int Ro { get; }
                    [NCborEnumAsString] public int NotAnEnum { get; set; }
                }
                public class D1 : Base { public int A { get; set; } }
                public class D2 : Base { public int B { get; set; } }

                [NCborSerializable(typeof(Base))]
                [NCborSerializable(typeof(D1))]
                [NCborSerializable(typeof(D2))]
                public partial class DedupeContext : NCborSerializerContext { }
            }
            """);

        result.DiagnosticsWithId("CBOR009").Should().ContainSingle(result.ToString())
            .Which.GetMessage().Should().Contain("Base.T");
        result.DiagnosticsWithId("CBOR005").Should().ContainSingle(result.ToString())
            .Which.GetMessage().Should().Contain("'Ro'").And.Contain("'Base'");
        result.DiagnosticsWithId("CBOR008").Should().ContainSingle(result.ToString())
            .Which.GetMessage().Should().Contain("'NotAnEnum'").And.Contain("'Base'");
        result.CompilationErrors.Should().BeEmpty(result.ToString());
    }

    // ---------- Jagged arrays ----------

    [Fact]
    public void JaggedArrays_CompileCleanly()
    {
        // `new int[][definiteLength]` was CS1586; the length belongs in the first rank specifier.
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using NCbor;

            namespace Models
            {
                public class Holder
                {
                    public int[][] Grid { get; set; } = new int[0][];
                    public byte[][] Blobs { get; set; } = new byte[0][];
                    public string[][][] Cube { get; set; } = new string[0][][];
                }

                [NCborSerializable(typeof(Holder))]
                public partial class JaggedContext : NCborSerializerContext { }
            }
            """);

        result.GeneratorDiagnostics.Should().BeEmpty(result.ToString());
        var text = result.AllGeneratedText;
        text.Should().Contain("result = new int[definiteLength][];");
        text.Should().Contain("result = new byte[definiteLength][];");
        text.Should().Contain("result = new string[definiteLength][][];");
    }

    // ---------- Dictionary keys ----------

    [Fact]
    public void Dictionary_RejectsNullKeys_AndWritesKeysWithoutNullBranch()
    {
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using System.Collections.Generic;
            using NCbor;

            namespace Models
            {
                [NCborSerializable(typeof(Dictionary<string, string>))]
                public partial class DictContext : NCborSerializerContext { }
            }
            """);

        var text = result.AllGeneratedText;
        text.Should().Contain("\"null dictionary key\"");
        text.Should().NotContain("kvp.Key is null", "dictionary keys are never null");
        text.Should().Contain("if (kvp.Value is null)");
    }
}
