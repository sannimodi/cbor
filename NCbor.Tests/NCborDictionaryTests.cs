namespace NCbor.Tests;

public class NCborDictionaryTests
{
    private readonly TestNCborContext _context = TestNCborContext.Default;

    [Fact]
    public void Serialize_DictionaryStringToString_ReturnsValidCborData()
    {
        // Arrange
        var model = new DictionaryModel
        {
            StringToString = new Dictionary<string, string>
            {
                {"key1", "value1"},
                {"key2", "value2"},
                {"", "empty_key"}
            }
        };

        // Act
        var result = NCborSerializer.Serialize(model, _context.DictionaryModel);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        
        // Verify it's valid CBOR
        var reader = new CborReader(result);
        reader.PeekState().Should().Be(CborReaderState.StartMap);
    }

    [Fact]
    public void Serialize_DictionaryStringToInt_ReturnsValidCborData()
    {
        // Arrange
        var dictionary = new Dictionary<string, int>
        {
            {"one", 1},
            {"two", 2},
            {"zero", 0},
            {"negative", -42}
        };

        // Act
        var result = NCborSerializer.Serialize(dictionary, _context.DictionaryOfStringAndInt32);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void Deserialize_DictionaryStringToString_ReturnsCorrectData()
    {
        // Arrange
        var original = new Dictionary<string, string>
        {
            {"firstName", "John"},
            {"lastName", "Doe"},
            {"city", "New York"}
        };
        var cborData = NCborSerializer.Serialize(original, _context.DictionaryOfStringAndString);

        // Act
        var deserialized = NCborSerializer.Deserialize(cborData, _context.DictionaryOfStringAndString);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().HaveCount(3);
        deserialized["firstName"].Should().Be("John");
        deserialized["lastName"].Should().Be("Doe");
        deserialized["city"].Should().Be("New York");
    }

    [Fact]
    public void RoundTrip_DictionaryModel_PreservesAllData()
    {
        // Arrange
        var original = new DictionaryModel
        {
            StringToString = new Dictionary<string, string>
            {
                {"name", "Alice"},
                {"role", "Developer"}
            },
            StringToInt = new Dictionary<string, int>
            {
                {"age", 30},
                {"years_experience", 8}
            },
            IntToString = new Dictionary<int, string>
            {
                {1, "first"},
                {2, "second"},
                {100, "hundred"}
            },
            StringToBool = new Dictionary<string, bool>
            {
                {"is_active", true},
                {"is_admin", false}
            },
            GuidToString = new Dictionary<Guid, string>
            {
                {Guid.Parse("550e8400-e29b-41d4-a716-446655440000"), "test-guid"},
                {Guid.NewGuid(), "random-guid"}
            }
        };
        var cborData = NCborSerializer.Serialize(original, _context.DictionaryModel);

        // Act
        var deserialized = NCborSerializer.Deserialize(cborData, _context.DictionaryModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.StringToString.Should().BeEquivalentTo(original.StringToString);
        deserialized.StringToInt.Should().BeEquivalentTo(original.StringToInt);
        deserialized.IntToString.Should().BeEquivalentTo(original.IntToString);
        deserialized.StringToBool.Should().BeEquivalentTo(original.StringToBool);
        deserialized.GuidToString.Should().BeEquivalentTo(original.GuidToString);
    }

    [Fact]
    public void Serialize_EmptyDictionary_ReturnsValidCborData()
    {
        // Arrange
        var emptyDict = new Dictionary<string, string>();

        // Act
        var result = NCborSerializer.Serialize(emptyDict, _context.DictionaryOfStringAndString);

        // Assert
        result.Should().NotBeNull();
        
        // Verify it's a valid empty CBOR map
        var reader = new CborReader(result);
        reader.PeekState().Should().Be(CborReaderState.StartMap);
        var mapSize = reader.ReadStartMap();
        mapSize.Should().Be(0);
        reader.PeekState().Should().Be(CborReaderState.EndMap);
    }

    [Fact]
    public void Deserialize_EmptyDictionary_ReturnsEmptyDictionary()
    {
        // Arrange
        var emptyDict = new Dictionary<string, int>();
        var cborData = NCborSerializer.Serialize(emptyDict, _context.DictionaryOfStringAndInt32);

        // Act
        var deserialized = NCborSerializer.Deserialize(cborData, _context.DictionaryOfStringAndInt32);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().BeEmpty();
    }

    [Fact]
    public void Serialize_NullableDictionaryWithValue_ReturnsValidCborData()
    {
        // Arrange
        var model = new NullableDictionaryModel
        {
            Name = "Test",
            OptionalStringDict = new Dictionary<string, string>
            {
                {"key", "value"}
            },
            OptionalIntDict = new Dictionary<string, int>
            {
                {"count", 42}
            }
        };

        // Act
        var result = NCborSerializer.Serialize(model, _context.NullableDictionaryModel);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void Serialize_NullableDictionaryWithNull_ReturnsValidCborData()
    {
        // Arrange
        var model = new NullableDictionaryModel
        {
            Name = "Test",
            OptionalStringDict = null,
            OptionalIntDict = null
        };

        // Act
        var result = NCborSerializer.Serialize(model, _context.NullableDictionaryModel);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void RoundTrip_NullableDictionaryModel_PreservesNullValues()
    {
        // Arrange
        var original = new NullableDictionaryModel
        {
            Name = "TestModel",
            OptionalStringDict = null,
            OptionalIntDict = new Dictionary<string, int> { {"test", 123} }
        };
        var cborData = NCborSerializer.Serialize(original, _context.NullableDictionaryModel);

        // Act
        var deserialized = NCborSerializer.Deserialize(cborData, _context.NullableDictionaryModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Name.Should().Be(original.Name);
        deserialized.OptionalStringDict.Should().BeNull();
        deserialized.OptionalIntDict.Should().BeEquivalentTo(original.OptionalIntDict);
    }

    [Fact]
    public void Serialize_ComplexDictionaryModel_ReturnsValidCborData()
    {
        // Arrange
        var model = new ComplexDictionaryModel
        {
            StringToModel = new Dictionary<string, SimpleModel>
            {
                {"person1", new SimpleModel { Name = "Alice", Age = 25, IsActive = true }},
                {"person2", new SimpleModel { Name = "Bob", Age = 30, IsActive = false }}
            },
            StringToList = new Dictionary<string, List<string>>
            {
                {"colors", new List<string> {"red", "green", "blue"}},
                {"fruits", new List<string> {"apple", "banana"}}
            },
            NestedDictionaries = new Dictionary<string, Dictionary<string, int>>
            {
                {"group1", new Dictionary<string, int> { {"a", 1}, {"b", 2} }},
                {"group2", new Dictionary<string, int> { {"x", 10}, {"y", 20} }}
            }
        };

        // Act
        var result = NCborSerializer.Serialize(model, _context.ComplexDictionaryModel);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void RoundTrip_ComplexDictionaryModel_PreservesAllData()
    {
        // Arrange
        var original = new ComplexDictionaryModel
        {
            StringToModel = new Dictionary<string, SimpleModel>
            {
                {"user1", new SimpleModel { Name = "Charlie", Age = 35, IsActive = true }}
            },
            StringToList = new Dictionary<string, List<string>>
            {
                {"tags", new List<string> {"important", "work", "priority"}}
            },
            NestedDictionaries = new Dictionary<string, Dictionary<string, int>>
            {
                {"metrics", new Dictionary<string, int> { {"views", 100}, {"clicks", 25} }}
            }
        };
        var cborData = NCborSerializer.Serialize(original, _context.ComplexDictionaryModel);

        // Act
        var deserialized = NCborSerializer.Deserialize(cborData, _context.ComplexDictionaryModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.StringToModel.Should().HaveCount(1);
        deserialized.StringToModel["user1"].Name.Should().Be("Charlie");
        deserialized.StringToModel["user1"].Age.Should().Be(35);
        deserialized.StringToModel["user1"].IsActive.Should().BeTrue();
        
        deserialized.StringToList.Should().HaveCount(1);
        deserialized.StringToList["tags"].Should().BeEquivalentTo(original.StringToList["tags"]);
        
        deserialized.NestedDictionaries.Should().HaveCount(1);
        deserialized.NestedDictionaries["metrics"].Should().BeEquivalentTo(original.NestedDictionaries["metrics"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(100)]
    public void RoundTrip_DictionaryWithDifferentSizes_WorksCorrectly(int size)
    {
        // Arrange
        var dictionary = new Dictionary<string, int>();
        for (int i = 0; i < size; i++)
        {
            dictionary[$"key{i}"] = i * 10;
        }
        var cborData = NCborSerializer.Serialize(dictionary, _context.DictionaryOfStringAndInt32);

        // Act
        var deserialized = NCborSerializer.Deserialize(cborData, _context.DictionaryOfStringAndInt32);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().HaveCount(size);
        deserialized.Should().BeEquivalentTo(dictionary);
    }
}