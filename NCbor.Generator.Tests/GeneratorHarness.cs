using System.Collections.Immutable;
using System.Formats.Cbor;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NCbor.Generator.Tests;

/// <summary>
/// Runs <see cref="NCborSourceGenerator"/> in-process over C# source strings via
/// <see cref="CSharpGeneratorDriver"/> and reports what came out: the generator's own
/// diagnostics, the generated sources, and the diagnostics of the final compilation (user source
/// + generated source).
/// </summary>
/// <remarks>
/// No global usings are injected: test sources must declare their own <c>using</c> directives
/// (typically <c>using NCbor;</c> and <c>using System.Collections.Generic;</c>), and generated code
/// must stand on its own usings, the same as a consumer project with ImplicitUsings disabled.
/// </remarks>
public static class GeneratorHarness
{
    /// <summary>C# parse options shared by the input trees and the generator driver.</summary>
    public static readonly CSharpParseOptions ParseOptions =
        new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.None);

    private static readonly CSharpCompilationOptions CompilationOptions =
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithNullableContextOptions(NullableContextOptions.Enable)
            // Same assembly-unification warnings the .NET SDK suppresses by default (NCbor.dll is
            // compiled against netstandard2.0 and binds up to the net10.0 reference assemblies).
            .WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>
            {
                ["CS1701"] = ReportDiagnostic.Suppress,
                ["CS1702"] = ReportDiagnostic.Suppress,
            });

    /// <summary>
    /// net10.0 reference assemblies, plus NCbor (for the attributes / base classes the generator
    /// looks up by metadata name) and System.Formats.Cbor (which the generated code calls into).
    /// </summary>
    public static readonly ImmutableArray<MetadataReference> References =
        Basic.Reference.Assemblies.Net100.References.All
            .Cast<MetadataReference>()
            .Concat(new[]
            {
                MetadataReference.CreateFromFile(typeof(NCborSerializerContext).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(CborWriter).Assembly.Location),
            })
            .ToImmutableArray();

    /// <summary>
    /// Runs the generator over <paramref name="source"/> (plus any <paramref name="additionalSources"/>)
    /// and returns everything observable about the run. Never throws for generator or compile
    /// failures — inspect the result, or use <see cref="AssertCompilesCleanly"/>.
    /// </summary>
    public static GeneratorResult Run(string source, params string[] additionalSources)
    {
        var inputTrees = new[] { source }
            .Concat(additionalSources)
            .Select((text, i) => CSharpSyntaxTree.ParseText(text, ParseOptions, path: $"Input{i}.cs"))
            .ToImmutableArray();

        var inputCompilation = CSharpCompilation.Create(
            "NCborGeneratorTestAssembly",
            inputTrees,
            References,
            CompilationOptions);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { new NCborSourceGenerator().AsSourceGenerator() },
            parseOptions: ParseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out var generatorDiagnostics);
        var runResult = driver.GetRunResult();

        var generated = runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .ToImmutableSortedDictionary(s => s.HintName, s => s.SourceText.ToString(), StringComparer.Ordinal);

        var generatedTrees = runResult.GeneratedTrees.ToImmutableHashSet();
        var exception = runResult.Results.Select(r => r.Exception).FirstOrDefault(e => e != null);

        return new GeneratorResult(
            inputCompilation,
            outputCompilation,
            generatorDiagnostics,
            generated,
            generatedTrees,
            outputCompilation.GetDiagnostics(),
            exception);
    }

    /// <summary>
    /// Runs the generator and asserts that it produced at least one source file, threw nothing,
    /// reported no Error-severity diagnostics of its own, and that the final compilation (user
    /// source + generated source) has zero errors. Warnings are allowed. On failure the message
    /// includes the offending diagnostics and every generated file.
    /// </summary>
    public static GeneratorResult AssertCompilesCleanly(string source, params string[] additionalSources)
    {
        var result = Run(source, additionalSources);
        result.AssertCompilesCleanly();
        return result;
    }
}

/// <summary>Everything observable about one generator run. See <see cref="GeneratorHarness.Run"/>.</summary>
public sealed class GeneratorResult
{
    private readonly ImmutableHashSet<SyntaxTree> _generatedTrees;

    internal GeneratorResult(
        Compilation inputCompilation,
        Compilation outputCompilation,
        ImmutableArray<Diagnostic> generatorDiagnostics,
        ImmutableSortedDictionary<string, string> generatedSources,
        ImmutableHashSet<SyntaxTree> generatedTrees,
        ImmutableArray<Diagnostic> compilationDiagnostics,
        Exception? generatorException)
    {
        InputCompilation = inputCompilation;
        OutputCompilation = outputCompilation;
        GeneratorDiagnostics = generatorDiagnostics;
        GeneratedSources = generatedSources;
        _generatedTrees = generatedTrees;
        CompilationDiagnostics = compilationDiagnostics;
        GeneratorException = generatorException;
    }

