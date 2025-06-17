using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CborSerialization.Generator;

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