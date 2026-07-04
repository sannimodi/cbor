namespace NCbor.Generator;

internal enum NCborIgnoreCondition
{
    Always = 0,
    Never = 1,
    WhenWritingDefault = 2,
    WhenWritingNull = 3
}
