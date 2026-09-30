using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.DataSources.Contracts;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workspace;

public sealed class WorkspaceDataSourcesOwner(DatabaseProfileWorkspaceService workspace, ICanonicalRuntimeDatabase canonical,
    ILogger<WorkspaceDataSourcesOwner> logger) : IDataSourcesOwner {
    public DataSourcesContext Context => new(canonical.Profile.Profile.Id, canonical.Generation);

    public Task<RuntimeSelection> ReadRuntimeAsync(CancellationToken cancellationToken) => ReadAsync(async () => {
        var value = await workspace.GetCurrentSelectionAsync(cancellationToken);
        return new RuntimeSelection(value.RuntimeProfileId, value.DisplayName, Provider(value.ProviderKind), value.Descriptor,
            value.WorkspaceRoot, Resolution(value.ResolutionSource), value.IsRuntimeLocked, value.PendingRestartProfileId,
            value.PendingRestartDisplayName, value.PendingRestartDescriptor);
    });

    public Task<IReadOnlyList<ProfileSummary>> ListAsync(CancellationToken cancellationToken) => ReadAsync<IReadOnlyList<ProfileSummary>>(async () =>
        (await workspace.ListProfilesAsync(cancellationToken)).Select(value => new ProfileSummary(value.Id, value.DisplayName,
            Provider(value.ProviderKind), value.Descriptor, value.IsActive, value.IsRuntimeLocked, value.IsPendingRestartActivation,
            value.CreatedUtc, value.LastUsedUtc)).ToArray());

    public Task<ProfileEditor> ReadEditorAsync(Guid id, CancellationToken cancellationToken) => ReadAsync(async () => {
        var current = await workspace.GetCurrentSelectionAsync(cancellationToken);
        var model = current.IsRuntimeLocked && current.RuntimeProfileId == id
            ? await workspace.GetCurrentEditorAsync(cancellationToken)
            : await workspace.GetProfileAsync(id, cancellationToken);
        try {
            if (model.Id != id) {
                throw new DataSourcesException(DataSourceFailure.Missing);
            }
            return new ProfileEditor(new(model.Id, model.DisplayName, model.WorkspaceRoot, model.PostgresHost,
                model.PostgresPort, model.PostgresDatabaseName, model.PostgresUsername, model.PostgresAdminDatabaseName,
                model.PostgresTrustServerCertificate), Provider(model.ProviderKind), model.IsRuntimeLocked,
                !string.IsNullOrEmpty(model.PostgresPassword));
        } finally {
            model.PostgresPassword = string.Empty;
        }
    });

    public Task<SchemaHealth> ReadSchemaAsync(Guid id, CancellationToken cancellationToken) => ReadAsync(async () => {
        var value = await workspace.GetSchemaHealthAsync(id, cancellationToken);
        var status = value.Status switch {
            DatabaseProfileSchemaStatus.Current => SchemaStatus.Current,
            DatabaseProfileSchemaStatus.NeedsMigration => SchemaStatus.NeedsMigration,
            DatabaseProfileSchemaStatus.Unavailable => SchemaStatus.Unavailable,
            _ => SchemaStatus.Unknown
        };
        return new SchemaHealth(id, status, status switch {
            SchemaStatus.Current => "Database schema is current.",
            SchemaStatus.NeedsMigration => "Database migrations or expected tables/columns need attention before activation or transfer.",
            SchemaStatus.Unavailable => "The database could not be inspected. Verify its saved connection and availability.",
            _ => "Database schema has not been established."
        }, value.PendingMigrations.ToArray(), value.SchemaIssues.ToArray(), value.CanApplySchema);
    });

    public Task<IReadOnlyList<TransferSource>> ListSourcesAsync(Guid targetId, CancellationToken cancellationToken)
        => ReadAsync<IReadOnlyList<TransferSource>>(async () => (await workspace.ListTransferSourcesAsync(targetId, cancellationToken))
            .Select(value => new TransferSource(value.ProfileId, value.DisplayName, value.Descriptor)).ToArray());

    public Task<IReadOnlyList<TransferPreview>> PreviewAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken)
        => ReadAsync<IReadOnlyList<TransferPreview>>(async () => (await workspace.PreviewTransferAsync(sourceId, targetId, cancellationToken))
            .Select(value => new TransferPreview(new(new(value.Descriptor.Key), value.Descriptor.Label, value.Descriptor.Description,
                    value.Descriptor.IsSensitive), value.IsAvailable,
                value.IsAvailable ? value.Summary : "This group is empty, unavailable or refused by its owner.",
                value.Warning is null ? null : "The owner reported a transfer constraint. Inspect this group's prerequisites before transferring.",
                value.SourceRecordCount, value.TargetRecordCount)).ToArray());

    public async Task<DataSourceResult> SaveAsync(DataSourcesContext context, ProfileValues values, ProfilePasswordIntent password) {
        var model = new DatabaseProfileEditorModel {
            Id = values.Id, DisplayName = values.DisplayName, WorkspaceRoot = values.WorkspaceRoot,
            ProviderKind = DatabaseProviderKind.PostgreSql, SourceKind = DatabaseProfileSourceKind.PostgresConnection,
            PostgresHost = values.PostgresHost, PostgresPort = values.PostgresPort,
            PostgresDatabaseName = values.PostgresDatabaseName, PostgresUsername = values.PostgresUsername,
            PostgresAdminDatabaseName = values.PostgresAdminDatabaseName,
            PostgresTrustServerCertificate = values.PostgresTrustServerCertificate, PostgresPassword = password.Take()
        };
        try {
            await RequireMutableAsync(context, values.Id);
            if (workspace.Validate(model).IsFailure) {
                throw new DataSourcesException(DataSourceFailure.InvalidRequest);
            }
            return await WriteAsync(values.Id ?? Guid.Empty, DataSourceAction.Save, async () => {
                var result = await workspace.SaveProfileAsync(model);
                return result.IsSuccess ? new(result.Value, DataSourceOutcome.Confirmed, "Data source saved. The database has not been created or activated by Save.")
                    : new(values.Id ?? Guid.Empty, DataSourceOutcome.Refused, "The profile owner refused the save. Review the required fields and current profile.");
            });
        } finally {
            model.PostgresPassword = string.Empty;
        }
    }

    public async Task<DataSourceResult> ExecuteAsync(DataSourceCommand command) {
        var selection = await RequireMutableAsync(command.Context, command.ProfileId);
        if (command.Action == DataSourceAction.Delete && (selection.RuntimeProfileId == command.ProfileId || selection.PendingRestartProfileId == command.ProfileId)) {
            throw new DataSourcesException(DataSourceFailure.Locked);
        }
        if (command.Action == DataSourceAction.ActivateForRestart &&
            (selection.RuntimeProfileId == command.ProfileId || selection.PendingRestartProfileId == command.ProfileId ||
                (await ReadSchemaAsync(command.ProfileId, CancellationToken.None)).Status != SchemaStatus.Current)) {
            throw new DataSourcesException(DataSourceFailure.InvalidRequest);
        }
        RequireContext(command.Context);
        return await WriteAsync(command.ProfileId, command.Action, async () => {
            if (command.Action == DataSourceAction.ActivateForRestart) {
                var activation = await workspace.ActivateProfileAsync(command.ProfileId);
                return activation.IsSuccess ? new(command.ProfileId, DataSourceOutcome.Confirmed,
                    "Activation saved for restart. The current process keeps its original canonical database.", RequiresRestart: activation.Value!.RequiresRestart)
                    : Unknown(command.ProfileId, "Activation did not finish. Bootstrap or saved selection may have progressed; inspect the original target before restarting.");
            }
            var result = command.Action switch {
                DataSourceAction.Delete => await workspace.DeleteProfileAsync(command.ProfileId),
                DataSourceAction.TestConnection => await workspace.TestConnectionAsync(command.ProfileId),
                DataSourceAction.CreateEmpty => await workspace.CreateEmptyAsync(command.ProfileId),
                DataSourceAction.ApplySchema => await workspace.ApplySchemaAsync(command.ProfileId),
                _ => throw new DataSourcesException(DataSourceFailure.InvalidRequest)
            };
            if (result.IsFailure) {
                return command.Action is DataSourceAction.TestConnection or DataSourceAction.Delete
                    ? new(command.ProfileId, DataSourceOutcome.Refused, "The profile owner refused or could not complete this operation. Inspect the original saved profile.")
                    : Unknown(command.ProfileId, "Database creation or schema bootstrap did not finish. Completed stages may remain; refresh schema health before further action.");
            }
            return new(command.ProfileId, DataSourceOutcome.Confirmed, command.Action switch {
                DataSourceAction.Delete => "Saved profile removed. Its physical database and files were not deleted.",
                DataSourceAction.TestConnection => "PostgreSQL connection succeeded.",
                DataSourceAction.CreateEmpty => "Empty database created or retained, and schema bootstrap completed.",
                _ => "Current database schema applied."
            });
        });
    }

    public async Task<DataSourceResult> TransferAsync(TransferRequest request) {
        var groups = request.Groups.ToArray();
        await RequireMutableAsync(request.Context, request.TargetProfileId);
        await RequireMutableAsync(request.Context, request.SourceProfileId);
        if (request.SourceProfileId == request.TargetProfileId || groups.Length == 0 || groups.Distinct().Count() != groups.Length
            || (await ReadSchemaAsync(request.SourceProfileId, CancellationToken.None)).Status != SchemaStatus.Current
            || (await ReadSchemaAsync(request.TargetProfileId, CancellationToken.None)).Status != SchemaStatus.Current) {
            throw new DataSourcesException(DataSourceFailure.InvalidRequest);
        }
        var available = await PreviewAsync(request.SourceProfileId, request.TargetProfileId, CancellationToken.None);
        if (groups.Any(key => !available.Any(item => item.Descriptor.Key == key && item.IsAvailable))) {
            throw new DataSourcesException(DataSourceFailure.InvalidRequest);
        }
        RequireContext(request.Context);
        return await WriteAsync(request.TargetProfileId, DataSourceAction.Transfer, async () => {
            var result = await workspace.TransferSettingsAsync(new() {
                SourceProfileId = request.SourceProfileId, TargetProfileId = request.TargetProfileId,
                ItemKeys = groups.Select(key => key.Value).ToList(), ReplaceExisting = request.ReplaceExisting
            });
            var items = result.Items.Select(item => new TransferGroupResult(new(item.Key), item.Label, item.Success,
                item.Success ? $"Copied {item.RecordsCopied} record(s)." : "This group did not report success. It may be refused or have incomplete progress; no rollback is assumed.",
                item.RecordsCopied)).ToArray();
            return new(request.TargetProfileId, result.IsSuccess ? DataSourceOutcome.Confirmed : DataSourceOutcome.Partial,
                result.IsSuccess ? $"Transferred {result.RecordsCopied} setting record(s)."
                    : "One or more groups did not report success. Confirmed groups are retained; further operations are held for operator review.", items);
        });
    }

    private async Task<RuntimeSelection> RequireMutableAsync(DataSourcesContext context, Guid? id) {
        RequireContext(context);
        var selection = await ReadRuntimeAsync(CancellationToken.None);
        if (selection.IsRuntimeLocked) {
            throw new DataSourcesException(DataSourceFailure.Locked);
        }
        if (id is { } profileId) {
            var editor = await ReadEditorAsync(profileId, CancellationToken.None);
            if (editor.IsRuntimeLocked || editor.ProviderKind != DataSourceProvider.PostgreSql) {
                throw new DataSourcesException(DataSourceFailure.Locked);
            }
        }
        RequireContext(context);
        return selection;
    }

    private void RequireContext(DataSourcesContext context) {
        if (Context != context) {
            throw new DataSourcesException(DataSourceFailure.StaleContext);
        }
    }

    private async Task<T> ReadAsync<T>(Func<Task<T>> read) {
        try {
            return await read();
        } catch (DataSourcesException) {
            throw;
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            logger.LogWarning("Data Sources read failed in runtime {ProfileId}; failure type {FailureType}.", Context.RuntimeProfileId, exception.GetType().Name);
            throw new DataSourcesException(DataSourceFailure.Unavailable);
        }
    }

    private async Task<DataSourceResult> WriteAsync(Guid id, DataSourceAction action, Func<Task<DataSourceResult>> write) {
        try {
            return await write();
        } catch (DataSourcesException) {
            throw;
        } catch (Exception exception) {
            logger.LogError("Data Sources {Action} did not acknowledge profile {ProfileId}; failure type {FailureType}.", action, id, exception.GetType().Name);
            return Unknown(id, "The owner did not acknowledge completion. Progress may have committed; inspect the original profile before another operation.");
        }
    }
    private static DataSourceResult Unknown(Guid id, string message) => new(id, DataSourceOutcome.Unknown, message);
    private static DataSourceResolution Resolution(DatabaseProfileResolutionSource source) => source switch {
        DatabaseProfileResolutionSource.ExplicitOverride => DataSourceResolution.ExplicitOverride,
        DatabaseProfileResolutionSource.PersistedActiveProfile => DataSourceResolution.PersistedActiveProfile,
        DatabaseProfileResolutionSource.PersistedCatalogFallback => DataSourceResolution.PersistedCatalogFallback,
        DatabaseProfileResolutionSource.AutoProvisionedPostgreSql => DataSourceResolution.AutoProvisionedPostgreSql,
        _ => throw new DataSourcesException(DataSourceFailure.Unavailable)
    };
    private static DataSourceProvider Provider(DatabaseProviderKind kind) => kind switch {
        DatabaseProviderKind.PostgreSql => DataSourceProvider.PostgreSql,
        DatabaseProviderKind.InMemory => DataSourceProvider.InMemory,
        _ => throw new DataSourcesException(DataSourceFailure.Unavailable)
    };
}
