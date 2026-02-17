using GroundUp.Core.interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace GroundUp.Services.Core.Services;

public sealed class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<string> GenerateTokenAsync(Guid userId, int tenantId, IEnumerable<Claim> existingClaims)
    {
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

        var userClaims = existingClaims
            .Where(c => !claimsToExclude.Contains(c.Type))
            .ToList();

        var claims = new List<Claim>(userClaims)
        {
            new("tenant_id", tenantId.ToString()),
            new("ApplicationUserId", userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };

        var secret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? _configuration["JWT_SECRET"];
        var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? _configuration["JWT_ISSUER"];
        var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? _configuration["JWT_AUDIENCE"];

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var now = DateTime.UtcNow;
        var expires = now.AddHours(1);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
    }
}
