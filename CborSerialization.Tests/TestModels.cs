using CborSerialization;

namespace CborSerialization.Tests;

/// <summary>
/// Test models for CBOR serialization testing
/// </summary>
public class SimpleModel
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool IsActive { get; set; }
}

public class ModelWithAttributes
{
    [CborPropertyName("full_name")]
    public string Name { get; set; } = string.Empty;
    
    public int Age { get; set; }
    
    [CborIgnore]
    public string InternalId { get; set; } = string.Empty;
    
    [CborDefaultValue(true)]
    public bool IsEnabled { get; set; } = true;
}

public class NestedModel
{
    public string Title { get; set; } = string.Empty;
    public SimpleModel? Details { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class AllTypesModel
{
    // Primitives
    public string StringValue { get; set; } = string.Empty;
    public int IntValue { get; set; }
    public long LongValue { get; set; }
    public double DoubleValue { get; set; }
    public float FloatValue { get; set; }
    public bool BoolValue { get; set; }
    public byte ByteValue { get; set; }
    public sbyte SByteValue { get; set; }
    public short ShortValue { get; set; }
    public ushort UShortValue { get; set; }
    public uint UIntValue { get; set; }
    public ulong ULongValue { get; set; }
    
    // Nullable types
    public int? NullableInt { get; set; }
    public bool? NullableBool { get; set; }
}

// Test context for source generator
[CborSerializable(typeof(SimpleModel))]
[CborSerializable(typeof(ModelWithAttributes))]
[CborSerializable(typeof(NestedModel))]
[CborSerializable(typeof(AllTypesModel))]
[CborSerializable(typeof(List<SimpleModel>))]
[CborSerializable(typeof(List<string>))]
public partial class TestCborContext : CborSerializerContext
{
}