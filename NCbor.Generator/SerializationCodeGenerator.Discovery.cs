namespace NCbor.Generator;

/// <summary>A type registered on the context via <c>[NCborSerializable(typeof(T))]</c>.</summary>
internal readonly struct RegisteredType
{
    public RegisteredType(ITypeSymbol type, Location? location)
    {
        Type = type;
        Location = location;
    }

    public ITypeSymbol Type { get; }

    /// <summary>The attribute application, for diagnostics.</summary>
    public Location? Location { get; }
}

/// <summary>Where a type was first reached during discovery — used to place diagnostics.</summary>
internal readonly struct TypeOrigin
{
    public TypeOrigin(Location? location, string description)
    {
        Location = location;
        Description = description;
    }

    public Location? Location { get; }

    /// <summary>Human-readable usage site, e.g. <c>Order.Items</c> or <c>[NCborSerializable] on MyContext</c>.</summary>
    public string Description { get; }
}

/// <summary>One place an unsupported type is used (CBOR009).</summary>
internal sealed class UnsupportedTypeUsage
{
    public UnsupportedTypeUsage(ITypeSymbol type, TypeOrigin origin, string reason)
    {
        Type = type;
        Origin = origin;
        Reason = reason;
    }

    public ITypeSymbol Type { get; }
    public TypeOrigin Origin { get; }
    public string Reason { get; }
}

/// <summary>
/// The context property name (also the stem of the <c>{Name}TypeInfo</c> class) of every type that
/// gets one, assigned once per context by <see cref="SerializationCodeGenerator.DiscoverTypeClosure"/>.
/// Every site that emits <c>_context.&lt;Name&gt;</c> or a TypeInfo class name looks the name up here,
/// so a type whose simple name is taken (<c>A.Item</c> / <c>B.Item</c>) is referenced by the same
/// disambiguated name everywhere.
/// </summary>
internal sealed class ContextTypeNames
{
    private readonly Dictionary<ITypeSymbol, string> _names;

    public ContextTypeNames(Dictionary<ITypeSymbol, string> names) => _names = names;

    /// <summary>
    /// The assigned name for <paramref name="type"/> (nullable annotations and tuple element names
    /// ignored, as in discovery), as an identifier ready to emit: a keyword is escaped
    /// (<c>@event</c>; the TypeInfo class is then <c>@eventTypeInfo</c>, which is also legal).
    /// Falls back to the simple name for a type discovery never saw rather than throwing — a
    /// generator exception would drop every context's output.
    /// </summary>
    public string Get(ITypeSymbol type) =>
        SerializationCodeGenerator.EscapeIdentifier(
            _names.TryGetValue(SerializationCodeGenerator.NormalizeForDiscovery(type), out var name)
                ? name
                : SerializationCodeGenerator.GetPropertyNameFromType(type));
}

/// <summary>Output of <see cref="SerializationCodeGenerator.DiscoverTypeClosure"/>.</summary>
internal sealed class TypeClosure
{
    public TypeClosure(List<ITypeSymbol> types, List<ITypeSymbol> stubTypes, List<UnsupportedTypeUsage> unsupported, ContextTypeNames names)
    {
        Types = types;
        StubTypes = stubTypes;
        Unsupported = unsupported;
        Names = names;
    }

    /// <summary>
    /// Every supported type that gets a context property and a real <c>{Name}TypeInfo</c>:
    /// registered types first (attribute order, duplicates removed), then types discovered
    /// transitively from them in BFS order.
    /// </summary>
    public List<ITypeSymbol> Types { get; }

    /// <summary>Unsupported types that get a context property backed by a throwing stub TypeInfo.</summary>
    public List<ITypeSymbol> StubTypes { get; }

    public List<UnsupportedTypeUsage> Unsupported { get; }

    /// <summary>Context property names for <see cref="Types"/> and <see cref="StubTypes"/>.</summary>
    public ContextTypeNames Names { get; }
}

