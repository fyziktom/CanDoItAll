using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CanDoItAll.Infrastructure.Configuration;

public static class DatabasePasswordFileConfiguration
{
    private const int MaximumPasswordFileBytes = 4096;
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

        var password = BoundedConfigurationSecretFileReader.Read(
            configuredPasswordFile,
            contentRootPath,
            "database password",
            MaximumPasswordFileBytes);

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(configuredConnectionString)
        {
            Password = password
        };
        configuration[ConnectionStringKey] = connectionStringBuilder.ConnectionString;
    }
}
