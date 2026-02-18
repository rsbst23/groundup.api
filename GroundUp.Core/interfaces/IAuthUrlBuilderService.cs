using GroundUp.Core.dtos;

namespace GroundUp.Core.interfaces
{
    /// <summary>
    /// Builds Keycloak authorization URLs (login/registration) and encodes the OIDC state.
    /// Kept separate from callback orchestration (AuthFlow) and HTTP concerns.
    /// </summary>
    public interface IAuthUrlBuilderService
    {
        /// <summary>
        /// Builds the Keycloak login URL.
        /// If <paramref name="domain"/> maps to an enterprise tenant, uses that tenant realm; otherwise uses shared realm.
        /// </summary>
        Task<string> BuildLoginUrlAsync(string? domain, string redirectUri, string? returnUrl = null);

        /// <summary>
        /// Builds the Keycloak registration URL for standard tenants (shared realm).
        /// </summary>
        Task<string> BuildRegistrationUrlAsync(string redirectUri, string? returnUrl = null);

        /// <summary>
        /// Builds the login URL for an invitation-based flow (realm is provided by the invitation).
        /// </summary>
        Task<string> BuildInvitationLoginUrlAsync(string realm, string invitationToken, string invitationEmail, string redirectUri);

        /// <summary>
        /// Builds the Keycloak registration URL for the enterprise first-admin flow.
        /// Uses the enterprise tenant realm and encodes state with Flow="enterprise_first_admin".
        /// </summary>
        Task<string> BuildEnterpriseFirstAdminRegistrationUrlAsync(string realm, string redirectUri);

        /// <summary>
        /// Builds the Keycloak login URL for the public join-link flow.
        /// Encodes state with Flow="join_link" and JoinToken.
        /// </summary>
        Task<string> BuildJoinLinkLoginUrlAsync(string realm, string joinToken, string redirectUri);
    }
}
