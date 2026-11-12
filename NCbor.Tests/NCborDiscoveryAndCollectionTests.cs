namespace NCbor.Tests;

// Models for transitive type discovery and the collection-interface table. Only the three roots
// (plus a few built-ins) are registered on DiscoveryContext; every nested collection / POCO type
// below is discovered by the generator.

public class DiscoveryChild
{
    public string Name { get; set; } = string.Empty;
    public int Score { get; set; }
}

public class DiscoveryRoot
{
    public List<List<int>> Grid { get; set; } = [];
    public Dictionary<int, string> Names { get; set; } = [];
    public DiscoveryChild Child { get; set; } = new();
    public List<DiscoveryChild> Children { get; set; } = [];
    public Dictionary<string, List<DiscoveryChild>> ChildrenByTeam { get; set; } = [];
    public List<int?> MaybeInts { get; set; } = [];
    public List<string?> MaybeStrings { get; set; } = [];
    public Dictionary<string, UserRole?> MaybeRoles { get; set; } = [];
    public int?[] MaybeIntArray { get; set; } = [];
    public List<byte[]> Blobs { get; set; } = [];
    public List<DiscoveryChild?> MaybeChildren { get; set; } = [];
}

public class InterfaceCollectionsModel
{
    public IList<int>? AList { get; set; }
    public ICollection<string>? ACollection { get; set; }
    public IReadOnlyList<int>? ReadOnlyList { get; set; }
    public IReadOnlyCollection<string>? ReadOnlyCollection { get; set; }
    public IEnumerable<int>? Sequence { get; set; }
    public HashSet<string>? Set { get; set; }
    public ISet<int>? ISet { get; set; }
    public IReadOnlySet<string>? ReadOnlySet { get; set; }
    public IDictionary<string, int>? Dict { get; set; }
    public IReadOnlyDictionary<string, DiscoveryChild>? ReadOnlyDict { get; set; }
}

public class DiscoveryNode
{
    public int Value { get; set; }
    public DiscoveryNode? Next { get; set; }
    public List<DiscoveryNode> Kids { get; set; } = [];
}

[NCborSerializable(typeof(DiscoveryRoot))]
[NCborSerializable(typeof(InterfaceCollectionsModel))]
[NCborSerializable(typeof(DiscoveryNode))]
[NCborSerializable(typeof(int))]
[NCborSerializable(typeof(string))]
[NCborSerializable(typeof(int?))]
public partial class DiscoveryContext : NCborSerializerContext
{
}

public class NCborDiscoveryAndCollectionTests
{
    private readonly DiscoveryContext _context = DiscoveryContext.Default;

    // ---------- Discovery ----------

