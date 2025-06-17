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
        sb.AppendLine("        public static byte[] Serialize<T>(T value, CborTypeInfo<T> typeInfo)");
        sb.AppendLine("        {");
        sb.AppendLine("            var writer = new CborWriter();");
        sb.AppendLine($"            if (typeof(T).Name == \"{className}\")");
        sb.AppendLine("            {");
        sb.AppendLine("                dynamic proxy = value;");
        sb.AppendLine("                writer.WriteStartMap(null);");
        
        foreach (var prop in properties)
        {
            var propName = prop.Name;
            var cborPropAttr = prop.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "CborSerialization.CborPropertyAttribute");
            
            if (cborPropAttr != null)
            {
                propName = (string)cborPropAttr.ConstructorArguments[0].Value!;
            }

            sb.AppendLine($"                writer.WriteTextString(\"{propName}\");");
            sb.AppendLine($"                writer.Write{GetCborTypeName(prop.Type)}(proxy.{prop.Name});");
        }

        sb.AppendLine("                writer.WriteEndMap();");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                throw new NotImplementedException(\"Serialization is not implemented for this type.\");");
        sb.AppendLine("            }");
        sb.AppendLine("            return writer.Encode();");
        sb.AppendLine("        }");

        // Generate Deserialize method
        sb.AppendLine("        public static T Deserialize<T>(byte[] cborData, CborTypeInfo<T> typeInfo)");
        sb.AppendLine("        {");
        sb.AppendLine("            var reader = new CborReader(cborData);");
        sb.AppendLine($"            if (typeof(T).Name == \"{className}\")");
        sb.AppendLine("            {");
        sb.AppendLine("                reader.ReadStartMap();");
        
        foreach (var prop in properties)
        {
            var propName = prop.Name;
            var cborPropAttr = prop.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "CborSerialization.CborPropertyAttribute");
            
            if (cborPropAttr != null)
            {
                propName = (string)cborPropAttr.ConstructorArguments[0].Value!;
            }

            sb.AppendLine($"                string {prop.Name.ToLower()}PropertyName = reader.ReadTextString();");
            sb.AppendLine($"                {GetCSharpTypeName(prop.Type)} {prop.Name.ToLower()}PropertyValue = reader.Read{GetCborTypeName(prop.Type)}();");
        }

        sb.AppendLine("                reader.ReadEndMap();");
        sb.AppendLine("                dynamic result = Activator.CreateInstance(typeof(T));");
        
        foreach (var prop in properties)
        {
            sb.AppendLine($"                result.{prop.Name} = {prop.Name.ToLower()}PropertyValue;");
        }

        sb.AppendLine("                return (T)result;");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                throw new NotImplementedException(\"Deserialization is not implemented for this type.\");");
        sb.AppendLine("            }");
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

    private string GetCSharpTypeName(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_String => "string",
            SpecialType.System_Int32 => "int",
            SpecialType.System_Int64 => "long",
            SpecialType.System_Double => "double",
            SpecialType.System_Boolean => "bool",
            _ => "string" // Default to string for unknown types
        };
    }
}
