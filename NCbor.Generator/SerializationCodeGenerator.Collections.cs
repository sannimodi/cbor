namespace NCbor.Generator;

/// <summary>How a collection type's elements are laid out on the wire and in the deserialized result.</summary>
internal enum CollectionKind
{
    /// <summary>CBOR array, deserialized into <c>List&lt;T&gt;</c>.</summary>
    List,

    /// <summary>CBOR array, deserialized into <c>HashSet&lt;T&gt;</c>.</summary>
    Set,

    /// <summary>CBOR map, deserialized into <c>Dictionary&lt;K, V&gt;</c>.</summary>
    Dictionary,
}

/// <summary>Result of <see cref="SerializationCodeGenerator.TryGetCollectionInfo"/>.</summary>
internal sealed class CollectionInfo
{
    public CollectionInfo(CollectionKind kind, ITypeSymbol? elementType, ITypeSymbol? keyType, ITypeSymbol? valueType, string concreteTypeName, bool hasCount)
    {
        Kind = kind;
        ElementType = elementType;
        KeyType = keyType;
        ValueType = valueType;
        ConcreteTypeName = concreteTypeName;
        HasCount = hasCount;
    }

    public CollectionKind Kind { get; }

    /// <summary>Element type for <see cref="CollectionKind.List"/>/<see cref="CollectionKind.Set"/>; null for dictionaries.</summary>
    public ITypeSymbol? ElementType { get; }

    public ITypeSymbol? KeyType { get; }
    public ITypeSymbol? ValueType { get; }

    /// <summary>The concrete type constructed on deserialize (e.g. <c>System.Collections.Generic.List&lt;int&gt;</c>).</summary>
    public string ConcreteTypeName { get; }

    /// <summary>
    /// False only for <c>IEnumerable&lt;T&gt;</c>, which has no <c>Count</c>: it is written as an
    /// indefinite-length CBOR array instead of being enumerated twice.
    /// </summary>
    public bool HasCount { get; }
}

/// <summary>Classification of a type for code generation and discovery. See <see cref="SerializationCodeGenerator.ClassifyType"/>.</summary>
internal enum TypeShape
{
    /// <summary>Written directly via CborWriter/CborReader (primitives, string, Guid, DateTime, DateTimeOffset, decimal, enums).</summary>
    BuiltIn,

    /// <summary><c>Nullable&lt;T&gt;</c> of a <see cref="BuiltIn"/> type.</summary>
    NullableBuiltIn,

    /// <summary><c>byte[]</c>, written as a CBOR byte string.</summary>
    ByteArray,

    /// <summary>Single-dimensional array other than <c>byte[]</c>.</summary>
    Array,

    /// <summary>A collection type from the supported table (see <see cref="SerializationCodeGenerator.TryGetCollectionInfo"/>).</summary>
    Collection,

    /// <summary>A class or struct serialized as a CBOR map of its properties.</summary>
    PlainObject,

    /// <summary>Not supported: reported as CBOR009, with a throwing stub TypeInfo when the type can be a generic argument.</summary>
    Unsupported,

    /// <summary>An error type (the compiler already reports it): ignored.</summary>
    Ignored,
}

internal static partial class SerializationCodeGenerator
{
    private const string CollectionsGenericNamespace = "System.Collections.Generic";

