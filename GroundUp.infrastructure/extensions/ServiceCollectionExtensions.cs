using Castle.DynamicProxy;
using FluentValidation;
using FluentValidation.AspNetCore;
using GroundUp.core.configuration;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.interceptors;
using GroundUp.infrastructure.services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;

namespace GroundUp.infrastructure.extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers infrastructure services, including logging, permission handling, and repository interception.
        /// </summary>
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
        {
            // Register Logging Service
            services.AddSingleton<ILoggingService, LoggingService>();

            // Register Proxy Generator for Castle Dynamic Proxy
            services.AddSingleton<ProxyGenerator>();

            // Register PermissionInterceptor for role-based access control
            services.AddScoped<PermissionInterceptor>();
            services.AddScoped<IInterceptor>(provider => provider.GetRequiredService<PermissionInterceptor>());

            // Register IHttpContextAccessor for accessing user claims in permission checks
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            // Register Permission Service
            services.AddScoped<IPermissionService, PermissionService>();

            // Register repositories dynamically with Castle Proxy interception
            var repositoryTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract && type.Name.EndsWith("Repository"));

            foreach (var repositoryType in repositoryTypes)
            {
                var interfaceType = repositoryType.GetInterfaces().FirstOrDefault();
                if (interfaceType != null)
                {
                    services.AddScoped(repositoryType); // Register repository itself
                    services.AddScoped(interfaceType, provider =>
                    {
                        var proxyGenerator = provider.GetRequiredService<ProxyGenerator>();
                        var repositoryInstance = provider.GetRequiredService(repositoryType);
                        var interceptor = provider.GetRequiredService<PermissionInterceptor>();
                        return proxyGenerator.CreateInterfaceProxyWithTarget(interfaceType, repositoryInstance, interceptor);
                    });
                }
            }

            return services;
        }

        /// <summary>
        /// Registers application services, including FluentValidation middleware.
        /// </summary>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Scan and register all FluentValidation validators
            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

            // Enable FluentValidation middleware
            services.AddFluentValidationAutoValidation();
            services.AddFluentValidationClientsideAdapters();

            return services;
        }

        public static IServiceCollection AddKeycloakServices(this IServiceCollection services)
        {
            services.Configure<KeycloakConfiguration>(config =>
            {
                config.AuthServerUrl = Environment.GetEnvironmentVariable("KEYCLOAK_AUTH_SERVER_URL") ?? "http://localhost:8080";
                config.Realm = Environment.GetEnvironmentVariable("KEYCLOAK_REALM") ?? "groundup";
                config.Resource = Environment.GetEnvironmentVariable("KEYCLOAK_RESOURCE") ?? "groundup-api";
                config.Secret = Environment.GetEnvironmentVariable("KEYCLOAK_CLIENT_SECRET") ?? "";
            });

            services.AddHttpClient<IKeycloakService, KeycloakService>();
            services.AddScoped<IKeycloakService, KeycloakService>();

            var keycloakConfig = services.BuildServiceProvider().GetRequiredService<IOptions<KeycloakConfiguration>>().Value;

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.RequireHttpsMetadata = false;
                    options.Authority = $"{keycloakConfig.AuthServerUrl}/realms/{keycloakConfig.Realm}";
                    options.Audience = keycloakConfig.Resource;

                    // Use default HttpClientHandler to handle Docker networking explicitly
                    options.BackchannelHttpHandler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    };

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = $"{keycloakConfig.AuthServerUrl}/realms/{keycloakConfig.Realm}",
                        ValidateAudience = true,
                        ValidAudiences = new[] { keycloakConfig.Resource, "account" },
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                            return Task.CompletedTask;
                        },
                        OnTokenValidated = context =>
                        {
                            var resourceAccessClaim = context.Principal.Claims.FirstOrDefault(c => c.Type == "resource_access")?.Value;
                            if (!string.IsNullOrEmpty(resourceAccessClaim))
                            {
                                try
                                {
                                    var parsedResourceAccess = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string[]>>>(resourceAccessClaim);
                                    if (parsedResourceAccess.TryGetValue(keycloakConfig.Resource, out var clientRoles) && clientRoles.TryGetValue("roles", out var roles))
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
                });

            return services;
        }
    }
}
