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

    private static readonly DiagnosticDescriptor UnsupportedType = new DiagnosticDescriptor(
        "CBOR009", "NCbor Generator",
        "Type '{0}' used by {1} is not supported by NCbor: {2}. Change the type, add [NCborConverter] to the property, or mark it [NCborIgnore].",
        "Usage", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor PropertyNameCollision = new DiagnosticDescriptor(
        "CBOR011", "NCbor Generator",
        "Type '{0}' (reached from {1}) would generate context property '{2}', which is already taken by {3}. Rename one of the types, or nest them so their simple names differ.",
        "Usage", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor NoUsableConstructor = new DiagnosticDescriptor(
        "CBOR010", "NCbor Generator",
        "Type '{0}' cannot be deserialized: {1}. Add an accessible parameterless constructor, a single constructor whose parameters all match properties by name and type, or mark one constructor [NCborConstructor].",
        "Usage", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor NonPartialContainingType = new DiagnosticDescriptor(
        "CBOR012", "NCbor Generator",
        "Context '{0}' is nested in '{1}', which is not declared partial. NCbor generates the context's members inside its containing types, so every containing type must be partial.",
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

        // Hint names are made unique across *all* contexts here, after Collect(): the transform
        // sees one context at a time, and Roslyn compares hint names case-insensitively, so two
        // contexts whose names differ only by case (or that sanitize to the same text) can only
        // be told apart with the full set in view. A duplicate would make AddSource throw, which
        // drops the output of every context (CS8785). The per-context Select stays tracked and
        // cached; only the cheap AddSource loop re-runs.
        var files = contexts
            .Where(static model => model.Source is not null)
            .Select(static (model, _) => (model.HintName, model.DisambiguatedHintName, Source: model.Source!))
            .WithTrackingName(TrackingNames.GeneratedSource);

        context.RegisterSourceOutput(files.Collect(), static (spc, all) =>
        {
            foreach (var (hintName, source) in AssignUniqueHintNames(all))
                spc.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
        });

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

        // Registered types plus everything reachable from them (nested POCOs, collection
        // element/key/value types, ...). Every loop below and in GenerateContextSource iterates
        // this closure, not the raw attributes.
        var contextDefaultIgnoreCondition = GetDefaultIgnoreCondition(typeSymbol);
        var closure = SerializationCodeGenerator.DiscoverTypeClosure(GetRegisteredTypes(serializableAttributes), contextDefaultIgnoreCondition, typeSymbol.Name);

        foreach (var usage in closure.Unsupported)
        {
            diagnostics.Add(DiagnosticInfo.Create(UnsupportedType, usage.Origin.Location, usage.Type.ToDisplayString(), usage.Origin.Description, usage.Reason));
        }

        foreach (var collision in closure.Collisions)
        {
            diagnostics.Add(DiagnosticInfo.Create(PropertyNameCollision, collision.Origin.Location, collision.Type.ToDisplayString(), collision.Origin.Description, collision.PropertyName, collision.ConflictsWith));
        }

        // Warn about get-only properties that will be silently dropped from serialization
        // (no setter, and not covered by a [NCborConstructor] parameter if one exists).
        // Only plain objects: collections, arrays, built-ins and registered Nullable<built-in>
        // (whose HasValue/Value would otherwise trip CBOR005) have no properties to check, and
        // CBOR009 types are never in closure.Types, so they get no CBOR005/006/007/008/010 on top.
        foreach (var registeredType in closure.Types.OfType<INamedTypeSymbol>().Where(t => SerializationCodeGenerator.ClassifyType(t, out _) == TypeShape.PlainObject))
        {
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
            // Reported once, on the second marked constructor (the first one that makes it ambiguous).
            if (SerializationCodeGenerator.GetFirstRedundantMarkedConstructor(registeredType) is { } redundantConstructor)
            {
                diagnostics.Add(DiagnosticInfo.Create(AmbiguousConstructors, redundantConstructor.Locations.FirstOrDefault(), registeredType.Name));
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

        // A nested context is emitted inside `partial` re-declarations of its containing types,
        // which is only possible when every one of them is partial.
        string? source = null;
        var nonPartialContainer = GetContainingTypes(typeSymbol).FirstOrDefault(t => !IsDeclaredPartial(t));
        if (nonPartialContainer != null)
        {
            diagnostics.Add(DiagnosticInfo.Create(NonPartialContainingType, typeSymbol.Locations.FirstOrDefault(), typeSymbol.ToDisplayString(), nonPartialContainer.ToDisplayString()));
        }
        else
        {
            source = GenerateContextSource(typeSymbol, closure);
        }

        var (hintName, disambiguatedHintName) = GetHintNames(typeSymbol);
        return new ContextModel(hintName, disambiguatedHintName, source, new EquatableArray<DiagnosticInfo>(diagnostics.ToArray()));
    }

    /// <summary>The <c>typeof(T)</c> argument of each <c>[NCborSerializable]</c>, in declaration order.</summary>
    private static IEnumerable<RegisteredType> GetRegisteredTypes(ImmutableArray<AttributeData> serializableAttributes)
    {
        foreach (var attr in serializableAttributes)
        {
            if (attr.ConstructorArguments.Length == 0 || attr.ConstructorArguments[0].Value is not ITypeSymbol registeredType)
                continue;
            yield return new RegisteredType(registeredType, attr.ApplicationSyntaxReference?.GetSyntax().GetLocation());
        }
    }

    /// <summary>
    /// Hint names for a context. The readable one is the fully qualified name (e.g.
    /// <c>MyApp.Serialization.MyContext.g.cs</c>; containing types are joined with '.'), with
    /// characters outside [A-Za-z0-9_.] (generic brackets, etc.) turned into '_'. Because that
    /// replacement is lossy (<c>Ctx&lt;T&gt;</c> and <c>Ctx_T_</c> would both become
    /// <c>Ctx_T_</c>), a lossy name also gets a short stable hash of the fully qualified name. The
    /// disambiguated name always carries that hash; <see cref="AssignUniqueHintNames"/> falls back
    /// to it when readable names still collide (e.g. <c>Ctx</c> vs <c>CTX</c>: Roslyn compares
    /// hint names case-insensitively).
    /// </summary>
    private static (string HintName, string DisambiguatedHintName) GetHintNames(INamedTypeSymbol typeSymbol)
    {
        var fullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        const string globalPrefix = "global::";
        if (fullName.StartsWith(globalPrefix, StringComparison.Ordinal))
            fullName = fullName.Substring(globalPrefix.Length);

        var builder = new StringBuilder(fullName.Length + 16);
        var lossy = false;
        foreach (var ch in fullName)
        {
            var valid = (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '_' || ch == '.';
            builder.Append(valid ? ch : '_');
            lossy |= !valid;
        }

        var readable = builder.ToString();
        var hashed = readable + "." + StableHash(fullName);
        return ((lossy ? hashed : readable) + ".g.cs", hashed + ".g.cs");
    }

    /// <summary>
    /// FNV-1a over the UTF-16 code units, as 8 hex digits. Unlike <see cref="string.GetHashCode()"/>
    /// it is the same in every process, so hint names (and generated file paths) are stable.
    /// </summary>
    private static string StableHash(string text)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var ch in text)
            {
                hash = (hash ^ ch) * 16777619u;
            }
            return hash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Picks one hint name per generated file such that no two are equal ignoring case (which is
    /// how Roslyn compares them). Every member of a group whose readable names collide switches to
    /// its disambiguated (hashed) name, so the outcome doesn't depend on which context came first.
    /// A numeric suffix is the last resort for a collision that survives that (only possible
    /// with a hash collision or a type literally named like another's hashed name).
    /// </summary>
    internal static IEnumerable<(string HintName, string Source)> AssignUniqueHintNames(ImmutableArray<(string HintName, string DisambiguatedHintName, string Source)> files)
    {
        var readableCounts = files
            .GroupBy(f => f.HintName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = files
            .Select(f => (Name: readableCounts[f.HintName] > 1 ? f.DisambiguatedHintName : f.HintName, f.Source))
            .OrderBy(f => f.Name, StringComparer.Ordinal);

        foreach (var (name, source) in ordered)
        {
            var unique = name;
            for (var i = 2; !used.Add(unique); i++)
                unique = name.Substring(0, name.Length - ".g.cs".Length) + "." + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".g.cs";
            yield return (unique, source);
        }
    }

    /// <summary>Containing types of <paramref name="typeSymbol"/>, outermost first.</summary>
    private static List<INamedTypeSymbol> GetContainingTypes(INamedTypeSymbol typeSymbol)
    {
        var containers = new List<INamedTypeSymbol>();
        for (var current = typeSymbol.ContainingType; current != null; current = current.ContainingType)
            containers.Add(current);
        containers.Reverse();
        return containers;
    }

    private static bool IsDeclaredPartial(INamedTypeSymbol typeSymbol) =>
        typeSymbol.DeclaringSyntaxReferences.Any(r =>
            r.GetSyntax() is TypeDeclarationSyntax declaration && declaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));

    /// <summary>
    /// <c>class Outer</c>, <c>record struct Outer&lt;T&gt;</c>, ... — the keyword and name (with
    /// type parameters) of a partial re-declaration. Modifiers, constraints and base lists are
    /// left out: each partial part may omit them and they're taken from the user's declaration.
    /// </summary>
    private static string GetPartialDeclarationHead(INamedTypeSymbol typeSymbol)
    {
        var keyword = typeSymbol.IsRecord
            ? (typeSymbol.TypeKind == TypeKind.Struct ? "record struct" : "record")
            : typeSymbol.TypeKind switch
            {
                TypeKind.Struct => "struct",
                TypeKind.Interface => "interface",
                _ => "class",
            };
        return $"{keyword} {GetNameWithTypeParameters(typeSymbol)}";
    }

    private static string GetNameWithTypeParameters(INamedTypeSymbol typeSymbol) =>
        typeSymbol.TypeParameters.Length == 0
            ? typeSymbol.Name
            : $"{typeSymbol.Name}<{string.Join(", ", typeSymbol.TypeParameters.Select(p => p.Name))}>";

    /// <summary>The context's declared accessibility, so an internal/private context stays that way.</summary>
    private static string GetAccessibilityKeyword(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "public ",
        Accessibility.Internal => "internal ",
        Accessibility.Private => "private ",
        Accessibility.Protected => "protected ",
        Accessibility.ProtectedOrInternal => "protected internal ",
        Accessibility.ProtectedAndInternal => "private protected ",
        _ => "",
    };

    private static string GenerateContextSource(INamedTypeSymbol contextType, TypeClosure closure)
    {
        var builder = new StringBuilder();
        var namingPolicy = GetNamingPolicy(contextType);
        var maxDepth = GetMaxDepth(contextType);
        var defaultIgnoreCondition = GetDefaultIgnoreCondition(contextType);
        var namespaceName = contextType.ContainingNamespace.IsGlobalNamespace ? null : contextType.ContainingNamespace.ToDisplayString();

        // Add diagnostic comment
        builder.AppendLine("// Generated by NCborSourceGenerator. This file should only contain the context class with Default, GetTypeInfo<T>(), and type info properties.");
        builder.AppendLine("#nullable enable");
        // List<string> and List<string?> are one symbol to the generator, so they share a single
        // TypeInfo (whose element code is null-tolerant either way); the nested-nullability
        // mismatch warnings that sharing produces are not actionable by the user. CS0693: a generic
        // context (or containing type) with a type parameter named T shadows the generated
        // GetTypeInfo<T>/ReadNullValue<T> type parameters, which is harmless here.
        builder.AppendLine("#pragma warning disable CS8619, CS8620, CS0693");
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

        // Containing types of a nested context (all partial — CBOR012 otherwise), outermost first.
        var containingTypes = GetContainingTypes(contextType);
        foreach (var containingType in containingTypes)
        {
            builder.AppendLine($"partial {GetPartialDeclarationHead(containingType)}");
            builder.AppendLine("{");
        }

        // Add partial class declaration, keeping the user's accessibility (a hard-coded `public`
        // conflicts with an `internal partial class` declaration: CS0262).
        var contextTypeRef = GetNameWithTypeParameters(contextType);
        builder.AppendLine($"{GetAccessibilityKeyword(contextType.DeclaredAccessibility)}partial class {contextTypeRef}");
        builder.AppendLine("{");

        // Add Default property
        builder.AppendLine($"    public static {contextTypeRef} Default {{ get; }} = new {contextTypeRef}();");
        builder.AppendLine();

        // Add GetTypeInfo implementation. Covers registered and discovered types; unsupported
        // types (CBOR009 stubs) are deliberately left out.
        builder.AppendLine("    public override NCborTypeInfo<T> GetTypeInfo<T>()");
        builder.AppendLine("    {");
        builder.AppendLine("        return typeof(T) switch");
        builder.AppendLine("        {");

        foreach (var typeSymbol in closure.Types)
        {
            var typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var propertyName = GetPropertyName(typeSymbol);
            builder.AppendLine($"            Type t when t == typeof({typeName}) => (NCborTypeInfo<T>)(object){propertyName},");
        }

        builder.AppendLine("            _ => throw new ArgumentException($\"Type {typeof(T)} is not registered for serialization\")");
        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine();

        // Add type info properties for each registered, discovered and stubbed type. Public
        // unless the type isn't effectively public (a public property of an internal type is CS0053).
        foreach (var typeSymbol in closure.Types.Concat(closure.StubTypes))
        {
            var typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var propertyName = GetPropertyNameFromType(typeSymbol);
            builder.AppendLine($"    {SerializationCodeGenerator.GetAccessibilityModifier(typeSymbol)} NCborTypeInfo<{typeName}> {propertyName} {{ get; }}");
        }

        // Add constructor to initialize type info properties with 'this'
        builder.AppendLine($"    public {contextType.Name}()");
        builder.AppendLine("    {");

        foreach (var typeSymbol in closure.Types.Concat(closure.StubTypes))
        {
            var propertyName = GetPropertyNameFromType(typeSymbol);
            builder.AppendLine($"        {propertyName} = new {propertyName}TypeInfo(this);");
        }

        builder.AppendLine("    }");

        // Add type info implementations for each serializable type
        foreach (var typeSymbol in closure.Types)
        {
            var typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var propertyName = GetPropertyNameFromType(typeSymbol);
            builder.AppendLine($"    private sealed class {propertyName}TypeInfo : NCborTypeInfo<{typeName}>");
            builder.AppendLine("    {");
            builder.AppendLine($"        private readonly {contextTypeRef} _context;");
            if (typeSymbol is INamedTypeSymbol namedTypeSymbolForConverters)
            {
                // Cache one instance per distinct [NCborConverter] type instead of allocating a
                // new converter on every single Serialize/Deserialize call.
                foreach (var converterType in SerializationCodeGenerator.GetDistinctConverterTypes(namedTypeSymbolForConverters, defaultIgnoreCondition))
                {
                    builder.AppendLine($"        private static readonly {converterType} {SerializationCodeGenerator.GetConverterFieldName(converterType)} = new {converterType}();");
                }
            }
            builder.AppendLine($"        public {propertyName}TypeInfo({contextTypeRef} context) => _context = context;");
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

        // Throwing stubs for unsupported types. The build already fails with CBOR009; these only
        // exist so every reference site still resolves and CBOR009 is the sole error reported.
        foreach (var typeSymbol in closure.StubTypes)
        {
            var typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var propertyName = GetPropertyNameFromType(typeSymbol);
            var message = $"Type '{typeSymbol.ToDisplayString()}' is not supported by NCbor (see diagnostic CBOR009).";
            var messageLiteral = "\"" + message.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            builder.AppendLine($"    private sealed class {propertyName}TypeInfo : NCborTypeInfo<{typeName}>");
            builder.AppendLine("    {");
            builder.AppendLine($"        public {propertyName}TypeInfo({contextTypeRef} context) {{ }}");
            builder.AppendLine($"        public override void Serialize(CborWriter writer, {typeName} value, int depth = 0) => throw new NCborSerializationException(typeof({typeName}), {messageLiteral});");
            builder.AppendLine($"        public override {typeName} Deserialize(CborReader reader, int depth = 0) => throw new NCborDeserializationException(typeof({typeName}), {messageLiteral});");
            builder.AppendLine("    }");
            builder.AppendLine();
        }

        // Add nullable helper methods
        GenerateNullableHelpers(builder);

        builder.AppendLine("}"); // close class
        foreach (var _ in containingTypes)
        {
            builder.AppendLine("}"); // close containing type
        }
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

    private static string GetPropertyNameFromType(ITypeSymbol typeSymbol) => SerializationCodeGenerator.GetPropertyNameFromType(typeSymbol);

    private static bool IsBuiltInType(ITypeSymbol typeSymbol) => SerializationCodeGenerator.IsBuiltInType(typeSymbol);

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
    public ContextModel(string hintName, string disambiguatedHintName, string? source, EquatableArray<DiagnosticInfo> diagnostics)
    {
        HintName = hintName;
        DisambiguatedHintName = disambiguatedHintName;
        Source = source;
        Diagnostics = diagnostics;
    }

    /// <summary>Readable hint name, used unless another context's collides with it.</summary>
    public string HintName { get; }

    /// <summary>Hint name carrying a stable hash of the fully qualified name; used on collision.</summary>
    public string DisambiguatedHintName { get; }

    /// <summary>Generated source, or null when nothing can be generated (e.g. CBOR012).</summary>
    public string? Source { get; }

    public EquatableArray<DiagnosticInfo> Diagnostics { get; }

    public bool Equals(ContextModel? other) =>
        other is not null &&
        string.Equals(HintName, other.HintName, StringComparison.Ordinal) &&
        string.Equals(DisambiguatedHintName, other.DisambiguatedHintName, StringComparison.Ordinal) &&
        string.Equals(Source, other.Source, StringComparison.Ordinal) &&
        Diagnostics.Equals(other.Diagnostics);

    public override bool Equals(object? obj) => Equals(obj as ContextModel);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = StringComparer.Ordinal.GetHashCode(HintName);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(DisambiguatedHintName);
            hash = (hash * 31) + (Source is null ? 0 : StringComparer.Ordinal.GetHashCode(Source));
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
/// compares by reference, which would defeat incremental caching. <c>default</c> behaves as
/// empty, for equality and hashing alike. The constructor copies its argument, so later writes
/// to the caller's array can't change a value already handed to the incremental pipeline.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly T[]? _items;

    public EquatableArray(T[] items) => _items = items.Length == 0 ? null : (T[])items.Clone();

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
        // Same value for default and for an empty array, which Equals treats as equal.
        unchecked
        {
            var hash = 17;
            foreach (var item in _items ?? Array.Empty<T>())
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
