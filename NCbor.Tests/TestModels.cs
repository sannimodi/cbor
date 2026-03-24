namespace NCbor.Tests;

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
    [NCborPropertyName("full_name")]
    public string Name { get; set; } = string.Empty;
    
    public int Age { get; set; }
    
    [NCborIgnore]
    public string InternalId { get; set; } = string.Empty;
    
    [NCborDefaultValue(true)]
    public bool IsEnabled { get; set; } = true;
}

public class NestedModel
{
    public string Title { get; set; } = string.Empty;
    public SimpleModel? Details { get; set; }
    public List<string> Tags { get; set; } = [];
}

public class DictionaryModel
{
    public Dictionary<string, string> StringToString { get; set; } = [];
    public Dictionary<string, int> StringToInt { get; set; } = [];
    public Dictionary<int, string> IntToString { get; set; } = [];
    public Dictionary<string, bool> StringToBool { get; set; } = [];
    public Dictionary<Guid, string> GuidToString { get; set; } = [];
}

public class NullableDictionaryModel
{
    public Dictionary<string, string>? OptionalStringDict { get; set; }
    public Dictionary<string, int>? OptionalIntDict { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ComplexDictionaryModel
{
    public Dictionary<string, SimpleModel> StringToModel { get; set; } = [];
    public Dictionary<string, List<string>> StringToList { get; set; } = [];
    public Dictionary<string, Dictionary<string, int>> NestedDictionaries { get; set; } = [];
}

public class SimpleDecimalModel
{
    public decimal Value { get; set; }
    public string Name { get; set; } = string.Empty;
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

public class ArrayModel
{
    public string[] StringArray { get; set; } = [];
    public int[] IntArray { get; set; } = [];
    public double[] DoubleArray { get; set; } = [];
    public SimpleModel[] ObjectArray { get; set; } = [];
}

public class NullableArrayModel
{
    public string[]? OptionalStringArray { get; set; }
    public int[]? OptionalIntArray { get; set; }
    public SimpleModel[]? OptionalObjectArray { get; set; }
}

public class MixedArrayModel
{
    public string Name { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
    public int[] Scores { get; set; } = [];
    public List<string> Notes { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
    public SimpleModel[]? OptionalModels { get; set; }
}

// Byte array test models
public class ByteArrayModel
{
    public byte[] Data { get; set; } = [];
    public byte[]? OptionalData { get; set; }
    public string Name { get; set; } = string.Empty;
}

// Enum test models
public enum UserRole
{
    Guest = 0,
    User = 1,
    Admin = 2,
    SuperAdmin = 3
}

public enum Priority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

[Flags]
public enum Permissions
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4,
    Delete = 8,
    All = Read | Write | Execute | Delete
}

public enum Status : byte
{
    Inactive = 0,
    Active = 1,
    Pending = 2,
    Suspended = 3
}

public class EnumModel
{
    public UserRole Role { get; set; }
    public UserRole? OptionalRole { get; set; }
    public Priority TaskPriority { get; set; }
    public Priority? OptionalPriority { get; set; }
    public Permissions UserPermissions { get; set; }
    public Permissions? OptionalPermissions { get; set; }
    public Status CurrentStatus { get; set; }
    public Status? OptionalStatus { get; set; }
    public string Name { get; set; } = string.Empty;
}

// Test context for source generator
[NCborSerializable(typeof(SimpleModel))]
[NCborSerializable(typeof(GuidModel))]
[NCborSerializable(typeof(DateTimeModel))]
[NCborSerializable(typeof(SimpleDecimalModel))]
[NCborSerializable(typeof(DecimalModel))]
[NCborSerializable(typeof(ModelWithAttributes))]
[NCborSerializable(typeof(NestedModel))]
[NCborSerializable(typeof(DictionaryModel))]
[NCborSerializable(typeof(NullableDictionaryModel))]
[NCborSerializable(typeof(ComplexDictionaryModel))]
[NCborSerializable(typeof(AllTypesModel))]
[NCborSerializable(typeof(ArrayModel))]
[NCborSerializable(typeof(NullableArrayModel))]
[NCborSerializable(typeof(MixedArrayModel))]
[NCborSerializable(typeof(ByteArrayModel))]
[NCborSerializable(typeof(EnumModel))]
[NCborSerializable(typeof(List<SimpleModel>))]
[NCborSerializable(typeof(List<string>))]
[NCborSerializable(typeof(Dictionary<string, string>))]
[NCborSerializable(typeof(Dictionary<string, int>))]
[NCborSerializable(typeof(Dictionary<int, string>))]
[NCborSerializable(typeof(Dictionary<string, bool>))]
[NCborSerializable(typeof(Dictionary<Guid, string>))]
[NCborSerializable(typeof(Dictionary<string, SimpleModel>))]
[NCborSerializable(typeof(Dictionary<string, List<string>>))]
[NCborSerializable(typeof(Dictionary<string, Dictionary<string, int>>))]
[NCborSerializable(typeof(string[]))]
[NCborSerializable(typeof(int[]))]
[NCborSerializable(typeof(double[]))]
[NCborSerializable(typeof(SimpleModel[]))]
[NCborSerializable(typeof(byte[]))]
public partial class TestNCborContext : NCborSerializerContext
{
}
