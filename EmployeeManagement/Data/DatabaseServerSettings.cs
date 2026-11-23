namespace EmployeeManagement.Data;

public sealed record DatabaseServerSettings(string Provider, Version Version)
{
    public bool IsMariaDb => Provider == "MariaDb";

    public static DatabaseServerSettings FromConfiguration(IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"]?.Trim() ?? "MySql";
        provider = provider.ToUpperInvariant() switch
        {
            "MYSQL" => "MySql",
            "MARIADB" => "MariaDb",
            _ => throw new InvalidOperationException("Database:Provider must be MySql or MariaDb.")
        };
        var versionText = configuration["Database:ServerVersion"] ?? (provider == "MariaDb" ? "10.4.32" : "8.0.36");
        if (!Version.TryParse(versionText, out var version))
            throw new InvalidOperationException("Database:ServerVersion must be a numeric version, for example 8.0.36 or 10.4.32.");
        var minimum = provider == "MariaDb" ? new Version(10, 4, 0) : new Version(8, 0, 16);
        if (version < minimum)
            throw new InvalidOperationException($"Database:Provider {provider} requires server version {minimum} or later.");
        return new DatabaseServerSettings(provider, version);
    }
}
