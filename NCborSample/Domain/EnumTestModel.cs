namespace NCborSample.Domain;

/// <summary>
/// Test class containing various enum properties
/// </summary>
public class EnumTestModel
{
    public UserRole Role { get; set; }
    public UserRole? OptionalRole { get; set; }
    public Priority TaskPriority { get; set; }
    public Priority? OptionalPriority { get; set; }
    public Permissions UserPermissions { get; set; }
    public Permissions? OptionalPermissions { get; set; }
    public Status CurrentStatus { get; set; }
    public Status? OptionalStatus { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Manual enum serialization implementation for validation
    /// This demonstrates both numeric and string serialization approaches
    /// </summary>
    public static void Serialize(CborWriter writer, EnumTestModel model, bool useStringEnums = false)
    {
        writer.WriteStartMap(null);

        // Name (for context)
        writer.WriteTextString("Name");
        writer.WriteTextString(model.Name);

        // Role - simple enum
        writer.WriteTextString("Role");
        if (useStringEnums)
        {
            writer.WriteTextString(model.Role.ToString());
        }
        else
        {
            writer.WriteInt32((int)model.Role);
        }

        // OptionalRole - nullable enum
        writer.WriteTextString("OptionalRole");
        if (model.OptionalRole.HasValue)
        {
            if (useStringEnums)
            {
                writer.WriteTextString(model.OptionalRole.Value.ToString());
            }
            else
            {
                writer.WriteInt32((int)model.OptionalRole.Value);
            }
        }
        else
        {
            writer.WriteNull();
        }

        // TaskPriority
        writer.WriteTextString("TaskPriority");
        if (useStringEnums)
        {
            writer.WriteTextString(model.TaskPriority.ToString());
        }
        else
        {
            writer.WriteInt32((int)model.TaskPriority);
        }

        // OptionalPriority
        writer.WriteTextString("OptionalPriority");
        if (model.OptionalPriority.HasValue)
        {
            if (useStringEnums)
            {
                writer.WriteTextString(model.OptionalPriority.Value.ToString());
            }
            else
            {
                writer.WriteInt32((int)model.OptionalPriority.Value);
            }
        }
        else
        {
            writer.WriteNull();
        }

        // UserPermissions - flags enum
        writer.WriteTextString("UserPermissions");
        if (useStringEnums)
        {
            writer.WriteTextString(model.UserPermissions.ToString());
        }
        else
        {
            writer.WriteInt32((int)model.UserPermissions);
        }

        // OptionalPermissions
        writer.WriteTextString("OptionalPermissions");
        if (model.OptionalPermissions.HasValue)
        {
            if (useStringEnums)
            {
                writer.WriteTextString(model.OptionalPermissions.Value.ToString());
            }
            else
            {
                writer.WriteInt32((int)model.OptionalPermissions.Value);
            }
        }
        else
        {
            writer.WriteNull();
        }

        // CurrentStatus - byte-backed enum
        writer.WriteTextString("CurrentStatus");
        if (useStringEnums)
        {
            writer.WriteTextString(model.CurrentStatus.ToString());
        }
        else
        {
            writer.WriteUInt32((byte)model.CurrentStatus);
        }

        // OptionalStatus
        writer.WriteTextString("OptionalStatus");
        if (model.OptionalStatus.HasValue)
        {
            if (useStringEnums)
            {
                writer.WriteTextString(model.OptionalStatus.Value.ToString());
            }
            else
            {
                writer.WriteUInt32((byte)model.OptionalStatus.Value);
            }
        }
        else
        {
            writer.WriteNull();
        }

        writer.WriteEndMap();
    }

    /// <summary>
    /// Manual enum deserialization implementation for validation
    /// This demonstrates both numeric and string deserialization approaches
    /// </summary>
    public static EnumTestModel Deserialize(CborReader reader, bool useStringEnums = false)
    {
        var model = new EnumTestModel();

        reader.ReadStartMap();

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var propertyName = reader.ReadTextString();

            switch (propertyName)
            {
                case "Name":
                    model.Name = reader.ReadTextString();
                    break;

                case "Role":
                    if (useStringEnums)
                    {
                        var roleString = reader.ReadTextString();
                        model.Role = Enum.Parse<UserRole>(roleString);
                    }
                    else
                    {
                        model.Role = (UserRole)reader.ReadInt32();
                    }
                    break;

                case "OptionalRole":
                    if (reader.PeekState() == CborReaderState.Null)
                    {
                        reader.ReadNull();
                        model.OptionalRole = null;
                    }
                    else
                    {
                        if (useStringEnums)
                        {
                            var roleString = reader.ReadTextString();
                            model.OptionalRole = Enum.Parse<UserRole>(roleString);
                        }
                        else
                        {
                            model.OptionalRole = (UserRole)reader.ReadInt32();
                        }
                    }
                    break;

                case "TaskPriority":
                    if (useStringEnums)
                    {
                        var priorityString = reader.ReadTextString();
                        model.TaskPriority = Enum.Parse<Priority>(priorityString);
                    }
                    else
                    {
                        model.TaskPriority = (Priority)reader.ReadInt32();
                    }
                    break;

                case "OptionalPriority":
                    if (reader.PeekState() == CborReaderState.Null)
                    {
                        reader.ReadNull();
                        model.OptionalPriority = null;
                    }
                    else
                    {
                        if (useStringEnums)
                        {
                            var priorityString = reader.ReadTextString();
                            model.OptionalPriority = Enum.Parse<Priority>(priorityString);
                        }
                        else
                        {
                            model.OptionalPriority = (Priority)reader.ReadInt32();
                        }
                    }
                    break;

                case "UserPermissions":
                    if (useStringEnums)
                    {
                        var permissionsString = reader.ReadTextString();
                        model.UserPermissions = Enum.Parse<Permissions>(permissionsString);
                    }
                    else
                    {
                        model.UserPermissions = (Permissions)reader.ReadInt32();
                    }
                    break;

                case "OptionalPermissions":
                    if (reader.PeekState() == CborReaderState.Null)
                    {
                        reader.ReadNull();
                        model.OptionalPermissions = null;
                    }
                    else
                    {
                        if (useStringEnums)
                        {
                            var permissionsString = reader.ReadTextString();
                            model.OptionalPermissions = Enum.Parse<Permissions>(permissionsString);
                        }
                        else
                        {
                            model.OptionalPermissions = (Permissions)reader.ReadInt32();
                        }
                    }
                    break;

                case "CurrentStatus":
                    if (useStringEnums)
                    {
                        var statusString = reader.ReadTextString();
                        model.CurrentStatus = Enum.Parse<Status>(statusString);
                    }
                    else
                    {
                        model.CurrentStatus = (Status)(byte)reader.ReadUInt32();
                    }
                    break;

                case "OptionalStatus":
                    if (reader.PeekState() == CborReaderState.Null)
                    {
                        reader.ReadNull();
                        model.OptionalStatus = null;
                    }
                    else
                    {
                        if (useStringEnums)
                        {
                            var statusString = reader.ReadTextString();
                            model.OptionalStatus = Enum.Parse<Status>(statusString);
                        }
                        else
                        {
                            model.OptionalStatus = (Status)(byte)reader.ReadUInt32();
                        }
                    }
                    break;

                default:
                    reader.SkipValue();
                    break;
            }
        }

        reader.ReadEndMap();
        return model;
    }
}