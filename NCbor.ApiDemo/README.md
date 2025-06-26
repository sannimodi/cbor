# CBOR Serialization API Demo

A minimal API demo showcasing CBOR serialization in ASP.NET Core using the NCbor library.

## Features

This demo provides a complete template for implementing CBOR serialization in a minimal API application, including:

- ✅ **CBOR Endpoints**: GET/POST endpoints that serialize/deserialize data using CBOR format
- ✅ **Source Generation**: Uses compile-time code generation for optimal performance
- ✅ **Comparison Endpoints**: JSON endpoints for direct comparison with CBOR
- ✅ **Swagger Integration**: Full OpenAPI documentation and testing interface
- ✅ **Error Handling**: Proper error responses for invalid CBOR data
- ✅ **CORS Support**: Configured for development and testing

## API Endpoints

### CBOR Endpoints (Content-Type: application/cbor)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/users` | Get all users in CBOR format |
| GET | `/api/users/{id}` | Get user by ID in CBOR format |
| POST | `/api/users/cbor` | Create user from CBOR data |

### JSON Endpoints (for comparison)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/users/json` | Get all users in JSON format |
| GET | `/api/health` | Health check endpoint |
| GET | `/api/cbor/types` | CBOR type demonstration info |

## Running the Demo

```bash
# Build and run the demo
dotnet run --project NCbor.ApiDemo

# The API will be available at:
# - HTTPS: https://localhost:58844
# - HTTP: http://localhost:58845
# - Swagger UI: https://localhost:58844/swagger
```

## Testing CBOR Endpoints

### Using curl

```bash
# Get all users (CBOR response)
curl -H "Accept: application/cbor" https://localhost:58844/api/users

# Get users as JSON for comparison
curl https://localhost:58844/api/users/json

# Create a user with CBOR data (requires CBOR encoding)
curl -X POST -H "Content-Type: application/cbor" \
  --data-binary @user.cbor \
  https://localhost:58844/api/users/cbor
```

### Using HTTP Clients

Tools like Postman, Insomnia, or HTTP clients can be used to test the endpoints:

1. Set `Content-Type: application/cbor` for POST requests
2. Set `Accept: application/cbor` for CBOR responses
3. Use binary data for CBOR payloads

## Implementation Details

### Models

```csharp
public class SimpleUser
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
}
```

### CBOR Context

```csharp
[NCborSerializable(typeof(SimpleUser))]
[NCborSerializable(typeof(List<SimpleUser>))]
[NCborSourceGenerationOptions(PropertyNamingPolicy = NCborKnownNamingPolicy.CamelCase)]
public partial class SimpleCborContext : NCborSerializerContext { }
```

### CBOR Serialization

```csharp
// Serialize to CBOR
var cborData = NCborSerializer.Serialize(users, cborContext.ListOfSimpleUser);
return Results.Bytes(cborData, "application/cbor");

// Deserialize from CBOR
var user = NCborSerializer.Deserialize(cborData, cborContext.SimpleUser);
```

## Project Structure

```
NCbor.ApiDemo/
├── Models.cs              # User model and CBOR context
├── Program.cs             # Minimal API configuration and endpoints
├── README.md              # This documentation
└── NCbor.ApiDemo.csproj
```

## Key Features Demonstrated

1. **Source Generation**: Compile-time generation of serialization code
2. **AOT Compatibility**: No runtime reflection, works with Native AOT
3. **Performance**: Efficient binary serialization with CBOR
4. **Type Safety**: Strongly-typed serialization contexts
5. **Error Handling**: Proper exception handling for malformed data
6. **Integration**: Seamless integration with ASP.NET Core minimal APIs

## Benefits of CBOR vs JSON

- **Smaller Size**: CBOR typically produces 20-30% smaller payloads
- **Faster Processing**: Binary format is faster to parse than text
- **Type Preservation**: Better handling of numeric types and dates
- **Extensibility**: Built-in support for custom tags and extensions

## Development Notes

This demo showcases a production-ready template for implementing CBOR APIs:

- Uses the latest .NET minimal API patterns
- Follows ASP.NET Core best practices
- Includes comprehensive error handling
- Provides both CBOR and JSON endpoints for comparison
- Ready for deployment and scaling

## Next Steps

To extend this demo:

1. Add authentication and authorization
2. Implement additional CRUD operations
3. Add database persistence
4. Include request/response logging
5. Add comprehensive validation
6. Implement caching strategies