using System.Text.Json.Serialization;

namespace GroundUp.core.dtos
{
    // Basic DTO for displaying user info in lists
    public class UserSummaryDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public bool Enabled { get; set; }
    }

    // Detailed DTO for complete user information
    public class UserDetailsDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public bool Enabled { get; set; }
        public bool EmailVerified { get; set; }
        public long? CreatedTimestamp { get; set; }
        public Dictionary<string, List<string>> Attributes { get; set; } = new();
        public List<UserGroupDto> Groups { get; set; } = new();
        public List<string> RealmRoles { get; set; } = new();
        public Dictionary<string, List<string>> ClientRoles { get; set; } = new();
    }

    // DTO for user groups
    public class UserGroupDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }

    // DTO for creating a new user
    public class CreateUserDto
    {
        public required string Username { get; set; }
        public required string Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public bool Enabled { get; set; } = true;
        public bool EmailVerified { get; set; } = false;
        public bool SendWelcomeEmail { get; set; } = true; // Send password setup email
        public Dictionary<string, List<string>>? Attributes { get; set; }
    }

    // DTO for updating an existing user
    public class UpdateUserDto
    {
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public bool? Enabled { get; set; }
        public bool? EmailVerified { get; set; }
        public Dictionary<string, List<string>>? Attributes { get; set; }
    }
}