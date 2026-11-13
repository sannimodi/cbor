using Microsoft.CodeAnalysis;

namespace NCbor.Generator.Tests;

/// <summary>
/// Generator-level regression tests for the wave-2 model-fidelity review: framework base classes
/// (item 1), static hiding (3), constructor parameter defaults and required members (5),
/// [SetsRequiredMembers] (6), struct constructor choice (7), case-clashing names (9), required
/// fields / CBOR014 (10a) and attribute inheritance through overrides (12). Runtime round-trips
/// for the same models are in NCbor.Tests/NCborModelFidelityRegressionTests.cs.
/// </summary>
public class ModelFidelityRegressionGeneratorTests
{
    private static string Wrap(string models, string registrations) => $$"""
        using System;
        using System.Collections.Generic;
        using System.Diagnostics.CodeAnalysis;
        using NCbor;

        namespace Models
        {
        {{models}}

        {{registrations}}
            public partial class RegressionContext : NCborSerializerContext { }
        }
        """;

    // ---------- Item 1 ----------

    public static TheoryData<string, string> FrameworkBaseModels => new()
    {
        { "Component", "public class C : System.ComponentModel.Component { public int Code { get; set; } }" },
        { "Exception", "public class MyErr : Exception { public int Code { get; set; } }" },
        { "Attribute", "public class Attr : Attribute { public int Code { get; set; } }" },
    };

    [Theory]
    [MemberData(nameof(FrameworkBaseModels))]
    public void FrameworkBaseClass_IsNotWalked_AndCompilesWithoutDiagnostics(string baseName, string model)
    {
        var typeName = model.Split(' ')[2];
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap(model, $"[NCborSerializable(typeof({typeName}))]"));

        result.DiagnosticsWithId("CBOR005").Should().BeEmpty($"{baseName}: {result}");
        result.DiagnosticsWithId("CBOR009").Should().BeEmpty($"{baseName}: {result}");
        result.GeneratedCodeWarnings.Should().BeEmpty(result.FormatDiagnostics(result.GeneratedCodeWarnings));

