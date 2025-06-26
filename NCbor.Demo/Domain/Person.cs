namespace NCbor.Demo.Domain;

/// <summary>
/// Enhanced Person model demonstrating all CBOR serialization library capabilities
/// </summary>
public class Person
{
    // Unique identifier using GUID support
    public Guid PersonId { get; set; }
    
    // Custom property name using attributes
    [NCborPropertyName("full_name")]
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
    public List<string> Hobbies { get; set; } = [];
    
    // Dictionary support - NEW feature!
    public Dictionary<string, string> ContactInfo { get; set; } = [];
    
    public Dictionary<string, int> Skills { get; set; } = [];
    
    // Nested complex objects
    public Address? HomeAddress { get; set; }
    
    // Ignored property (won't be serialized)
    [NCborIgnore]
    public string InternalNotes { get; set; } = string.Empty;
    
    // Default value attribute
    [NCborDefaultValue("active")]
    public string Status { get; set; } = "active";
}
