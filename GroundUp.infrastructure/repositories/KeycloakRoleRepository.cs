using GroundUp.core;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using GroundUp.core.security;
using Microsoft.AspNetCore.Http;

namespace GroundUp.infrastructure.repositories
{
    public class KeycloakRoleRepository : IRoleRepository
    {
        private readonly IKeycloakAdminService _keycloakAdminService;
        private readonly ILoggingService _logger;

        public KeycloakRoleRepository(
            IKeycloakAdminService keycloakAdminService,
            ILoggingService logger)
        {
            _keycloakAdminService = keycloakAdminService;
            _logger = logger;
        }

        //[RequiresPermission("roles.view", "ADMIN")]
        public async Task<ApiResponse<PaginatedData<RoleDto>>> GetAllAsync(FilterParams filterParams)
        {
            try
            {
                // Get all roles from Keycloak
                var roles = await _keycloakAdminService.GetAllRolesAsync();

                // Apply sorting
                if (!string.IsNullOrEmpty(filterParams.SortBy))
                {
                    bool descending = filterParams.SortBy.StartsWith("-");
                    string propertyName = descending ? filterParams.SortBy.Substring(1) : filterParams.SortBy;

                    roles = roles.OrderBy(r =>
                        propertyName switch
                        {
                            "Name" => r.Name,
                            "Description" => r.Description ?? "",
                            _ => r.Name
                        }).ToList();

                    if (descending)
                    {
                        roles.Reverse();
                    }
                }

                // Apply filtering
                if (!string.IsNullOrEmpty(filterParams.SearchTerm))
                {
                    var searchTerm = filterParams.SearchTerm.ToLower();
                    roles = roles.Where(r =>
                        r.Name.ToLower().Contains(searchTerm) ||
                        (r.Description?.ToLower().Contains(searchTerm) ?? false)
                    ).ToList();
                }

                // Pagination
                var totalRecords = roles.Count;
                var pagedRoles = roles
                    .Skip((filterParams.PageNumber - 1) * filterParams.PageSize)
                    .Take(filterParams.PageSize)
                    .ToList();

                var paginatedData = new PaginatedData<RoleDto>(
                    pagedRoles,
                    filterParams.PageNumber,
                    filterParams.PageSize,
                    totalRecords
                );

                return new ApiResponse<PaginatedData<RoleDto>>(paginatedData);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving roles: {ex.Message}", ex);
                return new ApiResponse<PaginatedData<RoleDto>>(
                    default!,
                    false,
                    "Failed to retrieve roles from Keycloak",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        [RequiresPermission("roles.view", "ADMIN")]
        public async Task<ApiResponse<RoleDto>> GetByNameAsync(string name)
        {
            try
            {
                var role = await _keycloakAdminService.GetRoleByNameAsync(name);
                if (role == null)
                {
                    return new ApiResponse<RoleDto>(
                        default!,
                        false,
                        $"Role '{name}' not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                return new ApiResponse<RoleDto>(role);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving role '{name}': {ex.Message}", ex);
                return new ApiResponse<RoleDto>(
                    default!,
                    false,
                    $"Failed to retrieve role '{name}' from Keycloak",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        [RequiresPermission("roles.create", "ADMIN")]
        public async Task<ApiResponse<RoleDto>> CreateAsync(CreateRoleDto roleDto)
        {
            try
            {
                var createdRole = await _keycloakAdminService.CreateRoleAsync(roleDto);
                return new ApiResponse<RoleDto>(
                    createdRole,
                    true,
                    "Role created successfully",
                    null,
                    StatusCodes.Status201Created
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating role: {ex.Message}", ex);
                return new ApiResponse<RoleDto>(
                    default!,
                    false,
                    "Failed to create role in Keycloak",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        [RequiresPermission("roles.update", "ADMIN")]
        public async Task<ApiResponse<RoleDto>> UpdateAsync(string name, UpdateRoleDto roleDto)
        {
            try
            {
                var updatedRole = await _keycloakAdminService.UpdateRoleAsync(name, roleDto);
                if (updatedRole == null)
                {
                    return new ApiResponse<RoleDto>(
                        default!,
                        false,
                        $"Role '{name}' not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                return new ApiResponse<RoleDto>(updatedRole);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating role '{name}': {ex.Message}", ex);
                return new ApiResponse<RoleDto>(
                    default!,
                    false,
                    $"Failed to update role '{name}' in Keycloak",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }

        [RequiresPermission("roles.delete", "ADMIN")]
        public async Task<ApiResponse<bool>> DeleteAsync(string name)
        {
            try
            {
                var result = await _keycloakAdminService.DeleteRoleAsync(name);
                if (!result)
                {
                    return new ApiResponse<bool>(
                        false,
                        false,
                        $"Role '{name}' not found",
                        null,
                        StatusCodes.Status404NotFound,
                        ErrorCodes.NotFound
                    );
                }

                return new ApiResponse<bool>(true, true, $"Role '{name}' deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting role '{name}': {ex.Message}", ex);
                return new ApiResponse<bool>(
                    false,
                    false,
                    $"Failed to delete role '{name}' from Keycloak",
                    new List<string> { ex.Message },
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.InternalServerError
                );
            }
        }
    }
}