using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MulhollandRealEstate.API.Configuration;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace MulhollandRealEstate.API.Data;

/// <summary>Design-time factory so <c>dotnet ef</c> can resolve the same MySQL connection as runtime (incl. DATABASE_URL).</summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{env}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = MysqlConnectionResolver.Resolve(configuration);
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseMySql(connectionString, serverVersion);
        return new AppDbContext(optionsBuilder.Options);
    }
}
