namespace NCbor.Generator;

/// <summary>
/// Plain-object ("POCO") support: which properties a type exposes to serialization (including
/// inherited ones), which constructor deserialization uses, and the code that builds the
/// instance on the read side.
/// </summary>
internal static partial class SerializationCodeGenerator
{
    private const string SetsRequiredMembersAttributeName = "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute";

    private enum ConstructorKind
    {
        /// <summary>Build with <c>new T()</c> (an accessible parameterless constructor, or a
        /// type outside this feature's scope — abstract/interface/Nullable — kept on the legacy
        /// path so behavior there is unchanged).</summary>
        Parameterless,

        /// <summary>Build with a constructor that takes arguments read from the payload.</summary>
        Parameterized,

        /// <summary>No usable constructor: CBOR010 is reported and Deserialize throws.</summary>
        None,
    }

    private sealed class ConstructorResolution
    {
        private ConstructorResolution(ConstructorKind kind, IMethodSymbol? constructor, string? problem)
        {
            Kind = kind;
            Constructor = constructor;
            Problem = problem;
        }

        public ConstructorKind Kind { get; }

        /// <summary>The chosen constructor, parameterless included when it's known (needed to
        /// look up <c>[SetsRequiredMembers]</c>). Null for <see cref="ConstructorKind.None"/>.</summary>
        public IMethodSymbol? Constructor { get; }

        /// <summary>Human-readable reason for <see cref="ConstructorKind.None"/>.</summary>
        public string? Problem { get; }

        /// <summary>The constructor whose parameters are fed from the payload, or null when the
        /// instance is built with <c>new T()</c> (or can't be built at all).</summary>
        public IMethodSymbol? ParameterizedConstructor => Kind == ConstructorKind.Parameterized ? Constructor : null;

        public static ConstructorResolution Parameterless(IMethodSymbol? constructor) => new(ConstructorKind.Parameterless, constructor, null);
        public static ConstructorResolution Parameterized(IMethodSymbol constructor) => new(ConstructorKind.Parameterized, constructor, null);
        public static ConstructorResolution NoneUsable(string problem) => new(ConstructorKind.None, null, problem);
    }

