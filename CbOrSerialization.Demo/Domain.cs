namespace CbOrSerialization.Demo;

/// <summary>
/// Enhanced Person model demonstrating all CBOR serialization library capabilities
/// </summary>
public class Person
{
    // Unique identifier using GUID support
    public Guid PersonId { get; set; }
    
    // Custom property name using attributes
    [CbOrPropertyName("full_name")]
    public string Name { get; set; } = string.Empty;
    
    // Basic primitive types
    public int Age { get; set; }
    public bool IsActive { get; set; }
    public double Height { get; set; }
    
    // DateTime support with CBOR Tag 0
    public DateTime DateOfBirth { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    
    // Nullable types
    public DateTime? LastLoginDate { get; set; }
    public int? Score { get; set; }
    public Guid? ManagerId { get; set; }
    
    // Collection support
    public List<string> Hobbies { get; set; } = new();
    
    // Dictionary support - NEW feature!
    public Dictionary<string, string> ContactInfo { get; set; } = new();
    public Dictionary<string, int> Skills { get; set; } = new();
    
    // Nested complex objects
    public Address? HomeAddress { get; set; }
    
    // Ignored property (won't be serialized)
    [CbOrIgnore]
    public string InternalNotes { get; set; } = string.Empty;
    
    // Default value attribute
    [CbOrDefaultValue("active")]
    public string Status { get; set; } = "active";
}

/// <summary>
/// Address model for nested object demonstration
/// </summary>
public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
}

/// <summary>
/// Department model for complex Dictionary scenarios
/// </summary>
public class Department
{
    public Guid DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, Person> Staff { get; set; } = new();
    public Dictionary<Guid, List<string>> ProjectAssignments { get; set; } = new();
}

