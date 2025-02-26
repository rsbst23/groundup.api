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

// Create an AWS CloudWatch Logs client
var cloudWatchClient = new AmazonCloudWatchLogsClient(); // We don't pass in anything because it will use the IAM user and region it is running under

// Configure Serilog to use AWS CloudWatch
// Define CloudWatch sink options
var cloudWatchSinkOptions = new CloudWatchSinkOptions
{
    LogGroupName = Environment.GetEnvironmentVariable("CLOUDWATCH_LOG_GROUP") ?? "GroundUpApiLogs",
    TextFormatter = new JsonFormatter(), // Use JSON for structured logging
    LogStreamNameProvider = new DefaultLogStreamProvider(), // Default stream provider
    BatchSizeLimit = 100, // How many logs to send in a batch
    Period = TimeSpan.FromSeconds(10), // How often logs are sent
    CreateLogGroup = true // Ensure log group is created
};

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug() // Set minimum log level here instead
    .WriteTo.Console()
    .WriteTo.AmazonCloudWatch(cloudWatchSinkOptions, cloudWatchClient)
    .CreateLogger();

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