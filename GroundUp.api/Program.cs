using Amazon.CloudWatchLogs;
using GroundUp.api.Middleware;
using GroundUp.core.entities;
using GroundUp.infrastructure.data;
using GroundUp.infrastructure.mappings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Formatting.Json;
using Serilog.Sinks.AwsCloudWatch;
using System.Text;
using GroundUp.infrastructure.extensions;
using GroundUp.api.Infrastructure.Swagger;

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

var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
if (string.IsNullOrEmpty(secretKey))
{
    throw new InvalidOperationException("JWT_SECRET_KEY is missing from environment variables.");
}

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

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Configure Authentication with JWT
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Allow token from either Cookie or Authorization Header
            var cookieToken = context.Request.Cookies["AuthToken"];
            if (!string.IsNullOrEmpty(cookieToken))
            {
                context.Token = cookieToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

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
        policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddControllers();
builder.Services.AddApplicationServices(); // Auto-register validators
builder.Services.AddInfrastructureServices(); // Auto-register repositories

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "GroundUp API", Version = "v1" });

    // JWT Bearer Token Authentication
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer {your_token}'",
    });

    // Cookie Authentication (JWT stored in cookies)
    options.AddSecurityDefinition("CookieAuth", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Name = "AuthToken",
        Description = "JWT stored in cookies"
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
            Array.Empty<string>()
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


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy
            .WithOrigins("http://localhost:5174") // Frontend URL
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()); // REQUIRED to allow cookies in requests
});

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();


// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
    app.UseSwaggerUI();
//}

app.UseCors("AllowAll");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }