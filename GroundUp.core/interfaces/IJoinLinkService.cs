using GroundUp.core.dtos;

namespace GroundUp.core.interfaces
{
    public interface IJoinLinkService
    {
        /// <summary>
        /// Validates a join token and builds the Keycloak auth redirect URL for the join-link flow.
        /// </summary>
        Task<ApiResponse<AuthUrlResponseDto>> BuildJoinAuthUrlAsync(string joinToken, string redirectUri);
    }
}