    [Fact]
    public void UnregisteredNestedTypes_RoundTrip()
    {
        var original = new DiscoveryRoot
        {
            Grid = [[1, 2, 3], [], [4]],
            Names = new() { [1] = "one", [-2] = "minus two" },
            Child = new DiscoveryChild { Name = "solo", Score = 7 },
            Children = [new DiscoveryChild { Name = "a", Score = 1 }, new DiscoveryChild { Name = "b", Score = 2 }],
            ChildrenByTeam = new() { ["red"] = [new DiscoveryChild { Name = "r1", Score = 10 }], ["blue"] = [] },
            Blobs = [[1, 2, 3], []],
        };

        var bytes = NCborSerializer.Serialize(original, _context.DiscoveryRoot);
        var result = NCborSerializer.Deserialize(bytes, _context.DiscoveryRoot);

        result.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void DiscoveredTypes_ArePublicContextProperties_AndInGetTypeInfo()
    {
        // These were never registered: they exist only because discovery found them.
        _context.ListOfListOfInt32.Should().NotBeNull();
        _context.DictionaryOfInt32AndString.Should().NotBeNull();
        _context.DiscoveryChild.Should().NotBeNull();
        _context.GetTypeInfo<DiscoveryChild>().Should().BeSameAs(_context.DiscoveryChild);
        _context.GetTypeInfo<List<List<int>>>().Should().BeSameAs(_context.ListOfListOfInt32);
        _context.GetTypeInfo<IReadOnlyList<int>>().Should().BeSameAs(_context.IReadOnlyListOfInt32);

        var grid = new List<List<int>> { new() { 1 }, new() { 2, 3 } };
        var bytes = NCborSerializer.Serialize(grid, _context.ListOfListOfInt32);
        NCborSerializer.Deserialize(bytes, _context.ListOfListOfInt32).Should().BeEquivalentTo(grid);
    }

    [Fact]
    public void NonDiscoveredType_StillThrowsFromGetTypeInfo()
    {
        var act = () => _context.GetTypeInfo<Dictionary<int, int>>();
        act.Should().Throw<ArgumentException>().WithMessage("*not registered for serialization*");
    }

    [Fact]
    public void SelfReferentialNode_RoundTrips()
    {
        var original = new DiscoveryNode
        {
            Value = 1,
            Next = new DiscoveryNode { Value = 2, Next = new DiscoveryNode { Value = 3 } },
            Kids = [new DiscoveryNode { Value = 10, Kids = [new DiscoveryNode { Value = 100 }] }, new DiscoveryNode { Value = 11 }],
        };

        var bytes = NCborSerializer.Serialize(original, _context.DiscoveryNode);
        var result = NCborSerializer.Deserialize(bytes, _context.DiscoveryNode);

        result.Should().BeEquivalentTo(original);
        result.Next!.Next!.Next.Should().BeNull();
    }

    // ---------- Nullable elements ----------

    [Fact]
    public void NullableElements_RoundTripNullsAndValues()
    {
        // MaybeChildren (List<DiscoveryChild?>) shares the ListOfDiscoveryChild TypeInfo with
        // Children (List<DiscoveryChild>), which is discovered first — the shared element code
        // must still handle null elements.
        var original = new DiscoveryRoot
        {
            MaybeInts = [1, null, -3],
            MaybeStrings = ["x", null, ""],
            MaybeRoles = new() { ["admin"] = UserRole.Admin, ["none"] = null },
            MaybeIntArray = [null, 42],
            MaybeChildren = [null, new DiscoveryChild { Name = "kid", Score = 3 }],
        };

        var bytes = NCborSerializer.Serialize(original, _context.DiscoveryRoot);
        var result = NCborSerializer.Deserialize(bytes, _context.DiscoveryRoot);

        result.MaybeInts.Should().Equal(1, null, -3);
        result.MaybeStrings.Should().Equal("x", null, "");
        result.MaybeRoles.Should().BeEquivalentTo(original.MaybeRoles);
        result.MaybeIntArray.Should().Equal(null, 42);
        result.MaybeChildren[0].Should().BeNull();
        result.MaybeChildren[1].Should().BeEquivalentTo(original.MaybeChildren[1]);
    }

    [Fact]
    public void ListOfNullableInt_WritesCborNullForNullElements()
    {
        var bytes = NCborSerializer.Serialize(new List<int?> { null, 5 }, _context.ListOfNullableOfInt32);

        var reader = new CborReader(bytes);
        reader.ReadStartArray().Should().Be(2);
        reader.PeekState().Should().Be(CborReaderState.Null);
        reader.ReadNull();
        reader.ReadInt32().Should().Be(5);
    }

    // ---------- Collection interfaces ----------

    private static InterfaceCollectionsModel PopulatedInterfaces() => new()
    {
        AList = new List<int> { 1, 2 },
        ACollection = new List<string> { "c1", "c2" },
        ReadOnlyList = new List<int> { 3, 1, 4 },
        ReadOnlyCollection = new[] { "r1" },
        Sequence = Enumerable.Range(1, 3).Select(i => i * 10),
        Set = new HashSet<string> { "s1", "s2" },
        ISet = new HashSet<int> { 7, 8, 9 },
        ReadOnlySet = new HashSet<string> { "ro" },
        Dict = new Dictionary<string, int> { ["k"] = 1 },
        ReadOnlyDict = new Dictionary<string, DiscoveryChild> { ["c"] = new DiscoveryChild { Name = "child", Score = 5 } },
    };

    [Fact]
    public void CollectionInterfaces_Populated_RoundTrip()
    {
        var original = PopulatedInterfaces();

        var bytes = NCborSerializer.Serialize(original, _context.InterfaceCollectionsModel);
        var result = NCborSerializer.Deserialize(bytes, _context.InterfaceCollectionsModel);

        result.AList.Should().Equal(1, 2);
        result.ACollection.Should().Equal("c1", "c2");
        result.ReadOnlyList.Should().Equal(3, 1, 4);
        result.ReadOnlyCollection.Should().Equal("r1");
        result.Sequence.Should().Equal(10, 20, 30);
        result.Set.Should().BeEquivalentTo(new[] { "s1", "s2" });
        result.ISet.Should().BeEquivalentTo(new[] { 7, 8, 9 });
        result.ReadOnlySet.Should().BeEquivalentTo(new[] { "ro" });
        result.Dict.Should().BeEquivalentTo(original.Dict);
        result.ReadOnlyDict.Should().BeEquivalentTo(original.ReadOnlyDict);
    }

    [Fact]
    public void CollectionInterfaces_DeserializeIntoConcreteListHashSetDictionary()
    {
        var bytes = NCborSerializer.Serialize(PopulatedInterfaces(), _context.InterfaceCollectionsModel);
        var result = NCborSerializer.Deserialize(bytes, _context.InterfaceCollectionsModel);

        result.AList.Should().BeOfType<List<int>>();
        result.ACollection.Should().BeOfType<List<string>>();
        result.ReadOnlyList.Should().BeOfType<List<int>>();
        result.ReadOnlyCollection.Should().BeOfType<List<string>>();
        result.Sequence.Should().BeOfType<List<int>>();
        result.Set.Should().BeOfType<HashSet<string>>();
        result.ISet.Should().BeOfType<HashSet<int>>();
        result.ReadOnlySet.Should().BeOfType<HashSet<string>>();
        result.Dict.Should().BeOfType<Dictionary<string, int>>();
        result.ReadOnlyDict.Should().BeOfType<Dictionary<string, DiscoveryChild>>();
    }

    [Fact]
    public void CollectionInterfaces_Null_RoundTrip()
    {
        var bytes = NCborSerializer.Serialize(new InterfaceCollectionsModel(), _context.InterfaceCollectionsModel);
        var result = NCborSerializer.Deserialize(bytes, _context.InterfaceCollectionsModel);

        result.Should().BeEquivalentTo(new InterfaceCollectionsModel());
        result.AList.Should().BeNull();
        result.Sequence.Should().BeNull();
        result.ReadOnlySet.Should().BeNull();
        result.ReadOnlyDict.Should().BeNull();
    }

    [Fact]
    public void CollectionInterfaces_Empty_RoundTrip()
    {
        var original = new InterfaceCollectionsModel
        {
            AList = new List<int>(),
            ACollection = new List<string>(),
            ReadOnlyList = Array.Empty<int>(),
            ReadOnlyCollection = Array.Empty<string>(),
            Sequence = Enumerable.Empty<int>(),
            Set = new HashSet<string>(),
            ISet = new HashSet<int>(),
            ReadOnlySet = new HashSet<string>(),
            Dict = new Dictionary<string, int>(),
            ReadOnlyDict = new Dictionary<string, DiscoveryChild>(),
        };

        var bytes = NCborSerializer.Serialize(original, _context.InterfaceCollectionsModel);
        var result = NCborSerializer.Deserialize(bytes, _context.InterfaceCollectionsModel);

        result.AList.Should().NotBeNull().And.BeEmpty();
        result.ACollection.Should().NotBeNull().And.BeEmpty();
        result.ReadOnlyList.Should().NotBeNull().And.BeEmpty();
        result.ReadOnlyCollection.Should().NotBeNull().And.BeEmpty();
        result.Sequence.Should().NotBeNull().And.BeEmpty();
        result.Set.Should().NotBeNull().And.BeEmpty();
        result.ISet.Should().NotBeNull().And.BeEmpty();
        result.ReadOnlySet.Should().NotBeNull().And.BeEmpty();
        result.Dict.Should().NotBeNull().And.BeEmpty();
        result.ReadOnlyDict.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void IReadOnlyList_HasTheSameWireFormatAsList()
    {
        var values = new List<int> { 5, 6, 7 };

        var fromInterface = NCborSerializer.Serialize<IReadOnlyList<int>>(values, _context.IReadOnlyListOfInt32);
        var fromList = NCborSerializer.Serialize(values, _context.ListOfInt32);

        fromInterface.Should().Equal(fromList);
    }

    [Fact]
    public void IEnumerable_IsWrittenAsIndefiniteLengthArray_AndReadsBack()
    {
        var bytes = NCborSerializer.Serialize<IEnumerable<int>>(Enumerable.Range(1, 4), _context.IEnumerableOfInt32);

        bytes[0].Should().Be(0x9F, "IEnumerable<T> has no Count, so it is written as an indefinite-length CBOR array");
        NCborSerializer.Deserialize(bytes, _context.IEnumerableOfInt32).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void HashSet_IsWrittenAsDefiniteLengthArray_AndCollapsesDuplicatesOnRead()
    {
        var bytes = NCborSerializer.Serialize(new HashSet<string> { "a", "b" }, _context.HashSetOfString);
        new CborReader(bytes).ReadStartArray().Should().Be(2);

        // A foreign payload with duplicate elements still deserializes into a valid set.
        var writer = new CborWriter();
        writer.WriteStartArray(3);
        writer.WriteTextString("x");
        writer.WriteTextString("x");
        writer.WriteTextString("y");
        writer.WriteEndArray();
        NCborSerializer.Deserialize(writer.Encode(), _context.HashSetOfString).Should().BeEquivalentTo(new[] { "x", "y" });
    }

    // ---------- Built-in registrations ----------

    [Fact]
    public void RegisteredBuiltIns_RoundTrip()
    {
        NCborSerializer.Deserialize(NCborSerializer.Serialize(42, _context.Int32), _context.Int32).Should().Be(42);
        NCborSerializer.Deserialize(NCborSerializer.Serialize("hi", _context.String), _context.String).Should().Be("hi");
        NCborSerializer.Deserialize(NCborSerializer.Serialize<int?>(7, _context.NullableOfInt32), _context.NullableOfInt32).Should().Be(7);
        NCborSerializer.Deserialize(NCborSerializer.Serialize<int?>(null, _context.NullableOfInt32), _context.NullableOfInt32).Should().BeNull();
        _context.GetTypeInfo<int>().Should().BeSameAs(_context.Int32);
        _context.GetTypeInfo<int?>().Should().BeSameAs(_context.NullableOfInt32);

        // Same encoding as an int property: a plain CBOR integer.
        new CborReader(NCborSerializer.Serialize(42, _context.Int32)).ReadInt32().Should().Be(42);
    }
}
