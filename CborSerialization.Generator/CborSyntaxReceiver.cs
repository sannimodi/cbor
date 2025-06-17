using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CborSerialization.Generator;

/// <summary>
/// Legacy syntax receiver implementation for older source generator patterns.
/// This class is kept for reference but is not currently used.
/// The current implementation uses IIncrementalGenerator in CborSourceGenerator.
/// </summary>
[System.Obsolete("This class is not used by the current implementation. Consider removing in future versions.")]
internal class CborSyntaxReceiver : ISyntaxReceiver
{
    public List<ClassDeclarationSyntax> ContextClasses { get; } = new();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        // Look for class declarations
        if (syntaxNode is ClassDeclarationSyntax classDeclaration)
        {
            // Check if the class has any CborSerializable attributes
            if (HasCborSerializableAttribute(classDeclaration))
            {
                ContextClasses.Add(classDeclaration);
            }
        }
    }

    private static bool HasCborSerializableAttribute(ClassDeclarationSyntax classDeclaration)
    {
        foreach (var attributeList in classDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var name = attribute.Name.ToString();
                if (name == "CborSerializable" || name.EndsWith(".CborSerializable"))
                {
                    return true;
                }
            }
        }
        return false;
    }
} 