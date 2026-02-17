using Castle.DynamicProxy;
using FluentValidation;
using FluentValidation.AspNetCore;
using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.Core.configuration;
using GroundUp.Core.interfaces;
using GroundUp.Core.validators;
using GroundUp.Services.Core.Interceptors;
using GroundUp.Services.Core.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace GroundUp.Services.Core;

public static class ServiceCollectionInfrastructureExtensions
{
    /// <summary>
    /// Cross-cutting services that previously lived in `GroundUp.infrastructure`.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<ILoggingService, LoggingService>();

        services.AddSingleton<ProxyGenerator>();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ITokenService, TokenService>();

        services.AddHttpClient<IIdentityProviderAdminService, IdentityProviderAdminService>();
        services.AddScoped<IIdentityProviderAdminService, IdentityProviderAdminService>();

        services.AddScoped<PermissionInterceptor>();
        services.AddMemoryCache();

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CreateRoleDtoValidator>();
        services.AddFluentValidationAutoValidation();
        services.AddFluentValidationClientsideAdapters();
        return services;
    }

    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
    {
        services.Configure<KeycloakConfiguration>(config =>
        {
            config.AuthServerUrl = Environment.GetEnvironmentVariable("KEYCLOAK_AUTH_SERVER_URL") ?? "http://localhost:8080";
            config.Realm = Environment.GetEnvironmentVariable("KEYCLOAK_REALM") ?? "groundup";
            config.Resource = Environment.GetEnvironmentVariable("KEYCLOAK_RESOURCE") ?? "groundup-api";
            config.Secret = Environment.GetEnvironmentVariable("KEYCLOAK_CLIENT_SECRET") ?? "";
            config.AdminClientId = Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_CLIENT_ID") ?? "admin-cli";
            config.AdminClientSecret = Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_CLIENT_SECRET") ?? "";
        });

        services.AddHttpClient<IIdentityProviderService, IdentityProviderService>();
        services.AddScoped<IIdentityProviderService, IdentityProviderService>();

        // Build provider only to read config (kept behavior parity with previous implementation).
        var keycloakConfig = services.BuildServiceProvider().GetRequiredService<IOptions<KeycloakConfiguration>>().Value;
        var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "supersecretkeythatshouldbe32charsmin!";
        var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "GroundUp";
        var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "GroundUpUsers";
        var keycloakIssuer = $"{keycloakConfig.AuthServerUrl}/realms/{keycloakConfig.Realm}";
        var keycloakAudience = keycloakConfig.Resource;

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer("Keycloak", options =>
            {
                options.RequireHttpsMetadata = false;
                options.Authority = keycloakIssuer;
                options.Audience = keycloakAudience;

                options.BackchannelHttpHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = keycloakIssuer,
                    ValidateAudience = true,
                    ValidAudiences = new[] { keycloakAudience, "account" },
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var resourceAccessClaim = context.Principal?.Claims.FirstOrDefault(c => c.Type == "resource_access")?.Value;
                        if (!string.IsNullOrEmpty(resourceAccessClaim))
                        {
                            try
                            {
                                var parsedResourceAccess = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string[]>>>(resourceAccessClaim);
                                if (parsedResourceAccess != null &&
                                    parsedResourceAccess.TryGetValue(keycloakAudience, out var clientRoles) &&
                                    clientRoles.TryGetValue("roles", out var roles))
                                {
                                    var claimsIdentity = context.Principal?.Identity as ClaimsIdentity;
                                    foreach (var role in roles)
                                    {
                                        claimsIdentity?.AddClaim(new Claim(ClaimTypes.Role, role));
                                    }
                                }
                            }
                            catch
                            {
                                // ignore role parsing errors
                            }
                        }
                        return Task.CompletedTask;
                    }
                };
            })
            .AddJwtBearer("Custom", options =>
            {
                options.RequireHttpsMetadata = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtAudience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSecret)),
                    ClockSkew = TimeSpan.Zero,
                    AuthenticationType = "Bearer"
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

                        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Token = authHeader["Bearer ".Length..].Trim();
                        }
                        else if (context.Request.Cookies.ContainsKey("AuthToken"))
                        {
                            context.Token = context.Request.Cookies["AuthToken"]; 
                        }

                        return Task.CompletedTask;
                    },

                    OnTokenValidated = async context =>
                    {
                        try
                        {
                            var token = context.Request.Cookies["AuthToken"] ??
                                       context.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");

                            if (!string.IsNullOrEmpty(token))
                            {
                                var handler = new JwtSecurityTokenHandler();
                                var jwtToken = handler.ReadJwtToken(token);

                                var expirationTime = jwtToken.ValidTo;
                                var issuedTime = jwtToken.ValidFrom;
                                var tokenLifetime = expirationTime - issuedTime;
                                var halfwayPoint = issuedTime.Add(tokenLifetime / 2);

                                if (DateTime.UtcNow >= halfwayPoint)
                                {
                                    var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                                    var tenantIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "tenant_id")?.Value;

                                    if (Guid.TryParse(userIdClaim, out var userId) && int.TryParse(tenantIdClaim, out var tenantId))
                                    {
                                        var authSessionService = context.HttpContext.RequestServices.GetRequiredService<IAuthSessionService>();

                                        var newToken = await authSessionService.TryRefreshAuthTokenAsync(userId, tenantId, jwtToken.Claims);

                                        if (!string.IsNullOrWhiteSpace(newToken))
                                        {
                                            context.Response.Cookies.Append("AuthToken", newToken, new CookieOptions
                                            {
                                                HttpOnly = true,
                                                Secure = true,
                                                SameSite = SameSiteMode.Lax,
                                                Expires = DateTimeOffset.UtcNow.AddHours(1)
                                            });

                                            context.Response.Headers.Append("X-New-Token", newToken);
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Don't fail request if token refresh fails
                        }
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder("Keycloak", "Custom")
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }
}
