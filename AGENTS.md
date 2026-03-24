# Repository Guidelines

## Project Structure & Module Organization
`NCbor/` contains the runtime serializer API and shared attributes. `NCbor.Generator/` contains the Roslyn source generator that emits type-specific CBOR serializers. `NCbor.Tests/` is the xUnit test suite, `NCbor.Demo/` is the console demo, `NCbor.ApiDemo/` is the ASP.NET Core minimal API sample, and `NCborSample/` is the manual sandbox for validating new serialization behavior before generator changes. Work from `NCbor.sln`.

## Build, Test, and Development Commands
Use the .NET CLI from the repo root:

- `dotnet build NCbor.sln` builds all projects.
- `dotnet test NCbor.Tests\NCbor.Tests.csproj --nologo` runs the full test suite.
- `dotnet test NCbor.Tests\NCbor.Tests.csproj --collect:"XPlat Code Coverage"` collects coverage via `coverlet.collector`.
- `dotnet run --project NCbor.Demo` runs the console feature demo.
- `dotnet run --project NCbor.ApiDemo` starts the API sample on the URLs in `NCbor.ApiDemo/Properties/launchSettings.json`.
- `dotnet run --project NCborSample` is useful for experimental/manual validation.

## Coding Style & Naming Conventions
Follow the existing C# style in-tree: file-scoped namespaces, nullable enabled, implicit usings enabled, and modern C# syntax where it improves clarity. Keep one primary type per file and use each project's `GlobalUsings.cs` for shared imports. Public types and members in `NCbor/` should keep XML documentation. Use `PascalCase` for types and members, `_camelCase` for private fields, and descriptive generated-context property names such as `ListOfPerson` or `DictionaryOfStringAndInt32`.

## Testing Guidelines
Tests use xUnit with FluentAssertions. Follow the existing `Method_Scenario_ExpectedResult` naming pattern, for example `Deserialize_ValidCborData_ReturnsCorrectModel`. Add focused tests beside the relevant area (`NCborArrayTests.cs`, `SourceGeneratorTests.cs`, etc.). Any change in `NCbor.Generator/` should include runtime coverage in `NCbor.Tests/`, and complex serialization features should be proven in `NCborSample/` first when practical.

## Commit & Pull Request Guidelines
Recent history follows short, imperative, Conventional Commit-style prefixes such as `feat:` and `docs:`. Keep commits scoped and readable. There is no PR template in the repo, so include: what changed, why it changed, linked issue(s), and the commands you ran. Include sample payloads or screenshots only when demo or API behavior changed.
