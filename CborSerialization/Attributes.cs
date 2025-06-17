namespace CborSerialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class CborSerializableAttribute : Attribute
{
    public Type? Type { get; }

    public CborSerializableAttribute() { }

    public CborSerializableAttribute(Type type)
    {
        Type = type;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class CborIgnoreAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class CborPropertyAttribute : Attribute
{
    public string Name { get; }

    public CborPropertyAttribute(string name)
    {
        Name = name;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field)]
public class CborConverterAttribute : Attribute
{
    public Type ConverterType { get; }

    public CborConverterAttribute(Type converterType)
    {
        ConverterType = converterType;
    }
}
