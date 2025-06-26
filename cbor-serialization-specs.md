# NCbor Serialization Library Specification

## Project Overview
Create a .NET library that provides CBOR (Concise Binary Object Representation) serialization using source generation, similar to System.Text.Json's approach with JsonSerializerContext. The library should be fully AOT-compatible and leverage System.Formats.Cbor for the underlying CBOR operations.

## Objective  
Create a CBOR serialization library that mirrors the approach of `System.Text.Json`, leveraging source generators for efficient, reflection-free serialization. This specification serves as comprehensive documentation for generating code, ensuring compatibility with AOT compilation and high performance.

---

## 1. Core Architecture

### 1.1 Architecture Pattern
Follow System.Text.Json's source generation pattern:
- Create `NCborSerializerContext` base class (equivalent to JsonSerializerContext)
- Implement `NCborSerializable` attribute (equivalent to JsonSerializable)
- Generate `NCborTypeInfo<T>` metadata (equivalent to JsonTypeInfo<T>)
- Provide `NCborSerializer` static class (equivalent to JsonSerializer)

### 1.2 Key Components
- **Source Generator**: A custom source generator that inspects user code, identifies types marked with `[NCborSerializable]`, and generates serialization code.  
- **CBOR Formatter**: Use `System.Formats.Cbor` for low-level CBOR encoding and decoding.  
- **Context Class**: Introduce a `NCborSerializerContext` to manage serialization metadata, similar to `JsonSerializerContext`.

---

## 2. Key Features  
- **Source Generation**: Generate serialization and deserialization code at compile time.  
- **Attribute-Based Configuration**: Use attributes to specify serialization behavior (e.g., `[NCborSerializable]`, `[NCborIgnore]`).  
- **AOT Compatibility**: Ensure the generated code avoids reflection, making it suitable for AOT scenarios.  
- **Performance**: Optimize for speed and memory efficiency, leveraging CBOR's compact binary format.
- **Zero-allocation paths**: Where possible, minimize memory allocations
- **Span<byte> and Memory<byte> support**: For efficient buffer management

---

## 3. API Design

### 3.1 Context Definition
```csharp
[NCborSerializable(typeof(Person))]
[NCborSerializable(typeof(List<Person>))]
[NCborSourceGenerationOptions(
    PropertyNamingPolicy = NCborKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = NCborIgnoreCondition.WhenWritingNull
)]
public partial class MyNCborContext : NCborSerializerContext
{
}

// Example model with annotations
public class Person
{
    [NCborPropertyName("full_name")]
    public string Name { get; set; }
    
    public int Age { get; set; }
    
    [NCborIgnore]
    public string InternalId { get; set; }
    
    [NCborDefaultValue(true)]
    public bool IsActive { get; set; } = true;
    
    public string Email { get; set; }
    
    [NCborConverter(typeof(CustomDateTimeConverter))]
    public DateTime CreatedAt { get; set; }
}

// Constructor-based deserialization
public class ImmutablePerson
{
    [NCborConstructor]
    public ImmutablePerson(
        [NCborPropertyName("full_name")] string name,
        int age)
    {
        Name = name;
        Age = age;
    }
    
    public string Name { get; }
    public int Age { get; }
}
```

### 3.2 Usage Pattern
```csharp
// Serialization
byte[] cborData = NCborSerializer.Serialize(person, MyNCborContext.Default.Person);

// Deserialization  
Person? person = NCborSerializer.Deserialize<Person>(cborData, MyNCborContext.Default.Person);
```

---

## 4. Core Components

### 4.1 Base Classes
- `NCborSerializerContext` - Base context class
- `NCborTypeInfo<T>` - Type metadata container
- `NCborSerializer` - Static serialization methods

### 4.2 Attribute System

#### Core Attributes

**NCborSerializableAttribute**:
```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class NCborSerializableAttribute : Attribute
{
    public NCborSerializableAttribute(Type type) => Type = type;
    public Type Type { get; }
}
```

**NCborIgnoreAttribute**:
```csharp
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class NCborIgnoreAttribute : Attribute
{
    public NCborIgnoreCondition Condition { get; set; } = NCborIgnoreCondition.Always;
}

public enum NCborIgnoreCondition
{
    Always,
    Never,
    WhenWritingDefault,
    WhenWritingNull
}
```

**NCborPropertyNameAttribute**:
```csharp
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NCborPropertyNameAttribute : Attribute
{
    public NCborPropertyNameAttribute(string name) => Name = name;
    public string Name { get; }
}
```

**NCborSourceGenerationOptionsAttribute**:
```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class NCborSourceGenerationOptionsAttribute : Attribute
{
    public NCborKnownNamingPolicy PropertyNamingPolicy { get; set; }
    public NCborIgnoreCondition DefaultIgnoreCondition { get; set; }
    public int MaxDepth { get; set; } = 64;
}
```

**NCborConverterAttribute**:
```csharp
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class NCborConverterAttribute : Attribute
{
    public NCborConverterAttribute(Type converterType) => ConverterType = converterType;
    public Type ConverterType { get; }
}
```

**NCborDefaultValueAttribute**:
```csharp
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class NCborDefaultValueAttribute : Attribute
{
    public NCborDefaultValueAttribute(object? value) => Value = value;
    public object? Value { get; }
}
```

**NCborConstructorAttribute**:
```csharp
[AttributeUsage(AttributeTargets.Constructor)]
public sealed class NCborConstructorAttribute : Attribute
{
}
```

#### Supporting Enums

