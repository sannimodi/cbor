namespace CbOrSerialization.Tests;

public class CbOrArrayTests
{
    private readonly TestCbOrContext _context = TestCbOrContext.Default;

    [Fact]
    public void SerializeStringArray_ShouldSucceed()
    {
        // Arrange
        var model = new ArrayModel
        {
            StringArray = new[] { "hello", "world", "cbor" }
        };

        // Act
        var bytes = CbOrSerializer.Serialize(model, _context.ArrayModel);
        var deserialized = CbOrSerializer.Deserialize(bytes, _context.ArrayModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.StringArray.Length.Should().Be(3);
        deserialized.StringArray.Should().BeEquivalentTo(model.StringArray);
    }

    [Fact]
    public void SerializeIntArray_ShouldSucceed()
    {
        // Arrange
        var model = new ArrayModel
        {
            IntArray = new[] { 1, 2, 3, 42, -10 }
        };

        // Act
        var bytes = CbOrSerializer.Serialize(model, _context.ArrayModel);
        var deserialized = CbOrSerializer.Deserialize(bytes, _context.ArrayModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.IntArray.Length.Should().Be(5);
        deserialized.IntArray.Should().BeEquivalentTo(model.IntArray);
    }

    [Fact]
    public void SerializeStringArrayDirectly_ShouldSucceed()
    {
        // Arrange
        var stringArray = new[] { "direct", "array", "test" };

        // Act
        var bytes = CbOrSerializer.Serialize(stringArray, _context.ArrayOfString);
        var deserialized = CbOrSerializer.Deserialize(bytes, _context.ArrayOfString);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Length.Should().Be(3);
        deserialized.Should().BeEquivalentTo(stringArray);
    }
}