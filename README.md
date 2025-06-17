# CBOR Serialization Library

A .NET library that provides CBOR (Concise Binary Object Representation) serialization using source generation, similar to System.Text.Json's approach. The library is fully AOT-compatible and leverages System.Formats.Cbor for the underlying CBOR operations.

## Features

- Source generation for AOT compatibility
- No runtime reflection required
- Similar API to System.Text.Json
- Full support for .NET types
- Efficient binary serialization
- Native AOT support
- Trimming-friendly

## Project Structure

```
├── CborSerialization/              # Main library project
│   ├── Attributes/                 # Serialization attributes
│   ├── Core/                      # Core interfaces and base classes
│   └── Runtime/                   # Runtime components
│
├── CborSerialization.Generator/    # Source generator project
│   ├── Analyzers/                 # Type analyzers
│   └── Generators/                # Code generators
│
├── CborSerialization.Demo/        # Demo project
│   ├── Examples/                  # Usage examples
│   └── AOT/                      # AOT-specific examples
│
└── CborSample/                    # Reference implementation (unchanged)
```

## Requirements

- .NET 8 or later
- System.Formats.Cbor package

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

- ✅ Source generator successfully generates optimized serialization/deserialization code for marked types.
- ✅ Support for basic types (primitives, custom classes) and collections (e.g., `List<Person>`) is working.
- ✅ AOT compatibility is achieved by avoiding runtime reflection.
- ✅ Demo project demonstrates serialization and deserialization of both single objects and collections.

## AOT Support

The library is designed to work with Native AOT. To use it in an AOT project:

1. Add the source generator package
2. Create a context class with `[CborSerializable]` attributes
3. Use the generated serialization methods

## Type Support

- Primitives (int, string, bool, etc.)
- Collections (List<T>, Dictionary<K,V>, arrays)
- Custom classes and structs
- Nullable types
- Enums
- DateTime/DateTimeOffset
- Guid
- Decimal
- Nested generic types
- Large binary data

## Error Handling

The library provides specific exception types:
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

# Build the solution
dotnet build

# Run tests
dotnet test
```

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
