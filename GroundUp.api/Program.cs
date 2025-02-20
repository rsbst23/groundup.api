using GroundUp.core.interfaces;
using GroundUp.infrastructure.data;
using GroundUp.infrastructure.mappings;
using GroundUp.infrastructure.repositories;
using GroundUp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using GroundUp.infrastructure.extensions;

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

var connectionString = $"Server={Environment.GetEnvironmentVariable("MYSQL_SERVER")};Port={Environment.GetEnvironmentVariable("MYSQL_PORT")};Database={Environment.GetEnvironmentVariable("MYSQL_DATABASE")};User={Environment.GetEnvironmentVariable("MYSQL_USER")};Password={Environment.GetEnvironmentVariable("MYSQL_PASSWORD")};SslMode=None;AllowPublicKeyRetrieval=True;";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(9, 1, 0)),
        mysqlOptions => mysqlOptions.EnableRetryOnFailure()
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