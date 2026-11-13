using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NCbor.Generator.Tests;

/// <summary>
/// Guards the incremental-pipeline properties of <see cref="NCborSourceGenerator"/> that the
/// ForAttributeWithMetadataName rewrite introduced: output caching across unrelated edits,
/// partial-declaration dedupe, and diagnostics carrying real in-source locations (which is what
/// lets <c>#pragma warning disable CBOR005</c> work). Each of these could regress while every
/// other generator test stays green.
/// </summary>
public class GeneratorPipelineTests
{
    // Mirrors TrackingNames in NCborSourceGenerator.cs (internal there).
    private const string ContextModels = "ContextModels";
    private const string GeneratedSource = "GeneratedSource";

    private const string ModelSource = """
        using NCbor;
        namespace Pipe;
        public class Person { public string Name { get; set; } = ""; public int Age { get; set; } }
        """;

    private const string ContextSource = """
        using NCbor;
        namespace Pipe;
        [NCborSerializable(typeof(Person))]
        public partial class PipeContext : NCborSerializerContext { }
        """;

    private static CSharpCompilation Compile(params (string path, string text)[] files) =>
        CSharpCompilation.Create(
            "PipelineTestAssembly",
            files.Select(f => CSharpSyntaxTree.ParseText(f.text, GeneratorHarness.ParseOptions, path: f.path)),
            GeneratorHarness.References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

    private static GeneratorDriver CreateTrackingDriver() =>
        CSharpGeneratorDriver.Create(
            generators: new[] { new NCborSourceGenerator().AsSourceGenerator() },
            parseOptions: GeneratorHarness.ParseOptions,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

    private static IEnumerable<IncrementalStepRunReason> OutputReasons(GeneratorDriver driver, string stepName) =>
        driver.GetRunResult().Results.Single().TrackedSteps[stepName]
            .SelectMany(step => step.Outputs)
            .Select(o => o.Reason);

    [Fact]
    public void UnrelatedEdit_LeavesModelAndSourceCached()
    {
        var compilation = Compile(("Model.cs", ModelSource), ("Context.cs", ContextSource));
        var driver = CreateTrackingDriver().RunGenerators(compilation);

        var edited = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText("namespace Pipe; public class Unrelated { public int X; }", GeneratorHarness.ParseOptions, path: "Unrelated.cs"));
        driver = driver.RunGenerators(edited);

        OutputReasons(driver, ContextModels).Should().NotBeEmpty()
            .And.OnlyContain(r => r == IncrementalStepRunReason.Cached || r == IncrementalStepRunReason.Unchanged);
        OutputReasons(driver, GeneratedSource).Should().NotBeEmpty()
            .And.OnlyContain(r => r == IncrementalStepRunReason.Cached || r == IncrementalStepRunReason.Unchanged);
    }

    [Fact]
    public void RegisteredModelChangedInAnotherFile_RegeneratesSameAsFreshRun()
    {
        var compilation = Compile(("Model.cs", ModelSource), ("Context.cs", ContextSource));
        var driver = CreateTrackingDriver().RunGenerators(compilation);

        var oldTree = compilation.SyntaxTrees.Single(t => t.FilePath == "Model.cs");
        var newModel = ModelSource.Replace("public int Age { get; set; }", "public int Age { get; set; } public bool IsActive { get; set; }");
        var edited = compilation.ReplaceSyntaxTree(oldTree, CSharpSyntaxTree.ParseText(newModel, GeneratorHarness.ParseOptions, path: "Model.cs"));
        driver = driver.RunGenerators(edited);

        OutputReasons(driver, GeneratedSource).Should().Contain(IncrementalStepRunReason.Modified);

        var incremental = driver.GetRunResult().GeneratedTrees.Single().ToString();
        var fresh = CreateTrackingDriver().RunGenerators(edited).GetRunResult().GeneratedTrees.Single().ToString();
        incremental.Should().Be(fresh);
        incremental.Should().Contain("IsActive");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContextSplitAcrossPartialDeclarations_GeneratesOneSourceWithAllTypes(bool reverseFileOrder)
    {
        const string partA = """
            using NCbor;
            namespace Pipe;
            public class A { public int X { get; set; } }
            [NCborSerializable(typeof(A))]
            public partial class SplitContext : NCborSerializerContext { }
            """;
        const string partB = """
            using NCbor;
            namespace Pipe;
            public class B { public int Y { get; set; } }
            [NCborSerializable(typeof(B))]
            public partial class SplitContext { }
            """;

        var files = reverseFileOrder ? new[] { ("B.cs", partB), ("A.cs", partA) } : new[] { ("A.cs", partA), ("B.cs", partB) };
        var compilation = Compile(files);
        var driver = CreateTrackingDriver().RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        var result = driver.GetRunResult();
        result.Results.Single().Exception.Should().BeNull();
        result.GeneratedTrees.Should().ContainSingle();
        var text = result.GeneratedTrees.Single().ToString();
        text.Should().Contain("ATypeInfo").And.Contain("BTypeInfo");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
    }

    [Fact]
    public void Cbor005_IsReportedAtTheRealPropertyLocation_InTheUsersTree()
    {
        const string model = """
            using NCbor;
            namespace Pipe;
            public class HasGetOnly
            {
                public int Settable { get; set; }
                public int ReadOnlyThing { get; }
            }
            """;
        const string context = """
            using NCbor;
            namespace Pipe;
            [NCborSerializable(typeof(HasGetOnly))]
            public partial class DiagContext : NCborSerializerContext { }
            """;
        var compilation = Compile(("Model.cs", model), ("Context.cs", context));
        CreateTrackingDriver().RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        var cbor005 = diagnostics.Should().ContainSingle(d => d.Id == "CBOR005").Subject;
        cbor005.Location.IsInSource.Should().BeTrue("a path-only location breaks #pragma suppression and IDE navigation");
        cbor005.Location.SourceTree.Should().BeSameAs(compilation.SyntaxTrees.Single(t => t.FilePath == "Model.cs"));
        var spanText = cbor005.Location.SourceTree!.GetText().ToString(cbor005.Location.SourceSpan);
        spanText.Should().Be("ReadOnlyThing");
    }
}
