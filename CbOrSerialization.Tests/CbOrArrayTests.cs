namespace CbOrSerialization.Tests;

[TestClass]
public class CbOrArrayTests
{
    private readonly TestCbOrContext _context = new TestCbOrContext();

    [TestMethod]
    public void SerializeStringArray_ShouldSucceed()
    {
        // Arrange
        var model = new ArrayModel
        {
            StringArray = new[] { "hello", "world", "cbor" }
        };

        // Act
        var bytes = CbOrSerializer.SerializeToBytes(model, _context);
        var deserialized = CbOrSerializer.DeserializeFromBytes<ArrayModel>(bytes, _context);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(3, deserialized.StringArray.Length);
        CollectionAssert.AreEqual(model.StringArray, deserialized.StringArray);
    }

    [TestMethod]
    public void SerializeIntArray_ShouldSucceed()
    {
        // Arrange
        var model = new ArrayModel
        {
            IntArray = new[] { 1, 2, 3, 42, -10 }
        };

        // Act
        var bytes = CbOrSerializer.SerializeToBytes(model, _context);
        var deserialized = CbOrSerializer.DeserializeFromBytes<ArrayModel>(bytes, _context);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(5, deserialized.IntArray.Length);
        CollectionAssert.AreEqual(model.IntArray, deserialized.IntArray);
    }

    [TestMethod]
    public void SerializeDoubleArray_ShouldSucceed()
    {
        // Arrange
        var model = new ArrayModel
        {
            DoubleArray = new[] { 1.5, 2.7, 3.14159, -0.5 }
        };

        // Act
        var bytes = CbOrSerializer.SerializeToBytes(model, _context);
        var deserialized = CbOrSerializer.DeserializeFromBytes<ArrayModel>(bytes, _context);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(4, deserialized.DoubleArray.Length);
        CollectionAssert.AreEqual(model.DoubleArray, deserialized.DoubleArray);
    }

    [TestMethod]
    public void SerializeObjectArray_ShouldSucceed()
    {
        // Arrange
        var model = new ArrayModel
        {
            ObjectArray = new[]
            {
                new SimpleModel { Name = "John", Age = 30, IsActive = true },
                new SimpleModel { Name = "Jane", Age = 25, IsActive = false }
            }
        };

        // Act
        var bytes = CbOrSerializer.SerializeToBytes(model, _context);
        var deserialized = CbOrSerializer.DeserializeFromBytes<ArrayModel>(bytes, _context);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(2, deserialized.ObjectArray.Length);
        Assert.AreEqual("John", deserialized.ObjectArray[0].Name);
        Assert.AreEqual(30, deserialized.ObjectArray[0].Age);
        Assert.AreEqual(true, deserialized.ObjectArray[0].IsActive);
        Assert.AreEqual("Jane", deserialized.ObjectArray[1].Name);
        Assert.AreEqual(25, deserialized.ObjectArray[1].Age);
        Assert.AreEqual(false, deserialized.ObjectArray[1].IsActive);
    }

    [TestMethod]
    public void SerializeEmptyArrays_ShouldSucceed()
    {
        // Arrange
        var model = new ArrayModel
        {
            StringArray = Array.Empty<string>(),
            IntArray = Array.Empty<int>(),
            DoubleArray = Array.Empty<double>(),
            ObjectArray = Array.Empty<SimpleModel>()
        };

        // Act
        var bytes = CbOrSerializer.SerializeToBytes(model, _context);
        var deserialized = CbOrSerializer.DeserializeFromBytes<ArrayModel>(bytes, _context);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(0, deserialized.StringArray.Length);
        Assert.AreEqual(0, deserialized.IntArray.Length);
        Assert.AreEqual(0, deserialized.DoubleArray.Length);
        Assert.AreEqual(0, deserialized.ObjectArray.Length);
    }

    [TestMethod]
    public void SerializeNullableArrays_NonNull_ShouldSucceed()
    {
        // Arrange
        var model = new NullableArrayModel
        {
            OptionalStringArray = new[] { "test1", "test2" },
            OptionalIntArray = new[] { 10, 20, 30 },
            OptionalObjectArray = new[]
            {
                new SimpleModel { Name = "Test", Age = 50, IsActive = true }
            }
        };

        // Act
        var bytes = CbOrSerializer.SerializeToBytes(model, _context);
        var deserialized = CbOrSerializer.DeserializeFromBytes<NullableArrayModel>(bytes, _context);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.IsNotNull(deserialized.OptionalStringArray);
        Assert.AreEqual(2, deserialized.OptionalStringArray.Length);
        CollectionAssert.AreEqual(model.OptionalStringArray, deserialized.OptionalStringArray);
        
        Assert.IsNotNull(deserialized.OptionalIntArray);
        Assert.AreEqual(3, deserialized.OptionalIntArray.Length);
        CollectionAssert.AreEqual(model.OptionalIntArray, deserialized.OptionalIntArray);
        
        Assert.IsNotNull(deserialized.OptionalObjectArray);
        Assert.AreEqual(1, deserialized.OptionalObjectArray.Length);
        Assert.AreEqual("Test", deserialized.OptionalObjectArray[0].Name);
    }

