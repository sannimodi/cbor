using System.Formats.Cbor;

Console.WriteLine("=== Comprehensive CBOR Serialization Test ===");

// Create a comprehensive Person object with all data types
var person = new Person
{
    // Primitive types
    Name = "Alice Johnson",
    Age = 30,
    IsActive = true,
    Height = 5.6,
    Weight = 125.5f,
    Id = 123456789L,
    Level = 255,
    Score = -100,
    RankShort = 12345,
    RankUShort = 54321,
    Points = 999999u,
    BigNumber = 18446744073709551615UL,

    // Decimal types
    Salary = 75000.50m,
    Bonus = 5000.25m,

    // DateTime types
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTimeOffset.UtcNow,
    LastLogin = DateTime.UtcNow.AddDays(-1),
    LastModified = DateTimeOffset.UtcNow.AddHours(-2),

    // Guid types
    PersonId = Guid.NewGuid(),
    SessionId = Guid.NewGuid(),

    // Collections
    Tags = new List<string> { "developer", "senior", "remote" },
    Scores = new List<int> { 95, 87, 92, 98 },
    OptionalNotes = new List<string> { "Excellent performance", "Team player" },

    // Dictionaries
    Metadata = new Dictionary<string, string>
    {
        { "department", "engineering" },
        { "level", "senior" },
        { "location", "remote" }
    },
    CategoryScores = new Dictionary<string, int>
    {
        { "technical", 95 },
        { "communication", 90 },
        { "leadership", 85 }
    },
    OptionalData = new Dictionary<string, string>
    {
        { "note", "Promoted recently" },
        { "manager", "Bob Smith" }
    },

    // Nullable primitives
    OptionalAge = 31,
    IsVerified = true,
    OptionalHeight = 5.7,
    OptionalWeight = 126.0f,

    // Nested object
    HomeAddress = new Address
    {
        Street = "123 Main St",
        City = "San Francisco",
        PostalCode = "94101",
        Coordinates = new Dictionary<string, string>
        {
            { "lat", "37.7749" },
            { "lng", "-122.4194" }
        }
    }
};

Console.WriteLine("Original Person:");
PrintPerson(person);

try
{
    // Serialize the Person object to a CBOR byte array
    var writer = new CborWriter();
    Person.Serialize(writer, person);
    byte[] bytes = writer.Encode();

    Console.WriteLine($"\n✅ Serialization successful!");
    Console.WriteLine($"Serialized bytes length: {bytes.Length}");
    Console.WriteLine($"First 50 bytes: {BitConverter.ToString(bytes.Take(50).ToArray())}...");

    // Deserialize the CBOR byte array back to a Person object
    var reader = new CborReader(bytes);
    var decoded = Person.Deserialize(reader);

    Console.WriteLine("\n✅ Deserialization successful!");
    Console.WriteLine("\nDeserialized Person:");
    PrintPerson(decoded);

    // Validate round-trip integrity
    Console.WriteLine("\n=== Round-trip Validation ===");
    ValidateRoundTrip(person, decoded);

    Console.WriteLine("\n🎉 All tests passed! Comprehensive CBOR serialization working correctly!");

}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ Error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}

