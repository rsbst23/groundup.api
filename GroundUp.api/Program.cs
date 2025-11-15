using Amazon.CloudWatchLogs;
using GroundUp.api.Infrastructure.Swagger;
using GroundUp.api.Middleware;
using GroundUp.infrastructure.data;
using GroundUp.infrastructure.extensions;
using GroundUp.infrastructure.mappings;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Formatting.Json;
using Serilog.Sinks.AwsCloudWatch;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

DotNetEnv.Env.Load();

AmazonCloudWatchLogsClient? cloudWatchClient = null;

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV")))
{
    cloudWatchClient = new AmazonCloudWatchLogsClient();
}

// Configure Serilog
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

var connectionString = $"Server={Environment.GetEnvironmentVariable("MYSQL_SERVER")};Port={Environment.GetEnvironmentVariable("MYSQL_PORT")};Database={Environment.GetEnvironmentVariable("MYSQL_DATABASE")};User={Environment.GetEnvironmentVariable("MYSQL_USER")};Password={Environment.GetEnvironmentVariable("MYSQL_PASSWORD")};SslMode=None;AllowPublicKeyRetrieval=True;";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(9, 1, 0)),
        mysqlOptions =>
        {
            mysqlOptions.MigrationsAssembly("GroundUp.infrastructure");
            mysqlOptions.EnableRetryOnFailure();
        }
    ));

builder.Services.AddAutoMapper(typeof(MappingProfile));

// Add authentication and authorization
builder.Services.AddAuthenticationServices();


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

// Register CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

// Register infrastructure services (Repositories, Proxies, etc.)
builder.Services.AddInfrastructureServices();

// Add additional application services
builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddApplicationServices();

// Require authentication globally for all endpoints
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder("Keycloak", "Custom")
        .RequireAuthenticatedUser()
        .Build();
    
    // Set fallback policy to require authentication
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder("Keycloak", "Custom")
        .RequireAuthenticatedUser()
        .Build();
});

// Ensure Swagger is correctly configured
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
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "CookieAuth"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddRateLimiter(options =>
{
    // Global rate limiter applied to all endpoints
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    // You can add specific policies for different endpoints
    options.AddPolicy("AdminApiPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Build the application
var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Enable Swagger UI
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

// Uncomment this to add rate limiting to the application
//app.UseRateLimiter();
app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
