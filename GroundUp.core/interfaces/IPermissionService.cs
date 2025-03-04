using System.Threading.Tasks;

namespace GroundUp.core.interfaces
{
    public interface IPermissionService
    {
        Task<bool> HasPermission(string userId, string permission);
    }
}
