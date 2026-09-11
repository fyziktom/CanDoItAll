using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.Persistence;
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
        CancellationToken cancellationToken = default)
        => await ReconcileWorkflowOutputCoreAsync(identity, cancellationToken) is not null;

    private async Task<ProjectStructureRuntimeNodeSummary?> ReconcileWorkflowOutputCoreAsync(WorkflowStructureOutputIdentity identity,
        CancellationToken cancellationToken, WorkflowAssetContinuationGuard? continuation = null) {
        var output = await workflowOutputs.FindAsync(identity, cancellationToken)
            ?? throw new InvalidOperationException("The native delivery has no Workflow-owned output intent.");
        var prepared = await projectWorkbenchService.FindPreparedWorkflowContributionAsync(identity, cancellationToken);
        if (prepared is null) {
            return null;
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
        var target = output.Plan.ProjectLifetime ?? throw new WorkflowStructureLegacyLineageException();
        return await ApplyWorkflowOutputAsync(new(run, target), new(identity.Occurrence, output.Plan.WorkflowVersionId, output.Plan.StepId, identity.Slot),
            output.Plan.Kind, prepared.Request, cancellationToken, preparedRecovery: true, continuation: continuation);
    }

    private async Task<WorkflowEffectAdmission> RequireWorkflowEffectAsync(Guid projectId,
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

        var target = await workflowOutputs.FindProjectLifetimeAsync(run.RunId, projectId, cancellationToken)
            ?? await workflowAuthority.CaptureOutputTargetAsync(authority, projectId, cancellationToken);
        var existing = await projectWorkbenchService.FindWorkflowContributionAsync(new(effect.Occurrence, effect.Slot), cancellationToken);
        await using var source = await workflowAuthority.AcquireAsync(authority,
            existing is null ? WorkflowOutputUse(kind) : WorkflowStructureAuthorityUse.Disclosure, target, cancellationToken);
        return new(run, target);
    }

    private async Task<ProjectStructureRuntimeNodeSummary> ApplyWorkflowOutputAsync(WorkflowEffectAdmission effectAdmission,
        WorkflowStructureEffectContext effect, WorkflowStructureOutputKind kind, ProjectObjectCreateRequest request,
        CancellationToken cancellationToken, bool preparedRecovery = false, WorkflowAssetContinuationGuard? continuation = null) {
        try {
            return await ApplyWorkflowOutputCoreAsync(effectAdmission, effect, kind, request, cancellationToken, preparedRecovery, continuation);
        } catch (WorkflowStructureOutputCancelledException) {
            throw CreateWorkflowRunNotExecutingException();
        }
    }

    private async Task<ProjectStructureRuntimeNodeSummary> ApplyWorkflowOutputCoreAsync(WorkflowEffectAdmission effectAdmission,
        WorkflowStructureEffectContext effect, WorkflowStructureOutputKind kind, ProjectObjectCreateRequest request,
        CancellationToken cancellationToken, bool preparedRecovery = false, WorkflowAssetContinuationGuard? continuation = null) {
        var run = effectAdmission.Run;
        var authority = run.Origin!.StructureAuthority!;
        var projectId = effectAdmission.Target.ProjectId;
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
            WorkflowStructureOutputRole.RequiredResult, string.Empty) {
            ProjectLifetime = effectAdmission.Target,
            SourceAuthorityFingerprint = WorkflowStructureAuthorityFingerprint.Create(authority)
        };
        plan = plan with { Fingerprint = ProjectWorkflowContributionFingerprint.Create(plan, request) };
        if (existingPlan is not null && existingPlan != plan) {
            throw new WorkflowStructureOutputConflictException();
        }
        var existingReceipt = await projectWorkbenchService.FindWorkflowContributionAsync(identity, cancellationToken);
        if (existingReceipt is not null) {
            RetainedEvidenceImport.RequireNative(existingReceipt.ImportedHistory);
            await using var read = await workflowAuthority.AcquireAsync(authority, WorkflowStructureAuthorityUse.Disclosure,
                effectAdmission.Target, cancellationToken);
            if (existingReceipt.Receipt!.Fingerprint != plan.Fingerprint || existingReceipt.Receipt.ProjectLifetime != effectAdmission.Target) {
                throw new WorkflowStructureOutputConflictException();
            }
            if (continuation is not null) {
                await continuation.RequireCurrentReadAsync(cancellationToken);
            }
            return await AcknowledgeWorkflowReceiptAsync(run, plan, existingReceipt, null);
        }
        if (continuation?.ReceiptOnly == true) {
            throw new WorkflowStructureOutputConflictException();
        }
        var prepared = continuation is null ? await workflowOutputs.PrepareAsync(plan, cancellationToken)
            : await workflowOutputs.FindAsync(identity, cancellationToken) ?? throw new WorkflowStructureOutputConflictException();
        if (prepared.Plan != plan) {
            throw new WorkflowStructureOutputConflictException();
        }
        request = request with {
            ExpectedProjectAdmission = ProjectStructureWorkflowAuthorityService.ToProjectAdmission(effectAdmission.Target),
            WorkflowMutationAdmission = new(authority, effectAdmission.Target, WorkflowOutputUse(kind), plan)
        };

        var leaseOwner = (run.Origin as WorkflowLaunchOrigin.ProjectStructureNode)?.StructureAdmission?.LeaseOwner;
        var agent = leaseOwner is null
            ? new ProjectStructureAgentContext(authority.Principal.SubjectId, "Workflow", Environment.MachineName,
                string.Empty, string.Empty, run.RunId.ToString())
            : new ProjectStructureAgentContext(leaseOwner.AgentId, leaseOwner.AgentName, leaseOwner.MachineName,
                leaseOwner.RepositoryRoot, leaseOwner.BranchName, leaseOwner.SessionId);
        ProjectWorkflowContributionResult? applied = null;
        Exception? acknowledgementFailure = null;
        try {
            applied = await leaseService.RunWithProjectMutationLeaseAsync(projectId, null, agent,
                "workflow-structure-output", async ct => {
                    await workflowAuthority.EnsureCurrentTargetAsync(authority, WorkflowOutputUse(kind), effectAdmission.Target, ct);
                    var receipt = await projectWorkbenchService.FindWorkflowContributionAsync(identity, ct);
                    if (receipt is null) {
                        var currentRun = await workflowRuns.GetRunAsync(run.RunId, ct);
                        if (currentRun is null || currentRun.State == WorkflowRunState.Cancelled ||
                            !preparedRecovery && currentRun.State is (WorkflowRunState.Completed or WorkflowRunState.Failed)) {
                            throw CreateWorkflowRunNotExecutingException();
                        }

                        if (preparedRecovery && await projectWorkbenchService.FindPreparedWorkflowContributionAsync(identity, ct) is null) {
                            throw new InvalidOperationException("The admitted native command is unavailable for recovery.");
                        }
                        await EnsureParentAuthorityAllowedAsync(projectId, parent, ct);
                    }

                    return continuation is null
                        ? await projectWorkbenchService.CreateWorkflowContributionAsync(plan, request, ct, prepared.StoragePlacementIntentId)
                        : await projectWorkbenchService.CompletePreparedWorkflowAssetAsync(plan, request,
                            prepared.StoragePlacementIntentId ?? throw new WorkflowStructureOutputConflictException(),
                            continuation.RequireCurrentMutationAsync, ct);
                }, cancellationToken);
        } catch (Exception exception) {
            await using var current = await workflowAuthority.AcquireAsync(authority, WorkflowStructureAuthorityUse.Disclosure,
                effectAdmission.Target, CancellationToken.None);
            var recovered = await projectWorkbenchService.FindWorkflowContributionAsync(identity, CancellationToken.None);
            if (recovered is null) {
                throw;
            }

            RetainedEvidenceImport.RequireNative(recovered.ImportedHistory);
            if (recovered.Receipt!.Fingerprint != plan.Fingerprint || recovered.Receipt.ProjectLifetime != plan.ProjectLifetime) {
                throw new WorkflowStructureOutputConflictException();
            }

            applied = recovered;
            acknowledgementFailure = exception;
            logger.LogWarning(exception, "Workflow Structure output {RunId}/{Occurrence}/{Slot} committed; its acknowledgement or lease cleanup failed.",
                run.RunId, identity.Occurrence.Path, identity.Slot);
        }

        if (continuation is not null) {
            await using var read = await workflowAuthority.AcquireAsync(authority, WorkflowStructureAuthorityUse.Disclosure,
                effectAdmission.Target, cancellationToken);
            await continuation.RequireCurrentReadAsync(cancellationToken);
            return await AcknowledgeWorkflowReceiptAsync(run, plan,
                applied ?? throw new InvalidOperationException("The native Workflow mutation returned no receipt."), acknowledgementFailure);
        }
        return await AcknowledgeWorkflowReceiptAsync(run, plan,
            applied ?? throw new InvalidOperationException("The native Workflow mutation returned no receipt."), acknowledgementFailure);
    }

    private async Task<ProjectStructureRuntimeNodeSummary> AcknowledgeWorkflowReceiptAsync(WorkflowRunSnapshot run,
        WorkflowStructureOutputPlan plan, ProjectWorkflowContributionResult applied, Exception? acknowledgementFailure) {
        var identity = plan.Identity;
        Exception? manifestFailure = null;
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

    private static ProjectStructureAgentException CreateWorkflowRunNotExecutingException()
        => new(409, "WorkflowRunNotExecuting",
            "A stopped workflow cannot admit a new Structure effect. Existing native receipts remain available for observation.");

    private sealed record WorkflowEffectAdmission(WorkflowRunSnapshot Run, WorkflowProjectLifetime Target);

    private static WorkflowStructureAuthorityUse WorkflowOutputUse(WorkflowStructureOutputKind kind) => kind switch {
        WorkflowStructureOutputKind.Task => WorkflowStructureAuthorityUse.TaskOutput,
        WorkflowStructureOutputKind.Asset => WorkflowStructureAuthorityUse.AssetOutput,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

}
