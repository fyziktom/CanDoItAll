using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed record DatabaseStartupConnectionOptions(
    DatabaseProviderKind ProviderKind,
    string? ConnectionString,
    DatabaseProfileResolutionSource ResolutionSource,
    Guid? ProfileId);

public static class DatabaseProfileStartupConnectionResolver
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static DatabaseStartupConnectionOptions? TryResolve(
        IConfiguration configuration,
        string? contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configuredProvider = configuration["Database:Provider"];
        var configuredConnection = configuration["Database:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(configuredProvider) ||
            !string.IsNullOrWhiteSpace(configuredConnection))
        {
            return new DatabaseStartupConnectionOptions(
                ParseProviderKind(configuredProvider, configuredConnection),
                configuredConnection,
                DatabaseProfileResolutionSource.ExplicitOverride,
                ProfileId: null);
        }

        return TryResolvePersistedActiveProfile(configuration, contentRootPath);
    }

    private static DatabaseStartupConnectionOptions? TryResolvePersistedActiveProfile(
        IConfiguration configuration,
        string? contentRootPath)
    {
        var controlPlaneRoot = ResolveControlPlaneRootPath(configuration, contentRootPath);
        var databaseProfilesRoot = Path.Combine(controlPlaneRoot, "database-profiles");
        var catalogPath = Path.Combine(databaseProfilesRoot, "catalog.json");
        if (!File.Exists(catalogPath))
        {
            return null;
        }

        var catalog = ReadDocument(
            catalogPath,
            static () => new DatabaseProfileCatalogDocument());
        if (catalog.Profiles.Count == 0)
        {
            return null;
        }

        var activeState = ReadDocument(
            Path.Combine(databaseProfilesRoot, "active-profile.json"),
            static () => new DatabaseActiveProfileState());
        var activeProfile = activeState.ActiveProfileId.HasValue
            ? catalog.Profiles.FirstOrDefault(item => item.Id == activeState.ActiveProfileId.Value)
            : null;
        var resolutionSource = DatabaseProfileResolutionSource.PersistedActiveProfile;
        if (activeProfile is null)
        {
            activeProfile = catalog.Profiles
                .OrderByDescending(item => item.Audit.LastSuccessfulOpenUtc ?? item.Audit.LastUsedUtc ?? item.Audit.CreatedUtc)
                .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .First();
            resolutionSource = DatabaseProfileResolutionSource.PersistedCatalogFallback;
        }

        return new DatabaseStartupConnectionOptions(
            activeProfile.ProviderKind,
            BuildConnectionString(activeProfile, controlPlaneRoot),
            resolutionSource,
            activeProfile.Id);
    }

    private static string? BuildConnectionString(DatabaseProfileRecord profile, string controlPlaneRoot)
    {
        return profile.ProviderKind switch
        {
            DatabaseProviderKind.Sqlite => $"Data Source={profile.Sqlite?.DatabasePath ?? throw new InvalidOperationException("SQLite profile is missing a database path.")}",
            DatabaseProviderKind.PostgreSql => BuildPostgreSqlConnectionString(profile, controlPlaneRoot),
            DatabaseProviderKind.InMemory => profile.InMemory?.DatabaseName ?? throw new InvalidOperationException("In-memory profile is missing a database name."),
            _ => throw new InvalidOperationException($"Unsupported provider '{profile.ProviderKind}'.")
        };
    }

    private static string BuildPostgreSqlConnectionString(DatabaseProfileRecord profile, string controlPlaneRoot)
    {
        var descriptor = profile.PostgreSql
            ?? throw new InvalidOperationException("PostgreSQL profile is missing connection metadata.");
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = descriptor.Host,
            Port = descriptor.Port,
            Database = descriptor.DatabaseName,
            Username = descriptor.Username
        };

        if (!string.IsNullOrWhiteSpace(descriptor.EncryptedPassword))
        {
            builder.Password = UnprotectControlPlaneSecret(controlPlaneRoot, descriptor.EncryptedPassword);
        }

        return builder.ConnectionString;
    }

    private static string UnprotectControlPlaneSecret(string controlPlaneRoot, string protectedValue)
    {
        var keysPath = Path.Combine(controlPlaneRoot, "dataprotection-keys");
        var services = new ServiceCollection();
        services.AddDataProtection()
            .SetApplicationName("CanDoItAll")
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        using var provider = services.BuildServiceProvider();
        return provider
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("CanDoItAll.ControlPlaneSecrets")
            .Unprotect(protectedValue);
    }

    private static string ResolveControlPlaneRootPath(
        IConfiguration configuration,
        string? contentRootPath)
    {
        var configuredRoot = configuration["ControlPlane:RootPath"];
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            var rootBasePath = string.IsNullOrWhiteSpace(contentRootPath)
                ? Directory.GetCurrentDirectory()
                : contentRootPath;
            return ControlPlanePathDefaults.ResolveConfiguredPath(rootBasePath, configuredRoot);
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return Path.Combine(localAppData, "CanDoItAll", "control-plane");
        }

        return Path.Combine(contentRootPath ?? Directory.GetCurrentDirectory(), ".artifacts", "control-plane");
    }

    private static T ReadDocument<T>(string path, Func<T> createDefault)
    {
        if (!File.Exists(path))
        {
            return createDefault();
        }

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return createDefault();
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, SerializerOptions) ?? createDefault();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Control-plane document '{path}' is invalid.", ex);
        }
    }

    private static DatabaseProviderKind ParseProviderKind(string? configuredProvider, string? configuredConnection)
    {
        if (!string.IsNullOrWhiteSpace(configuredProvider))
        {
            return configuredProvider.Trim().ToLowerInvariant() switch
            {
                "sqlite" => DatabaseProviderKind.Sqlite,
                "postgres" or "postgresql" => DatabaseProviderKind.PostgreSql,
                "inmemory" or "memory" => DatabaseProviderKind.InMemory,
                _ => throw new InvalidOperationException($"Unsupported database provider '{configuredProvider}'.")
            };
        }

        if (!string.IsNullOrWhiteSpace(configuredConnection) &&
            configuredConnection.Contains("host=", StringComparison.OrdinalIgnoreCase))
        {
            return DatabaseProviderKind.PostgreSql;
        }

        return DatabaseProviderKind.Sqlite;
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class DatabaseProfileCatalogDocument
    {
        public int SchemaVersion { get; set; } = 1;

        public List<DatabaseProfileRecord> Profiles { get; set; } = [];
    }
}
