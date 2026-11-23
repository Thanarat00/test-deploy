using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EmployeeManagement.Data;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var workingDirectory = Directory.GetCurrentDirectory();
        var configurationDirectory = File.Exists(Path.Combine(workingDirectory, "appsettings.json"))
            ? workingDirectory : AppContext.BaseDirectory;
        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(configurationDirectory)
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.{environment}.json", optional: true);
        if (environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
            configurationBuilder.AddJsonFile("appsettings.Local.json", optional: true)
                .AddUserSecrets<ApplicationDbContextFactory>(optional: true);
        var configuration = configurationBuilder
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        MySqlConfiguration.Configure(options, configuration);
        return new ApplicationDbContext(options.Options);
    }
}
