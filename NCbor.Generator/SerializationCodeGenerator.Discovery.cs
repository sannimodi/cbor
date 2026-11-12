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

/// <summary>Two distinct types that map to the same context property name (CBOR011).</summary>
internal sealed class PropertyNameCollision
{
    public PropertyNameCollision(ITypeSymbol type, string conflictsWith, string propertyName, TypeOrigin origin)
    {
        Type = type;
        ConflictsWith = conflictsWith;
        PropertyName = propertyName;
        Origin = origin;
    }

    public ITypeSymbol Type { get; }

    /// <summary>Display name of the type (or context member) that already owns the name.</summary>
    public string ConflictsWith { get; }

    public string PropertyName { get; }
    public TypeOrigin Origin { get; }
}

/// <summary>Output of <see cref="SerializationCodeGenerator.DiscoverTypeClosure"/>.</summary>
internal sealed class TypeClosure
{
    public TypeClosure(List<ITypeSymbol> types, List<ITypeSymbol> stubTypes, List<UnsupportedTypeUsage> unsupported, List<PropertyNameCollision> collisions)
    {
        Types = types;
        StubTypes = stubTypes;
        Unsupported = unsupported;
        Collisions = collisions;
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
    public List<PropertyNameCollision> Collisions { get; }
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
        string contextName)
    {
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
            var type = Normalize(rawType);
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
                        var propertyOrigin = new TypeOrigin(property.Locations.FirstOrDefault(), $"{namedType.Name}.{property.Name}");
                        Consider(property.Type, propertyOrigin, isRoot: false);
                    }
                    break;
            }
        }

        var collisions = new List<PropertyNameCollision>();
        var types = RemoveNameCollisions(ordered, stubs, origins, contextName, collisions, out var keptStubs);
        return new TypeClosure(types, keptStubs, unsupported, collisions);
    }

    /// <summary>
    /// Drops every type whose context property name (or <c>{Name}TypeInfo</c> class name) is
    /// already taken — by an earlier type, or by a member the generator always emits — and
    /// records a CBOR011 collision for it, so the user sees one clear error instead of CS0102.
    /// </summary>
    private static List<ITypeSymbol> RemoveNameCollisions(
        List<ITypeSymbol> ordered,
        List<ITypeSymbol> stubs,
        Dictionary<ITypeSymbol, TypeOrigin> origins,
        string contextName,
        List<PropertyNameCollision> collisions,
        out List<ITypeSymbol> keptStubs)
    {
        // Member names the generated context already declares.
        var owners = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Default"] = $"the generated {contextName}.Default property",
            ["GetTypeInfo"] = $"the generated {contextName}.GetTypeInfo<T>() method",
            [contextName] = $"the context class {contextName} itself",
        };

        bool TryClaim(ITypeSymbol type)
        {
            var name = GetPropertyNameFromType(type);
            var typeInfoName = name + "TypeInfo";
            string? owner = null;
            if (owners.TryGetValue(name, out var o1))
                owner = o1;
            else if (owners.TryGetValue(typeInfoName, out var o2))
                owner = o2;

            if (owner != null)
            {
                collisions.Add(new PropertyNameCollision(type, owner, name, origins[type]));
                return false;
            }

            var display = type.ToDisplayString();
            owners[name] = display;
            owners[typeInfoName] = display;
            return true;
        }

        var kept = ordered.Where(TryClaim).ToList();
        keptStubs = stubs.Where(TryClaim).ToList();
        return kept;
    }

    /// <summary>
    /// Strips a top-level nullable reference annotation so <c>Foo?</c> and <c>Foo</c> are one
    /// entry, and maps tuples to their underlying type so tuple element names don't split them.
    /// (<see cref="SymbolEqualityComparer.Default"/> already ignores nested nullable annotations,
    /// so <c>List&lt;string?&gt;</c> and <c>List&lt;string&gt;</c> also share one entry.)
    /// </summary>
    private static ITypeSymbol Normalize(ITypeSymbol type)
    {
        type = StripNullableAnnotation(type);
        if (type is INamedTypeSymbol { IsTupleType: true, TupleUnderlyingType: { } underlying })
            return underlying;
        return type;
    }
}
