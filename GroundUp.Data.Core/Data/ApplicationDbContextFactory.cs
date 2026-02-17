using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GroundUp.Data.Core.Data;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = $"Server={Environment.GetEnvironmentVariable("MYSQL_SERVER")};" +
                               $"Port={Environment.GetEnvironmentVariable("MYSQL_PORT")};" +
                               $"Database={Environment.GetEnvironmentVariable("MYSQL_DATABASE")};" +
                               $"User={Environment.GetEnvironmentVariable("MYSQL_USER")};" +
                               $"Password={Environment.GetEnvironmentVariable("MYSQL_PASSWORD")};" +
                               $"SslMode=None;AllowPublicKeyRetrieval=True;";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // Pomelo MySql provider requires a real MySQL server version.
        // Our dev/docker images use MySQL 8, so default to that.
        optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)));

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
