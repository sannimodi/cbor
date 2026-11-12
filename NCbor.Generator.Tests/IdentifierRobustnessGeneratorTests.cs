using Microsoft.CodeAnalysis;

namespace NCbor.Generator.Tests;

/// <summary>
/// Generated code must not mis-bind or fail to parse because of what the user named things:
/// global-namespace types are referenced <c>global::</c>-qualified (so they can't bind to a
/// generated <c>{Name}TypeInfo</c> class, the context, a context type parameter or a nested type),
/// nested types of the context are reserved in the context-property name map, and C# keywords
/// are escaped wherever they're emitted as identifiers. Also CBOR015 (framework base properties
/// that are silently not serialized). Runtime round-trips are in
/// NCbor.Tests/NCborIdentifierRobustnessTests.cs.
/// </summary>
public class IdentifierRobustnessGeneratorTests
{
    // ---------- Item 1: fully qualified type names ----------

    [Fact]
    public void GlobalType_NamedLikeAGeneratedTypeInfoClass_DoesNotBindToIt()
    {
        // Foo gets a generated nested `FooTypeInfo` class; the user's own global FooTypeInfo must
        // still be what `new FooTypeInfo()` in its deserializer constructs.
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using NCbor;

            public class FooTypeInfo { public int X { get; set; } }
            public class Foo { public int A { get; set; } public FooTypeInfo? Info { get; set; } }

            [NCborSerializable(typeof(Foo))]
            [NCborSerializable(typeof(FooTypeInfo))]
            public partial class Ctx : NCborSerializerContext { }
            """);

        result.GeneratedCodeWarnings.Should().BeEmpty(result.FormatDiagnostics(result.GeneratedCodeWarnings));
        result.AllGeneratedText.Should().Contain("new global::FooTypeInfo()").And.Contain("new global::Foo()");
    }

    [Fact]
    public void ModelType_NamedLikeTheContext_CompilesCleanly()
    {
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using NCbor;

            public class Ctx { public int A { get; set; } }
            public class Holder { public Ctx? Inner { get; set; } public Ctx[]? Many { get; set; } }

            namespace Models
            {
                [NCborSerializable(typeof(global::Holder))]
                public partial class Ctx : NCborSerializerContext { }
            }
            """);

