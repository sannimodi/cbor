namespace NCbor.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class NCborSourceGenerator : IIncrementalGenerator
{
    private const string SerializableAttributeMetadataName = "NCbor.NCborSerializableAttribute";

    // RS2008 (release tracking) needs AnalyzerReleases.*.md wired up as AdditionalFiles in the
    // csproj; RS1032 objects to CBOR008's existing message punctuation, which is kept verbatim.
    // Both only surface now that the descriptors are static fields rather than inline `new`s.
#pragma warning disable RS2008, RS1032
    private static readonly DiagnosticDescriptor UncoveredGetOnlyProperty = new DiagnosticDescriptor(
        "CBOR005", "NCbor Generator",
        "Property '{0}' on type '{1}' has no public setter and is not covered by a [NCborConstructor] parameter — it will be silently excluded from serialization. Add a setter, cover it in the constructor, or mark it [NCborIgnore] if intentional.",
        "Usage", DiagnosticSeverity.Warning, true);

    private static readonly DiagnosticDescriptor UnmatchedConstructorParameter = new DiagnosticDescriptor(
        "CBOR006", "NCbor Generator",
        "Constructor parameter '{0}' on type '{1}' has no matching property (by name) — it will always deserialize to its default value instead of data from the CBOR payload. Add a property named '{0}' or remove the parameter.",
        "Usage", DiagnosticSeverity.Warning, true);

    private static readonly DiagnosticDescriptor AmbiguousConstructors = new DiagnosticDescriptor(
        "CBOR007", "NCbor Generator",
        "Type '{0}' has more than one constructor marked [NCborConstructor]. Exactly one constructor may be marked for deserialization — remove the attribute from the others.",
        "Usage", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MisusedEnumAsString = new DiagnosticDescriptor(
        "CBOR008", "NCbor Generator",
        "Property '{0}' on type '{1}' is marked [NCborEnumAsString] but its type is not an enum (or nullable enum) — the attribute has no effect and the property serializes normally.",
        "Usage", DiagnosticSeverity.Warning, true);

    private static readonly DiagnosticDescriptor NoUsableConstructor = new DiagnosticDescriptor(
        "CBOR010", "NCbor Generator",
        "Type '{0}' cannot be deserialized: {1}. Add an accessible parameterless constructor, a single constructor whose parameters all match properties by name and type, or mark one constructor [NCborConstructor].",
        "Usage", DiagnosticSeverity.Error, true);
#pragma warning restore RS2008, RS1032

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // ForAttributeWithMetadataName only re-parses syntax trees that changed and only hands the
        // transform the declarations that actually carry [NCborSerializable]. The transform does
        // all symbol-dependent work and returns a fully equatable model (no ISymbol, Compilation,
        // SyntaxNode or Location), so when an edit elsewhere yields an identical model Roslyn
        // skips the AddSource output step entirely.
        var contexts = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                SerializableAttributeMetadataName,
                predicate: static (node, _) => node is ClassDeclarationSyntax c && c.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)),
                transform: static (ctx, ct) => CreateContextModel(ctx, ct))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!)
            .WithTrackingName(TrackingNames.ContextModels);

        context.RegisterSourceOutput(
            contexts.Select(static (model, _) => (model.HintName, model.Source)).WithTrackingName(TrackingNames.GeneratedSource),
            static (spc, output) => spc.AddSource(output.HintName, SourceText.From(output.Source, Encoding.UTF8)));

        // Diagnostics are reported from a separate node joined with the compilation so they can be
        // rehydrated against the real SyntaxTree. A path-only Location.Create(...) location has no
        // SourceTree, which makes `#pragma warning disable CBOR00x` (and per-file .editorconfig
        // severity) silently stop applying. Only contexts that actually have diagnostics reach
        // this node, and the work is a cheap tree lookup — source generation above stays cached.
        var diagnostics = contexts
            .Select(static (model, _) => model.Diagnostics)
            .Where(static diags => diags.Length > 0)
            .Combine(context.CompilationProvider);

        context.RegisterSourceOutput(diagnostics, static (spc, source) =>
        {
            foreach (var diagnostic in source.Left)
                spc.ReportDiagnostic(diagnostic.ToDiagnostic(source.Right));
        });
    }

    private static ContextModel? CreateContextModel(GeneratorAttributeSyntaxContext ctx, CancellationToken cancellationToken)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol typeSymbol || ctx.Attributes.IsEmpty)
            return null;

        // ctx.Attributes only holds the matching attributes written on *this* declaration. A context
        // split across several partial declarations must still see every [NCborSerializable] from
        // all of them, in declaration order — which is what GetAttributes() returns.
        var serializableAttributeClass = ctx.Attributes[0].AttributeClass;
        var serializableAttributes = typeSymbol.GetAttributes()
            .Where(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, serializableAttributeClass))
            .ToImmutableArray();

        if (serializableAttributes.IsEmpty)
            return null;

        // The transform runs once per attributed declaration. Only the declaration holding the
        // first matching attribute emits, so a multi-declaration context yields one model (and one
        // AddSource call) instead of duplicates with the same hint name.
        var firstApplication = serializableAttributes[0].ApplicationSyntaxReference;
        if (firstApplication != null &&
            (firstApplication.SyntaxTree != ctx.TargetNode.SyntaxTree || !ctx.TargetNode.Span.Contains(firstApplication.Span)))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = new List<DiagnosticInfo>();

        // Warn about get-only properties that will be silently dropped from serialization
        // (no setter, and not covered by a [NCborConstructor] parameter if one exists).
        var contextDefaultIgnoreCondition = GetDefaultIgnoreCondition(typeSymbol);
        foreach (var attr in serializableAttributes)
        {
            if (attr.ConstructorArguments.Length == 0)
                continue;
            if (attr.ConstructorArguments[0].Value is not INamedTypeSymbol registeredType)
                continue;

            foreach (var uncovered in SerializationCodeGenerator.GetUncoveredGetOnlyProperties(registeredType, contextDefaultIgnoreCondition))
            {
                diagnostics.Add(DiagnosticInfo.Create(UncoveredGetOnlyProperty, uncovered.Locations.FirstOrDefault(), uncovered.Name, registeredType.Name));
            }

            // Warn about [NCborConstructor] parameters with no matching property — they
            // would otherwise silently deserialize to default(T) forever.
            foreach (var unmatchedParam in SerializationCodeGenerator.GetUnmatchedConstructorParameters(registeredType, contextDefaultIgnoreCondition))
            {
                diagnostics.Add(DiagnosticInfo.Create(UnmatchedConstructorParameter, unmatchedParam.Locations.FirstOrDefault(), unmatchedParam.Name, registeredType.Name));
            }

            // Error: more than one [NCborConstructor] on the same type is ambiguous — the
            // generator has no principled way to pick the "right" one, so this must be an
            // error rather than a silently-arbitrary choice.
            if (SerializationCodeGenerator.HasAmbiguousConstructors(registeredType))
            {
                diagnostics.Add(DiagnosticInfo.Create(AmbiguousConstructors, null, registeredType.Name));
            }

            // Error: no constructor the generated Deserialize can call (see SerializationCodeGenerator.ResolveConstructor).
            foreach (var problem in SerializationCodeGenerator.GetConstructorProblems(registeredType))
                diagnostics.Add(DiagnosticInfo.Create(NoUsableConstructor, registeredType.Locations.FirstOrDefault(), registeredType.Name, problem));

            // Warn about [NCborEnumAsString] applied to a non-enum property — it silently
            // has no effect today.
            foreach (var misused in SerializationCodeGenerator.GetMisusedEnumAsStringProperties(registeredType))
            {
                diagnostics.Add(DiagnosticInfo.Create(MisusedEnumAsString, misused.Locations.FirstOrDefault(), misused.Name, registeredType.Name));
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        var source = GenerateContextSource(typeSymbol, serializableAttributes);

        return new ContextModel(GetHintName(typeSymbol), source, new EquatableArray<DiagnosticInfo>(diagnostics.ToArray()));
    }

    /// <summary>
    /// Fully qualified hint name (e.g. <c>MyApp.Serialization.MyContext.g.cs</c>; nested types are
    /// joined with '.') so same-named contexts in different namespaces or containing types don't
    /// collide. Characters outside [A-Za-z0-9_.] (generic brackets, etc.) become '_'.
    /// </summary>
    private static string GetHintName(INamedTypeSymbol typeSymbol)
    {
        var fullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        const string globalPrefix = "global::";
        if (fullName.StartsWith(globalPrefix, StringComparison.Ordinal))
            fullName = fullName.Substring(globalPrefix.Length);

        var builder = new StringBuilder(fullName.Length + 5);
        foreach (var ch in fullName)
        {
            var valid = (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '_' || ch == '.';
            builder.Append(valid ? ch : '_');
        }

        builder.Append(".g.cs");
        return builder.ToString();
    }

    private static string GenerateContextSource(INamedTypeSymbol contextType, ImmutableArray<AttributeData> serializableAttributes)
    {
        var builder = new StringBuilder();
        var namingPolicy = GetNamingPolicy(contextType);
        var maxDepth = GetMaxDepth(contextType);
        var defaultIgnoreCondition = GetDefaultIgnoreCondition(contextType);
        var namespaceName = contextType.ContainingNamespace.IsGlobalNamespace ? null : contextType.ContainingNamespace.ToDisplayString();

        // Add diagnostic comment
        builder.AppendLine("// Generated by NCborSourceGenerator. This file should only contain the context class with Default, GetTypeInfo<T>(), and type info properties.");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();

        // Add using statements
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Formats.Cbor;");
        builder.AppendLine("using NCbor;");
        builder.AppendLine();

        // Add namespace (block-scoped)
        if (!string.IsNullOrEmpty(namespaceName))
        {
            builder.AppendLine($"namespace {namespaceName}");
            builder.AppendLine("{");
        }

        // Add partial class declaration
        builder.AppendLine($"public partial class {contextType.Name}");
        builder.AppendLine("{");

        // Add Default property
        builder.AppendLine($"    public static {contextType.Name} Default {{ get; }} = new {contextType.Name}();");
        builder.AppendLine();

        // Add GetTypeInfo implementation
        builder.AppendLine("    public override NCborTypeInfo<T> GetTypeInfo<T>()");
        builder.AppendLine("    {");
        builder.AppendLine("        return typeof(T) switch");
        builder.AppendLine("        {");

        foreach (var args in serializableAttributes.Select(x => x.ConstructorArguments))
        {
            if (args.Length == 0)
            {
                continue;
            }

            var typeArg = args[0];

            if (typeArg.Value is not ITypeSymbol typeSymbol)
            {
                continue;
            }

            var typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var propertyName = GetPropertyName(typeSymbol);
            builder.AppendLine($"            Type t when t == typeof({typeName}) => (NCborTypeInfo<T>)(object){propertyName},");
        }

        builder.AppendLine("            _ => throw new ArgumentException($\"Type {typeof(T)} is not registered for serialization\")");
        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine();

        // Add type info properties for each serializable type
        foreach (var args in serializableAttributes.Select(x => x.ConstructorArguments))
        {
            if (args.Length == 0)
            {
                continue;
            }

            var typeArg = args[0];

            if (typeArg.Value is not ITypeSymbol typeSymbol)
            {
                continue;
            }

            // Skip built-in types and nullable built-in types - they don't need context properties
            if (IsBuiltInType(typeSymbol) || IsNullableBuiltInType(typeSymbol))
            {
                continue;
            }

            var typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var propertyName = GetPropertyNameFromType(typeSymbol);
            builder.AppendLine($"    public NCborTypeInfo<{typeName}> {propertyName} {{ get; }}");
        }

        // Add constructor to initialize type info properties with 'this'
        builder.AppendLine($"    public {contextType.Name}()");
        builder.AppendLine("    {");

        foreach (var args in serializableAttributes.Select(x => x.ConstructorArguments))
        {
            if (args.Length == 0)
            {
                continue;
            }

            var typeArg = args[0];

            if (typeArg.Value is not ITypeSymbol typeSymbol)
            {
                continue;
            }

            // Skip built-in types and nullable built-in types - they don't need context properties
            if (IsBuiltInType(typeSymbol) || IsNullableBuiltInType(typeSymbol))
            {
                continue;
            }

            var propertyName = GetPropertyNameFromType(typeSymbol);
            builder.AppendLine($"        {propertyName} = new {propertyName}TypeInfo(this);");
        }

        builder.AppendLine("    }");

        // Add type info implementations for each serializable type
        foreach (var args in serializableAttributes.Select(x => x.ConstructorArguments))
        {
            if (args.Length == 0)
            {
                continue;
            }

            var typeArg = args[0];

            if (typeArg.Value is not ITypeSymbol typeSymbol)
            {
                continue;
            }

            // Skip built-in types and nullable built-in types - they don't need context properties
            if (IsBuiltInType(typeSymbol) || IsNullableBuiltInType(typeSymbol))
            {
                continue;
            }

            var typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var propertyName = GetPropertyNameFromType(typeSymbol);
            builder.AppendLine($"    private sealed class {propertyName}TypeInfo : NCborTypeInfo<{typeName}>");
            builder.AppendLine("    {");
            builder.AppendLine($"        private readonly {contextType.Name} _context;");
            if (typeSymbol is INamedTypeSymbol namedTypeSymbolForConverters)
            {
                // Cache one instance per distinct [NCborConverter] type instead of allocating a
                // new converter on every single Serialize/Deserialize call.
                foreach (var converterType in SerializationCodeGenerator.GetDistinctConverterTypes(namedTypeSymbolForConverters, defaultIgnoreCondition))
                {
                    builder.AppendLine($"        private static readonly {converterType} {SerializationCodeGenerator.GetConverterFieldName(converterType)} = new {converterType}();");
                }
            }
            builder.AppendLine($"        public {propertyName}TypeInfo({contextType.Name} context) => _context = context;");
            builder.AppendLine($"        public override void Serialize(CborWriter writer, {typeName} value, int depth = 0)");
            builder.AppendLine("        {");
            builder.AppendLine($"            if (depth >= {maxDepth}) throw new NCborSerializationException(typeof({typeName}), \"Maximum serialization depth ({maxDepth}) exceeded at depth \" + depth + \". This usually indicates a circular reference; increase NCborSourceGenerationOptionsAttribute.MaxDepth if the object graph is intentionally this deep.\");");
            builder.AppendLine(SerializationCodeGenerator.GenerateSerializationCode(typeSymbol, namingPolicy, "_context", defaultIgnoreCondition));
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine($"        public override {typeName} Deserialize(CborReader reader, int depth = 0)");
            builder.AppendLine("        {");
            builder.AppendLine($"            if (depth >= {maxDepth}) throw new NCborDeserializationException(typeof({typeName}), \"Maximum deserialization depth ({maxDepth}) exceeded at depth \" + depth + \". The CBOR data may be malformed or unexpectedly deep; increase NCborSourceGenerationOptionsAttribute.MaxDepth if intentional.\");");
            builder.AppendLine(SerializationCodeGenerator.GenerateDeserializationCode(typeSymbol, namingPolicy, "_context", defaultIgnoreCondition));
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine();
        }

        // Add nullable helper methods
        GenerateNullableHelpers(builder);

        builder.AppendLine("}"); // close class
        if (!string.IsNullOrEmpty(namespaceName))
        {
            builder.AppendLine("}"); // close namespace
        }

        return builder.ToString();
    }

    private static void GenerateNullableHelpers(StringBuilder builder)
    {
        var primitiveTypes = new[]
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
            ("DateTimeOffset", "ReadTextString")
        };

        foreach (var (typeName, readerMethod) in primitiveTypes)
        {
            var castPrefix = typeName switch
            {
                "Byte" => "(byte)",
                "SByte" => "(sbyte)",
                "Int16" => "(short)",
                "UInt16" => "(ushort)",
                _ => ""
            };

            builder.AppendLine($"    private static {typeName}? ReadNullable{typeName}(CborReader reader)");
            builder.AppendLine("    {");
            builder.AppendLine("        if (reader.PeekState() == CborReaderState.Null)");
            builder.AppendLine("        {");
            builder.AppendLine("            reader.ReadNull();");
            builder.AppendLine("            return null;");
            builder.AppendLine("        }");
            
            if (typeName == "Guid")
            {
                builder.AppendLine("        return new System.Guid(reader.ReadByteString());");
            }
            else if (typeName == "DateTime")
            {
                builder.AppendLine("        return System.DateTime.ParseExact(reader.ReadTag() == System.Formats.Cbor.CborTag.DateTimeString ? reader.ReadTextString() : throw new System.InvalidOperationException(\"Expected DateTimeString tag\"), \"yyyy-MM-ddTHH:mm:ss.FFFFFFFK\", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);");
            }
            else if (typeName == "DateTimeOffset")
            {
                builder.AppendLine("        return System.DateTimeOffset.ParseExact(reader.ReadTag() == System.Formats.Cbor.CborTag.DateTimeString ? reader.ReadTextString() : throw new System.InvalidOperationException(\"Expected DateTimeString tag\"), \"yyyy-MM-ddTHH:mm:ss.FFFFFFFK\", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);");
            }
            else
            {
                builder.AppendLine($"        return {castPrefix}reader.{readerMethod}();");
            }
            
            builder.AppendLine("    }");
            builder.AppendLine();
        }

        // Add helper method for reading null values
        builder.AppendLine("    private static T? ReadNullValue<T>(CborReader reader) where T : class");
        builder.AppendLine("    {");
        builder.AppendLine("        reader.ReadNull();");
        builder.AppendLine("        return null;");
        builder.AppendLine("    }");
        builder.AppendLine();
        
        // Add helper method for reading null enum values
        builder.AppendLine("    private static T? ReadNullEnum<T>(CborReader reader) where T : struct, System.Enum");
        builder.AppendLine("    {");
        builder.AppendLine("        reader.ReadNull();");
        builder.AppendLine("        return null;");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private static string GetPropertyName(ITypeSymbol typeSymbol)
    {
        // Delegate to GetPropertyNameFromType for consistency
        return GetPropertyNameFromType(typeSymbol);
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
        // Arrays (including byte[]) are NOT built-in types - they need context properties.
        // byte[] uses CBOR byte string encoding but still needs a generated TypeInfo class.
        if (typeSymbol is IArrayTypeSymbol)
        {
            return false;
        }
        
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

    private static bool IsNullableBuiltInType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType && 
            namedType.Name == "Nullable" && namedType.TypeArguments.Length == 1)
        {
            var underlyingType = namedType.TypeArguments[0];
            return IsBuiltInType(underlyingType);
        }
        return false;
    }

    private static NCborNamingPolicy GetNamingPolicy(INamedTypeSymbol contextType)
    {
        var attr = contextType.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "NCbor.NCborSourceGenerationOptionsAttribute");

        if (attr != null)
        {
            foreach (var arg in attr.NamedArguments)
            {
                if (arg.Key == "PropertyNamingPolicy" && arg.Value.Value is int val)
                    return (NCborNamingPolicy)val;
            }
        }

        return NCborNamingPolicy.Unspecified;
    }

    private static int GetMaxDepth(INamedTypeSymbol contextType)
    {
        var attr = contextType.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "NCbor.NCborSourceGenerationOptionsAttribute");

        if (attr != null)
        {
            foreach (var arg in attr.NamedArguments)
            {
                if (arg.Key == "MaxDepth" && arg.Value.Value is int val)
                    return val;
            }
        }

        // Matches NCborSourceGenerationOptionsAttribute.MaxDepth's default.
        return 64;
    }

    private static NCborIgnoreCondition GetDefaultIgnoreCondition(INamedTypeSymbol contextType)
    {
        var attr = contextType.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "NCbor.NCborSourceGenerationOptionsAttribute");

        if (attr != null)
        {
            foreach (var arg in attr.NamedArguments)
            {
                if (arg.Key == "DefaultIgnoreCondition" && arg.Value.Value is int val)
                    return (NCborIgnoreCondition)val;
            }
        }

        // NCborSourceGenerationOptionsAttribute.DefaultIgnoreCondition has no field initializer,
        // so its C# default is enum value 0 (Always) purely from member ordering — NOT a safe
        // "unset" fallback (that would silently exclude every unattributed property). When the
        // named argument isn't present at all, fall back to Never instead, matching
        // System.Text.Json's actual default behavior.
        return NCborIgnoreCondition.Never;
    }
}

