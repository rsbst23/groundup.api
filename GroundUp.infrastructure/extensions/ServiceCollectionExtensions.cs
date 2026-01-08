using Castle.DynamicProxy;
using FluentValidation;
using FluentValidation.AspNetCore;
using GroundUp.core.configuration;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using GroundUp.core.validators;
using GroundUp.infrastructure.interceptors;
using GroundUp.infrastructure.repositories;
using GroundUp.infrastructure.services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;

namespace GroundUp.infrastructure.extensions
{
    /// <summary>
    /// Extension methods for configuring application services and authentication.
    /// These methods are called from Program.cs to set up the dependency injection container.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers infrastructure services (cross-cutting concerns).
        ///
        /// NOTE (service-layer refactor):
        /// - Repositories are no longer auto-registered/proxied here.
        /// - Permission enforcement is moving to the SERVICE boundary (service interfaces).
        /// - Repository registrations should be explicit in `GroundUp.Repositories.*` projects.
        /// </summary>
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
        {
            // Check if we're running EF Core migrations - if so, skip optional registrations
            // to avoid dependency injection issues during database migrations
            var isEfCoreMigration = Environment.GetCommandLineArgs().Any(arg => arg.Contains("ef"));

            // Register core infrastructure services
            services.AddSingleton<ILoggingService, LoggingService>();

            // Castle DynamicProxy for interceptors (used to proxy SERVICES)
            services.AddSingleton<ProxyGenerator>();

            // Register IHttpContextAccessor to access HTTP context (User, Request, etc.) in services
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            // Register TenantContext - provides current tenant ID from authenticated user's claims
            services.AddScoped<ITenantContext, TenantContext>();

            // Register Permission Service - handles permission checks for authorization
            services.AddScoped<IPermissionService, PermissionService>();

            // Register Token Service - creates custom JWT tokens with tenant context
            services.AddScoped<ITokenService, TokenService>();

            // Register Keycloak Admin Service - manages users and roles in Keycloak
            services.AddHttpClient<IIdentityProviderAdminService, IdentityProviderAdminService>();
            services.AddScoped<IIdentityProviderAdminService, IdentityProviderAdminService>();

            // Register the permission interceptor (used by service proxies)
            services.AddScoped<PermissionInterceptor>();

            // Add memory cache for performance optimization
            services.AddMemoryCache();

            // NOTE: repository registration removed from here as part of refactor.
            // Repositories should be registered explicitly in `GroundUp.Repositories.*`.

            // TEMP (Phase 3): Core bounded-context repos still live in `GroundUp.infrastructure`.
            // Register narrowly-scoped repos needed by core services.
            services.AddScoped<ITenantSsoSettingsRepository, TenantSsoSettingsRepository>();
            services.AddScoped<IPermissionQueryRepository, PermissionQueryRepository>();

            // Register AuthFlowService in DI so AuthController can delegate auth callback orchestration.
            if (!isEfCoreMigration)
            {
                services.AddProxiedScoped<IAuthFlowService, AuthFlowService>();

                // Register AuthUrlBuilderService so controllers can generate Keycloak auth URLs.
                services.AddProxiedScoped<IAuthUrlBuilderService, AuthUrlBuilderService>();

                // Register JoinLinkService so JoinLinkController can delegate public join flow orchestration.
                services.AddProxiedScoped<IJoinLinkService, JoinLinkService>();

                // Register EnterpriseSignupService in DI so controllers can delegate enterprise signup orchestration to the service layer.
                services.AddProxiedScoped<IEnterpriseSignupService, EnterpriseSignupService>();

                // Register TenantSsoSettingsService so TenantController can stay thin.
                services.AddProxiedScoped<ITenantSsoSettingsService, TenantSsoSettingsService>();

                services.AddProxiedScoped<IPermissionAdminService, PermissionAdminService>();
            }

            return services;
        }

        /// <summary>
        /// Registers application services, including FluentValidation middleware.
        /// This sets up automatic model validation for DTOs used in API requests.
        /// </summary>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Scan the core assembly and register all FluentValidation validators
            services.AddValidatorsFromAssembly(typeof(CreateRoleDtoValidator).Assembly);

            // Enable automatic validation on controller actions
            services.AddFluentValidationAutoValidation();

            // Enable client-side validation adapters (for future SPA integration)
            services.AddFluentValidationClientsideAdapters();

