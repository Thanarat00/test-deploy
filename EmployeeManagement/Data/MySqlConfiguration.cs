using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Data;

public static class MySqlConfiguration
{
    public static void Configure(DbContextOptionsBuilder options, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Set ConnectionStrings:DefaultConnection using user secrets or environment variables.");
        var settings = DatabaseServerSettings.FromConfiguration(configuration);
        // Both engines use Pomelo; their version-specific SQL generation differs.
        // An explicit version keeps migration scaffolding independent of a live server.
        ServerVersion serverVersion = settings.IsMariaDb
            ? new MariaDbServerVersion(settings.Version)
            : new MySqlServerVersion(settings.Version);
        options.UseMySql(connectionString, serverVersion, mysql => mysql.CommandTimeout(30));
    }
}
