using DotNetEnv;
using GroundUp.api;
using Microsoft.EntityFrameworkCore;

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

//var mysqlServer = Environment.GetEnvironmentVariable("MYSQL_SERVER") ?? "127.0.0.1";
//var mysqlPort = Environment.GetEnvironmentVariable("MYSQL_PORT") ?? "3306";
//var mysqlDatabase = Environment.GetEnvironmentVariable("MYSQL_DATABASE") ?? "groundup_db";
//var mysqlUser = Environment.GetEnvironmentVariable("MYSQL_USER") ?? "root";
//var mysqlPassword = Environment.GetEnvironmentVariable("MYSQL_PASSWORD") ?? "root";

//var connectionString = $"Server={mysqlServer};Port={mysqlPort};Database={mysqlDatabase};User={mysqlUser};Password={mysqlPassword};SslMode=None;AllowPublicKeyRetrieval=True;";

//var connectionString = $"Server=localhost;Port=3306;Database=groundup_db;User=root;Password=root;SslMode=None;AllowPublicKeyRetrieval=True;";

var connectionString = $"Server={Environment.GetEnvironmentVariable("MYSQL_SERVER")};Port={Environment.GetEnvironmentVariable("MYSQL_PORT")};Database={Environment.GetEnvironmentVariable("MYSQL_DATABASE")};User={Environment.GetEnvironmentVariable("MYSQL_USER")};Password={Environment.GetEnvironmentVariable("MYSQL_PASSWORD")};SslMode=None;AllowPublicKeyRetrieval=True;";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(9, 1, 0)),
        mysqlOptions => mysqlOptions.EnableRetryOnFailure()
    ));

builder.Services.AddControllers();
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

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }