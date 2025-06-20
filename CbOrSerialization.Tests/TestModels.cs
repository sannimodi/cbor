namespace CbOrSerialization.Tests;

/// <summary>
/// Test models for CBOR serialization testing
/// </summary>
public class SimpleModel
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool IsActive { get; set; }
}

public class GuidModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? OptionalId { get; set; }
}

public class DateTimeModel
{
    public DateTime CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime? OptionalDate { get; set; }
    public DateTimeOffset? OptionalDateOffset { get; set; }
}

public class ModelWithAttributes
{
    [CbOrPropertyName("full_name")]
    public string Name { get; set; } = string.Empty;
    
    public int Age { get; set; }
    
    [CbOrIgnore]
    public string InternalId { get; set; } = string.Empty;
    
    [CbOrDefaultValue(true)]
    public bool IsEnabled { get; set; } = true;
}

public class NestedModel
{
    public string Title { get; set; } = string.Empty;
    public SimpleModel? Details { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class DictionaryModel
{
    public Dictionary<string, string> StringToString { get; set; } = new();
    public Dictionary<string, int> StringToInt { get; set; } = new();
    public Dictionary<int, string> IntToString { get; set; } = new();
    public Dictionary<string, bool> StringToBool { get; set; } = new();
    public Dictionary<Guid, string> GuidToString { get; set; } = new();
}

public class NullableDictionaryModel
{
    public Dictionary<string, string>? OptionalStringDict { get; set; }
    public Dictionary<string, int>? OptionalIntDict { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ComplexDictionaryModel
{
    public Dictionary<string, SimpleModel> StringToModel { get; set; } = new();
    public Dictionary<string, List<string>> StringToList { get; set; } = new();
    public Dictionary<string, Dictionary<string, int>> NestedDictionaries { get; set; } = new();
}

public class DecimalModel
{
    public decimal Value { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? OptionalValue { get; set; }
    public decimal Zero { get; set; }
    public decimal Large { get; set; }
    public decimal Small { get; set; }
    public decimal Negative { get; set; }
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
[CbOrSerializable(typeof(SimpleModel))]
[CbOrSerializable(typeof(GuidModel))]
[CbOrSerializable(typeof(DateTimeModel))]
//[CbOrSerializable(typeof(DecimalModel))]  // Temporarily commented out to debug
[CbOrSerializable(typeof(ModelWithAttributes))]
[CbOrSerializable(typeof(NestedModel))]
[CbOrSerializable(typeof(DictionaryModel))]
[CbOrSerializable(typeof(NullableDictionaryModel))]
[CbOrSerializable(typeof(ComplexDictionaryModel))]
[CbOrSerializable(typeof(AllTypesModel))]
[CbOrSerializable(typeof(List<SimpleModel>))]
[CbOrSerializable(typeof(List<string>))]
[CbOrSerializable(typeof(Dictionary<string, string>))]
[CbOrSerializable(typeof(Dictionary<string, int>))]
[CbOrSerializable(typeof(Dictionary<int, string>))]
[CbOrSerializable(typeof(Dictionary<string, bool>))]
[CbOrSerializable(typeof(Dictionary<Guid, string>))]
[CbOrSerializable(typeof(Dictionary<string, SimpleModel>))]
[CbOrSerializable(typeof(Dictionary<string, List<string>>))]
[CbOrSerializable(typeof(Dictionary<string, Dictionary<string, int>>))]
public partial class TestCbOrContext : CbOrSerializerContext
{
}

