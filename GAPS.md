# NCbor Gap Tracker

Living checklist of doc/implementation gaps found by auditing README.md, roadmap.md,
cbor-serialization-specs.md, and release.md against the actual source (`NCbor/`,
`NCbor.Generator/`). Fix items one by one, check them off, and keep this file current —
it is the single source of truth for "is X actually done," not the prose docs.

Last audited: 2026-07-04. Last gap closed: 2026-07-04 (GAP-2, CI/CD pipeline).

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
| GAP-3 | Unverified performance claims | README.md and roadmap.md repeatedly state "5-10x faster than Dahomey.Cbor/PeterO.Cbor" as fact, but roadmap.md's own progress metrics mark the benchmarking suite (Phase 2.2) at **0% complete** — no benchmark exists anywhere in the repo. | Performance claims in public docs should be either measured and cited, or clearly labeled as a target/hypothesis, not stated as an established fact. | Add a BenchmarkDotNet project comparing against Dahomey.Cbor/PeterO.Cbor, publish real numbers, and until then soften README/roadmap language from "5-10x faster" to "targeting 5-10x faster (unverified — see GAP-3)". | Open |

## P2 — Medium priority (attribute system gaps already stubbed but inert)

| ID | Area | Current state | It should be | Fix required | Status |
|----|------|---------------|---------------|---------------|--------|
| GAP-4 | `NCborConverterAttribute` | Attribute class exists (`NCbor/NCborConverterAttribute.cs`) but is never inspected by the generator — decorating a property with it has zero effect, silently. | Should let a property opt into a custom `INCborConverter<T>` for serialization/deserialization. | Define `INCborConverter<T>` interface; have the generator detect the attribute and emit calls to the converter instead of built-in/complex-object codegen. | Open |
| GAP-5 | `NCborConstructorAttribute` | Attribute class exists (`NCbor/NCborConstructorAttribute.cs`) but generator only supports property-based deserialization — constructor-marked types are silently deserialized the normal way (or fail if there's no parameterless path). | Should support constructor-based deserialization for immutable types/records, matching constructor parameters to properties by name. | Generator: detect `[NCborConstructor]`, match parameter names (respecting `[NCborPropertyName]`) to read values, emit `new T(...)` instead of property setters. | Open |
| GAP-6 | `NCborDefaultValueAttribute` | Attribute class exists (`NCbor/NCborDefaultValueAttribute.cs`) but its value is never read — properties always serialize regardless of matching the declared default. | Should skip serializing a property when its runtime value equals the declared default (mirrors `System.Text.Json`'s `JsonIgnoreCondition.WhenWritingDefault` semantics). | Generator: read the attribute's value at codegen time, emit an `if (value.Prop != default) { ... }` guard around the write call. | Open |
| GAP-7 | `NCborIgnoreCondition` | Enum defines `Always`, `Never`, `WhenWritingDefault`, `WhenWritingNull`, but the generator only honors `Always` (property is either fully included or fully excluded). | The other three conditions should conditionally skip serialization per their semantics. | Generator: branch on `NCborIgnoreAttribute.Condition` and emit the matching conditional-write guard (ties into GAP-6 for `WhenWritingDefault`). | Open |
| GAP-8 | String-enum serialization | cbor-serialization-specs.md §7.2 lists "optional string serialization via attribute" for enums; no such attribute or code path exists — enums always serialize as their underlying numeric value (`SerializationCodeGenerator.cs:591-592`). | Should offer an opt-in attribute (e.g. `[NCborEnumAsString]`) to serialize an enum's name instead of its numeric value. | Add attribute + generator branch emitting `writer.WriteTextString(value.ToString())` / `Enum.Parse` on the read side when present. | Open |
| GAP-9 | DateTimeKind preservation | cbor-serialization-specs.md §7.2 claims "Preserve DateTimeKind information." Implementation (`SerializationCodeGenerator.cs:562`) forces `Unspecified → Utc` then calls `.ToUniversalTime()` before writing — a `Local` value round-trips back as effectively UTC-normalized; original `Kind` is not recoverable. | Either (a) genuinely preserve `Kind` by encoding it alongside the timestamp, or (b) keep the current UTC-normalization behavior but document it honestly instead of claiming preservation. | Decide the intended behavior with the maintainer, then either add Kind-encoding to the generator or correct the spec (spec already corrected as DOC-7 pending this decision). | Open |

## P3 — Low priority (documented as future work; sequencing convenience only)

| ID | Area | Current state | It should be | Fix required | Status |
|----|------|---------------|---------------|---------------|--------|
| GAP-10 | Chunked/streaming byte[] | cbor-serialization-specs.md §7.2 claims "support for chunked serialization" / "memory-efficient streaming" for binary data. Implementation is a single unconditional `writer.WriteByteString(value)` call (`SerializationCodeGenerator.cs:10,272,274`) — fully buffered, no chunking. | Fine for typical payloads; only matters once byte[] properties routinely carry very large buffers. | Add indefinite-length byte-string chunking in the generator, or (cheaper) leave as-is and keep the spec honest about current single-buffer behavior. | Open |
| GAP-11 | Array deserialization allocation | Arrays deserialize via a `List<T>` intermediary then `.ToArray()` (roadmap.md Phase 2.5.2), causing avoidable allocation for large arrays. | Direct array allocation when the CBOR array has a definite length. | Read definite length up front, allocate `T[length]` directly, fall back to `List<T>` only for indefinite-length arrays. | Open |
| GAP-12 | NuGet publishing | release.md documents a publish flow (`dotnet nuget push ... NCbor.Generator.1.0.0.nupkg`) as routine process, but `git tag` is empty and no package has ever been published. | Either publish once the library is ready, or keep release.md framed as "process for when we do this" rather than implying it already happened. | No code change — track as a release milestone, not a doc bug (already reworded per DOC-4/DOC-5). | Open |

---

## Already tracked in roadmap.md (not duplicated here)

`roadmap.md` already carries detailed Phase 2.5/3/4/5 breakdowns (effort estimates,
dependencies) for several of the above (circular reference protection, custom converters,
constructor deserialization, attribute completion, CI/CD, benchmarking, NuGet publishing).
This file is the flat, no-marketing-language version of the same backlog — treat `roadmap.md`
as the narrative/strategic view and this file as the "is it actually done yet" checklist.
Known low-priority unsupported types not broken out above: `TimeSpan`, `Uri`.
