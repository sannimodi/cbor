namespace NCbor.Demo.Domain;

/// <summary>
/// Department model for complex Dictionary scenarios
/// </summary>
public class Department
{
    public Guid DepartmentId { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    public Dictionary<string, Person> Staff { get; set; } = [];
    
    public Dictionary<Guid, List<string>> ProjectAssignments { get; set; } = [];
}
