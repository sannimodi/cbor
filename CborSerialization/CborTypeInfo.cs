namespace CborSerialization;

public class CborTypeInfo<T>
{
    public static CborTypeInfo<T> Default { get; } = new();
}
