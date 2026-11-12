# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

NCbor is a .NET CBOR serialization library using Roslyn source generators for AOT compatibility. It follows System.Text.Json patterns (context classes, type info, static serializer API) and uses `System.Formats.Cbor` for encoding/decoding. Zero runtime reflection.

## Build & Test Commands

```bash
# Build entire solution
dotnet build

# Run all tests (both NCbor.Tests and NCbor.Generator.Tests; xUnit + FluentAssertions)
dotnet test NCbor.sln

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
- **NCbor.Tests/** — xUnit runtime round-trip tests (targets `net10.0`). Uses FluentAssertions.
- **NCbor.Generator.Tests/** — generator tests via `CSharpGeneratorDriver` (targets `net10.0`). `GeneratorHarness.Run(source)` returns generator diagnostics, generated sources and final-compilation diagnostics; `GeneratorHarness.AssertCompilesCleanly(source)` fails on generator exceptions, generator errors, or any compile error. Use it to test diagnostics and "this model shape compiles" — anything a runtime test can't express.
- **NCbor.Demo/** — Console demo app (targets `net10.0`).
- **NCborSample/** — Manual reference implementation used for experimental validation before automating in the generator.

### Source Generator Pipeline

The generator is split across these files:

1. **NCborSourceGenerator.cs** — Roslyn `IIncrementalGenerator` using `ForAttributeWithMetadataName("NCbor.NCborSerializableAttribute")`. All symbol work happens in the transform, which produces an **equatable** `ContextModel` (hint name, source text, `EquatableArray<DiagnosticInfo>`) — never let an `ISymbol`/`Compilation`/`SyntaxNode`/`Location` flow past the transform, or caching breaks (`GeneratorPipelineTests` guards this). Diagnostics are reported in a second step joined with `CompilationProvider` so they get real in-source locations (needed for `#pragma warning disable`). Hint names are `{Namespace}.{Context}.g.cs` (stable hash suffix when sanitising is lossy or names collide case-insensitively). The registered types' transitive closure is discovered automatically, and the generated context class contains:
   - A `Default` singleton property
   - `GetTypeInfo<T>()` switch dispatch
   - Private sealed `{TypeName}TypeInfo` inner classes implementing `NCborTypeInfo<T>`
   - Nullable helper methods (`ReadNullable*`, `ReadNullEnum<T>`)

2. **SerializationCodeGenerator\*.cs** (one `internal static partial class`):
   - `SerializationCodeGenerator.cs` — built-in types, property read/write, null handling for reference types.
   - `.Objects.cs` — base-chain property walk (stops at framework `System.*`/`Microsoft.*` bases), constructor selection (`[NCborConstructor]` → parameterless → single all-matching / primary ctor → CBOR010), object-initializer construction for `init`/`required`, CBOR014.
   - `.Collections.cs` — `TryGetCollectionInfo` (interface collections → `List`/`HashSet`/`Dictionary`), shared element read/write, CBOR013, CBOR009 classification.
   - `.Discovery.cs` — `DiscoverTypeClosure`, per-context name map (`ContextTypeNames`, auto-disambiguates collisions like `A.Item`/`B.Item` → `Item`/`B_Item`); every `_context.<Name>` emission must go through it.

   Key methods:
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
4. Add tests in NCbor.Tests (runtime round-trips) and NCbor.Generator.Tests (diagnostics / compiles-cleanly)

## Key Technical Details

- **netstandard2.0** for library and generator (broad compatibility); **net10.0** for tests and demos
- `System.Formats.Cbor` handles all CBOR encoding — the generator emits `CborWriter`/`CborReader` calls
- `IsBuiltInType()` lives in `SerializationCodeGenerator.cs`; `NCborSourceGenerator.cs`'s copy is a one-line delegation, so update only the former. Also remove the type from the CBOR009 unsupported list (`SerializationCodeGenerator.Collections.cs`) when you add support for it.
- Type names like `"System.Decimal"` may appear as `"decimal"` in some Roslyn contexts — always check both forms
- Arrays use `IArrayTypeSymbol` detection and generate context properties named `ArrayOf{ElementType}`
- `byte[]` is a special case: treated as a built-in type using CBOR byte string (major type 2) via `WriteByteString`/`ReadByteString`, not element-by-element like other arrays. Nullable `byte[]?` is handled inline with null checks.
- Enums serialize as their underlying numeric type, not as strings
- Three custom exception types: `NCborSerializationException`, `NCborDeserializationException`, `NCborValidationException`

## Known Gaps

`GAPS.md` at the repo root is the maintained, non-aspirational checklist of what's actually
implemented vs. stubbed vs. missing — it exists because README.md/roadmap.md/
cbor-serialization-specs.md have previously described unimplemented behavior as done. Before
stating a feature is complete or fixed, check `GAPS.md`; some attribute classes exist
(`NCborConverterAttribute`, `NCborConstructorAttribute`, `NCborDefaultValueAttribute`) but are
never read by the generator, which does not count as implemented. When you close a gap, update
`GAPS.md` and whichever doc made the original claim so they stay in sync.

`NCborSourceGenerationOptionsAttribute.MaxDepth` (previously an unread stub) is now wired up:
`NCborTypeInfo<T>.Serialize`/`Deserialize` take an `int depth = 0` parameter, every generated
`{Type}TypeInfo` checks it against `MaxDepth` and throws `NCborSerializationException`/
`NCborDeserializationException` before recursing further, and every nested `.Serialize`/
`.Deserialize` call passes `depth + 1`. This is depth-limiting, not exact cycle detection — a
legitimately deep-but-acyclic graph past `MaxDepth` also throws (same tradeoff as
`System.Text.Json`'s `MaxDepth`). See `NCbor.Tests/NCborCircularReferenceTests.cs`.

Generator diagnostics in use: CBOR005–010, CBOR012–014 (see the table in readme.md). CBOR001–004
and CBOR011 were removed — never reuse a retired ID; take the next free one (CBOR015).
Only root types need `[NCborSerializable(typeof(...))]` on a context; nested types are discovered.