        result.AllGeneratedText.Should().Contain("new global::Ctx()");
    }

    [Fact]
    public void GlobalType_NamedLikeAContextTypeParameter_CompilesCleanly()
    {
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using NCbor;

            public class T { public int A { get; set; } }

            namespace Models
            {
                [NCborSerializable(typeof(global::T))]
                [NCborSerializable(typeof(System.Collections.Generic.List<global::T>))]
                public partial class Ctx<T> : NCborSerializerContext { }
            }
            """);

        result.AllGeneratedText.Should().Contain("new global::T()");
    }

    [Fact]
    public void GlobalTypes_ShadowedByContextMembersOrTypesInTheContextNamespace_CompileCleanly()
    {
        // The context's namespace declares types named like framework types and NCbor's own
        // helpers' types; every reference the generated code makes must still reach the real ones.
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using System;
            using System.Collections.Generic;
            using NCbor;

            namespace Models.System { public class Guid { } }

            namespace Models
            {
                public class Guid { }
                public class DateTime { }
                public class Enum { }
                public enum Color { Red, Green }

                public class M
                {
                    public global::System.Guid G { get; set; }
                    public global::System.DateTime D { get; set; }
                    public global::System.DateTimeOffset O { get; set; }
                    public global::System.Guid? NG { get; set; }
                    public List<int>? L { get; set; }
                    public HashSet<string>? S { get; set; }
                    public Dictionary<string, int>? Map { get; set; }
                    public int[][]? Jagged { get; set; }
                    public byte[]? Bytes { get; set; }
                    [NCborEnumAsString] public Color C { get; set; }
                    [NCborEnumAsString] public Color? NC { get; set; }
                    [NCborDefaultValue(Color.Green)] public Color DC { get; set; }
                    [NCborIgnore(Condition = NCborIgnoreCondition.WhenWritingDefault)] public int W { get; set; }
                }

                [NCborSerializable(typeof(M))]
                public partial class Ctx : NCborSerializerContext { }
            }
            """);

        result.GeneratedCodeWarnings.Should().BeEmpty(result.FormatDiagnostics(result.GeneratedCodeWarnings));
    }

    [Fact]
    public void TypesNamedLikeTheGeneratedCodesOwnDependencies_DoNotShadowThem()
    {
        // Types in the context's namespace (and nested in the context) beat the generated file's
        // `using` directives, so the generated code must not rely on them.
        var result = GeneratorHarness.Run("""
            using System.Collections.Generic;
            using NCbor;

            namespace Models
            {
                public class CborWriter { }
                public class CborReader { }
                public class CborReaderState { }
                public class Type { }
                public class ArgumentException { }
                public class NCborTypeInfo<T> { }
                public class NCborSerializationException { }
                public class NCborDeserializationException { }

                public enum Color { Red, Green }
                public class M
                {
                    public string? S { get; set; }
                    public int? N { get; set; }
                    public Color? C { get; set; }
                    public List<string>? L { get; set; }
                    public Dictionary<string, M>? D { get; set; }
                    public M[]? Arr { get; set; }
                }

                public record R(int A, string B) { public required int Req { get; init; } }

                [NCborSerializable(typeof(M))]
                [NCborSerializable(typeof(R))]
                [NCborSerializable(typeof(int))]
                [NCborSerializable(typeof(System.TimeSpan))]
                public partial class Ctx : NCborSerializerContext
                {
                    public class CborTag { }
                    public class InvalidOperationException { }
                }
            }
            """);

        // TimeSpan is unsupported (CBOR009, an error) so that its throwing stub TypeInfo is
        // covered too; apart from that the output must compile cleanly.
        result.GeneratorException.Should().BeNull();
        result.GeneratedSources.Should().NotBeEmpty();
        result.GeneratorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.Id).Should().Equal("CBOR009");
        result.CompilationErrors.Should().BeEmpty(result.FormatDiagnostics(result.CompilationErrors) + "\n" + result.DumpGeneratedSources());
        result.GeneratedCodeWarnings.Should().BeEmpty(result.FormatDiagnostics(result.GeneratedCodeWarnings));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExplicitlyTypedLocals_AddNoNullableWarnings_ForSharedAnnotatedAndUnannotatedCollections(bool annotatedFirst)
    {
        // List<Foo> and List<Foo?> share one TypeInfo built from whichever is seen first, so the
        // explicitly typed loop/element locals must not produce nullability warnings either way.
        var annotated = "public Dictionary<string, Foo?>? D1 { get; set; } public List<Foo?>? L1 { get; set; } public Foo?[]? A1 { get; set; } public HashSet<string?>? S1 { get; set; } public IEnumerable<Foo?>? E1 { get; set; }";
        var plain = "public Dictionary<string, Foo>? D2 { get; set; } public List<Foo>? L2 { get; set; } public Foo[]? A2 { get; set; } public HashSet<string>? S2 { get; set; } public IEnumerable<Foo>? E2 { get; set; }";
        var result = GeneratorHarness.AssertCompilesCleanly($$"""
            using System.Collections.Generic;
            using NCbor;

            namespace Models
            {
                public class Foo { public int A { get; set; } }
                public class M
                {
                    {{(annotatedFirst ? annotated : plain)}}
                    {{(annotatedFirst ? plain : annotated)}}
                }

                [NCborSerializable(typeof(M))]
                public partial class Ctx : NCborSerializerContext { }
            }
            """);

        result.GeneratedCodeWarnings.Should().BeEmpty(result.FormatDiagnostics(result.GeneratedCodeWarnings));
    }

    // ---------- Item 2: nested types reserved in the context-property name map ----------

    [Fact]
    public void NestedTypeOfTheContext_NamedLikeARegisteredType_DoesNotCollide()
    {
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using NCbor;

            public class Item { public int A { get; set; } }

            namespace Models
            {
                [NCborSerializable(typeof(global::Item))]
                [NCborSerializable(typeof(Ctx.Item))]
                public partial class Ctx : NCborSerializerContext
                {
                    public class Item { public string? B { get; set; } }
                }
            }
            """);

        result.CompilationDiagnostics.Where(d => d.Id is "CS0102" or "CS0229").Should().BeEmpty();
        result.AllGeneratedText.Should().Contain("new global::Item()").And.Contain("new global::Models.Ctx.Item()");
    }

    [Fact]
    public void ContextMembers_NamedLikeAGeneratedTypeInfoClass_DoNotCollide()
    {
        // A nested type and a method of the context both take a `{Name}TypeInfo` name.
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using NCbor;

            namespace Models
            {
                public class Helper { public int A { get; set; } }
                public class Other { public int B { get; set; } }

                [NCborSerializable(typeof(Helper))]
                [NCborSerializable(typeof(Other))]
                public partial class Ctx : NCborSerializerContext
                {
                    public class HelperTypeInfo { }
                    public static int OtherTypeInfo() => 0;
                }
            }
            """);

        result.CompilationDiagnostics.Where(d => d.Id is "CS0102" or "CS0229" or "CS0111").Should().BeEmpty();
        result.AllGeneratedText.Should().NotContain("class HelperTypeInfo").And.NotContain("class OtherTypeInfo");
    }

    // ---------- Item 3: keyword identifiers ----------

    [Fact]
    public void KeywordNamedProperties_AreEscaped_OnEveryPath_AndWireKeysStayUnescaped()
    {
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using System.Collections.Generic;
            using NCbor;

            namespace Models
            {
                // Fast path: new T() + assignments.
                public class Plain
                {
                    public int @class { get; set; }
                    public string? @params { get; set; }
                    public List<int>? @event { get; set; }
                    [NCborIgnore(Condition = NCborIgnoreCondition.WhenWritingNull)] public string? @null { get; set; }
                    [NCborIgnore(Condition = NCborIgnoreCondition.WhenWritingDefault)] public int @default { get; set; }
                    [NCborDefaultValue(7)] public int @int { get; set; }
                    public int value { get; set; }
                    public int var { get; set; }
                    public int record { get; set; }
                    public int field { get; set; }
                }

                // General path: constructor argument, init-only and required (object initializer),
                // and a conditionally assigned setter.
                public record Rec(int @class, string @event)
                {
                    public int @fixed { get; init; }
                    public required int @operator { get; set; }
                    public int @this { get; set; }
                }

                [NCborSerializable(typeof(Plain))]
                [NCborSerializable(typeof(Rec))]
                public partial class Ctx : NCborSerializerContext { }
            }
            """);

        result.GeneratedCodeWarnings.Should().BeEmpty(result.FormatDiagnostics(result.GeneratedCodeWarnings));
        var text = result.AllGeneratedText;
        text.Should().Contain("value.@class").And.Contain("result.@params =").And.Contain("value.@null != null");
        text.Should().Contain("@fixed = __v_fixed").And.Contain("@operator = __v_operator").And.Contain("result.@this = __v_this");
        text.Should().Contain("__v_class").And.NotContain("__v_@");
        foreach (var key in new[] { "class", "params", "event", "null", "default", "int", "value", "var", "record", "field", "fixed", "operator", "this" })
        {
            text.Should().Contain($"writer.WriteTextString(\"{key}\")").And.Contain($"case \"{key}\":");
        }
        text.Should().NotContain("\"@");
    }

    [Fact]
    public void KeywordNamedTypes_ContextAndNamespace_AreEscaped()
    {
        // CS8981 (lower-case type name) is a user-code warning; the harness allows warnings.
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using System.Collections.Generic;
            using NCbor;

            namespace Models.@event
            {
                public class @event { public int @class { get; set; } }
                public enum @enum { A, B }
                public class Holder { public @event? E { get; set; } public List<@event>? Es { get; set; } public @enum K { get; set; } public @enum? NK { get; set; } }

                [NCborSerializable(typeof(Holder))]
                [NCborSerializable(typeof(@enum))]
                public partial class @class : NCborSerializerContext { }
            }
            """);

        result.GeneratedCodeWarnings.Should().BeEmpty(result.FormatDiagnostics(result.GeneratedCodeWarnings));
        var text = result.AllGeneratedText;
        text.Should().Contain("namespace Models.@event").And.Contain("partial class @class").And.Contain("public @class()");
        text.Should().Contain("global::NCbor.NCborTypeInfo<global::Models.@event.@event> @event { get; }");
        text.Should().Contain("_context.@event.Serialize(");
        text.Should().Contain("global::NCbor.NCborTypeInfo<global::Models.@event.@enum> @enum { get; }");
    }

    [Fact]
    public void TypeNamedVar_DoesNotHijackGeneratedLocals()
    {
        // `var x = ...` binds to a type named `var` when one is in scope; generated code must not
        // depend on `var` meaning "infer".
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using System.Collections.Generic;
            using NCbor;

            public class var { public int A { get; set; } }

            namespace Models
            {
                public class M
                {
                    public global::var? V { get; set; }
                    public List<int>? L { get; set; }
                    public Dictionary<string, int>? D { get; set; }
                    public int[]? Arr { get; set; }
                }

                public record R(int A, string B);

                [NCborSerializable(typeof(M))]
                [NCborSerializable(typeof(R))]
                public partial class Ctx : NCborSerializerContext { }
            }
            """);

        result.AllGeneratedText.Should().Contain("new global::var()");
    }

    // ---------- Item 4: CBOR015 ----------

    private static string FrameworkBaseSource(string models, params string[] registered) => $$"""
        using System;
        using System.Collections.Generic;
        using NCbor;

        namespace Models
        {
        {{models}}

        {{string.Join("\n", registered.Select(r => $"    [NCborSerializable(typeof({r}))]"))}}
            public partial class Ctx : NCborSerializerContext { }
        }
        """;

    [Fact]
    public void CBOR015_Fires_ForUriBuilderSubclass_ListingTheDroppedProperties()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(FrameworkBaseSource(
            "    public class MyUri : UriBuilder { public int Extra { get; set; } }", "MyUri"));

        var cbor015 = result.DiagnosticsWithId("CBOR015");
        cbor015.Should().ContainSingle(result.ToString());
        cbor015[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        // Property order is the framework's metadata order, so only membership is pinned.
        var message = cbor015[0].GetMessage();
        message.Should().StartWith("Type 'Models.MyUri' derives from framework type 'System.UriBuilder'; its inherited properties (")
            .And.EndWith(") are not serialized. Redeclare them, or use a converter.");
        var listed = message.Substring(message.IndexOf('(') + 1, message.IndexOf(')') - message.IndexOf('(') - 1).Split(", ");
        listed.Should().BeEquivalentTo("Scheme", "UserName", "Password", "Host", "Port", "Path", "Query", "Fragment");
        cbor015[0].Location.SourceTree.Should().NotBeNull();
        cbor015[0].Location.SourceTree!.GetText().ToString(cbor015[0].Location.SourceSpan).Should().Be("MyUri");
        result.AllGeneratedText.Should().Contain("writer.WriteTextString(\"Extra\")").And.NotContain("\"Host\"");
    }

    [Fact]
    public void CBOR015_Fires_ForHttpRequestMessageSubclass()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(FrameworkBaseSource(
            "    public class MyReq : System.Net.Http.HttpRequestMessage { public int Extra { get; set; } }", "MyReq"));

        var message = result.DiagnosticsWithId("CBOR015").Should().ContainSingle(result.ToString()).Which.GetMessage();
        message.Should().Contain("'System.Net.Http.HttpRequestMessage'").And.Contain("RequestUri").And.Contain("Method");
        message.Should().NotContain("Headers", "get-only properties were never serializable");
    }

    [Fact]
    public void CBOR015_IsReportedOncePerDeclaringType_AndOmitsRedeclaredProperties()
    {
        var result = GeneratorHarness.AssertCompilesCleanly(FrameworkBaseSource("""
                public class MyUri : UriBuilder { public new string Host { get => base.Host; set => base.Host = value; } }
                public class Derived1 : MyUri { public int A { get; set; } }
                public class Derived2 : MyUri { public int B { get; set; } }
            """, "Derived1", "Derived2", "MyUri"));

        var message = result.DiagnosticsWithId("CBOR015").Should().ContainSingle(result.ToString()).Which.GetMessage();
        message.Should().StartWith("Type 'Models.MyUri' derives").And.Contain("Scheme").And.NotContain("Host");
    }

    [Fact]
    public void CBOR015_IsNotReported_WhenEveryDroppedPropertyIsRedeclared()
    {
        var redeclared = string.Join(" ", new[] { "Scheme", "UserName", "Password", "Host", "Path", "Query", "Fragment" }
            .Select(p => $"public new string {p} {{ get => base.{p}; set => base.{p} = value; }}"));
        var result = GeneratorHarness.AssertCompilesCleanly(FrameworkBaseSource(
            $"    public class MyUri : UriBuilder {{ {redeclared} public new int Port {{ get => base.Port; set => base.Port = value; }} }}", "MyUri"));

        result.DiagnosticsWithId("CBOR015").Should().BeEmpty();
    }

    public static TheoryData<string> InfrastructureBaseModels => new()
    {
        "public class MyErr : Exception { public int Code { get; set; } }",
        "public class MyArgErr : ArgumentException { public int Code { get; set; } }",
        "public class Attr : Attribute { public int Code { get; set; } }",
        "public class Comp : System.ComponentModel.Component { public int Code { get; set; } }",
        "public class MyList : List<int> { public int Code { get; set; } }",
        "public class Plain { public int Code { get; set; } }",
    };

    [Theory]
    [MemberData(nameof(InfrastructureBaseModels))]
    public void CBOR015_IsNotReported_ForExceptionAttributeComponentOrCollectionSubclasses(string model)
    {
        // Exception (HelpLink/Source/HResult) and Component (Site) do have public settable
        // properties; they're infrastructure, not model data, so they're deliberately exempt.
        var typeName = model.Split(' ')[2];
        var result = GeneratorHarness.AssertCompilesCleanly(FrameworkBaseSource($"    {model}", typeName));

        result.DiagnosticsWithId("CBOR015").Should().BeEmpty(result.ToString());
    }
}
