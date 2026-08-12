using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CanDoItAll.Infrastructure.Configuration;

public static class DatabasePasswordFileConfiguration
{
    private const string ConnectionStringKey = "Database:ConnectionString";
    private const string PasswordFileKey = "Database:PasswordFile";

    public static void Apply(IConfiguration configuration, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        var configuredPasswordFile = configuration[PasswordFileKey];
        if (string.IsNullOrWhiteSpace(configuredPasswordFile))
        {
            return;
        }

        var configuredConnectionString = configuration[ConnectionStringKey];
        if (string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            throw new InvalidOperationException(
                $"{ConnectionStringKey} is required when {PasswordFileKey} is configured.");
        }

        var passwordFilePath = Path.IsPathRooted(configuredPasswordFile)
            ? Path.GetFullPath(configuredPasswordFile)
            : Path.GetFullPath(configuredPasswordFile, contentRootPath);
        if (!File.Exists(passwordFilePath))
        {
            throw new FileNotFoundException(
                $"The configured database password file '{passwordFilePath}' was not found.",
                passwordFilePath);
        }

        var password = File.ReadAllText(passwordFilePath).TrimEnd('\r', '\n');
        if (password.Length == 0)
        {
            throw new InvalidOperationException(
                $"The configured database password file '{passwordFilePath}' is empty.");
        }

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(configuredConnectionString)
        {
            Password = password
        };
        configuration[ConnectionStringKey] = connectionStringBuilder.ConnectionString;
    }
}
