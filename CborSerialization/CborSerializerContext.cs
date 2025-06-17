namespace CborSerialization;

public abstract class CborSerializerContext
{
    public static CborSerializerContext Default { get; } = new DefaultContext();

    private sealed class DefaultContext : CborSerializerContext
    {
    }
}