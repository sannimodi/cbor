using System.Text;
using Microsoft.CodeAnalysis;

namespace CborSerialization.Generator;

internal static class SerializationCodeGenerator
{
    public static string GenerateSerializationCode(INamedTypeSymbol typeSymbol, string contextRef = "this")
    {
        var builder = new StringBuilder();
        if (IsList(typeSymbol, out var elementType))
        {
            if (elementType == null)
                throw new InvalidOperationException("Element type cannot be null for a valid list type.");
                
            builder.AppendLine("writer.WriteStartArray(value.Count);");
            builder.AppendLine($"foreach (var item in value)");
            builder.AppendLine("{");
            builder.AppendLine($"    // Use the parent context's type info for the element type");
            builder.AppendLine($"    {contextRef}.{GetPropertyNameFromType(elementType)}.Serialize(writer, item);");
            builder.AppendLine("}");
            builder.AppendLine("writer.WriteEndArray();");
            return builder.ToString();
        }
        var properties = GetSerializableProperties(typeSymbol);
        builder.AppendLine("writer.WriteStartMap(null);");
        foreach (var property in properties)
        {
            var propertyName = GetPropertyName(property);
            var propertyType = property.Type;
            builder.AppendLine($"writer.WriteTextString(\"{propertyName}\");");
            builder.AppendLine(GeneratePropertySerialization(property));
        }
        builder.AppendLine("writer.WriteEndMap();");
        return builder.ToString();
    }

    public static string GenerateDeserializationCode(INamedTypeSymbol typeSymbol, string contextRef = "this")
    {
        var builder = new StringBuilder();
        if (IsList(typeSymbol, out var elementType))
        {
            if (elementType == null)
                throw new InvalidOperationException("Element type cannot be null for a valid list type.");
                
            builder.AppendLine($"var list = new System.Collections.Generic.List<{elementType.ToDisplayString()}>();");
            builder.AppendLine("int? length = reader.ReadStartArray();");
            builder.AppendLine("for (int i = 0; length == null || i < length; i++)");
            builder.AppendLine("{");
            builder.AppendLine($"    // Use the parent context's type info for the element type");
            builder.AppendLine($"    var item = {contextRef}.{GetPropertyNameFromType(elementType)}.Deserialize(reader);");
            builder.AppendLine("    list.Add(item);");
            builder.AppendLine("    if (length == null && reader.PeekState() == CborReaderState.EndArray) break;");
            builder.AppendLine("}");
            builder.AppendLine("reader.ReadEndArray();");
            builder.AppendLine("return list;");
            return builder.ToString();
        }
        var properties = GetSerializableProperties(typeSymbol);
        builder.AppendLine($"var result = new {typeSymbol.ToDisplayString()}();");
        builder.AppendLine("reader.ReadStartMap();");
        builder.AppendLine("while (reader.PeekState() != CborReaderState.EndMap)");
        builder.AppendLine("{");
        builder.AppendLine("    var propertyName = reader.ReadTextString();");
        builder.AppendLine("    switch (propertyName)");
        builder.AppendLine("    {");
        foreach (var property in properties)
        {
            var propertyName = GetPropertyName(property);
            builder.AppendLine($"        case \"{propertyName}\":");
            builder.AppendLine($"            result.{property.Name} = {GeneratePropertyDeserialization(property)};");
            builder.AppendLine("            break;");
        }
        builder.AppendLine("        default:");
        builder.AppendLine("            reader.SkipValue();");
        builder.AppendLine("            break;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine("reader.ReadEndMap();");
        builder.AppendLine("return result;");
        return builder.ToString();
    }

    private static IEnumerable<IPropertySymbol> GetSerializableProperties(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic && p.GetMethod != null && p.SetMethod != null)
            .Where(p => !HasIgnoreAttribute(p));
    }

    private static bool HasIgnoreAttribute(IPropertySymbol property)
    {
        return property.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() == "CborSerialization.CborIgnoreAttribute");
    }

    private static string GetPropertyName(IPropertySymbol property)
    {
        var nameAttr = property.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == "CborSerialization.CborPropertyNameAttribute");

        if (nameAttr != null && nameAttr.ConstructorArguments.Length > 0)
        {
            return nameAttr.ConstructorArguments[0].Value?.ToString() ?? property.Name;
        }

        return property.Name;
    }

    private static string GeneratePropertySerialization(IPropertySymbol property)
    {
        var type = property.Type;
        return type.SpecialType switch
        {
            SpecialType.System_String => $"writer.WriteTextString(value.{property.Name});",
            SpecialType.System_Int32 => $"writer.WriteInt32(value.{property.Name});",
            SpecialType.System_Boolean => $"writer.WriteBoolean(value.{property.Name});",
            SpecialType.System_Double => $"writer.WriteDouble(value.{property.Name});",
            SpecialType.System_Single => $"writer.WriteSingle(value.{property.Name});",
            SpecialType.System_Int64 => $"writer.WriteInt64(value.{property.Name});",
            SpecialType.System_UInt32 => $"writer.WriteUInt32(value.{property.Name});",
            SpecialType.System_UInt64 => $"writer.WriteUInt64(value.{property.Name});",
            SpecialType.System_Byte => $"writer.WriteUInt32(value.{property.Name});",
            SpecialType.System_SByte => $"writer.WriteInt32(value.{property.Name});",
            SpecialType.System_Int16 => $"writer.WriteInt32(value.{property.Name});",
            SpecialType.System_UInt16 => $"writer.WriteUInt32(value.{property.Name});",
            _ => $"// TODO: Implement serialization for {type.ToDisplayString()}"
        };
    }

    private static string GeneratePropertyDeserialization(IPropertySymbol property)
    {
        var type = property.Type;
        return type.SpecialType switch
        {
            SpecialType.System_String => "reader.ReadTextString()",
            SpecialType.System_Int32 => "reader.ReadInt32()",
            SpecialType.System_Boolean => "reader.ReadBoolean()",
            SpecialType.System_Double => "reader.ReadDouble()",
            SpecialType.System_Single => "reader.ReadSingle()",
            SpecialType.System_Int64 => "reader.ReadInt64()",
            SpecialType.System_UInt32 => "reader.ReadUInt32()",
            SpecialType.System_UInt64 => "reader.ReadUInt64()",
            SpecialType.System_Byte => "(byte)reader.ReadUInt32()",
            SpecialType.System_SByte => "(sbyte)reader.ReadInt32()",
            SpecialType.System_Int16 => "(short)reader.ReadInt32()",
            SpecialType.System_UInt16 => "(ushort)reader.ReadUInt32()",
            _ => $"// TODO: Implement deserialization for {type.ToDisplayString()}"
        };
    }

    private static bool IsList(INamedTypeSymbol typeSymbol, out ITypeSymbol? elementType)
    {
        elementType = null;
        if (typeSymbol is { IsGenericType: true } && typeSymbol.Name == "List" && typeSymbol.TypeArguments.Length == 1)
        {
            elementType = typeSymbol.TypeArguments[0];
            return true;
        }
        return false;
    }

    private static string GetPropertyNameFromType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var name = namedType.Name;
            int backtickIndex = name.IndexOf('`');
            if (backtickIndex >= 0)
                name = name.Substring(0, backtickIndex);
            name += "Of" + string.Join("And", namedType.TypeArguments.Select(t => GetPropertyNameFromType(t)));
            return name;
        }
        return typeSymbol.Name;
    }
} 