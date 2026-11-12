namespace NCbor.Generator;

internal static partial class SerializationCodeGenerator
{
    /// <summary>Name of the generated context helper that a property/element of an unsupported, un-stubbable type (CBOR009, e.g. <c>dynamic</c>) calls on write.</summary>
    internal const string UnsupportedWriteHelperName = "ThrowUnsupportedTypeOnWrite";

    /// <summary>Read-side counterpart of <see cref="UnsupportedWriteHelperName"/> (generic in the value type).</summary>
    internal const string UnsupportedReadHelperName = "ThrowUnsupportedTypeOnRead";

    /// <summary>
    /// Built-in types that get a generated <c>ReadNullable{TypeName}</c> helper, and the reader
    /// method it uses. Shared with discovery, which reserves the helper names.
    /// </summary>
    internal static readonly (string TypeName, string ReaderMethod)[] NullableHelpers =
    {
        ("Int32", "ReadInt32"),
        ("Boolean", "ReadBoolean"),
        ("String", "ReadTextString"),
        ("Double", "ReadDouble"),
        ("Single", "ReadSingle"),
        ("Int64", "ReadInt64"),
        ("UInt32", "ReadUInt32"),
        ("UInt64", "ReadUInt64"),
        ("Byte", "ReadUInt32"),
        ("SByte", "ReadInt32"),
        ("Int16", "ReadInt32"),
        ("UInt16", "ReadUInt32"),
        ("Decimal", "ReadDecimal"),
        ("Guid", "ReadByteString"),
        ("DateTime", "ReadTextString"),
        ("DateTimeOffset", "ReadTextString"),
    };

    private static IEnumerable<string> NullableHelperTypeNames => NullableHelpers.Select(h => h.TypeName);

    public static string GenerateSerializationCode(ITypeSymbol typeSymbol, NCborNamingPolicy namingPolicy, ContextTypeNames names, string contextRef = "this", NCborIgnoreCondition defaultIgnoreCondition = NCborIgnoreCondition.Never)
    {
        var builder = new StringBuilder();
        if (IsByteArray(typeSymbol))
        {
            builder.AppendLine(GenerateByteStringWrite("value"));
            return builder.ToString();
        }
        // Registered built-ins (e.g. [NCborSerializable(typeof(int))]) and Nullable<built-in>.
        if (IsBuiltInType(typeSymbol) || IsNullableBuiltInType(typeSymbol))
        {
            builder.AppendLine(GenerateElementWrite("value", typeSymbol, contextRef, names));
            return builder.ToString();
        }
        if (TryGetCollectionInfo(typeSymbol, out var collection))
        {
            if (collection.Kind == CollectionKind.Dictionary)
            {
                builder.AppendLine("writer.WriteStartMap(value.Count);");
                builder.AppendLine($"foreach (var kvp in value)");
                builder.AppendLine("{");
                // Dictionary keys are never null, so the key write has no null branch.
                builder.AppendLine($"    {GenerateElementWrite("kvp.Key", collection.KeyType!, contextRef, names, isDictionaryKey: true)}");
                builder.AppendLine($"    {GenerateElementWrite("kvp.Value", collection.ValueType!, contextRef, names)}");
                builder.AppendLine("}");
                builder.AppendLine("writer.WriteEndMap();");
                return builder.ToString();
            }

            // IEnumerable<T> has no Count: write an indefinite-length array rather than enumerate twice.
            builder.AppendLine(collection.HasCount ? "writer.WriteStartArray(value.Count);" : "writer.WriteStartArray(null);");
            builder.AppendLine($"foreach (var item in value)");
            builder.AppendLine("{");
            builder.AppendLine($"    {GenerateElementWrite("item", collection.ElementType!, contextRef, names)}");
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
            builder.AppendLine($"    {GenerateElementWrite("item", arrayElementType, contextRef, names)}");
            builder.AppendLine("}");
            builder.AppendLine("writer.WriteEndArray();");
            return builder.ToString();
        }
        var properties = GetSerializableProperties(typeSymbol as INamedTypeSymbol ?? throw new InvalidOperationException($"Type {typeSymbol} is not a named type and cannot have properties"), defaultIgnoreCondition);
        builder.AppendLine("writer.WriteStartMap(null);");
        foreach (var property in properties)
        {
            var propertyName = GetPropertyName(property, namingPolicy);
            var body = $"writer.WriteTextString(\"{propertyName}\");\n{GeneratePropertySerialization(property, names)}";
            var writeGuard = GetWriteGuard(property, defaultIgnoreCondition);
            if (writeGuard != null)
            {
                builder.AppendLine($"if ({writeGuard})");
                builder.AppendLine("{");
                builder.AppendLine(body);
                builder.AppendLine("}");
            }
            else
            {
                builder.AppendLine(body);
            }
        }
        builder.AppendLine("writer.WriteEndMap();");
        return builder.ToString();
    }