/// <summary>Step names for incremental-caching assertions (GeneratorDriverOptions.TrackIncrementalGeneratorSteps).</summary>
internal static class TrackingNames
{
    public const string ContextModels = nameof(ContextModels);
    public const string GeneratedSource = nameof(GeneratedSource);
}

/// <summary>
/// Everything RegisterSourceOutput needs for one context class. Value-equatable and free of
/// ISymbol / Compilation / SyntaxNode / Location references, so the incremental pipeline can
/// compare it against the previous run and skip the output step when nothing changed.
/// </summary>
internal sealed class ContextModel : IEquatable<ContextModel>
{
    public ContextModel(string hintName, string source, EquatableArray<DiagnosticInfo> diagnostics)
    {
        HintName = hintName;
        Source = source;
        Diagnostics = diagnostics;
    }

    public string HintName { get; }
    public string Source { get; }
    public EquatableArray<DiagnosticInfo> Diagnostics { get; }

    public bool Equals(ContextModel? other) =>
        other is not null &&
        string.Equals(HintName, other.HintName, StringComparison.Ordinal) &&
        string.Equals(Source, other.Source, StringComparison.Ordinal) &&
        Diagnostics.Equals(other.Diagnostics);

    public override bool Equals(object? obj) => Equals(obj as ContextModel);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = StringComparer.Ordinal.GetHashCode(HintName);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Source);
            hash = (hash * 31) + Diagnostics.GetHashCode();
            return hash;
        }
    }
}

/// <summary>
/// A diagnostic captured in the transform step without holding a <see cref="Location"/> (which
/// can root a SyntaxTree/Compilation). Rehydrated via <see cref="ToDiagnostic"/> at output time.
/// Equality covers descriptor, location and message args, so an unchanged set of diagnostics
/// compares equal across runs.
/// </summary>
internal sealed class DiagnosticInfo : IEquatable<DiagnosticInfo>
{
    private DiagnosticInfo(DiagnosticDescriptor descriptor, LocationInfo? location, EquatableArray<string> messageArgs)
    {
        Descriptor = descriptor;
        SourceLocation = location;
        MessageArgs = messageArgs;
    }

    /// <summary>Always one of the generator's static readonly descriptors (equatable by id/content).</summary>
    public DiagnosticDescriptor Descriptor { get; }
    public LocationInfo? SourceLocation { get; }
    public EquatableArray<string> MessageArgs { get; }

    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location? location, params string[] messageArgs) =>
        new DiagnosticInfo(descriptor, LocationInfo.From(location), new EquatableArray<string>(messageArgs));

    public Diagnostic ToDiagnostic(Compilation compilation) =>
        Diagnostic.Create(Descriptor, SourceLocation?.ToLocation(compilation) ?? Location.None, MessageArgs.ToObjectArray());

    public bool Equals(DiagnosticInfo? other) =>
        other is not null &&
        Descriptor.Equals(other.Descriptor) &&
        Equals(SourceLocation, other.SourceLocation) &&
        MessageArgs.Equals(other.MessageArgs);

    public override bool Equals(object? obj) => Equals(obj as DiagnosticInfo);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = Descriptor.GetHashCode();
            hash = (hash * 31) + (SourceLocation?.GetHashCode() ?? 0);
            hash = (hash * 31) + MessageArgs.GetHashCode();
            return hash;
        }
    }
}

