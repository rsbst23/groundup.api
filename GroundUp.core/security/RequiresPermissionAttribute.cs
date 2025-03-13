namespace GroundUp.core.security
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public class RequiresPermissionAttribute : Attribute
    {
        public string[] Permissions { get; }
        public string[] RequiredRoles { get; }

        // Allows any authenticated user with the specific permission
        public RequiresPermissionAttribute(string permission)
        {
            Permissions = new[] { permission };
        }

        // Allows users with specific permission AND/OR specific roles
        public RequiresPermissionAttribute(string permission, params string[] requiredRoles)
        {
            Permissions = new[] { permission };
            RequiredRoles = requiredRoles;
        }

        // Allows users with specific roles
        public RequiresPermissionAttribute(params string[] requiredRoles)
        {
            RequiredRoles = requiredRoles;
        }
    }
}