using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowHttpProviderDisclosurePolicy(
    IDbContextFactory<WorkflowDbContext> factory,
    PersistentWorkflowExternalResponseOperationStore operations,
    ICanonicalRuntimeDatabase canonical,
    IDatabaseRuntimeWriteFence profileFence,
    WorkflowRuntimeSourceObservation sources,
    SecretReferenceQuery secrets,
    TimeProvider clock) : IWorkflowProviderDisclosurePolicy {
    public WorkflowDisclosureOwnerId Owner => WorkflowHttpSecretUse.DisclosureOwner;

    public bool RequiresEvidence(WorkflowNode node) => node.Settings.ExecutorId == WorkflowExecutorIds.HttpFetch &&
        Settings(node).SecretHeader.SecretId is not null;

    public async ValueTask RequireCurrentAsync(WorkflowRunSnapshot run, WorkflowDefinition definition,
        IReadOnlyList<WorkflowCompletedNodeRead> reads, CancellationToken cancellationToken = default) {
        var profile = canonical.Profile.Profile;
        await profileFence.ExecuteAsync(new DatabaseRuntimeSnapshot(profile.Id, profile.Runtime.Fingerprint, canonical.Generation),
            async token => {
                await RequireUnderProfileFenceAsync(run, definition, reads, token);
                return true;
            }, cancellationToken);
    }

    private async Task RequireUnderProfileFenceAsync(WorkflowRunSnapshot originalRun, WorkflowDefinition definition,
        IReadOnlyList<WorkflowCompletedNodeRead> reads, CancellationToken cancellationToken) {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var saved = await database.Set<WorkflowRunRecordEntity>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.RunId == originalRun.RunId.Value, cancellationToken) ?? throw Denied();
        var run = saved.ToSnapshot();
        var version = await database.Set<WorkflowDefinitionRecord>().AsNoTracking().SingleOrDefaultAsync(row =>
            row.WorkflowId == run.WorkflowId.Value && row.VersionId == run.VersionId.Value, cancellationToken) ?? throw Denied();
        var retainedDefinition = JsonSerializer.Deserialize<WorkflowDefinition>(version.DefinitionJson,
            WorkflowProviderDisclosureContent.JsonOptions) ?? throw Denied();
        if (run.WorkflowId != definition.Id || run.VersionId != definition.VersionId ||
                run.State is not (WorkflowRunState.Running or WorkflowRunState.Idle or WorkflowRunState.WaitingForInput) ||
                WorkflowProviderDisclosureContent.Source(run.Origin) != WorkflowProviderDisclosureContent.Source(originalRun.Origin) ||
                WorkflowProviderDisclosureContent.Definition(retainedDefinition) != WorkflowProviderDisclosureContent.Definition(definition)) {
            throw Denied();
        }
        await using var sourceLease = await AcquireSourceAsync(run, cancellationToken);
        var parsed = reads.Select(read => (Read: read, Evidence: Parse(read, definition))).ToArray();
        var requestIds = parsed.Select(item => item.Evidence.RequestId).Distinct().ToArray();
        var operationIds = parsed.Select(item => item.Evidence.OperationId).Distinct().ToArray();
        var requests = await database.Set<WorkflowExternalRequestRecordEntity>().AsNoTracking()
            .Where(row => requestIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id, cancellationToken);
        var boundaries = await database.Set<WorkflowExternalRequestBoundaryEntity>().AsNoTracking()
            .Where(row => requestIds.Contains(row.RequestId)).ToDictionaryAsync(row => row.RequestId, cancellationToken);
        var responses = await database.Set<WorkflowExternalResponseOperationEntity>().AsNoTracking()
            .Where(row => operationIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id, cancellationToken);
        var existingSecrets = await secrets.GetExistingIdsAsync(parsed.Select(item => item.Evidence.SecretId).Distinct().ToArray(), cancellationToken);
        foreach (var (read, evidence) in parsed) {
            if (evidence.DatabaseProfileId != canonical.Profile.Profile.Id || !existingSecrets.Contains(evidence.SecretId) ||
                    !requests.TryGetValue(evidence.RequestId, out var requestRow) ||
                    !boundaries.TryGetValue(evidence.RequestId, out var boundaryRow) ||
                    !responses.TryGetValue(evidence.OperationId, out var responseRow)) {
                throw Denied();
            }
            var request = PersistentWorkflowExternalRequestBoundaryStore.HydrateRequest(requestRow, boundaryRow);
            var boundary = PersistentWorkflowExternalRequestBoundaryStore.ToRecord(boundaryRow);
            var operation = operations.ToRecord(responseRow);
            var observingInProgress = operation.State == WorkflowExternalResponseOperationState.Resuming &&
                boundary.State == WorkflowExternalRequestState.ResponseClaimed && request.RespondedAtUtc is null &&
                operation.Lease is { } lease && !lease.IsExpired(clock.GetUtcNow());
            var observingConsumed = operation.State is WorkflowExternalResponseOperationState.WaitingAgain or WorkflowExternalResponseOperationState.Completed &&
                boundary.State == WorkflowExternalRequestState.Responded && request.RespondedAtUtc is not null && operation.CompletedAtUtc is not null;
            if ((!observingInProgress && !observingConsumed) || request.Kind is not (WorkflowExternalRequestKind.Approval or WorkflowExternalRequestKind.ToolApproval) ||
                    request.NodeId != read.Proof.NodeId || request.Version.Value != evidence.RequestVersion ||
                    operation.Actor.Kind != WorkflowLaunchActorKind.User || boundary.AuthorizationPolicy is not { } policy ||
                    policy.ExecutorId != WorkflowExecutorIds.HttpFetch ||
                    policy.RequiredCapabilities != HttpFetchWorkflowExecutor.CredentialAwareDescriptor.PermissionPolicy.RequiredCapabilities ||
                    policy.ApprovalRequirement != HttpFetchWorkflowExecutor.CredentialAwareDescriptor.PermissionPolicy.ApprovalRequirement) {
                throw Denied();
            }
            var authorization = WorkflowExternalResponseAuthorizationFactory.Create(operation, run, request, boundary,
                WorkflowExternalResponseAction.Approve, clock.GetUtcNow());
            if (!authorization.Succeeded || authorization.Authorization!.AuthorizedAtUtc != evidence.ApprovedAtUtc ||
                    authorization.Authorization.ExpiresAtUtc != evidence.ExpiresAtUtc) {
                throw Denied();
            }
        }
    }

    private async Task<IAsyncDisposable?> AcquireSourceAsync(WorkflowRunSnapshot run, CancellationToken cancellationToken) {
        try {
            return await sources.AcquireAsync(run, cancellationToken);
        } catch (WorkflowRuntimeSourceRejectedException) {
            throw Denied();
        }
    }

    private WorkflowHttpApprovedReadEvidence Parse(WorkflowCompletedNodeRead read, WorkflowDefinition definition) {
        var node = definition.Graph.Nodes.Single(candidate => candidate.Id == read.Proof.NodeId);
        if (!RequiresEvidence(node) || read.Evidence.Count != 1 || read.Evidence[0].Owner != Owner ||
                read.Evidence[0].SchemaVersion != WorkflowHttpApprovedReadEvidence.SchemaVersion) {
            throw Denied();
        }
        var evidence = JsonSerializer.Deserialize<WorkflowHttpApprovedReadEvidence>(read.Evidence[0].PayloadJson,
            WorkflowProviderDisclosureContent.JsonOptions) ?? throw Denied();
        if (evidence.SecretId != Settings(node).SecretHeader.SecretId || evidence.RequestId == Guid.Empty ||
                evidence.OperationId == Guid.Empty || evidence.RequestVersion <= 0 ||
                evidence.InputHash != read.Proof.InputHash || string.IsNullOrWhiteSpace(evidence.DestinationHash.Value)) {
            throw Denied();
        }
        return evidence;
    }

    private static WorkflowHttpExecutorSettings Settings(WorkflowNode node) => WorkflowExecutorJson.Deserialize<WorkflowHttpExecutorSettings>(
        string.IsNullOrWhiteSpace(node.Settings.ExecutorSettingsJson)
            ? BuiltInWorkflowExecutorDescriptors.HttpFetch.DefaultSettingsJson : node.Settings.ExecutorSettingsJson);

    private static InvalidOperationException Denied()
        => new("The retained HTTP response no longer has its original approved request, live source, credential reference and unexpired disclosure authority.");
}
