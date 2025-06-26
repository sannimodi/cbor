using NCbor;

using NCbor.ApiDemo;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

var cborContext = new SimpleCborContext();

// In-memory data store
var users = new List<SimpleUser>
{
    new() { Id = Guid.NewGuid(), Name = "Alice Johnson", Email = "alice@example.com", Age = 28 },
    new() { Id = Guid.NewGuid(), Name = "Bob Smith", Email = "bob@example.com", Age = 35 },
    new() { Id = Guid.NewGuid(), Name = "Carol Davis", Email = "carol@example.com", Age = 42 }
};

// === CBOR API ENDPOINTS ===

// GET /api/users - Return users as CBOR
app.MapGet("/api/users", () =>
{
    var cborData = NCborSerializer.Serialize(users, cborContext.ListOfSimpleUser);
    return Results.Bytes(cborData, "application/cbor");
})
.WithName("GetUsersCbor")
.WithSummary("Get all users (CBOR format)")
.Produces(200, contentType: "application/cbor");

// GET /api/users/{id} - Get single user as CBOR
app.MapGet("/api/users/{id:guid}", (Guid id) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    if (user == null)
    {
        return Results.NotFound();
    }
    
    var cborData = NCborSerializer.Serialize(user, cborContext.SimpleUser);
    return Results.Bytes(cborData, "application/cbor");
})
.WithName("GetUserByIdCbor")
.WithSummary("Get user by ID (CBOR format)")
.Produces(200, contentType: "application/cbor")
.Produces(404);

// POST /api/users/cbor - Create user from CBOR data
app.MapPost("/api/users/cbor", async (HttpRequest request) =>
{
    try
    {
        using var memoryStream = new MemoryStream();
        await request.Body.CopyToAsync(memoryStream);
        var cborData = memoryStream.ToArray();
        
        var user = NCborSerializer.Deserialize(cborData, cborContext.SimpleUser);
        if (user == null)
        {
            return Results.BadRequest("Invalid CBOR data");
        }
        
        var newUser = new SimpleUser 
        { 
            Id = Guid.NewGuid(), 
            Name = user.Name, 
            Email = user.Email, 
            Age = user.Age 
        };
        users.Add(newUser);
        
        var responseCbor = NCborSerializer.Serialize(newUser, cborContext.SimpleUser);
        return Results.Bytes(responseCbor, "application/cbor");
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"CBOR deserialization failed: {ex.Message}");
    }
})
.WithName("CreateUserCbor")
.WithSummary("Create user from CBOR data")
.Accepts<SimpleUser>("application/cbor")
.Produces(200, contentType: "application/cbor")
.Produces(400);

// === JSON ENDPOINTS FOR COMPARISON ===

// GET /api/users/json - Return users as JSON
app.MapGet("/api/users/json", () => users)
.WithName("GetUsersJson")
.WithSummary("Get all users (JSON format)");

// GET /api/health - Simple health check
app.MapGet("/api/health", () => new { 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    cborSupported = true,
    userCount = users.Count
})
.WithName("HealthCheck")
.WithSummary("Health check endpoint");

// === CBOR DEMONSTRATION ENDPOINTS ===

// GET /api/cbor/types - Demonstrates CBOR with various data types
app.MapGet("/api/cbor/types", () =>
{
    // This endpoint returns JSON to show the data structure
    // but also provides CBOR endpoint for the same data
    var demoData = new
    {
        message = "CBOR Type Demonstration",
        timestamp = DateTime.UtcNow,
        guid = Guid.NewGuid(),
        numbers = new { integer = 42, floating = 3.14159 },
        collections = new
        {
            simpleArray = new[] { 1, 2, 3, 4, 5 },
            stringList = new List<string> { "apple", "banana", "cherry" }
        },
        booleans = new { isTrue = true, isFalse = false },
        nullValue = (string?)null
    };
    
    return Results.Ok(new {
        data = demoData,
        note = "Use /api/users for CBOR format examples",
        cborEndpoints = new[] {
            "GET /api/users (CBOR)",
            "GET /api/users/{id} (CBOR)",
            "POST /api/users/cbor (CBOR input/output)"
        }
    });
})
.WithName("CborTypeDemo")
.WithSummary("Demonstrates various data types available for CBOR serialization");

Console.WriteLine("🚀 CBOR API Demo starting...");
Console.WriteLine("📖 Swagger UI: https://localhost:5001/swagger");
Console.WriteLine();
Console.WriteLine("🔗 CBOR Endpoints:");
Console.WriteLine("   GET    /api/users           - Get all users (CBOR)");
Console.WriteLine("   GET    /api/users/{id}      - Get user by ID (CBOR)");
Console.WriteLine("   POST   /api/users/cbor      - Create user (CBOR input/output)");
Console.WriteLine();
Console.WriteLine("🔗 JSON Endpoints (for comparison):");
Console.WriteLine("   GET    /api/users/json      - Get all users (JSON)");
Console.WriteLine("   GET    /api/health          - Health check (JSON)");
Console.WriteLine("   GET    /api/cbor/types      - Type demo info (JSON)");
Console.WriteLine();
Console.WriteLine("💡 CBOR endpoints return Content-Type: application/cbor");
Console.WriteLine("💡 Use tools like curl or Postman to test CBOR endpoints");

await app.RunAsync();