internal static partial class SerializationCodeGenerator
{
    /// <summary>
    /// Computes every type the generated context needs a TypeInfo for, starting from the
    /// registered types and following property types (via <see cref="GetSerializableProperties"/>,
    /// so it sees exactly the properties the codegen will emit), collection elements, dictionary
    /// keys/values and array elements. A symbol set guards against recursion
    /// (<c>Node { Node Next; List&lt;Node&gt; Kids }</c>); types are added before being expanded.
    /// <para>
    /// Not discovered (no context property needed): built-ins and <c>Nullable&lt;built-in&gt;</c>
    /// reached through a property or element (written inline), <c>byte[]</c> properties (written
    /// inline — but <c>byte[]</c> as a collection/array element <i>is</i> discovered), and
    /// properties carrying <c>[NCborConverter]</c>. Registered built-ins do get a TypeInfo.
    /// </para>
    /// </summary>
    internal static TypeClosure DiscoverTypeClosure(
        IEnumerable<RegisteredType> roots,
        NCborIgnoreCondition defaultIgnoreCondition,
        INamedTypeSymbol contextType)
    {
        var contextName = contextType.Name;
        var comparer = SymbolEqualityComparer.Default;
        var visited = new HashSet<ITypeSymbol>(comparer);
        var origins = new Dictionary<ITypeSymbol, TypeOrigin>(comparer);
        var queue = new Queue<ITypeSymbol>();
        var ordered = new List<ITypeSymbol>();
        var stubs = new List<ITypeSymbol>();
        var unsupported = new List<UnsupportedTypeUsage>();

        // isRoot: registered built-ins / Nullable<built-in> still get a TypeInfo (so
        // GetTypeInfo<int>() works); discovered ones are written inline and need nothing.
        void Consider(ITypeSymbol rawType, TypeOrigin origin, bool isRoot)
        {
            var type = NormalizeForDiscovery(rawType);

            // typeof(Wrapper<>) is legal in an attribute but names no type the generated code can
            // reference: report it alone, with no stub and none of the per-type checks (CBOR010...).
            if (IsOpenGenericType(type))
            {
                unsupported.Add(new UnsupportedTypeUsage(type, origin, "open generic types cannot be registered (register a closed constructed type with concrete type arguments instead)"));
                return;
            }

            var shape = ClassifyType(type, out var reason);
            switch (shape)
            {
                case TypeShape.Ignored:
                    return;
                case TypeShape.BuiltIn:
                case TypeShape.NullableBuiltIn:
                    if (!isRoot)
                        return;
                    break;
                case TypeShape.Unsupported:
                    unsupported.Add(new UnsupportedTypeUsage(type, origin, reason!));
                    if (CanEmitStub(type) && visited.Add(type))
                    {
                        origins[type] = origin;
                        stubs.Add(type);
                    }
                    return;
            }

            if (visited.Add(type))
            {
                origins[type] = origin;
                ordered.Add(type);
                queue.Enqueue(type);
            }
        }

        foreach (var root in roots)
        {
            Consider(root.Type, new TypeOrigin(root.Location, $"[NCborSerializable] on {contextName}"), isRoot: true);
        }

        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            var origin = origins[type];
            switch (ClassifyType(type, out _))
            {
                case TypeShape.Array:
                    Consider(((IArrayTypeSymbol)type).ElementType, origin, isRoot: false);
                    break;
                case TypeShape.Collection:
                    TryGetCollectionInfo(type, out var info);
                    if (info.Kind == CollectionKind.Dictionary)
                    {
                        Consider(info.KeyType!, origin, isRoot: false);
                        Consider(info.ValueType!, origin, isRoot: false);
                    }
                    else
                    {
                        Consider(info.ElementType!, origin, isRoot: false);
                    }
                    break;
                case TypeShape.PlainObject when type is INamedTypeSymbol namedType:
                    foreach (var property in GetSerializableProperties(namedType, defaultIgnoreCondition))
                    {
                        // A converter replaces all codegen for the property; byte[]/byte[]? and
                        // built-ins are written inline (Consider skips the latter).
                        if (GetConverterType(property) != null || IsByteArray(property.Type))
                            continue;
                        // Named after the declaring type, not the type being walked: a base member
                        // reached from several derived types then yields identical diagnostics,
                        // which the generator de-duplicates (one CBOR009 per declaration).
                        var propertyOrigin = new TypeOrigin(property.Locations.FirstOrDefault(), $"{property.ContainingType.Name}.{property.Name}");
                        Consider(property.Type, propertyOrigin, isRoot: false);
                    }
                    break;
            }
        }

