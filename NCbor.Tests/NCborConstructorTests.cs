namespace NCbor.Tests;

public class NCborConstructorTests
{
    private readonly TestNCborContext _context = TestNCborContext.Default;

    [Fact]
    public void Serialize_ImmutableType_GetOnlyPropertiesAreIncludedInMap()
    {
        var model = new ImmutablePerson("Ada Lovelace", 36);

        var data = NCborSerializer.Serialize(model, _context.ImmutablePerson);

        var reader = new CborReader(data);
        reader.ReadStartMap();
        var propertyNames = new List<string>();
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            propertyNames.Add(reader.ReadTextString());
            reader.SkipValue();
        }

        propertyNames.Should().Contain("Name");
        propertyNames.Should().Contain("Age");
    }

    [Fact]
    public void RoundTrip_ImmutableType_ConstructsViaMarkedConstructor()
    {
        var model = new ImmutablePerson("Ada Lovelace", 36);

        var data = NCborSerializer.Serialize(model, _context.ImmutablePerson);
        var result = NCborSerializer.Deserialize(data, _context.ImmutablePerson);

        result.Name.Should().Be("Ada Lovelace");
        result.Age.Should().Be(36);
    }

    [Fact]
    public void RoundTrip_HybridConstructorAndSettableProperty_BothArePopulated()
    {
        var model = new HybridConstructorModel("abc-123") { Note = "extra info" };

        var data = NCborSerializer.Serialize(model, _context.HybridConstructorModel);
        var result = NCborSerializer.Deserialize(data, _context.HybridConstructorModel);

        result.Id.Should().Be("abc-123");
        result.Note.Should().Be("extra info");
    }
}