            return services;
        }

        /// <summary>
        /// Configures authentication with two JWT Bearer schemes:
        /// 1. "Keycloak" - Validates tokens issued by Keycloak (initial login)
        /// 2. "Custom" - Validates custom tokens issued by our API (after tenant selection with sliding expiration)
        /// 
        /// This dual-token approach allows:
        /// - Users to authenticate via Keycloak (with social login, MFA, etc.)
        /// - API to issue custom tokens with tenant context for multi-tenancy
        /// - Automatic sliding expiration (tokens renew during active use)
        /// </summary>
        public static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
        {
            // Configure Keycloak settings from environment variables or defaults
            services.Configure<KeycloakConfiguration>(config =>
            {
                config.AuthServerUrl = Environment.GetEnvironmentVariable("KEYCLOAK_AUTH_SERVER_URL") ?? "http://localhost:8080";
                config.Realm = Environment.GetEnvironmentVariable("KEYCLOAK_REALM") ?? "groundup";
                config.Resource = Environment.GetEnvironmentVariable("KEYCLOAK_RESOURCE") ?? "groundup-api";
                config.Secret = Environment.GetEnvironmentVariable("KEYCLOAK_CLIENT_SECRET") ?? "";
                config.AdminClientId = Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_CLIENT_ID") ?? "admin-cli";
                config.AdminClientSecret = Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_CLIENT_SECRET") ?? "";
            });

            // Register Keycloak identity provider service for user management
            services.AddHttpClient<IIdentityProviderService, IdentityProviderService>();
            services.AddScoped<IIdentityProviderService, IdentityProviderService>();

            // Get configuration values
            var keycloakConfig = services.BuildServiceProvider().GetRequiredService<IOptions<KeycloakConfiguration>>().Value;
            var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "supersecretkeythatshouldbe32charsmin!";
            var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "GroundUp";
            var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "GroundUpUsers";
            var keycloakIssuer = $"{keycloakConfig.AuthServerUrl}/realms/{keycloakConfig.Realm}";
            var keycloakAudience = keycloakConfig.Resource;

            // Configure JWT Bearer authentication with TWO schemes
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                // SCHEME 1: "Keycloak" - Validates tokens from Keycloak (initial login)
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
                            // Extract roles from Keycloak's resource_access claim
                            var resourceAccessClaim = context.Principal.Claims.FirstOrDefault(c => c.Type == "resource_access")?.Value;
                            if (!string.IsNullOrEmpty(resourceAccessClaim))
                            {
                                try
                                {
                                    var parsedResourceAccess = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string[]>>>(resourceAccessClaim);
                                    if (parsedResourceAccess != null && parsedResourceAccess.TryGetValue(keycloakAudience, out var clientRoles) && clientRoles.TryGetValue("roles", out var roles))
                                    {
                                        var claimsIdentity = context.Principal.Identity as ClaimsIdentity;
                                        foreach (var role in roles)
                                        {
                                            claimsIdentity?.AddClaim(new Claim(ClaimTypes.Role, role));
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error parsing roles: {ex.Message}");
                                }
                            }
                            return Task.CompletedTask;
                        }
                    };
                })
                // SCHEME 2: "Custom" - Validates tokens issued by our API with sliding expiration
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
                        // Extract token from header or cookie
                        OnMessageReceived = context =>
                        {
                            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

                            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                            {
                                context.Token = authHeader.Substring("Bearer ".Length).Trim();
                            }
                            else if (context.Request.Cookies.ContainsKey("AuthToken"))
                            {
                                context.Token = context.Request.Cookies["AuthToken"];
                            }

                            return Task.CompletedTask;
                        },

                        // SLIDING EXPIRATION: Refresh token if past halfway to expiration
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

                                    // Check if token is past halfway to expiration
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
                                            var tokenService = context.HttpContext.RequestServices.GetRequiredService<ITokenService>();
                                            var userTenantRepository = context.HttpContext.RequestServices.GetRequiredService<IUserTenantRepository>();

                                            // Verify user still has access to tenant
                                            var userTenant = await userTenantRepository.GetUserTenantAsync(userId, tenantId);

                                            if (userTenant != null)
                                            {
                                                // Generate new token with fresh expiration
                                                var newToken = await tokenService.GenerateTokenAsync(userId, tenantId, jwtToken.Claims);

                                                // Replace cookie
                                                context.Response.Cookies.Append("AuthToken", newToken, new CookieOptions
                                                {
                                                    HttpOnly = true,
                                                    Secure = true,
                                                    SameSite = SameSiteMode.Lax,
                                                    Expires = DateTimeOffset.UtcNow.AddHours(1)
                                                });

                                                // Add header for clients using Authorization header
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

            // Configure authorization policy to accept EITHER authentication scheme
            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder("Keycloak", "Custom")
                    .RequireAuthenticatedUser()
                    .Build();
            });

            return services;
        }
    }
}