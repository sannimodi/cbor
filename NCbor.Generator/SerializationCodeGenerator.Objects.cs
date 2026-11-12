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
        var allProperties = GetAllInstanceProperties(typeSymbol);
        // Under [SetsRequiredMembers] the constructor already sets the required members, so they
        // are treated like any other member: not forced into the initializer (which would
        // clobber the constructor's values with defaults) and not required in the payload.
        var requiredNames = setsRequiredMembers
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(requiredMembers.Select(m => m.Name), StringComparer.Ordinal);

        // A property is fed from exactly one place: a constructor argument, the object
        // initializer (init-only / required — neither can be assigned afterwards), or a
        // conditional assignment after construction (plain setter).
        IParameterSymbol? ParameterFor(IPropertySymbol p) => constructor == null ? null : FindMatchingParameter(constructor, p, allProperties);
        bool IsConstructorArgument(IPropertySymbol p) => ParameterFor(p) != null;
        bool IsInInitializer(IPropertySymbol p) => requiredNames.Contains(p.Name) || (!IsConstructorArgument(p) && IsInitOnlyProperty(p));
        bool IsConditionallyAssigned(IPropertySymbol p) => !IsConstructorArgument(p) && !IsInInitializer(p);
        // A required member's key must be in the payload (as in System.Text.Json) — unless NCbor's
        // own writer may omit it ([NCborDefaultValue] / WhenWritingNull / WhenWritingDefault), in
        // which case an absent key means "the omitted value", so it must not throw.
        bool MustBePresent(IPropertySymbol p) => requiredNames.Contains(p.Name) && GetWriteGuard(p, defaultIgnoreCondition) == null;
        bool TracksPresence(IPropertySymbol p) => IsConditionallyAssigned(p) || MustBePresent(p);

        foreach (var property in properties)
        {
            var propertyType = property.Type.ToDisplayString();
            // Constructor/initializer values are always passed, present in the payload or not, so
            // seed them with what an absent key means:
            // - [NCborDefaultValue] omits the key on write when the value equals the default, so
            //   seed with that default — otherwise the read side would get default(T) instead;
            // - otherwise the constructor parameter's own default (`int A = 42`), as a caller
            //   omitting the argument would get — unless an ignore condition may omit the key on
            //   write, where absent means default(T)/null and the parameter default would be wrong.
            var seed = "default!";
            if (!IsConditionallyAssigned(property))
            {
                if (GetDefaultValueAttribute(property) is { ConstructorArguments.Length: > 0 } defaultValueAttr)
                {
                    seed = $"({propertyType})({FormatDefaultValueLiteral(defaultValueAttr.ConstructorArguments[0])})!";
                }
                else if (ParameterFor(property) is { } parameter &&
                         GetWriteGuard(property, defaultIgnoreCondition) == null &&
                         FormatParameterDefaultValue(parameter) is { } parameterDefault)
                {
                    seed = parameterDefault;
                }
            }
            builder.AppendLine($"{propertyType} __v_{property.Name} = {seed};");
            if (TracksPresence(property))
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
            if (TracksPresence(property))
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

        foreach (var property in properties.Where(MustBePresent))
        {
            var message = $"Required property '{property.Name}' (CBOR key '{GetPropertyName(property, namingPolicy)}') was not found in the payload.";
            builder.AppendLine($"if (!__h_{property.Name}) throw new NCborDeserializationException(typeof({typeName}), {SymbolDisplay.FormatLiteral(message, quote: true)});");
        }

        // Parameters with no serialized property (unmatched, or matched to an ignored one) get
        // their own default value when they declare one, else default(T).
        var ctorArgs = constructor == null
            ? Enumerable.Empty<string>()
            : constructor.Parameters.Select(p =>
            {
                var matched = FindSerializedPropertyForParameter(p, allProperties, properties);
                return matched != null
                    ? $"__v_{matched.Name}"
                    : FormatParameterDefaultValue(p) ?? $"default({p.Type.ToDisplayString()})!";
            });

        var initializers = properties.Where(IsInInitializer).Select(p => $"{p.Name} = __v_{p.Name}").ToList();
        if (!setsRequiredMembers)
        {
            // Required members that aren't serialized (ignored, or fields — see CBOR014) must
            // still be set or the object creation doesn't compile (CS9035).
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
    /// Every instance property NCbor can read on <paramref name="typeSymbol"/>, across its base
    /// chain up to (not including) the first framework base type (see
    /// <see cref="GetTypeChainBaseFirst"/>): base-first, declaration order within each level. A
    /// property re-declared in a derived class (<c>override</c> or <c>new</c>, static or not)
    /// appears once, as the most-derived symbol, in the slot of its first declaration — exactly
    /// what <c>value.Name</c> binds to in the generated code; if that symbol is static (a
    /// <c>new static</c> property hiding an instance one) the name is dropped. Excludes static
    /// properties, indexers, the record-synthesized <c>EqualityContract</c>, and anything the
    /// generated code can't reach (see <see cref="IsReachable"/>), including properties whose
    /// getter it can't call.
    /// </summary>
    private static List<IPropertySymbol> GetAllInstanceProperties(INamedTypeSymbol typeSymbol)
    {
        var ordered = new List<IPropertySymbol>();
        var slotByName = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var level in GetTypeChainBaseFirst(typeSymbol, stopAtFrameworkBase: true))
        {
            foreach (var property in level.GetMembers().OfType<IPropertySymbol>())
            {
                // Inaccessible members are dropped *before* de-duplication: C# member lookup
                // ignores them, so a private `new` property doesn't hide a public base one.
                // Statics are dropped *after* it: a `new static` property hides an instance one
                // of the same name, so `value.Name` would bind to the static (CS0176).
                if (property.IsIndexer || IsEqualityContract(property) || !IsReachable(property))
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

        return ordered
            .Where(p => !p.IsStatic)
            .Where(p => GetEffectiveAccessor(p, getter: true) is { } getter && IsReachable(getter))
            .ToList();
    }

    /// <summary>
    /// <paramref name="typeSymbol"/> and its base types, base-first, excluding
    /// <c>object</c>/<c>ValueType</c>. With <paramref name="stopAtFrameworkBase"/>, the walk also
    /// stops at the first <i>base</i> type that is a framework type (see
    /// <see cref="IsFrameworkType"/>): a user type deriving from <c>Exception</c>,
    /// <c>Component</c>, <c>Attribute</c>, <c>List&lt;T&gt;</c>, ... serializes only the members
    /// declared on its own (non-framework) levels, not <c>HResult</c>/<c>Site</c>/<c>TypeId</c>.
    /// User base types from other assemblies are still walked. The starting type itself is never
    /// cut off.
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> GetTypeChainBaseFirst(INamedTypeSymbol typeSymbol, bool stopAtFrameworkBase)
    {
        var chain = new List<INamedTypeSymbol>();
        for (var current = typeSymbol; current != null; current = current.BaseType)
        {
            if (current.SpecialType == SpecialType.System_Object || current.SpecialType == SpecialType.System_ValueType)
                break;
            if (stopAtFrameworkBase && chain.Count > 0 && IsFrameworkType(current))
                break;
            chain.Add(current);
        }
        chain.Reverse();
        return chain;
    }

    /// <summary>
    /// A type that comes from metadata (not this compilation) and lives in the <c>System</c> or
    /// <c>Microsoft</c> namespace or one of their sub-namespaces.
    /// </summary>
    private static bool IsFrameworkType(INamedTypeSymbol type)
    {
        if (IsDeclaredInSource(type))
            return false;

        var ns = type.OriginalDefinition.ContainingNamespace;
        if (ns == null)
            return false;
        while (ns.ContainingNamespace is { IsGlobalNamespace: false } parent)
            ns = parent;
        return !ns.IsGlobalNamespace && (ns.Name == "System" || ns.Name == "Microsoft");
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

    /// <summary>A usable setter that isn't <c>init</c> — assignable after construction.</summary>
    private static bool HasPlainSetter(IPropertySymbol property) =>
        HasUsableSetter(property) && !IsInitOnlyProperty(property);

    /// <summary>
    /// Required properties and fields across the base chain (most-derived declaration wins).
    /// Every one of them has to appear in the object initializer unless the constructor is
    /// <c>[SetsRequiredMembers]</c>.
    /// </summary>
    private static List<ISymbol> GetRequiredMembers(INamedTypeSymbol typeSymbol)
    {
        var ordered = new List<ISymbol>();
        var slotByName = new Dictionary<string, int>(StringComparer.Ordinal);
        // The full chain, framework bases included: a required member anywhere has to be set or
        // the object creation doesn't compile (CS9035), even one NCbor doesn't serialize.
        foreach (var level in GetTypeChainBaseFirst(typeSymbol, stopAtFrameworkBase: false))
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
    /// struct's implicit parameterless constructor loses to an explicit matching one below when
    /// that constructor covers a property with no plain setter, or readonly structs / readonly
    /// record structs would silently deserialize to default.</item>
    /// <item>Otherwise the single accessible constructor whose parameters all match a property by
    /// name (see <see cref="FindMatchingProperty"/>) and type, ignoring a record's copy
    /// constructor; if several match, the primary constructor among them.</item>
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
            .Where(c => c.Parameters.All(param => FindMatchingProperty(properties, param) is { } prop &&
                SymbolEqualityComparer.Default.Equals(prop.Type, param.Type)))
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
            // A struct always has the implicit `new T()`. Prefer the explicit constructor only
            // when it covers a property that setters can't reach (get-only, or init-only as in a
            // readonly record struct); otherwise `new T()` + setters, as for classes, so a
            // validating constructor isn't run against an absent key.
            if (typeSymbol.IsValueType && parameterless.IsImplicitlyDeclared && picked != null &&
                picked.Parameters.Any(param => FindMatchingProperty(properties, param) is { } prop && !HasPlainSetter(prop)))
            {
                return ConstructorResolution.Parameterized(picked);
            }
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

    /// <summary>
    /// A C# expression for <paramref name="parameter"/>'s explicit default value (<c>= 42</c>,
    /// <c>= "def"</c>, <c>= MyEnum.B</c>, <c>= null</c>, <c>= default</c>), cast to the
    /// parameter's type; null when it declares none.
    /// </summary>
    private static string? FormatParameterDefaultValue(IParameterSymbol parameter)
    {
        if (!parameter.HasExplicitDefaultValue)
            return null;

        var typeName = parameter.Type.ToDisplayString();
        var value = parameter.ExplicitDefaultValue;
        // `= null` / `= default` on any type (reference, Nullable<T>, or a struct like Guid).
        if (value == null)
            return $"default({typeName})!";

        // Enum (and Nullable<enum>) defaults arrive as the boxed underlying number.
        var isEnum = parameter.Type.TypeKind == TypeKind.Enum || TryGetNullableEnumUnderlyingType(parameter.Type, out _);
        var literal = isEnum
            ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "0"
            : FormatConstantLiteral(value);
        return $"({typeName})({literal})";
    }

    /// <summary>A C# literal for a compile-time constant value (non-null, non-enum).</summary>
    private static string FormatConstantLiteral(object value)
    {
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        return value switch
        {
            bool b => b ? "true" : "false",
            string s => SymbolDisplay.FormatLiteral(s, quote: true),
            char c => SymbolDisplay.FormatLiteral(c, quote: true),
            double d when double.IsNaN(d) => "double.NaN",
            double d when double.IsPositiveInfinity(d) => "double.PositiveInfinity",
            double d when double.IsNegativeInfinity(d) => "double.NegativeInfinity",
            double d => d.ToString("R", invariant) + "d",
            float f when float.IsNaN(f) => "float.NaN",
            float f when float.IsPositiveInfinity(f) => "float.PositiveInfinity",
            float f when float.IsNegativeInfinity(f) => "float.NegativeInfinity",
            float f => f.ToString("R", invariant) + "f",
            decimal m => m.ToString(invariant) + "m",
            long l => l.ToString(invariant) + "L",
            uint ui => ui.ToString(invariant) + "u",
            ulong ul => ul.ToString(invariant) + "ul",
            _ => Convert.ToString(value, invariant) ?? "default",
        };
    }

    private static bool HasAttribute(ISymbol symbol, string attributeFullName) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == attributeFullName);

    private static string ToStringLiteral(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    /// <summary>
    /// CBOR014: <c>required</c> fields the generated Deserialize has to set to <c>default!</c>
    /// (NCbor serializes properties only, but a required member must appear in the object
    /// initializer or the creation doesn't compile). Empty when the constructor deserialization
    /// uses is <c>[SetsRequiredMembers]</c> (the constructor sets them instead), when no
    /// constructor is usable (CBOR010), and for non-object types.
    /// </summary>
    public static IEnumerable<IFieldSymbol> GetDefaultedRequiredFields(INamedTypeSymbol typeSymbol)
    {
        if (!IsPlainObjectType(typeSymbol) || HasAmbiguousConstructors(typeSymbol))
            return Enumerable.Empty<IFieldSymbol>();

        var resolution = ResolveConstructor(typeSymbol);
        if (resolution.Kind == ConstructorKind.None ||
            (resolution.Constructor != null && HasAttribute(resolution.Constructor, SetsRequiredMembersAttributeName)))
        {
            return Enumerable.Empty<IFieldSymbol>();
        }

        return GetRequiredMembers(typeSymbol).OfType<IFieldSymbol>();
    }

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
