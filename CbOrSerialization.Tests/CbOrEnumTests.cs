using FluentAssertions;

namespace CbOrSerialization.Tests;

public class CbOrEnumTests
{
    private readonly TestCbOrContext _context = TestCbOrContext.Default;

    [Fact]
    public void SerializeEnumModel_ShouldSucceed()
    {
        // Arrange
        var model = new EnumModel
        {
            Name = "EnumTest",
            Role = UserRole.Admin,
            OptionalRole = UserRole.SuperAdmin,
            TaskPriority = Priority.High,
            OptionalPriority = Priority.Critical,
            UserPermissions = Permissions.Read | Permissions.Write | Permissions.Execute,
            OptionalPermissions = Permissions.All,
            CurrentStatus = Status.Active,
            OptionalStatus = Status.Pending
        };

        // Act
        var bytes = CbOrSerializer.Serialize(model, _context.EnumModel);
        var deserialized = CbOrSerializer.Deserialize(bytes, _context.EnumModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Name.Should().Be(model.Name);
        deserialized.Role.Should().Be(model.Role);
        deserialized.OptionalRole.Should().Be(model.OptionalRole);
        deserialized.TaskPriority.Should().Be(model.TaskPriority);
        deserialized.OptionalPriority.Should().Be(model.OptionalPriority);
        deserialized.UserPermissions.Should().Be(model.UserPermissions);
        deserialized.OptionalPermissions.Should().Be(model.OptionalPermissions);
        deserialized.CurrentStatus.Should().Be(model.CurrentStatus);
        deserialized.OptionalStatus.Should().Be(model.OptionalStatus);
    }

    [Fact]
    public void SerializeNullableEnums_ShouldSucceed()
    {
        // Arrange
        var model = new EnumModel
        {
            Name = "NullableEnumTest",
            Role = UserRole.User,
            OptionalRole = null,
            TaskPriority = Priority.Low,
            OptionalPriority = null,
            UserPermissions = Permissions.Read,
            OptionalPermissions = null,
            CurrentStatus = Status.Inactive,
            OptionalStatus = null
        };

        // Act
        var bytes = CbOrSerializer.Serialize(model, _context.EnumModel);
        var deserialized = CbOrSerializer.Deserialize(bytes, _context.EnumModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Name.Should().Be(model.Name);
        deserialized.Role.Should().Be(model.Role);
        deserialized.OptionalRole.Should().BeNull();
        deserialized.TaskPriority.Should().Be(model.TaskPriority);
        deserialized.OptionalPriority.Should().BeNull();
        deserialized.UserPermissions.Should().Be(model.UserPermissions);
        deserialized.OptionalPermissions.Should().BeNull();
        deserialized.CurrentStatus.Should().Be(model.CurrentStatus);
        deserialized.OptionalStatus.Should().BeNull();
    }

    [Fact]
    public void SerializeFlagsEnum_ShouldSucceed()
    {
        // Arrange
        var model = new EnumModel
        {
            Name = "FlagsTest",
            Role = UserRole.Admin,
            TaskPriority = Priority.Medium,
            UserPermissions = Permissions.Read | Permissions.Write | Permissions.Delete, // Combined flags
            CurrentStatus = Status.Active
        };

        // Act
        var bytes = CbOrSerializer.Serialize(model, _context.EnumModel);
        var deserialized = CbOrSerializer.Deserialize(bytes, _context.EnumModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.UserPermissions.Should().Be(Permissions.Read | Permissions.Write | Permissions.Delete);
        deserialized.UserPermissions.Should().HaveFlag(Permissions.Read);
        deserialized.UserPermissions.Should().HaveFlag(Permissions.Write);
        deserialized.UserPermissions.Should().HaveFlag(Permissions.Delete);
        deserialized.UserPermissions.Should().NotHaveFlag(Permissions.Execute);
    }

    [Fact]
    public void SerializeByteEnum_ShouldSucceed()
    {
        // Arrange
        var model = new EnumModel
        {
            Name = "ByteEnumTest",
            Role = UserRole.Guest,
            TaskPriority = Priority.Low,
            UserPermissions = Permissions.None,
            CurrentStatus = Status.Suspended // byte-backed enum
        };

        // Act
        var bytes = CbOrSerializer.Serialize(model, _context.EnumModel);
        var deserialized = CbOrSerializer.Deserialize(bytes, _context.EnumModel);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.CurrentStatus.Should().Be(Status.Suspended);
        deserialized.Name.Should().Be(model.Name);
    }

    [Fact]
    public void SerializeEnumBoundaryValues_ShouldSucceed()
    {
        // Arrange - Test all enum values to ensure boundary handling
        var models = new[]
        {
            new EnumModel { Role = UserRole.Guest, TaskPriority = Priority.Low, UserPermissions = Permissions.None, CurrentStatus = Status.Inactive, Name = "Guest" },
            new EnumModel { Role = UserRole.User, TaskPriority = Priority.Medium, UserPermissions = Permissions.Read, CurrentStatus = Status.Active, Name = "User" },
            new EnumModel { Role = UserRole.Admin, TaskPriority = Priority.High, UserPermissions = Permissions.Write, CurrentStatus = Status.Pending, Name = "Admin" },
            new EnumModel { Role = UserRole.SuperAdmin, TaskPriority = Priority.Critical, UserPermissions = Permissions.All, CurrentStatus = Status.Suspended, Name = "SuperAdmin" }
        };

        foreach (var model in models)
        {
            // Act
            var bytes = CbOrSerializer.Serialize(model, _context.EnumModel);
            var deserialized = CbOrSerializer.Deserialize(bytes, _context.EnumModel);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Role.Should().Be(model.Role);
            deserialized.TaskPriority.Should().Be(model.TaskPriority);
            deserialized.UserPermissions.Should().Be(model.UserPermissions);
            deserialized.CurrentStatus.Should().Be(model.CurrentStatus);
            deserialized.Name.Should().Be(model.Name);
        }
    }
}