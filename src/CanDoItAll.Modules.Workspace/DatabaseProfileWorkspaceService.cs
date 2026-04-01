using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workspace;

public sealed class DatabaseProfileWorkspaceService(
    IDatabaseProfileService profileService,
    IDatabaseProfileRuntimeAccessor profileAccessor,
    IDatabaseSnapshotService snapshotService,
    IDatabaseDriverRegistry driverRegistry,
    IAppDatabaseBootstrapper bootstrapper,
    IDatabaseSwitchCoordinator switchCoordinator,
    ILogger<DatabaseProfileWorkspaceService> logger)
{
    public Task<IReadOnlyList<DatabaseProfileSummary>> ListProfilesAsync(CancellationToken cancellationToken = default)
    {
        return profileService.ListAsync(cancellationToken);
    }

    public Task<DatabaseProfileEditorModel> GetProfileAsync(Guid? id = null, CancellationToken cancellationToken = default)
    {
        return profileService.GetEditorAsync(id, cancellationToken);
    }

    public Task<DatabaseSelectionStateModel> GetCurrentSelectionAsync(CancellationToken cancellationToken = default)
    {
        return profileService.GetCurrentSelectionAsync(cancellationToken);
    }

    public async Task<DatabaseProfileEditorModel> GetCurrentEditorAsync(CancellationToken cancellationToken = default)
    {
        var selection = await profileService.GetCurrentSelectionAsync(cancellationToken);
        if (!selection.IsRuntimeLocked)
        {
            return await profileService.GetEditorAsync(selection.ActiveProfileId, cancellationToken);
        }

        var profile = profileAccessor.ResolveCurrentProfile();
        return CreateEditor(profile.Profile);
    }

    public Result Validate(DatabaseProfileEditorModel model)
    {
        return profileService.Validate(model);
    }

    public Task<Result<Guid>> SaveProfileAsync(DatabaseProfileEditorModel model, CancellationToken cancellationToken = default)
    {
        return profileService.SaveAsync(model, cancellationToken);
    }

    public Task<Result> DeleteProfileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return profileService.DeleteAsync(id, cancellationToken);
    }

    public Task<Result<DatabaseSwitchResult>> ActivateProfileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return switchCoordinator.SwitchAsync(id, cancellationToken);
    }

    public async Task<Result> CreateEmptyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var profile = profileAccessor.ResolveProfile(id);
            if (profile.Profile.Runtime.LockedByRuntimeOverride)
            {
                return Result.Failure(Error.Failure("Runtime override profiles cannot be created or bootstrapped from the UI."));
            }

            var driver = driverRegistry.Resolve(profile.Profile.ProviderKind);
            await driver.CreateEmptyAsync(profile, cancellationToken);
            await bootstrapper.EnsureProfileReadyAsync(profile, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Creating an empty database failed for profile {ProfileId}.", id);
            return Result.Failure(Error.Failure($"Creating an empty database failed: {ex.Message}"));
        }
    }

    public async Task<Result> TestConnectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var profile = profileAccessor.ResolveProfile(id);
            if (profile.Profile.ProviderKind != DatabaseProviderKind.PostgreSql)
            {
                return Result.Failure(Error.Validation("Connection testing is only available for PostgreSQL profiles."));
            }

            if (profile.Profile.Runtime.LockedByRuntimeOverride)
            {
                return Result.Failure(Error.Failure("Runtime override profiles cannot be retested from the UI."));
            }

            await driverRegistry.Resolve(DatabaseProviderKind.PostgreSql)
                .EnsureDatabaseAsync(profile, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Testing the PostgreSQL connection failed for profile {ProfileId}.", id);
            return Result.Failure(Error.Failure($"PostgreSQL connection test failed: {ex.Message}"));
        }
    }

    public async Task<Result<Guid>> CreateManagedSqliteAndActivateAsync(
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        var saveResult = await profileService.SaveAsync(new DatabaseProfileEditorModel
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? "Managed SQLite workspace"
                : displayName.Trim(),
            ProviderKind = DatabaseProviderKind.Sqlite,
            SourceKind = DatabaseProfileSourceKind.ManagedSqlite
        }, cancellationToken);
        if (saveResult.IsFailure)
        {
            return saveResult;
        }

        var createResult = await CreateEmptyAsync(saveResult.Value, cancellationToken);
        if (createResult.IsFailure)
        {
            return Result<Guid>.Failure(createResult.Errors);
        }

        var switchResult = await switchCoordinator.SwitchAsync(saveResult.Value, cancellationToken);
        if (switchResult.IsFailure)
        {
            return Result<Guid>.Failure(switchResult.Errors);
        }

        return Result<Guid>.Success(saveResult.Value);
    }

    public Task<Result<DatabaseSnapshotExportResult>> CreateSnapshotAsync(
        Guid sourceProfileId,
        DatabaseSnapshotTransportKind transportKind,
        CancellationToken cancellationToken = default)
    {
        return snapshotService.CreateSnapshotAsync(sourceProfileId, transportKind, cancellationToken);
    }

    public Task<Result<DatabaseSnapshotMaterializationResult>> CloneAsync(
        Guid sourceProfileId,
        string displayName,
        DatabaseSnapshotTransportKind transportKind = DatabaseSnapshotTransportKind.Local,
        CancellationToken cancellationToken = default)
    {
        return snapshotService.CloneAsync(new DatabaseCloneRequest
        {
            SourceProfileId = sourceProfileId,
            DisplayName = displayName,
            TransportKind = transportKind
        }, cancellationToken);
    }

    public Task<Result<DatabaseSnapshotMaterializationResult>> MaterializeSnapshotAsync(
        DatabaseSnapshotMaterializationRequest request,
        CancellationToken cancellationToken = default)
    {
        return snapshotService.MaterializeSnapshotAsync(request, cancellationToken);
    }

    private static DatabaseProfileEditorModel CreateEditor(DatabaseProfileRecord profile)
    {
        return new DatabaseProfileEditorModel
        {
            Id = profile.Id,
            DisplayName = profile.DisplayName,
            ProviderKind = profile.ProviderKind,
            SourceKind = profile.SourceKind,
            SqliteDatabasePath = profile.Sqlite?.DatabasePath ?? profile.InMemory?.DatabaseName,
            WorkspaceRoot = profile.Storage.WorkspaceRoot,
            PostgresHost = profile.PostgreSql?.Host ?? "localhost",
            PostgresPort = profile.PostgreSql?.Port ?? 5432,
            PostgresDatabaseName = profile.PostgreSql?.DatabaseName ?? "candoitall",
            PostgresUsername = profile.PostgreSql?.Username ?? "postgres",
            PostgresPassword = string.Empty,
            PostgresAdminDatabaseName = profile.PostgreSql?.AdminDatabaseName,
            PostgresTrustServerCertificate = profile.PostgreSql?.TrustServerCertificate ?? false,
            OriginProfileId = profile.Clone.OriginProfileId,
            OriginSnapshotId = profile.Clone.OriginSnapshotId,
            IsRuntimeLocked = profile.Runtime.LockedByRuntimeOverride
        };
    }
}
