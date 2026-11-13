# NCbor Gap Tracker

Living checklist of doc/implementation gaps found by auditing README.md, roadmap.md,
cbor-serialization-specs.md, and release.md against the actual source (`NCbor/`,
`NCbor.Generator/`). Fix items one by one, check them off, and keep this file current —
it is the single source of truth for "is X actually done," not the prose docs.

Last audited: 2026-11-13. GAP-1–14 all have a resolution as of 2026-07-04: 11 fully done
(GAP-1, 2, 4, 5, 6, 7, 8, 10, 11, 13, 14), GAP-9 resolved by decision (documented, not
implemented), GAP-3 resolved by decision (claims softened, benchmark suite itself still not
built), and GAP-12 packaging prepped but not published (needs credentials/action, not code).
GAP-15–26 (model-fidelity pass, 2026-11-13 — see the section below) are done; the known limits
that pass deliberately left open are listed at the end of that section as LIMIT-1–6.
Nothing is silently unaddressed — every row states its actual current status.

## Code review pass (2026-07-05)

An 8-angle review of the whole session's diff (`git diff @{upstream}...HEAD`) found and fixed 8
issues introduced or exposed while closing the gaps above — none tracked as separate GAP rows
since they were fixed same-day, but recorded here for traceability:

1. **Enum-typed `[NCborDefaultValue]` failed to compile** (`CS0176`) — `FormatDefaultValueLiteral`
   now casts enum constants to their type instead of emitting a bare numeric literal.
2. **`DefaultIgnoreCondition = Always` + a property with only `[NCborDefaultValue]`** was
   silently, unconditionally dropped regardless of its value — `IsAlwaysIgnored` now treats
   `[NCborDefaultValue]` (with no explicit `[NCborIgnore]`) as opting out of the context-wide
   `Always` fallback, matching `GetWriteGuard`'s own priority for that attribute.
3. **Unmatched `[NCborConstructor]` parameter** (no property with a matching name) now reports
   `CBOR006` instead of silently deserializing to `default(T)` forever.
4. **Ambiguous `[NCborConstructor]`** (more than one marked constructor) now reports `CBOR007` as
   a build **error** instead of silently picking one via `FirstOrDefault`.
5. **`[NCborEnumAsString]` on a non-enum property** now reports `CBOR008` instead of silently
   having no effect.
6. **Off-by-one in `MaxDepth`**: the guard was `depth > maxDepth`, permitting `maxDepth + 1`
   levels before throwing. Changed to `depth >= maxDepth` so `MaxDepth = N` means at most `N`
   levels succeed, matching the documented/intuitive meaning.
7. **`[NCborConverter]`-decorated properties allocated a new converter instance on every single
   call** — the generator now declares one cached `private static readonly` field per distinct
   converter type on the `{Type}TypeInfo` class instead of `new {converterType}()` at each
   dispatch site.
8. **`NCborConverterAttribute`/`NCborConstructorAttribute` skipped CLAUDE.md's "NCborSample
   first" methodology** — backfilled with manual reference implementations in
   `NCborSample/Domain/ConverterSample.cs` and `ImmutablePersonSample.cs`, exercised from
   `NCborSample/Program.cs`. (`NCborEnumAsStringAttribute` turned out to already have prior
   manual validation — `NCborSample/Domain/EnumTestModel.cs`'s `useStringEnums` parameter — from
   before this session; the original review finding overstated this one.)

