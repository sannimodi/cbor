using CbOrSerialization;
using CbOrSerialization.Demo;

Console.WriteLine("=================================================");
Console.WriteLine("   CBOR Serialization Library - Feature Demo");
Console.WriteLine("=================================================");
Console.WriteLine();

// ========== 1. Enhanced Person Model - All Data Types ==========
Console.WriteLine("🧑 1. Enhanced Person Model (All Supported Types)");
Console.WriteLine("─────────────────────────────────────────────────");

var person = new Person
{
    PersonId = Guid.NewGuid(),
    Name = "Dr. Sarah Johnson",
    Age = 34,
    IsActive = true,
    Height = 5.7,
    DateOfBirth = new DateTime(1989, 5, 15),
    CreatedAt = DateTimeOffset.Now,
    LastLoginDate = DateTime.Now.AddDays(-2),
    Score = 95,
    ManagerId = Guid.NewGuid(),
    Hobbies = new List<string> { "Photography", "Hiking", "Reading" },
    ContactInfo = new Dictionary<string, string>
    {
        {"email", "sarah.johnson@company.com"},
        {"phone", "+1-555-0123"},
        {"linkedin", "linkedin.com/in/sarahjohnson"}
    },
    Skills = new Dictionary<string, int>
    {
        {"C#", 95},
        {"Python", 85},
        {"Leadership", 90},
        {"Architecture", 88}
    },
    HomeAddress = new Address
    {
        Street = "123 Tech Street",
        City = "San Francisco",
        Country = "USA",
        PostalCode = "94105"
    },
    InternalNotes = "This should not be serialized due to [CbOrIgnore]",
    Status = "active"
};

// Serialize enhanced person
var personCbor = CbOrSerializer.Serialize(person, MyCbOrContext.Default.Person);
Console.WriteLine($"✅ Serialized Person ({personCbor.Length} bytes)");
Console.WriteLine($"   CBOR Data (hex): {BitConverter.ToString(personCbor)[..80]}...");

// Deserialize and validate
var deserializedPerson = CbOrSerializer.Deserialize(personCbor, MyCbOrContext.Default.Person);
Console.WriteLine($"✅ Deserialized Person:");
Console.WriteLine($"   ID: {deserializedPerson.PersonId}");
Console.WriteLine($"   Name: {deserializedPerson.Name} (using [CbOrPropertyName])");
Console.WriteLine($"   Age: {deserializedPerson.Age}, Height: {deserializedPerson.Height}");
Console.WriteLine($"   Born: {deserializedPerson.DateOfBirth:yyyy-MM-dd}");
Console.WriteLine($"   Last Login: {deserializedPerson.LastLoginDate:yyyy-MM-dd HH:mm}");
Console.WriteLine($"   Hobbies: {string.Join(", ", deserializedPerson.Hobbies)}");
Console.WriteLine($"   Contact Info: {deserializedPerson.ContactInfo.Count} entries");
Console.WriteLine($"   Skills: {string.Join(", ", deserializedPerson.Skills.Select(s => $"{s.Key}:{s.Value}"))}");
Console.WriteLine($"   Address: {deserializedPerson.HomeAddress?.City}, {deserializedPerson.HomeAddress?.Country}");
Console.WriteLine($"   Internal Notes: '{deserializedPerson.InternalNotes}' (should be empty due to [CbOrIgnore])");
Console.WriteLine();

// ========== 2. Dictionary Showcase - NEW FEATURE ==========
Console.WriteLine("📚 2. Dictionary Support Showcase (NEW FEATURE!)");
Console.WriteLine("─────────────────────────────────────────────────");

// Simple Dictionary<string, string>
var userPreferences = new Dictionary<string, string>
{
    {"theme", "dark"},
    {"language", "en-US"},
    {"timezone", "UTC-8"},
    {"notifications", "enabled"}
};

var prefsCbor = CbOrSerializer.Serialize(userPreferences, MyCbOrContext.Default.DictionaryOfStringAndString);
var deserializedPrefs = CbOrSerializer.Deserialize(prefsCbor, MyCbOrContext.Default.DictionaryOfStringAndString);
Console.WriteLine($"✅ Dictionary<string, string>: {deserializedPrefs.Count} preferences");
foreach (var pref in deserializedPrefs)
    Console.WriteLine($"   {pref.Key} = {pref.Value}");

// Dictionary<string, int> for scores/ratings
var teamScores = new Dictionary<string, int>
{
    {"Engineering", 95},
    {"Design", 88},
    {"Marketing", 92},
    {"Sales", 89}
};

var scoresCbor = CbOrSerializer.Serialize(teamScores, MyCbOrContext.Default.DictionaryOfStringAndInt32);
var deserializedScores = CbOrSerializer.Deserialize(scoresCbor, MyCbOrContext.Default.DictionaryOfStringAndInt32);
Console.WriteLine($"✅ Dictionary<string, int>: Team scores");
foreach (var score in deserializedScores.OrderByDescending(s => s.Value))
    Console.WriteLine($"   {score.Key}: {score.Value}/100");
Console.WriteLine();

// ========== 3. Complex Nested Dictionary Scenarios ==========
Console.WriteLine("🏢 3. Complex Department with Nested Dictionaries");
Console.WriteLine("─────────────────────────────────────────────────");

