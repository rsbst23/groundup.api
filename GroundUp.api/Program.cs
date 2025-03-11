using Amazon.CloudWatchLogs;
using GroundUp.api.Middleware;
using GroundUp.infrastructure.data;
using GroundUp.infrastructure.mappings;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Formatting.Json;
using Serilog.Sinks.AwsCloudWatch;
using GroundUp.infrastructure.extensions;
using GroundUp.api.Infrastructure.Swagger;
using static Mysqlx.Crud.Order.Types;

DotNetEnv.Env.Load();

AmazonCloudWatchLogsClient? cloudWatchClient = null;

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV")))
{
    cloudWatchClient = new AmazonCloudWatchLogsClient();
}

// Use Serilog with or without CloudWatch
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

// Configure Serilog
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

// IMPORTANT: Add Keycloak services - this will configure JWT authentication
builder.Services.AddKeycloakServices();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("ADMIN"));

    options.AddPolicy("UserAccess", policy =>
        policy.RequireRole("USER"));
});

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

// Enable CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy
            .WithOrigins("http://localhost:5174") // Frontend URL
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()); // REQUIRED to allow cookies in requests
});

builder.Services.AddControllers();
builder.Services.AddApplicationServices(); // Auto-register validators
builder.Services.AddInfrastructureServices(); // Auto-register repositories

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "GroundUp API", Version = "v1" });

    // OAuth2/OpenID Connect for Keycloak
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

    // Cookie Authentication (JWT stored in cookies)
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    // Make Swagger send cookies by default
    options.OperationFilter<CookieAuthOperationFilter>();

    // Enforce security requirements
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

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "GroundUp API V1");

    // Enable Bearer token input in Swagger UI
    c.DocumentTitle = "GroundUp API";
    c.DefaultModelsExpandDepth(-1); // Disable schemas section
    c.DisplayOperationId();
    c.DisplayRequestDuration();
    c.EnableDeepLinking();
    c.EnableFilter();
});

app.UseCors("AllowAll");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }