# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

NCbor is a .NET CBOR serialization library using Roslyn source generators for AOT compatibility. It follows System.Text.Json patterns (context classes, type info, static serializer API) and uses `System.Formats.Cbor` for encoding/decoding. Zero runtime reflection.

## Build & Test Commands

```bash
# Build entire solution
dotnet build

# Run all tests (xUnit + FluentAssertions)
dotnet test

# Run a specific test class
dotnet test --filter "NCborDecimalTests"

# Run a single test
dotnet test --filter "FullyQualifiedName~NCborDecimalTests.Serialize_Decimal_RoundTrip"

# Run the demo app
dotnet run --project NCbor.Demo

# Run the sample/reference implementation
dotnet run --project NCborSample
```

## Architecture

### Solution Structure

- **NCbor/** — Runtime library (targets `netstandard2.0`). Public API: `NCborSerializer` static class, `NCborSerializerContext` base class, `NCborTypeInfo<T>`, attributes, and custom exceptions.
- **NCbor.Generator/** — Roslyn incremental source generator (targets `netstandard2.0`). Scans for `[NCborSerializable]` on partial context classes and generates `{Context}.g.cs` files.
- **NCbor.Tests/** — xUnit test suite (targets `net10.0`). Uses FluentAssertions.
- **NCbor.Demo/** — Console demo app (targets `net10.0`).
- **NCborSample/** — Manual reference implementation used for experimental validation before automating in the generator.

### Source Generator Pipeline

The generator has two key files:

1. **NCborSourceGenerator.cs** — Roslyn `IIncrementalGenerator`. Finds partial classes with `[NCborSerializable(typeof(T))]`, extracts types, determines which are built-in vs complex, and emits a generated context class containing:
   - A `Default` singleton property
   - `GetTypeInfo<T>()` switch dispatch
   - Private sealed `{TypeName}TypeInfo` inner classes implementing `NCborTypeInfo<T>`
   - Nullable helper methods (`ReadNullable*`, `ReadNullEnum<T>`)

2. **SerializationCodeGenerator.cs** — Static methods that produce C# source strings for serialize/deserialize logic. Handles built-in types (primitives, Guid, DateTime, Decimal, enums), collections (List, Dictionary, arrays), and complex objects. Key methods:
   - `GenerateSerializationCode()` / `GenerateDeserializationCode()` — entry points for object types
   - `GenerateDirectSerialization()` / `GenerateDirectDeserialization()` — CborWriter/CborReader calls for built-in types
   - `IsBuiltInType()`, `IsList()`, `IsArray()`, `IsDictionary()` — type classification
   - `ApplyNamingPolicy()` — transforms property names per the 7 supported policies

### Usage Pattern (mirrors System.Text.Json)

```csharp
// 1. Mark types
[NCborSerializable]
public class MyModel { ... }

// 2. Create context
[NCborSerializable(typeof(MyModel))]
public partial class MyContext : NCborSerializerContext { }

// 3. Serialize/Deserialize
byte[] bytes = NCborSerializer.Serialize(obj, MyContext.Default.MyModel);
MyModel? result = NCborSerializer.Deserialize<MyModel>(bytes, MyContext.Default.MyModel);
```

## Development Methodology

For new type support, use the **experimental validation first** approach:
1. Manually implement serialize/deserialize in NCborSample
2. Test thoroughly via `dotnet run --project NCborSample`
3. Update the source generator to produce equivalent code
4. Add tests in NCbor.Tests

## Key Technical Details

- **netstandard2.0** for library and generator (broad compatibility); **net10.0** for tests and demos
- `System.Formats.Cbor` handles all CBOR encoding — the generator emits `CborWriter`/`CborReader` calls
- `IsBuiltInType()` exists in both `NCborSourceGenerator.cs` and `SerializationCodeGenerator.cs` — both must be updated when adding new built-in types
- Type names like `"System.Decimal"` may appear as `"decimal"` in some Roslyn contexts — always check both forms
- Arrays use `IArrayTypeSymbol` detection and generate context properties named `ArrayOf{ElementType}`
- `byte[]` is a special case: treated as a built-in type using CBOR byte string (major type 2) via `WriteByteString`/`ReadByteString`, not element-by-element like other arrays. Nullable `byte[]?` is handled inline with null checks.
- Enums serialize as their underlying numeric type, not as strings
- Three custom exception types: `NCborSerializationException`, `NCborDeserializationException`, `NCborValidationException`
