using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;

namespace GroundUp.Services.Core.Services;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _repo;

    public UserService(IUserRepository repo)
    {
        _repo = repo;
    }

    public Task<ApiResponse<PaginatedData<UserSummaryDto>>> GetAllAsync(FilterParams filterParams) =>
        _repo.GetAllAsync(filterParams);

    public Task<ApiResponse<UserDetailsDto>> GetByIdAsync(string userId) =>
        _repo.GetByIdAsync(userId);

    public Task<ApiResponse<bool>> EnsureLocalUserExistsAsync(Guid userId, string keycloakUserId, string realm) =>
        _repo.EnsureLocalUserExistsAsync(userId, keycloakUserId, realm);
}
