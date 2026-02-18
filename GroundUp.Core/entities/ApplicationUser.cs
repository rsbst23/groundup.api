using Microsoft.AspNetCore.Identity;

namespace GroundUp.Core.entities
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
    }
}