10 new tests added (186 total) covering all of the above except #8 (verified by running
`NCborSample` directly) and #3/#4/#5 (verified by manual build reproduction with deliberately
violating types — same approach as GAP-14's `CBOR005`, since asserting generator diagnostics
automatically would need a `CSharpGeneratorDriver` test harness this repo didn't have yet).
**Update:** that harness now exists — `NCbor.Generator.Tests/GeneratorHarness.cs` — and
`NCbor.Generator.Tests/GeneratorDiagnosticTests.cs` covers `CBOR005`–`CBOR008` (positive and
negative cases, including the `List<T>`/`Dictionary<K,V>` `CBOR005` false-positive regression).
`NCbor.Generator.Tests/KnownBugTests.cs` started as skipped tests for generator bugs found while
building it (base-class properties dropped, positional records / `init` properties /
`IReadOnlyList<T>` / unregistered nested `List<List<T>>` failing to compile, `CBOR005` firing on
records' `EqualityContract`); all of them are fixed and un-skipped as of 2026-11-13 (GAP-15–18).

## Model-fidelity pass (2026-11-13)

A probe of ordinary C# model shapes against the generator found that `roadmap.md`'s "Type Support
(100% Complete)" claim did not hold: several everyday shapes silently lost data or produced
generated code that failed to compile with errors pointing into `*.g.cs`. All rows below are
fixed on branch `feat/model-fidelity`, each with runtime round-trip tests in `NCbor.Tests` and
compile/diagnostic tests in `NCbor.Generator.Tests` (the new `CSharpGeneratorDriver` harness).

| ID | Area | Was | Now | Status |
|----|------|-----|-----|--------|
| GAP-15 | Inheritance | **Silent data loss**: only the declared type's own properties were serialized (`GetMembers()`), so `Derived : Base` dropped every `Base` property with no diagnostic. | Base chain walked base-first; overrides / `new`-hidden members deduped (most-derived wins, base slot kept); overrides inherit NCbor attributes from the overridden property unless they redeclare them. The walk stops at framework (`System.*`/`Microsoft.*` metadata) bases, so `Exception`/`Component`/`Attribute` subclasses serialize only their own members. See `SerializationCodeGenerator.Objects.cs` (`GetAllInstanceProperties`). | **Done** |
| GAP-16 | `init` / `required` members | `{ get; init; }` produced CS8852 in generated code; `required` members didn't compile. | Constructed via object initializer; a missing key for a `required` member throws `NCborDeserializationException`; `[SetsRequiredMembers]` constructors are respected. A `required` *field* (never serialized — NCbor serializes properties only) reports warning **CBOR014**. | **Done** |
| GAP-17 | Records / constructor-only types | Positional `record R(int A, string B)` failed (CS7036 + CS8852); records got a false CBOR005 for `EqualityContract`. | Constructor auto-selected without `[NCborConstructor]`: parameterless if available, else the single constructor whose parameters all match properties (exact case first, then unique case-insensitive), preferring the primary constructor; explicit parameter defaults apply when a key is absent. No usable constructor → error **CBOR010**. Structs keep `new T()` + setters unless a property is get-only/init-only. | **Done** |
| GAP-18 | Nested-type registration | Every nested type (`List<int>`, `List<List<int>>`, `Dictionary<int,string>`, nested POCOs) had to be registered by hand with `[NCborSerializable(typeof(...))]`, otherwise CS1061 in generated code. Undocumented. | Transitive closure of registered types is discovered automatically (as in System.Text.Json): registered types first, then discovered types in BFS order; context properties are `public`/`internal`/`private` to match the type's effective accessibility. Registering a primitive (`typeof(int)`) also works now (was CS0119). | **Done** |
| GAP-19 | Interface collections and sets | Only exact `List<T>`, `Dictionary<K,V>` and `T[]` worked; `IReadOnlyList<T>` etc. failed with CS1061/CS0144; `HashSet<T>` was treated as a plain object. | `IList`/`ICollection`/`IReadOnlyList`/`IReadOnlyCollection`/`IEnumerable<T>` → `List<T>`; `HashSet`/`ISet`/`IReadOnlySet` → `HashSet<T>`; `IDictionary`/`IReadOnlyDictionary` → `Dictionary<K,V>`. `IEnumerable<T>` writes an indefinite-length array. Jagged arrays (`int[][]`) now compile (was CS1586). `List<int?>` fixed (was CS1061). | **Done** |
| GAP-20 | Unsupported types | Unsupported property types (`TimeSpan`, `Uri`, `object`, …) failed with CS1061 deep in generated code. | Error **CBOR009** naming the type and the property that uses it, with a throwing stub so no cascading compiler errors. Covers `TimeSpan`, `Uri`, `DateOnly`, `TimeOnly`, `char`, `object`, `dynamic`, delegates, tuples, multi-dim arrays, abstract/interface types outside the collection table, framework collections outside the table (`Queue`, `Stack`, …), `Nullable<struct POCO>`, open generics. `[NCborConverter]`/`[NCborIgnore]` properties are exempt. A *user* type that implements `IEnumerable` but isn't in the table is serialized as an object with warning **CBOR013**. | **Done** (implementing `TimeSpan`/`Uri` themselves is still open — LIMIT-6) |
| GAP-21 | Context-property name collisions | Two types with the same simple name (e.g. `A.Item`, `B.Item`) → CS0102. | Names auto-disambiguated per context (first claimant keeps the simple name; later ones are prefixed with the nearest namespace/containing-type segment, e.g. `B_Item`), threaded through every emission site. `CBOR011` was briefly introduced for this and is **retired** (ID not reused). | **Done** |
| GAP-22 | Null reference-type properties | **Crash**: a null `string?` threw a raw `ArgumentNullException`; a null `T[]?` threw a wrapped `NullReferenceException`. 12 CS8604 warnings in generated code. | Any reference-type property writes CBOR `null` when null and reads it back as null, regardless of annotation (System.Text.Json behavior). `WhenWritingNull` still omits the key. Generated code builds with 0 warnings. | **Done** |
| GAP-23 | Generator pipeline cost | `CompilationProvider` combined with every partial class → whole generator re-ran on every edit; one CBOR004 Info diagnostic per *line* of generated source (5,598 in a detailed build log), plus CBOR001/003. | `ForAttributeWithMetadataName`; all symbol work in the transform producing an equatable model, so unrelated edits leave output cached (tested via step tracking). CBOR001–004 removed (IDs not reused). Hint names are `Namespace.Context.g.cs`, with a stable hash suffix when sanitising is lossy or names collide case-insensitively. Nested contexts (inside `partial` types — else error **CBOR012**), generic contexts, and `internal`/`private` contexts now generate correctly. | **Done** |
| GAP-24 | Serializer API surface | `byte[]` only; trailing bytes after the top-level value were silently ignored. | `Stream` (sync + `SerializeAsync`/`DeserializeAsync` returning `ValueTask<T>`), `IBufferWriter<byte>`, `ReadOnlyMemory<byte>` overloads (buffered, not streaming — roadmap 2.5.3). Trailing data → `NCborValidationException`. Payload-caused `ArgumentException`s (duplicate map keys, unknown enum names) → `NCborDeserializationException`. | **Done** |
| GAP-25 | Generator test harness | Generator diagnostics were only verified by manual builds. | `NCbor.Generator.Tests` (`GeneratorHarness.Run`/`AssertCompilesCleanly`) covers every diagnostic CBOR005–015, compile-cleanly checks per model shape, incremental caching, and diagnostic locations; CI runs `dotnet test NCbor.sln`. | **Done** |
| GAP-26 | Identifier robustness in generated code | Generated code used unqualified type names and unescaped identifiers: a user type named `FooTypeInfo`, `Type`, `CborReader` or `var`, a model named like its context, a context-nested `Item`, or a property named `@class` broke compilation; framework base classes with settable properties (e.g. `UriBuilder`) were cut off silently. | Every emitted type reference is `global::`-qualified (no `using` lines in generated files), locals are explicitly typed, keywords are `@`-escaped (wire keys stay unescaped, emitted as escaped string literals), nested type names of the context are reserved in the name map. Warning **CBOR015** when a framework base's public settable properties are skipped (`Exception`/`Attribute`/`Component` exempt — their settable members like `HelpLink`/`Site` are never wanted). | **Done** |

**Known limits deliberately left open (not silently missing):**

- **LIMIT-1** — an `init`-only property loses its field initializer when the payload omits its key (it can't be conditionally assigned in an object initializer). Use `[NCborDefaultValue]` or a settable property if that matters.
- **LIMIT-2** — `init`-only `required` members on a `[SetsRequiredMembers]` constructor are still assigned from the payload, so an absent key overwrites the constructor's value with `default`.
- **LIMIT-3** — a mutable `record struct R(int A = 42)` uses `new R()` + setters, so the parameter default is not applied when the key is absent.
- **LIMIT-4** — disambiguated context-property names depend on registration/discovery order: adding a type can rename an existing *discovered* property (e.g. `Item` → `B_Item`). Register the type explicitly if you depend on its property name.
- **LIMIT-5** — the `Stream` overloads buffer the whole payload (`CborReader`/`CborWriter` aren't streaming); `ValueTask` comes from `System.Threading.Tasks.Extensions` on netstandard2.0.
- **WIRE-1 (intentional wire change)** — overrides now inherit NCbor attributes from the overridden
  property (GAP-15). If a base `virtual` property carries `[NCborPropertyName("n")]` or `[NCborIgnore]`
  and a derived class overrides it *without* repeating the attribute, older builds wrote the derived
  type under the C# name (`"Name"`, and wrote the ignored property); new builds write `"n"` and omit it.
  Payloads written by an older build for such types still deserialize without error, but the renamed
  property comes back as its default value. Repeat the attribute on the override if you need the old
  wire shape. All other wire-format differences in this pass are additive (inherited base properties
  now present; null reference properties written as CBOR `null` instead of crashing).
- **LIMIT-6** — `TimeSpan`, `Uri`, `DateOnly`, `TimeOnly`, `char` are diagnosed (CBOR009) but not implemented; use `[NCborConverter]` meanwhile. Pointer-typed properties still produce compiler errors on top of CBOR009.

## How to use this file

- When you close a gap: flip its Status to `Done`, add the commit/PR reference, and update
  whichever doc (README/roadmap/spec) made the original claim so it matches reality again.
- When you find a new gap: add a row in the right priority section with the same shape.
- Don't mark something `Done` because a stub/attribute exists — it's only done when the
  generator actually reads it and a test proves it (see Development Methodology in CLAUDE.md).

---

## P0 — Documentation accuracy (fixed 2026-07-04)

These were docs describing code that doesn't exist, or stale claims. Fixed as part of this
audit; listed here so the mismatch and its fix are traceable.

| ID | File | Was | Now | Status |
|----|------|-----|-----|--------|
| DOC-1 | README.md Quick Start | Code sample used pre-rename names (`[CborSerializable]`, `CborSerializerContext`, `CborKnownNamingPolicy`, `CborSerializer.Serialize`) that don't compile | Uses actual `NCbor`-prefixed API | Done |
| DOC-2 | README.md Project Structure | Tree listed `CborSerializer.cs`, `NCbor.Demo/Domain.cs`, `MyCborContext.cs`, `CborSample/Person.Cbor.g.cs` — none exist | Matches actual layout (`NCbor.cs`, `NCborContext.cs`, `Domain/`, `MyContext.cs`, `NCborSample/`) | Done |
| DOC-3 | README.md Error Handling | Named exception type `NCborException` (doesn't exist) | Corrected to `NCborSerializationException` | Done |
| DOC-4 | release.md | Referenced `[CborSerializable]` (missing `N` prefix) | Corrected to `[NCborSerializable]` | Done |
| DOC-5 | release.md Future Enhancements | Listed "comprehensive test suite" as future work while 148 tests already exist | Removed / reworded | Done |
| DOC-6 | roadmap.md header | `Last Updated: 2025-06-23` despite being edited 2026-03-24 for byte[] support | Date corrected | Done |
| DOC-7 | cbor-serialization-specs.md §7.2 | DateTime/Enum/Binary Data subsections read as current behavior when they're target/aspirational | Annotated with actual status + link to GAP-1/GAP-3/GAP-4 below | Done |

---

## P1 — High priority (correctness / project health)

| ID | Area | Current state | It should be | Fix required | Status |
|----|------|---------------|---------------|---------------|--------|
| GAP-1 | Circular reference protection | ~~No cycle detection anywhere in the generator...~~ **Fixed 2026-07-04**: `NCborTypeInfo<T>.Serialize`/`Deserialize` now take an `int depth = 0` parameter; every generated `{Type}TypeInfo.Serialize`/`Deserialize` checks `depth` against the context's `NCborSourceGenerationOptionsAttribute.MaxDepth` (default 64) and throws `NCborSerializationException`/`NCborDeserializationException` before recursing further; every nested `.Serialize(...)`/`.Deserialize(...)` call (properties, list/array elements, dictionary keys/values) passes `depth + 1`. | Self-referencing object graphs throw a clear exception instead of a `StackOverflowException`, respecting `MaxDepth`. | Done — see `NCbor/NCborTypeInfo.cs`, `NCbor.Generator/NCborSourceGenerator.cs` (`GetMaxDepth`), `NCbor.Generator/SerializationCodeGenerator.cs`, and `NCbor.Tests/NCborCircularReferenceTests.cs`. **Known tradeoff (by design, matches System.Text.Json's `MaxDepth`)**: this is depth-limiting, not true reference-tracking — a legitimately deep-but-acyclic graph past `MaxDepth` also throws; raise `MaxDepth` on the context if that's expected. A `HashSet<object>`-based exact-cycle-detection pass would be a further, lower-priority refinement if the depth-limit false-positive rate becomes a problem in practice. | **Done** |
| GAP-2 | CI/CD pipeline | ~~No GitHub Actions workflow runs build/test on push or PR...~~ **Fixed 2026-07-04**: `.github/workflows/build.yml` restores, builds (`Release`), and runs `NCbor.Tests` on every push/PR to `main`, uploading `.trx` results as an artifact. README now carries a live build-status badge. | Every push/PR should build the solution and run `NCbor.Tests` automatically. | Done — see `.github/workflows/build.yml`. **Not yet done**: no publish/pack step (tracked separately as GAP-12), no matrix beyond `ubuntu-latest` (the library has no OS-specific code, so this is low-risk to skip for now). | **Done** |
| GAP-3 | Unverified performance claims | **Partially resolved by decision (2026-07-04)**: maintainer chose to soften the claim rather than build the benchmark suite right now. Every "5-10x faster" mention in README.md/roadmap.md that was stated as achieved fact (including one that had a ✅ checkmark on it) now says "target, unverified" with a link here. | Performance claims in public docs should be either measured and cited, or clearly labeled as a target/hypothesis, not stated as an established fact. | Docs done — see README.md/roadmap.md. **Still open**: no BenchmarkDotNet project exists; the "5-10x" number itself is still unverified against Dahomey.Cbor/PeterO.Cbor. Build one if/when proving the performance claim becomes a priority. | **Docs done, benchmark still open** |

## P2 — Medium priority (attribute system gaps already stubbed but inert)

| ID | Area | Current state | It should be | Fix required | Status |
|----|------|---------------|---------------|---------------|--------|
| GAP-4 | `NCborConverterAttribute` | ~~Never inspected by the generator...~~ **Fixed 2026-07-04**: `[NCborConverter(typeof(X))]` on a property now takes priority over every other dispatch rule — the generator emits `new X().Write(writer, value.Prop)` / `new X().Read(reader)` instead of built-in/complex-object codegen. | Let a property opt into a custom `INCborConverter<T>` for serialization/deserialization. | Done — see `NCbor/INCborConverter.cs`, `SerializationCodeGenerator.GetConverterType`, and `NCbor.Tests/NCborConverterTests.cs` (proves the override by checking the raw CBOR wire type differs from the default encoding). **Scope note**: property-level only; the attribute also allows Class/Struct/Interface targets but type-level converter dispatch (bypassing TypeInfo generation entirely) is not implemented — not currently tracked as a separate gap since no spec text promises it. | **Done** |
| GAP-5 | `NCborConstructorAttribute` | ~~Generator only supports property-based deserialization...~~ **Fixed 2026-07-04**: a type with a `[NCborConstructor]`-marked constructor now deserializes by reading all properties into locals, then calling that constructor (matching parameters to properties by name, case-insensitive), then assigning any remaining settable properties it didn't cover. Get-only properties (needed for immutable types) are now included in serialization/deserialization when constructor-matched — previously they were silently dropped entirely by the `SetMethod != null` filter, for *every* type, not just constructor ones. | Support constructor-based deserialization for immutable types/records, matching constructor parameters to properties by name. | Done — see `SerializationCodeGenerator.GetDeserializationConstructor`/`FindMatchingParameter`/`FindMatchingProperty` and `NCbor.Tests/NCborConstructorTests.cs`. **Known limitation (documented, not silently missing)**: `[NCborPropertyName]` on a constructor *parameter* (as shown in cbor-serialization-specs.md §3.1's `ImmutablePerson` example) is not consulted for the wire key name — only property-level `[NCborPropertyName]` is read. Put the attribute on the property if you need a custom wire name. | **Done** |
| GAP-6 | `NCborDefaultValueAttribute` | ~~Attribute value is never read...~~ **Fixed 2026-07-04**: the generator now reads the declared value and wraps the property's key+value write in `!EqualityComparer<T>.Default.Equals(value.Prop, declaredDefault)`, so a property matching its declared default is omitted from the CBOR map entirely (and comes back via the property's own field initializer on deserialize). | Skip serializing a property when its runtime value equals the declared default (mirrors `System.Text.Json`'s `JsonIgnoreCondition.WhenWritingDefault` semantics). | Done — see `SerializationCodeGenerator.GetWriteGuard`/`FormatDefaultValueLiteral` and `NCbor.Tests/NCborIgnoreConditionTests.cs`. | **Done** |
| GAP-7 | `NCborIgnoreCondition` | ~~Generator only honors Always...~~ **Fixed 2026-07-04**: `Never` now genuinely means "always write" (previously ANY `[NCborIgnore]` — regardless of `Condition` — caused full exclusion, which was actively wrong, not just unimplemented); `WhenWritingNull` and `WhenWritingDefault` now conditionally omit the key+value pair, matching System.Text.Json semantics (including: `WhenWritingNull` is a no-op on non-nullable value types). | The other three conditions should conditionally skip serialization per their semantics. | Done — see `SerializationCodeGenerator.GetWriteGuard`/`IsAlwaysIgnored`/`CanBeNull` and `NCbor.Generator/NCborIgnoreCondition.cs` (generator-local enum copy, same pattern as `NCborNamingPolicy`). | **Done** |
| GAP-8 | String-enum serialization | ~~No such attribute or code path exists...~~ **Fixed 2026-07-04**: new `[NCborEnumAsString]` property-level attribute (`NCbor/NCborEnumAsStringAttribute.cs`) takes priority over the default numeric encoding, emitting `writer.WriteTextString(value.ToString())` / `Enum.Parse` on read. Works on both plain and nullable enum properties. | Offer an opt-in attribute to serialize an enum's name instead of its numeric value. | Done — see `SerializationCodeGenerator.HasEnumAsStringAttribute`/`TryGetNullableEnumUnderlyingType` and `NCbor.Tests/NCborEnumAsStringTests.cs`. | **Done** |
| GAP-9 | DateTimeKind preservation | **Resolved by decision (2026-07-04, maintainer choice: option b)**: `SerializationCodeGenerator.cs` forces `Unspecified → Utc` then calls `.ToUniversalTime()` before writing — a `Local` value round-trips back UTC-normalized; original `Kind` is not recoverable. **This is being kept as-is intentionally**, not fixed. | Behavior stays as UTC-normalization on write; docs state this honestly instead of claiming Kind preservation. | Done (docs-only) — cbor-serialization-specs.md §7.2, README.md, and roadmap.md all now describe `DateTime` as UTC-normalizing and `DateTimeOffset` (which does preserve its offset) separately, instead of a blanket "timezone preservation" claim covering both. No code change was made or is planned. | **Done** |
| GAP-13 | `NCborSourceGenerationOptionsAttribute.DefaultIgnoreCondition` | ~~Never read by the generator...~~ **Fixed 2026-07-04**: `NCborSourceGenerator.GetDefaultIgnoreCondition` now reads it, and `GetWriteGuard`/`IsAlwaysIgnored` fall back to it for any property with no explicit `[NCborIgnore]` of its own. **Footgun avoided**: the attribute property has no field initializer, so its literal C# default is enum value `0` (`Always`) purely from member ordering — naively treating "unset" as `Always` would have silently excluded every property in every context that doesn't explicitly set this. The generator instead treats "named argument absent" as `Never` (matches System.Text.Json's actual default), verified by a regression test. | A context-wide default so users don't have to annotate every single property with `[NCborIgnore(Condition = WhenWritingDefault)]` one at a time. | Done — see `NCborSourceGenerator.GetDefaultIgnoreCondition` and `NCbor.Tests/NCborDefaultIgnoreConditionTests.cs` (includes `NoDefaultIgnoreConditionSet_UnsetFallsBackToNever_NotAlways`, guarding the footgun above). | **Done** |
| GAP-14 | Get-only properties without `[NCborConstructor]` | ~~No error, no warning...~~ **Fixed 2026-07-04**: the generator now reports `CBOR005`, a compile-time warning naming the exact property and type, whenever a get-only property has no setter and isn't covered by a `[NCborConstructor]` parameter (or is covered but the effective ignore condition isn't `Always`, in which case the exclusion was already deliberate — no warning). | Either emit a compile-time generator diagnostic when a get-only property has no covering constructor parameter (fail loud instead of silently dropping data), or document the requirement prominently in the README/spec attribute section. | Done — see `SerializationCodeGenerator.GetUncoveredGetOnlyProperties` and `NCborSourceGenerator.cs`'s `CBOR005` diagnostic. **Verified by manual build inspection** (confirmed the warning fires for a deliberately-violating type and does *not* false-positive on `List<T>`/`Dictionary<K,V>`'s own BCL get-only properties, a real bug the manual check caught before this shipped) — now also covered by automated regression tests in `NCbor.Generator.Tests/GeneratorDiagnosticTests.cs` (the `CSharpGeneratorDriver` harness in `NCbor.Generator.Tests/GeneratorHarness.cs`). | **Done** |

