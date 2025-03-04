using Castle.DynamicProxy;
using FluentValidation;
using FluentValidation.AspNetCore;
using GroundUp.core.interfaces;
using GroundUp.infrastructure.interceptors;
using GroundUp.infrastructure.services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

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
    }
}
