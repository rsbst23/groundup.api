using GroundUp.Core.dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GroundUp.Core.interfaces
{
    public interface IIdentityProviderService
    {
        Task<bool> ValidateTokenAsync(string token);

        /// <summary>
        /// Exchanges an authorization code for access and refresh tokens
        /// </summary>
        /// <param name="code">Authorization code from Keycloak</param>
        /// <param name="redirectUri">Redirect URI used in the authorization request</param>
        /// <param name="realm">Optional realm name. If null, uses default from configuration. 
        /// Must match the realm that issued the authorization code.</param>
        /// <returns>Token response containing access token, refresh token, etc.</returns>
        Task<TokenResponseDto?> ExchangeCodeForTokensAsync(string code, string redirectUri, string? realm = null);
    }
}