        var text = result.AllGeneratedText;
        text.Should().Contain("writer.WriteTextString(\"Code\")");
        foreach (var frameworkMember in new[] { "HResult", "HelpLink", "Source", "Site", "Container", "TypeId" })
            text.Should().NotContain($"\"{frameworkMember}\"", $"{baseName}'s own members are not serialized");
    }

    [Fact]
    public void UserBaseClass_BetweenTypeAndFrameworkBase_IsStillWalked()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap("""
            public class AppError : Exception { public int Area { get; set; } }
            public class DbError : AppError { public int Code { get; set; } }
            """, "[NCborSerializable(typeof(DbError))]"));

        result.GeneratorDiagnostics.Should().BeEmpty(result.ToString());
        var text = result.AllGeneratedText;
        var area = text.IndexOf("writer.WriteTextString(\"Area\")", StringComparison.Ordinal);
        var code = text.IndexOf("writer.WriteTextString(\"Code\")", StringComparison.Ordinal);
        area.Should().BeGreaterThan(-1);
        code.Should().BeGreaterThan(area);
        text.Should().NotContain("\"HResult\"");
    }

    [Fact]
    public void NonFrameworkBaseClassFromMetadata_IsStillWalked()
    {
        // NCborDeserializationException lives in the NCbor assembly (metadata, not System.*):
        // its get-only `Type` is walked (and so reported as CBOR005); Exception's members aren't.
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap(
            "public class MyErr : NCborDeserializationException { public int Code { get; set; } }",
            "[NCborSerializable(typeof(MyErr))]"));

        result.DiagnosticsWithId("CBOR005").Select(d => d.GetMessage()).Should().ContainSingle()
            .Which.Should().Contain("'Type'");
        result.AllGeneratedText.Should().NotContain("\"HResult\"").And.NotContain("\"Message\"");
    }

    // ---------- Item 3 ----------

    [Fact]
    public void NewStaticProperty_HidingInstanceProperty_IsDropped_AndCompiles()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap("""
            public class Base { public int Id { get; set; } }
            public class Stat : Base { public new static int Id { get; set; } public string Name { get; set; } = ""; }
            """, "[NCborSerializable(typeof(Stat))]"));

        result.GeneratorDiagnostics.Should().BeEmpty(result.ToString());
        result.AllGeneratedText.Should().NotContain("\"Id\"").And.Contain("\"Name\"");
    }

    // ---------- Item 5 ----------

    [Fact]
    public void ConstructorParameterDefaults_SeedTheLocals()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap("""
            public enum Color { Red = 1, Blue = 3 }
            public record Opt(int A = 42, string S = "d\"e\nf", Color C = Color.Blue, Guid G = default,
                int? N = null, double D = 1.5, decimal M = 2.25m, long L = 5000000000,
                float F = float.NaN, byte By = 7, [property: NCborIgnore] char Ch = '\'');
            """, "[NCborSerializable(typeof(Opt))]"));

        // Only the ignored (char isn't a supported type) parameter: CBOR006.
        result.GeneratorDiagnostics.Should().ContainSingle(d => d.Id == "CBOR006").And.HaveCount(1, result.ToString());
        result.GeneratedCodeWarnings.Should().BeEmpty(result.FormatDiagnostics(result.GeneratedCodeWarnings));
        var text = result.AllGeneratedText;
        text.Should().Contain("int __v_A = (int)(42);");
        text.Should().Contain("string __v_S = (string)(\"d\\\"e\\nf\");");
        text.Should().Contain("global::Models.Color __v_C = (global::Models.Color)(3);");
        text.Should().Contain("global::System.Guid __v_G = default(global::System.Guid)!;");
        text.Should().Contain("int? __v_N = default(int?)!;");
        text.Should().Contain("float __v_F = (float)(float.NaN);");
        text.Should().Contain("double __v_D = (double)(1.5d);");
        text.Should().Contain("decimal __v_M = (decimal)(2.25m);");
        text.Should().Contain("long __v_L = (long)(5000000000L);");
        text.Should().Contain("byte __v_By = (byte)(7);");
        text.Should().Contain(", (char)('\\''))", "an unmatched parameter is passed its own default");
    }

    [Fact]
    public void UnmatchedParameter_PassesItsDefault()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap(
            "public record OptIgnored([property: NCborIgnore] int Ig = 3, int B = 0);",
            "[NCborSerializable(typeof(OptIgnored))]"));

        result.DiagnosticsWithId("CBOR006").Should().ContainSingle();
        result.AllGeneratedText.Should().Contain("new global::Models.OptIgnored((int)(3), __v_B)");
    }

    [Fact]
    public void ParameterDefault_IsNotUsed_WhenAnIgnoreConditionMayOmitTheKey()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap(
            "public record G([property: NCborIgnore(Condition = NCborIgnoreCondition.WhenWritingDefault)] int A = 42, [property: NCborDefaultValue(7)] int B = 42);",
            "[NCborSerializable(typeof(G))]"));

        var text = result.AllGeneratedText;
        text.Should().Contain("int __v_A = default!;");
        text.Should().Contain("int __v_B = (int)(7)!;", "[NCborDefaultValue] wins over the parameter default");
    }

    [Fact]
    public void RequiredMember_MissingKeyThrows_UnlessTheWriterMayOmitIt()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap("""
            public class Req
            {
                public required string Name { get; set; }
                [NCborIgnore(Condition = NCborIgnoreCondition.WhenWritingNull)] public required string? Note { get; set; }
                [NCborDefaultValue(5)] public required int Level { get; set; }
            }
            """, "[NCborSerializable(typeof(Req))]"));

        var text = result.AllGeneratedText;
        text.Should().Contain("if (!__h_Name) throw new global::NCbor.NCborDeserializationException(typeof(global::Models.Req), \"Required property 'Name' (CBOR key 'Name') was not found in the payload.\");");
        text.Should().NotContain("__h_Note").And.NotContain("__h_Level");
    }

    // ---------- Item 6 ----------

    [Fact]
    public void SetsRequiredMembers_SettableRequiredMembersAreConditionallyAssigned()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap("""
            public class Req2
            {
                [SetsRequiredMembers] public Req2() { X = 1; S = "ctor"; I = 2; }
                public required int X { get; set; }
                public required string S { get; set; }
                public required int I { get; init; }
            }
            """, "[NCborSerializable(typeof(Req2))]"));

        result.GeneratorDiagnostics.Should().BeEmpty(result.ToString());
        var text = result.AllGeneratedText;
        text.Should().Contain("if (__h_X) result.X = __v_X;");
        text.Should().Contain("if (__h_S) result.S = __v_S;");
        text.Should().NotContain("throw new global::NCbor.NCborDeserializationException(typeof(global::Models.Req2), \"Required");
        // Known limit: an init-only member can only be set in the initializer, so it is always
        // passed (and the constructor's value lost when its key is absent).
        text.Should().Contain("global::Models.Req2 result = new global::Models.Req2() { I = __v_I };");
    }

    // ---------- Item 7 ----------

    [Fact]
    public void Struct_WithAllSettableProperties_UsesNewT_NotTheExplicitConstructor()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap("""
            public struct SV
            {
                public SV(string name) { Name = name ?? throw new ArgumentNullException(nameof(name)); B = 0; }
                public string Name { get; set; }
                public int B { get; set; }
            }
            """, "[NCborSerializable(typeof(SV))]"));

        result.GeneratorDiagnostics.Should().BeEmpty(result.ToString());
        result.AllGeneratedText.Should().Contain("global::Models.SV result = new global::Models.SV();").And.NotContain("new global::Models.SV(__v_");
    }

    [Fact]
    public void Struct_WithGetOnlyPropertyCoveredByConstructor_StillUsesTheConstructor()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap("""
            public struct P
            {
                public P(int x) { X = x; }
                public int X { get; }
                public int Y { get; set; }
            }
            """, "[NCborSerializable(typeof(P))]"));

        result.GeneratorDiagnostics.Should().BeEmpty(result.ToString());
        result.AllGeneratedText.Should().Contain("new global::Models.P(__v_X)").And.Contain("if (__h_Y) result.Y = __v_Y;");
    }

    // ---------- Item 9 ----------

    [Fact]
    public void ConstructorParameter_PrefersExactCaseMatch()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap("""
            public class CaseClash
            {
                public CaseClash(int value) { this.value = value; }
                public int Value { get; }
                public int value { get; set; }
            }
            """, "[NCborSerializable(typeof(CaseClash))]"));

        result.DiagnosticsWithId("CBOR005").Select(d => d.GetMessage()).Should().ContainSingle()
            .Which.Should().Contain("'Value'");
        result.DiagnosticsWithId("CBOR006").Should().BeEmpty();
        result.AllGeneratedText.Should().Contain("new global::Models.CaseClash(__v_value)");
    }

    [Fact]
    public void ConstructorParameter_WithTwoCaseInsensitiveMatchesAndNoExactOne_IsUnmatched()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap("""
            public class Twin
            {
                [NCborConstructor] public Twin(int vALUE) { }
                public int Value { get; set; }
                public int VALUE { get; set; }
            }
            """, "[NCborSerializable(typeof(Twin))]"));

        result.DiagnosticsWithId("CBOR006").Select(d => d.GetMessage()).Should().ContainSingle()
            .Which.Should().Contain("'vALUE'");
        result.AllGeneratedText.Should().Contain("new global::Models.Twin(default(int)!)");
    }

    // ---------- Item 10a ----------

    [Fact]
    public void CBOR014_IsReported_ForARequiredField()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap("""
            public class Base { public required int BaseField; }
            public class Req : Base
            {
                public required int Count { get; init; }
                public required int Field;
            }
            """, "[NCborSerializable(typeof(Req))]"));

        var cbor014 = result.DiagnosticsWithId("CBOR014");
        cbor014.Should().HaveCount(2, result.ToString());
        cbor014.Should().OnlyContain(d => d.Severity == DiagnosticSeverity.Warning && d.Location.IsInSource);
        cbor014.Select(d => d.GetMessage()).Should().Contain(m => m.Contains("'Field'") && m.Contains("'Req'"))
            .And.Contain(m => m.Contains("'BaseField'") && m.Contains("'Base'"));
        result.AllGeneratedText.Should().Contain("Field = default!").And.Contain("BaseField = default!");
    }

    [Fact]
    public void CBOR014_IsNotReported_UnderSetsRequiredMembers()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap("""
            public class Req
            {
                [SetsRequiredMembers] public Req() { Field = 1; }
                public required int Field;
                public int Plain { get; set; }
            }
            """, "[NCborSerializable(typeof(Req))]"));

        result.DiagnosticsWithId("CBOR014").Should().BeEmpty(result.ToString());
        result.AllGeneratedText.Should().NotContain("Field = default!");
    }

    // ---------- Item 12 ----------

    [Fact]
    public void Override_InheritsNCborAttributesFromTheOverriddenProperty()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap("""
            public enum Mode { A, B }
            public class Base
            {
                [NCborIgnore] public virtual string Secret { get; set; } = "";
                [NCborPropertyName("n")] public virtual string Name { get; set; } = "";
                [NCborDefaultValue(5)] public virtual int Level { get; set; } = 5;
                [NCborEnumAsString] public virtual Mode Mode { get; set; }
            }
            public class Mid : Base { }
            public class Derived : Mid
            {
                public override string Secret { get; set; } = "";
                public override string Name { get; set; } = "";
                public override int Level { get; set; } = 5;
                public override Mode Mode { get; set; }
            }
            """, "[NCborSerializable(typeof(Derived))]"));

        result.GeneratorDiagnostics.Should().BeEmpty(result.ToString());
        var text = result.AllGeneratedText;
        text.Should().NotContain("\"Secret\"").And.NotContain("\"Name\"");
        text.Should().Contain("writer.WriteTextString(\"n\")");
        text.Should().Contain("EqualityComparer<int>.Default.Equals(value.Level, 5)");
        text.Should().Contain("writer.WriteTextString(value.Mode.ToString())");
    }

    [Fact]
    public void NewProperty_DoesNotInheritAttributesFromTheHiddenOne()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(Wrap("""
            public class Base { [NCborPropertyName("n")] public virtual string Name { get; set; } = ""; }
            public class Derived : Base { public new string Name { get; set; } = ""; }
            """, "[NCborSerializable(typeof(Derived))]"));

        result.AllGeneratedText.Should().Contain("writer.WriteTextString(\"Name\")").And.NotContain("\"n\"");
    }
}