    public static string GenerateDeserializationCode(ITypeSymbol typeSymbol, NCborNamingPolicy namingPolicy, ContextTypeNames names, string contextRef = "this", NCborIgnoreCondition defaultIgnoreCondition = NCborIgnoreCondition.Never)
    {
        var builder = new StringBuilder();
        if (IsByteArray(typeSymbol))
        {
            builder.AppendLine("return reader.ReadByteString();");
            return builder.ToString();
        }
        // Registered built-ins (e.g. [NCborSerializable(typeof(int))]) and Nullable<built-in>.
        if (IsBuiltInType(typeSymbol) || IsNullableBuiltInType(typeSymbol))
        {
            builder.AppendLine($"return {GenerateElementRead(typeSymbol, contextRef, names)};");
            return builder.ToString();
        }
        if (TryGetCollectionInfo(typeSymbol, out var collection))
        {
            if (collection.Kind == CollectionKind.Dictionary)
            {
                builder.AppendLine($"var dictionary = new {collection.ConcreteTypeName}();");
                builder.AppendLine("int? mapSize = reader.ReadStartMap();");
                builder.AppendLine("for (int i = 0; mapSize == null || i < mapSize; i++)");
                builder.AppendLine("{");
                builder.AppendLine("    if (mapSize == null && reader.PeekState() == CborReaderState.EndMap)");
                builder.AppendLine("        break;");
                // A CBOR null key would reach Dictionary.Add as a null (or default) key and escape as
                // a raw ArgumentNullException / InvalidOperationException; reject it up front.
                builder.AppendLine("    if (reader.PeekState() == CborReaderState.Null)");
                builder.AppendLine($"        throw new NCborDeserializationException(typeof({typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}), \"null dictionary key\");");
                builder.AppendLine($"    var key = {GenerateElementRead(collection.KeyType!, contextRef, names)};");
                builder.AppendLine($"    var value = {GenerateElementRead(collection.ValueType!, contextRef, names)};");
                builder.AppendLine("    dictionary.Add(key, value);");
                builder.AppendLine("}");
                builder.AppendLine("reader.ReadEndMap();");
                builder.AppendLine("return dictionary;");
                return builder.ToString();
            }

            // Interface-typed lists/sets (IReadOnlyList<T>, ISet<T>, ...) deserialize into the
            // concrete List<T>/HashSet<T>, which is assignable to the declared return type.
            builder.AppendLine($"var list = new {collection.ConcreteTypeName}();");
            builder.AppendLine("int? length = reader.ReadStartArray();");
            builder.AppendLine("for (int i = 0; length == null || i < length; i++)");
            builder.AppendLine("{");
            builder.AppendLine("    if (length == null && reader.PeekState() == CborReaderState.EndArray)");
            builder.AppendLine("        break;");
            builder.AppendLine($"    var item = {GenerateElementRead(collection.ElementType!, contextRef, names)};");
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

            var elementTypeName = arrayElementType.ToDisplayString();
            var itemExpression = GenerateElementRead(arrayElementType, contextRef, names);

            // Definite-length arrays (which is everything this library's own writer produces,
            // via WriteStartArray(value.Length)) allocate the array directly instead of paying
            // for a List<T> + ToArray() copy. Indefinite-length arrays (possible when reading
            // CBOR from another encoder) still need the List<T> accumulator since the final
            // size isn't known up front.
            builder.AppendLine($"{elementTypeName}[] result;");
            builder.AppendLine("int? length = reader.ReadStartArray();");
            builder.AppendLine("if (length is int definiteLength)");
            builder.AppendLine("{");
            builder.AppendLine($"    result = {GenerateArrayAllocation(arrayElementType, "definiteLength")};");
            builder.AppendLine("    for (int i = 0; i < definiteLength; i++)");
            builder.AppendLine("    {");
            builder.AppendLine($"        result[i] = {itemExpression};");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine("else");
            builder.AppendLine("{");
            builder.AppendLine($"    var list = new System.Collections.Generic.List<{elementTypeName}>();");
            builder.AppendLine("    while (reader.PeekState() != CborReaderState.EndArray)");
            builder.AppendLine("    {");
            builder.AppendLine($"        list.Add({itemExpression});");
            builder.AppendLine("    }");
            builder.AppendLine("    result = list.ToArray();");
            builder.AppendLine("}");
            builder.AppendLine("reader.ReadEndArray();");
            builder.AppendLine("return result;");
            return builder.ToString();
        }
        var namedType = typeSymbol as INamedTypeSymbol ?? throw new InvalidOperationException($"Type {typeSymbol} is not a named type and cannot have properties");
        return GenerateObjectDeserialization(namedType, namingPolicy, defaultIgnoreCondition, names);
    }

    /// <summary>
    /// <c>new E[length]</c> for an array of <paramref name="elementType"/>. When the element is
    /// itself an array the length goes in the <i>first</i> rank specifier — <c>new int[length][]</c>,
    /// not <c>new int[][length]</c> (CS1586) — so the innermost element type is written first.
    /// </summary>
    private static string GenerateArrayAllocation(ITypeSymbol elementType, string lengthExpression)
    {
        var trailingRanks = new StringBuilder();
        var innermost = elementType;
        while (innermost is IArrayTypeSymbol nested)
        {
            trailingRanks.Append('[').Append(',', nested.Rank - 1).Append(']');
            innermost = nested.ElementType;
        }
        return $"new {innermost.ToDisplayString()}[{lengthExpression}]{trailingRanks}";
    }