## P3 — Low priority (documented as future work; sequencing convenience only)

| ID | Area | Current state | It should be | Fix required | Status |
|----|------|---------------|---------------|---------------|--------|
| GAP-10 | Chunked/streaming byte[] | ~~Single unconditional writer.WriteByteString(value) call...~~ **Fixed 2026-07-04 (maintainer chose to implement)**: byte[] values over 4096 bytes (`ByteStringChunkThreshold`) now write as a CBOR indefinite-length byte string composed of 4096-byte chunks via `WriteStartIndefiniteLengthByteString`/`WriteByteString`(chunk)/`WriteEndIndefiniteLengthByteString`. Values at or under the threshold keep the original single-call encoding — no wire format change for the common case. **Important caveat, not "true streaming"**: the input is still a fully-materialized C# `byte[]` — this chunks the *wire encoding*, it does not reduce runtime memory use, since the whole array must already be in memory before this code runs. Genuine streaming (accepting `Stream`/`IAsyncEnumerable` instead of `byte[]`) is a materially larger, separate feature (roadmap.md Phase 2.5.3) not covered by this fix. | Support for chunked serialization matching typical CBOR interop expectations. | Done — see `SerializationCodeGenerator.GenerateByteStringWrite`. Deserialization needed **no changes**: `CborReader.ReadByteString()` already transparently reassembles indefinite-length byte strings into one byte[] (verified directly against `System.Formats.Cbor` before implementing, not assumed). Tests: `NCbor.Tests/NCborByteArrayTests.cs` (`..._UsesIndefiniteLengthEncoding`, `..._AboveChunkThreshold_PreservesAllBytes`, `..._ExactlyAtChunkBoundary_PreservesAllBytes`). | **Done** |
| GAP-11 | Array deserialization allocation | ~~Arrays deserialize via a List<T> intermediary...~~ **Fixed 2026-07-04**: when the CBOR array has a definite length (which is everything NCbor's own writer produces, via `WriteStartArray(value.Length)`), the generator now allocates the array directly and assigns by index — no `List<T>` intermediary or `.ToArray()` copy. Indefinite-length arrays (only relevant when reading CBOR from another encoder) still use the `List<T>` accumulator, since the final size isn't known up front. | Direct array allocation when the CBOR array has a definite length. | Done — see the `IsArray` branch of `SerializationCodeGenerator.GenerateDeserializationCode`, and `NCbor.Tests/NCborArrayTests.cs`'s `DeserializeIndefiniteLengthArray_FromAnotherEncoder_ShouldSucceed` (proves the fallback path still works, since NCbor's own serializer never naturally exercises it). | **Done** |
| GAP-12 | NuGet publishing | **Packaging prepared 2026-07-04 (maintainer chose: prep only, not an actual publish)**: both `NCbor.csproj` and `NCbor.Generator.csproj` had zero packaging metadata before this — `dotnet pack` would have produced a nupkg with an inferred PackageId, `1.0.0` version, no license/description/tags, and (for the generator) no `analyzers/dotnet/cs` layout at all, so installing it from a real feed wouldn't have worked as a generator. `git tag` is still empty; no package has ever actually been published. | Packaging should be ready to publish when the maintainer decides to; actual `dotnet nuget push` is a deliberate, credentialed action, not something to automate. | Done (prep only) — both `.csproj` files now have `PackageId`/`Version`/`Authors`/`Description`/`PackageLicenseExpression` (Apache-2.0, matching the repo's actual LICENSE)/`PackageProjectUrl`/`RepositoryUrl`/`PackageReadmeFile`, and the generator project correctly packs its DLL under `analyzers/dotnet/cs` (not `lib/`) via the standard `IncludeBuildOutput=false` + `None ... PackagePath="analyzers/dotnet/cs"` pattern — verified by actually running `dotnet pack` on both and inspecting the resulting .nupkg contents, not just written and assumed correct. ~~**New finding surfaced by that verification**: `dotnet pack` on `NCbor.csproj` emits `NU5104` — a stable `1.0.0` package should not depend on a prerelease `System.Formats.Cbor` version (`10.0.0-preview.5.25277.114`).~~ **Fixed**: every project now pins stable `System.Formats.Cbor 10.0.15`; re-running `dotnet pack NCbor/NCbor.csproj -c Release` no longer emits `NU5104`. **Still not done**: no version has ever been published; `Authors` is currently set to the GitHub org name (`atcults`) as a placeholder — confirm before publishing. | **Prep done, publish still open** |

---

## Already tracked in roadmap.md (not duplicated here)

`roadmap.md` already carries detailed Phase 2.5/3/4/5 breakdowns (effort estimates,
dependencies) for several of the above (circular reference protection, custom converters,
constructor deserialization, attribute completion, CI/CD, benchmarking, NuGet publishing).
This file is the flat, no-marketing-language version of the same backlog — treat `roadmap.md`
as the narrative/strategic view and this file as the "is it actually done yet" checklist.
Known low-priority unsupported types not broken out above: `TimeSpan`, `Uri` (now diagnosed with CBOR009 — see LIMIT-6).
