using Amazon.CloudWatchLogs;
using GroundUp.Api;
using GroundUp.Api.Logging;
using GroundUp.Api.Middleware;
using GroundUp.Data.Core;
using GroundUp.Repositories.Inventory;
using GroundUp.Services.Core;
using GroundUp.Services.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Formatting.Json;
using Serilog.Sinks.AwsCloudWatch;
using GroundUp.Sample.Swagger;
using GroundUp.Api.RateLimiting;

DotNetEnv.Env.Load();

AmazonCloudWatchLogsClient? cloudWatchClient = null;
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV")))
{
    cloudWatchClient = new AmazonCloudWatchLogsClient();
}

var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console();

if (cloudWatchClient != null)
{
    var cloudWatchSinkOptions = new CloudWatchSinkOptions
    {
        LogGroupName = Environment.GetEnvironmentVariable("CLOUDWATCH_LOG_GROUP") ?? "GroundUpApiLogs",
        TextFormatter = new JsonFormatter(),
        LogStreamNameProvider = new DefaultLogStreamProvider(),
        BatchSizeLimit = 100,
        Period = TimeSpan.FromSeconds(10),
        CreateLogGroup = true
    };

    loggerConfig.WriteTo.AmazonCloudWatch(cloudWatchSinkOptions, cloudWatchClient);
}

Log.Logger = loggerConfig.CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// Opt-in GroundUp logging wiring (placeholder for future settings-driven logging).
builder.Services.AddGroundUpLogging(builder.Configuration);

var connectionString = $"Server={Environment.GetEnvironmentVariable("MYSQL_SERVER")};Port={Environment.GetEnvironmentVariable("MYSQL_PORT")};Database={Environment.GetEnvironmentVariable("MYSQL_DATABASE")};User={Environment.GetEnvironmentVariable("MYSQL_USER")};Password={Environment.GetEnvironmentVariable("MYSQL_PASSWORD")};SslMode=None;AllowPublicKeyRetrieval=True;";

// Cross-cutting infra (logging, tenant context, permission checks, token service, proxy infra, etc.)
builder.Services.AddInfrastructureServices();

// Persistence + repositories
builder.Services.AddCorePersistence(connectionString);
builder.Services.AddInventoryRepositories(connectionString);

// Services
builder.Services.AddCoreServices();
builder.Services.AddInventoryServices();

// Load controllers from GroundUp.Api but host in this executable
builder.Services.AddGroundUpApiControllers();

builder.Services.AddMemoryCache();
builder.Services.AddApplicationServices();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

builder.Services.AddAuthenticationServices();

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder("Keycloak", "Custom")
        .RequireAuthenticatedUser()
        .Build();

    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder("Keycloak", "Custom")
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "GroundUp API", Version = "v1" });

    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            Implicit = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"{Environment.GetEnvironmentVariable("KEYCLOAK_AUTH_SERVER_URL") ?? "http://localhost:8080"}/realms/{Environment.GetEnvironmentVariable("KEYCLOAK_REALM") ?? "groundup"}/protocol/openid-connect/auth"),
                TokenUrl = new Uri($"{Environment.GetEnvironmentVariable("KEYCLOAK_AUTH_SERVER_URL") ?? "http://localhost:8080"}/realms/{Environment.GetEnvironmentVariable("KEYCLOAK_REALM") ?? "groundup"}/protocol/openid-connect/token"),
                Scopes = new Dictionary<string, string>
                {
                    { "openid", "OpenID Connect" },
                    { "profile", "User profile" }
                }
            }
        }
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.OperationFilter<CookieAuthOperationFilter>();

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new List<string>()
        }
    });
});

// Rate limiting (opt-in policies in GroundUp.Api) - Skip in Testing environment
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddGroundUpRateLimiting();
}

var app = builder.Build();

// Dev-only migrations (ordered)
if (app.Environment.IsDevelopment())
{
    app.MigrateCoreDatabase();

    // Inventory still has dev migrations in this repo
    using var scope = app.Services.CreateScope();
    var inventoryDb = scope.ServiceProvider.GetRequiredService<GroundUp.Repositories.Inventory.Data.InventoryDbContext>();
    if (inventoryDb.Database.IsRelational())
    {
        inventoryDb.Database.Migrate();
    }
}

app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "GroundUp API V1");
    c.DocumentTitle = "GroundUp API";
    c.DefaultModelsExpandDepth(-1);
    c.DisplayOperationId();
    c.DisplayRequestDuration();
    c.EnableDeepLinking();
    c.EnableFilter();
});

// Apply rate limiting middleware (opt-in) - Skip in Testing environment
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseGroundUpRateLimiting();
}

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

namespace GroundUp.Sample
{
    public partial class Program { }
}
