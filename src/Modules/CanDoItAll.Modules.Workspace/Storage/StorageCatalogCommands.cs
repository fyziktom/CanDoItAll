using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workspace.StorageCatalog.Contracts;
using CanDoItAll.Security.Abstractions;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workspace;

public sealed class StorageCatalogCommands(StorageCatalogService catalog, IStorageDriverRegistry drivers,
    ISecretRuntimeResolver secrets, IActivityStream activity, IDatabaseRuntimeWriteFence fence,
    ILogger<StorageCatalogCommands> logger) {
    public async Task<CatalogOutcome> ExecuteAsync(CatalogCommand command, DatabaseRuntimeSnapshot expected, CancellationToken cancellationToken) {
        var draft = command.Draft;
        if (!Enum.IsDefined(command.Effect) || !Enum.IsDefined(draft.ProviderKind) || !Enum.IsDefined(draft.ConnectionMode) ||
            draft.DefaultPurposes.IsDefault || draft.DefaultPurposes.Any(purpose => !WorkspaceStorageDefaults.TrackedPurposes.Contains((StorageUsagePurpose)purpose)) ||
            (command.Effect != CatalogEffect.Delete && (string.IsNullOrWhiteSpace(draft.EndpointOrRoot) || draft.Port is <= 0 or > 65535)) ||
            (command.Effect == CatalogEffect.Save && string.IsNullOrWhiteSpace(draft.Name)) ||
            (command.Effect == CatalogEffect.Delete && !draft.Id.HasValue)) {
            return new() { Diagnostic = CatalogDiagnostic.Invalid };
        }
        if (draft.IsSystemDefault) {
            return new() { Diagnostic = CatalogDiagnostic.Protected };
        }
        try {
            return await fence.ExecuteAsync(expected, token => ExecuteAdmittedAsync(command, token), cancellationToken);
        } catch (DatabaseRuntimeProfileChangedException) {
            return new() { Diagnostic = CatalogDiagnostic.Retired };
        }
    }

    private async Task<CatalogOutcome> ExecuteAdmittedAsync(CatalogCommand command, CancellationToken cancellationToken) {
        var outcome = new CatalogOutcome();
        var draft = command.Draft;
        if (command.Effect == CatalogEffect.Delete) {
            try {
                await catalog.DeleteEditorAsync(draft.Id!.Value, cancellationToken);
                outcome = outcome with { CatalogId = draft.Id, Write = CatalogWrite.Committed };
            } catch (StorageCatalogEditorRefusedException refused) {
                return Refused(refused);
            } catch (Exception exception) {
                return Failed(command, outcome with { Write = CatalogWrite.Unknown }, CatalogDiagnostic.PersistenceFailed, exception);
            }
            return await RecordActivityAsync(command, outcome, cancellationToken);
        }

        StorageCatalogSaveRequest request;
        StorageDriverInput input;
        try {
            request = StorageCatalogProjection.Request(draft, command.Effect == CatalogEffect.Test);
            input = StorageDriverInput.FromDraft(request);
        } catch (Exception exception) {
            return Failed(command, outcome, CatalogDiagnostic.Invalid, exception);
        }
        if (command.Effect == CatalogEffect.Test) {
            if (draft.Id is { } id) {
                StorageCatalogSnapshot? target;
                try {
                    target = await catalog.GetAsync(id, cancellationToken);
                } catch (Exception exception) {
                    return Failed(command, outcome, CatalogDiagnostic.ReadUnavailable, exception);
                }
                if (target is null || target.IsSystemDefault) {
                    return new() { Diagnostic = target is null ? CatalogDiagnostic.Missing : CatalogDiagnostic.Protected };
                }
            }
            if (!drivers.TryResolve(input.ProviderKind, out var driver)) {
                return new() { Diagnostic = CatalogDiagnostic.DriverMissing };
            }
            string? credential;
            try {
                credential = input.CredentialSecretId is { } secretId
                    ? await secrets.ResolveValueAsync(new SecretRuntimeRequest(secretId, SecretRuntimePurposes.StorageCredential,
                        [secretId], ConsumerType: SecretRuntimeConsumerTypes.StorageCredential,
                        ConsumerId: SecretRuntimeConsumerIds.StorageCatalog(input.Id)), cancellationToken)
                    : null;
                if (input.CredentialSecretId.HasValue && credential is null) {
                    return new() { Diagnostic = CatalogDiagnostic.CredentialDenied };
                }
            } catch (Exception exception) {
                return Failed(command, outcome, CatalogDiagnostic.CredentialDenied, exception);
            }
            StorageConnectionTestResult tested;
            try {
                tested = await driver.TestConnectionAsync(input, credential, cancellationToken);
            } catch (Exception exception) {
                return Failed(command, outcome with { Driver = CatalogDriver.Unknown }, CatalogDiagnostic.DriverFailed, exception);
            }
            var health = StorageCatalogProjection.Health(tested.HealthStatus, tested.CapabilityMask, tested.TestedAtUtc);
            outcome = outcome with { Driver = CatalogDriver.Completed, Health = health };
            request = request with {
                HealthStatus = tested.HealthStatus, CapabilityMask = tested.CapabilityMask,
                LastTestedAtUtc = tested.TestedAtUtc, LastHealthMessage = health.Message
            };
        }

        if (command.Effect == CatalogEffect.Save || draft.Id.HasValue) {
            try {
                var saved = await catalog.SaveEditorAsync(request, !draft.Id.HasValue, cancellationToken);
                outcome = outcome with { CatalogId = saved.Id, Write = CatalogWrite.Committed };
            } catch (StorageCatalogEditorRefusedException refused) {
                return outcome with { Diagnostic = Refused(refused).Diagnostic };
            } catch (Exception exception) {
                return Failed(command, outcome with { Write = CatalogWrite.Unknown }, CatalogDiagnostic.PersistenceFailed, exception);
            }
        }

        if (command.Effect == CatalogEffect.Save) {
            try {
                await catalog.ApplyDefaultPurposesAsync(outcome.CatalogId!.Value,
                    draft.DefaultPurposes.Select(purpose => (StorageUsagePurpose)purpose).ToArray(), cancellationToken);
                outcome = outcome with { Routing = CatalogRouting.Complete };
            } catch (Exception exception) {
                return Failed(command, outcome with { Routing = CatalogRouting.PossiblyPartial }, CatalogDiagnostic.RoutingFailed, exception);
            }
        }
        return await RecordActivityAsync(command, outcome, cancellationToken);
    }

    private async Task<CatalogOutcome> RecordActivityAsync(CatalogCommand command, CatalogOutcome outcome, CancellationToken cancellationToken) {
        try {
            var verb = command.Effect switch {
                CatalogEffect.Delete => "delete",
                CatalogEffect.Test => "health-check",
                _ => command.Draft.Id.HasValue ? "update" : "create"
            };
            await activity.RecordAsync(new ActivityWriteRequest("storage", verb, "Storage catalog administration",
                $"{command.Effect}: {outcome.Health?.Message ?? "Catalog write acknowledged."}", ArtifactKind: "storage-catalog",
                ArtifactId: outcome.CatalogId ?? command.Draft.Id, Route: "/settings?tab=storage"), cancellationToken);
            return outcome with { Activity = CatalogActivity.Complete };
        } catch (Exception exception) {
            return Failed(command, outcome with { Activity = CatalogActivity.Failed }, CatalogDiagnostic.ActivityFailed, exception);
        }
    }

    private CatalogOutcome Failed(CatalogCommand command, CatalogOutcome outcome, CatalogDiagnostic diagnostic, Exception exception) {
        try {
            logger.LogWarning("Storage operation {OperationId} for profile {ProfileId}, target {StorageId}, effect {Effect} failed at {Stage} ({FailureType}). Catalog acknowledgement: {Write}.",
                command.OperationId, command.Context.ProfileId, command.Draft.Id, command.Effect, diagnostic, exception.GetType().Name, outcome.Write);
        } catch (Exception) {
            return outcome with { Diagnostic = diagnostic, DiagnosticsUnavailable = true };
        }
        return outcome with { Diagnostic = diagnostic };
    }

    private static CatalogOutcome Refused(StorageCatalogEditorRefusedException exception) => new() {
        Diagnostic = exception.Reason == StorageCatalogEditorRefusal.Missing ? CatalogDiagnostic.Missing : CatalogDiagnostic.Protected
    };
}
