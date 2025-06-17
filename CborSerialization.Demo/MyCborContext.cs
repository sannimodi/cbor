namespace CborSerialization.Demo;

[CborSerializable(typeof(Person))]
public partial class MyCborContext : CborSerializerContext {
    public static CborTypeInfo<Person> Person { get; } = CborTypeInfo<Person>.Default;
}
