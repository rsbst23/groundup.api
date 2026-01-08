using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GroundUp.Repositories.Core.Data;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
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
        optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(9, 1, 0)));

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
