using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed class ControlPlaneSecretProtector(IDataProtectionProvider dataProtectionProvider) : IControlPlaneSecretProtector
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("CanDoItAll.ControlPlaneSecrets");

    public string Protect(string plainText) => _protector.Protect(plainText);

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}

public sealed class DatabaseProfileControlPlaneService(
    IConfiguration configuration,
    IOptions<StorageOptions> storageOptions,
    IHostEnvironment hostEnvironment,
    IControlPlanePathResolver controlPlanePathResolver,
    IControlPlaneSecretProtector secretProtector,
    DurableFileWriter durableFileWriter,
    IClock clock,
    ILogger<DatabaseProfileControlPlaneService> logger) :
    IDatabaseProfileService,
    IDatabaseProfileRuntimeAccessor,
    IControlPlaneSecretContinuityVerifier
{
    private const int CurrentCatalogSchemaVersion = 2;
    private const string WorkspacePathMigrationDirectoryName = "workspace-path-v2";
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private readonly StorageOptions _storageOptions = storageOptions.Value;
    private readonly object _sync = new();
    private string? _lastLoggedSelectionKey;

    public Task<IReadOnlyList<DatabaseProfileSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            using IDisposable coordination = AcquireCoordination(cancellationToken);
            DatabaseProfileCatalogDocument document = ReadCatalogLocked();
            Guid? activeProfileId = TryResolveExplicitOverrideLocked() is null
                ? ReadActiveProfileStateLocked().ActiveProfileId
                : null;
            var summaries = document
                .Profiles
                .Where(IsPersistedRuntimeProfile)
                .OrderBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(profile => CreateSummary(
                    profile,
                    activeProfileId == profile.Id))
                .ToList();

            return Task.FromResult<IReadOnlyList<DatabaseProfileSummary>>(summaries);
        }
    }

    public Task<ControlPlaneSecretContinuityReport> VerifyAsync(
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            using IDisposable coordination = AcquireCoordination(cancellationToken);
            int protectedPasswordCount = 0;
            foreach (DatabaseProfileRecord profile in ReadCatalogLocked().Profiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string? protectedPassword = profile.PostgreSql?.EncryptedPassword;
                if (string.IsNullOrWhiteSpace(protectedPassword))
                {
                    continue;
                }

                _ = secretProtector.Unprotect(protectedPassword);
                protectedPasswordCount++;
            }

            return Task.FromResult(new ControlPlaneSecretContinuityReport(protectedPasswordCount));
        }
    }

    public Task<DatabaseProfileEditorModel> GetEditorAsync(Guid? id = null, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            using IDisposable coordination = AcquireCoordination(cancellationToken);
            if (!id.HasValue)
            {
                return Task.FromResult(CreateDefaultEditor());
            }

            var profile = ReadCatalogLocked().Profiles.FirstOrDefault(item => item.Id == id.Value && IsPersistedRuntimeProfile(item));
            if (profile is null)
            {
                return Task.FromResult(CreateDefaultEditor());
            }

            return Task.FromResult(CreateEditor(profile));
        }
    }

    public Result Validate(DatabaseProfileEditorModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var errors = new List<Error>();
        if (string.IsNullOrWhiteSpace(model.DisplayName))
        {
            errors.Add(Error.Validation("Database profile name is required."));
        }

        switch (model.ProviderKind)
        {
            case DatabaseProviderKind.PostgreSql:
                if (model.SourceKind != DatabaseProfileSourceKind.PostgresConnection)
                {
                    errors.Add(Error.Validation("PostgreSQL profiles must use the PostgresConnection source kind."));
                }

                if (string.IsNullOrWhiteSpace(model.PostgresHost))
                {
                    errors.Add(Error.Validation("PostgreSQL host is required."));
                }

                if (model.PostgresPort is < 1 or > 65535)
                {
                    errors.Add(Error.Validation("PostgreSQL port must be between 1 and 65535."));
                }

                if (string.IsNullOrWhiteSpace(model.PostgresDatabaseName))
                {
                    errors.Add(Error.Validation("PostgreSQL database name is required."));
                }

                if (string.IsNullOrWhiteSpace(model.PostgresUsername))
                {
                    errors.Add(Error.Validation("PostgreSQL username is required."));
                }
                break;

            case DatabaseProviderKind.InMemory:
                errors.Add(Error.Validation("In-memory database profiles are only allowed through explicit runtime overrides and test harness configuration."));
                break;

            default:
                errors.Add(Error.Validation($"Unsupported database provider '{model.ProviderKind}'."));
                break;
        }

        return errors.Count == 0 ? Result.Success() : Result.Failure(errors);
    }

    public Task<Result<Guid>> SaveAsync(DatabaseProfileEditorModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var validation = Validate(model);
        if (validation.IsFailure)
        {
            return Task.FromResult(Result<Guid>.Failure(validation.Errors));
        }

        lock (_sync)
        {
            using IDisposable coordination = AcquireCoordination(cancellationToken);
            var document = ReadCatalogLocked();
            var existing = model.Id.HasValue
                ? document.Profiles.FirstOrDefault(item => item.Id == model.Id.Value)
                : null;

            if (existing?.Runtime.LockedByRuntimeOverride == true)
            {
                return Task.FromResult(Result<Guid>.Failure(Error.Failure("Runtime override profiles cannot be persisted.")));
            }

            var profile = BuildPersistedProfile(model, existing);
            UpsertProfile(document, profile);
            WriteCatalogLocked(document);

            var activeState = ReadActiveProfileStateLocked();
            if (!activeState.ActiveProfileId.HasValue)
            {
                activeState.ActiveProfileId = profile.Id;
                WriteActiveProfileStateLocked(activeState);
            }

            ClearSelectionLogLocked();
            return Task.FromResult(Result<Guid>.Success(profile.Id));
        }
    }

    public Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            using IDisposable coordination = AcquireCoordination(cancellationToken);
            var document = ReadCatalogLocked();
            var profile = document.Profiles.FirstOrDefault(item => item.Id == id && IsPersistedRuntimeProfile(item));
            if (profile is null)
            {
                return Task.FromResult(Result.Failure(Error.Validation("Database profile not found.")));
            }

            document.Profiles.Remove(profile);
            WriteCatalogLocked(document);

            var activeState = ReadActiveProfileStateLocked();
            if (activeState.ActiveProfileId == id)
            {
                activeState.ActiveProfileId = null;
                WriteActiveProfileStateLocked(activeState);
            }

            ClearSelectionLogLocked();
            return Task.FromResult(Result.Success());
        }
    }

    public Task<Result> ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            using IDisposable coordination = AcquireCoordination(cancellationToken);
            var document = ReadCatalogLocked();
            var profile = document.Profiles.FirstOrDefault(item => item.Id == id && IsPersistedRuntimeProfile(item));
            if (profile is null)
            {
                return Task.FromResult(Result.Failure(Error.Validation("Database profile not found.")));
            }

            if (!HostBoundPathPolicy.TryResolve(
                    profile.Storage.WorkspacePath,
                    HostPathContext.CaptureCurrent(),
                    out _,
                    out string diagnostic))
            {
                return Task.FromResult(Result.Failure(Error.Validation(
                    $"The database profile workspace is unavailable. {diagnostic}")));
            }

            profile.Audit.LastUsedUtc = clock.GetUtcNow();
            UpsertProfile(document, profile);
            WriteCatalogLocked(document);

            var activeState = ReadActiveProfileStateLocked();
            activeState.ActiveProfileId = id;
            WriteActiveProfileStateLocked(activeState);

            ClearSelectionLogLocked();
            return Task.FromResult(Result.Success());
        }
    }

    public Task<Result> RebindWorkspaceAsync(
        Guid id,
        string workspaceRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        lock (_sync)
        {
            using IDisposable coordination = AcquireCoordination(cancellationToken);
            DatabaseProfileCatalogDocument document = ReadCatalogLocked();
            DatabaseProfileRecord? profile = document.Profiles.FirstOrDefault(item => item.Id == id && IsPersistedRuntimeProfile(item));
            if (profile is null)
            {
                return Task.FromResult(Result.Failure(Error.Validation("Database profile not found.")));
            }

            string resolvedRoot = ControlPlanePathDefaults.ResolveConfiguredPath(
                hostEnvironment.ContentRootPath,
                workspaceRoot);
            profile.Storage.WorkspacePath = HostBoundPathPolicy.RebindCurrent(resolvedRoot, clock.GetUtcNow());
            profile.Storage.LegacyWorkspaceRoot = null;
            profile.Runtime.Fingerprint = BuildFingerprint(profile);
            WriteCatalogLocked(document);
            ClearSelectionLogLocked();
            return Task.FromResult(Result.Success());
        }
    }

    public Task<Result> RollbackWorkspacePathMigrationAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            using IDisposable coordination = AcquireCoordination(cancellationToken);
            string migrationRoot = ResolveWorkspacePathMigrationRoot();
            string backupPath = Path.Combine(migrationRoot, "catalog.v1.backup.json");
            if (!File.Exists(backupPath))
            {
                return Task.FromResult(Result.Failure(Error.Validation(
                    "No database-profile workspace-path migration backup is available.")));
            }

            string backupJson;
            try
            {
                backupJson = MigrationBackupIntegrity.ReadVerified(backupPath);
            }
            catch (InvalidOperationException exception)
            {
                return Task.FromResult(Result.Failure(Error.Failure(exception.Message)));
            }
            DatabaseProfileCatalogDocument backup = DeserializeCatalog(backupJson);
            if (backup.SchemaVersion != 1)
            {
                return Task.FromResult(Result.Failure(Error.Failure(
                    "The database-profile migration backup has an unexpected schema version.")));
            }

            string commitPath = Path.Combine(migrationRoot, "commit.json");
            if (File.Exists(commitPath))
            {
                WorkspacePathMigrationManifest? commit;
                try
                {
                    commit = JsonSerializer.Deserialize<WorkspacePathMigrationManifest>(
                        File.ReadAllText(commitPath),
                        SerializerOptions);
                }
                catch (JsonException)
                {
                    return Task.FromResult(Result.Failure(Error.Failure(
                        "The database-profile migration commit marker is invalid.")));
                }

                string backupSha256 = ComputeSha256(backupJson);
                if (commit is null ||
                    commit.FormatVersion != 1 ||
                    !string.Equals(commit.State, "PointerCommitted", StringComparison.Ordinal) ||
                    !string.Equals(commit.SourceSha256, backupSha256, StringComparison.Ordinal))
                {
                    return Task.FromResult(Result.Failure(Error.Failure(
                        "The database-profile migration backup checksum is invalid.")));
                }
            }

            string currentCatalogPath = controlPlanePathResolver.ResolveCatalogFilePath();
            string preRollbackPath = Path.Combine(migrationRoot, "catalog.v2.pre-rollback.json");
            if (!File.Exists(preRollbackPath) && File.Exists(currentCatalogPath))
            {
                durableFileWriter.WriteText(
                    controlPlanePathResolver.ResolveRootPath(),
                    preRollbackPath,
                    File.ReadAllText(currentCatalogPath),
                    CreateNewPrivateWriteOptions());
            }

            durableFileWriter.WriteText(
                controlPlanePathResolver.ResolveRootPath(),
                currentCatalogPath,
                backupJson,
                DurableFileWriteOptions.Private);
            durableFileWriter.WriteText(
                controlPlanePathResolver.ResolveRootPath(),
                Path.Combine(migrationRoot, "rollback.commit.json"),
                JsonSerializer.Serialize(new
                {
                    formatVersion = 1,
                    state = "RolledBack",
                    rolledBackAtUtc = clock.GetUtcNow()
                }, SerializerOptions),
                DurableFileWriteOptions.Private);
            ClearSelectionLogLocked();
            return Task.FromResult(Result.Success());
        }
    }

    public Task<DatabaseSelectionStateModel> GetCurrentSelectionAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            using IDisposable coordination = AcquireCoordination(cancellationToken);
            var resolvedProfile = ResolveCurrentProfileLocked(logSelection: false);
            return Task.FromResult(new DatabaseSelectionStateModel
            {
                ActiveProfileId = resolvedProfile.Profile.Id,
                RuntimeProfileId = resolvedProfile.Profile.Id,
                DisplayName = resolvedProfile.Profile.DisplayName,
                ProviderKind = resolvedProfile.Profile.ProviderKind,
                SourceKind = resolvedProfile.Profile.SourceKind,
                ResolutionSource = resolvedProfile.ResolutionSource,
                IsRuntimeLocked = resolvedProfile.Profile.Runtime.LockedByRuntimeOverride,
                Fingerprint = resolvedProfile.Profile.Runtime.Fingerprint,
                WorkspaceRoot = resolvedProfile.Profile.Storage.WorkspaceRoot,
                WorkspacePathState = ResolveWorkspacePathState(resolvedProfile.Profile.Storage.WorkspacePath),
                Descriptor = BuildDescriptor(resolvedProfile.Profile)
            });
        }
    }

    public ResolvedDatabaseProfile ResolveCurrentProfile()
    {
        lock (_sync)
        {
            using IDisposable coordination = AcquireCoordination();
            return ResolveCurrentProfileLocked(logSelection: true);
        }
    }

    public ResolvedDatabaseProfile ResolveProfile(Guid profileId)
    {
        lock (_sync)
        {
            using IDisposable coordination = AcquireCoordination();
            var explicitOverride = TryResolveExplicitOverrideLocked();
            if (explicitOverride is not null && explicitOverride.Profile.Id == profileId)
            {
                return explicitOverride;
            }

            var document = ReadCatalogLocked();
            var profile = document.Profiles.FirstOrDefault(item => item.Id == profileId && IsPersistedRuntimeProfile(item))
                ?? throw new InvalidOperationException($"Database profile '{profileId}' was not found.");

            var activeProfileId = ReadActiveProfileStateLocked().ActiveProfileId;
            var resolutionSource = activeProfileId == profileId
                ? DatabaseProfileResolutionSource.PersistedActiveProfile
                : DatabaseProfileResolutionSource.PersistedCatalogFallback;

            return BuildResolvedProfile(profile, resolutionSource);
        }
    }

    private ResolvedDatabaseProfile ResolveCurrentProfileLocked(bool logSelection)
    {
        if (TryResolveExplicitOverrideLocked() is { } overrideProfile)
        {
            if (logSelection)
            {
                LogSelectionLocked(overrideProfile);
            }

            return overrideProfile;
        }

        var document = ReadCatalogLocked();
        var persistedRuntimeProfiles = document.Profiles
            .Where(IsPersistedRuntimeProfile)
            .ToList();
        if (persistedRuntimeProfiles.Count == 0)
        {
            var seededProfile = CreateDefaultPostgreSqlProfileLocked();
            document.Profiles.Add(seededProfile);
            WriteCatalogLocked(document);

            var newState = ReadActiveProfileStateLocked();
            newState.ActiveProfileId = seededProfile.Id;
            WriteActiveProfileStateLocked(newState);

            var resolvedSeed = BuildResolvedProfile(
                seededProfile,
                DatabaseProfileResolutionSource.AutoProvisionedPostgreSql);
            if (logSelection)
            {
                LogSelectionLocked(resolvedSeed);
            }

            return resolvedSeed;
        }

        var activeState = ReadActiveProfileStateLocked();
        DatabaseProfileRecord? activeProfile = null;
        DatabaseProfileResolutionSource resolution = DatabaseProfileResolutionSource.PersistedActiveProfile;

        if (activeState.ActiveProfileId.HasValue)
        {
            activeProfile = persistedRuntimeProfiles.FirstOrDefault(item => item.Id == activeState.ActiveProfileId.Value);
        }

        if (activeProfile is null)
        {
            activeProfile = persistedRuntimeProfiles
                .OrderByDescending(item => item.Audit.LastSuccessfulOpenUtc ?? item.Audit.LastUsedUtc ?? item.Audit.CreatedUtc)
                .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .First();

            activeState.ActiveProfileId = activeProfile.Id;
            WriteActiveProfileStateLocked(activeState);
            resolution = DatabaseProfileResolutionSource.PersistedCatalogFallback;
        }

        var resolvedProfile = BuildResolvedProfile(activeProfile, resolution);
        if (logSelection)
        {
            LogSelectionLocked(resolvedProfile);
        }

        return resolvedProfile;
    }

    private ResolvedDatabaseProfile? TryResolveExplicitOverrideLocked()
    {
        var configuredProvider = configuration["Database:Provider"];
        var configuredConnection = configuration["Database:ConnectionString"];
        if (string.IsNullOrWhiteSpace(configuredProvider) && string.IsNullOrWhiteSpace(configuredConnection))
        {
            return null;
        }

        var providerKind = ParseProviderKind(configuredProvider, configuredConnection);
        var workspaceRoot = ResolveOverrideWorkspaceRoot(providerKind, configuredConnection);
        var now = clock.GetUtcNow();

        return providerKind switch
        {
            DatabaseProviderKind.PostgreSql => BuildPostgreSqlOverrideProfile(configuredConnection, workspaceRoot, now),
            DatabaseProviderKind.InMemory => BuildInMemoryOverrideProfile(configuredConnection, workspaceRoot, now),
            _ => throw new InvalidOperationException($"Unsupported database provider '{providerKind}'.")
        };
    }

    private ResolvedDatabaseProfile BuildPostgreSqlOverrideProfile(string? configuredConnection, string workspaceRoot, DateTimeOffset now)
    {
        var builder = string.IsNullOrWhiteSpace(configuredConnection)
            ? new NpgsqlConnectionStringBuilder("Host=localhost;Database=candoitall;Username=postgres;Password=postgres")
            : new NpgsqlConnectionStringBuilder(configuredConnection);

        var profile = new DatabaseProfileRecord
        {
            Id = CreateDeterministicGuid(
                $"postgres-override:{builder.Host}:{builder.Port}:{builder.Database}:{builder.Username}"),
            DisplayName = "Configured PostgreSQL override",
            ProviderKind = DatabaseProviderKind.PostgreSql,
            SourceKind = DatabaseProfileSourceKind.PostgresConnection,
            PostgreSql = new PostgreSqlDatabaseProfileConnection
            {
                Host = builder.Host ?? "localhost",
                Port = builder.Port,
                DatabaseName = builder.Database ?? "candoitall",
                Username = builder.Username ?? "postgres",
                EncryptedPassword = string.IsNullOrWhiteSpace(builder.Password)
                    ? null
                    : secretProtector.Protect(builder.Password),
                AdminDatabaseName = string.IsNullOrWhiteSpace(builder.Database) ? null : builder.Database,
                TrustServerCertificate = false
            },
            Storage = new DatabaseProfileStorageDescriptor
            {
                Mode = DatabaseProfileStorageMode.ExternalWorkspaceRoot,
                WorkspaceRoot = workspaceRoot
            },
            Runtime = new DatabaseProfileRuntimeMetadata
            {
                LockedByRuntimeOverride = true
            },
            Audit = new DatabaseProfileAuditMetadata
            {
                CreatedUtc = now,
                LastUsedUtc = now
            }
        };

        profile.Runtime.Fingerprint = BuildFingerprint(profile);
        return new ResolvedDatabaseProfile(
            profile,
            DatabaseProfileResolutionSource.ExplicitOverride,
            builder.ConnectionString);
    }

    private ResolvedDatabaseProfile BuildInMemoryOverrideProfile(string? configuredConnection, string workspaceRoot, DateTimeOffset now)
    {
        string databaseName = InMemoryDatabaseIdentity.ResolveOverrideName(configuredConnection);

        var profile = new DatabaseProfileRecord
        {
            Id = CreateDeterministicGuid($"inmemory-override:{databaseName}"),
            DisplayName = "Configured in-memory override",
            ProviderKind = DatabaseProviderKind.InMemory,
            SourceKind = DatabaseProfileSourceKind.InMemory,
            InMemory = new InMemoryDatabaseProfileConnection
            {
                DatabaseName = databaseName
            },
            Storage = new DatabaseProfileStorageDescriptor
            {
                Mode = DatabaseProfileStorageMode.Ephemeral,
                WorkspaceRoot = workspaceRoot
            },
            Runtime = new DatabaseProfileRuntimeMetadata
            {
                LockedByRuntimeOverride = true
            },
            Audit = new DatabaseProfileAuditMetadata
            {
                CreatedUtc = now,
                LastUsedUtc = now
            }
        };

        profile.Runtime.Fingerprint = BuildFingerprint(profile);
        return BuildResolvedProfile(profile, DatabaseProfileResolutionSource.ExplicitOverride);
    }

    private DatabaseProfileRecord CreateDefaultPostgreSqlProfileLocked()
    {
        var profileId = Guid.NewGuid();
        var now = clock.GetUtcNow();

        logger.LogInformation(
            "Provisioning default PostgreSQL database profile for the main runtime.");

        var profile = new DatabaseProfileRecord
        {
            Id = profileId,
            DisplayName = "Local PostgreSQL",
            ProviderKind = DatabaseProviderKind.PostgreSql,
            SourceKind = DatabaseProfileSourceKind.PostgresConnection,
            PostgreSql = new PostgreSqlDatabaseProfileConnection
            {
                Host = "localhost",
                Port = 5432,
                DatabaseName = "candoitall",
                Username = "postgres",
                EncryptedPassword = secretProtector.Protect("postgres")
            },
            Storage = new DatabaseProfileStorageDescriptor
            {
                Mode = DatabaseProfileStorageMode.ExternalWorkspaceRoot,
                WorkspaceRoot = ResolveDefaultWorkspaceRoot()
            },
            Audit = new DatabaseProfileAuditMetadata
            {
                CreatedUtc = now,
                LastUsedUtc = now
            }
        };

        profile.Runtime.Fingerprint = BuildFingerprint(profile);
        return profile;
    }

    private DatabaseProfileRecord BuildPersistedProfile(DatabaseProfileEditorModel model, DatabaseProfileRecord? existing)
    {
        var profileId = existing?.Id ?? model.Id ?? Guid.NewGuid();
        var now = clock.GetUtcNow();
        var profile = new DatabaseProfileRecord
        {
            Id = profileId,
            DisplayName = NormalizeDisplayName(model.DisplayName, model.ProviderKind),
            ProviderKind = model.ProviderKind,
            SourceKind = NormalizeSourceKind(model.ProviderKind, model.SourceKind),
            Storage = new DatabaseProfileStorageDescriptor(),
            Audit = new DatabaseProfileAuditMetadata
            {
                CreatedUtc = existing?.Audit.CreatedUtc ?? now,
                LastUsedUtc = existing?.Audit.LastUsedUtc,
                LastSuccessfulOpenUtc = existing?.Audit.LastSuccessfulOpenUtc
            },
            Runtime = new DatabaseProfileRuntimeMetadata()
        };

        switch (profile.ProviderKind)
        {
            case DatabaseProviderKind.PostgreSql:
                profile.PostgreSql = new PostgreSqlDatabaseProfileConnection
                {
                    Host = model.PostgresHost.Trim(),
                    Port = model.PostgresPort,
                    DatabaseName = model.PostgresDatabaseName.Trim(),
                    Username = model.PostgresUsername.Trim(),
                    EncryptedPassword = string.IsNullOrWhiteSpace(model.PostgresPassword)
                        ? existing?.PostgreSql?.EncryptedPassword
                        : secretProtector.Protect(model.PostgresPassword),
                    AdminDatabaseName = string.IsNullOrWhiteSpace(model.PostgresAdminDatabaseName)
                        ? null
                        : model.PostgresAdminDatabaseName.Trim(),
                    TrustServerCertificate = model.PostgresTrustServerCertificate
                };
                profile.Storage = new DatabaseProfileStorageDescriptor
                {
                    Mode = DatabaseProfileStorageMode.ExternalWorkspaceRoot,
                    WorkspaceRoot = ResolveWorkspaceRootForProfile(profile, model, existing)
                };
                break;

            case DatabaseProviderKind.InMemory:
                profile.InMemory = new InMemoryDatabaseProfileConnection
                {
                    DatabaseName = string.IsNullOrWhiteSpace(model.InMemoryDatabaseName)
                        ? existing?.InMemory?.DatabaseName ?? "candoitall"
                        : model.InMemoryDatabaseName.Trim()
                };
                profile.Storage = new DatabaseProfileStorageDescriptor
                {
                    Mode = DatabaseProfileStorageMode.Ephemeral,
                    WorkspaceRoot = ResolveWorkspaceRootForProfile(profile, model, existing)
                };
                break;
        }

        profile.Runtime.Fingerprint = BuildFingerprint(profile);
        return profile;
    }

    private string ResolveWorkspaceRootForProfile(
        DatabaseProfileRecord profile,
        DatabaseProfileEditorModel model,
        DatabaseProfileRecord? existing)
    {
        if (!string.IsNullOrWhiteSpace(model.WorkspaceRoot))
        {
            return ControlPlanePathDefaults.ResolveConfiguredPath(hostEnvironment.ContentRootPath, model.WorkspaceRoot);
        }

        if (!string.IsNullOrWhiteSpace(existing?.Storage.WorkspaceRoot))
        {
            return HostBoundPathPolicy.ResolveRequired(
                existing.Storage.WorkspacePath,
                "database profile workspace");
        }

        return ResolveDefaultWorkspaceRoot();
    }

    private ResolvedDatabaseProfile BuildResolvedProfile(DatabaseProfileRecord profile, DatabaseProfileResolutionSource resolutionSource)
    {
        HostBoundPathPolicy.ResolveRequired(profile.Storage.WorkspacePath, "database profile workspace");
        var connectionString = profile.ProviderKind switch
        {
            DatabaseProviderKind.PostgreSql => BuildPostgreSqlConnectionString(profile),
            DatabaseProviderKind.InMemory => profile.InMemory?.DatabaseName ?? throw new InvalidOperationException("In-memory profile is missing a database name."),
            _ => throw new InvalidOperationException($"Unsupported provider '{profile.ProviderKind}'.")
        };

        return new ResolvedDatabaseProfile(profile, resolutionSource, connectionString);
    }

    private string BuildPostgreSqlConnectionString(DatabaseProfileRecord profile)
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
            builder.Password = secretProtector.Unprotect(descriptor.EncryptedPassword);
        }

        return builder.ConnectionString;
    }

    private DatabaseProfileSummary CreateSummary(DatabaseProfileRecord profile, bool isActive)
    {
        return new DatabaseProfileSummary(
            profile.Id,
            profile.DisplayName,
            profile.ProviderKind,
            profile.SourceKind,
            BuildDescriptor(profile),
            profile.Runtime.Fingerprint,
            isActive,
            profile.Runtime.LockedByRuntimeOverride,
            profile.Audit.CreatedUtc,
            profile.Audit.LastUsedUtc)
        {
            WorkspacePathState = ResolveWorkspacePathState(profile.Storage.WorkspacePath)
        };
    }

    private DatabaseProfileEditorModel CreateEditor(DatabaseProfileRecord profile)
    {
        return new DatabaseProfileEditorModel
        {
            Id = profile.Id,
            DisplayName = profile.DisplayName,
            ProviderKind = profile.ProviderKind,
            SourceKind = profile.SourceKind,
            WorkspaceRoot = profile.Storage.WorkspaceRoot,
            WorkspacePathState = ResolveWorkspacePathState(profile.Storage.WorkspacePath),
            PostgresHost = profile.PostgreSql?.Host ?? "localhost",
            PostgresPort = profile.PostgreSql?.Port ?? 5432,
            PostgresDatabaseName = profile.PostgreSql?.DatabaseName ?? "candoitall",
            PostgresUsername = profile.PostgreSql?.Username ?? "postgres",
            PostgresPassword = string.IsNullOrWhiteSpace(profile.PostgreSql?.EncryptedPassword)
                ? string.Empty
                : secretProtector.Unprotect(profile.PostgreSql.EncryptedPassword),
            PostgresAdminDatabaseName = profile.PostgreSql?.AdminDatabaseName,
            PostgresTrustServerCertificate = profile.PostgreSql?.TrustServerCertificate ?? false,
            IsRuntimeLocked = profile.Runtime.LockedByRuntimeOverride
        };
    }

    private static DatabaseProfileEditorModel CreateDefaultEditor()
    {
        return new DatabaseProfileEditorModel
        {
            DisplayName = "PostgreSQL workspace",
            ProviderKind = DatabaseProviderKind.PostgreSql,
            SourceKind = DatabaseProfileSourceKind.PostgresConnection
        };
    }

    private string ResolveOverrideWorkspaceRoot(DatabaseProviderKind providerKind, string? configuredConnection)
    {
        var configuredWorkspaceRoot = configuration["Storage:WorkspaceRoot"];
        if (!string.IsNullOrWhiteSpace(configuredWorkspaceRoot))
        {
            return ControlPlanePathDefaults.ResolveConfiguredPath(hostEnvironment.ContentRootPath, configuredWorkspaceRoot);
        }

        return ResolveRuntimeOverrideWorkspaceRoot(providerKind, configuredConnection);
    }

    private string ResolveDefaultWorkspaceRoot()
    {
        return string.IsNullOrWhiteSpace(_storageOptions.WorkspaceRoot)
            ? ApplicationPurposeRootPolicy.ResolveCurrent().WorkspaceRoot
            : ControlPlanePathDefaults.ResolveConfiguredPath(hostEnvironment.ContentRootPath, _storageOptions.WorkspaceRoot);
    }

    private string ResolveRuntimeOverrideWorkspaceRoot(
        DatabaseProviderKind providerKind,
        string? configuredConnection)
    {
        var workspaceKey = CreateDeterministicGuid(
                $"runtime-override-workspace:{BuildRuntimeOverrideWorkspaceFingerprint(providerKind, configuredConnection)}")
            .ToString("N");
        return Path.Combine(ResolveDefaultWorkspaceRoot(), "runtime-overrides", workspaceKey);
    }

    private static string BuildRuntimeOverrideWorkspaceFingerprint(
        DatabaseProviderKind providerKind,
        string? configuredConnection)
    {
        return providerKind switch
        {
            DatabaseProviderKind.PostgreSql => BuildPostgreSqlOverrideWorkspaceFingerprint(configuredConnection),
            DatabaseProviderKind.InMemory => InMemoryDatabaseIdentity.CreateFingerprint(
                InMemoryDatabaseIdentity.ResolveOverrideName(configuredConnection)),
            _ => providerKind.ToString()
        };
    }

    private static string BuildPostgreSqlOverrideWorkspaceFingerprint(string? configuredConnection)
    {
        var builder = string.IsNullOrWhiteSpace(configuredConnection)
            ? new NpgsqlConnectionStringBuilder("Host=localhost;Database=candoitall;Username=postgres;Password=postgres")
            : new NpgsqlConnectionStringBuilder(configuredConnection);
        var host = (builder.Host ?? string.Empty).Trim().ToLowerInvariant();
        var database = (builder.Database ?? string.Empty).Trim().ToLowerInvariant();
        var username = (builder.Username ?? string.Empty).Trim().ToLowerInvariant();
        return $"postgres:{host}:{builder.Port}:{database}:{username}";
    }

    private static string NormalizeDisplayName(string displayName, DatabaseProviderKind providerKind)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        return providerKind switch
        {
            DatabaseProviderKind.PostgreSql => "PostgreSQL profile",
            DatabaseProviderKind.InMemory => "In-memory profile",
            _ => "Database profile"
        };
    }

    private static DatabaseProfileSourceKind NormalizeSourceKind(DatabaseProviderKind providerKind, DatabaseProfileSourceKind sourceKind)
    {
        return providerKind switch
        {
            DatabaseProviderKind.PostgreSql => DatabaseProfileSourceKind.PostgresConnection,
            DatabaseProviderKind.InMemory => DatabaseProfileSourceKind.InMemory,
            _ => sourceKind
        };
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

    private static string BuildDescriptor(DatabaseProfileRecord profile)
    {
        return profile.ProviderKind switch
        {
            DatabaseProviderKind.PostgreSql => profile.PostgreSql is null
                ? string.Empty
                : $"{profile.PostgreSql.Host}:{profile.PostgreSql.Port}/{profile.PostgreSql.DatabaseName}",
            DatabaseProviderKind.InMemory => profile.InMemory?.DatabaseName ?? string.Empty,
            _ => string.Empty
        };
    }

    private static string BuildFingerprint(DatabaseProfileRecord profile)
    {
        return profile.ProviderKind switch
        {
            DatabaseProviderKind.PostgreSql
                => $"postgres:{profile.PostgreSql?.Host.Trim().ToLowerInvariant()}:{profile.PostgreSql?.Port}:{profile.PostgreSql?.DatabaseName.Trim().ToLowerInvariant()}:{profile.PostgreSql?.Username.Trim().ToLowerInvariant()}",
            DatabaseProviderKind.InMemory
                => InMemoryDatabaseIdentity.CreateFingerprint(profile.InMemory?.DatabaseName),
            _ => throw new InvalidOperationException($"Unsupported provider '{profile.ProviderKind}'.")
        };
    }

    private static bool IsPersistedRuntimeProfile(DatabaseProfileRecord profile)
    {
        return profile.ProviderKind == DatabaseProviderKind.PostgreSql &&
            profile.SourceKind == DatabaseProfileSourceKind.PostgresConnection;
    }

    private void LogSelectionLocked(ResolvedDatabaseProfile resolvedProfile)
    {
        var selectionKey =
            $"{resolvedProfile.ResolutionSource}:{resolvedProfile.Profile.Id:N}:{resolvedProfile.Profile.Runtime.LockedByRuntimeOverride}";
        if (string.Equals(_lastLoggedSelectionKey, selectionKey, StringComparison.Ordinal))
        {
            return;
        }

        _lastLoggedSelectionKey = selectionKey;
        logger.LogInformation(
            "Resolved database profile {DisplayName} ({ProfileId}) via {ResolutionSource}. Provider={ProviderKind}. Fingerprint={Fingerprint}. RuntimeLocked={RuntimeLocked}.",
            resolvedProfile.Profile.DisplayName,
            resolvedProfile.Profile.Id,
            resolvedProfile.ResolutionSource,
            resolvedProfile.Profile.ProviderKind,
            resolvedProfile.Profile.Runtime.Fingerprint,
            resolvedProfile.Profile.Runtime.LockedByRuntimeOverride);
    }

    private void ClearSelectionLogLocked() => _lastLoggedSelectionKey = null;

    private DatabaseProfileCatalogDocument ReadCatalogLocked()
    {
        LegacyDatabaseProfileCatalogQuarantine.QuarantineIfNeeded(
            controlPlanePathResolver.ResolveRootPath(),
            controlPlanePathResolver.ResolveCatalogFilePath(),
            controlPlanePathResolver.ResolveActiveProfileStateFilePath(),
            durableFileWriter,
            logger);

        string catalogPath = controlPlanePathResolver.ResolveCatalogFilePath();
        DatabaseProfileCatalogDocument document = ReadDocument(
            catalogPath,
            static () => new DatabaseProfileCatalogDocument());
        if (document.SchemaVersion > CurrentCatalogSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported database profile catalog schema version '{document.SchemaVersion}'.");
        }

        if (document.SchemaVersion < CurrentCatalogSchemaVersion)
        {
            return MigrateCatalogWorkspacePathsLocked(catalogPath, document);
        }

        EnsureWorkspacePathMigrationCommitMarker(document);
        return document;
    }

    private DatabaseActiveProfileState ReadActiveProfileStateLocked()
    {
        return ReadDocument(
            controlPlanePathResolver.ResolveActiveProfileStateFilePath(),
            static () => new DatabaseActiveProfileState());
    }

    private void WriteCatalogLocked(DatabaseProfileCatalogDocument document)
    {
        document.SchemaVersion = CurrentCatalogSchemaVersion;
        WriteDocument(controlPlanePathResolver.ResolveCatalogFilePath(), document);
    }

    private DatabaseProfileCatalogDocument MigrateCatalogWorkspacePathsLocked(
        string catalogPath,
        DatabaseProfileCatalogDocument document)
    {
        string sourceJson = File.Exists(catalogPath)
            ? File.ReadAllText(catalogPath)
            : JsonSerializer.Serialize(document, SerializerOptions);
        string migrationRoot = ResolveWorkspacePathMigrationRoot();
        durableFileWriter.EnsureDirectory(
            controlPlanePathResolver.ResolveRootPath(),
            migrationRoot,
            requirePrivateUnixMode: true);
        string backupPath = Path.Combine(migrationRoot, "catalog.v1.backup.json");
        string backupJson = MigrationBackupIntegrity.CreateOrVerify(
            durableFileWriter,
            controlPlanePathResolver.ResolveRootPath(),
            backupPath,
            sourceJson);

        Dictionary<Guid, string?> protectedPasswords = document.Profiles.ToDictionary(
            profile => profile.Id,
            profile => profile.PostgreSql?.EncryptedPassword);
        HostPathContext currentHost = HostPathContext.CaptureCurrent();
        foreach (DatabaseProfileRecord profile in document.Profiles)
        {
            if (profile.Storage.WorkspacePath is not null)
            {
                continue;
            }

            string? legacyRoot = profile.Storage.LegacyWorkspaceRoot;
            if (string.IsNullOrWhiteSpace(legacyRoot))
            {
                continue;
            }

            string migrationCandidate = PhysicalPathSyntaxClassifier.Classify(legacyRoot) == PhysicalPathSyntax.Relative
                ? ControlPlanePathDefaults.ResolveConfiguredPath(hostEnvironment.ContentRootPath, legacyRoot)
                : legacyRoot;
            profile.Storage.WorkspacePath = HostBoundPathPolicy.ImportLegacy(
                migrationCandidate,
                currentHost);
            profile.Storage.LegacyWorkspaceRoot = null;
        }

        document.SchemaVersion = CurrentCatalogSchemaVersion;
        string targetJson = JsonSerializer.Serialize(document, SerializerOptions);
        DatabaseProfileCatalogDocument verified = DeserializeCatalog(targetJson);
        foreach ((Guid profileId, string? encryptedPassword) in protectedPasswords)
        {
            string? verifiedPassword = verified.Profiles
                .Single(profile => profile.Id == profileId)
                .PostgreSql?
                .EncryptedPassword;
            if (!string.Equals(encryptedPassword, verifiedPassword, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Database-profile workspace migration did not preserve protected connection credentials.");
            }
        }

        string stagedPath = Path.Combine(migrationRoot, "catalog.v2.staged.json");
        durableFileWriter.WriteText(
            controlPlanePathResolver.ResolveRootPath(),
            stagedPath,
            targetJson,
            DurableFileWriteOptions.Private);
        durableFileWriter.WriteText(
            controlPlanePathResolver.ResolveRootPath(),
            catalogPath,
            targetJson,
            DurableFileWriteOptions.Private);
        WriteWorkspacePathMigrationCommitMarker(backupJson, targetJson, document.Profiles.Count);
        return verified;
    }

    private void EnsureWorkspacePathMigrationCommitMarker(DatabaseProfileCatalogDocument document)
    {
        string migrationRoot = ResolveWorkspacePathMigrationRoot();
        string backupPath = Path.Combine(migrationRoot, "catalog.v1.backup.json");
        string commitPath = Path.Combine(migrationRoot, "commit.json");
        if (!File.Exists(backupPath) || File.Exists(commitPath))
        {
            return;
        }

        string stagedPath = Path.Combine(migrationRoot, "catalog.v2.staged.json");
        if (!File.Exists(stagedPath))
        {
            throw new InvalidOperationException(
                "The database-profile workspace-path migration is missing its staged catalog.");
        }

        string targetJson = JsonSerializer.Serialize(document, SerializerOptions);
        if (!string.Equals(
                ComputeSha256(File.ReadAllText(stagedPath)),
                ComputeSha256(targetJson),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The database-profile workspace-path migration stage does not match the committed catalog.");
        }

        string backupJson = MigrationBackupIntegrity.ReadVerified(backupPath);
        WriteWorkspacePathMigrationCommitMarker(backupJson, targetJson, document.Profiles.Count);
    }

    private void WriteWorkspacePathMigrationCommitMarker(string sourceJson, string targetJson, int profileCount)
    {
        string migrationRoot = ResolveWorkspacePathMigrationRoot();
        durableFileWriter.EnsureDirectory(
            controlPlanePathResolver.ResolveRootPath(),
            migrationRoot,
            requirePrivateUnixMode: true);
        string manifestJson = JsonSerializer.Serialize(new WorkspacePathMigrationManifest
        {
            SourceSha256 = ComputeSha256(sourceJson),
            TargetSha256 = ComputeSha256(targetJson),
            ProfileCount = profileCount,
            CommittedAtUtc = clock.GetUtcNow()
        }, SerializerOptions);
        durableFileWriter.WriteText(
            controlPlanePathResolver.ResolveRootPath(),
            Path.Combine(migrationRoot, "commit.json"),
            manifestJson,
            DurableFileWriteOptions.Private);
    }

    private static string ComputeSha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private string ResolveWorkspacePathMigrationRoot()
        => Path.Combine(
            controlPlanePathResolver.ResolveDatabaseProfilesRootPath(),
            "migrations",
            WorkspacePathMigrationDirectoryName);

    private static DurableFileWriteOptions CreateNewPrivateWriteOptions()
        => new()
        {
            CommitMode = DurableFileCommitMode.CreateNew,
            RequirePrivateUnixMode = true
        };

    private static DatabaseProfileCatalogDocument DeserializeCatalog(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<DatabaseProfileCatalogDocument>(json, SerializerOptions)
                ?? throw new InvalidOperationException("The database-profile catalog is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("The database-profile catalog migration payload is invalid.", exception);
        }
    }

    private static HostBoundPathState ResolveWorkspacePathState(HostBoundPathRecord? record)
    {
        return HostBoundPathPolicy.TryResolve(
            record,
            HostPathContext.CaptureCurrent(),
            out _,
            out _)
            ? HostBoundPathState.Active
            : HostBoundPathState.NeedsRebind;
    }

    private void WriteActiveProfileStateLocked(DatabaseActiveProfileState state)
    {
        WriteDocument(controlPlanePathResolver.ResolveActiveProfileStateFilePath(), state);
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

    private void WriteDocument<T>(string path, T document)
    {
        var json = JsonSerializer.Serialize(document, SerializerOptions);
        durableFileWriter.WriteText(
            controlPlanePathResolver.ResolveRootPath(),
            path,
            json,
            DurableFileWriteOptions.Private);
    }

    private IDisposable AcquireCoordination(CancellationToken cancellationToken = default)
        => ControlPlaneFileCoordination.Acquire(
            durableFileWriter,
            controlPlanePathResolver.ResolveRootPath(),
            ControlPlaneCoordinationScope.DatabaseProfiles,
            cancellationToken);

    private static void UpsertProfile(DatabaseProfileCatalogDocument document, DatabaseProfileRecord profile)
    {
        var existingIndex = document.Profiles.FindIndex(item => item.Id == profile.Id);
        if (existingIndex >= 0)
        {
            document.Profiles[existingIndex] = profile;
            return;
        }

        document.Profiles.Add(profile);
    }

    private static Guid CreateDeterministicGuid(string value)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class DatabaseProfileCatalogDocument
    {
        public int SchemaVersion { get; set; } = CurrentCatalogSchemaVersion;

        public List<DatabaseProfileRecord> Profiles { get; set; } = [];
    }

    private sealed class WorkspacePathMigrationManifest
    {
        public int FormatVersion { get; set; } = 1;

        public string State { get; set; } = "PointerCommitted";

        public string SourceSha256 { get; set; } = string.Empty;

        public string TargetSha256 { get; set; } = string.Empty;

        public int ProfileCount { get; set; }

        public DateTimeOffset CommittedAtUtc { get; set; }
    }
}