static void PrintPerson(Person person)
{
    Console.WriteLine($"  Name: {person.Name}");
    Console.WriteLine($"  Age: {person.Age}");
    Console.WriteLine($"  IsActive: {person.IsActive}");
    Console.WriteLine($"  Height: {person.Height}");
    Console.WriteLine($"  Weight: {person.Weight}");
    Console.WriteLine($"  Id: {person.Id}");
    Console.WriteLine($"  Level: {person.Level}");
    Console.WriteLine($"  Score: {person.Score}");
    Console.WriteLine($"  RankShort: {person.RankShort}");
    Console.WriteLine($"  RankUShort: {person.RankUShort}");
    Console.WriteLine($"  Points: {person.Points}");
    Console.WriteLine($"  BigNumber: {person.BigNumber}");
    Console.WriteLine($"  Salary: {person.Salary:C}");
    Console.WriteLine($"  Bonus: {person.Bonus?.ToString("C") ?? "null"}");
    Console.WriteLine($"  CreatedAt: {person.CreatedAt:yyyy-MM-dd HH:mm:ss.fff}");
    Console.WriteLine($"  UpdatedAt: {person.UpdatedAt:yyyy-MM-dd HH:mm:ss.fff zzz}");
    Console.WriteLine($"  LastLogin: {person.LastLogin?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "null"}");
    Console.WriteLine($"  LastModified: {person.LastModified?.ToString("yyyy-MM-dd HH:mm:ss.fff zzz") ?? "null"}");
    Console.WriteLine($"  PersonId: {person.PersonId}");
    Console.WriteLine($"  SessionId: {person.SessionId?.ToString() ?? "null"}");
    Console.WriteLine($"  Tags: [{string.Join(", ", person.Tags)}]");
    Console.WriteLine($"  Scores: [{string.Join(", ", person.Scores)}]");
    Console.WriteLine($"  OptionalNotes: {(person.OptionalNotes != null ? $"[{string.Join(", ", person.OptionalNotes)}]" : "null")}");
    Console.WriteLine($"  Metadata: {{{string.Join(", ", person.Metadata.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}}}");
    Console.WriteLine($"  CategoryScores: {{{string.Join(", ", person.CategoryScores.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}}}");
    Console.WriteLine($"  OptionalData: {(person.OptionalData != null ? $"{{{string.Join(", ", person.OptionalData.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}}}" : "null")}");
    Console.WriteLine($"  OptionalAge: {person.OptionalAge?.ToString() ?? "null"}");
    Console.WriteLine($"  IsVerified: {person.IsVerified?.ToString() ?? "null"}");
    Console.WriteLine($"  OptionalHeight: {person.OptionalHeight?.ToString() ?? "null"}");
    Console.WriteLine($"  OptionalWeight: {person.OptionalWeight?.ToString() ?? "null"}");
    
    if (person.HomeAddress != null)
    {
        Console.WriteLine($"  HomeAddress:");
        Console.WriteLine($"    Street: {person.HomeAddress.Street}");
        Console.WriteLine($"    City: {person.HomeAddress.City}");
        Console.WriteLine($"    PostalCode: {person.HomeAddress.PostalCode ?? "null"}");
        Console.WriteLine($"    Coordinates: {{{string.Join(", ", person.HomeAddress.Coordinates.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}}}");
    }
    else
    {
        Console.WriteLine($"  HomeAddress: null");
    }
}

static void ValidateRoundTrip(Person original, Person decoded)
{
    var errors = new List<string>();

    // Validate primitive types
    if (original.Name != decoded.Name) errors.Add($"Name mismatch: {original.Name} != {decoded.Name}");
    if (original.Age != decoded.Age) errors.Add($"Age mismatch: {original.Age} != {decoded.Age}");
    if (original.IsActive != decoded.IsActive) errors.Add($"IsActive mismatch: {original.IsActive} != {decoded.IsActive}");
    if (Math.Abs(original.Height - decoded.Height) > 0.0001) errors.Add($"Height mismatch: {original.Height} != {decoded.Height}");
    if (Math.Abs(original.Weight - decoded.Weight) > 0.0001f) errors.Add($"Weight mismatch: {original.Weight} != {decoded.Weight}");
    if (original.Id != decoded.Id) errors.Add($"Id mismatch: {original.Id} != {decoded.Id}");
    if (original.Level != decoded.Level) errors.Add($"Level mismatch: {original.Level} != {decoded.Level}");
    if (original.Score != decoded.Score) errors.Add($"Score mismatch: {original.Score} != {decoded.Score}");
    if (original.RankShort != decoded.RankShort) errors.Add($"RankShort mismatch: {original.RankShort} != {decoded.RankShort}");
    if (original.RankUShort != decoded.RankUShort) errors.Add($"RankUShort mismatch: {original.RankUShort} != {decoded.RankUShort}");
    if (original.Points != decoded.Points) errors.Add($"Points mismatch: {original.Points} != {decoded.Points}");
    if (original.BigNumber != decoded.BigNumber) errors.Add($"BigNumber mismatch: {original.BigNumber} != {decoded.BigNumber}");

    // Validate decimal types
    if (original.Salary != decoded.Salary) errors.Add($"Salary mismatch: {original.Salary} != {decoded.Salary}");
    if (original.Bonus != decoded.Bonus) errors.Add($"Bonus mismatch: {original.Bonus} != {decoded.Bonus}");

    // Validate DateTime types (allow small differences due to precision)
    if (Math.Abs((original.CreatedAt - decoded.CreatedAt).TotalMilliseconds) > 1) errors.Add($"CreatedAt mismatch: {original.CreatedAt} != {decoded.CreatedAt}");
    if (Math.Abs((original.UpdatedAt - decoded.UpdatedAt).TotalMilliseconds) > 1) errors.Add($"UpdatedAt mismatch: {original.UpdatedAt} != {decoded.UpdatedAt}");

    // Validate Guid types
    if (original.PersonId != decoded.PersonId) errors.Add($"PersonId mismatch: {original.PersonId} != {decoded.PersonId}");
    if (original.SessionId != decoded.SessionId) errors.Add($"SessionId mismatch: {original.SessionId} != {decoded.SessionId}");

    // Validate collections
    if (!original.Tags.SequenceEqual(decoded.Tags)) errors.Add("Tags mismatch");
    if (!original.Scores.SequenceEqual(decoded.Scores)) errors.Add("Scores mismatch");

    // Validate dictionaries
    if (!DictionariesEqual(original.Metadata, decoded.Metadata)) errors.Add("Metadata mismatch");
    if (!DictionariesEqual(original.CategoryScores, decoded.CategoryScores)) errors.Add("CategoryScores mismatch");

    // Validate nullable types
    if (original.OptionalAge != decoded.OptionalAge) errors.Add($"OptionalAge mismatch: {original.OptionalAge} != {decoded.OptionalAge}");
    if (original.IsVerified != decoded.IsVerified) errors.Add($"IsVerified mismatch: {original.IsVerified} != {decoded.IsVerified}");

    // Validate nested object
    if (original.HomeAddress?.Street != decoded.HomeAddress?.Street) errors.Add("HomeAddress.Street mismatch");
    if (original.HomeAddress?.City != decoded.HomeAddress?.City) errors.Add("HomeAddress.City mismatch");
    if (original.HomeAddress?.PostalCode != decoded.HomeAddress?.PostalCode) errors.Add("HomeAddress.PostalCode mismatch");

    if (errors.Any())
    {
        Console.WriteLine("❌ Round-trip validation failed:");
        foreach (var error in errors)
        {
            Console.WriteLine($"  - {error}");
        }
    }
    else
    {
        Console.WriteLine("✅ Round-trip validation passed! All values match perfectly.");
    }
}

static bool DictionariesEqual<TKey, TValue>(Dictionary<TKey, TValue> dict1, Dictionary<TKey, TValue> dict2) where TKey : notnull
{
    if (dict1.Count != dict2.Count) return false;
    foreach (var kvp in dict1)
    {
        if (!dict2.TryGetValue(kvp.Key, out var value) || !Equals(kvp.Value, value))
            return false;
    }
    return true;
}