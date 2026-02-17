using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.Core.dtos;
using GroundUp.Core.interfaces;

namespace GroundUp.Services.Core.Services;

public sealed class InvitationService : IInvitationService
{
    private readonly ITenantInvitationRepository _invitationRepo;

    public InvitationService(ITenantInvitationRepository invitationRepo)
    {
        _invitationRepo = invitationRepo;
    }

    public Task<ApiResponse<PaginatedData<TenantInvitationDto>>> GetAllAsync(FilterParams filterParams) =>
        _invitationRepo.GetAllAsync(filterParams);

    public Task<ApiResponse<TenantInvitationDto>> GetByIdAsync(int id) =>
        _invitationRepo.GetByIdAsync(id);

    public Task<ApiResponse<TenantInvitationDto>> CreateAsync(CreateTenantInvitationDto dto, Guid createdByUserId) =>
        _invitationRepo.AddAsync(dto, createdByUserId);

    public Task<ApiResponse<TenantInvitationDto>> UpdateAsync(int id, UpdateTenantInvitationDto dto) =>
        _invitationRepo.UpdateAsync(id, dto);

    public Task<ApiResponse<bool>> DeleteAsync(int id) =>
        _invitationRepo.DeleteAsync(id);

    public Task<ApiResponse<List<TenantInvitationDto>>> GetPendingAsync() =>
        _invitationRepo.GetPendingInvitationsAsync();

    public Task<ApiResponse<bool>> ResendAsync(int id, int expirationDays = 7) =>
        _invitationRepo.ResendInvitationAsync(id, expirationDays);

    public Task<ApiResponse<List<TenantInvitationDto>>> GetMyInvitationsAsync(string email) =>
        _invitationRepo.GetInvitationsForEmailAsync(email);

    public Task<ApiResponse<bool>> AcceptInvitationAsync(string invitationToken, Guid userId) =>
        _invitationRepo.AcceptInvitationAsync(invitationToken, userId);

    public Task<ApiResponse<TenantInvitationDto>> GetByTokenAsync(string token) =>
        _invitationRepo.GetByTokenAsync(token);
}
