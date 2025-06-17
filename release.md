# CBOR Serialization Library Release Notes

## Overview

This project provides a .NET library for CBOR (Concise Binary Object Representation) serialization using source generation. It is designed to be fully AOT-compatible and leverages System.Formats.Cbor for the underlying CBOR operations.

## Project Structure

- **CborSerialization**: The main runtime library that provides the core serialization logic and base classes.
- **CborSerialization.Generator**: A source generator project that analyzes types marked with `[CborSerializable]` and generates optimized serialization/deserialization code.
- **CborSerialization.Demo**: A demo project showcasing the usage of the library.

## Local Development

### Building the Project

1. **Clone the Repository**:
   ```bash
   git clone https://github.com/yourusername/cbor.git
   cd cbor
   ```

2. **Build the Solution**:
   ```bash
   dotnet build CbotSerialization.sln
   ```

### Using the Source Generator Locally

- The source generator is included as a project reference in the demo project.
- To use the generator in your project, add the following to your `.csproj`:
  ```xml
  <ItemGroup>
      <ProjectReference Include="..\CborSerialization\CborSerialization.csproj" />
      <Analyzer Include="..\CborSerialization.Generator\bin\Debug\netstandard2.0\CborSerialization.Generator.dll" />
  </ItemGroup>
  ```

### Why Explicit Analyzer Reference is Needed

- When developing locally, the source generator is not automatically included as an analyzer.
- You must explicitly add the generator DLL as an analyzer to ensure it runs during the build process.
- This is different from NuGet packages, where analyzers are automatically included.

## NuGet Packaging

### Preparing the Generator for NuGet

1. **Update the Generator Project File**:
   Add the following to `CborSerialization.Generator.csproj`:
   ```xml
   <PropertyGroup>
       <IncludeBuildOutput>false</IncludeBuildOutput>
       <IncludeAnalyzer>true</IncludeAnalyzer>
   </PropertyGroup>
   ```

2. **Pack the Generator**:
   ```bash
   dotnet pack CborSerialization.Generator/CborSerialization.Generator.csproj
   ```

### Publishing to NuGet

1. **Create a NuGet Account** (if you don't have one).
2. **Get Your API Key** from the NuGet website.
3. **Publish the Package**:
   ```bash
   dotnet nuget push CborSerialization.Generator/bin/Debug/CborSerialization.Generator.1.0.0.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
   ```

### Using the NuGet Package

- Once published, users can install the package:
  ```bash
  dotnet add package CborSerialization.Generator
  ```
- The analyzer will be automatically included, and no explicit `<Analyzer>` reference is needed.

## Source Generator Details

### How It Works

- The source generator analyzes types marked with `[CborSerializable]`.
- It generates optimized serialization and deserialization methods.
- The generated code is added to the project during the build process.

### Key Components

- **CborSourceGenerator**: The main generator class that implements `IIncrementalGenerator`.
- **CborSyntaxReceiver**: Collects syntax nodes for processing.
- **SerializationCodeGenerator**: Generates the serialization and deserialization code.

## Troubleshooting

- **Build Errors**: Ensure the generator project is built before the demo project.
- **Missing Analyzer**: Verify the `<Analyzer>` reference is correctly added in the project file.
- **NuGet Issues**: Check the package version and API key.

## Future Enhancements

- Support for more complex types.
- Advanced error handling.
- Comprehensive test suite.
- Performance optimizations.

## License

This project is licensed under the terms of the LICENSE file.

---

For more details, refer to the [specification document](cbor_spec_enhanced.md). 