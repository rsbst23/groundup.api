using GroundUp.Core.entities;

namespace GroundUp.Core.dtos
{
    // DTO for System role representation 
    public class SystemRoleDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsClientRole { get; set; }
        public string? ContainerId { get; set; } // Client ID or realm
        public bool Composite { get; set; } // Whether this role is a composite role
    }

    // Common DTO for application and workspace roles
    public class RoleDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public RoleType RoleType { get; set; }
        public string? WorkspaceId { get; set; } // Only meaningful for Workspace roles
        public DateTime CreatedDate { get; set; }
    }

    public class CreateSystemRoleDto
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        public bool IsClientRole { get; set; } = false;
        public string? ClientId { get; set; } // Only needed if IsClientRole is true
    }

    // DTO for creating a new role
    public class CreateRoleDto
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        public RoleType RoleType { get; set; } = RoleType.Application;
        public string? WorkspaceId { get; set; } // Only needed for Workspace roles
    }

    // DTO for updating an existing role
    public class UpdateRoleDto
    {
        public string? Description { get; set; }
    }

    // DTO for mapping roles and policies
    public class RolePolicyMappingDto
    {
        public string RoleName { get; set; } = string.Empty;
        public RoleType RoleType { get; set; }
        public List<PolicyDto> Policies { get; set; } = new List<PolicyDto>();
    }

    // DTO for assigning a policy to a role
    public class AssignPolicyDto
    {
        public required string RoleName { get; set; }
        public RoleType RoleType { get; set; }
        public required int PolicyId { get; set; }
    }

    // DTO for bulk assigning policies to a role
    public class BulkAssignPoliciesDto
    {
        public required string RoleName { get; set; }
        public RoleType RoleType { get; set; }
        public required List<int> PolicyIds { get; set; }
    }

    // DTO for assigning a role to a user
    public class UserRoleAssignmentDto
    {
        public required string UserId { get; set; }
        public required string RoleName { get; set; }
        public RoleType RoleType { get; set; } = RoleType.System; // Default to System roles
    }

    // DTO for bulk assigning roles to a user
    public class UserRolesBulkAssignmentDto
    {
        public required string UserId { get; set; }
        public required List<UserRoleAssignmentDto> RoleAssignments { get; set; }
    }

    public class UserRoleDto
    {
        public int Id { get; set; }

        public Guid UserId { get; set; }

        public int RoleId { get; set; }
    }
}