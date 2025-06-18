namespace CbOrSerialization.Demo;

[CbOrSerializable(typeof(Person))]
[CbOrSerializable(typeof(List<Person>))]
public partial class MyCbOrContext : CbOrSerializerContext
{
} 