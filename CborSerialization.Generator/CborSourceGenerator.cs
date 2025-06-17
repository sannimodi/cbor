using System.Text;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CborSerialization.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class CborSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register for syntax notifications
        var syntaxProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is ClassDeclarationSyntax c && c.AttributeLists.Count > 0,
                transform: (context, _) => context.Node as ClassDeclarationSyntax)
            .Where(node => node != null);

        // Combine with compilation
        var compilationAndClasses = context.CompilationProvider.Combine(syntaxProvider.Collect());

        // Register the source generator
        context.RegisterSourceOutput(compilationAndClasses, (spc, source) => GenerateSource(spc, source.Left, source.Right));
    }

    private void GenerateSource(SourceProductionContext context, Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes)
    {
        // Get the CborSerializable attribute symbol
        var cborSerializableAttributeSymbol = compilation.GetTypeByMetadataName("CborSerialization.CborSerializableAttribute");
        if (cborSerializableAttributeSymbol == null)
            return;

        // Process each context class
        foreach (var contextClass in classes)
        {
            var model = compilation.GetSemanticModel(contextClass.SyntaxTree);
            var typeSymbol = model.GetDeclaredSymbol(contextClass);
            if (typeSymbol == null)
                continue;

            // Get the CborSerializable attributes
            var serializableAttributes = typeSymbol.GetAttributes()
                .Where(attr => attr.AttributeClass?.Equals(cborSerializableAttributeSymbol, SymbolEqualityComparer.Default) == true)
                .ToList();

            if (!serializableAttributes.Any())
                continue;

            // Generate the source code
            var source = GenerateContextSource(typeSymbol, serializableAttributes);
            context.AddSource($"{typeSymbol.Name}.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    private string GenerateContextSource(INamedTypeSymbol contextType, List<AttributeData> serializableAttributes)
    {
        var builder = new StringBuilder();
        var namespaceName = contextType.ContainingNamespace.ToDisplayString();

        // Add using statements
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Formats.Cbor;");
        builder.AppendLine("using CborSerialization;");
        builder.AppendLine();

        // Add namespace
        builder.AppendLine($"namespace {namespaceName};");
        builder.AppendLine();

        // Add partial class declaration
        builder.AppendLine($"public partial class {contextType.Name}");
        builder.AppendLine("{");

        // Add Default property
        builder.AppendLine("    public static CborSerializerContext Default { get; } = new();");
        builder.AppendLine();

        // Add GetTypeInfo implementation
        builder.AppendLine("    public override CborTypeInfo<T> GetTypeInfo<T>()");
        builder.AppendLine("    {");
        builder.AppendLine("        return typeof(T) switch");
        builder.AppendLine("        {");

        foreach (var attr in serializableAttributes)
        {
            if (attr.ConstructorArguments.Length == 0)
                continue;

            var typeArg = attr.ConstructorArguments[0];
            if (typeArg.Value is not INamedTypeSymbol typeSymbol)
                continue;

            var typeName = typeSymbol.ToDisplayString();
            var propertyName = GetPropertyName(typeSymbol);

            builder.AppendLine($"            Type t when t == typeof({typeName}) => (CborTypeInfo<T>)(object){propertyName},");
        }

        builder.AppendLine("            _ => throw new ArgumentException($\"Type {typeof(T)} is not registered for serialization\")");
        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine();

        // Add type info properties and methods for each serializable type
        foreach (var attr in serializableAttributes)
        {
            if (attr.ConstructorArguments.Length == 0)
                continue;

            var typeArg = attr.ConstructorArguments[0];
            if (typeArg.Value is not INamedTypeSymbol typeSymbol)
                continue;

            var typeName = typeSymbol.ToDisplayString();
            var propertyName = GetPropertyName(typeSymbol);

            // Add type info property
            builder.AppendLine($"    public CborTypeInfo<{typeName}> {propertyName} {{ get; }}");
            builder.AppendLine();

            // Add serialization methods
            builder.AppendLine($"    private static void Serialize{propertyName}(CborWriter writer, {typeName} value)");
            builder.AppendLine("    {");
            builder.AppendLine(SerializationCodeGenerator.GenerateSerializationCode(typeSymbol));
            builder.AppendLine("    }");
            builder.AppendLine();

            builder.AppendLine($"    private static {typeName} Deserialize{propertyName}(ref CborReader reader)");
            builder.AppendLine("    {");
            builder.AppendLine(SerializationCodeGenerator.GenerateDeserializationCode(typeSymbol));
            builder.AppendLine("    }");
            builder.AppendLine();
        }

        builder.AppendLine("}");

        return builder.ToString();
    }

    private string GetPropertyName(INamedTypeSymbol typeSymbol)
    {
        var name = typeSymbol.Name;
        if (typeSymbol.IsGenericType)
        {
            name = name.Substring(0, name.IndexOf('`'));
            name += string.Join("", typeSymbol.TypeArguments.Select(t => t.Name));
        }
        return name;
    }
}