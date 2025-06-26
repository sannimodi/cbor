namespace NCbor.Tests;

public class NCborExceptionTests
{
    private readonly TestNCborContext _context = TestNCborContext.Default;

    #region NCborException Tests

    [Fact]
    public void NCborException_DefaultConstructor_CreatesInstance()
    {
        // Act
        var exception = new NCborSerializationException();

        // Assert
        exception.Should().NotBeNull();
        exception.Message.Should().NotBeNullOrEmpty();
        exception.Type.Should().BeNull();
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void NCborException_MessageConstructor_SetsMessage()
    {
        // Arrange
        const string message = "Test serialization error";

        // Act
        var exception = new NCborSerializationException(message);

        // Assert
        exception.Message.Should().Be(message);
        exception.Type.Should().BeNull();
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void NCborException_MessageAndInnerExceptionConstructor_SetsProperties()
    {
        // Arrange
        const string message = "Test serialization error";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new NCborSerializationException(message, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.Type.Should().BeNull();
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    public void NCborException_TypeAndMessageConstructor_SetsTypeAndMessage()
    {
        // Arrange
        var type = typeof(SimpleModel);
        const string message = "Test error";

        // Act
        var exception = new NCborSerializationException(type, message);

        // Assert
        exception.Message.Should().Contain(type.Name);
        exception.Message.Should().Contain(message);
        exception.Type.Should().Be(type);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void NCborException_TypeMessageAndInnerExceptionConstructor_SetsAllProperties()
    {
        // Arrange
        var type = typeof(SimpleModel);
        const string message = "Test error";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new NCborSerializationException(type, message, innerException);

        // Assert
        exception.Message.Should().Contain(type.Name);
        exception.Message.Should().Contain(message);
        exception.Type.Should().Be(type);
        exception.InnerException.Should().Be(innerException);
    }

    #endregion

    #region NCborDeserializationException Tests

    [Fact]
    public void NCborDeserializationException_DefaultConstructor_CreatesInstance()
    {
        // Act
        var exception = new NCborDeserializationException();

        // Assert
        exception.Should().NotBeNull();
        exception.Message.Should().NotBeNullOrEmpty();
        exception.Type.Should().BeNull();
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void NCborDeserializationException_MessageConstructor_SetsMessage()
    {
        // Arrange
        const string message = "Test deserialization error";

        // Act
        var exception = new NCborDeserializationException(message);

        // Assert
        exception.Message.Should().Be(message);
        exception.Type.Should().BeNull();
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void NCborDeserializationException_TypeAndMessageConstructor_SetsTypeAndMessage()
    {
        // Arrange
        var type = typeof(SimpleModel);
        const string message = "Test error";

        // Act
        var exception = new NCborDeserializationException(type, message);

        // Assert
        exception.Message.Should().Contain(type.Name);
        exception.Message.Should().Contain(message);
        exception.Type.Should().Be(type);
        exception.InnerException.Should().BeNull();
    }

    #endregion

    #region NCborValidationException Tests

    [Fact]
    public void NCborValidationException_DefaultConstructor_CreatesInstance()
    {
        // Act
        var exception = new NCborValidationException();

        // Assert
        exception.Should().NotBeNull();
        exception.Message.Should().NotBeNullOrEmpty();
        exception.Type.Should().BeNull();
        exception.PropertyName.Should().BeNull();
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void NCborValidationException_MessageConstructor_SetsMessage()
    {
        // Arrange
        const string message = "Test validation error";

        // Act
        var exception = new NCborValidationException(message);

        // Assert
        exception.Message.Should().Be(message);
        exception.Type.Should().BeNull();
        exception.PropertyName.Should().BeNull();
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void NCborValidationException_PropertyNameAndMessageConstructor_SetsProperties()
    {
        // Arrange
        const string propertyName = "TestProperty";
        const string message = "Test error";

        // Act
        var exception = new NCborValidationException(propertyName, message);

        // Assert
        exception.Message.Should().Contain(propertyName);
        exception.Message.Should().Contain(message);
        exception.PropertyName.Should().Be(propertyName);
        exception.Type.Should().BeNull();
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void NCborValidationException_TypePropertyNameAndMessageConstructor_SetsAllProperties()
    {
        // Arrange
        var type = typeof(SimpleModel);
        const string propertyName = "Name";
        const string message = "Test error";

        // Act
        var exception = new NCborValidationException(type, propertyName, message);

        // Assert
        exception.Message.Should().Contain(type.Name);
        exception.Message.Should().Contain(propertyName);
        exception.Message.Should().Contain(message);
        exception.Type.Should().Be(type);
        exception.PropertyName.Should().Be(propertyName);
        exception.InnerException.Should().BeNull();
    }

    #endregion

    #region Integration Tests - Actual Exception Throwing

    [Fact]
    public void Serialize_WithNullTypeInfo_ThrowsArgumentNullException()
    {
        // Arrange
        var model = new SimpleModel { Name = "Test", Age = 30, IsActive = true };

        // Act & Assert
        var act = () => NCborSerializer.Serialize(model, null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("typeInfo");
    }

    [Fact]
    public void Deserialize_WithEmptyData_ThrowsNCborValidationException()
    {
        // Arrange
        var emptyData = Array.Empty<byte>();

        // Act & Assert
        var act = () => NCborSerializer.Deserialize(emptyData, _context.SimpleModel);
        act.Should().Throw<NCborValidationException>()
           .WithMessage("*CBOR data cannot be empty*");
    }

    [Fact]
    public void Deserialize_WithInvalidCborData_ThrowsNCborDeserializationException()
    {
        // Arrange - Invalid CBOR structure
        var invalidData = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };

        // Act & Assert
        var act = () => NCborSerializer.Deserialize(invalidData, _context.SimpleModel);
        act.Should().Throw<NCborDeserializationException>()
           .WithMessage("*Failed to deserialize to type 'SimpleModel'*");
    }


    [Fact]
    public void Deserialize_WithNullData_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => NCborSerializer.Deserialize(null!, _context.SimpleModel);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("data");
    }

    [Fact]
    public void Deserialize_WithNullTypeInfo_ThrowsArgumentNullException()
    {
        // Arrange
        var validData = new byte[] { 0xA0 }; // Empty CBOR map

        // Act & Assert
        var act = () => NCborSerializer.Deserialize<SimpleModel>(validData, null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("typeInfo");
    }

    [Fact]
    public void Serialize_ThenDeserialize_WithValidData_WorksCorrectly()
    {
        // Arrange
        var original = new SimpleModel { Name = "Test", Age = 30, IsActive = true };

        // Act
        var serialized = NCborSerializer.Serialize(original, _context.SimpleModel);
        var deserialized = NCborSerializer.Deserialize(serialized, _context.SimpleModel);

        // Assert
        deserialized.Should().BeEquivalentTo(original);
    }

    #endregion

    #region End-to-End Exception Verification Tests

    [Fact]
    public void ExceptionHandling_EndToEnd_AllExceptionTypesWorkCorrectly()
    {
        // Test 1: ArgumentNullException for null type info
        var model = new SimpleModel { Name = "Test", Age = 30, IsActive = true };
        var act1 = () => NCborSerializer.Serialize(model, null!);
        act1.Should().Throw<ArgumentNullException>().WithParameterName("typeInfo");

        // Test 2: NCborValidationException for empty data
        var emptyData = Array.Empty<byte>();
        var act2 = () => NCborSerializer.Deserialize(emptyData, _context.SimpleModel);
        act2.Should().Throw<NCborValidationException>().WithMessage("*CBOR data cannot be empty*");

        // Test 3: NCborDeserializationException for invalid CBOR structure
        var invalidData = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
        var act3 = () => NCborSerializer.Deserialize(invalidData, _context.SimpleModel);
        var exception = act3.Should().Throw<NCborDeserializationException>().Which;
        exception.Type.Should().Be(typeof(SimpleModel));
        exception.Message.Should().Contain("Failed to deserialize to type 'SimpleModel'");

        // Test 4: Valid round-trip should work normally (no exceptions)
        var validModel = new SimpleModel { Name = "Valid", Age = 25, IsActive = false };
        var serialized = NCborSerializer.Serialize(validModel, _context.SimpleModel);
        var deserialized = NCborSerializer.Deserialize(serialized, _context.SimpleModel);
        deserialized.Should().BeEquivalentTo(validModel);
    }

    [Fact] 
    public void CustomExceptions_ProvideContextualInformation()
    {
        // Verify NCborException includes type information
        var serializationEx = new NCborSerializationException(typeof(SimpleModel), "Test error");
        serializationEx.Type.Should().Be(typeof(SimpleModel));
        serializationEx.Message.Should().Contain("SimpleModel");
        serializationEx.Message.Should().Contain("Test error");

        // Verify NCborDeserializationException includes type information  
        var deserializationEx = new NCborDeserializationException(typeof(SimpleModel), "Deserialization failed");
        deserializationEx.Type.Should().Be(typeof(SimpleModel));
        deserializationEx.Message.Should().Contain("SimpleModel");
        deserializationEx.Message.Should().Contain("Deserialization failed");

        // Verify NCborValidationException includes property information
        var validationEx = new NCborValidationException(typeof(SimpleModel), "Name", "Invalid value");
        validationEx.Type.Should().Be(typeof(SimpleModel));
        validationEx.PropertyName.Should().Be("Name");
        validationEx.Message.Should().Contain("SimpleModel");
        validationEx.Message.Should().Contain("Name");
        validationEx.Message.Should().Contain("Invalid value");
    }

    #endregion
}