using GroundUp.core;
using GroundUp.core.dtos;

namespace GroundUp.core.interfaces;

/// <summary>
/// Read-oriented permission queries used by services.
/// Kept narrow to support Phase 3 refactor (services call repositories only).
/// </summary>
public interface IPermissionQueryRepository
{
    Task<List<string>> GetUserPermissionsAsync(string userId);

    Task<ApiResponse<List<PermissionDto>>> GetAllPermissionsAsync();
    Task<ApiResponse<PermissionDto>> GetPermissionByIdAsync(int id);
    Task<ApiResponse<PermissionDto>> GetPermissionByNameAsync(string name);
}
