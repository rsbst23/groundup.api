using Castle.DynamicProxy;
using FluentValidation;
using FluentValidation.AspNetCore;
using GroundUp.core.configuration;
using GroundUp.core.dtos;
using GroundUp.core.interfaces;
using GroundUp.core.validators;
using GroundUp.infrastructure.interceptors;
using GroundUp.infrastructure.services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
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
        /// Registers infrastructure services, including logging, permission handling, and repository interception.
        /// This sets up the core infrastructure layer services used throughout the application.
        /// </summary>
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
        {
            // Check if we're running EF Core migrations - if so, skip repository registration
            // to avoid dependency injection issues during database migrations
            var isEfCoreMigration = Environment.GetCommandLineArgs().Any(arg => arg.Contains("ef"));

            if (isEfCoreMigration)
            {
                return services; // Skip registering repositories during migrations
            }

            // Register core infrastructure services
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddSingleton<ProxyGenerator>(); // Castle DynamicProxy for interceptors

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

            // Register the permission interceptor used by repositories
            services.AddScoped<PermissionInterceptor>();

            // Add memory cache for performance optimization
            services.AddMemoryCache();

            // Auto-discover and register all repositories with interceptors
            // This finds all classes ending with "Repository" and wraps them with dynamic proxies
            // for automatic permission checking via the PermissionInterceptor
            var repositoryTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract && type.Name.EndsWith("Repository"))
                .ToList();

            foreach (var repositoryType in repositoryTypes)
            {
                var interfaceType = repositoryType.GetInterfaces().FirstOrDefault();
                if (interfaceType != null)
                {
                    // Register the actual repository implementation
                    services.AddScoped(repositoryType);
                    
                    // Register the interface with a dynamic proxy that intercepts calls
                    // This allows the PermissionInterceptor to check permissions before method execution
                    services.AddScoped(interfaceType, provider =>
                    {
                        var proxyGenerator = provider.GetRequiredService<ProxyGenerator>();
                        var repositoryInstance = provider.GetRequiredService(repositoryType);

                        // Create a proxy that wraps the repository and applies the LazyInterceptor
                        return proxyGenerator.CreateInterfaceProxyWithTarget(interfaceType, repositoryInstance, new LazyInterceptor(provider));
                    });
                }
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
        /// 2. "Custom" - Validates custom tokens issued by our API (after tenant selection)
        /// 
        /// This dual-token approach allows:
        /// - Users to authenticate via Keycloak (with social login, MFA, etc.)
        /// - API to issue custom tokens with tenant context for multi-tenancy
        /// </summary>
        public static IServiceCollection AddKeycloakServices(this IServiceCollection services)
        {
            // Configure Keycloak settings from environment variables or defaults
            services.Configure<KeycloakConfiguration>(config =>
            {
                config.AuthServerUrl = Environment.GetEnvironmentVariable("KEYCLOAK_AUTH_SERVER_URL") ?? "http://localhost:8080";
                config.Realm = Environment.GetEnvironmentVariable("KEYCLOAK_REALM") ?? "groundup";
                config.Resource = Environment.GetEnvironmentVariable("KEYCLOAK_RESOURCE") ?? "groundup-api";
                config.Secret = Environment.GetEnvironmentVariable("KEYCLOAK_CLIENT_SECRET") ?? "";

                // Admin API credentials for managing Keycloak users/roles programmatically
                config.AdminClientId = Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_CLIENT_ID") ?? "admin-cli";
                config.AdminClientSecret = Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_CLIENT_SECRET") ?? "";
            });

            // Register Keycloak identity provider service for user management
            services.AddHttpClient<IIdentityProviderService, IdentityProviderService>();
            services.AddScoped<IIdentityProviderService, IdentityProviderService>();

            // Get Keycloak configuration for setting up authentication
            var keycloakConfig = services.BuildServiceProvider().GetRequiredService<IOptions<KeycloakConfiguration>>().Value;

            // Custom JWT token settings (for tokens issued by our API)
            var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "supersecretkeythatshouldbe32charsmin!";
            var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "GroundUp";
            var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "GroundUpUsers";
            
            // Keycloak token settings (for tokens issued by Keycloak)
            var keycloakIssuer = $"{keycloakConfig.AuthServerUrl}/realms/{keycloakConfig.Realm}";
            var keycloakAudience = keycloakConfig.Resource;

            // Configure JWT Bearer authentication with TWO schemes
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                // SCHEME 1: "Keycloak" - Validates tokens from Keycloak
                // Used for initial authentication and /api/auth/set-tenant endpoint
                .AddJwtBearer("Keycloak", options =>
                {
                    options.RequireHttpsMetadata = false; // Allow HTTP in development
                    options.Authority = keycloakIssuer;   // Keycloak's token issuer URL
                    options.Audience = keycloakAudience;  // Expected audience claim
                    
                    // Allow self-signed certificates in development
                    options.BackchannelHttpHandler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    };
                    
                    // Token validation rules for Keycloak tokens
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = keycloakIssuer,
                        ValidateAudience = true,
                        ValidAudiences = new[] { keycloakAudience, "account" }, // Allow both audiences
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero // No tolerance for expired tokens
                    };
                    
                    // Event handlers for token validation
                    options.Events = new JwtBearerEvents
                    {
                        // Called after token is validated - extract roles from Keycloak's resource_access claim
                        OnTokenValidated = context =>
                        {
                            // Keycloak stores roles in a complex JSON structure
                            var resourceAccessClaim = context.Principal.Claims.FirstOrDefault(c => c.Type == "resource_access")?.Value;
                            if (!string.IsNullOrEmpty(resourceAccessClaim))
                            {
                                try
                                {
                                    // Parse the JSON to extract roles for this client (groundup-api)
                                    var parsedResourceAccess = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string[]>>>(resourceAccessClaim);
                                    if (parsedResourceAccess.TryGetValue(keycloakAudience, out var clientRoles) && clientRoles.TryGetValue("roles", out var roles))
                                    {
                                        // Add each role as a standard ClaimTypes.Role claim
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
                // SCHEME 2: "Custom" - Validates tokens issued by our API
                // Used for all API calls after tenant selection
                .AddJwtBearer("Custom", options =>
                {
                    options.RequireHttpsMetadata = false;
                    
                    // Token validation rules for custom tokens
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtIssuer,           // "GroundUp"
                        ValidateAudience = true,
                        ValidAudience = jwtAudience,       // "GroundUpUsers"
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSecret)),
                        ClockSkew = TimeSpan.Zero,
                        AuthenticationType = "Bearer"      // Critical: marks identity as authenticated
                    };
                    
                    options.Events = new JwtBearerEvents
                    {
                        // Called before token validation - extract token from header or cookie
                        OnMessageReceived = context =>
                        {
                            // First, check Authorization header (standard approach)
                            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                            
                            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                            {
                                context.Token = authHeader.Substring("Bearer ".Length).Trim();
                            }
                            // Fallback: check cookie (for browser-based clients)
                            else if (context.Request.Cookies.ContainsKey("AuthToken"))
                            {
                                context.Token = context.Request.Cookies["AuthToken"];
                            }
                            
                            return Task.CompletedTask;
                        }
                    };
                });

            // Configure authorization policy to accept EITHER authentication scheme
            // This allows endpoints to accept both Keycloak and Custom tokens
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