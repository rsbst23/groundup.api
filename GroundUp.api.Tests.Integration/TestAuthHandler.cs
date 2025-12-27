using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace GroundUp.Tests.Integration
{
    public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string Scheme = "Test";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim>
            {
                new Claim("ApplicationUserId", TestData.UserId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, TestData.UserId.ToString()),
                new Claim(ClaimTypes.Email, TestData.Email),
                new Claim("tenant_id", TestData.TenantId.ToString()),
                new Claim(ClaimTypes.Role, "TENANTADMIN")
            };

            var identity = new ClaimsIdentity(claims, Scheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    public static class TestData
    {
        public static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        public const int TenantId = 1;
        public const string Email = "testuser@example.com";
    }
}
