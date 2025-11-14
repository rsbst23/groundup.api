using GroundUp.core.interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace GroundUp.infrastructure.services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Generates a custom JWT token for a user with tenant context.
        /// This token is created after initial Keycloak authentication and tenant selection.
        /// </summary>
        /// <param name="userId">The unique identifier of the authenticated user</param>
        /// <param name="tenantId">The selected tenant ID to include in the token</param>
        /// <param name="existingClaims">Claims from the Keycloak token (user profile, roles, etc.)</param>
        /// <returns>A JWT token string with user claims and tenant context</returns>
        public Task<string> GenerateTokenAsync(Guid userId, int tenantId, IEnumerable<Claim> existingClaims)
        {
            // Filter out infrastructure claims that should not be copied from Keycloak token.
            // We exclude JWT metadata claims (issuer, audience, expiration, etc.) because:
            // 1. We want our own issuer/audience for this custom token
            // 2. We set our own expiration time
            // 3. Each token should have a unique JWT ID
            var claimsToExclude = new HashSet<string>
            {
                "iss", "aud", "exp", "nbf", "iat", "jti",  // Short names
                JwtRegisteredClaimNames.Iss,                // Issuer
                JwtRegisteredClaimNames.Aud,                // Audience
                JwtRegisteredClaimNames.Exp,                // Expiration
                JwtRegisteredClaimNames.Nbf,                // Not Before
                JwtRegisteredClaimNames.Iat,                // Issued At
                JwtRegisteredClaimNames.Jti                 // JWT ID
            };
            
            // Keep only user-related claims from Keycloak (name, email, roles, etc.)
            var userClaims = existingClaims
                .Where(c => !claimsToExclude.Contains(c.Type))
                .ToList();
            
            // Build the final claim set with:
            // 1. All user claims from Keycloak (profile, roles, etc.)
            // 2. tenant_id - the selected tenant for multi-tenancy filtering
            // 3. NameIdentifier - ensure userId is in the token
            var claims = new List<Claim>(userClaims)
            {
                new Claim("tenant_id", tenantId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            };

            // Get JWT configuration from environment variables or appsettings
            // These should match the values expected by the "Custom" JWT Bearer scheme
            var secret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? _configuration["JWT_SECRET"];
            var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? _configuration["JWT_ISSUER"];
            var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? _configuration["JWT_AUDIENCE"];

            // Create signing credentials using HMAC SHA256
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            
            // Token expires in 1 hour (should match cookie expiration)
            var expires = DateTime.UtcNow.AddHours(1);

            // Create the JWT token with our custom claims and configuration
            var token = new JwtSecurityToken(
                issuer: issuer,           // "GroundUp" - identifies our API as the token issuer
                audience: audience,       // "GroundUpUsers" - identifies who the token is for
                claims: claims,           // All user claims + tenant_id
                expires: expires,         // Token expiration time
                signingCredentials: creds // HMAC signature for validation
            );

            // Serialize the token to a string that can be sent to the client
            return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
        }
    }
}
