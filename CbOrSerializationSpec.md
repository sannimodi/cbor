# Specification File for CBOR Serialization Using Source Generators

## Objective  
Create a CBOR serialization library that mirrors the approach of `System.Text.Json`, leveraging source generators for efficient, reflection-free serialization. This specification will serve as AI instructions for generating code, ensuring compatibility with AOT compilation and high performance.

---

## 1. Overview  
- **Purpose**: Define a CBOR serialization mechanism using source generators to eliminate runtime reflection, similar to `System.Text.Json`.  
- **Target**: Generate serialization code at compile time for specified types, ensuring compatibility with AOT compilation.  
- **Dependencies**: Utilize Microsoft’s CBOR formatter (e.g., `System.Formats.Cbor`) and a custom source generator.

---

## 2. Key Features  
- **Source Generation**: Generate serialization and deserialization code at compile time.  
- **Attribute-Based Configuration**: Use attributes to specify serialization behavior (e.g., `[CborSerializable]`, `[CborIgnore]`).  
- **AOT Compatibility**: Ensure the generated code avoids reflection, making it suitable for AOT scenarios.  
- **Performance**: Optimize for speed and memory efficiency, leveraging CBOR’s compact binary format.

---

## 3. Architecture  
- **Source Generator**: A custom source generator that inspects user code, identifies types marked with `[CborSerializable]`, and generates serialization code.  
- **CBOR Formatter**: Use `System.Formats.Cbor` for low-level CBOR encoding and decoding.  
- **Context Class**: Introduce a `CborSerializerContext` to manage serialization metadata, similar to `JsonSerializerContext`.

---

## 4. Usage Pattern  
### User Code  
```csharp
[CborSerializable]
public class Person
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    [CborIgnore]
    public int Age { get; set; }
}

[CborSerializable(typeof(Person))]
public partial class MyCborContext : CborSerializerContext { }
```

### Serialization  
```csharp
var person = new Person { FirstName = "John", LastName = "Doe" };
byte[] cborData = CborSerializer.Serialize(person, MyCborContext.Default.Person);
```

### Deserialization  
```csharp
Person deserialized = CborSerializer.Deserialize<Person>(cborData, MyCborContext.Default.Person);
```

---

## 5. Source Generator Requirements  
- **Input**: Analyze types marked with `[CborSerializable]` or specified in `[CborSerializable(typeof(T))]`.  
- **Output**: Generate partial classes or extension methods for serialization and deserialization.  
- **Handling**:  
  - Generate code for properties and fields, respecting attributes like `[CborIgnore]`.  
  - Support collections, nested types, and custom converters.  
  - Ensure generated code is AOT-friendly (no reflection).

---

## 6. Attribute Definitions  
- **`[CborSerializable]`**: Marks a type for CBOR serialization.  
- **`[CborIgnore]`**: Excludes a property or field from serialization.  
- **`[CborProperty("name")]`**: Specifies a custom name for the CBOR property.  
- **`[CborConverter(typeof(T))]`**: Specifies a custom converter for a type or member.

---

## 7. CborSerializerContext  
- **Purpose**: Manages serialization metadata for specified types.  
- **Implementation**: A partial class where the source generator adds serialization logic.  
- **Example**:  
  ```csharp
  public partial class MyCborContext : CborSerializerContext
  {
      // Source generator adds methods like GetCborTypeInfo<T>()
  }
  ```

---

## 8. Serialization API  
- **`CborSerializer.Serialize<T>(T value, CborTypeInfo<T> typeInfo)`**: Serializes an object to CBOR.  
- **`CborSerializer.Deserialize<T>(byte[] cborData, CborTypeInfo<T> typeInfo)`**: Deserializes CBOR data to an object.  
- **Options**: Support custom options (e.g., `CborSerializerOptions`) for indentation, naming policies, etc.

---

## 9. AOT Considerations  
- **No Reflection**: Resolve all type information at compile time.  
- **Static Code Generation**: Generate static methods for serialization and deserialization.  
- **Compatibility**: Ensure the generated code works with AOT’s `IlcDisableReflection` option.

---

## 10. Error Handling  
- **Compile-Time Errors**: Report errors for unsupported types or misconfigurations.  
- **Runtime Errors**: Handle deserialization errors gracefully (e.g., missing properties, type mismatches).

---

## 11. Testing  
- **Unit Tests**: Verify serialization and deserialization for various types (primitives, collections, nested objects).  
- **AOT Tests**: Ensure functionality in AOT-compiled applications.  
- **Performance Benchmarks**: Compare with reflection-based serialization.

---

## 12. Documentation  
- **Usage Guide**: Provide examples of attribute usage and serialization calls.  
- **API Reference**: Document all public APIs, including generated methods.  
- **AOT Instructions**: Explain how to configure AOT compilation with the library.

---

## Conclusion  
This specification outlines a CBOR serialization library that leverages source generators for efficient, AOT-compatible serialization, mirroring the approach of `System.Text.Json`. By following these guidelines, the generated code ensures high performance, reflection-free operation, and seamless integration into .NET projects.