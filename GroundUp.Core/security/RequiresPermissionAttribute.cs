using System;

namespace GroundUp.Core.security
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public class RequiresPermissionAttribute : Attribute
    {
        public string[] Permissions { get; }
        public string[] RequiredRoles { get; }
        public bool RequireAllPermissions { get; }

        // Allows any authenticated user with the specific permission
        public RequiresPermissionAttribute(string permission)
        {
            Permissions = new[] { permission };
            RequireAllPermissions = false;
        }

        // Allow specifying whether all permissions are required or just any one of them
        public RequiresPermissionAttribute(string[] permissions, bool requireAll = false)
        {
            Permissions = permissions;
            RequireAllPermissions = requireAll;
        }

        // Allows users with specific permission AND/OR specific roles
        public RequiresPermissionAttribute(string permission, params string[] requiredRoles)
        {
            Permissions = new[] { permission };
            RequiredRoles = requiredRoles;
            RequireAllPermissions = false;
        }

        // Allows users with specific roles
        public RequiresPermissionAttribute(params string[] requiredRoles)
        {
            RequiredRoles = requiredRoles;
        }
    }
}