        var names = AssignContextPropertyNames(ordered.Concat(stubs), GetReservedMemberNames(contextType));
        return new TypeClosure(ordered, stubs, unsupported, names);
    }

    /// <summary>
    /// Gives every type a context property name, deterministically: types are taken in closure
    /// order (registered types in attribute order, then discovered types in BFS order, then
    /// stubs). The first claimant of a name keeps the simple name (<c>Item</c>); a later one takes
    /// the first free name from <see cref="GetNameCandidates"/> — qualified with one more
    /// namespace / containing-type segment at a time (<c>B_Item</c>, <c>OuterOfString_Inner</c>,
    /// <c>ListOfB_Item</c>) — with a numeric suffix as the last resort, so a collision is always
    /// resolved. A name is free when neither it nor <c>{name}TypeInfo</c> is taken by an earlier
    /// type or by a context member.
    /// </summary>
    private static ContextTypeNames AssignContextPropertyNames(IEnumerable<ITypeSymbol> types, HashSet<string> reserved)
    {
        var taken = new HashSet<string>(reserved, StringComparer.Ordinal);
        var names = new Dictionary<ITypeSymbol, string>(SymbolEqualityComparer.Default);

        bool IsFree(string name) => !taken.Contains(name) && !taken.Contains(name + "TypeInfo");

        foreach (var type in types)
        {
            string? chosen = null;
            var last = "";
            foreach (var candidate in GetNameCandidates(type))
            {
                last = candidate;
                if (IsFree(candidate))
                {
                    chosen = candidate;
                    break;
                }
            }

            for (var i = 2; chosen == null; i++)
            {
                var suffixed = last + "_" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (IsFree(suffixed))
                    chosen = suffixed;
            }

            taken.Add(chosen);
            taken.Add(chosen + "TypeInfo");
            names[type] = chosen;
        }

        return new ContextTypeNames(names);
    }

    /// <summary>
    /// Names a generated context property must not take: members the generator always emits
    /// (Default, GetTypeInfo, the nullable/throw helpers), the context's own name and type
    /// parameters, and every member the context declares or inherits (members the user wrote in
    /// another partial declaration, <c>object.Equals</c>, ...) — nested types included, which
    /// <see cref="INamespaceOrTypeSymbol.GetTypeMembers()"/> lists but <c>MemberNames</c> does not.
    /// Since <see cref="AssignContextPropertyNames"/> also checks <c>{name}TypeInfo</c> against this
    /// set, a user member named <c>HelperTypeInfo</c> keeps the generated class off that name too.
    /// All names are unescaped (<c>event</c>, not <c>@event</c>), as Roslyn reports them.
    /// </summary>
    private static HashSet<string> GetReservedMemberNames(INamedTypeSymbol contextType)
    {
        var reserved = new HashSet<string>(StringComparer.Ordinal)
        {
            "Default",
            "GetTypeInfo",
            "ReadNullValue",
            "ReadNullEnum",
            UnsupportedWriteHelperName,
            UnsupportedReadHelperName,
            contextType.Name,
        };

        foreach (var helperType in NullableHelperTypeNames)
            reserved.Add("ReadNullable" + helperType);

        foreach (var typeParameter in contextType.TypeParameters)
            reserved.Add(typeParameter.Name);

        for (var current = contextType; current != null; current = current.BaseType)
        {
            foreach (var memberName in current.MemberNames)
                reserved.Add(memberName);
            foreach (var nestedType in current.GetTypeMembers())
                reserved.Add(nestedType.Name);
        }

        return reserved;
    }

    /// <summary>
    /// Candidate context property names for <paramref name="type"/>, most readable first: the
    /// simple name (<see cref="GetPropertyNameFromType"/>), then the name with every user type in
    /// it qualified by 1, 2, ... of its nearest containing types / namespaces, then every
    /// non-built-in component fully qualified (framework types included).
    /// </summary>
    private static IEnumerable<string> GetNameCandidates(ITypeSymbol type)
    {
        string? previous = null;
        for (var level = 0; ; level++)
        {
            var candidate = BuildContextPropertyName(type, level, qualifyFramework: false);
            // No component has a level-th container, so deeper levels can't differ either.
            if (candidate == previous)
                break;
            yield return candidate;
            previous = candidate;
        }

        var full = BuildContextPropertyName(type, int.MaxValue, qualifyFramework: true);
        if (full != previous)
            yield return full;
    }

    /// <summary><c>typeof(Wrapper&lt;&gt;)</c>, or a type nested in one (<c>typeof(Outer&lt;&gt;.Inner)</c>).</summary>
    private static bool IsOpenGenericType(ITypeSymbol type)
    {
        for (var current = type as INamedTypeSymbol; current != null; current = current.ContainingType)
        {
            if (current.IsUnboundGenericType)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Strips a top-level nullable reference annotation so <c>Foo?</c> and <c>Foo</c> are one
    /// entry, and maps tuples to their underlying type so tuple element names don't split them.
    /// (<see cref="SymbolEqualityComparer.Default"/> already ignores nested nullable annotations,
    /// so <c>List&lt;string?&gt;</c> and <c>List&lt;string&gt;</c> also share one entry.)
    /// </summary>
    internal static ITypeSymbol NormalizeForDiscovery(ITypeSymbol type)
    {
        type = StripNullableAnnotation(type);
        if (type is INamedTypeSymbol { IsTupleType: true, TupleUnderlyingType: { } underlying })
            return underlying;
        return type;
    }
}
