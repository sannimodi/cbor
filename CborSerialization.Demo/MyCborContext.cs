namespace CborSerialization.Demo;

[CborSerializable(typeof(Person))]
[CborSerializable(typeof(List<Person>))]
public partial class MyCborContext : CborSerializerContext
{
} 