    /// <summary>
    /// Emits the Deserialize body for a plain object type. Three shapes:
    /// <list type="bullet">
    /// <item>Fast path — <c>new T()</c> then assign each property as its key is read. Used for
    /// plain settable POCOs (no constructor parameters, no init-only or required members) and
    /// kept byte-for-byte identical to the generator's historical output.</item>
    /// <item>General path — read every value into a local (plus a "was present" flag for plain
    /// setters), then <c>new T(ctorArgs) { InitOrRequired = ... }</c>, then assign plain setters
    /// only when their key was present so property initializers survive an omitted key.</item>
    /// <item>No usable constructor — throws; CBOR010 reports the problem at build time.</item>
    /// </list>
    /// </summary>
    private static string GenerateObjectDeserialization(INamedTypeSymbol typeSymbol, NCborNamingPolicy namingPolicy, NCborIgnoreCondition defaultIgnoreCondition, ContextTypeNames names)
    {
        var builder = new StringBuilder();
        var resolution = ResolveConstructor(typeSymbol);
        var typeName = typeSymbol.ToDisplayString();

        if (resolution.Kind == ConstructorKind.None)
        {
            var message = $"Type '{typeSymbol.Name}' has no constructor NCbor can use for deserialization ({resolution.Problem}). See generator diagnostic CBOR010.";
            builder.AppendLine($"throw new NCborDeserializationException(typeof({typeName}), {ToStringLiteral(message)});");
            return builder.ToString();
        }

        var properties = GetSerializableProperties(typeSymbol, defaultIgnoreCondition).ToList();
        var requiredMembers = GetRequiredMembers(typeSymbol);

        if (resolution.Kind == ConstructorKind.Parameterless && requiredMembers.Count == 0 && !properties.Any(IsInitOnlyProperty))
        {
            builder.AppendLine($"var result = new {typeName}();");
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
                builder.AppendLine($"            result.{property.Name} = {GeneratePropertyDeserialization(property, names)};");
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

        var constructor = resolution.ParameterizedConstructor;
        var setsRequiredMembers = resolution.Constructor != null && HasAttribute(resolution.Constructor, SetsRequiredMembersAttributeName);
        var requiredNames = new HashSet<string>(requiredMembers.Select(m => m.Name), StringComparer.Ordinal);

        // A property is fed from exactly one place: a constructor argument, the object
        // initializer (init-only / required — neither can be assigned afterwards), or a
        // conditional assignment after construction (plain setter).
        bool IsConstructorArgument(IPropertySymbol p) => constructor != null && FindMatchingParameter(constructor, p) != null;
        bool IsInInitializer(IPropertySymbol p) => requiredNames.Contains(p.Name) || (!IsConstructorArgument(p) && IsInitOnlyProperty(p));
        bool IsConditionallyAssigned(IPropertySymbol p) => !IsConstructorArgument(p) && !IsInInitializer(p);

        foreach (var property in properties)
        {
            var propertyType = property.Type.ToDisplayString();
            // Constructor/initializer values are always passed, present in the payload or not.
            // [NCborDefaultValue] omits the key on write when the value equals the default, so
            // seed with that default — otherwise the read side would get default(T) instead.
            var seed = "default!";
            if (!IsConditionallyAssigned(property) &&
                GetDefaultValueAttribute(property) is { ConstructorArguments.Length: > 0 } defaultValueAttr)
            {
                seed = $"({propertyType})({FormatDefaultValueLiteral(defaultValueAttr.ConstructorArguments[0])})!";
            }
            builder.AppendLine($"{propertyType} __v_{property.Name} = {seed};");
            if (IsConditionallyAssigned(property))
            {
                builder.AppendLine($"bool __h_{property.Name} = false;");
            }
        }
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
            builder.AppendLine($"            __v_{property.Name} = {GeneratePropertyDeserialization(property, names)};");
            if (IsConditionallyAssigned(property))
            {
                builder.AppendLine($"            __h_{property.Name} = true;");
            }
            builder.AppendLine("            break;");
        }
        builder.AppendLine("        default:");
        builder.AppendLine("            reader.SkipValue();");
        builder.AppendLine("            break;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine("reader.ReadEndMap();");

        var ctorArgs = constructor == null
            ? Enumerable.Empty<string>()
            : constructor.Parameters.Select(p =>
            {
                var matched = FindMatchingProperty(properties, p);
                return matched != null ? $"__v_{matched.Name}" : $"default({p.Type.ToDisplayString()})!";
            });

        var initializers = properties.Where(IsInInitializer).Select(p => $"{p.Name} = __v_{p.Name}").ToList();
        if (!setsRequiredMembers)
        {
            // Required members that aren't serialized (ignored, or fields) must still be set or
            // the object creation doesn't compile (CS9035).
            var serializedNames = new HashSet<string>(properties.Select(p => p.Name), StringComparer.Ordinal);
            initializers.AddRange(requiredMembers.Where(m => !serializedNames.Contains(m.Name)).Select(m => $"{m.Name} = default!"));
        }

        var creation = $"new {typeName}({string.Join(", ", ctorArgs)})";
        builder.AppendLine(initializers.Count == 0
            ? $"var result = {creation};"
            : $"var result = {creation} {{ {string.Join(", ", initializers)} }};");

        foreach (var property in properties.Where(IsConditionallyAssigned))
        {
            builder.AppendLine($"if (__h_{property.Name}) result.{property.Name} = __v_{property.Name};");
        }

        builder.AppendLine("return result;");
        return builder.ToString();
    }

    /// <summary>
    /// Every instance property NCbor can read on <paramref name="typeSymbol"/>, across the whole
    /// base chain: base-first, declaration order within each level. A property re-declared in a
    /// derived class (<c>override</c> or <c>new</c>) appears once, as the most-derived symbol, in
    /// the slot of its first declaration — exactly what <c>value.Name</c> binds to in the
    /// generated code. Excludes static properties, indexers, the record-synthesized
    /// <c>EqualityContract</c>, and anything the generated code can't reach (see
    /// <see cref="IsReachable"/>), including properties whose getter it can't call.
    /// </summary>
    private static List<IPropertySymbol> GetAllInstanceProperties(INamedTypeSymbol typeSymbol)
    {
        var ordered = new List<IPropertySymbol>();
        var slotByName = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var level in GetTypeChainBaseFirst(typeSymbol))
        {
            foreach (var property in level.GetMembers().OfType<IPropertySymbol>())
            {
                // Inaccessible members are dropped *before* de-duplication: C# member lookup
                // ignores them, so a private `new` property doesn't hide a public base one.
                if (property.IsStatic || property.IsIndexer || IsEqualityContract(property) || !IsReachable(property))
                    continue;

                if (slotByName.TryGetValue(property.Name, out var slot))
                {
                    ordered[slot] = property;
                }
                else
                {
                    slotByName[property.Name] = ordered.Count;
                    ordered.Add(property);
                }
            }
        }

        return ordered.Where(p => GetEffectiveAccessor(p, getter: true) is { } getter && IsReachable(getter)).ToList();
    }

