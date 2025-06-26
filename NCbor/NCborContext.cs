namespace NCbor;

/// <summary>
/// Base class for CBOR serialization context that provides type information and serialization methods.
/// </summary>
public abstract class NCborSerializerContext
{
    private static readonly Dictionary<Type, NCborSerializerContext> _defaultContexts = [];

    /// <summary>
    /// Gets the default instance of the context.
    /// </summary>
    public static T Default<T>() where T : NCborSerializerContext, new()
    {
        var type = typeof(T);
        if (!_defaultContexts.TryGetValue(type, out var context))
        {
            context = new T();
            _defaultContexts[type] = context;
        }
        return (T)context;
    }

    /// <summary>
    /// Gets the type information for the specified type.
    /// </summary>
    public abstract NCborTypeInfo<T> GetTypeInfo<T>();
} 

