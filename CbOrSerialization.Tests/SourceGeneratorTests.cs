namespace CbOrSerialization.Tests;

public class SourceGeneratorTests
{
    [Fact]
    public void TestCbOrContext_Default_IsNotNull()
    {
        // Act
        var context = TestCbOrContext.Default;

        // Assert
        context.Should().NotBeNull();
        context.Should().BeOfType<TestCbOrContext>();
    }

    [Fact]
    public void TestCbOrContext_HasExpectedTypeInfoProperties()
    {
        // Arrange
        var context = TestCbOrContext.Default;
        var contextType = typeof(TestCbOrContext);

        // Act & Assert
        var simpleModelProperty = contextType.GetProperty("SimpleModel");
        simpleModelProperty.Should().NotBeNull();
        simpleModelProperty!.PropertyType.Should().Be(typeof(CbOrTypeInfo<SimpleModel>));

        var modelWithAttributesProperty = contextType.GetProperty("ModelWithAttributes");
        modelWithAttributesProperty.Should().NotBeNull();
        modelWithAttributesProperty!.PropertyType.Should().Be(typeof(CbOrTypeInfo<ModelWithAttributes>));

        var listProperty = contextType.GetProperty("ListOfSimpleModel");
        listProperty.Should().NotBeNull();
        listProperty!.PropertyType.Should().Be(typeof(CbOrTypeInfo<List<SimpleModel>>));
    }

    [Fact]
    public void TestCbOrContext_TypeInfoProperties_ReturnValidInstances()
    {
        // Arrange
        var context = TestCbOrContext.Default;

        // Act & Assert
        context.SimpleModel.Should().NotBeNull();
        context.ModelWithAttributes.Should().NotBeNull();
        context.ListOfSimpleModel.Should().NotBeNull();
        context.AllTypesModel.Should().NotBeNull();
    }

    [Fact]
    public void TestCbOrContext_GetTypeInfo_ReturnsCorrectTypeInfo()
    {
        // Arrange
        var context = TestCbOrContext.Default;

        // Act
        var simpleModelTypeInfo = context.GetTypeInfo<SimpleModel>();
        var listTypeInfo = context.GetTypeInfo<List<SimpleModel>>();

        // Assert
        simpleModelTypeInfo.Should().NotBeNull();
        simpleModelTypeInfo.Should().BeSameAs(context.SimpleModel);
        
        listTypeInfo.Should().NotBeNull();
        listTypeInfo.Should().BeSameAs(context.ListOfSimpleModel);
    }

    [Fact]
    public void TestCbOrContext_GetTypeInfo_UnsupportedType_ThrowsException()
    {
        // Arrange
        var context = TestCbOrContext.Default;

        // Act & Assert
        var act = context.GetTypeInfo<DateTime>; // DateTime not registered
        act.Should().Throw<ArgumentException>()
           .WithMessage("*Type System.DateTime is not registered for serialization*");
    }

    [Fact]
    public void GeneratedTypeInfo_Type_ReturnsCorrectType()
    {
        // Arrange
        var context = TestCbOrContext.Default;

        // Act & Assert
        context.SimpleModel.Type.Should().Be(typeof(SimpleModel));
        context.ModelWithAttributes.Type.Should().Be(typeof(ModelWithAttributes));
        context.ListOfSimpleModel.Type.Should().Be(typeof(List<SimpleModel>));
    }

    [Fact]
    public void GeneratedCode_CanSerializeAndDeserialize()
    {
        // Arrange
        var context = TestCbOrContext.Default;
        var model = new SimpleModel { Name = "Generated Test", Age = 99, IsActive = true };

        // Act
        var typeInfo = context.SimpleModel;
        var writer = new System.Formats.Cbor.CborWriter();
        typeInfo.Serialize(writer, model);
        var data = writer.Encode();

        var reader = new System.Formats.Cbor.CborReader(data);
        var deserialized = typeInfo.Deserialize(reader);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Name.Should().Be(model.Name);
        deserialized.Age.Should().Be(model.Age);
        deserialized.IsActive.Should().Be(model.IsActive);
    }

    [Fact]
    public void GeneratedCode_HandlesComplexTypes()
    {
        // Arrange
        var context = TestCbOrContext.Default;
        var model = new NestedModel
        {
            Title = "Complex Test",
            Details = new SimpleModel { Name = "Nested", Age = 25, IsActive = false },
            Tags = new List<string> { "tag1", "tag2", "tag3" }
        };

        // Act - This tests that the generator can handle complex nested types
        var act = () =>
        {
            var typeInfo = context.NestedModel;
            var writer = new System.Formats.Cbor.CborWriter();
            typeInfo.Serialize(writer, model);
            return writer.Encode();
        };

        // Assert - Should not throw for complex types that are properly registered
        act.Should().NotThrow();
    }

    [Fact]
    public void GeneratedContext_IsSingleton()
    {
        // Act
        var context1 = TestCbOrContext.Default;
        var context2 = TestCbOrContext.Default;

        // Assert
        context1.Should().BeSameAs(context2);
    }

    [Fact]
    public void GeneratedTypeInfo_IsReusable()
    {
        // Arrange
        var context = TestCbOrContext.Default;
        var model1 = new SimpleModel { Name = "First", Age = 1, IsActive = true };
        var model2 = new SimpleModel { Name = "Second", Age = 2, IsActive = false };

        // Act
        var typeInfo = context.SimpleModel;
        
        var writer1 = new System.Formats.Cbor.CborWriter();
        typeInfo.Serialize(writer1, model1);
        var data1 = writer1.Encode();
        
        var writer2 = new System.Formats.Cbor.CborWriter();
        typeInfo.Serialize(writer2, model2);
        var data2 = writer2.Encode();

        // Assert
        data1.Should().NotBeNull();
        data2.Should().NotBeNull();
        data1.Should().NotEqual(data2); // Different models should produce different data

        var reader1 = new System.Formats.Cbor.CborReader(data1);
        var deserialized1 = typeInfo.Deserialize(reader1);
        deserialized1.Name.Should().Be("First");

        var reader2 = new System.Formats.Cbor.CborReader(data2);
        var deserialized2 = typeInfo.Deserialize(reader2);
        deserialized2.Name.Should().Be("Second");
    }

    [Fact]
    public void NamingPolicy_SnakeCaseLower_UsesSnakeCasePropertyNames()
    {
        var context = SnakeCaseContext.Default;
        var model = new SimpleModel { Name = "CaseTest", Age = 5, IsActive = true };

        var data = CbOrSerializer.Serialize(model, context.SimpleModel);
        var reader = new System.Formats.Cbor.CborReader(data);
        reader.ReadStartMap();
        var names = new List<string>();
        while (reader.PeekState() != System.Formats.Cbor.CborReaderState.EndMap)
        {
            names.Add(reader.ReadTextString());
            reader.SkipValue();
        }
        reader.ReadEndMap();

        names.Should().Contain("is_active");
        names.Should().Contain("name");
        names.Should().Contain("age");
        names.Should().NotContain("IsActive");
    }
}