    /// <summary>
    /// Recognizes the supported collection types by their generic definition and namespace (not
    /// by bare name, so a user type named <c>List&lt;T&gt;</c> in another namespace is not
    /// mistaken for one):
    /// <list type="bullet">
    /// <item>List, IList, ICollection, IReadOnlyList, IReadOnlyCollection, IEnumerable → List&lt;T&gt;</item>
    /// <item>HashSet, ISet, IReadOnlySet → HashSet&lt;T&gt;</item>
    /// <item>Dictionary, IDictionary, IReadOnlyDictionary → Dictionary&lt;K, V&gt;</item>
    /// </list>
    /// </summary>
    internal static bool TryGetCollectionInfo(ITypeSymbol typeSymbol, out CollectionInfo info)
    {
        info = null!;
        if (typeSymbol is not INamedTypeSymbol { IsGenericType: true } namedType)
            return false;

        var definition = namedType.OriginalDefinition;
        if (definition.ContainingNamespace?.ToDisplayString() != CollectionsGenericNamespace)
            return false;

        var args = namedType.TypeArguments;
        switch (definition.MetadataName)
        {
            case "List`1":
            case "IList`1":
            case "ICollection`1":
            case "IReadOnlyList`1":
            case "IReadOnlyCollection`1":
                info = new CollectionInfo(CollectionKind.List, args[0], null, null, $"System.Collections.Generic.List<{args[0].ToDisplayString()}>", hasCount: true);
                return true;
            case "IEnumerable`1":
                info = new CollectionInfo(CollectionKind.List, args[0], null, null, $"System.Collections.Generic.List<{args[0].ToDisplayString()}>", hasCount: false);
                return true;
            case "HashSet`1":
            case "ISet`1":
            case "IReadOnlySet`1":
                info = new CollectionInfo(CollectionKind.Set, args[0], null, null, $"System.Collections.Generic.HashSet<{args[0].ToDisplayString()}>", hasCount: true);
                return true;
            case "Dictionary`2":
            case "IDictionary`2":
            case "IReadOnlyDictionary`2":
                info = new CollectionInfo(CollectionKind.Dictionary, null, args[0], args[1], $"System.Collections.Generic.Dictionary<{args[0].ToDisplayString()}, {args[1].ToDisplayString()}>", hasCount: true);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Single source of truth for "what kind of type is this" — discovery, element dispatch and
    /// property dispatch all go through it, so they agree on which types get a context property.
    /// Precedence: built-in, nullable built-in, byte[], array, collection table, unsupported,
    /// plain object. Nullable reference annotations are ignored.
    /// </summary>
    internal static TypeShape ClassifyType(ITypeSymbol typeSymbol, out string? unsupportedReason)
    {
        unsupportedReason = null;
        var type = StripNullableAnnotation(typeSymbol);

        if (type.TypeKind == TypeKind.Error)
            return TypeShape.Ignored;

        // dynamic anywhere in the type (List<dynamic>, dynamic[], Foo<dynamic>): the TypeInfo
        // would need typeof(...) over it, which is CS1962.
        if (type.TypeKind != TypeKind.Dynamic && ContainsDynamic(type))
        {
            unsupportedReason = "dynamic has no fixed shape to generate code for, including as a type argument or array element";
            return TypeShape.Unsupported;
        }

        if (IsBuiltInType(type))
            return TypeShape.BuiltIn;

        if (TryGetNullableUnderlyingType(type, out var underlying))
        {
            if (IsBuiltInType(underlying!))
                return TypeShape.NullableBuiltIn;
            unsupportedReason = "Nullable<T> is only supported when T is a built-in type (primitive, string, Guid, DateTime, DateTimeOffset, decimal or enum)";
            return TypeShape.Unsupported;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            if (arrayType.Rank != 1)
            {
                unsupportedReason = "multi-dimensional arrays are not supported (use a jagged array or a List<List<T>>)";
                return TypeShape.Unsupported;
            }
            return IsByteArray(arrayType) ? TypeShape.ByteArray : TypeShape.Array;
        }

        if (TryGetCollectionInfo(type, out _))
            return TypeShape.Collection;

        unsupportedReason = GetUnsupportedReason(type);
        return unsupportedReason != null ? TypeShape.Unsupported : TypeShape.PlainObject;
    }

    private static string? GetUnsupportedReason(ITypeSymbol type)
    {
        switch (type.TypeKind)
        {
            case TypeKind.Dynamic:
                return "dynamic has no fixed shape to generate code for";
            case TypeKind.Delegate:
                return "delegates cannot be serialized";
            case TypeKind.Pointer:
            case TypeKind.FunctionPointer:
                return "pointer types cannot be serialized";
            case TypeKind.TypeParameter:
                return "open generic type parameters cannot be serialized";
            case TypeKind.Interface:
                return "interfaces other than the supported collection interfaces (IList<T>, IReadOnlyList<T>, ISet<T>, IDictionary<K,V>, ...) have no concrete type to deserialize into";
        }

        if (type.SpecialType == SpecialType.System_Object)
            return "object has no fixed shape to generate code for";

        if (type.IsTupleType || IsSystemTuple(type))
            return "tuples are not supported (use a class, struct or record with named properties)";

        if (type.IsRefLikeType)
            return "ref struct types cannot be serialized";

        if (type.IsAbstract && type.TypeKind == TypeKind.Class)
            return "abstract classes cannot be instantiated on deserialize (polymorphism is not supported)";

        // A framework collection outside the table (Queue, SortedSet, Stack, ConcurrentX, ...) is
        // unsupported. A user type that happens to implement IEnumerable (`Bag : IEnumerable<int>`,
        // `MyList : List<int>`) stays a plain object serialized by its properties, as before
        // collections were recognized by interface; CBOR013 warns that its elements are dropped.
        if (IsEnumerable(type) && IsFrameworkMetadataType(type))
            return "only List/IList/ICollection/IReadOnlyList/IReadOnlyCollection/IEnumerable, HashSet/ISet/IReadOnlySet and Dictionary/IDictionary/IReadOnlyDictionary collections are supported";

        if (type.DeclaringSyntaxReferences.IsEmpty && IsInSystemNamespace(type))
            return "this framework type is not one of NCbor's built-in types (primitives, string, Guid, DateTime, DateTimeOffset, decimal, enums, byte[])";

        return null;
    }

    private static bool IsEnumerable(ITypeSymbol type) =>
        type.AllInterfaces.Any(i => i.SpecialType == SpecialType.System_Collections_IEnumerable);

    /// <summary>
    /// CBOR013: a plain object (user type, so not in the collection table and not a framework
    /// collection) that implements <c>IEnumerable</c>. It is serialized by its properties; its
    /// elements are not.
    /// </summary>
    internal static bool IsEnumerablePlainObject(ITypeSymbol type) =>
        ClassifyType(type, out _) == TypeShape.PlainObject && IsEnumerable(StripNullableAnnotation(type));

    /// <summary>
    /// True for a framework type: from metadata, with root namespace <c>System</c> or
    /// <c>Microsoft</c> (the same rule as the base-chain walk, see <see cref="IsFrameworkType"/>).
    /// A type declared in source, or in a non-framework referenced assembly, is a user type.
    /// </summary>
    internal static bool IsFrameworkMetadataType(ITypeSymbol type) =>
        type is INamedTypeSymbol named && IsFrameworkType(named);

    private static bool ContainsDynamic(ITypeSymbol type) => type switch
    {
        { TypeKind: TypeKind.Dynamic } => true,
        IArrayTypeSymbol array => ContainsDynamic(array.ElementType),
        INamedTypeSymbol named => named.TypeArguments.Any(ContainsDynamic) || (named.ContainingType != null && ContainsDynamic(named.ContainingType)),
        _ => false,
    };

    private static bool IsSystemTuple(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { IsGenericType: true } named &&
               named.OriginalDefinition.MetadataName.StartsWith("Tuple`", StringComparison.Ordinal) &&
               named.ContainingNamespace?.ToDisplayString() == "System";
    }

    private static bool IsInSystemNamespace(ITypeSymbol type)
    {
        var ns = type.ContainingNamespace;
        if (ns == null || ns.IsGlobalNamespace)
            return false;
        while (ns.ContainingNamespace != null && !ns.ContainingNamespace.IsGlobalNamespace)
            ns = ns.ContainingNamespace;
        return ns.Name == "System";
    }

    /// <summary>
    /// True when an unsupported type can still be used as a generic argument, so a throwing stub
    /// <c>NCborTypeInfo&lt;T&gt;</c> can be emitted for it — that keeps the user's build output to the
    /// CBOR009 error instead of an additional trailing CS1061 at every reference site.
    /// </summary>
    internal static bool CanEmitStub(ITypeSymbol type)
    {
        type = StripNullableAnnotation(type);
        return type.TypeKind is not (TypeKind.Error or TypeKind.Dynamic or TypeKind.Pointer or TypeKind.FunctionPointer or TypeKind.TypeParameter)
               && !type.IsRefLikeType
               && !ContainsDynamic(type)
               && !(type is IArrayTypeSymbol array && !CanEmitStub(array.ElementType));
    }

    internal static bool IsUnsupportedStubbedType(ITypeSymbol type) =>
        ClassifyType(type, out _) == TypeShape.Unsupported && CanEmitStub(type);

    /// <summary>
    /// Unsupported (CBOR009) and without a stub TypeInfo (<c>dynamic</c>, <c>List&lt;dynamic&gt;</c>,
    /// ...): reference sites call the context's throwing helpers instead (see
    /// <see cref="GenerateUnsupportedWrite"/>), so CBOR009 stays the only build error.
    /// </summary>
    internal static bool IsUnsupportedUnstubbableType(ITypeSymbol type) =>
        ClassifyType(type, out _) == TypeShape.Unsupported && !CanEmitStub(type);

    /// <summary>
    /// Statement that throws <c>NCborSerializationException</c> for an un-stubbable unsupported
    /// value. A helper call rather than an inline <c>throw</c>, so the statements after it aren't
    /// flagged as unreachable (CS0162).
    /// </summary>
    internal static string GenerateUnsupportedWrite(ITypeSymbol type) =>
        $"{UnsupportedWriteHelperName}({ToStringLiteral(type.ToDisplayString())});";

    /// <summary>Expression of type <paramref name="type"/> that throws <c>NCborDeserializationException</c>.</summary>
    internal static string GenerateUnsupportedRead(ITypeSymbol type) =>
        $"{UnsupportedReadHelperName}<{type.ToDisplayString()}>({ToStringLiteral(type.ToDisplayString())})";

    internal static ITypeSymbol StripNullableAnnotation(ITypeSymbol type) =>
        type.IsReferenceType && type.NullableAnnotation == NullableAnnotation.Annotated
            ? type.WithNullableAnnotation(NullableAnnotation.NotAnnotated)
            : type;

    internal static bool TryGetNullableUnderlyingType(ITypeSymbol type, out ITypeSymbol? underlying)
    {
        if (type is INamedTypeSymbol { IsGenericType: true } named &&
            named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            underlying = named.TypeArguments[0];
            return true;
        }
        underlying = null;
        return false;
    }

    internal static bool IsNullableBuiltInType(ITypeSymbol typeSymbol) =>
        TryGetNullableUnderlyingType(typeSymbol, out var underlying) && IsBuiltInType(underlying!);

    /// <summary>
    /// Emits a statement writing one collection element / dictionary key / dictionary value held
    /// in <paramref name="expression"/>. Built-ins are written directly; <c>Nullable&lt;built-in&gt;</c>
    /// writes the value or CBOR null; everything else dispatches to the context property for its
    /// type. Reference-type elements write CBOR null for null <i>regardless of their nullable
    /// annotation</i>: <c>List&lt;Foo&gt;</c> and <c>List&lt;Foo?&gt;</c> are the same symbol under
    /// <see cref="SymbolEqualityComparer.Default"/> and therefore share one generated TypeInfo, so
    /// the shared code must handle nulls for whichever declaration reaches it — except for a
    /// dictionary key (<paramref name="isDictionaryKey"/>), which is never null.
    /// </summary>
    internal static string GenerateElementWrite(string expression, ITypeSymbol elementType, string contextRef, ContextTypeNames names, bool isDictionaryKey = false)
    {
        if (IsUnsupportedUnstubbableType(elementType))
        {
            return GenerateUnsupportedWrite(elementType);
        }

        if (TryGetNullableUnderlyingType(elementType, out var underlying) && IsBuiltInType(underlying!))
        {
            return $"if ({expression}.HasValue) {{ {GenerateDirectSerialization($"{expression}.Value", underlying!)} }} else {{ writer.WriteNull(); }}";
        }

        var core = IsBuiltInType(elementType)
            ? GenerateDirectSerialization(expression, elementType)
            : $"{contextRef}.{names.Get(elementType)}.Serialize(writer, {expression}, depth + 1);";

        if (elementType.IsReferenceType && !isDictionaryKey)
        {
            return $"if ({expression} is null) {{ writer.WriteNull(); }} else {{ {core} }}";
        }

        return core;
    }

    /// <summary>
    /// Emits an expression reading one collection element / dictionary key / dictionary value.
    /// Mirrors <see cref="GenerateElementWrite"/>.
    /// </summary>
    internal static string GenerateElementRead(ITypeSymbol elementType, string contextRef, ContextTypeNames names)
    {
        if (IsUnsupportedUnstubbableType(elementType))
        {
            return GenerateUnsupportedRead(elementType);
        }

        if (TryGetNullableUnderlyingType(elementType, out var underlying) && IsBuiltInType(underlying!))
        {
            if (underlying!.TypeKind == TypeKind.Enum)
            {
                return $"(reader.PeekState() == CborReaderState.Null ? ReadNullEnum<{underlying.ToDisplayString()}>(reader) : {GenerateDirectDeserialization(underlying)})";
            }
            return $"ReadNullable{underlying.Name}(reader)";
        }

        var core = IsBuiltInType(elementType)
            ? GenerateDirectDeserialization(elementType)
            : $"{contextRef}.{names.Get(elementType)}.Deserialize(reader, depth + 1)";

        if (elementType.IsReferenceType)
        {
            // `!`: a null on the wire was written from a null element, so it round-trips even when
            // this shared TypeInfo was generated from the non-annotated declaration (see above).
            var nonNullable = elementType.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString();
            return $"(reader.PeekState() == CborReaderState.Null ? ReadNullValue<{nonNullable}>(reader)! : {core})";
        }

        return core;
    }

    /// <summary>
    /// The context property's access modifier for <paramref name="type"/>: <c>public</c> when the
    /// type (and every containing type and type argument) is public, <c>private</c> when any of
    /// them is private (only reachable when nested in the context itself), else <c>internal</c> —
    /// a <c>public</c> property of a less accessible type is CS0053.
    /// </summary>
    internal static string GetAccessibilityModifier(ITypeSymbol type)
    {
        var worst = Accessibility.Public;
        Visit(type);
        return worst switch
        {
            Accessibility.Public => "public",
            Accessibility.Private => "private",
            _ => "internal",
        };

        void Visit(ITypeSymbol t)
        {
            switch (t)
            {
                case IArrayTypeSymbol array:
                    Visit(array.ElementType);
                    return;
                case INamedTypeSymbol named:
                    for (var current = named; current != null; current = current.ContainingType)
                    {
                        var accessibility = current.DeclaredAccessibility;
                        if (accessibility == Accessibility.Private)
                            worst = Accessibility.Private;
                        else if (accessibility != Accessibility.Public && accessibility != Accessibility.NotApplicable && worst == Accessibility.Public)
                            worst = Accessibility.Internal;
                    }
                    foreach (var arg in named.TypeArguments)
                        Visit(arg);
                    return;
            }
        }
    }
}
