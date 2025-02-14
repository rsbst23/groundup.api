using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using GroundUp.api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;


    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            //builder.ConfigureServices(services =>
            //{
            //    // Remove existing database context (if any)
            //    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            //    if (descriptor != null)
            //    {
            //        services.Remove(descriptor);
            //    }

            //    // Add in-memory database for testing
            //    services.AddDbContext<ApplicationDbContext>(options =>
            //    {
            //        options.UseInMemoryDatabase("TestDb");
            //    });

            //    // Ensure the database is created
            //    using var scope = services.BuildServiceProvider().CreateScope();
            //    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            //    db.Database.EnsureCreated();
            //});
        }
    }


