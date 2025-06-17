# CBOR Serialization Library

A .NET library that provides CBOR (Concise Binary Object Representation) serialization using source generation, similar to System.Text.Json's approach. The library is fully AOT-compatible and leverages System.Formats.Cbor for the underlying CBOR operations.

## Features

- ✅ Source generation for AOT compatibility
- ✅ No runtime reflection required  
- ✅ Similar API to System.Text.Json
- ✅ Support for .NET primitive types and collections
- ✅ Efficient binary serialization
- ✅ Native AOT support
- ✅ Trimming-friendly
- ✅ Comprehensive test suite (47 tests)
- ✅ Nullable type support
- ✅ Custom attributes (CborPropertyName, CborIgnore, CborDefaultValue)

## Project Structure

```
├── CborSerialization/              # Main library project
│   ├── Attributes/                 # Serialization attributes
│   ├── CborSerializer.cs           # Static serialization API
│   ├── CborSerializerContext.cs    # Base context class
│   └── CborTypeInfo.cs             # Type metadata container
│
├── CborSerialization.Generator/    # Source generator project
│   ├── CborSourceGenerator.cs      # Incremental source generator
│   ├── CborSyntaxReceiver.cs       # Syntax analysis
│   └── SerializationCodeGenerator.cs # Code generation logic
│
├── CborSerialization.Tests/       # Comprehensive test suite (47 tests)
│   ├── CborSerializerTests.cs      # Core serialization tests
│   ├── CborSerializerErrorTests.cs # Error handling tests
│   ├── AttributeTests.cs           # Attribute functionality tests
│   ├── SourceGeneratorTests.cs     # Generator validation tests
│   └── TestModels.cs               # Test model definitions
│
├── CborSerialization.Demo/        # Working demo project
│   ├── Program.cs                  # Demonstrates serialization
│   ├── Domain.cs                   # Domain model definitions
│   └── MyCborContext.cs            # Context implementation
│
└── CborSample/                    # Reference implementation
    └── Person.Cbor.g.cs            # Manual implementation example
```

## Requirements

- .NET 10 (preview) or .NET 8+ 
- System.Formats.Cbor package (10.0.0-preview.5.25277.114 for .NET 10)

## Installation

```bash
# Add the main package
dotnet add package CborSerialization

# Add the source generator package
dotnet add package CborSerialization.Generator
```

## Quick Start

1. Define your model:

```csharp
[CborSerializable]
public class Person
{
    [CborPropertyName("full_name")]
    public string Name { get; set; }
    
    public int Age { get; set; }
    
    [CborIgnore]
    public string InternalId { get; set; }
}
```

2. Create a context:

```csharp
[CborSerializable(typeof(Person))]
[CborSourceGenerationOptions(
    PropertyNamingPolicy = CborKnownNamingPolicy.CamelCase
)]
public partial class MyCborContext : CborSerializerContext
{
}
```

3. Serialize/Deserialize:

```csharp
// Serialization
byte[] cborData = CborSerializer.Serialize(person, MyCborContext.Default.Person);

// Deserialization
Person? person = CborSerializer.Deserialize<Person>(cborData, MyCborContext.Default.Person);
```

## Current Status

- ✅ **Source generator successfully generates optimized serialization/deserialization code** for marked types
- ✅ **Full primitive type support** (string, int, bool, double, float, byte, sbyte, short, ushort, uint, ulong, long)
- ✅ **Collection support** (List<T>, List<string>) with optimized built-in type handling
- ✅ **Nullable type support** with auto-generated helper methods
- ✅ **Custom attributes support** (CborPropertyName, CborIgnore, CborDefaultValue)
- ✅ **AOT compatibility** achieved by avoiding runtime reflection
- ✅ **Comprehensive test suite** with 47 passing tests covering all functionality
- ✅ **Enhanced error handling** with try-catch blocks and detailed error messages
- ✅ **Demo project** demonstrates serialization and deserialization of objects and collections

## AOT Support

The library is designed to work with Native AOT. To use it in an AOT project:

1. Add the source generator package
2. Create a context class with `[CborSerializable]` attributes
3. Use the generated serialization methods

## Type Support

### Currently Supported ✅
- **Primitives**: string, int, bool, double, float, byte, sbyte, short, ushort, uint, ulong, long
- **Collections**: List<T>, List<string> with optimized built-in type handling
- **Custom classes and structs** with source generation
- **Nullable types** (int?, bool?, etc.) with auto-generated helper methods
- **Nested objects** and complex type hierarchies
- **Custom attributes**: CborPropertyName, CborIgnore, CborDefaultValue

### Planned for Future Releases
- Dictionary<K,V> and arrays
- Enums (numeric + string options)
- DateTime/DateTimeOffset (ISO 8601)
- Guid
- Decimal (high precision)
- Large binary data (byte[])

## Error Handling

The library provides comprehensive error handling:
- ✅ **Enhanced CborSerializer** with try-catch blocks and detailed error messages
- ✅ **Parameter validation** with ArgumentNullException for null inputs
- ✅ **CBOR format validation** with descriptive error messages
- ✅ **Comprehensive error test suite** (8 tests) covering edge cases and invalid inputs

### Future Exception Types (Planned)
- `CborSerializationException`
- `CborDeserializationException`
- `CborValidationException`

## Contributing

We welcome contributions! Please follow these steps:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Run tests
5. Submit a pull request

## Building from Source

```bash
# Clone the repository
git clone https://github.com/yourusername/cbor.git

# Build the solution (.NET 10 required)
dotnet build

# Run the comprehensive test suite (47 tests)
dotnet test CborSerialization.Tests/

# Run the demo
dotnet run --project CborSerialization.Demo/
```

## Test Results

The library includes a comprehensive test suite with **47 tests, 0 failures**:

- **CborSerializerTests** (11 tests): Core serialization functionality
- **CborSerializerErrorTests** (8 tests): Error handling and edge cases  
- **AttributeTests** (12 tests): Attribute functionality validation
- **SourceGeneratorTests** (16 tests): Generated code validation

All tests demonstrate the library's reliability and production readiness.

## License

This project is licensed under the terms of the LICENSE file.

## Roadmap

See the [specification document](cbor_spec_enhanced.md) for planned features and future enhancements.

### Future Enhancements

#### Testing Infrastructure
- Comprehensive test project structure
- Test categories (unit, integration, AOT)
- Test coverage requirements
- Automated test scenarios

#### CI/CD Pipeline
- Automated build pipeline
- Test automation
- Package release process
- Quality gates

#### Package Management
- Versioning strategy
- Package release process
- Version compatibility rules
- Dependency management

#### Performance Optimization
- Performance benchmark suite
- Baseline performance metrics
- Benchmark scenarios
- Performance requirements
- Optimization guidelines

#### Documentation
- API documentation generation
- Performance benchmark results
- Migration guides
- Best practices guide
