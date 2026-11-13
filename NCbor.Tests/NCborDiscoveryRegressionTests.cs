using System.Collections;
using System.Formats.Cbor;

// Two types with the same simple name in different namespaces. Both are reached from
// RegressionCollisionRoot, so both need a context property: the first claimant keeps `Item`, the
// second is disambiguated to `B_Item` (it used to be dropped with CBOR011 and then referenced
// anyway, a cascade of CS1503/CS0029).
namespace NCbor.Tests.Collide.A
{
    public class Item
    {
        public int Number { get; set; }
    }
}

namespace NCbor.Tests.Collide.B
{
    public class Item
    {
        public string Text { get; set; } = string.Empty;
    }
}

namespace NCbor.Tests
{
    // CBOR013 is the intended warning for these two: they implement IEnumerable but aren't a
    // supported collection, so they serialize as objects (their properties) — as they did before
    // collections were recognized by interface. The pragma keeps the test build warning-free.
#pragma warning disable CBOR013
    public class Bag : IEnumerable<int>
    {
        private readonly List<int> _items = [];

        public int Code { get; set; }

        public void Add(int item) => _items.Add(item);

        public IEnumerator<int> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class MyList : List<int>
    {
        public int Code { get; set; }
    }
#pragma warning restore CBOR013

    public class Outer<T>
    {
        public class Inner
        {
            public T? Value { get; set; }
        }
    }

    public class RegressionCollisionRoot
    {
        public Collide.A.Item First { get; set; } = new();
        public Collide.B.Item Second { get; set; } = new();
        public List<Collide.A.Item> FirstList { get; set; } = [];
        public List<Collide.B.Item> SecondList { get; set; } = [];
        public Outer<int>.Inner IntInner { get; set; } = new();
        public Outer<string>.Inner StringInner { get; set; } = new();
    }

    public class JaggedHolder
    {
        public int[][] Grid { get; set; } = [];
        public byte[][] Blobs { get; set; } = [];
    }

    [NCborSerializable(typeof(Bag))]
    [NCborSerializable(typeof(MyList))]
    [NCborSerializable(typeof(RegressionCollisionRoot))]
    [NCborSerializable(typeof(JaggedHolder))]
    [NCborSerializable(typeof(int[][]))]
    [NCborSerializable(typeof(byte[][]))]
    [NCborSerializable(typeof(Dictionary<string, int>))]
    [NCborSerializable(typeof(Dictionary<int, string>))]
    public partial class DiscoveryRegressionContext : NCborSerializerContext
    {
    }

    /// <summary>
    /// Runtime round-trips for the wave-2 discovery regressions: IEnumerable POCOs, colliding
    /// context property names, jagged arrays and null dictionary keys.
    /// </summary>
    public class NCborDiscoveryRegressionTests
    {
        private static readonly DiscoveryRegressionContext Context = DiscoveryRegressionContext.Default;

        // ---------- IEnumerable POCOs (CBOR013) ----------

        [Fact]
        public void EnumerablePoco_RoundTripsItsProperties()
        {
            var bag = new Bag { Code = 42 };
            bag.Add(1);
            bag.Add(2);

            var bytes = NCborSerializer.Serialize(bag, Context.Bag);
            var result = NCborSerializer.Deserialize(bytes, Context.Bag);

            result.Code.Should().Be(42);
            result.Should().BeEmpty("only properties are serialized, not the elements (CBOR013)");
            new CborReader(bytes).PeekState().Should().Be(CborReaderState.StartMap, "it is written as an object");
        }

        [Fact]
        public void ListSubclass_RoundTripsItsProperties()
        {
            var list = new MyList { Code = 7 };
            list.Add(10);

            var bytes = NCborSerializer.Serialize(list, Context.MyList);
            var result = NCborSerializer.Deserialize(bytes, Context.MyList);

            result.Code.Should().Be(7);
            result.Should().BeEmpty("only properties are serialized, not the elements (CBOR013)");
            ReadMapKeys(bytes).Should().Equal(["Code"], "List<int>'s own Capacity/Count are framework members, not part of the object");
        }

        // ---------- Context property name collisions ----------

        [Fact]
        public void CollidingSimpleNames_BothRoundTrip()
        {
            var root = new RegressionCollisionRoot
            {
                First = new Collide.A.Item { Number = 5 },
                Second = new Collide.B.Item { Text = "five" },
                FirstList = [new Collide.A.Item { Number = 1 }, new Collide.A.Item { Number = 2 }],
                SecondList = [new Collide.B.Item { Text = "one" }],
                IntInner = new Outer<int>.Inner { Value = 99 },
                StringInner = new Outer<string>.Inner { Value = "ninety-nine" },
            };

            var bytes = NCborSerializer.Serialize(root, Context.RegressionCollisionRoot);
            var result = NCborSerializer.Deserialize(bytes, Context.RegressionCollisionRoot);

            result.First.Number.Should().Be(5);
            result.Second.Text.Should().Be("five");
            result.FirstList.Select(i => i.Number).Should().Equal(1, 2);
            result.SecondList.Select(i => i.Text).Should().Equal("one");
            result.IntInner.Value.Should().Be(99);
            result.StringInner.Value.Should().Be("ninety-nine");
        }

