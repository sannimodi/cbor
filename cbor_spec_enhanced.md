# Create AOT-Compatible CBOR Serialization Library with Source Generation

## Project Overview
Create a .NET library that provides CBOR (Concise Binary Object Representation) serialization using source generation, similar to System.Text.Json's approach with JsonSerializerContext. The library should be fully AOT-compatible and leverage System.Formats.Cbor for the underlying CBOR operations.

## Core Requirements

### 1. Architecture Pattern
Follow System.Text.Json's source generation pattern:
- Create `CborSerializerContext` base class (equivalent to JsonSerializerContext)
- Implement `CborSerializable` attribute (equivalent to JsonSerializable)
- Generate `CborTypeInfo<T>` metadata (equivalent to JsonTypeInfo<T>)
- Provide `CborSerializer` static class (equivalent to JsonSerializer)

### 2. Source Generator Implementation
Create an incremental source generator that:
- Analyzes types marked with `[CborSerializable(typeof(T))]`
- Generates optimized serialization/deserialization methods
- Creates type metadata without reflection
- Supports AOT compilation scenarios

### 3. API Design

#### Context Definition:
```csharp
[CborSerializable(typeof(Person))]
[CborSerializable(typeof(List<Person>))]
[CborSourceGenerationOptions(
    PropertyNamingPolicy = CborKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = CborIgnoreCondition.WhenWritingNull
)]

public partial class MyCborContext : CborSerializerContext
{
}

// Example model with annotations
public class Person
{
    [CborPropertyName("full_name")]
    public string Name { get; set; }
    
    public int Age { get; set; }
    
    [CborIgnore]
    public string InternalId { get; set; }
    
    [CborDefaultValue(true)]
    public bool IsActive { get; set; } = true;
    
    public string Email { get; set; }
    
    [CborConverter(typeof(CustomDateTimeConverter))]
    public DateTime CreatedAt { get; set; }
}

// Constructor-based deserialization
public class ImmutablePerson
{
    [CborConstructor]
    public ImmutablePerson(
        [CborPropertyName("full_name")] string name,
        int age)
    {
        Name = name;
        Age = age;
    }
    
    public string Name { get; }
    public int Age { get; }
}
```

#### Usage Pattern:
```csharp
// Serialization
byte[] cborData = CborSerializer.Serialize(person, MyCborContext.Default.Person);

// Deserialization  
Person? person = CborSerializer.Deserialize<Person>(cborData, MyCborContext.Default.Person);
```

### 4. Core Components to Implement

#### Base Classes:
- `CborSerializerContext` - Base context class
- `CborTypeInfo<T>` - Type metadata container
- `CborSerializer` - Static serialization methods

#### Attributes:
- `CborSerializableAttribute` - Mark types for generation
- `CborSourceGenerationOptionsAttribute` - Configure generation options
- `CborPropertyNameAttribute` - Custom property naming
- `CborIgnoreAttribute` - Exclude properties
- `CborConverterAttribute` - Apply custom converter to property/type
- `CborDefaultValueAttribute` - Specify default values
- `CborConstructorAttribute` - Mark constructor for deserialization

#### Generation Options:
- Property naming policies (CamelCase, SnakeCase, KebabCase, UpperCase, LowerCase)
- Include/exclude fields vs properties
- Null handling strategies (Include, Ignore, Never)
- Custom converters registration
- Default value handling (Include, IgnoreWhenWriting, Always)
- Case sensitivity for deserialization
- Allow trailing commas
- Comment handling options
- Number handling (Strict, AllowReadingFromString, AllowNamedFloatingPointLiterals)
- Circular reference detection and handling

### 5. System.Formats.Cbor Integration

Utilize System.Formats.Cbor classes:
- `CborWriter` for serialization
- `CborReader` for deserialization
- Proper CBOR type mapping (integers, strings, arrays, maps, etc.)
- Handle CBOR-specific features (indefinite length, tags, etc.)

### 6. Generated Code Structure

The source generator should produce:
```csharp
public partial class MyCborContext : CborSerializerContext
{
    public static MyCborContext Default { get; }
    public CborTypeInfo<Person> Person { get; }
    
    // Generated serialization methods
    private static void SerializePerson(CborWriter writer, Person value) { }
    private static Person DeserializePerson(ref CborReader reader) { }
}
```

### 7. Type Support
Support common .NET types:
- Primitives (int, string, bool, etc.)
- Collections (List<T>, Dictionary<K,V>, arrays)
- Custom classes and structs
- Nullable types
- Enums (serialized as numbers by default, with option to serialize as strings)
- DateTime/DateTimeOffset (ISO 8601 format)
- Guid
- Decimal (preserve full precision)
- Nested generic types (e.g., List<Dictionary<string, T>>)
- Large binary data (byte arrays with efficient chunking)

### Type Serialization Details

#### DateTime Handling
- DateTime values are serialized as ISO 8601 strings
- DateTimeOffset includes timezone information
- Support for UTC and local time conversions
- Preserve DateTimeKind information

#### Enum Handling
- Default serialization as underlying numeric values
- Optional string serialization via attribute
- Support for [Flags] enums
- Handle invalid enum values gracefully

