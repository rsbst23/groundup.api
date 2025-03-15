namespace GroundUp.core.dtos
{
    // Common DTO for role representation
    public class RoleDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsClientRole { get; set; }
        public string? ContainerId { get; set; } // Client ID or realm
        public bool Composite { get; set; } // Whether this role is a composite role
    }

    // DTO for creating a new role
    public class CreateRoleDto
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        public bool IsClientRole { get; set; } = false;
        public string? ClientId { get; set; } // Only needed if IsClientRole is true
    }

    // DTO for updating an existing role
    public class UpdateRoleDto
    {
        public string? Description { get; set; }
    }

    // DTO for mapping roles and permissions
    public class RolePermissionMappingDto
    {
        public string RoleName { get; set; } = string.Empty;
        public List<PermissionDto> Permissions { get; set; } = new List<PermissionDto>();
    }

    // DTO for assigning a permission to a role
    public class AssignPermissionDto
    {
        public required string RoleName { get; set; }
        public required int PermissionId { get; set; }
    }

    // DTO for bulk assigning permissions to a role
    public class BulkAssignPermissionsDto
    {
        public required string RoleName { get; set; }
        public required List<int> PermissionIds { get; set; }
    }

    // DTO for assigning a role to a user
    public class UserRoleAssignmentDto
    {
        public required string UserId { get; set; }
        public required string RoleName { get; set; }
    }

    // DTO for bulk assigning roles to a user
    public class UserRolesBulkAssignmentDto
    {
        public required string UserId { get; set; }
        public required List<string> RoleNames { get; set; }
    }
}