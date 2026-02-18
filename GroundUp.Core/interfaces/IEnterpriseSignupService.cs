using GroundUp.Core;
using GroundUp.Core.dtos;

namespace GroundUp.Core.interfaces
{
    /// <summary>
    /// Orchestrates enterprise onboarding (tenant + realm provisioning) and produces the first-admin registration URL.
    /// This is conceptually a registration/onboarding flow, not tenant administration.
    /// </summary>
    public interface IEnterpriseSignupService
    {
        Task<ApiResponse<EnterpriseSignupResponseDto>> SignupAsync(EnterpriseSignupRequestDto request, string redirectUri);
    }
}
