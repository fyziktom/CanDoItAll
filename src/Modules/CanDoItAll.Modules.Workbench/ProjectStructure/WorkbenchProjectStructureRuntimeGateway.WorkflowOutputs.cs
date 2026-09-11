using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public sealed partial class WorkbenchProjectStructureRuntimeGateway {
    public async Task<ProjectStructureRuntimeNodeSummary> CreateWorkflowTaskAsync(Guid projectId,
        ProjectStructureRuntimeNodeCreateRequest request, WorkflowStructureEffectContext effect,
        CancellationToken cancellationToken = default) {
        if (request.ObjectType != ProjectObjectType.WorkItem || request.Media is not null) {
            throw new ArgumentException("The workflow task adapter accepts native work items without media.", nameof(request));
        }

        var run = await RequireWorkflowEffectAsync(projectId, effect, WorkflowStructureOutputKind.Task, cancellationToken);
        var subtype = ProjectStructureRequestedNodeKindParser.NormalizeSubtypeForType(request.ObjectType, request.ObjectSubtype);
        var metadata = runtimeMetadataBoundary.ValidateAndCanonicalizeForAgent(request.ObjectType, subtype, request.Notes, request.MetadataJson);
        ProjectStructureAgentRootAuthorityWriteGuard.EnsureAllowed(metadata,
            workspacePathResolver.ResolveWorkspaceRoot(), externalTargetPathRegistryFactory);
        return await ApplyWorkflowOutputAsync(run, effect, WorkflowStructureOutputKind.Task,
            new ProjectObjectCreateRequest(request.ObjectType, request.Title, request.Subtitle, request.Notes,
                request.ParentNodeKey, request.X, request.Y, request.StartUtc, request.EndUtc, subtype,
                MetadataJson: BuildIdempotentMetadataJson(metadata, NormalizeIdempotencyKey(request.IdempotencyKey), request.IdempotencyBatchKey),
                DurationSeconds: request.DurationSeconds, PlacementIntent: ProjectObjectPlacementIntent.AutomaticAroundParent),
            cancellationToken);
    }

    public async Task<ProjectStructureRuntimeNodeSummary> CreateWorkflowAssetAsync(Guid projectId,
        ProjectStructureRuntimeAssetCreateRequest request, WorkflowStructureEffectContext effect,
        CancellationToken cancellationToken = default) {
        if (request.ObjectType is not (ProjectObjectType.File or ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset)) {
            throw new ArgumentException("The workflow asset adapter accepts file, image, or video assets.", nameof(request));
        }

        var run = await RequireWorkflowEffectAsync(projectId, effect, WorkflowStructureOutputKind.Asset, cancellationToken);
        ProjectStructureManagedAssetCreationPolicy.EnsureExplicitParent(request.ParentNodeKey);
        var media = await ResolveAssetCreateMediaAsync(projectId, request, cancellationToken);
        return await ApplyWorkflowOutputAsync(run, effect, WorkflowStructureOutputKind.Asset,
            new ProjectObjectCreateRequest(request.ObjectType, request.Title, request.Subtitle, request.Notes,
                request.ParentNodeKey, ObjectSubtype: request.ObjectSubtype, Media: media,
                MetadataJson: BuildIdempotentMetadataJson(request.MetadataJson, NormalizeIdempotencyKey(request.IdempotencyKey), request.IdempotencyBatchKey),
                PlacementIntent: ProjectObjectPlacementIntent.AutomaticAroundParent), cancellationToken);
    }

    internal async Task<bool> ReconcileWorkflowOutputAsync(WorkflowStructureOutputIdentity identity,
        CancellationToken cancellationToken = default) {
        var output = await workflowOutputs.FindAsync(identity, cancellationToken)
            ?? throw new InvalidOperationException("The native delivery has no Workflow-owned output intent.");
        var prepared = await projectWorkbenchService.FindPreparedWorkflowContributionAsync(identity, cancellationToken);
        if (prepared is null) {
            return false;
        }
        if (prepared.Plan != output.Plan || prepared.StoragePlacementIntentId != output.StoragePlacementIntentId) {
            throw new WorkflowStructureOutputConflictException();
        }
        var run = await workflowRuns.GetRunAsync(identity.Occurrence.RunId, cancellationToken)
            ?? throw new InvalidOperationException("The prepared native delivery has no retained workflow run.");
        if (run.VersionId != output.Plan.WorkflowVersionId || run.Origin?.StructureAuthority is not { } authority ||
            authority.ProjectId != output.Plan.ProjectId && !authority.AllProjects && !authority.ProjectIds.Contains(output.Plan.ProjectId)) {
            throw new ProjectStructureAgentException(403, "WorkflowOriginRequired", "The prepared delivery has no matching retained run authority.");
        }
        if (run.State == WorkflowRunState.Cancelled) {
            throw new ProjectStructureAgentException(409, "WorkflowCancelled", "A cancelled run cannot finish a pending native mutation without explicit reconciliation.");
        }
        authority = authority with { ProjectId = output.Plan.ProjectId };
        await workflowAuthority.EnsureCurrentAsync(authority, output.Plan.Kind, cancellationToken);
        run = run with { Origin = run.Origin with { StructureAuthority = authority } };
        await ApplyWorkflowOutputAsync(run, new(identity.Occurrence, output.Plan.WorkflowVersionId, output.Plan.StepId, identity.Slot),
            output.Plan.Kind, prepared.Request, cancellationToken, preparedRecovery: true);
        return true;
    }

    private async Task<WorkflowRunSnapshot> RequireWorkflowEffectAsync(Guid projectId,
        WorkflowStructureEffectContext effect, WorkflowStructureOutputKind kind, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(effect);
        if (effect.Occurrence is null) {
            throw new WorkflowStructureLegacyLineageException();
        }

        if (WorkflowExecutorExecutionAuditScope.CurrentRunId != effect.Occurrence.RunId) {
            throw new ProjectStructureAgentException(403, "WorkflowExecutionScopeRequired",
                "Workflow outputs require the matching trusted runtime execution scope.");
        }

        var run = await workflowRuns.GetRunAsync(effect.Occurrence.RunId, cancellationToken)
            ?? throw new InvalidOperationException("The workflow output has no durable admitted run.");
        if (run.VersionId != effect.VersionId || run.Origin?.StructureAuthority is not { } authority ||
            authority.ProjectId != projectId && (!authority.AllProjects && !authority.ProjectIds.Contains(projectId))) {
            throw new ProjectStructureAgentException(403, "WorkflowOriginRequired",
                "This workflow output has no matching saved authority. Reconcile its trusted launch source before continuing.");
        }

        if (run.Origin is WorkflowLaunchOrigin.ProjectStructureNode { StructureAdmission: { } admission } && admission.RunId != run.RunId) {
            throw new InvalidOperationException("The workflow run and Structure admission identity differ.");
        }

        var boundAuthority = authority with { ProjectId = projectId };
        await workflowAuthority.EnsureCurrentAsync(boundAuthority, kind, cancellationToken);
        return run with { Origin = run.Origin with { StructureAuthority = boundAuthority } };
    }

    private async Task<ProjectStructureRuntimeNodeSummary> ApplyWorkflowOutputAsync(WorkflowRunSnapshot run,
        WorkflowStructureEffectContext effect, WorkflowStructureOutputKind kind, ProjectObjectCreateRequest request,
        CancellationToken cancellationToken, bool preparedRecovery = false) {
        var authority = run.Origin!.StructureAuthority!;
        var projectId = authority.ProjectId;
        var parent = request.ParentNodeKey?.Trim();
        if (string.IsNullOrEmpty(parent)) {
            throw new ArgumentException("Workflow output creation requires an explicit prepared parent.", nameof(request));
        }

        var identity = new WorkflowStructureOutputIdentity(effect.Occurrence, effect.Slot);
        var existingPlan = (await workflowOutputs.FindAsync(identity, cancellationToken))?.Plan;
        var targetFingerprint = existingPlan?.TargetBindingFingerprint ??
            await projectWorkbenchService.ReadWorkflowTargetBindingAsync(projectId, parent, cancellationToken);
        var plan = new WorkflowStructureOutputPlan(identity, effect.VersionId, effect.StepId, projectId,
            new WorkflowProjectStructureNodeId(parent), targetFingerprint, kind,
            WorkflowStructureOutputRole.RequiredResult, string.Empty);
        plan = plan with { Fingerprint = ProjectWorkflowContributionFingerprint.Create(plan, request) };
        var prepared = await workflowOutputs.PrepareAsync(plan, cancellationToken);

        var leaseOwner = (run.Origin as WorkflowLaunchOrigin.ProjectStructureNode)?.StructureAdmission?.LeaseOwner;
        var agent = leaseOwner is null
            ? new ProjectStructureAgentContext(authority.Principal.SubjectId, "Workflow", Environment.MachineName,
                string.Empty, string.Empty, run.RunId.ToString())
            : new ProjectStructureAgentContext(leaseOwner.AgentId, leaseOwner.AgentName, leaseOwner.MachineName,
                leaseOwner.RepositoryRoot, leaseOwner.BranchName, leaseOwner.SessionId);
        ProjectWorkflowContributionResult? applied = null;
        Exception? acknowledgementFailure = null;
        Exception? manifestFailure = null;
        try {
            applied = await leaseService.RunWithProjectMutationLeaseAsync(projectId, null, agent,
                "workflow-structure-output", async ct => {
                    await workflowAuthority.EnsureCurrentAsync(authority, kind, ct);
                    var receipt = await projectWorkbenchService.FindWorkflowContributionAsync(identity, ct);
                    if (receipt is null) {
                        var currentRun = await workflowRuns.GetRunAsync(run.RunId, ct);
                        if (currentRun is null || currentRun.State == WorkflowRunState.Cancelled ||
                            !preparedRecovery && currentRun.State is (WorkflowRunState.Completed or WorkflowRunState.Failed)) {
                            throw new ProjectStructureAgentException(409, "WorkflowRunNotExecuting",
                                "A stopped workflow cannot admit a new Structure effect. Existing native receipts remain available for observation.");
                        }

                        if (preparedRecovery && await projectWorkbenchService.FindPreparedWorkflowContributionAsync(identity, ct) is null) {
                            throw new InvalidOperationException("The admitted native command is unavailable for recovery.");
                        }
                        await EnsureParentAuthorityAllowedAsync(projectId, parent, ct);
                    }

                    return await projectWorkbenchService.CreateWorkflowContributionAsync(plan, request, ct, prepared.StoragePlacementIntentId);
                }, cancellationToken);
        } catch (Exception exception) {
            await workflowAuthority.EnsureCurrentAsync(authority, kind, CancellationToken.None);
            var recovered = await projectWorkbenchService.FindWorkflowContributionAsync(identity, CancellationToken.None);
            if (recovered is null) {
                throw;
            }

            if (recovered.Receipt!.Fingerprint != plan.Fingerprint) {
                throw new WorkflowStructureOutputConflictException();
            }

            applied = recovered;
            acknowledgementFailure = exception;
            logger.LogWarning(exception, "Workflow Structure output {RunId}/{Occurrence}/{Slot} committed; its acknowledgement or lease cleanup failed.",
                run.RunId, identity.Occurrence.Path, identity.Slot);
        }

        acknowledgementFailure ??= applied.StorageObservationException;
        var pending = false;
        try {
            await workflowOutputs.CompleteAsync(applied.Receipt!, CancellationToken.None);
        } catch (Exception exception) {
            pending = true;
            manifestFailure = exception;
            logger.LogWarning(exception, "Workflow Structure receipt {RunId}/{Occurrence}/{Slot} is durable; manifest acknowledgement remains pending.",
                run.RunId, identity.Occurrence.Path, identity.Slot);
        }

        return MapNode(applied.Node, applied.Node.Priority, FullNodeReadRequest) with {
            WorkflowOutputReceipt = applied.Receipt,
            WorkflowManifestPending = pending,
            WorkflowOutputTargetDeleted = applied.TargetDeleted,
            WorkflowOutputObservationException = acknowledgementFailure,
            WorkflowManifestObservationException = manifestFailure
        };
    }
}
