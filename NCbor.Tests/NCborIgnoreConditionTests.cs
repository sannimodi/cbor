namespace NCbor.Tests;

public class NCborIgnoreConditionTests
{
    private readonly TestNCborContext _context = TestNCborContext.Default;

    private static List<string> ReadPropertyNames(byte[] data)
    {
        var reader = new CborReader(data);
        reader.ReadStartMap();

        var propertyNames = new List<string>();
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            propertyNames.Add(reader.ReadTextString());
            reader.SkipValue();
        }

        return propertyNames;
    }

    [Fact]
    public void Never_PropertyPresent_IsAlwaysIncludedEvenAtDefaultValue()
    {
        var model = new IgnoreConditionModel { Name = "a", AlwaysIncluded = 0 };

        var data = NCborSerializer.Serialize(model, _context.IgnoreConditionModel);

        ReadPropertyNames(data).Should().Contain("AlwaysIncluded");
    }

    [Fact]
    public void WhenWritingNull_ValueIsNull_PropertyOmittedFromMap()
    {
        var model = new IgnoreConditionModel { Name = "a", OptionalNote = null };

        var data = NCborSerializer.Serialize(model, _context.IgnoreConditionModel);

        ReadPropertyNames(data).Should().NotContain("OptionalNote");
    }

    [Fact]
    public void WhenWritingNull_ValueIsNotNull_PropertyIncludedAndRoundTrips()
    {
        var model = new IgnoreConditionModel { Name = "a", OptionalNote = "hello" };

        var data = NCborSerializer.Serialize(model, _context.IgnoreConditionModel);
        var result = NCborSerializer.Deserialize(data, _context.IgnoreConditionModel);

        ReadPropertyNames(data).Should().Contain("OptionalNote");
        result.OptionalNote.Should().Be("hello");
    }

    [Fact]
    public void WhenWritingDefault_ValueIsDefault_PropertyOmittedFromMap()
    {
        var model = new IgnoreConditionModel { Name = "a", Count = 0 };

        var data = NCborSerializer.Serialize(model, _context.IgnoreConditionModel);
        var result = NCborSerializer.Deserialize(data, _context.IgnoreConditionModel);

        ReadPropertyNames(data).Should().NotContain("Count");
        result.Count.Should().Be(0);
    }

    [Fact]
    public void WhenWritingDefault_ValueIsNonDefault_PropertyIncludedAndRoundTrips()
    {
        var model = new IgnoreConditionModel { Name = "a", Count = 7 };

        var data = NCborSerializer.Serialize(model, _context.IgnoreConditionModel);
        var result = NCborSerializer.Deserialize(data, _context.IgnoreConditionModel);

        ReadPropertyNames(data).Should().Contain("Count");
        result.Count.Should().Be(7);
    }

    [Fact]
    public void NCborDefaultValue_ValueMatchesDeclaredDefault_PropertyOmittedFromMap()
    {
        var model = new ModelWithAttributes { Name = "a", Age = 1, IsEnabled = true };

        var data = NCborSerializer.Serialize(model, _context.ModelWithAttributes);

        ReadPropertyNames(data).Should().NotContain("IsEnabled");
    }

    [Fact]
    public void NCborDefaultValue_ValueDiffersFromDeclaredDefault_PropertyIncluded()
    {
        var model = new ModelWithAttributes { Name = "a", Age = 1, IsEnabled = false };

        var data = NCborSerializer.Serialize(model, _context.ModelWithAttributes);

        ReadPropertyNames(data).Should().Contain("IsEnabled");
    }

    [Fact]
    public void NCborDefaultValue_EnumTypedDefault_ValueMatchesDeclaredDefault_PropertyOmittedFromMap()
    {
        // Regression test: [NCborDefaultValue(UserRole.Admin)] previously generated
        // EqualityComparer<UserRole>.Default.Equals(value.Role, 3) — a bare numeric literal
        // with no cast, which fails to compile (CS0176) for any non-zero enum default. If this
        // model didn't compile, the whole test assembly would fail to build.
        var model = new EnumDefaultValueModel { Name = "a", Role = UserRole.Admin };

        var data = NCborSerializer.Serialize(model, _context.EnumDefaultValueModel);

        ReadPropertyNames(data).Should().NotContain("Role");
    }

    [Fact]
    public void NCborDefaultValue_EnumTypedDefault_ValueDiffersFromDeclaredDefault_PropertyIncludedAndRoundTrips()
    {
        var model = new EnumDefaultValueModel { Name = "a", Role = UserRole.SuperAdmin };

        var data = NCborSerializer.Serialize(model, _context.EnumDefaultValueModel);
        var result = NCborSerializer.Deserialize(data, _context.EnumDefaultValueModel);

        ReadPropertyNames(data).Should().Contain("Role");
        result.Role.Should().Be(UserRole.SuperAdmin);
    }

    [Fact]
    public void NCborDefaultValue_OnlyAttribute_ContextDefaultIgnoreConditionAlways_StillIncludedWhenNonDefault()
    {
        // Regression test: IsAlwaysIgnored previously had no awareness of [NCborDefaultValue].
        // A context with DefaultIgnoreCondition = Always would silently drop a property that
        // carries only [NCborDefaultValue] (no explicit [NCborIgnore]) unconditionally, even
        // when its value differed from the declared default — bypassing the attribute's whole
        // point. AlwaysIgnoreContext below sets exactly that context-wide default.
        var model = new AlwaysIgnoreDefaultValueModel { Count = 7 };

        var data = NCborSerializer.Serialize(model, AlwaysIgnoreContext.Default.AlwaysIgnoreDefaultValueModel);
        var result = NCborSerializer.Deserialize(data, AlwaysIgnoreContext.Default.AlwaysIgnoreDefaultValueModel);

        ReadPropertyNames(data).Should().Contain("Count");
        result.Count.Should().Be(7);
    }

    [Fact]
    public void NCborDefaultValue_OnlyAttribute_ContextDefaultIgnoreConditionAlways_OmittedWhenMatchesDefault()
    {
        var model = new AlwaysIgnoreDefaultValueModel { Count = 0 };

        var data = NCborSerializer.Serialize(model, AlwaysIgnoreContext.Default.AlwaysIgnoreDefaultValueModel);

        ReadPropertyNames(data).Should().NotContain("Count");
    }

    [Fact]
    public void NCborIgnore_UnattributedProperty_ContextDefaultIgnoreConditionAlways_IsDropped()
    {
        // Confirms the fix is scoped correctly: a property with NEITHER [NCborIgnore] NOR
        // [NCborDefaultValue] must still honor a context-wide DefaultIgnoreCondition = Always.
        var model = new AlwaysIgnoreDefaultValueModel { Count = 7, PlainProperty = "should vanish" };

        var data = NCborSerializer.Serialize(model, AlwaysIgnoreContext.Default.AlwaysIgnoreDefaultValueModel);

        ReadPropertyNames(data).Should().NotContain("PlainProperty");
    }
}
