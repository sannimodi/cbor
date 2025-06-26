namespace NCborSample.Domain;

// Enum with byte backing type
public enum Status : byte
{
    Inactive = 0,
    Active = 1,
    Pending = 2,
    Suspended = 3
}
