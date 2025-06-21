namespace CbOrSerialization.Generator;

internal static class SerializationCodeGenerator
{
    public static string GenerateSerializationCode(INamedTypeSymbol typeSymbol, CbOrKnownNamingPolicy namingPolicy, string contextRef = "this")
    {
        var builder = new StringBuilder();
        if (IsList(typeSymbol, out var elementType))
        {
            if (elementType == null)
                throw new InvalidOperationException("Element type cannot be null for a valid list type.");
                
            builder.AppendLine("writer.WriteStartArray(value.Count);");
            builder.AppendLine($"foreach (var item in value)");
            builder.AppendLine("{");
            
            // Handle built-in types directly, complex types via context
            if (IsBuiltInType(elementType))
            {
                builder.AppendLine($"    {GenerateDirectSerialization("item", elementType)}");
            }
            else
            {
                builder.AppendLine($"    // Use the parent context's type info for the element type");
                builder.AppendLine($"    {contextRef}.{GetPropertyNameFromType(elementType)}.Serialize(writer, item);");
            }
            builder.AppendLine("}");
            builder.AppendLine("writer.WriteEndArray();");
            return builder.ToString();
        }
        if (IsArray(typeSymbol, out var arrayElementType))
        {
            if (arrayElementType == null)
                throw new InvalidOperationException("Element type cannot be null for a valid array type.");
                
            builder.AppendLine("writer.WriteStartArray(value.Length);");
            builder.AppendLine($"foreach (var item in value)");
            builder.AppendLine("{");
            
            // Handle built-in types directly, complex types via context
            if (IsBuiltInType(arrayElementType))
            {
                builder.AppendLine($"    {GenerateDirectSerialization("item", arrayElementType)}");
            }
            else
            {
                builder.AppendLine($"    // Use the parent context's type info for the element type");
                builder.AppendLine($"    {contextRef}.{GetPropertyNameFromType(arrayElementType)}.Serialize(writer, item);");
            }
            builder.AppendLine("}");
            builder.AppendLine("writer.WriteEndArray();");
            return builder.ToString();
        }
        if (IsDictionary(typeSymbol, out var keyType, out var valueType))
        {
            if (keyType == null || valueType == null)
                throw new InvalidOperationException("Key and value types cannot be null for a valid dictionary type.");
                
            builder.AppendLine("writer.WriteStartMap(value.Count);");
            builder.AppendLine($"foreach (var kvp in value)");
            builder.AppendLine("{");
            
            // Serialize key
            builder.AppendLine("    // Serialize key");
            if (IsBuiltInType(keyType))
            {
                builder.AppendLine($"    {GenerateDirectSerialization("kvp.Key", keyType)}");
            }
            else
            {
                builder.AppendLine($"    {contextRef}.{GetPropertyNameFromType(keyType)}.Serialize(writer, kvp.Key);");
            }
            
            // Serialize value
            builder.AppendLine("    // Serialize value");
            if (IsBuiltInType(valueType))
            {
                builder.AppendLine($"    {GenerateDirectSerialization("kvp.Value", valueType)}");
            }
            else
            {
                builder.AppendLine($"    {contextRef}.{GetPropertyNameFromType(valueType)}.Serialize(writer, kvp.Value);");
            }
            
            builder.AppendLine("}");
            builder.AppendLine("writer.WriteEndMap();");
            return builder.ToString();
        }
        var properties = GetSerializableProperties(typeSymbol);
        builder.AppendLine("writer.WriteStartMap(null);");
        foreach (var property in properties)
        {
            var propertyName = GetPropertyName(property, namingPolicy);
            var propertyType = property.Type;
            builder.AppendLine($"writer.WriteTextString(\"{propertyName}\");");
            builder.AppendLine(GeneratePropertySerialization(property));
        }
        builder.AppendLine("writer.WriteEndMap();");
        return builder.ToString();
    }

    public static string GenerateDeserializationCode(INamedTypeSymbol typeSymbol, CbOrKnownNamingPolicy namingPolicy, string contextRef = "this")
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
            builder.AppendLine("    if (length == null && reader.PeekState() == CborReaderState.EndArray)");
            builder.AppendLine("        break;");
            
