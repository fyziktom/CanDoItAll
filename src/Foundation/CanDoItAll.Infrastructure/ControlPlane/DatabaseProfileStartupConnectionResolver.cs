using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed record DatabaseStartupConnectionOptions(
    DatabaseProviderKind ProviderKind,
    string? ConnectionString,
    DatabaseProfileResolutionSource ResolutionSource,
    Guid? ProfileId);

public static class DatabaseProfileStartupConnectionResolver
{
    private const string DefaultEnvironmentName = "Production";
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static DatabaseStartupConnectionOptions? TryResolve(
        IConfiguration configuration,
        string? contentRootPath,
        string environmentName = DefaultEnvironmentName)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configuredProvider = configuration["Database:Provider"];
        var configuredConnection = configuration["Database:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(configuredProvider) ||
            !string.IsNullOrWhiteSpace(configuredConnection))
        {
            DatabaseProviderKind providerKind = ParseProviderKind(configuredProvider, configuredConnection);
            return new DatabaseStartupConnectionOptions(
                providerKind,
                providerKind == DatabaseProviderKind.InMemory
                    ? InMemoryDatabaseIdentity.ResolveOverrideName(configuredConnection)
                    : configuredConnection,
                DatabaseProfileResolutionSource.ExplicitOverride,
                ProfileId: null);
        }

        return TryResolvePersistedActiveProfile(configuration, contentRootPath, environmentName);
    }

    private static DatabaseStartupConnectionOptions? TryResolvePersistedActiveProfile(
        IConfiguration configuration,
        string? contentRootPath,
        string environmentName)
    {
        var controlPlaneRoot = ResolveControlPlaneRootPath(configuration, contentRootPath);
        var databaseProfilesRoot = Path.Combine(controlPlaneRoot, "database-profiles");
        var catalogPath = Path.Combine(databaseProfilesRoot, "catalog.json");
        if (!File.Exists(catalogPath))
        {
            return CreateDefaultPostgreSqlStartupOptions();
        }

        var durableFileWriter = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        using IDisposable coordination = ControlPlaneFileCoordination.Acquire(
            durableFileWriter,
            controlPlaneRoot,
            ControlPlaneCoordinationScope.DatabaseProfiles);
        LegacyDatabaseProfileCatalogQuarantine.QuarantineIfNeeded(
            controlPlaneRoot,
            catalogPath,
            Path.Combine(databaseProfilesRoot, "active-profile.json"),
            durableFileWriter,
            logger: null);

        var catalog = ReadDocument(
            catalogPath,
            static () => new DatabaseProfileCatalogDocument());
        if (catalog.Profiles.Count == 0)
        {
            return CreateDefaultPostgreSqlStartupOptions();
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

        EnsureWorkspacePathReady(activeProfile, contentRootPath);

        return new DatabaseStartupConnectionOptions(
            activeProfile.ProviderKind,
            BuildConnectionString(
                activeProfile,
                configuration,
                contentRootPath,
                environmentName,
                controlPlaneRoot),
            resolutionSource,
            activeProfile.Id);
    }

    private static DatabaseStartupConnectionOptions CreateDefaultPostgreSqlStartupOptions()
    {
        return new DatabaseStartupConnectionOptions(
            DatabaseProviderKind.PostgreSql,
            "Host=localhost;Database=candoitall;Username=postgres;Password=postgres",
            DatabaseProfileResolutionSource.AutoProvisionedPostgreSql,
            ProfileId: null);
    }

    private static string? BuildConnectionString(
        DatabaseProfileRecord profile,
        IConfiguration configuration,
        string? contentRootPath,
        string environmentName,
        string controlPlaneRoot)
    {
        return profile.ProviderKind switch
        {
            DatabaseProviderKind.PostgreSql => BuildPostgreSqlConnectionString(
                profile,
                configuration,
                contentRootPath,
                environmentName,
                controlPlaneRoot),
            DatabaseProviderKind.InMemory => profile.InMemory?.DatabaseName ?? throw new InvalidOperationException("In-memory profile is missing a database name."),
            _ => throw new InvalidOperationException($"Unsupported provider '{profile.ProviderKind}'.")
        };
    }

    private static string BuildPostgreSqlConnectionString(
        DatabaseProfileRecord profile,
        IConfiguration configuration,
        string? contentRootPath,
        string environmentName,
        string controlPlaneRoot)
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
            builder.Password = UnprotectControlPlaneSecret(
                configuration,
                contentRootPath,
                environmentName,
                controlPlaneRoot,
                descriptor.EncryptedPassword);
        }

        return builder.ConnectionString;
    }

    private static string UnprotectControlPlaneSecret(
        IConfiguration configuration,
        string? contentRootPath,
        string environmentName,
        string controlPlaneRoot,
        string protectedValue)
    {
        string keysPath = ResolveDataProtectionKeysPath(configuration, contentRootPath, controlPlaneRoot);
        string resolvedContentRoot = string.IsNullOrWhiteSpace(contentRootPath)
            ? Directory.GetCurrentDirectory()
            : contentRootPath;
        var durableFileWriter = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        var services = new ServiceCollection();
        DataProtectionKeyRingProtection.Configure(
            services.AddDataProtection(),
            configuration,
            string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase),
            resolvedContentRoot,
            keysPath,
            durableFileWriter);

        using var provider = services.BuildServiceProvider();
        return provider
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("CanDoItAll.ControlPlaneSecrets")
            .Unprotect(protectedValue);
    }

    private static string ResolveDataProtectionKeysPath(
        IConfiguration configuration,
        string? contentRootPath,
        string controlPlaneRoot)
    {
        string? configuredPath = configuration["ControlPlane:DataProtectionKeysPath"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return ControlPlanePathDefaults.ResolveConfiguredPath(
                contentRootPath ?? Directory.GetCurrentDirectory(),
                configuredPath);
        }

        return !string.IsNullOrWhiteSpace(configuration["ControlPlane:RootPath"])
            ? Path.Combine(controlPlaneRoot, "dataprotection-keys")
            : ApplicationPurposeRootPolicy.ResolveCurrent().DataProtectionKeysRoot;
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

        return ApplicationPurposeRootPolicy.ResolveCurrent().ControlPlaneRoot;
    }

    private static void EnsureWorkspacePathReady(
        DatabaseProfileRecord profile,
        string? contentRootPath)
    {
        if (profile.Storage.WorkspacePath is null &&
            !string.IsNullOrWhiteSpace(profile.Storage.LegacyWorkspaceRoot))
        {
            string legacyRoot = profile.Storage.LegacyWorkspaceRoot;
            if (PhysicalPathSyntaxPolicy.Classify(legacyRoot) == PhysicalPathSyntax.Relative)
            {
                legacyRoot = ControlPlanePathDefaults.ResolveConfiguredPath(
                    contentRootPath ?? Directory.GetCurrentDirectory(),
                    legacyRoot);
            }

            profile.Storage.WorkspacePath = HostBoundPathPolicy.ImportLegacy(
                legacyRoot,
                HostPathContext.CaptureCurrent());
            profile.Storage.LegacyWorkspaceRoot = null;
        }

        HostBoundPathPolicy.ResolveRequired(
            profile.Storage.WorkspacePath,
            "database profile workspace");
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

        if (!string.IsNullOrWhiteSpace(configuredConnection))
        {
            throw new InvalidOperationException("Database connection string does not look like a PostgreSQL connection string.");
        }

        return DatabaseProviderKind.PostgreSql;
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
