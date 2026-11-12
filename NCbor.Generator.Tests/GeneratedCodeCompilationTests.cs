namespace NCbor.Generator.Tests;

/// <summary>
/// Asserts that generated code compiles, and sanity-checks the harness itself.
/// </summary>
public class GeneratedCodeCompilationTests
{
    [Fact]
    public void SimpleModel_GeneratesCodeThatCompilesWithZeroErrors()
    {
        var result = GeneratorHarness.AssertCompilesCleanly("""
            using System;
            using System.Collections.Generic;
            using NCbor;

            namespace Models
            {
                public enum Status { Active, Inactive }

                public class Person
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                    public string? Nickname { get; set; }
                    public double Score { get; set; }
                    public bool IsActive { get; set; }
                    public DateTime CreatedAt { get; set; }
                    public Guid ExternalId { get; set; }
                    public decimal Balance { get; set; }
                    public Status Status { get; set; }
                    public int? Age { get; set; }
                    public byte[]? Avatar { get; set; }
                    public List<string> Tags { get; set; } = new();
                    public Dictionary<string, int> Counters { get; set; } = new();
                }

                // Today, collection-typed properties require their collection type to be
                // registered on the context too (see KnownBugTests for the nested case).
                [NCborSerializable(typeof(Person))]
                [NCborSerializable(typeof(List<string>))]
                [NCborSerializable(typeof(Dictionary<string, int>))]
                public partial class PersonContext : NCborSerializerContext { }
            }
            """);

        result.GeneratedCodeErrors.Should().BeEmpty();
        result.AllGeneratedText.Should().Contain("class PersonContext");
        result.AllGeneratedText.Should().Contain("\"Name\"");
    }

    [Fact]
    public void Harness_AssertCompilesCleanly_FailsWhenNothingIsGenerated()
    {
        // Guards against vacuous passes: a source with no registered context generates nothing,
        // which must not count as "compiles cleanly".
        var act = () => GeneratorHarness.AssertCompilesCleanly("""
            namespace Models
            {
                public class NotRegistered { public int X { get; set; } }
            }
            """);

        act.Should().Throw<Xunit.Sdk.XunitException>().WithMessage("*produced no sources*");
    }

    [Fact]
    public void Harness_AssertCompilesCleanly_FailsOnGeneratorErrors()
    {
        // CBOR007 is a generator-reported Error that never shows up in the output compilation's
        // diagnostics; the helper must still fail on it.
        var act = () => GeneratorHarness.AssertCompilesCleanly("""
            using NCbor;

            namespace Models
            {
                public class Ambiguous
                {
                    public Ambiguous() { }
                    [NCborConstructor] public Ambiguous(int a) { A = a; }
                    [NCborConstructor] public Ambiguous(int a, int b) { A = a; }
                    public int A { get; set; }
                }

                [NCborSerializable(typeof(Ambiguous))]
                public partial class AmbiguousContext : NCborSerializerContext { }
            }
            """);

        act.Should().Throw<Xunit.Sdk.XunitException>().WithMessage("*CBOR007*");
    }

    [Fact]
    public void Harness_AssertCompilesCleanly_FailsOnCompileErrors()
    {
        // A registered context generates code, but the user's own source has a compile error.
        var act = () => GeneratorHarness.AssertCompilesCleanly("""
            using NCbor;

            namespace Models
            {
                public class Broken { public int X { get; set; } = "not an int"; }

                [NCborSerializable(typeof(Broken))]
                public partial class BrokenContext : NCborSerializerContext { }
            }
            """);

        act.Should().Throw<Xunit.Sdk.XunitException>().WithMessage("*Compilation errors*CS0029*");
    }

    [Fact]
    public void Harness_ReportsUserSourceCompileErrors()
    {
        var result = GeneratorHarness.Run("""
            namespace Models
            {
                public class Broken { public int X { get; set; } = "not an int"; }
            }
            """);

        result.CompilationErrors.Should().Contain(d => d.Id == "CS0029");
        result.GeneratedCodeErrors.Should().BeEmpty();
    }
}
