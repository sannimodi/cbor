namespace NCborSample.Domain;

// Flags enum for testing [Flags] attribute
[Flags]
public enum Permissions
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4,
    Delete = 8,
    All = Read | Write | Execute | Delete
}