#### Decimal and Floating-Point
- Decimal: Preserve full precision (28-29 significant digits)
- Double/Float: Use standard IEEE 754 representation
- Handle special values (NaN, Infinity) appropriately
- Support for custom precision via attributes

#### Binary Data
- Efficient handling of large byte arrays
- Support for chunked serialization
- Memory-efficient streaming for large binary data
- Base64 encoding option for string representation

#### Versioning Support
- Type versioning through attributes
- Backward compatibility handling
- Forward compatibility strategies
- Version mismatch detection and handling

#### Nested Generic Types
- Support for complex generic type hierarchies
- Handle type constraints appropriately
- Support for generic type parameters
- Proper type resolution for nested generics

### 8. Advanced Features
- Custom converter interface (`ICborConverter<T>`)
- Circular reference detection
- Binary data optimization

### 9. Performance Optimizations
- Zero-allocation paths where possible
- Span<byte> and Memory<byte> support
- Use of standard .NET pooling mechanisms
- Minimal boxing/unboxing
- Inlined hot paths

### 10. Error Handling
- Basic error handling with descriptive messages
- Core exception types:
  - `CborSerializationException` - For serialization failures
  - `CborDeserializationException` - For deserialization failures
  - `CborValidationException` - For data validation failures
- Basic validation of CBOR data integrity
- Graceful handling of unknown properties

### 11. Source Generator Implementation
The source generator should:
- Analyze types marked with `[CborSerializable]` attribute
- Generate optimized serialization/deserialization code
- Handle partial classes and nested types
- Support generic type constraints
- Implement efficient type metadata generation
- Generate strongly-typed serialization methods
- Handle circular references with depth tracking
- Support inheritance hierarchies
- Generate appropriate null checks and validation
- Implement efficient buffer management
- Support custom converters and type converters
- Handle collection types (arrays, lists, dictionaries)
- Support nullable reference types
- Generate appropriate XML documentation comments

### 12. Documentation
Provide:
- API reference documentation in markdown format
- XML documentation comments for all public APIs
- Code examples for common scenarios
- Basic usage guide
- Migration guide from reflection-based serialization
- CBOR-specific considerations
- AOT deployment examples

## Technical Constraints
- Target .NET 8 and later versions
- Native AOT support required
- No runtime reflection allowed
- Minimize dependencies (only System.Formats.Cbor)
- Follow .NET naming conventions
- Support trimming-friendly patterns
- No F# support required
- Generic type support where possible

## Roadmap
Future features to be implemented in subsequent versions:
1. Polymorphic serialization support
2. Advanced CBOR tags support
3. Streaming serialization for large objects
4. Comprehensive test suite including:
   - AOT compilation scenarios
   - Performance benchmarks
   - Edge cases
   - CBOR compliance tests
5. Advanced error handling and validation
6. Extended type system coverage
7. Advanced buffer management strategies
8. Support for additional .NET versions
9. F# language support
10. Advanced documentation and examples

## Deliverables
1. Source generator project
2. Runtime library with base classes
3. Attribute definitions
4. Sample usage project
5. Basic unit test suite
6. Documentation and examples

### 13. Annotation System Details

#### Core Attributes Implementation

**CborIgnoreAttribute**:
```csharp
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class CborIgnoreAttribute : Attribute
{
    public CborIgnoreCondition Condition { get; set; } = CborIgnoreCondition.Always;
}

public enum CborIgnoreCondition
{
    Always,
    Never,
    WhenWritingDefault,
    WhenWritingNull
}
```

**CborPropertyNameAttribute**:
```csharp
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CborPropertyNameAttribute : Attribute
{
    public CborPropertyNameAttribute(string name) => Name = name;
    public string Name { get; }
}
```

**CborSourceGenerationOptionsAttribute**:
```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CborSourceGenerationOptionsAttribute : Attribute
{
    public CborKnownNamingPolicy PropertyNamingPolicy { get; set; }
    public CborIgnoreCondition DefaultIgnoreCondition { get; set; }
    public int MaxDepth { get; set; } = 64;
}
```

**CborConverterAttribute**:
```csharp
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class CborConverterAttribute : Attribute
{
    public CborConverterAttribute(Type converterType) => ConverterType = converterType;
    public Type ConverterType { get; }
}
```

**CborDefaultValueAttribute**:
```csharp
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class CborDefaultValueAttribute : Attribute
{
    public CborDefaultValueAttribute(object? value) => Value = value;
    public object? Value { get; }
}
```

**CborConstructorAttribute**:
```csharp
[AttributeUsage(AttributeTargets.Constructor)]
public sealed class CborConstructorAttribute : Attribute
{
}
```

#### Supporting Enums

**CborKnownNamingPolicy**:
```csharp
public enum CborKnownNamingPolicy
{
    Unspecified = 0,
    CamelCase = 1,
    SnakeCaseLower = 2,
    SnakeCaseUpper = 3,
    KebabCaseLower = 4,
    KebabCaseUpper = 5,
    UpperCase = 6,
    LowerCase = 7
}
```

- Serialization performance within 10% of hand-written code
- Full AOT compatibility
- API similarity to System.Text.Json experience
- Support for complex object graphs
- Comprehensive type system coverage