# CBOR Serialization Library Release Notes

## Overview

This project provides a .NET library for CBOR (Concise Binary Object Representation) serialization using source generation. It is designed to be fully AOT-compatible and leverages System.Formats.Cbor for the underlying CBOR operations.

## Project Structure

- **NCbor**: The main runtime library that provides the core serialization logic and base classes.
- **NCbor.Generator**: A source generator project that analyzes types marked with `[NCborSerializable]` and generates optimized serialization/deserialization code.
- **NCbor.Demo**: A demo project showcasing the usage of the library.

## Local Development

### Building the Project

1. **Clone the Repository**:
   ```bash
   git clone https://github.com/sannimodi/cbor.git
   cd cbor
   ```

2. **Build the Solution**:
   ```bash
   dotnet build NCbor.sln
   ```

### Using the Source Generator Locally

- The source generator is included as a project reference in the demo project.
- To use the generator in your project, add the following to your `.csproj`:
  ```xml
  <ItemGroup>
      <ProjectReference Include="..\NCbor\NCbor.csproj" />
      <ProjectReference Include="..\NCbor.Generator\NCbor.Generator.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  </ItemGroup>
  ```

### Why Explicit Analyzer Reference is Needed

- When developing locally, the source generator is not automatically included as an analyzer.
- Reference the generator project with `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"` so it runs as an analyzer (always built in the right configuration) instead of being linked as a normal assembly. Pointing an `<Analyzer>` item at a `bin/Debug` DLL breaks Release builds and clean clones.
- This is different from NuGet packages, where analyzers are automatically included.

## NuGet Packaging

### Packaging Metadata (already configured, as of 2026-07-04)

Both `NCbor.csproj` and `NCbor.Generator.csproj` already carry `PackageId`/`Version`/`Authors`/
`Description`/`PackageLicenseExpression`/`PackageProjectUrl`/`PackageReadmeFile`. `Authors` is
`Sanni Modi` and both URLs point at https://github.com/sannimodi/cbor (settled 2026-11-13). Bump
`Version` before each publish. Package versions for every project live in `Directory.Packages.props`
(central package management).

The generator project packs its DLL as an analyzer, not a normal library reference. The
previous version of this doc described a nonexistent `<IncludeAnalyzer>true</IncludeAnalyzer>`
MSBuild property — there is no such property. The actual pattern, already applied in
`NCbor.Generator.csproj`:
```xml
<PropertyGroup>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <DevelopmentDependency>true</DevelopmentDependency>
</PropertyGroup>

<ItemGroup>
    <None Include="$(OutputPath)\$(AssemblyName).dll" Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
</ItemGroup>
```
This has been verified by actually running `dotnet pack` and inspecting the resulting `.nupkg`
— the DLL lands under `analyzers/dotnet/cs/`, not `lib/`.

### Building the Packages

```bash
dotnet pack NCbor/NCbor.csproj --configuration Release
dotnet pack NCbor.Generator/NCbor.Generator.csproj --configuration Release
```

~~**Known issue to resolve before publishing**: packing `NCbor` emitted `NU5104` because it
depended on the prerelease `System.Formats.Cbor 10.0.0-preview.5.25277.114`.~~ **Fixed** (see
[GAPS.md](GAPS.md) GAP-12): now pinned to stable `System.Formats.Cbor 10.0.15`; `dotnet pack` no
longer emits `NU5104`.

### Publishing to NuGet

**Not yet done for either package** — no version has ever been published (`git tag` is empty).
This is a deliberate, credentialed action for whoever maintains the NuGet account, not something
to run casually:

1. **Create a NuGet Account** (if you don't have one).
2. **Get Your API Key** from the NuGet website.
3. **Publish the Package**:
   ```bash
   dotnet nuget push NCbor/bin/Release/NCbor.1.0.0.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
   dotnet nuget push NCbor.Generator/bin/Release/NCbor.Generator.1.0.0.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
   ```

### Using the NuGet Package

- Once published, users can install the package:
  ```bash
  dotnet add package NCbor.Generator
  ```
- The analyzer will be automatically included, and no explicit `<Analyzer>` reference is needed.

## Source Generator Details

### How It Works

- The source generator analyzes types marked with `[NCborSerializable]`.
- It generates optimized serialization and deserialization methods.
- The generated code is added to the project during the build process.

### Key Components

- **NCborSourceGenerator**: The main generator class that implements `IIncrementalGenerator`.
- **SerializationCodeGenerator**: Generates the serialization and deserialization code.

## Troubleshooting

- **Build Errors**: Ensure the generator project is built before the demo project.
- **Missing Analyzer**: Verify the `<Analyzer>` reference is correctly added in the project file.
- **NuGet Issues**: Check the package version and API key.

## Future Enhancements

- Support for more complex types (see [GAPS.md](GAPS.md) for the current tracked list).
- Advanced error handling.
- Performance optimizations (benchmarking suite not yet built — GAPS.md GAP-3).

## License

This project is licensed under the terms of the LICENSE file.

---

For more details, refer to the [specification document](cbor-serialization-specs.md) and [GAPS.md](GAPS.md) for what's actually implemented versus planned.

