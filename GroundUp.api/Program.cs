using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.Runtime;
using GroundUp.api.Middleware;
using GroundUp.infrastructure.data;
using GroundUp.infrastructure.extensions;
using GroundUp.infrastructure.mappings;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Json;
using Serilog.Sinks.AwsCloudWatch;

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
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();


// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
    app.UseSwaggerUI();
//}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }