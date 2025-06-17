using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace CborSerialization.Generator;

[Generator]
public class CborSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all classes marked with [CborSerializable]
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (s, _) => s is ClassDeclarationSyntax,
                transform: (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
            .Where(cls => cls.AttributeLists.Count > 0);

        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndClasses, (spc, source) =>
        {
            var (compilation, classes) = source;
            var cborSerializableAttr = compilation.GetTypeByMetadataName("CborSerialization.CborSerializableAttribute");
            if (cborSerializableAttr == null) return;

            foreach (var classNode in classes)
            {
                var model = compilation.GetSemanticModel(classNode.SyntaxTree);
                var symbol = model.GetDeclaredSymbol(classNode);
                if (symbol == null) continue;

                var hasAttr = symbol.GetAttributes().Any(a => 
                    SymbolEqualityComparer.Default.Equals(a.AttributeClass, cborSerializableAttr));
                if (!hasAttr) continue;

                if (symbol is INamedTypeSymbol namedType)
                {
                    GenerateSerializationCode(spc, namedType);
                }
            }
        });
    }

    private void GenerateSerializationCode(SourceProductionContext context, INamedTypeSymbol classSymbol)
    {
        var className = classSymbol.Name;
        var ns = classSymbol.ContainingNamespace.ToDisplayString();
        var properties = classSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public)
            .Where(p => !p.GetAttributes().Any(a => 
                a.AttributeClass?.ToDisplayString() == "CborSerialization.CborIgnoreAttribute"));

        var sb = new StringBuilder();
        sb.AppendLine($"namespace {ns}");
        sb.AppendLine("{");
        sb.AppendLine($"    partial class {className}");
        sb.AppendLine("    {");
        sb.AppendLine("        public static byte[] Serialize(CborWriter writer)");
        sb.AppendLine("        {");
        sb.AppendLine("            writer.WriteStartMap(null);");
        
        foreach (var prop in properties)
        {
            var propName = prop.Name;
            var cborPropAttr = prop.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "CborSerialization.CborPropertyAttribute");
            
            if (cborPropAttr != null)
            {
                propName = (string)cborPropAttr.ConstructorArguments[0].Value!;
            }

            sb.AppendLine($"            writer.WriteTextString(\"{propName}\");");
            sb.AppendLine($"            writer.Write{GetCborTypeName(prop.Type)}({prop.Name});");
        }

        sb.AppendLine("            writer.WriteEndMap();");
        sb.AppendLine("            return writer.Encode();");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource($"{className}.CborSerialization.g.cs", sb.ToString());
    }

    private string GetCborTypeName(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_String => "TextString",
            SpecialType.System_Int32 => "Int32",
            SpecialType.System_Int64 => "Int64",
            SpecialType.System_Double => "Double",
            SpecialType.System_Boolean => "Boolean",
            _ => "TextString" // Default to string for unknown types
        };
    }
}
