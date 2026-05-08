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
    IClock clock,
    ILogger<DatabaseProfileControlPlaneService> logger) : IDatabaseProfileService, IDatabaseProfileRuntimeAccessor
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private readonly StorageOptions _storageOptions = storageOptions.Value;
    private readonly object _sync = new();
    private string? _lastLoggedSelectionKey;

    public Task<IReadOnlyList<DatabaseProfileSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var currentSelection = ResolveCurrentProfileLocked(logSelection: false);
            var summaries = ReadCatalogLocked()
                .Profiles
                .OrderBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(profile => CreateSummary(
                    profile,
                    !currentSelection.Profile.Runtime.LockedByRuntimeOverride && currentSelection.Profile.Id == profile.Id))
                .ToList();

            return Task.FromResult<IReadOnlyList<DatabaseProfileSummary>>(summaries);
        }
    }

    public Task<DatabaseProfileEditorModel> GetEditorAsync(Guid? id = null, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (!id.HasValue)
            {
                return Task.FromResult(CreateDefaultEditor());
            }

            var profile = ReadCatalogLocked().Profiles.FirstOrDefault(item => item.Id == id.Value);
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
            case DatabaseProviderKind.Sqlite:
                if (model.SourceKind is DatabaseProfileSourceKind.PostgresConnection or DatabaseProfileSourceKind.InMemory)
                {
                    errors.Add(Error.Validation("SQLite profiles must use a SQLite-compatible source kind."));
                }

                if (model.SourceKind is not DatabaseProfileSourceKind.ManagedSqlite and not DatabaseProfileSourceKind.SnapshotCache and not DatabaseProfileSourceKind.IpfsSnapshot &&
                    string.IsNullOrWhiteSpace(model.SqliteDatabasePath))
                {
                    errors.Add(Error.Validation("SQLite profiles require a database path unless they are managed profiles."));
                }
                break;

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
                if (model.SourceKind != DatabaseProfileSourceKind.InMemory)
                {
                    errors.Add(Error.Validation("In-memory profiles must use the InMemory source kind."));
                }
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
            var document = ReadCatalogLocked();
            var profile = document.Profiles.FirstOrDefault(item => item.Id == id);
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
            var document = ReadCatalogLocked();
            var profile = document.Profiles.FirstOrDefault(item => item.Id == id);
            if (profile is null)
            {
                return Task.FromResult(Result.Failure(Error.Validation("Database profile not found.")));
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

    public Task<DatabaseSelectionStateModel> GetCurrentSelectionAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var resolvedProfile = ResolveCurrentProfileLocked(logSelection: false);
            return Task.FromResult(new DatabaseSelectionStateModel
            {
                ActiveProfileId = resolvedProfile.Profile.Id,
                DisplayName = resolvedProfile.Profile.DisplayName,
                ProviderKind = resolvedProfile.Profile.ProviderKind,
                SourceKind = resolvedProfile.Profile.SourceKind,
                ResolutionSource = resolvedProfile.ResolutionSource,
                IsRuntimeLocked = resolvedProfile.Profile.Runtime.LockedByRuntimeOverride,
                Fingerprint = resolvedProfile.Profile.Runtime.Fingerprint,
                WorkspaceRoot = resolvedProfile.Profile.Storage.WorkspaceRoot,
                Descriptor = BuildDescriptor(resolvedProfile.Profile)
            });
        }
    }

    public ResolvedDatabaseProfile ResolveCurrentProfile()
    {
        lock (_sync)
        {
            return ResolveCurrentProfileLocked(logSelection: true);
        }
    }

    public ResolvedDatabaseProfile ResolveProfile(Guid profileId)
    {
        lock (_sync)
        {
            var explicitOverride = TryResolveExplicitOverrideLocked();
            if (explicitOverride is not null && explicitOverride.Profile.Id == profileId)
            {
                return explicitOverride;
            }

            var document = ReadCatalogLocked();
            var profile = document.Profiles.FirstOrDefault(item => item.Id == profileId)
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
        if (document.Profiles.Count == 0)
        {
            var seededProfile = TryCreateLegacyProfileLocked() ?? CreateManagedSqliteProfileLocked();
            document.Profiles.Add(seededProfile);
            WriteCatalogLocked(document);

            var newState = ReadActiveProfileStateLocked();
            newState.ActiveProfileId = seededProfile.Id;
            WriteActiveProfileStateLocked(newState);

            var resolutionSource = seededProfile.SourceKind == DatabaseProfileSourceKind.ManagedSqlite
                ? DatabaseProfileResolutionSource.AutoProvisionedManagedSqlite
                : DatabaseProfileResolutionSource.LegacyDiscovery;

            var resolvedSeed = BuildResolvedProfile(seededProfile, resolutionSource);
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
            activeProfile = document.Profiles.FirstOrDefault(item => item.Id == activeState.ActiveProfileId.Value);
        }

        if (activeProfile is null)
        {
            activeProfile = document.Profiles
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
        if (providerKind == DatabaseProviderKind.Sqlite &&
            TryResolveCatalogBackedSqliteOverrideLocked(configuredConnection) is { } catalogBackedOverride)
        {
            return catalogBackedOverride;
        }

        return providerKind switch
        {
            DatabaseProviderKind.Sqlite => BuildSqliteOverrideProfile(configuredConnection, workspaceRoot, now),
            DatabaseProviderKind.PostgreSql => BuildPostgreSqlOverrideProfile(configuredConnection, workspaceRoot, now),
            DatabaseProviderKind.InMemory => BuildInMemoryOverrideProfile(configuredConnection, workspaceRoot, now),
            _ => throw new InvalidOperationException($"Unsupported database provider '{providerKind}'.")
        };
    }

    private ResolvedDatabaseProfile? TryResolveCatalogBackedSqliteOverrideLocked(string? configuredConnection)
    {
        var databasePath = TryExtractSqliteDatabasePath(configuredConnection);
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            return null;
        }

        var normalizedDatabasePath = Path.GetFullPath(databasePath);
        var document = ReadCatalogLocked();
        var matchedProfile = document.Profiles.FirstOrDefault(item =>
            item.ProviderKind == DatabaseProviderKind.Sqlite &&
            !string.IsNullOrWhiteSpace(item.Sqlite?.DatabasePath) &&
            string.Equals(
                Path.GetFullPath(item.Sqlite!.DatabasePath),
                normalizedDatabasePath,
                StringComparison.OrdinalIgnoreCase));
        if (matchedProfile is null)
        {
            return null;
        }

        return BuildResolvedProfile(
            CloneProfileForRuntimeOverride(matchedProfile),
            DatabaseProfileResolutionSource.ExplicitOverride);
    }

    private ResolvedDatabaseProfile BuildSqliteOverrideProfile(string? configuredConnection, string workspaceRoot, DateTimeOffset now)
    {
        var databasePath = TryExtractSqliteDatabasePath(configuredConnection);
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            databasePath = Path.Combine(workspaceRoot, "candoitall.db");
        }

        databasePath = Path.GetFullPath(databasePath);
        var profile = new DatabaseProfileRecord
        {
            Id = CreateDeterministicGuid($"sqlite-override:{databasePath}"),
            DisplayName = "Configured SQLite override",
            ProviderKind = DatabaseProviderKind.Sqlite,
            SourceKind = DatabaseProfileSourceKind.ExternalSqliteFile,
            Sqlite = new SqliteDatabaseProfileConnection
            {
                DatabasePath = databasePath
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
        return BuildResolvedProfile(profile, DatabaseProfileResolutionSource.ExplicitOverride);
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
        var databaseName = string.IsNullOrWhiteSpace(configuredConnection)
            ? "candoitall"
            : configuredConnection.Trim();

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

    private static DatabaseProfileRecord CloneProfileForRuntimeOverride(DatabaseProfileRecord profile)
    {
        return new DatabaseProfileRecord
        {
            Id = profile.Id,
            DisplayName = profile.DisplayName,
            ProviderKind = profile.ProviderKind,
            SourceKind = profile.SourceKind,
            Sqlite = profile.Sqlite is null
                ? null
                : new SqliteDatabaseProfileConnection
                {
                    DatabasePath = profile.Sqlite.DatabasePath
                },
            PostgreSql = profile.PostgreSql is null
                ? null
                : new PostgreSqlDatabaseProfileConnection
                {
                    Host = profile.PostgreSql.Host,
                    Port = profile.PostgreSql.Port,
                    DatabaseName = profile.PostgreSql.DatabaseName,
                    Username = profile.PostgreSql.Username,
                    EncryptedPassword = profile.PostgreSql.EncryptedPassword,
                    AdminDatabaseName = profile.PostgreSql.AdminDatabaseName,
                    TrustServerCertificate = profile.PostgreSql.TrustServerCertificate
                },
            InMemory = profile.InMemory is null
                ? null
                : new InMemoryDatabaseProfileConnection
                {
                    DatabaseName = profile.InMemory.DatabaseName
                },
            Storage = new DatabaseProfileStorageDescriptor
            {
                Mode = profile.Storage.Mode,
                WorkspaceRoot = profile.Storage.WorkspaceRoot
            },
            Clone = new DatabaseProfileCloneMetadata
            {
                OriginProfileId = profile.Clone.OriginProfileId,
                OriginSnapshotId = profile.Clone.OriginSnapshotId
            },
            Runtime = new DatabaseProfileRuntimeMetadata
            {
                Fingerprint = profile.Runtime.Fingerprint,
                LockedByRuntimeOverride = true
            },
            Audit = new DatabaseProfileAuditMetadata
            {
                CreatedUtc = profile.Audit.CreatedUtc,
                LastUsedUtc = profile.Audit.LastUsedUtc,
                LastSuccessfulOpenUtc = profile.Audit.LastSuccessfulOpenUtc
            }
        };
    }

    private DatabaseProfileRecord? TryCreateLegacyProfileLocked()
    {
        var workspaceRoot = ResolveDefaultWorkspaceRoot();
        var databasePath = Path.Combine(workspaceRoot, "candoitall.db");
        if (!File.Exists(databasePath))
        {
            return null;
        }

        logger.LogInformation(
            "Discovered legacy SQLite workspace database at {DatabasePath}.",
            databasePath);

        var now = clock.GetUtcNow();
        var profile = new DatabaseProfileRecord
        {
            DisplayName = "Legacy SQLite workspace",
            ProviderKind = DatabaseProviderKind.Sqlite,
            SourceKind = DatabaseProfileSourceKind.ExternalSqliteFile,
            Sqlite = new SqliteDatabaseProfileConnection
            {
                DatabasePath = databasePath
            },
            Storage = new DatabaseProfileStorageDescriptor
            {
                Mode = DatabaseProfileStorageMode.ExternalWorkspaceRoot,
                WorkspaceRoot = workspaceRoot
            },
            Audit = new DatabaseProfileAuditMetadata
            {
                CreatedUtc = now,
                LastUsedUtc = now,
                LastSuccessfulOpenUtc = now
            }
        };

        profile.Runtime.Fingerprint = BuildFingerprint(profile);
        return profile;
    }

    private DatabaseProfileRecord CreateManagedSqliteProfileLocked()
    {
        var profileId = Guid.NewGuid();
        var databasePath = controlPlanePathResolver.ResolveManagedSqliteDatabasePath(profileId);
        var workspaceRoot = controlPlanePathResolver.ResolveManagedSqliteWorkspaceRootPath(profileId);
        var now = clock.GetUtcNow();

        logger.LogInformation(
            "Provisioning managed SQLite database profile at {DatabasePath}.",
            databasePath);

        var profile = new DatabaseProfileRecord
        {
            Id = profileId,
            DisplayName = "Managed SQLite workspace",
            ProviderKind = DatabaseProviderKind.Sqlite,
            SourceKind = DatabaseProfileSourceKind.ManagedSqlite,
            Sqlite = new SqliteDatabaseProfileConnection
            {
                DatabasePath = databasePath
            },
            Storage = new DatabaseProfileStorageDescriptor
            {
                Mode = DatabaseProfileStorageMode.ManagedPerProfile,
                WorkspaceRoot = workspaceRoot
            },
            Audit = new DatabaseProfileAuditMetadata
            {
                CreatedUtc = now,
                LastUsedUtc = now,
                LastSuccessfulOpenUtc = now
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
            Clone = new DatabaseProfileCloneMetadata
            {
                OriginProfileId = model.OriginProfileId,
                OriginSnapshotId = model.OriginSnapshotId
            },
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
            case DatabaseProviderKind.Sqlite:
                var sqlitePath = ResolveSqliteDatabasePath(profile, model, existing);
                profile.Sqlite = new SqliteDatabaseProfileConnection
                {
                    DatabasePath = sqlitePath
                };
                profile.Storage = new DatabaseProfileStorageDescriptor
                {
                    Mode = profile.SourceKind == DatabaseProfileSourceKind.ManagedSqlite
                        ? DatabaseProfileStorageMode.ManagedPerProfile
                        : DatabaseProfileStorageMode.ExternalWorkspaceRoot,
                    WorkspaceRoot = ResolveWorkspaceRootForProfile(profile, model, existing, sqlitePath)
                };
                break;

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
                    WorkspaceRoot = ResolveWorkspaceRootForProfile(profile, model, existing, sqliteDatabasePath: null)
                };
                break;

            case DatabaseProviderKind.InMemory:
                profile.InMemory = new InMemoryDatabaseProfileConnection
                {
                    DatabaseName = string.IsNullOrWhiteSpace(model.SqliteDatabasePath)
                        ? existing?.InMemory?.DatabaseName ?? "candoitall"
                        : model.SqliteDatabasePath.Trim()
                };
                profile.Storage = new DatabaseProfileStorageDescriptor
                {
                    Mode = DatabaseProfileStorageMode.Ephemeral,
                    WorkspaceRoot = ResolveWorkspaceRootForProfile(profile, model, existing, sqliteDatabasePath: null)
                };
                break;
        }

        profile.Runtime.Fingerprint = BuildFingerprint(profile);
        return profile;
    }

    private string ResolveSqliteDatabasePath(
        DatabaseProfileRecord profile,
        DatabaseProfileEditorModel model,
        DatabaseProfileRecord? existing)
    {
        if (profile.SourceKind == DatabaseProfileSourceKind.ManagedSqlite)
        {
            if (!string.IsNullOrWhiteSpace(existing?.Sqlite?.DatabasePath))
            {
                return Path.GetFullPath(existing.Sqlite.DatabasePath);
            }

            return controlPlanePathResolver.ResolveManagedSqliteDatabasePath(profile.Id);
        }

        if (profile.SourceKind is DatabaseProfileSourceKind.SnapshotCache or DatabaseProfileSourceKind.IpfsSnapshot)
        {
            if (!string.IsNullOrWhiteSpace(existing?.Sqlite?.DatabasePath))
            {
                return Path.GetFullPath(existing.Sqlite.DatabasePath);
            }

            return controlPlanePathResolver.ResolveSnapshotCacheDatabasePath(profile.Id);
        }

        if (!string.IsNullOrWhiteSpace(model.SqliteDatabasePath))
        {
            return ControlPlanePathDefaults.ResolveConfiguredPath(hostEnvironment.ContentRootPath, model.SqliteDatabasePath);
        }

        if (!string.IsNullOrWhiteSpace(existing?.Sqlite?.DatabasePath))
        {
            return Path.GetFullPath(existing.Sqlite.DatabasePath);
        }

        throw new InvalidOperationException("SQLite profiles require a database path.");
    }

    private string ResolveWorkspaceRootForProfile(
        DatabaseProfileRecord profile,
        DatabaseProfileEditorModel model,
        DatabaseProfileRecord? existing,
        string? sqliteDatabasePath)
    {
        if (profile.SourceKind == DatabaseProfileSourceKind.ManagedSqlite)
        {
            if (!string.IsNullOrWhiteSpace(existing?.Storage.WorkspaceRoot))
            {
                return Path.GetFullPath(existing.Storage.WorkspaceRoot);
            }

            return controlPlanePathResolver.ResolveManagedSqliteWorkspaceRootPath(profile.Id);
        }

        if (profile.SourceKind is DatabaseProfileSourceKind.SnapshotCache or DatabaseProfileSourceKind.IpfsSnapshot)
        {
            if (!string.IsNullOrWhiteSpace(existing?.Storage.WorkspaceRoot))
            {
                return Path.GetFullPath(existing.Storage.WorkspaceRoot);
            }

            return controlPlanePathResolver.ResolveSnapshotCacheWorkspaceRootPath(profile.Id);
        }

        if (!string.IsNullOrWhiteSpace(model.WorkspaceRoot))
        {
            return ControlPlanePathDefaults.ResolveConfiguredPath(hostEnvironment.ContentRootPath, model.WorkspaceRoot);
        }

        if (!string.IsNullOrWhiteSpace(existing?.Storage.WorkspaceRoot))
        {
            return Path.GetFullPath(existing.Storage.WorkspaceRoot);
        }

        if (!string.IsNullOrWhiteSpace(sqliteDatabasePath))
        {
            return Path.GetDirectoryName(sqliteDatabasePath)
                ?? throw new InvalidOperationException($"Unable to resolve a workspace root from '{sqliteDatabasePath}'.");
        }

        return ResolveDefaultWorkspaceRoot();
    }

    private ResolvedDatabaseProfile BuildResolvedProfile(DatabaseProfileRecord profile, DatabaseProfileResolutionSource resolutionSource)
    {
        var connectionString = profile.ProviderKind switch
        {
            DatabaseProviderKind.Sqlite => $"Data Source={profile.Sqlite?.DatabasePath ?? throw new InvalidOperationException("SQLite profile is missing a database path.")}",
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
            profile.Audit.LastUsedUtc);
    }

    private DatabaseProfileEditorModel CreateEditor(DatabaseProfileRecord profile)
    {
        return new DatabaseProfileEditorModel
        {
            Id = profile.Id,
            DisplayName = profile.DisplayName,
            ProviderKind = profile.ProviderKind,
            SourceKind = profile.SourceKind,
            SqliteDatabasePath = profile.Sqlite?.DatabasePath,
            WorkspaceRoot = profile.Storage.WorkspaceRoot,
            PostgresHost = profile.PostgreSql?.Host ?? "localhost",
            PostgresPort = profile.PostgreSql?.Port ?? 5432,
            PostgresDatabaseName = profile.PostgreSql?.DatabaseName ?? "candoitall",
            PostgresUsername = profile.PostgreSql?.Username ?? "postgres",
            PostgresPassword = string.IsNullOrWhiteSpace(profile.PostgreSql?.EncryptedPassword)
                ? string.Empty
                : secretProtector.Unprotect(profile.PostgreSql.EncryptedPassword),
            PostgresAdminDatabaseName = profile.PostgreSql?.AdminDatabaseName,
            PostgresTrustServerCertificate = profile.PostgreSql?.TrustServerCertificate ?? false,
            OriginProfileId = profile.Clone.OriginProfileId,
            OriginSnapshotId = profile.Clone.OriginSnapshotId,
            IsRuntimeLocked = profile.Runtime.LockedByRuntimeOverride
        };
    }

    private static DatabaseProfileEditorModel CreateDefaultEditor()
    {
        return new DatabaseProfileEditorModel
        {
            DisplayName = "Managed SQLite workspace",
            ProviderKind = DatabaseProviderKind.Sqlite,
            SourceKind = DatabaseProfileSourceKind.ManagedSqlite
        };
    }

    private string ResolveOverrideWorkspaceRoot(DatabaseProviderKind providerKind, string? configuredConnection)
    {
        var configuredWorkspaceRoot = configuration["Storage:WorkspaceRoot"];
        if (!string.IsNullOrWhiteSpace(configuredWorkspaceRoot))
        {
            return ControlPlanePathDefaults.ResolveConfiguredPath(hostEnvironment.ContentRootPath, configuredWorkspaceRoot);
        }

        if (providerKind == DatabaseProviderKind.Sqlite)
        {
            var sqlitePath = TryExtractSqliteDatabasePath(configuredConnection);
            if (!string.IsNullOrWhiteSpace(sqlitePath))
            {
                return Path.GetDirectoryName(Path.GetFullPath(sqlitePath))
                    ?? throw new InvalidOperationException($"Unable to resolve a workspace root from '{sqlitePath}'.");
            }
        }

        return providerKind == DatabaseProviderKind.Sqlite
            ? ResolveDefaultWorkspaceRoot()
            : ResolveRuntimeOverrideWorkspaceRoot(providerKind, configuredConnection);
    }

    private string ResolveDefaultWorkspaceRoot()
    {
        return ControlPlanePathDefaults.ResolveConfiguredPath(hostEnvironment.ContentRootPath, _storageOptions.WorkspaceRoot);
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
            DatabaseProviderKind.InMemory => $"inmemory:{(string.IsNullOrWhiteSpace(configuredConnection) ? "candoitall" : configuredConnection.Trim().ToLowerInvariant())}",
            DatabaseProviderKind.Sqlite => $"sqlite:{Path.GetFullPath(TryExtractSqliteDatabasePath(configuredConnection) ?? string.Empty).Replace('\\', '/').ToLowerInvariant()}",
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
            DatabaseProviderKind.Sqlite => "SQLite profile",
            DatabaseProviderKind.PostgreSql => "PostgreSQL profile",
            DatabaseProviderKind.InMemory => "In-memory profile",
            _ => "Database profile"
        };
    }

    private static DatabaseProfileSourceKind NormalizeSourceKind(DatabaseProviderKind providerKind, DatabaseProfileSourceKind sourceKind)
    {
        return providerKind switch
        {
            DatabaseProviderKind.Sqlite => sourceKind,
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

    private static string BuildDescriptor(DatabaseProfileRecord profile)
    {
        return profile.ProviderKind switch
        {
            DatabaseProviderKind.Sqlite => profile.Sqlite?.DatabasePath ?? string.Empty,
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
            DatabaseProviderKind.Sqlite when profile.SourceKind == DatabaseProfileSourceKind.ManagedSqlite
                => $"sqlite:managed:{profile.Id:N}",
            DatabaseProviderKind.Sqlite when profile.SourceKind == DatabaseProfileSourceKind.SnapshotCache
                => $"sqlite:snapshot:{profile.Id:N}",
            DatabaseProviderKind.Sqlite when profile.SourceKind == DatabaseProfileSourceKind.IpfsSnapshot
                => $"sqlite:ipfs:{profile.Id:N}",
            DatabaseProviderKind.Sqlite
                => $"sqlite:file:{Path.GetFullPath(profile.Sqlite?.DatabasePath ?? string.Empty).Replace('\\', '/').ToLowerInvariant()}",
            DatabaseProviderKind.PostgreSql
                => $"postgres:{profile.PostgreSql?.Host.Trim().ToLowerInvariant()}:{profile.PostgreSql?.Port}:{profile.PostgreSql?.DatabaseName.Trim().ToLowerInvariant()}:{profile.PostgreSql?.Username.Trim().ToLowerInvariant()}",
            DatabaseProviderKind.InMemory
                => $"inmemory:{profile.InMemory?.DatabaseName.Trim().ToLowerInvariant()}",
            _ => throw new InvalidOperationException($"Unsupported provider '{profile.ProviderKind}'.")
        };
    }

    private static string? TryExtractSqliteDatabasePath(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = segment[..separatorIndex].Trim();
            if (!key.Equals("Data Source", StringComparison.OrdinalIgnoreCase) &&
                !key.Equals("Filename", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = segment[(separatorIndex + 1)..].Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
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
        return ReadDocument(
            controlPlanePathResolver.ResolveCatalogFilePath(),
            static () => new DatabaseProfileCatalogDocument());
    }

    private DatabaseActiveProfileState ReadActiveProfileStateLocked()
    {
        return ReadDocument(
            controlPlanePathResolver.ResolveActiveProfileStateFilePath(),
            static () => new DatabaseActiveProfileState());
    }

    private void WriteCatalogLocked(DatabaseProfileCatalogDocument document)
    {
        WriteDocument(controlPlanePathResolver.ResolveCatalogFilePath(), document);
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

    private static void WriteDocument<T>(string path, T document)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Unable to resolve a directory for '{path}'.");
        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        var json = JsonSerializer.Serialize(document, SerializerOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, path, true);
    }

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
        public int SchemaVersion { get; set; } = 1;

        public List<DatabaseProfileRecord> Profiles { get; set; } = [];
    }
}
