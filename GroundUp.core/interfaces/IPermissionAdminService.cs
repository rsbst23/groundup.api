using GroundUp.core.dtos;
using GroundUp.core.security;

namespace GroundUp.core.interfaces;

/// <summary>
/// Service-boundary API for permission administration.
/// Controllers should depend on this service, not repositories.
/// Authorization is enforced via <see cref="RequiresPermissionAttribute"/>.
/// </summary>
public interface IPermissionAdminService
{
    [RequiresPermission("permissions.view", "SYSTEMADMIN")]
    Task<OperationResult<PaginatedData<PermissionDto>>> GetAllAsync(FilterParams filterParams);

    [RequiresPermission("permissions.view", "SYSTEMADMIN")]
    Task<OperationResult<PermissionDto>> GetByIdAsync(int id);

    [RequiresPermission("permissions.create", "SYSTEMADMIN")]
    Task<OperationResult<PermissionDto>> AddAsync(PermissionDto permissionDto);

    [RequiresPermission("permissions.update", "SYSTEMADMIN")]
    Task<OperationResult<PermissionDto>> UpdateAsync(int id, PermissionDto permissionDto);

    [RequiresPermission("permissions.delete", "SYSTEMADMIN")]
    Task<OperationResult<bool>> DeleteAsync(int id);
}
