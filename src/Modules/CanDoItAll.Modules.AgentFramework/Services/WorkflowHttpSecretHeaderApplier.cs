using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Security.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowHttpSecretHeaderApplier(
    IDbContextFactory<WorkflowDbContext> factory,
    PersistentWorkflowExternalResponseOperationStore operations,
    ICanonicalRuntimeDatabase canonical,
    IDatabaseRuntimeWriteFence profileFence,
    CoordinatedDatabaseTransaction transactions,
    ISecretRuntimeResolver secrets,
    TimeProvider clock,
    IWorkflowStructureSourceAuthorityPolicy? sourcePolicy = null,
    ProjectStructureWorkflowAuthorityService? legacySourcePolicy = null,
    IWorkflowMappedProcessSourceAuthority? mappedProcessSource = null,
    IWorkflowProcessToolSourceAuthority? processToolSource = null) : IWorkflowHttpSecretHeaderApplier {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WorkflowHttpSecretUse> ApplyAsync(WorkflowExecutorExecutionContext context, WorkflowNodeInput input,
        HttpRequestMessage request, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(request);
        var admission = context.ApprovalAdmission
            ?? throw Denied("HTTP credential use requires the restored, validated Workflow approval admission.");
        admission.RequireMatches(context, input);
        if (WorkflowExecutorExecutionAuditScope.CurrentRunId != context.RunId ||
                admission.ResponseLease is null || context.Descriptor.Id != WorkflowExecutorIds.HttpFetch) {
            throw Denied("HTTP credential use requires the active Workflow run and response claim.");
        }

        var profile = canonical.Profile.Profile;
        return await profileFence.ExecuteAsync(new DatabaseRuntimeSnapshot(profile.Id, profile.Runtime.Fingerprint, canonical.Generation),
            token => ApplyUnderProfileFenceAsync(context, input, request, admission, token), cancellationToken);
    }

    private async Task<WorkflowHttpSecretUse> ApplyUnderProfileFenceAsync(WorkflowExecutorExecutionContext context, WorkflowNodeInput input,
        HttpRequestMessage request, WorkflowExecutorApprovalAdmission admission, CancellationToken cancellationToken) {
        var approval = admission.Authorization;
        var response = approval.ExternalResponseAuthorization;
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var observedRun = await database.Set<WorkflowRunRecordEntity>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.RunId == approval.RunId.Value, cancellationToken)
            ?? throw Denied("The approved Workflow run no longer exists in this profile.");
        var origin = observedRun.ToSnapshot().Origin;
        if (!string.Equals(WorkflowRunRecordEntity.SerializeOrigin(origin),
                WorkflowRunRecordEntity.SerializeOrigin(WorkflowExecutorExecutionAuditScope.CurrentOrigin), StringComparison.Ordinal)) {
            throw Denied("The saved Workflow source differs from the validated response continuation.");
        }
        var source = origin?.StructureAuthority;
        await using var sourceLease = origin switch {
            WorkflowLaunchOrigin.ProcessDispatchAssignment or WorkflowLaunchOrigin.ProcessAssignment => await (mappedProcessSource
                ?? throw Denied("The original mapped Process source policy is unavailable."))
                .AcquireForContinuationAsync(observedRun.ToSnapshot(), cancellationToken),
            WorkflowLaunchOrigin.ProcessToolInvocation tool => await (processToolSource
                ?? throw Denied("The original Process tool source policy is unavailable."))
                .AcquireForMutationAsync(tool, cancellationToken),
            _ when source?.ProjectScope is not null => await (sourcePolicy
                ?? throw Denied("The Workflow source owner policy is unavailable."))
                .AcquireAsync(source, WorkflowStructureAuthorityUse.Admission, cancellationToken: cancellationToken),
            _ => null
        };
        if (sourceLease is null && (source?.ProjectScope is not null ||
                origin is WorkflowLaunchOrigin.ProcessDispatchAssignment or WorkflowLaunchOrigin.ProcessAssignment or WorkflowLaunchOrigin.ProcessToolInvocation)) {
            throw Denied("The original Workflow source owner did not supply its required lease.");
        }
        if (source is not null && sourceLease is null) {
            await RequireLegacySourceAsync(source, cancellationToken);
        }

        using var memoryGate = await WorkflowPersistenceProvider.EnterInMemoryMutationAsync(database, cancellationToken);
        await using var transaction = WorkflowPersistenceProvider.IsInMemory(database)
            ? null : await database.Database.BeginTransactionAsync(cancellationToken);
        using var coordination = transactions.Enter(database);
        if (sourceLease is not null) {
            await sourceLease.RequireForMutationAsync(cancellationToken);
        }
        var requestEntity = await LockRequestAsync(database, response.RequestId, cancellationToken)
            ?? throw Denied("The approved Workflow request no longer exists.");
        var operationEntity = await PersistentWorkflowExternalResponseOperationStore.LockOperationAsync(
            database, response.OperationId, cancellationToken)
            ?? throw Denied("The approved Workflow response operation no longer exists.");
        var runEntity = await LockRunAsync(database, approval.RunId, cancellationToken)
            ?? throw Denied("The approved Workflow run no longer exists.");
        if (!string.Equals(runEntity.OriginJson, observedRun.OriginJson, StringComparison.Ordinal)) {
            throw Denied("The Workflow source changed before credential use.");
        }
        var boundaryEntity = await database.Set<WorkflowExternalRequestBoundaryEntity>()
            .SingleOrDefaultAsync(row => row.RequestId == response.RequestId.Value, cancellationToken)
            ?? throw Denied("The approved Workflow response boundary is unavailable.");
        var definitionEntity = await LockDefinitionAsync(database, approval.WorkflowId, approval.WorkflowVersionId, cancellationToken)
            ?? throw Denied("The exact approved Workflow version is unavailable.");
        var definition = JsonSerializer.Deserialize<WorkflowDefinition>(definitionEntity.DefinitionJson, JsonOptions)
            ?? throw Denied("The exact approved Workflow version cannot be read.");
        var node = definition.Graph.Nodes.SingleOrDefault(candidate => candidate.Id == approval.NodeId)
            ?? throw Denied("The approved HTTP node is absent from the exact Workflow version.");
        var settingsJson = string.IsNullOrWhiteSpace(node.Settings.ExecutorSettingsJson)
            ? BuiltInWorkflowExecutorDescriptors.HttpFetch.DefaultSettingsJson : node.Settings.ExecutorSettingsJson;
        if (definition.Id != context.Definition.Id || definition.VersionId != context.Definition.VersionId ||
                node.Settings.ExecutorId != WorkflowExecutorIds.HttpFetch ||
                !string.Equals(settingsJson, context.SettingsJson, StringComparison.Ordinal) ||
                (node.Settings.ExecutionPolicy ?? BuiltInWorkflowExecutorDescriptors.HttpFetch.DefaultPolicy) != context.Policy) {
            throw Denied("The HTTP invocation differs from its immutable approved Workflow node.");
        }
        var settings = WorkflowExecutorJson.Deserialize<WorkflowHttpExecutorSettings>(settingsJson);
        var binding = settings.SecretHeader;
        if (binding.SecretId is not { } secretId ||
                !string.Equals(request.RequestUri?.AbsoluteUri, WorkflowHttpRequestUriResolver.Resolve(settings, input).AbsoluteUri,
                    StringComparison.Ordinal) ||
                request.Method != HttpFetchWorkflowExecutor.ToHttpMethod(settings.Method)) {
            throw Denied("The HTTP credential binding or destination differs from the approved Workflow invocation.");
        }
        var run = runEntity.ToSnapshot();
        var mappedContinuation = sourceLease as IWorkflowMappedProcessContinuationLease;
        if (mappedContinuation is not null) {
            await RequireRetainedMappedChildAsync(database, run, mappedContinuation, cancellationToken);
        }
        var boundary = PersistentWorkflowExternalRequestBoundaryStore.ToRecord(boundaryEntity);
        var externalRequest = PersistentWorkflowExternalRequestBoundaryStore.HydrateRequest(requestEntity, boundaryEntity);
        var operation = operations.ToRecord(operationEntity);
        RequireCurrentApproval(context, admission, run, externalRequest, boundary, operation, mappedContinuation);
        if (sourceLease is not null) {
            await sourceLease.RequireForMutationAsync(cancellationToken);
        }

        var evidence = WorkflowHttpApprovedReadEvidence.Capture(context, input, run, admission, canonical.Profile.Profile.Id,
            secretId, request.RequestUri!);
        var headerName = NormalizeHeaderName(binding.HeaderName);
        var secret = await secrets.ResolveValueAsync(new SecretRuntimeRequest(secretId,
            string.IsNullOrWhiteSpace(binding.Purpose) ? WorkflowSecretPurposes.HttpHeader : binding.Purpose.Trim(),
            [secretId], SecretRuntimeConsumerTypes.WorkflowHttpExecutor,
            SecretRuntimeConsumerIds.WorkflowNode(definition.Id.Value, node.Id.Value)), cancellationToken);
        if (string.IsNullOrWhiteSpace(secret)) {
            throw Denied($"HTTP executor secret '{secretId:D}' was not found or did not contain a usable value.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        RequireCurrentApproval(context, admission, run, externalRequest, boundary, operation, mappedContinuation);
        if (sourceLease is not null) {
            await sourceLease.RequireForMutationAsync(cancellationToken);
        } else if (source is not null) {
            await RequireLegacySourceAsync(source, cancellationToken);
        }
        var headerValue = FormatSecretHeaderValue(binding, secret);
        request.Headers.Remove(headerName);
        if (!request.Headers.TryAddWithoutValidation(headerName, headerValue)) {
            throw Denied($"HTTP executor could not apply secret header '{headerName}'.");
        }
        await WorkflowPersistenceProvider.CommitAsync(transaction, cancellationToken);
        return new WorkflowHttpSecretUse(secret, headerName, headerValue, evidence);
    }

    private void RequireCurrentApproval(WorkflowExecutorExecutionContext context, WorkflowExecutorApprovalAdmission admission,
        WorkflowRunSnapshot run, WorkflowExternalRequestRecord request, WorkflowExternalRequestBoundaryRecord boundary,
        WorkflowExternalResponseOperationRecord operation, IWorkflowMappedProcessContinuationLease? mappedContinuation) {
        var now = clock.GetUtcNow();
        var expected = admission.Authorization.ExternalResponseAuthorization;
        var originalLease = admission.ResponseLease!;
        var sourceProfile = run.Origin switch {
            WorkflowLaunchOrigin.ProcessDispatchAssignment mappedOrigin => mappedOrigin.Dispatch.ProfileId,
            WorkflowLaunchOrigin.ProcessAssignment => mappedContinuation?.DatabaseProfileId,
            WorkflowLaunchOrigin.ProcessToolInvocation toolOrigin => toolOrigin.Invocation.Profile.ProfileId,
            _ => run.Origin?.StructureAuthority?.DatabaseProfileId
        };
        if (run.State != WorkflowRunState.WaitingForInput || request.NodeId != context.Node.Id || request.RespondedAtUtc is not null ||
                request.RunId != run.RunId || boundary.State != WorkflowExternalRequestState.ResponseClaimed ||
                operation.State != WorkflowExternalResponseOperationState.Resuming ||
                operation.Lease is not { } lease || lease.OwnerId != originalLease.OwnerId || lease.Epoch != originalLease.Epoch ||
                lease.AcquiredAtUtc != originalLease.AcquiredAtUtc || lease.IsExpired(now) ||
                operation.Actor.Kind != WorkflowLaunchActorKind.User ||
                run.Origin?.AuthorizationScope is not { } scope ||
                scope.Kind == WorkspaceScopeKind.Organization && scope != WorkspaceScopeDescriptor.Organization(canonical.Profile.Profile.Id.ToString("N")) ||
                scope.Kind != WorkspaceScopeKind.Organization && sourceProfile != canonical.Profile.Profile.Id ||
                sourceProfile is { } profile && profile != canonical.Profile.Profile.Id ||
                run.Origin is WorkflowLaunchOrigin.ProcessDispatchAssignment mapped &&
                    (mapped.Dispatch.WorkflowId != run.WorkflowId || mapped.Dispatch.RequestedVersionId is { } version && version != run.VersionId) ||
                run.Origin is WorkflowLaunchOrigin.ProcessToolInvocation tool && tool.Invocation.PreparedRunId != run.RunId) {
            throw Denied("The approved Workflow response claim, source or current profile no longer permits credential use.");
        }
        var authorized = WorkflowExternalResponseAuthorizationFactory.Create(operation, run, request, boundary,
            WorkflowExternalResponseAction.Approve, now);
        if (!authorized.Succeeded || authorized.Authorization != expected ||
                boundary.AuthorizationPolicy is not { } policy || policy.ExecutorId != context.Descriptor.Id ||
                policy.RequiredCapabilities != context.Descriptor.PermissionPolicy.RequiredCapabilities ||
                policy.ApprovalRequirement != context.Descriptor.PermissionPolicy.ApprovalRequirement) {
            throw Denied("The saved Workflow approval no longer authorizes this exact HTTP invocation.");
        }
    }

    private Task RequireRetainedMappedChildAsync(WorkflowDbContext database, WorkflowRunSnapshot child,
        IWorkflowMappedProcessContinuationLease proof, CancellationToken cancellationToken)
        => PersistentWorkflowProcessAssignmentRunQuery.RequireRetainedAsync(database, child, canonical.Profile.Profile.Id,
            proof.DatabaseProfileId, proof.OriginalSelection, proof.OriginalInputJson, cancellationToken);

    private Task RequireLegacySourceAsync(WorkflowStructureAuthority source, CancellationToken cancellationToken)
        => (legacySourcePolicy ?? throw Denied("The saved Workflow source owner policy is unavailable."))
            .EnsureCurrentForMutationAsync(source, kind: null, cancellationToken);

    private static Task<WorkflowExternalRequestRecordEntity?> LockRequestAsync(WorkflowDbContext database,
        WorkflowExternalRequestId requestId, CancellationToken cancellationToken)
        => WorkflowPersistenceProvider.IsInMemory(database)
            ? database.Set<WorkflowExternalRequestRecordEntity>().SingleOrDefaultAsync(row => row.Id == requestId.Value, cancellationToken)
            : database.Set<WorkflowExternalRequestRecordEntity>().FromSqlInterpolated($"""
                SELECT * FROM "AgentFramework_WorkflowExternalRequests" WHERE "Id" = {requestId.Value} FOR UPDATE
                """).SingleOrDefaultAsync(cancellationToken);

    private static Task<WorkflowRunRecordEntity?> LockRunAsync(WorkflowDbContext database,
        WorkflowRunId runId, CancellationToken cancellationToken)
        => WorkflowPersistenceProvider.IsInMemory(database)
            ? database.Set<WorkflowRunRecordEntity>().SingleOrDefaultAsync(row => row.RunId == runId.Value, cancellationToken)
            : database.Set<WorkflowRunRecordEntity>().FromSqlInterpolated($"""
                SELECT * FROM "AgentFramework_WorkflowRuns" WHERE "RunId" = {runId.Value} FOR UPDATE
                """).SingleOrDefaultAsync(cancellationToken);

    private static Task<WorkflowDefinitionRecord?> LockDefinitionAsync(WorkflowDbContext database,
        WorkflowId workflowId, WorkflowVersionId versionId, CancellationToken cancellationToken)
        => WorkflowPersistenceProvider.IsInMemory(database)
            ? database.Set<WorkflowDefinitionRecord>().SingleOrDefaultAsync(row => row.WorkflowId == workflowId.Value &&
                row.VersionId == versionId.Value, cancellationToken)
            : database.Set<WorkflowDefinitionRecord>().FromSqlInterpolated($"""
                SELECT * FROM "AgentFramework_WorkflowDefinitions"
                WHERE "WorkflowId" = {workflowId.Value} AND "VersionId" = {versionId.Value} FOR SHARE
                """).SingleOrDefaultAsync(cancellationToken);

    private static string NormalizeHeaderName(string headerName) {
        if (string.IsNullOrWhiteSpace(headerName)) {
            throw Denied("HTTP executor secret header name is required.");
        }
        var normalized = headerName.Trim();
        if (normalized.Contains('\r', StringComparison.Ordinal) || normalized.Contains('\n', StringComparison.Ordinal)) {
            throw Denied("HTTP executor secret header name cannot contain line breaks.");
        }
        return normalized;
    }

    private static string FormatSecretHeaderValue(WorkflowHttpSecretHeaderBinding binding, string value) {
        if (value.Contains('\r', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal)) {
            throw Denied("HTTP executor secret header value cannot contain line breaks.");
        }
        return binding.ValueFormat switch {
            WorkflowHttpSecretValueFormat.Raw => value,
            WorkflowHttpSecretValueFormat.Bearer => $"Bearer {value}",
            WorkflowHttpSecretValueFormat.Basic => $"Basic {value}",
            WorkflowHttpSecretValueFormat.CustomPrefix => $"{NormalizeHeaderPrefix(binding.CustomPrefix)} {value}",
            _ => throw Denied($"HTTP secret header value format '{binding.ValueFormat}' is not supported.")
        };
    }

    private static string NormalizeHeaderPrefix(string prefix) {
        if (string.IsNullOrWhiteSpace(prefix)) {
            throw Denied("HTTP executor custom secret header prefix is required.");
        }
        var normalized = prefix.Trim();
        if (normalized.Contains('\r', StringComparison.Ordinal) || normalized.Contains('\n', StringComparison.Ordinal)) {
            throw Denied("HTTP executor custom secret header prefix cannot contain line breaks.");
        }
        return normalized;
    }

    private static InvalidOperationException Denied(string message) => new(message);
}