    private static IEnumerable<INamedTypeSymbol> GetTypeChainBaseFirst(INamedTypeSymbol typeSymbol)
    {
        var chain = new List<INamedTypeSymbol>();
        for (var current = typeSymbol; current != null; current = current.BaseType)
        {
            if (current.SpecialType == SpecialType.System_Object || current.SpecialType == SpecialType.System_ValueType)
                break;
            // Framework base types (List<T>, Exception, Component, ...) contribute no members.
            if (!SymbolEqualityComparer.Default.Equals(current, typeSymbol) && IsFrameworkMetadataType(current))
                break;
            chain.Add(current);
        }
        chain.Reverse();
        return chain;
    }

    private static bool IsEqualityContract(IPropertySymbol property) =>
        property.Name == "EqualityContract" && property.ContainingType.IsRecord;

    /// <summary>
    /// Whether generated code (a nested class of the user's context, in the same assembly as the
    /// context) can access <paramref name="symbol"/>: public always; internal / protected
    /// internal only when the member is declared in this compilation; private / protected /
    /// private protected never.
    /// </summary>
    private static bool IsReachable(ISymbol symbol)
    {
        switch (symbol.DeclaredAccessibility)
        {
            case Accessibility.Public:
                return true;
            case Accessibility.Internal:
            case Accessibility.ProtectedOrInternal:
                return IsDeclaredInSource(symbol);
            default:
                return false;
        }
    }

    private static bool IsDeclaredInSource(ISymbol symbol)
    {
        var owner = symbol.ContainingType?.OriginalDefinition ?? symbol.OriginalDefinition;
        return owner.Locations.Any(l => l.IsInSource);
    }

    /// <summary>
    /// The getter or setter a property actually exposes. An override may declare only one
    /// accessor and inherit the other, so walk the overridden-property chain.
    /// </summary>
    private static IMethodSymbol? GetEffectiveAccessor(IPropertySymbol property, bool getter)
    {
        for (var current = property; current != null; current = current.OverriddenProperty)
        {
            var accessor = getter ? current.GetMethod : current.SetMethod;
            if (accessor != null)
                return accessor;
        }
        return null;
    }

    /// <summary>A setter (or init accessor) the generated code can call.</summary>
    private static bool HasUsableSetter(IPropertySymbol property) =>
        GetEffectiveAccessor(property, getter: false) is { } setter && IsReachable(setter);

    private static bool IsInitOnlyProperty(IPropertySymbol property) =>
        GetEffectiveAccessor(property, getter: false) is { IsInitOnly: true };

    /// <summary>
    /// Required properties and fields across the base chain (most-derived declaration wins).
    /// Every one of them has to appear in the object initializer unless the constructor is
    /// <c>[SetsRequiredMembers]</c>.
    /// </summary>
    private static List<ISymbol> GetRequiredMembers(INamedTypeSymbol typeSymbol)
    {
        var ordered = new List<ISymbol>();
        var slotByName = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var level in GetTypeChainBaseFirst(typeSymbol))
        {
            foreach (var member in level.GetMembers())
            {
                if (member.IsStatic || member is not (IPropertySymbol { IsIndexer: false } or IFieldSymbol { IsImplicitlyDeclared: false }))
                    continue;

                if (slotByName.TryGetValue(member.Name, out var slot))
                {
                    ordered[slot] = member;
                }
                else
                {
                    slotByName[member.Name] = ordered.Count;
                    ordered.Add(member);
                }
            }
        }