    [TestMethod]
    public void SerializeNullableArrays_Null_ShouldSucceed()
    {
        // Arrange
        var model = new NullableArrayModel
        {
            OptionalStringArray = null,
            OptionalIntArray = null,
            OptionalObjectArray = null
        };

        // Act
        var bytes = CbOrSerializer.SerializeToBytes(model, _context);
        var deserialized = CbOrSerializer.DeserializeFromBytes<NullableArrayModel>(bytes, _context);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.IsNull(deserialized.OptionalStringArray);
        Assert.IsNull(deserialized.OptionalIntArray);
        Assert.IsNull(deserialized.OptionalObjectArray);
    }

    [TestMethod]
    public void SerializeMixedArrayModel_ShouldSucceed()
    {
        // Arrange
        var model = new MixedArrayModel
        {
            Name = "Test Model",
            Tags = new[] { "tag1", "tag2", "tag3" },
            Scores = new[] { 85, 90, 92 },
            Notes = new List<string> { "note1", "note2" },
            Metadata = new Dictionary<string, string> { { "key1", "value1" } },
            OptionalModels = new[]
            {
                new SimpleModel { Name = "Model1", Age = 10, IsActive = true },
                new SimpleModel { Name = "Model2", Age = 20, IsActive = false }
            }
        };

        // Act
        var bytes = CbOrSerializer.SerializeToBytes(model, _context);
        var deserialized = CbOrSerializer.DeserializeFromBytes<MixedArrayModel>(bytes, _context);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual("Test Model", deserialized.Name);
        
        Assert.AreEqual(3, deserialized.Tags.Length);
        CollectionAssert.AreEqual(model.Tags, deserialized.Tags);
        
        Assert.AreEqual(3, deserialized.Scores.Length);
        CollectionAssert.AreEqual(model.Scores, deserialized.Scores);
        
        Assert.AreEqual(2, deserialized.Notes.Count);
        CollectionAssert.AreEqual(model.Notes, deserialized.Notes);
        
        Assert.AreEqual(1, deserialized.Metadata.Count);
        Assert.AreEqual("value1", deserialized.Metadata["key1"]);
        
        Assert.IsNotNull(deserialized.OptionalModels);
        Assert.AreEqual(2, deserialized.OptionalModels.Length);
        Assert.AreEqual("Model1", deserialized.OptionalModels[0].Name);
        Assert.AreEqual("Model2", deserialized.OptionalModels[1].Name);
    }

    [TestMethod]
    public void SerializeLargeArray_ShouldSucceed()
    {
        // Arrange
        var largeArray = Enumerable.Range(1, 100).ToArray();
        var model = new ArrayModel
        {
            IntArray = largeArray
        };

        // Act
        var bytes = CbOrSerializer.SerializeToBytes(model, _context);
        var deserialized = CbOrSerializer.DeserializeFromBytes<ArrayModel>(bytes, _context);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(100, deserialized.IntArray.Length);
        CollectionAssert.AreEqual(largeArray, deserialized.IntArray);
    }

    [TestMethod]
    public void SerializeStringArrayDirectly_ShouldSucceed()
    {
        // Arrange
        var stringArray = new[] { "direct", "array", "test" };

        // Act
        var bytes = CbOrSerializer.SerializeToBytes(stringArray, _context);
        var deserialized = CbOrSerializer.DeserializeFromBytes<string[]>(bytes, _context);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(3, deserialized.Length);
        CollectionAssert.AreEqual(stringArray, deserialized);
    }

    [TestMethod]
    public void SerializeIntArrayDirectly_ShouldSucceed()
    {
        // Arrange
        var intArray = new[] { 42, -10, 0, 999 };

        // Act
        var bytes = CbOrSerializer.SerializeToBytes(intArray, _context);
        var deserialized = CbOrSerializer.DeserializeFromBytes<int[]>(bytes, _context);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(4, deserialized.Length);
        CollectionAssert.AreEqual(intArray, deserialized);
    }

    [TestMethod]
    public void SerializeObjectArrayDirectly_ShouldSucceed()
    {
        // Arrange
        var objectArray = new[]
        {
            new SimpleModel { Name = "Direct1", Age = 100, IsActive = true },
            new SimpleModel { Name = "Direct2", Age = 200, IsActive = false }
        };

        // Act
        var bytes = CbOrSerializer.SerializeToBytes(objectArray, _context);
        var deserialized = CbOrSerializer.DeserializeFromBytes<SimpleModel[]>(bytes, _context);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(2, deserialized.Length);
        Assert.AreEqual("Direct1", deserialized[0].Name);
        Assert.AreEqual(100, deserialized[0].Age);
        Assert.AreEqual(true, deserialized[0].IsActive);
        Assert.AreEqual("Direct2", deserialized[1].Name);
        Assert.AreEqual(200, deserialized[1].Age);
        Assert.AreEqual(false, deserialized[1].IsActive);
    }
}