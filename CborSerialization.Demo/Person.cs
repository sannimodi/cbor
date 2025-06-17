namespace CborSerialization.Demo;

[CborSerializable]
public partial class Person
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    [CborIgnore]
    public int Age { get; set; }
}