        [Fact]
        public void CollidingSimpleNames_GetDisambiguatedContextProperties()
        {
            // First claimant keeps the simple name; later ones are qualified by their nearest
            // namespace / containing type, through type arguments too.
            Context.Item.Should().BeAssignableTo<NCborTypeInfo<Collide.A.Item>>();
            Context.B_Item.Should().BeAssignableTo<NCborTypeInfo<Collide.B.Item>>();
            Context.ListOfItem.Should().BeAssignableTo<NCborTypeInfo<List<Collide.A.Item>>>();
            Context.ListOfB_Item.Should().BeAssignableTo<NCborTypeInfo<List<Collide.B.Item>>>();
            Context.Inner.Should().BeAssignableTo<NCborTypeInfo<Outer<int>.Inner>>();
            Context.OuterOfString_Inner.Should().BeAssignableTo<NCborTypeInfo<Outer<string>.Inner>>();

            var bytes = NCborSerializer.Serialize(new Collide.B.Item { Text = "direct" }, Context.B_Item);
            NCborSerializer.Deserialize(bytes, Context.B_Item).Text.Should().Be("direct");
            Context.GetTypeInfo<Collide.B.Item>().Should().BeSameAs(Context.B_Item);
            Context.GetTypeInfo<Outer<string>.Inner>().Should().BeSameAs(Context.OuterOfString_Inner);
        }

        // ---------- Jagged arrays ----------

        [Fact]
        public void JaggedIntArray_RoundTrips()
        {
            int[][] grid = [[1, 2, 3], [], [4]];

            var bytes = NCborSerializer.Serialize(grid, Context.ArrayOfArrayOfInt32);
            var result = NCborSerializer.Deserialize(bytes, Context.ArrayOfArrayOfInt32);

            result.Should().HaveCount(3);
            result[0].Should().Equal(1, 2, 3);
            result[1].Should().BeEmpty();
            result[2].Should().Equal(4);
        }

        [Fact]
        public void JaggedByteArray_RoundTrips()
        {
            byte[][] blobs = [[0x01, 0x02], [], [0xFF]];

            var bytes = NCborSerializer.Serialize(blobs, Context.ArrayOfArrayOfByte);
            var result = NCborSerializer.Deserialize(bytes, Context.ArrayOfArrayOfByte);

            result.Should().HaveCount(3);
            result[0].Should().Equal(0x01, 0x02);
            result[1].Should().BeEmpty();
            result[2].Should().Equal(0xFF);
        }

        [Fact]
        public void JaggedArrayProperties_RoundTrip()
        {
            var holder = new JaggedHolder
            {
                Grid = [[1], [2, 3]],
                Blobs = [[0xAB], [0xCD, 0xEF]],
            };

            var bytes = NCborSerializer.Serialize(holder, Context.JaggedHolder);
            var result = NCborSerializer.Deserialize(bytes, Context.JaggedHolder);

            result.Grid.Should().HaveCount(2);
            result.Grid[1].Should().Equal(2, 3);
            result.Blobs.Should().HaveCount(2);
            result.Blobs[1].Should().Equal(0xCD, 0xEF);
        }

        [Fact]
        public void JaggedIntArray_IndefiniteLength_ReadsBack()
        {
            var writer = new CborWriter();
            writer.WriteStartArray(null);
            writer.WriteStartArray(2);
            writer.WriteInt32(1);
            writer.WriteInt32(2);
            writer.WriteEndArray();
            writer.WriteEndArray();

            var result = NCborSerializer.Deserialize(writer.Encode(), Context.ArrayOfArrayOfInt32);

            result.Should().ContainSingle().Which.Should().Equal(1, 2);
        }

        // ---------- Null dictionary keys ----------

        [Fact]
        public void NullStringKey_ThrowsDeserializationException()
        {
            var act = () => NCborSerializer.Deserialize(NullKeyPayload(w => w.WriteInt32(1)), Context.DictionaryOfStringAndInt32);

            act.Should().Throw<NCborDeserializationException>().WithMessage("*null dictionary key*");
        }

        [Fact]
        public void NullIntKey_ThrowsDeserializationException()
        {
            var act = () => NCborSerializer.Deserialize(NullKeyPayload(w => w.WriteTextString("v")), Context.DictionaryOfInt32AndString);

            act.Should().Throw<NCborDeserializationException>().WithMessage("*null dictionary key*");
        }

        /// <summary>A one-entry map <c>{null: value}</c>.</summary>
        private static byte[] NullKeyPayload(Action<CborWriter> writeValue)
        {
            var writer = new CborWriter();
            writer.WriteStartMap(1);
            writer.WriteNull();
            writeValue(writer);
            writer.WriteEndMap();
            return writer.Encode();
        }

        private static List<string> ReadMapKeys(byte[] bytes)
        {
            var reader = new CborReader(bytes);
            var keys = new List<string>();
            reader.ReadStartMap();
            while (reader.PeekState() != CborReaderState.EndMap)
            {
                keys.Add(reader.ReadTextString());
                reader.SkipValue();
            }
            return keys;
        }
    }
}