/// <summary>Serializable stand-in for a source <see cref="Location"/>.</summary>
internal sealed class LocationInfo : IEquatable<LocationInfo>
{
    private LocationInfo(string filePath, TextSpan textSpan, LinePositionSpan lineSpan)
    {
        FilePath = filePath;
        TextSpan = textSpan;
        LineSpan = lineSpan;
    }

    public string FilePath { get; }
    public TextSpan TextSpan { get; }
    public LinePositionSpan LineSpan { get; }

    /// <summary>Returns null (→ <see cref="Location.None"/>) for metadata or missing locations.</summary>
    public static LocationInfo? From(Location? location)
    {
        if (location is null || !location.IsInSource || location.SourceTree is null)
            return null;

        return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
    }

    /// <summary>
    /// Prefers a real source location in <paramref name="compilation"/> (so pragma/editorconfig
    /// suppression keeps working); falls back to a path-only location if the tree isn't found.
    /// </summary>
    public Location ToLocation(Compilation compilation)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            // Line span is checked too: in-memory trees (e.g. generator tests) often share an
            // empty FilePath, so the path alone doesn't identify the tree.
            if (string.Equals(tree.FilePath, FilePath, StringComparison.Ordinal) &&
                TextSpan.End <= tree.Length &&
                tree.GetLineSpan(TextSpan).Span.Equals(LineSpan))
            {
                return Location.Create(tree, TextSpan);
            }
        }

        return Location.Create(FilePath, TextSpan, LineSpan);
    }

    public bool Equals(LocationInfo? other) =>
        other is not null &&
        string.Equals(FilePath, other.FilePath, StringComparison.Ordinal) &&
        TextSpan.Equals(other.TextSpan) &&
        LineSpan.Equals(other.LineSpan);

    public override bool Equals(object? obj) => Equals(obj as LocationInfo);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = StringComparer.Ordinal.GetHashCode(FilePath);
            hash = (hash * 31) + TextSpan.GetHashCode();
            hash = (hash * 31) + LineSpan.GetHashCode();
            return hash;
        }
    }
}

/// <summary>
/// Immutable array wrapper with element-wise (value) equality. <see cref="ImmutableArray{T}"/>
/// compares by reference, which would defeat incremental caching.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly T[]? _items;

    public EquatableArray(T[] items) => _items = items;

    public int Length => _items?.Length ?? 0;

    public bool Equals(EquatableArray<T> other)
    {
        var left = _items ?? Array.Empty<T>();
        var right = other._items ?? Array.Empty<T>();
        if (left.Length != right.Length)
            return false;

        for (var i = 0; i < left.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(left[i], right[i]))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (_items is null)
            return 0;

        unchecked
        {
            var hash = 17;
            foreach (var item in _items)
                hash = (hash * 31) + (item is null ? 0 : EqualityComparer<T>.Default.GetHashCode(item));
            return hash;
        }
    }

    public object[] ToObjectArray()
    {
        var items = _items ?? Array.Empty<T>();
        var result = new object[items.Length];
        for (var i = 0; i < items.Length; i++)
            result[i] = items[i]!;
        return result;
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(_items ?? Array.Empty<T>())).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
