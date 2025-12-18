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
        /// 
        /// Token Expiration:
        /// - Initial expiration: 1 hour
        /// - Sliding expiration: Token is automatically renewed when past 30 minutes (halfway point)
        /// - Active users: Session maintained indefinitely
        /// - Inactive users: Logged out after 1 hour of inactivity
        /// </summary>
        /// <param name="userId">The unique identifier of the authenticated user</param>
        /// <param name="tenantId">The selected tenant ID to include in the token</param>
        /// <param name="existingClaims">Claims from the Keycloak token (user profile, roles, etc.)</param>
        /// <returns>A JWT token string with user claims and tenant context</returns>
        public Task<string> GenerateTokenAsync(Guid userId, int tenantId, IEnumerable<Claim> existingClaims)
        {
            // Filter out infrastructure claims that should not be copied from Keycloak token
            var claimsToExclude = new HashSet<string>
            {
                "iss", "aud", "exp", "nbf", "iat", "jti",
                JwtRegisteredClaimNames.Iss,
                JwtRegisteredClaimNames.Aud,
                JwtRegisteredClaimNames.Exp,
                JwtRegisteredClaimNames.Nbf,
                JwtRegisteredClaimNames.Iat,
                JwtRegisteredClaimNames.Jti
            };
            
            // Keep only user-related claims from Keycloak
            var userClaims = existingClaims
                .Where(c => !claimsToExclude.Contains(c.Type))
                .ToList();
            
            // Build final claim set
            var claims = new List<Claim>(userClaims)
            {
                new Claim("tenant_id", tenantId.ToString()),
                new Claim("ApplicationUserId", userId.ToString()), // Application-level user ID (distinct from external IdP)
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()) // Keep this for backwards compatibility
            };

            // Get JWT configuration
            var secret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? _configuration["JWT_SECRET"];
            var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? _configuration["JWT_ISSUER"];
            var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? _configuration["JWT_AUDIENCE"];

            // Create signing credentials
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            
            // Token lifecycle timestamps
            var now = DateTime.UtcNow;
            var expires = now.AddHours(1);

            // Create the JWT token
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: now,        // Explicitly set NotBefore (nbf claim)
                expires: expires,      // Expires in 1 hour
                signingCredentials: creds
            );

            return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
        }
    }
}
