namespace NCbor.Tests;

public class NCborEnumAsStringTests
{
    private readonly TestNCborContext _context = TestNCborContext.Default;

    private static string ReadTextStringForKey(byte[] data, string key)
    {
        var reader = new CborReader(data);
        reader.ReadStartMap();
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var propertyName = reader.ReadTextString();
            if (propertyName == key)
            {
                return reader.ReadTextString();
            }
            reader.SkipValue();
        }
        throw new InvalidOperationException($"Key '{key}' not found");
    }

    [Fact]
    public void Serialize_EnumAsString_WritesEnumNameNotNumber()
    {
        var model = new EnumAsStringModel { Name = "a", Role = UserRole.Admin };

        var data = NCborSerializer.Serialize(model, _context.EnumAsStringModel);

        ReadTextStringForKey(data, "Role").Should().Be("Admin");
    }

    [Fact]
    public void RoundTrip_EnumAsString_PreservesValue()
    {
        var model = new EnumAsStringModel { Name = "a", Role = UserRole.SuperAdmin };

        var data = NCborSerializer.Serialize(model, _context.EnumAsStringModel);
        var result = NCborSerializer.Deserialize(data, _context.EnumAsStringModel);

        result.Role.Should().Be(UserRole.SuperAdmin);
    }

    [Fact]
    public void RoundTrip_NullableEnumAsString_NonNull_PreservesValue()
    {
        var model = new EnumAsStringModel { Name = "a", Role = UserRole.User, OptionalRole = UserRole.Guest };

        var data = NCborSerializer.Serialize(model, _context.EnumAsStringModel);
        var result = NCborSerializer.Deserialize(data, _context.EnumAsStringModel);

        result.OptionalRole.Should().Be(UserRole.Guest);
    }

    [Fact]
    public void RoundTrip_NullableEnumAsString_Null_PreservesNull()
    {
        var model = new EnumAsStringModel { Name = "a", Role = UserRole.User, OptionalRole = null };

        var data = NCborSerializer.Serialize(model, _context.EnumAsStringModel);
        var result = NCborSerializer.Deserialize(data, _context.EnumAsStringModel);

        result.OptionalRole.Should().BeNull();
    }
}