**NCborKnownNamingPolicy**:
```csharp
public enum NCborKnownNamingPolicy
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

### 4.3 Generation Options
- Property naming policies (CamelCase, SnakeCase, KebabCase, UpperCase, LowerCase)
- Include/exclude fields vs properties
- Null handling strategies (Include, Ignore, Never)
- Custom converters registration
- Default value handling (Include, IgnoreWhenWriting, Always)
- Case sensitivity for deserialization
- Circular reference detection and handling

---

## 5. Source Generator Requirements

### 5.1 Input Analysis
- **Input**: Analyze types marked with `[NCborSerializable]` or specified in `[NCborSerializable(typeof(T))]`.  
- **Output**: Generate partial classes or extension methods for serialization and deserialization.  

### 5.2 Code Generation
- Generate code for properties and fields, respecting attributes like `[NCborIgnore]`.  
- Support collections, nested types, and custom converters.  
- Ensure generated code is AOT-friendly (no reflection).
- Handle partial classes and nested types
- Support generic type constraints
- Implement efficient type metadata generation
- Generate strongly-typed serialization methods
- Handle circular references with depth tracking
- Support inheritance hierarchies
- Generate appropriate null checks and validation

### 5.3 Generated Code Structure
The source generator should produce:
```csharp
public partial class MyNCborContext : NCborSerializerContext
{
    public static MyNCborContext Default { get; }
    public NCborTypeInfo<Person> Person { get; }
    
    // Generated serialization methods
    private static void SerializePerson(NCborWriter writer, Person value) { }
    private static Person DeserializePerson(ref NCborReader reader) { }
}
```

---

## 6. System.Formats.Cbor Integration

Utilize System.Formats.Cbor classes:
- `CborWriter` for serialization
- `CborReader` for deserialization
- Proper CBOR type mapping (integers, strings, arrays, maps, etc.)
- Handle CBOR-specific features (indefinite length, tags, etc.)

---

## 7. Type System Support

### 7.1 Supported Types
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

### 7.2 Type Serialization Details

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

#### Binary Data
- Efficient handling of large byte arrays
- Support for chunked serialization
- Memory-efficient streaming for large binary data

---

## 8. Serialization API  
- **`NCborSerializer.Serialize<T>(T value, NCborTypeInfo<T> typeInfo)`**: Serializes an object to CBOR.  
- **`NCborSerializer.Deserialize<T>(byte[] cborData, NCborTypeInfo<T> typeInfo)`**: Deserializes CBOR data to an object.  
- **Options**: Support custom options (e.g., `NCborSerializerOptions`) for naming policies, etc.

---

## 9. Performance Optimizations
- Zero-allocation paths where possible
- Span<byte> and Memory<byte> support
- Use of standard .NET pooling mechanisms
- Minimal boxing/unboxing
- Inlined hot paths
- Serialization performance within 10% of hand-written code

---

## 10. AOT Considerations  
- **No Reflection**: Resolve all type information at compile time.  
- **Static Code Generation**: Generate static methods for serialization and deserialization.  
- **Compatibility**: Ensure the generated code works with AOT's `IlcDisableReflection` option.
- Support trimming-friendly patterns
- Full AOT compatibility

---

## 11. Error Handling  
- **Compile-Time Errors**: Report errors for unsupported types or misconfigurations.  
- **Runtime Errors**: Handle deserialization errors gracefully (e.g., missing properties, type mismatches).
- Core exception types:
  - `NCborException` - For serialization failures
  - `NCborDeserializationException` - For deserialization failures
  - `NCborValidationException` - For data validation failures
- Basic validation of CBOR data integrity
- Graceful handling of unknown properties

---

## 12. Advanced Features
- Custom converter interface (`INCborConverter<T>`)
- Circular reference detection
- Binary data optimization
- Versioning support through attributes
- Backward and forward compatibility handling

---

## 13. Technical Constraints
- Target netstandard2.0
- Native AOT support required
- No runtime reflection allowed
- Minimize dependencies (only System.Formats.Cbor)
- Follow .NET naming conventions
- Support trimming-friendly patterns
- Generic type support where possible

---

## 14. Testing Requirements
- **Unit Tests**: Verify serialization and deserialization for various types (primitives, collections, nested objects).  
- **AOT Tests**: Ensure functionality in AOT-compiled applications.  
- **Performance Benchmarks**: Compare with reflection-based serialization.
- Basic unit test suite including:
  - AOT compilation scenarios
  - Performance benchmarks
  - Edge cases
  - CBOR compliance tests

---

## 15. Documentation Requirements
- **Usage Guide**: Provide examples of attribute usage and serialization calls.  
- **API Reference**: Document all public APIs, including generated methods.  
- **AOT Instructions**: Explain how to configure AOT compilation with the library.
- API reference documentation in markdown format
- XML documentation comments for all public APIs
- Code examples for common scenarios
- Migration guide from reflection-based serialization
- CBOR-specific considerations

---

## 16. Deliverables
1. Source generator project
2. Runtime library with base classes
3. Attribute definitions
4. Sample usage project
5. Basic unit test suite
6. Documentation and examples

---

## 17. Future Roadmap
Features to be implemented in subsequent versions:
1. Polymorphic serialization support
2. Advanced CBOR tags support
3. Streaming serialization for large objects
4. Advanced error handling and validation
5. Extended type system coverage
6. F# language support
7. Advanced documentation and examples

---

## Conclusion  
This specification outlines a comprehensive CBOR serialization library that leverages source generators for efficient, AOT-compatible serialization, mirroring the approach of `System.Text.Json`. By following these guidelines, the generated code ensures high performance, reflection-free operation, and seamless integration into .NET projects with full AOT support and API similarity to System.Text.Json experience.

