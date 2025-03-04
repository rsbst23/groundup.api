using GroundUp.core.entities;
using GroundUp.core.interfaces;
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GroundUp.infrastructure.services
{
    public class PermissionService : IPermissionService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public PermissionService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<bool> HasPermission(string userId, string permission)
        {
            var appUser = await _userManager.FindByIdAsync(userId);
            if (appUser == null) return false;

            var userRoles = await _userManager.GetRolesAsync(appUser);
            if (!userRoles.Any()) return false; // User has no roles, so no permissions

            var roleTasks = userRoles.Select(async role =>
            {
                var identityRole = await _roleManager.FindByNameAsync(role);
                if (identityRole == null) return false;

                var roleClaims = await _roleManager.GetClaimsAsync(identityRole);
                return roleClaims.Any(c => c.Type == "permission" && c.Value == permission);
            });

            var results = await Task.WhenAll(roleTasks);
            return results.Any(r => r);
        }
    }
}