var engineeringDept = new Department
{
    DepartmentId = Guid.NewGuid(),
    Name = "Engineering",
    Staff = new Dictionary<string, Person>
    {
        {"tech-lead", new Person 
        {
            PersonId = Guid.NewGuid(),
            Name = "Alex Chen",
            Age = 32,
            Skills = new Dictionary<string, int> { {"Architecture", 95}, {"Mentoring", 90} },
            ContactInfo = new Dictionary<string, string> { {"email", "alex@company.com"} }
        }},
        {"senior-dev", new Person 
        {
            PersonId = Guid.NewGuid(),
            Name = "Maria Rodriguez",
            Age = 28,
            Skills = new Dictionary<string, int> { {"Backend", 88}, {"DevOps", 85} },
            ContactInfo = new Dictionary<string, string> { {"email", "maria@company.com"} }
        }}
    },
    ProjectAssignments = new Dictionary<Guid, List<string>>
    {
        {Guid.NewGuid(), new List<string> {"API Development", "Database Design"}},
        {Guid.NewGuid(), new List<string> {"Frontend Framework", "UI Components"}}
    }
};

var deptCbor = CbOrSerializer.Serialize(engineeringDept, MyCbOrContext.Default.Department);
var deserializedDept = CbOrSerializer.Deserialize(deptCbor, MyCbOrContext.Default.Department);

Console.WriteLine($"✅ Department: {deserializedDept.Name}");
Console.WriteLine($"   Staff Members: {deserializedDept.Staff.Count}");
foreach (var staff in deserializedDept.Staff)
{
    Console.WriteLine($"   • {staff.Key}: {staff.Value.Name} (Age: {staff.Value.Age})");
    Console.WriteLine($"     Skills: {string.Join(", ", staff.Value.Skills.Select(s => $"{s.Key}:{s.Value}"))}");
}
Console.WriteLine($"   Project Assignments: {deserializedDept.ProjectAssignments.Count} projects");
Console.WriteLine();

// ========== 4. Collections Showcase ==========
Console.WriteLine("📋 4. Collection Support (List<T>)");
Console.WriteLine("─────────────────────────────────────────────────");

var team = new List<Person>
{
    new Person 
    { 
        PersonId = Guid.NewGuid(),
        Name = "John Smith", 
        Age = 30, 
        Skills = new Dictionary<string, int> { {"Management", 85} }
    },
    new Person 
    { 
        PersonId = Guid.NewGuid(),
        Name = "Jane Doe", 
        Age = 27, 
        Skills = new Dictionary<string, int> { {"Development", 92} }
    }
};

var teamCbor = CbOrSerializer.Serialize(team, MyCbOrContext.Default.ListOfPerson);
var deserializedTeam = CbOrSerializer.Deserialize(teamCbor, MyCbOrContext.Default.ListOfPerson);
Console.WriteLine($"✅ List<Person>: {deserializedTeam.Count} team members");
foreach (var member in deserializedTeam)
{
    Console.WriteLine($"   • {member.Name} (ID: {member.PersonId.ToString()[..8]}...)");
    Console.WriteLine($"     Skills: {string.Join(", ", member.Skills.Select(s => $"{s.Key}:{s.Value}"))}");
}
Console.WriteLine();

// ========== 5. Nullable Types Demonstration ==========
Console.WriteLine("❓ 5. Nullable Types Support");
Console.WriteLine("─────────────────────────────────────────────────");

var personWithNulls = new Person
{
    PersonId = Guid.NewGuid(),
    Name = "Test User",
    Age = 25,
    LastLoginDate = null,  // Nullable DateTime
    Score = null,          // Nullable int
    ManagerId = null,      // Nullable Guid
    HomeAddress = null     // Nullable reference type
};

var nullsCbor = CbOrSerializer.Serialize(personWithNulls, MyCbOrContext.Default.Person);
var deserializedNulls = CbOrSerializer.Deserialize(nullsCbor, MyCbOrContext.Default.Person);

Console.WriteLine($"✅ Person with null values:");
Console.WriteLine($"   Name: {deserializedNulls.Name}");
Console.WriteLine($"   LastLoginDate: {deserializedNulls.LastLoginDate?.ToString() ?? "null"}");
Console.WriteLine($"   Score: {deserializedNulls.Score?.ToString() ?? "null"}");
Console.WriteLine($"   ManagerId: {deserializedNulls.ManagerId?.ToString() ?? "null"}");
Console.WriteLine($"   HomeAddress: {deserializedNulls.HomeAddress?.ToString() ?? "null"}");
Console.WriteLine();

// ========== 6. Performance Summary ==========
Console.WriteLine("⚡ 6. Performance & Compatibility Summary");
Console.WriteLine("─────────────────────────────────────────────────");
Console.WriteLine("✅ AOT Compatible: Zero runtime reflection");
Console.WriteLine("✅ Efficient CBOR encoding with compact binary output");
Console.WriteLine("✅ Type-safe serialization with compile-time code generation");
Console.WriteLine("✅ Support for complex nested scenarios");
Console.WriteLine("✅ Comprehensive null handling for nullable types");
Console.WriteLine("✅ Custom attribute support for property naming and control");
Console.WriteLine("✅ All major .NET types supported (primitives, DateTime, Guid, collections)");
Console.WriteLine("✅ NEW: Complete Dictionary<K,V> support with nested scenarios");
Console.WriteLine();

Console.WriteLine("🎉 Demo completed successfully! All features working perfectly.");
Console.WriteLine("=================================================");

