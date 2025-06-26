namespace NCbor.Tests;

public class SourceGeneratorTests
{
    [Fact]
    public void TestNCborContext_Default_IsNotNull()
    {
        // Act
        var context = TestNCborContext.Default;

        // Assert
        context.Should().NotBeNull();
        context.Should().BeOfType<TestNCborContext>();
    }

    [Fact]
    public void TestNCborContext_HasExpectedTypeInfoProperties()
    {
        // Arrange
        var context = TestNCborContext.Default;
        var contextType = typeof(TestNCborContext);

        // Act & Assert
        var simpleModelProperty = contextType.GetProperty("SimpleModel");
        simpleModelProperty.Should().NotBeNull();
        simpleModelProperty!.PropertyType.Should().Be(typeof(NCborTypeInfo<SimpleModel>));

        var modelWithAttributesProperty = contextType.GetProperty("ModelWithAttributes");
        modelWithAttributesProperty.Should().NotBeNull();
        modelWithAttributesProperty!.PropertyType.Should().Be(typeof(NCborTypeInfo<ModelWithAttributes>));

        var listProperty = contextType.GetProperty("ListOfSimpleModel");
        listProperty.Should().NotBeNull();
        listProperty!.PropertyType.Should().Be(typeof(NCborTypeInfo<List<SimpleModel>>));
    }

    [Fact]
    public void TestNCborContext_TypeInfoProperties_ReturnValidInstances()
    {
        // Arrange
        var context = TestNCborContext.Default;

        // Act & Assert
        context.SimpleModel.Should().NotBeNull();
        context.ModelWithAttributes.Should().NotBeNull();
        context.ListOfSimpleModel.Should().NotBeNull();
        context.AllTypesModel.Should().NotBeNull();
    }

    [Fact]
    public void TestNCborContext_GetTypeInfo_ReturnsCorrectTypeInfo()
    {
        // Arrange
        var context = TestNCborContext.Default;

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
    public void TestNCborContext_GetTypeInfo_UnsupportedType_ThrowsException()
    {
        // Arrange
        var context = TestNCborContext.Default;

        // Act & Assert
        var act = context.GetTypeInfo<DateTime>; // DateTime not registered
        act.Should().Throw<ArgumentException>()
           .WithMessage("*Type System.DateTime is not registered for serialization*");
    }

    [Fact]
    public void GeneratedTypeInfo_Type_ReturnsCorrectType()
    {
        // Arrange
        var context = TestNCborContext.Default;

        // Act & Assert
        context.SimpleModel.Type.Should().Be(typeof(SimpleModel));
        context.ModelWithAttributes.Type.Should().Be(typeof(ModelWithAttributes));
        context.ListOfSimpleModel.Type.Should().Be(typeof(List<SimpleModel>));
    }

    [Fact]
    public void GeneratedCode_CanSerializeAndDeserialize()
    {
        // Arrange
        var context = TestNCborContext.Default;
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
        var context = TestNCborContext.Default;
        var model = new NestedModel
        {
            Title = "Complex Test",
            Details = new SimpleModel { Name = "Nested", Age = 25, IsActive = false },
            Tags = ["tag1", "tag2", "tag3"]
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
        var context1 = TestNCborContext.Default;
        var context2 = TestNCborContext.Default;

        // Assert
        context1.Should().BeSameAs(context2);
    }

    [Fact]
    public void GeneratedTypeInfo_IsReusable()
    {
        // Arrange
        var context = TestNCborContext.Default;
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

        var names = SerializeAndGetNames(model, context.SimpleModel);

        names.Should().Contain("is_active");
        names.Should().Contain("name");
        names.Should().Contain("age");
        names.Should().NotContain("IsActive");
    }

    [Fact]
    public void NamingPolicy_SnakeCaseUpper_UsesSnakeCaseUpperPropertyNames()
    {
        var context = SnakeCaseUpperContext.Default;
        var names = SerializeAndGetNames(new SimpleModel(), context.SimpleModel);
        names.Should().Contain("IS_ACTIVE");
    }

    [Fact]
    public void NamingPolicy_KebabCaseLower_UsesKebabCaseLowerNames()
    {
        var context = KebabCaseLowerContext.Default;
        var names = SerializeAndGetNames(new SimpleModel(), context.SimpleModel);
        names.Should().Contain("is-active");
    }

    [Fact]
    public void NamingPolicy_KebabCaseUpper_UsesKebabCaseUpperNames()
    {
        var context = KebabCaseUpperContext.Default;
        var names = SerializeAndGetNames(new SimpleModel(), context.SimpleModel);
        names.Should().Contain("IS-ACTIVE");
    }

    [Fact]
    public void NamingPolicy_UpperCase_UsesUpperCaseNames()
    {
        var context = UpperCaseContext.Default;
        var names = SerializeAndGetNames(new SimpleModel(), context.SimpleModel);
        names.Should().Contain("ISACTIVE");
    }

    [Fact]
    public void NamingPolicy_LowerCase_UsesLowerCaseNames()
    {
        var context = LowerCaseContext.Default;
        var names = SerializeAndGetNames(new SimpleModel(), context.SimpleModel);
        names.Should().Contain("isactive");
    }

    private static List<string> SerializeAndGetNames(SimpleModel model, NCborTypeInfo<SimpleModel> typeInfo)
    {
        var data = NCborSerializer.Serialize(model, typeInfo);
        var reader = new System.Formats.Cbor.CborReader(data);
        reader.ReadStartMap();
        var names = new List<string>();
        while (reader.PeekState() != System.Formats.Cbor.CborReaderState.EndMap)
        {
            names.Add(reader.ReadTextString());
            reader.SkipValue();
        }
        reader.ReadEndMap();
        return names;
    }
}

