using GroundUp.Core.dtos;

namespace GroundUp.Core.interfaces
{
    /// <summary>
    /// Orchestrates the user authentication callback flows (invitation/join-link/new-org/default/etc.).
    /// Extracted from the API controller for testability and separation of concerns.
    /// </summary>
    public interface IAuthFlowService
    {
        Task<AuthCallbackResponseDto> HandleAuthCallbackAsync(
            string code,
            string? state,
            string redirectUri);
    }
}