            // Handle built-in types directly, complex types via context
            if (IsBuiltInType(elementType))
            {
                builder.AppendLine($"    var item = {GenerateDirectDeserialization(elementType)};");
            }
            else
            {
                builder.AppendLine($"    // Use the parent context's type info for the element type");
                builder.AppendLine($"    var item = {contextRef}.{GetPropertyNameFromType(elementType)}.Deserialize(reader);");
            }
            builder.AppendLine("    list.Add(item);");
            builder.AppendLine("}");
            builder.AppendLine("reader.ReadEndArray();");
            builder.AppendLine("return list;");
            return builder.ToString();
        }
        if (IsArray(typeSymbol, out var arrayElementType))
        {
            if (arrayElementType == null)
                throw new InvalidOperationException("Element type cannot be null for a valid array type.");
                
            builder.AppendLine($"var list = new System.Collections.Generic.List<{arrayElementType.ToDisplayString()}>();");
            builder.AppendLine("int? length = reader.ReadStartArray();");
            builder.AppendLine("for (int i = 0; length == null || i < length; i++)");
            builder.AppendLine("{");
            builder.AppendLine("    if (length == null && reader.PeekState() == CborReaderState.EndArray)");
            builder.AppendLine("        break;");
            
            // Handle built-in types directly, complex types via context
            if (IsBuiltInType(arrayElementType))
            {
                builder.AppendLine($"    var item = {GenerateDirectDeserialization(arrayElementType)};");
            }
            else
            {
                builder.AppendLine($"    // Use the parent context's type info for the element type");
                builder.AppendLine($"    var item = {contextRef}.{GetPropertyNameFromType(arrayElementType)}.Deserialize(reader);");
            }
            builder.AppendLine("    list.Add(item);");
            builder.AppendLine("}");
            builder.AppendLine("reader.ReadEndArray();");
            builder.AppendLine("return list.ToArray();");
            return builder.ToString();
        }
        if (IsDictionary(typeSymbol, out var keyType, out var valueType))
        {
            if (keyType == null || valueType == null)
                throw new InvalidOperationException("Key and value types cannot be null for a valid dictionary type.");
                
            builder.AppendLine($"var dictionary = new System.Collections.Generic.Dictionary<{keyType.ToDisplayString()}, {valueType.ToDisplayString()}>();");
            builder.AppendLine("int? mapSize = reader.ReadStartMap();");
            builder.AppendLine("for (int i = 0; mapSize == null || i < mapSize; i++)");
            builder.AppendLine("{");
            builder.AppendLine("    if (mapSize == null && reader.PeekState() == CborReaderState.EndMap)");
            builder.AppendLine("        break;");
            
            // Deserialize key
            builder.AppendLine("    // Deserialize key");
            if (IsBuiltInType(keyType))
            {
                builder.AppendLine($"    var key = {GenerateDirectDeserialization(keyType)};");
            }
            else
            {
                builder.AppendLine($"    var key = {contextRef}.{GetPropertyNameFromType(keyType)}.Deserialize(reader);");
            }
            
            // Deserialize value
            builder.AppendLine("    // Deserialize value");
            if (IsBuiltInType(valueType))
            {
                builder.AppendLine($"    var value = {GenerateDirectDeserialization(valueType)};");
            }
            else
            {
                builder.AppendLine($"    var value = {contextRef}.{GetPropertyNameFromType(valueType)}.Deserialize(reader);");
            }
            
            builder.AppendLine("    dictionary.Add(key, value);");
            builder.AppendLine("}");
            builder.AppendLine("reader.ReadEndMap();");
            builder.AppendLine("return dictionary;");
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
            var propertyName = GetPropertyName(property, namingPolicy);
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
            .Any(attr => attr.AttributeClass?.ToDisplayString() == "CbOrSerialization.CbOrIgnoreAttribute");
    }

    private static string GetPropertyName(IPropertySymbol property, CbOrKnownNamingPolicy namingPolicy)
    {
        var nameAttr = property.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == "CbOrSerialization.CbOrPropertyNameAttribute");

        if (nameAttr != null && nameAttr.ConstructorArguments.Length > 0)
        {
            return nameAttr.ConstructorArguments[0].Value?.ToString() ?? property.Name;
        }

        return ApplyNamingPolicy(property.Name, namingPolicy);
    }

    private static string GeneratePropertySerialization(IPropertySymbol property)
    {
        var type = property.Type;
        
        // Handle built-in types directly
        if (IsBuiltInType(type))
        {
            return GenerateDirectSerialization($"value.{property.Name}", type);
        }
        
        // Handle arrays (including nullable arrays)
        if (IsArray(type, out var arrayElementType))
        {
            if (arrayElementType == null)
                throw new InvalidOperationException("Element type cannot be null for a valid array type.");
                
            return $"_context.{GetPropertyNameFromType(type)}.Serialize(writer, value.{property.Name});";
        }
        
        // Handle nullable arrays
        if (type.CanBeReferencedByName && type.NullableAnnotation == Microsoft.CodeAnalysis.NullableAnnotation.Annotated && 
            IsArray(type.WithNullableAnnotation(Microsoft.CodeAnalysis.NullableAnnotation.NotAnnotated), out var nullableArrayElementType))
        {
            if (nullableArrayElementType == null)
                throw new InvalidOperationException("Element type cannot be null for a valid nullable array type.");
                
            return $"if (value.{property.Name} != null) {{ _context.{GetPropertyNameFromType(type.WithNullableAnnotation(Microsoft.CodeAnalysis.NullableAnnotation.NotAnnotated))}.Serialize(writer, value.{property.Name}); }} else {{ writer.WriteNull(); }}";
        }
        
        // Handle nullable built-in types
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType && 
            namedType.Name == "Nullable" && namedType.TypeArguments.Length == 1)
        {
            var underlyingType = namedType.TypeArguments[0];
            if (IsBuiltInType(underlyingType))
            {
                return $"if (value.{property.Name}.HasValue) {{ {GenerateDirectSerialization($"value.{property.Name}.Value", underlyingType)} }} else {{ writer.WriteNull(); }}";
            }
        }
        
        // Handle nullable reference types (like Dictionary<string, string>?)
        if (type.CanBeReferencedByName && type.NullableAnnotation == Microsoft.CodeAnalysis.NullableAnnotation.Annotated)
        {
            return $"if (value.{property.Name} != null) {{ _context.{GetPropertyNameFromType(type.WithNullableAnnotation(Microsoft.CodeAnalysis.NullableAnnotation.NotAnnotated))}.Serialize(writer, value.{property.Name}); }} else {{ writer.WriteNull(); }}";
        }
        
        // Handle complex types via context reference
        return $"_context.{GetPropertyNameFromType(type)}.Serialize(writer, value.{property.Name});";
    }

    private static string GeneratePropertyDeserialization(IPropertySymbol property)
    {
        var type = property.Type;
        
        // Handle built-in types directly
        if (IsBuiltInType(type))
        {
            return GenerateDirectDeserialization(type);
        }
        
        // Handle arrays (including nullable arrays)
        if (IsArray(type, out var arrayElementType))
        {
            if (arrayElementType == null)
                throw new InvalidOperationException("Element type cannot be null for a valid array type.");
                
            return $"_context.{GetPropertyNameFromType(type)}.Deserialize(reader)";
        }
        
        // Handle nullable arrays
        if (type.CanBeReferencedByName && type.NullableAnnotation == Microsoft.CodeAnalysis.NullableAnnotation.Annotated && 
            IsArray(type.WithNullableAnnotation(Microsoft.CodeAnalysis.NullableAnnotation.NotAnnotated), out var nullableArrayElementType))
        {
            if (nullableArrayElementType == null)
                throw new InvalidOperationException("Element type cannot be null for a valid nullable array type.");
                
            var nonNullableType = type.WithNullableAnnotation(Microsoft.CodeAnalysis.NullableAnnotation.NotAnnotated);
            return $"reader.PeekState() == CborReaderState.Null ? ReadNullValue<{nonNullableType.ToDisplayString()}>(reader) : _context.{GetPropertyNameFromType(nonNullableType)}.Deserialize(reader)";
        }
        
        // Handle nullable built-in types
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType && 
            namedType.Name == "Nullable" && namedType.TypeArguments.Length == 1)
        {
            var underlyingType = namedType.TypeArguments[0];
            if (IsBuiltInType(underlyingType))
            {
                return $"ReadNullable{underlyingType.Name}(reader)";
            }
        }
        
        // Handle nullable reference types (like Dictionary<string, string>?)
        if (type.CanBeReferencedByName && type.NullableAnnotation == Microsoft.CodeAnalysis.NullableAnnotation.Annotated)
        {
            var nonNullableType = type.WithNullableAnnotation(Microsoft.CodeAnalysis.NullableAnnotation.NotAnnotated);
            return $"reader.PeekState() == CborReaderState.Null ? ReadNullValue<{nonNullableType.ToDisplayString()}>(reader) : _context.{GetPropertyNameFromType(nonNullableType)}.Deserialize(reader)";
        }
        
        // Handle complex types via context reference
        return $"_context.{GetPropertyNameFromType(type)}.Deserialize(reader)";
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

    private static bool IsDictionary(INamedTypeSymbol typeSymbol, out ITypeSymbol? keyType, out ITypeSymbol? valueType)
    {
        keyType = null;
        valueType = null;
        if (typeSymbol is { IsGenericType: true } && typeSymbol.Name == "Dictionary" && typeSymbol.TypeArguments.Length == 2)
        {
            keyType = typeSymbol.TypeArguments[0];
            valueType = typeSymbol.TypeArguments[1];
            return true;
        }
        return false;
    }

    private static bool IsArray(ITypeSymbol typeSymbol, out ITypeSymbol? elementType)
    {
        elementType = null;
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            elementType = arrayType.ElementType;
            return true;
        }
        return false;
    }

    private static string GetPropertyNameFromType(ITypeSymbol typeSymbol)
    {
        // Handle arrays
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            return $"ArrayOf{GetPropertyNameFromType(arrayType.ElementType)}";
        }
        
        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var name = namedType.Name;
            int backtickIndex = name.IndexOf('`');
            if (backtickIndex >= 0)
                name = name.Substring(0, backtickIndex);
            name += "Of" + string.Join("And", namedType.TypeArguments.Select(GetPropertyNameFromType));
            return name;
        }
        
        // Handle built-in types specially - they don't need context references
        var specialTypeName = typeSymbol.SpecialType switch
        {
            SpecialType.System_String => "String",
            SpecialType.System_Int32 => "Int32", 
            SpecialType.System_Boolean => "Boolean",
            SpecialType.System_Double => "Double",
            SpecialType.System_Single => "Single",
            SpecialType.System_Int64 => "Int64",
            SpecialType.System_UInt32 => "UInt32",
            SpecialType.System_UInt64 => "UInt64",
            SpecialType.System_Byte => "Byte",
            SpecialType.System_SByte => "SByte",
            SpecialType.System_Int16 => "Int16",
            SpecialType.System_UInt16 => "UInt16",
            _ => null
        };
        
        if (specialTypeName != null) return specialTypeName;
        
        // Handle other built-in types
        var displayString = typeSymbol.ToDisplayString();
        if (displayString == "System.Guid")
        {
            return "Guid";
        }
        
        if (displayString == "System.DateTime")
        {
            return "DateTime";
        }
        
        if (displayString == "System.DateTimeOffset")
        {
            return "DateTimeOffset";
        }
        
        if (displayString == "System.Decimal" || displayString == "decimal")
        {
            return "Decimal";
        }
        
        return typeSymbol.Name;
    }

    private static bool IsBuiltInType(ITypeSymbol typeSymbol)
    {
        // Handle special types
        var isSpecialType = typeSymbol.SpecialType switch
        {
            SpecialType.System_String => true,
            SpecialType.System_Int32 => true,
            SpecialType.System_Boolean => true,
            SpecialType.System_Double => true,
            SpecialType.System_Single => true,
            SpecialType.System_Int64 => true,
            SpecialType.System_UInt32 => true,
            SpecialType.System_UInt64 => true,
            SpecialType.System_Byte => true,
            SpecialType.System_SByte => true,
            SpecialType.System_Int16 => true,
            SpecialType.System_UInt16 => true,
            _ => false
        };
        
        if (isSpecialType) return true;
        
        // Handle other built-in types (not SpecialTypes)
        var displayString = typeSymbol.ToDisplayString();
        return displayString == "System.Guid" ||
               displayString == "System.DateTime" ||
               displayString == "System.DateTimeOffset" ||
               displayString == "System.Decimal" ||
               displayString == "decimal";
    }

    private static string GenerateDirectSerialization(string variableName, ITypeSymbol typeSymbol)
    {
        // Handle special types
        var result = typeSymbol.SpecialType switch
        {
            SpecialType.System_String => $"writer.WriteTextString({variableName});",
            SpecialType.System_Int32 => $"writer.WriteInt32({variableName});",
            SpecialType.System_Boolean => $"writer.WriteBoolean({variableName});",
            SpecialType.System_Double => $"writer.WriteDouble({variableName});",
            SpecialType.System_Single => $"writer.WriteSingle({variableName});",
            SpecialType.System_Int64 => $"writer.WriteInt64({variableName});",
            SpecialType.System_UInt32 => $"writer.WriteUInt32({variableName});",
            SpecialType.System_UInt64 => $"writer.WriteUInt64({variableName});",
            SpecialType.System_Byte => $"writer.WriteUInt32({variableName});",
            SpecialType.System_SByte => $"writer.WriteInt32({variableName});",
            SpecialType.System_Int16 => $"writer.WriteInt32({variableName});",
            SpecialType.System_UInt16 => $"writer.WriteUInt32({variableName});",
            _ => null
        };
        
        if (result != null) return result;
        
        // Handle other built-in types
        var displayString = typeSymbol.ToDisplayString();
        if (displayString == "System.Guid")
        {
            return $"writer.WriteByteString({variableName}.ToByteArray());"; 
        }
        
        if (displayString == "System.DateTime")
        {
            return $"writer.WriteTag(System.Formats.Cbor.CborTag.DateTimeString); writer.WriteTextString(({variableName}.Kind == System.DateTimeKind.Unspecified ? System.DateTime.SpecifyKind({variableName}, System.DateTimeKind.Utc) : {variableName}).ToUniversalTime().ToString(\"yyyy-MM-ddTHH:mm:ss.FFFFFFFK\", System.Globalization.CultureInfo.InvariantCulture));"; 
        }
        
        if (displayString == "System.DateTimeOffset")
        {
            return $"writer.WriteTag(System.Formats.Cbor.CborTag.DateTimeString); writer.WriteTextString({variableName}.ToString(\"yyyy-MM-ddTHH:mm:ss.FFFFFFFK\", System.Globalization.CultureInfo.InvariantCulture));"; 
        }
        
        if (displayString == "System.Decimal" || displayString == "decimal")
        {
            return $"writer.WriteDecimal({variableName});";
        }
        
        return $"// TODO: Implement direct serialization for {typeSymbol.ToDisplayString()}";
    }

    private static string GenerateDirectDeserialization(ITypeSymbol typeSymbol)
    {
        // Handle special types
        var result = typeSymbol.SpecialType switch
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
            _ => null
        };
        
        if (result != null) return result;
        
        // Handle other built-in types
        var displayString = typeSymbol.ToDisplayString();
        if (displayString == "System.Guid")
        {
            return "new System.Guid(reader.ReadByteString())";
        }
        
        if (displayString == "System.DateTime")
        {
            return "System.DateTime.ParseExact(reader.ReadTag() == System.Formats.Cbor.CborTag.DateTimeString ? reader.ReadTextString() : throw new System.InvalidOperationException(\"Expected DateTimeString tag\"), \"yyyy-MM-ddTHH:mm:ss.FFFFFFFK\", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind)";
        }
        
        if (displayString == "System.DateTimeOffset")
        {
            return "System.DateTimeOffset.ParseExact(reader.ReadTag() == System.Formats.Cbor.CborTag.DateTimeString ? reader.ReadTextString() : throw new System.InvalidOperationException(\"Expected DateTimeString tag\"), \"yyyy-MM-ddTHH:mm:ss.FFFFFFFK\", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind)";
        }
        
        if (displayString == "System.Decimal" || displayString == "decimal")
        {
            return "reader.ReadDecimal()";
        }
        
        return $"// TODO: Implement direct deserialization for {typeSymbol.ToDisplayString()}";
    }

    private static string ApplyNamingPolicy(string name, CbOrKnownNamingPolicy policy)
    {
        return policy switch
        {
            CbOrKnownNamingPolicy.CamelCase => ToCamelCase(name),
            CbOrKnownNamingPolicy.SnakeCaseLower => ToSnakeCase(name, upper: false),
            CbOrKnownNamingPolicy.SnakeCaseUpper => ToSnakeCase(name, upper: true),
            CbOrKnownNamingPolicy.KebabCaseLower => ToKebabCase(name, upper: false),
            CbOrKnownNamingPolicy.KebabCaseUpper => ToKebabCase(name, upper: true),
            CbOrKnownNamingPolicy.UpperCase => name.ToUpperInvariant(),
            CbOrKnownNamingPolicy.LowerCase => name.ToLowerInvariant(),
            _ => name,
        };
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private static string ToSnakeCase(string name, bool upper)
    {
        var separated = ToSeparatedCase(name, '_');
        return upper ? separated.ToUpperInvariant() : separated.ToLowerInvariant();
    }

    private static string ToKebabCase(string name, bool upper)
    {
        var separated = ToSeparatedCase(name, '-');
        return upper ? separated.ToUpperInvariant() : separated.ToLowerInvariant();
    }

    private static string ToSeparatedCase(string name, char delimiter)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        var builder = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                    builder.Append(delimiter);
                builder.Append(c);
            }
            else
            {
                builder.Append(c);
            }
        }
        return builder.ToString();
    }
} 