    /// <summary>
    /// Properties that take part in serialization, in wire order: every reachable instance
    /// property across the base chain (see <see cref="GetAllInstanceProperties"/>) that can be
    /// written back — through a usable setter / init accessor, or a matching parameter of the
    /// constructor deserialization uses — minus the ones ignored with Always.
    /// </summary>
    private static IEnumerable<IPropertySymbol> GetSerializableProperties(INamedTypeSymbol typeSymbol, NCborIgnoreCondition defaultIgnoreCondition)
    {
        var constructor = GetDeserializationConstructor(typeSymbol);
        return GetAllInstanceProperties(typeSymbol)
            .Where(p => HasUsableSetter(p) || (constructor != null && FindMatchingParameter(constructor, p) != null))
            .Where(p => !IsAlwaysIgnored(p, defaultIgnoreCondition));
    }

    /// <summary>
    /// Returns every constructor marked <c>[NCborConstructor]</c> on the type — normally 0 or 1,
    /// but nothing in the language prevents marking more than one.
    /// </summary>
    private static IEnumerable<IMethodSymbol> GetConstructorCandidates(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.Constructors.Where(c =>
            c.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "NCbor.NCborConstructorAttribute"));
    }

    /// <summary>
    /// Returns the constructor whose parameters deserialization fills from the payload — the
    /// <c>[NCborConstructor]</c>-marked one, or one auto-selected per
    /// <see cref="ResolveConstructor"/> — or null when the instance is built with <c>new T()</c>
    /// (or can't be built at all; see CBOR010). If more than one constructor is marked, picks the
    /// first arbitrarily — <c>CBOR007</c> reports that as a usage error.
    /// </summary>
    private static IMethodSymbol? GetDeserializationConstructor(INamedTypeSymbol typeSymbol)
    {
        return ResolveConstructor(typeSymbol).ParameterizedConstructor;
    }

    /// <summary>
    /// True when a type has more than one constructor marked <c>[NCborConstructor]</c> — an
    /// ambiguous configuration the generator can't resolve on the user's behalf.
    /// </summary>
    public static bool HasAmbiguousConstructors(INamedTypeSymbol typeSymbol)
    {
        return GetConstructorCandidates(typeSymbol).Skip(1).Any();
    }

    /// <summary>
    /// The second <c>[NCborConstructor]</c>-marked constructor (declaration order), or null when
    /// fewer than two are marked — where CBOR007 is reported.
    /// </summary>
    public static IMethodSymbol? GetFirstRedundantMarkedConstructor(INamedTypeSymbol typeSymbol)
    {
        return GetConstructorCandidates(typeSymbol).Skip(1).FirstOrDefault();
    }

    /// <summary>
    /// Only "plain object" types go through property-based dispatch at all; collections, byte[],
    /// and built-ins are serialized by iterating elements/values, so checks scoped to "properties
    /// the codegen loop will actually see" must exclude these or they misfire on the type's own
    /// BCL properties (e.g. List&lt;T&gt;.Count).
    /// </summary>
    private static bool IsPlainObjectType(ITypeSymbol typeSymbol)
    {
        return !IsByteArray(typeSymbol) && !IsList(typeSymbol, out _) && !IsArray(typeSymbol, out _) &&
               !IsDictionary(typeSymbol, out _, out _) && !IsBuiltInType(typeSymbol);
    }

    /// <summary>
    /// Matches a constructor parameter to a property by name (case-insensitive). Custom wire
    /// names via [NCborPropertyName] on the *parameter* (rather than the property) are not
    /// consulted here — only the C# names are compared.
    /// </summary>
    private static IParameterSymbol? FindMatchingParameter(IMethodSymbol constructor, IPropertySymbol property)
    {
        return constructor.Parameters.FirstOrDefault(p => string.Equals(p.Name, property.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static IPropertySymbol? FindMatchingProperty(IEnumerable<IPropertySymbol> properties, IParameterSymbol parameter)
    {
        return properties.FirstOrDefault(p => string.Equals(p.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns readable properties that will be silently excluded from serialization: no setter
    /// the generated code can call (none, or private/protected), and not matched to a parameter
    /// of the deserialization constructor either. Used by the generator to emit a usage
    /// diagnostic instead of dropping the property with no signal at all. Empty when the type has
    /// no usable constructor — CBOR010 already fails the build for that.
    /// </summary>
    public static IEnumerable<IPropertySymbol> GetUncoveredGetOnlyProperties(INamedTypeSymbol typeSymbol, NCborIgnoreCondition defaultIgnoreCondition)
    {
        if (!IsPlainObjectType(typeSymbol))
        {
            return Enumerable.Empty<IPropertySymbol>();
        }

        var resolution = ResolveConstructor(typeSymbol);
        if (resolution.Kind == ConstructorKind.None)
        {
            return Enumerable.Empty<IPropertySymbol>();
        }

        var constructor = resolution.ParameterizedConstructor;
        return GetAllInstanceProperties(typeSymbol)
            .Where(p => !HasUsableSetter(p))
            // Already-Always-ignored properties (own attribute or context default) are excluded
            // deliberately, not silently — no diagnostic needed for those.
            .Where(p => !IsAlwaysIgnored(p, defaultIgnoreCondition))
            .Where(p => constructor == null || FindMatchingParameter(constructor, p) == null);
    }

    /// <summary>
    /// Returns deserialization-constructor parameters with no matching property, which the
    /// generator will silently initialize to <c>default(T)</c> on every deserialize — used to
    /// emit a usage diagnostic instead of leaving that discovery to a confused bug report.
    /// </summary>
    public static IEnumerable<IParameterSymbol> GetUnmatchedConstructorParameters(INamedTypeSymbol typeSymbol, NCborIgnoreCondition defaultIgnoreCondition)
    {
        if (!IsPlainObjectType(typeSymbol))
        {
            return Enumerable.Empty<IParameterSymbol>();
        }

        var constructor = GetDeserializationConstructor(typeSymbol);
        if (constructor == null)
        {
            return Enumerable.Empty<IParameterSymbol>();
        }

        var properties = GetSerializableProperties(typeSymbol, defaultIgnoreCondition).ToList();
        return constructor.Parameters.Where(p => FindMatchingProperty(properties, p) == null);
    }

    /// <summary>
    /// Returns properties decorated with <c>[NCborEnumAsString]</c> whose type is neither an enum
    /// nor a nullable enum — the attribute silently has no effect for these today; used to emit a
    /// usage diagnostic instead of letting the misapplication pass unnoticed.
    /// </summary>
    public static IEnumerable<IPropertySymbol> GetMisusedEnumAsStringProperties(INamedTypeSymbol typeSymbol)
    {
        if (!IsPlainObjectType(typeSymbol))
        {
            return Enumerable.Empty<IPropertySymbol>();
        }

        return GetAllInstanceProperties(typeSymbol)
            .Where(HasEnumAsStringAttribute)
            .Where(p => p.Type.TypeKind != TypeKind.Enum && !TryGetNullableEnumUnderlyingType(p.Type, out _));
    }

    private static AttributeData? GetIgnoreAttribute(IPropertySymbol property)
    {
        return property.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == "NCbor.NCborIgnoreAttribute");
    }

    private static bool IsAlwaysIgnored(IPropertySymbol property, NCborIgnoreCondition defaultIgnoreCondition)
    {
        var ignoreAttr = GetIgnoreAttribute(property);
        if (ignoreAttr != null)
        {
            return GetIgnoreCondition(ignoreAttr) == NCborIgnoreCondition.Always;
        }

        // A property with its own [NCborDefaultValue] (but no explicit [NCborIgnore]) opts into
        // value-based inclusion via GetWriteGuard, which already gives that attribute top
        // priority — it must not be swallowed by a context-wide DefaultIgnoreCondition.Always,
        // or the property would be silently dropped unconditionally regardless of its value.
        if (GetDefaultValueAttribute(property) != null)
        {
            return false;
        }

        return defaultIgnoreCondition == NCborIgnoreCondition.Always;
    }

    private static AttributeData? GetDefaultValueAttribute(IPropertySymbol property)
    {
        return property.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == "NCbor.NCborDefaultValueAttribute");
    }

    /// <summary>
    /// Returns the fully-qualified type name of the property's <c>[NCborConverter(typeof(X))]</c>
    /// converter, or null if the property has no custom converter.
    /// </summary>
    private static string? GetConverterType(IPropertySymbol property)
    {
        var converterAttr = property.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == "NCbor.NCborConverterAttribute");
        if (converterAttr == null || converterAttr.ConstructorArguments.Length == 0)
            return null;

        return converterAttr.ConstructorArguments[0].Value is ITypeSymbol converterTypeSymbol
            ? converterTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            : null;
    }

    /// <summary>
    /// Returns the distinct <c>[NCborConverter]</c> types used by a type's serializable
    /// properties, so the generator can declare one cached instance field per converter type
    /// instead of allocating a new instance on every single serialize/deserialize call.
    /// </summary>
    public static IEnumerable<string> GetDistinctConverterTypes(INamedTypeSymbol typeSymbol, NCborIgnoreCondition defaultIgnoreCondition)
    {
        if (!IsPlainObjectType(typeSymbol))
        {
            return Enumerable.Empty<string>();
        }

        return GetSerializableProperties(typeSymbol, defaultIgnoreCondition)
            .Select(GetConverterType)
            .Where(t => t != null)
            .Distinct()!;
    }

    /// <summary>
    /// Deterministic field name for a cached converter instance — must match exactly between
    /// where the field is declared (NCborSourceGenerator.cs) and where it's referenced
    /// (GeneratePropertySerialization/GeneratePropertyDeserialization below).
    /// </summary>
    public static string GetConverterFieldName(string converterType)
    {
        var sanitized = new StringBuilder(converterType.Length);
        foreach (var c in converterType)
        {
            sanitized.Append(char.IsLetterOrDigit(c) ? c : '_');
        }
        return "__converter_" + sanitized;
    }

    private static bool HasEnumAsStringAttribute(IPropertySymbol property)
    {
        return property.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() == "NCbor.NCborEnumAsStringAttribute");
    }

    private static bool TryGetNullableEnumUnderlyingType(ITypeSymbol type, out ITypeSymbol? enumType)
    {
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType && namedType.Name == "Nullable" &&
            namedType.TypeArguments.Length == 1 && namedType.TypeArguments[0].TypeKind == TypeKind.Enum)
        {
            enumType = namedType.TypeArguments[0];
            return true;
        }
        enumType = null;
        return false;
    }

    private static NCborIgnoreCondition GetIgnoreCondition(AttributeData ignoreAttr)
    {
        foreach (var arg in ignoreAttr.NamedArguments)
        {
            if (arg.Key == "Condition" && arg.Value.Value is int val)
                return (NCborIgnoreCondition)val;
        }
        return NCborIgnoreCondition.Always;
    }

    /// <summary>
    /// Returns a C# boolean expression that must be true for the property's key/value pair to
    /// be written, or null if the property should always be written unconditionally.
    /// </summary>
    private static string? GetWriteGuard(IPropertySymbol property, NCborIgnoreCondition defaultIgnoreCondition)
    {
        var defaultValueAttr = GetDefaultValueAttribute(property);
        if (defaultValueAttr != null && defaultValueAttr.ConstructorArguments.Length > 0)
        {
            var literal = FormatDefaultValueLiteral(defaultValueAttr.ConstructorArguments[0]);
            var typeName = property.Type.ToDisplayString();
            return $"!System.Collections.Generic.EqualityComparer<{typeName}>.Default.Equals(value.{property.Name}, {literal})";
        }

        var ignoreAttr = GetIgnoreAttribute(property);
        // No explicit [NCborIgnore] on the property: fall back to the context-wide default.
        var condition = ignoreAttr != null ? GetIgnoreCondition(ignoreAttr) : defaultIgnoreCondition;
        var typeDisplayName = property.Type.ToDisplayString();
        switch (condition)
        {
            case NCborIgnoreCondition.WhenWritingNull:
                // Matches System.Text.Json: has no effect on non-nullable value types.
                if (!CanBeNull(property.Type))
                    return null;
                return $"value.{property.Name} != null";
            case NCborIgnoreCondition.WhenWritingDefault:
                return $"!System.Collections.Generic.EqualityComparer<{typeDisplayName}>.Default.Equals(value.{property.Name}, default({typeDisplayName}))";
            default:
                // Always is filtered out by GetSerializableProperties; Never means always write.
                return null;
        }
    }

    private static bool CanBeNull(ITypeSymbol type)
    {
        if (type.IsReferenceType)
            return true;
        return type is INamedTypeSymbol namedType && namedType.IsGenericType && namedType.Name == "Nullable";
    }

    /// <summary>
    /// Formats an <c>[NCborDefaultValue(...)]</c> constructor argument as a C# literal matching
    /// the property's own type. Enum-typed arguments need an explicit cast: Roslyn's
    /// <see cref="TypedConstant.Value"/> for an enum constant is its boxed underlying numeric
    /// value (e.g. boxed <c>int</c>), and C# only allows an *implicit* conversion from the
    /// literal <c>0</c> to an enum type — any other bare numeric literal fails to compile against
    /// an enum-typed parameter (e.g. <c>EqualityComparer&lt;MyEnum&gt;.Default.Equals(x, 3)</c>).
    /// </summary>
    private static string FormatDefaultValueLiteral(TypedConstant constant)
    {
        if (constant.Type is { TypeKind: TypeKind.Enum } enumType)
        {
            var numericLiteral = Convert.ToString(constant.Value, System.Globalization.CultureInfo.InvariantCulture) ?? "0";
            return $"({enumType.ToDisplayString()}){numericLiteral}";
        }

        return constant.Value switch
        {
            null => "null",
            bool b => b ? "true" : "false",
            string s => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
            char c => $"'{c}'",
            double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture) + "d",
            float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture) + "f",
            decimal m => m.ToString(System.Globalization.CultureInfo.InvariantCulture) + "m",
            long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture) + "L",
            uint ui => ui.ToString(System.Globalization.CultureInfo.InvariantCulture) + "u",
            ulong ul => ul.ToString(System.Globalization.CultureInfo.InvariantCulture) + "ul",
            var value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "default"
        };
    }

    private static string GetPropertyName(IPropertySymbol property, NCborNamingPolicy namingPolicy)
    {
        var nameAttr = property.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == "NCbor.NCborPropertyNameAttribute");

        if (nameAttr != null && nameAttr.ConstructorArguments.Length > 0)
        {
            return nameAttr.ConstructorArguments[0].Value?.ToString() ?? property.Name;
        }

        return ApplyNamingPolicy(property.Name, namingPolicy);
    }

    private static string GeneratePropertySerialization(IPropertySymbol property, ContextTypeNames names)
    {
        var type = property.Type;
        var access = $"value.{property.Name}";

        // A custom converter takes priority over every other dispatch rule, null handling
        // included: the converter receives the value as-is and decides how to encode null.
        var converterType = GetConverterType(property);
        if (converterType != null)
        {
            return $"{GetConverterFieldName(converterType)}.Write(writer, {access}!);";
        }

        // Unsupported types that can't have a stub TypeInfo (dynamic, List<dynamic>, ...):
        // a throwing helper call, so CBOR009 stays the only build error.
        if (IsUnsupportedUnstubbableType(type))
        {
            return GenerateUnsupportedWrite(type);
        }

        // Unsupported types (CBOR009) dispatch to their throwing stub TypeInfo by their exact
        // type name, bypassing the nullable branches below (which would e.g. emit
        // ReadNullValue<T> for a Nullable<struct>), so the only build error is CBOR009 itself.
        if (IsUnsupportedStubbedType(type))
        {
            return $"_context.{names.Get(type)}.Serialize(writer, {access}!, depth + 1);";
        }

        // [NCborEnumAsString] takes priority over the default numeric enum encoding.
        if (HasEnumAsStringAttribute(property))
        {
            if (type.TypeKind == TypeKind.Enum)
            {
                return $"writer.WriteTextString({access}.ToString());";
            }
            if (TryGetNullableEnumUnderlyingType(type, out _))
            {
                return $"if ({access}.HasValue) {{ writer.WriteTextString({access}.Value.ToString()); }} else {{ writer.WriteNull(); }}";
            }
        }

        // Nullable<built-in>: the value or CBOR null.
        if (TryGetNullableUnderlyingType(type, out var underlyingType) && IsBuiltInType(underlyingType!))
        {
            return $"if ({access}.HasValue) {{ {GenerateDirectSerialization($"{access}.Value", underlyingType!)} }} else {{ writer.WriteNull(); }}";
        }

        var core = GenerateNonNullPropertyWrite(access, StripNullableAnnotation(type), names);

        // Matches System.Text.Json: a null reference-type property is written as CBOR null
        // whatever its nullable annotation says — a non-annotated `string` can still be null at
        // runtime, and passing it on would crash (WriteTextString throws ArgumentNullException,
        // a nested TypeInfo dereferences it). Flow analysis sees the null check, so the core
        // call needs no `!`.
        if (type.IsReferenceType)
        {
            return $"if ({access} is null) {{ writer.WriteNull(); }} else {{ {core} }}";
        }

        return core;
    }

    /// <summary>Statement writing a property value already known to be non-null.</summary>
    private static string GenerateNonNullPropertyWrite(string access, ITypeSymbol type, ContextTypeNames names)
    {
        // byte[] as a CBOR byte string (major type 2)
        if (IsByteArray(type))
        {
            return GenerateByteStringWrite(access);
        }

        if (IsBuiltInType(type))
        {
            return GenerateDirectSerialization(access, type);
        }

        // Arrays, collections and plain objects go through the context property for their type.
        return $"_context.{names.Get(type)}.Serialize(writer, {access}, depth + 1);";
    }

    private static string GeneratePropertyDeserialization(IPropertySymbol property, ContextTypeNames names)
    {
        var type = property.Type;

        // A custom converter takes priority over every other dispatch rule.
        var converterType = GetConverterType(property);
        if (converterType != null)
        {
            return $"{GetConverterFieldName(converterType)}.Read(reader)";
        }

        // See GeneratePropertySerialization.
        if (IsUnsupportedUnstubbableType(type))
        {
            return GenerateUnsupportedRead(type);
        }

        if (IsUnsupportedStubbedType(type))
        {
            return $"_context.{names.Get(type)}.Deserialize(reader, depth + 1)";
        }

        // [NCborEnumAsString] takes priority over the default numeric enum encoding.
        if (HasEnumAsStringAttribute(property))
        {
            if (type.TypeKind == TypeKind.Enum)
            {
                var enumTypeName = type.ToDisplayString();
                return $"({enumTypeName})System.Enum.Parse(typeof({enumTypeName}), reader.ReadTextString())";
            }
            if (TryGetNullableEnumUnderlyingType(type, out var nullableEnumType))
            {
                var enumTypeName = nullableEnumType!.ToDisplayString();
                return $"reader.PeekState() == CborReaderState.Null ? ReadNullEnum<{enumTypeName}>(reader) : ({enumTypeName})System.Enum.Parse(typeof({enumTypeName}), reader.ReadTextString())";
            }
        }

        // Nullable<built-in>
        if (TryGetNullableUnderlyingType(type, out var underlyingType) && IsBuiltInType(underlyingType!))
        {
            // For enums, use inline null checking instead of helper methods
            if (underlyingType!.TypeKind == TypeKind.Enum)
            {
                var enumDeserialization = GenerateDirectDeserialization(underlyingType);
                return $"reader.PeekState() == CborReaderState.Null ? ReadNullEnum<{underlyingType.ToDisplayString()}>(reader) : {enumDeserialization}";
            }
            return $"ReadNullable{underlyingType.Name}(reader)";
        }

        var nonNullableType = StripNullableAnnotation(type);
        var core = GenerateNonNullPropertyRead(nonNullableType, names);

        // Mirrors GeneratePropertySerialization: CBOR null reads back as null for every
        // reference-type property. `!` because the target may be declared non-nullable — a null
        // on the wire was written from a null value, so it round-trips (as in System.Text.Json).
        if (type.IsReferenceType)
        {
            return $"(reader.PeekState() == CborReaderState.Null ? ReadNullValue<{nonNullableType.ToDisplayString()}>(reader)! : {core})";
        }

        return core;
    }

    /// <summary>Expression reading a non-null property value.</summary>
    private static string GenerateNonNullPropertyRead(ITypeSymbol type, ContextTypeNames names)
    {
        if (IsByteArray(type))
        {
            return "reader.ReadByteString()";
        }

        if (IsBuiltInType(type))
        {
            return GenerateDirectDeserialization(type);
        }

        return $"_context.{names.Get(type)}.Deserialize(reader, depth + 1)";
    }

    /// <summary>
    /// True for every array-shaped collection in the <see cref="TryGetCollectionInfo"/> table —
    /// list-like <i>and</i> set-like (<c>HashSet&lt;T&gt;</c>, <c>ISet&lt;T&gt;</c>, ...), so callers such
    /// as <c>IsPlainObjectType</c> never treat a set as a POCO (CBOR005 on its <c>Count</c>).
    /// </summary>
    private static bool IsList(ITypeSymbol typeSymbol, out ITypeSymbol? elementType)
    {
        if (TryGetCollectionInfo(typeSymbol, out var info) && info.Kind != CollectionKind.Dictionary)
        {
            elementType = info.ElementType;
            return true;
        }
        elementType = null;
        return false;
    }

    private static bool IsDictionary(ITypeSymbol typeSymbol, out ITypeSymbol? keyType, out ITypeSymbol? valueType)
    {
        if (TryGetCollectionInfo(typeSymbol, out var info) && info.Kind == CollectionKind.Dictionary)
        {
            keyType = info.KeyType;
            valueType = info.ValueType;
            return true;
        }
        keyType = null;
        valueType = null;
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

    private static bool IsByteArray(ITypeSymbol typeSymbol)
    {
        return typeSymbol is IArrayTypeSymbol arrayType && arrayType.Rank == 1 && arrayType.ElementType.SpecialType == SpecialType.System_Byte;
    }

    /// <summary>
    /// The <i>simple</i> context property name for a type (<c>Item</c>, <c>ListOfItem</c>,
    /// <c>ArrayOfInt32</c>). Distinct types can share it (same simple name in two namespaces), so
    /// generated code never emits it directly: discovery assigns each type its final, unique name
    /// (see <see cref="ContextTypeNames"/>), starting from this one.
    /// </summary>
    internal static string GetPropertyNameFromType(ITypeSymbol typeSymbol) =>
        BuildContextPropertyName(typeSymbol, qualificationLevel: 0, qualifyFramework: false);

    /// <summary>
    /// Builds a context property name. Each named type in <paramref name="typeSymbol"/> — the type
    /// itself, its type arguments, array element types — is prefixed with up to
    /// <paramref name="qualificationLevel"/> of its nearest containing types / namespaces
    /// (<c>B_Item</c>, <c>OuterOfString_Inner</c>), joined with '_'. Primitives, string, Guid,
    /// DateTime, DateTimeOffset and decimal are never qualified (enums are, like any user type);
    /// framework types only when <paramref name="qualifyFramework"/> is set, so that
    /// <c>List&lt;B.Item&gt;</c> reads <c>ListOfB_Item</c> rather than <c>Generic_ListOfB_Item</c>.
    /// </summary>
    internal static string BuildContextPropertyName(ITypeSymbol typeSymbol, int qualificationLevel, bool qualifyFramework)
    {
        // Handle arrays (multi-dimensional ones carry their rank so int[,] != int[])
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            var rankPart = arrayType.Rank == 1 ? "" : $"Rank{arrayType.Rank}";
            return $"Array{rankPart}Of{BuildContextPropertyName(arrayType.ElementType, qualificationLevel, qualifyFramework)}";
        }

        if (GetBuiltInPropertyName(typeSymbol) is { } builtInName)
            return builtInName;

        if (typeSymbol is not INamedTypeSymbol namedType)
            return typeSymbol.Name;

        var ownSegment = GetNameSegment(namedType, qualificationLevel, qualifyFramework);
        if (qualificationLevel == 0 || (!qualifyFramework && IsFrameworkMetadataType(namedType)))
            return ownSegment;

        // Nearest container first: containing types (with their type arguments), then namespaces.
        var containers = new List<string>();
        for (var containing = namedType.ContainingType; containing != null; containing = containing.ContainingType)
            containers.Add(GetNameSegment(containing, qualificationLevel, qualifyFramework));
        for (var ns = namedType.ContainingNamespace; ns != null && !ns.IsGlobalNamespace; ns = ns.ContainingNamespace)
            containers.Add(ns.Name);

        var taken = containers.Take(qualificationLevel).Reverse().ToList();
        taken.Add(ownSegment);
        return string.Join("_", taken);
    }

    /// <summary>
    /// <c>Name</c>, or <c>NameOfArg1AndArg2</c> for a type with type arguments of its own. A type
    /// nested in a generic one (<c>Outer&lt;int&gt;.Inner</c>) has <c>IsGenericType</c> set but no
    /// type arguments, and is just <c>Inner</c> (not <c>InnerOf</c>).
    /// </summary>
    private static string GetNameSegment(INamedTypeSymbol namedType, int qualificationLevel, bool qualifyFramework)
    {
        var name = namedType.Name;
        var backtickIndex = name.IndexOf('`');
        if (backtickIndex >= 0)
            name = name.Substring(0, backtickIndex);
        if (namedType.TypeArguments.Length > 0)
            name += "Of" + string.Join("And", namedType.TypeArguments.Select(a => BuildContextPropertyName(a, qualificationLevel, qualifyFramework)));
        return name;
    }

    /// <summary>The fixed context property name of a built-in type, or null.</summary>
    private static string? GetBuiltInPropertyName(ITypeSymbol typeSymbol)
    {
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

        return null;
    }

    internal static bool IsBuiltInType(ITypeSymbol typeSymbol)
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
        
        // Handle enums - they serialize as their underlying type
        if (typeSymbol.TypeKind == TypeKind.Enum)
        {
            return true;
        }
        
        // Handle other built-in types (not SpecialTypes)
        var displayString = typeSymbol.ToDisplayString();
        return displayString == "System.Guid" ||
               displayString == "System.DateTime" ||
               displayString == "System.DateTimeOffset" ||
               displayString == "System.Decimal" ||
               displayString == "decimal";
    }

    /// <summary>
    /// Byte strings at or under this size are written in one <c>WriteByteString</c> call, same
    /// as before. Larger ones are split into an indefinite-length byte string of chunks this
    /// size — <c>CborReader.ReadByteString()</c> transparently reassembles either encoding into
    /// a single byte[] on the read side, so no deserialization changes are needed.
    /// </summary>
    private const int ByteStringChunkThreshold = 4096;

    /// <summary>
    /// Emits a statement (not an expression — do not append a trailing semicolon) that writes
    /// <paramref name="variableName"/> (a non-null byte[] expression) as a CBOR byte string,
    /// chunking it via an indefinite-length byte string if it exceeds
    /// <see cref="ByteStringChunkThreshold"/>.
    /// </summary>
    private static string GenerateByteStringWrite(string variableName)
    {
        return $"if ({variableName}.Length > {ByteStringChunkThreshold}) " +
               "{ " +
               "writer.WriteStartIndefiniteLengthByteString(); " +
               $"for (int __chunkOffset = 0; __chunkOffset < {variableName}.Length; __chunkOffset += {ByteStringChunkThreshold}) " +
               "{ " +
               $"writer.WriteByteString(new System.ReadOnlySpan<byte>({variableName}, __chunkOffset, System.Math.Min({ByteStringChunkThreshold}, {variableName}.Length - __chunkOffset))); " +
               "} " +
               "writer.WriteEndIndefiniteLengthByteString(); " +
               "} " +
               $"else {{ writer.WriteByteString({variableName}); }}";
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
        
        // Handle enums - serialize as their underlying numeric value
        if (typeSymbol.TypeKind == TypeKind.Enum)
        {
            if (typeSymbol is INamedTypeSymbol namedEnum)
            {
                var underlyingType = namedEnum.EnumUnderlyingType;
                if (underlyingType != null)
                {
                    return underlyingType.SpecialType switch
                    {
                        SpecialType.System_Byte => $"writer.WriteUInt32((uint)(byte){variableName});",
                        SpecialType.System_SByte => $"writer.WriteInt32((int)(sbyte){variableName});",
                        SpecialType.System_Int16 => $"writer.WriteInt32((int)(short){variableName});",
                        SpecialType.System_UInt16 => $"writer.WriteUInt32((uint)(ushort){variableName});",
                        SpecialType.System_Int32 => $"writer.WriteInt32((int){variableName});",
                        SpecialType.System_UInt32 => $"writer.WriteUInt32((uint){variableName});",
                        SpecialType.System_Int64 => $"writer.WriteInt64((long){variableName});",
                        SpecialType.System_UInt64 => $"writer.WriteUInt64((ulong){variableName});",
                        _ => $"writer.WriteInt32((int){variableName});" // Default to int
                    };
                }
            }
            return $"writer.WriteInt32((int){variableName});"; // Fallback
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
        
        // Handle enums - deserialize from their underlying numeric value
        if (typeSymbol.TypeKind == TypeKind.Enum)
        {
            if (typeSymbol is INamedTypeSymbol namedEnum)
            {
                var underlyingType = namedEnum.EnumUnderlyingType;
                var enumTypeName = typeSymbol.ToDisplayString();
                if (underlyingType != null)
                {
                    return underlyingType.SpecialType switch
                    {
                        SpecialType.System_Byte => $"({enumTypeName})(byte)reader.ReadUInt32()",
                        SpecialType.System_SByte => $"({enumTypeName})(sbyte)reader.ReadInt32()",
                        SpecialType.System_Int16 => $"({enumTypeName})(short)reader.ReadInt32()",
                        SpecialType.System_UInt16 => $"({enumTypeName})(ushort)reader.ReadUInt32()",
                        SpecialType.System_Int32 => $"({enumTypeName})reader.ReadInt32()",
                        SpecialType.System_UInt32 => $"({enumTypeName})reader.ReadUInt32()",
                        SpecialType.System_Int64 => $"({enumTypeName})reader.ReadInt64()",
                        SpecialType.System_UInt64 => $"({enumTypeName})reader.ReadUInt64()",
                        _ => $"({enumTypeName})reader.ReadInt32()" // Default to int
                    };
                }
            }
            return $"({typeSymbol.ToDisplayString()})reader.ReadInt32()"; // Fallback
        }
        
        return $"// TODO: Implement direct deserialization for {typeSymbol.ToDisplayString()}";
    }

    private static string ApplyNamingPolicy(string name, NCborNamingPolicy policy)
    {
        return policy switch
        {
            NCborNamingPolicy.CamelCase => ToCamelCase(name),
            NCborNamingPolicy.SnakeCaseLower => ToSnakeCase(name, upper: false),
            NCborNamingPolicy.SnakeCaseUpper => ToSnakeCase(name, upper: true),
            NCborNamingPolicy.KebabCaseLower => ToKebabCase(name, upper: false),
            NCborNamingPolicy.KebabCaseUpper => ToKebabCase(name, upper: true),
            NCborNamingPolicy.UpperCase => name.ToUpperInvariant(),
            NCborNamingPolicy.LowerCase => name.ToLowerInvariant(),
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
