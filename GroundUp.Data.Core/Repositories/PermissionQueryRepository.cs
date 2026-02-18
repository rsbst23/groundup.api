using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.Core;
using GroundUp.Core.dtos;
using GroundUp.Core.interfaces;
using GroundUp.Data.Core.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GroundUp.Data.Core.Repositories;

public class PermissionQueryRepository : IPermissionQueryRepository
{
    private readonly ApplicationDbContext _context;

    public PermissionQueryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<string>> GetUserPermissionsAsync(string userId)
    {
        // Get roles for the user from the database
        var userRoles = await _context.UserRoles
            .Where(ur => ur.UserId.ToString() == userId)
            .Include(ur => ur.Role)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

        // Get permissions for these roles through policies
        var permissions = await _context.RolePolicies
            .Where(rp => userRoles.Contains(rp.RoleName))
            .Join(_context.PolicyPermissions,
                rp => rp.PolicyId,
                pp => pp.PolicyId,
                (rp, pp) => pp.PermissionId)
            .Join(_context.Permissions,
                permId => permId,
                perm => perm.Id,
                (permId, perm) => perm.Name)
            .Distinct()
            .ToListAsync();

        return permissions;
    }

    public async Task<ApiResponse<List<PermissionDto>>> GetAllPermissionsAsync()
    {
        var permissions = await _context.Permissions
            .OrderBy(p => p.Group)
            .ThenBy(p => p.Name)
            .ToListAsync();

        var permissionDtos = permissions.Select(p => new PermissionDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Group = p.Group
        }).ToList();

        return new ApiResponse<List<PermissionDto>>(permissionDtos);
    }

    public async Task<ApiResponse<PermissionDto>> GetPermissionByIdAsync(int id)
    {
        var permission = await _context.Permissions.FindAsync(id);
        if (permission == null)
        {
            return new ApiResponse<PermissionDto>(
                default!,
                false,
                $"Permission with ID {id} not found",
                null,
                StatusCodes.Status404NotFound,
                ErrorCodes.NotFound);
        }

        var permissionDto = new PermissionDto
        {
            Id = permission.Id,
            Name = permission.Name,
            Description = permission.Description,
            Group = permission.Group
        };

        return new ApiResponse<PermissionDto>(permissionDto);
    }

    public async Task<ApiResponse<PermissionDto>> GetPermissionByNameAsync(string name)
    {
        var permission = await _context.Permissions
            .FirstOrDefaultAsync(p => p.Name.ToLower() == name.ToLower());

        if (permission == null)
        {
            return new ApiResponse<PermissionDto>(
                default!,
                false,
                $"Permission with name '{name}' not found",
                null,
                StatusCodes.Status404NotFound,
                ErrorCodes.NotFound);
        }

        var permissionDto = new PermissionDto
        {
            Id = permission.Id,
            Name = permission.Name,
            Description = permission.Description,
            Group = permission.Group
        };

        return new ApiResponse<PermissionDto>(permissionDto);
    }
}