        return ordered.Where(m => m is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true }).ToList();
    }

    /// <summary>
    /// Picks the constructor deserialization builds the instance with:
    /// <list type="number">
    /// <item><c>[NCborConstructor]</c> wins (CBOR007 reports more than one).</item>
    /// <item>Otherwise an accessible parameterless constructor (<c>new T()</c>) — except that a
    /// struct's implicit parameterless constructor loses to an explicit matching one below, or
    /// readonly structs / readonly record structs would silently deserialize to default.</item>
    /// <item>Otherwise the single accessible constructor whose parameters all match a property by
    /// name (case-insensitive) and type, ignoring a record's copy constructor; if several match,
    /// the primary constructor among them.</item>
    /// <item>Otherwise none — CBOR010.</item>
    /// </list>
    /// </summary>
    private static ConstructorResolution ResolveConstructor(INamedTypeSymbol typeSymbol)
    {
        // Abstract types, interfaces and Nullable<T> aren't constructible POCOs; leave them on
        // the legacy path (unchanged output) rather than reporting a misleading CBOR010.
        if (typeSymbol.IsAbstract || typeSymbol.TypeKind == TypeKind.Interface ||
            typeSymbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return ConstructorResolution.Parameterless(null);
        }

        var marked = GetConstructorCandidates(typeSymbol).FirstOrDefault();
        if (marked != null)
            return ConstructorResolution.Parameterized(marked);

        var usable = typeSymbol.InstanceConstructors.Where(IsReachable).ToList();
        var parameterless = usable.FirstOrDefault(c => c.Parameters.Length == 0);

        var properties = GetAllInstanceProperties(typeSymbol);
        var matching = usable
            .Where(c => c.Parameters.Length > 0 && !IsCopyConstructor(c, typeSymbol))
            .Where(c => c.Parameters.All(param => properties.Any(prop =>
                string.Equals(prop.Name, param.Name, StringComparison.OrdinalIgnoreCase) &&
                SymbolEqualityComparer.Default.Equals(prop.Type, param.Type))))
            .ToList();

        IMethodSymbol? picked = null;
        if (matching.Count == 1)
        {
            picked = matching[0];
        }
        else if (matching.Count > 1)
        {
            var primary = matching.Where(IsPrimaryConstructor).ToList();
            if (primary.Count == 1)
                picked = primary[0];
        }

        if (parameterless != null)
        {
            if (typeSymbol.IsValueType && parameterless.IsImplicitlyDeclared && picked != null)
                return ConstructorResolution.Parameterized(picked);
            return ConstructorResolution.Parameterless(parameterless);
        }

        if (picked != null)
            return ConstructorResolution.Parameterized(picked);

        if (usable.Count == 0)
            return ConstructorResolution.NoneUsable("it has no public or internal constructor");
        if (matching.Count == 0)
            return ConstructorResolution.NoneUsable("no accessible constructor is parameterless or has parameters that all match a property by name and type");
        return ConstructorResolution.NoneUsable($"{matching.Count} constructors have parameters that all match properties and none of them is the primary constructor, so the choice is ambiguous");
    }

    private static bool IsCopyConstructor(IMethodSymbol constructor, INamedTypeSymbol typeSymbol) =>
        constructor.Parameters.Length == 1 &&
        SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, typeSymbol);

    private static bool IsPrimaryConstructor(IMethodSymbol constructor) =>
        constructor.DeclaringSyntaxReferences.Any(r => r.GetSyntax() is TypeDeclarationSyntax);

    private static bool HasAttribute(ISymbol symbol, string attributeFullName) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == attributeFullName);

    private static string ToStringLiteral(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    /// <summary>
    /// CBOR010: reasons the type can't be deserialized because no constructor is usable. Empty
    /// for non-object types and for types already reported as CBOR007 (ambiguous
    /// <c>[NCborConstructor]</c>).
    /// </summary>
    public static IEnumerable<string> GetConstructorProblems(INamedTypeSymbol typeSymbol)
    {
        if (!IsPlainObjectType(typeSymbol) || HasAmbiguousConstructors(typeSymbol))
            return Enumerable.Empty<string>();

        var resolution = ResolveConstructor(typeSymbol);
        return resolution.Kind == ConstructorKind.None
            ? new[] { resolution.Problem! }
            : Enumerable.Empty<string>();
    }
}
