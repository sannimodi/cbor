namespace CborSerialization.Demo
{
    // Define a simple model
    public class Person
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public bool IsActive { get; set; }
    }

    // Create a context for serialization
    [CborSerializable(typeof(Person))]
    public partial class MyCborContext : CborSerializerContext
    {
    }
}