    /// <summary>The compilation before the generator ran (user source only).</summary>
    public Compilation InputCompilation { get; }

    /// <summary>The compilation after generated sources were added.</summary>
    public Compilation OutputCompilation { get; }

    /// <summary>Diagnostics reported by the generator itself (CBORxxx), not by the compiler.</summary>
    public ImmutableArray<Diagnostic> GeneratorDiagnostics { get; }

    /// <summary>Generated sources, keyed by hint name. Prefer <see cref="AllGeneratedText"/> in
    /// assertions so tests don't depend on the generator's file-naming scheme.</summary>
    public IReadOnlyDictionary<string, string> GeneratedSources { get; }

    /// <summary>Every generated source concatenated (hint-name order).</summary>
    public string AllGeneratedText => string.Join("\n", GeneratedSources.Values);

    /// <summary>All compiler diagnostics (any severity) of <see cref="OutputCompilation"/>.</summary>
    public ImmutableArray<Diagnostic> CompilationDiagnostics { get; }

    /// <summary>Error-severity compiler diagnostics of <see cref="OutputCompilation"/>, wherever located.</summary>
    public ImmutableArray<Diagnostic> CompilationErrors =>
        CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();

    /// <summary>Error-severity compiler diagnostics located inside generated code.</summary>
    public ImmutableArray<Diagnostic> GeneratedCodeErrors =>
        CompilationErrors.Where(IsInGeneratedCode).ToImmutableArray();

    /// <summary>Warning-severity compiler diagnostics located inside generated code.</summary>
    public ImmutableArray<Diagnostic> GeneratedCodeWarnings =>
        CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning && IsInGeneratedCode(d)).ToImmutableArray();

    /// <summary>An exception thrown by the generator, if any. Roslyn otherwise only surfaces
    /// this as warning CS8785 with no generated output.</summary>
    public Exception? GeneratorException { get; }

    /// <summary>Generator diagnostics with the given ID (e.g. "CBOR005").</summary>
    public ImmutableArray<Diagnostic> DiagnosticsWithId(string id) =>
        GeneratorDiagnostics.Where(d => d.Id == id).ToImmutableArray();

    public bool IsInGeneratedCode(Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree is { } tree && _generatedTrees.Contains(tree);

    /// <summary>See <see cref="GeneratorHarness.AssertCompilesCleanly"/>.</summary>
    public GeneratorResult AssertCompilesCleanly()
    {
        var problems = new List<string>();
        if (GeneratorException != null)
            problems.Add($"Generator threw: {GeneratorException}");
        if (GeneratedSources.Count == 0)
            problems.Add("Generator produced no sources (is the context partial, deriving NCborSerializerContext, with [NCborSerializable(typeof(...))]?).");

        var generatorErrors = GeneratorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (generatorErrors.Count > 0)
            problems.Add("Generator reported errors:\n" + FormatDiagnostics(generatorErrors));
        if (CompilationErrors.Length > 0)
            problems.Add("Compilation errors:\n" + FormatDiagnostics(CompilationErrors));

        if (problems.Count > 0)
            throw new Xunit.Sdk.XunitException(string.Join("\n\n", problems) + "\n\n" + DumpGeneratedSources());

        return this;
    }

    /// <summary>All generated sources with line numbers, for failure messages.</summary>
    public string DumpGeneratedSources()
    {
        var sb = new StringBuilder();
        foreach (var (hint, text) in GeneratedSources)
        {
            sb.AppendLine($"===== {hint} =====");
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
                sb.AppendLine($"{i + 1,5}: {lines[i].TrimEnd('\r')}");
        }
        return sb.ToString();
    }

    public string FormatDiagnostics(IEnumerable<Diagnostic> diagnostics) =>
        string.Join("\n", diagnostics.Select(d =>
        {
            var where = d.Location.SourceTree is not null
                ?(IsInGeneratedCode(d) ? "[generated] " : "") + d.Location.GetLineSpan()
                : d.Location.ToString();
            return $"  {d.Id} {d.Severity} at {where}: {d.GetMessage()}";
        }));

    public override string ToString() =>
        "Generator diagnostics:\n" + FormatDiagnostics(GeneratorDiagnostics.Where(d => d.Severity > DiagnosticSeverity.Info)) +
        "\nCompilation errors:\n" + FormatDiagnostics(CompilationErrors);
}
