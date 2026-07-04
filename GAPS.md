# NCbor Gap Tracker

Living checklist of doc/implementation gaps found by auditing README.md, roadmap.md,
cbor-serialization-specs.md, and release.md against the actual source (`NCbor/`,
`NCbor.Generator/`). Fix items one by one, check them off, and keep this file current —
it is the single source of truth for "is X actually done," not the prose docs.

Last audited: 2026-07-04. All 14 tracked gaps have a resolution as of 2026-07-04: 11 fully done
(GAP-1, 2, 4, 5, 6, 7, 8, 10, 11, 13, 14), GAP-9 resolved by decision (documented, not
implemented), GAP-3 resolved by decision (claims softened, benchmark suite itself still not
built), and GAP-12 packaging prepped but not published (needs credentials/action, not code).
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
automatically would need a `CSharpGeneratorDriver` test harness this repo doesn't have yet).

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
| GAP-14 | Get-only properties without `[NCborConstructor]` | ~~No error, no warning...~~ **Fixed 2026-07-04**: the generator now reports `CBOR005`, a compile-time warning naming the exact property and type, whenever a get-only property has no setter and isn't covered by a `[NCborConstructor]` parameter (or is covered but the effective ignore condition isn't `Always`, in which case the exclusion was already deliberate — no warning). | Either emit a compile-time generator diagnostic when a get-only property has no covering constructor parameter (fail loud instead of silently dropping data), or document the requirement prominently in the README/spec attribute section. | Done — see `SerializationCodeGenerator.GetUncoveredGetOnlyProperties` and `NCborSourceGenerator.cs`'s `CBOR005` diagnostic. **Verified by manual build inspection** (confirmed the warning fires for a deliberately-violating type and does *not* false-positive on `List<T>`/`Dictionary<K,V>`'s own BCL get-only properties, a real bug the manual check caught before this shipped) — **not yet covered by an automated regression test**, since asserting compiler diagnostics from this generator would need a `CSharpGeneratorDriver` test harness that doesn't exist in this repo yet. Worth building if CBOR005 regresses silently in the future. | **Done** |

## P3 — Low priority (documented as future work; sequencing convenience only)

| ID | Area | Current state | It should be | Fix required | Status |
|----|------|---------------|---------------|---------------|--------|
| GAP-10 | Chunked/streaming byte[] | ~~Single unconditional writer.WriteByteString(value) call...~~ **Fixed 2026-07-04 (maintainer chose to implement)**: byte[] values over 4096 bytes (`ByteStringChunkThreshold`) now write as a CBOR indefinite-length byte string composed of 4096-byte chunks via `WriteStartIndefiniteLengthByteString`/`WriteByteString`(chunk)/`WriteEndIndefiniteLengthByteString`. Values at or under the threshold keep the original single-call encoding — no wire format change for the common case. **Important caveat, not "true streaming"**: the input is still a fully-materialized C# `byte[]` — this chunks the *wire encoding*, it does not reduce runtime memory use, since the whole array must already be in memory before this code runs. Genuine streaming (accepting `Stream`/`IAsyncEnumerable` instead of `byte[]`) is a materially larger, separate feature (roadmap.md Phase 2.5.3) not covered by this fix. | Support for chunked serialization matching typical CBOR interop expectations. | Done — see `SerializationCodeGenerator.GenerateByteStringWrite`. Deserialization needed **no changes**: `CborReader.ReadByteString()` already transparently reassembles indefinite-length byte strings into one byte[] (verified directly against `System.Formats.Cbor` before implementing, not assumed). Tests: `NCbor.Tests/NCborByteArrayTests.cs` (`..._UsesIndefiniteLengthEncoding`, `..._AboveChunkThreshold_PreservesAllBytes`, `..._ExactlyAtChunkBoundary_PreservesAllBytes`). | **Done** |
| GAP-11 | Array deserialization allocation | ~~Arrays deserialize via a List<T> intermediary...~~ **Fixed 2026-07-04**: when the CBOR array has a definite length (which is everything NCbor's own writer produces, via `WriteStartArray(value.Length)`), the generator now allocates the array directly and assigns by index — no `List<T>` intermediary or `.ToArray()` copy. Indefinite-length arrays (only relevant when reading CBOR from another encoder) still use the `List<T>` accumulator, since the final size isn't known up front. | Direct array allocation when the CBOR array has a definite length. | Done — see the `IsArray` branch of `SerializationCodeGenerator.GenerateDeserializationCode`, and `NCbor.Tests/NCborArrayTests.cs`'s `DeserializeIndefiniteLengthArray_FromAnotherEncoder_ShouldSucceed` (proves the fallback path still works, since NCbor's own serializer never naturally exercises it). | **Done** |
| GAP-12 | NuGet publishing | **Packaging prepared 2026-07-04 (maintainer chose: prep only, not an actual publish)**: both `NCbor.csproj` and `NCbor.Generator.csproj` had zero packaging metadata before this — `dotnet pack` would have produced a nupkg with an inferred PackageId, `1.0.0` version, no license/description/tags, and (for the generator) no `analyzers/dotnet/cs` layout at all, so installing it from a real feed wouldn't have worked as a generator. `git tag` is still empty; no package has ever actually been published. | Packaging should be ready to publish when the maintainer decides to; actual `dotnet nuget push` is a deliberate, credentialed action, not something to automate. | Done (prep only) — both `.csproj` files now have `PackageId`/`Version`/`Authors`/`Description`/`PackageLicenseExpression` (Apache-2.0, matching the repo's actual LICENSE)/`PackageProjectUrl`/`RepositoryUrl`/`PackageReadmeFile`, and the generator project correctly packs its DLL under `analyzers/dotnet/cs` (not `lib/`) via the standard `IncludeBuildOutput=false` + `None ... PackagePath="analyzers/dotnet/cs"` pattern — verified by actually running `dotnet pack` on both and inspecting the resulting .nupkg contents, not just written and assumed correct. **New finding surfaced by that verification**: `dotnet pack` on `NCbor.csproj` emits `NU5104` — a stable `1.0.0` package should not depend on a prerelease `System.Formats.Cbor` version (`10.0.0-preview.5.25277.114`); worth checking whether a stable `System.Formats.Cbor` now exists for .NET 10 GA before actually publishing. **Still not done**: no version has ever been published; `Authors` is currently set to the GitHub org name (`atcults`) as a placeholder — confirm before publishing. | **Prep done, publish still open** |

---

## Already tracked in roadmap.md (not duplicated here)

`roadmap.md` already carries detailed Phase 2.5/3/4/5 breakdowns (effort estimates,
dependencies) for several of the above (circular reference protection, custom converters,
constructor deserialization, attribute completion, CI/CD, benchmarking, NuGet publishing).
This file is the flat, no-marketing-language version of the same backlog — treat `roadmap.md`
as the narrative/strategic view and this file as the "is it actually done yet" checklist.
Known low-priority unsupported types not broken out above: `TimeSpan`, `Uri`.
