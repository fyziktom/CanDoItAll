using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectProcessLaunchDeliveryService(
    IDbContextFactory<WorkbenchDbContext> factory,
    CoordinatedDatabaseTransaction transactions,
    ProjectWriteAdmissionService projectAdmissions,
    ProjectProcessLaunchTargetQuery targets,
    ProjectWorkbenchRelationService relations,
    IProcessPreparedLaunchStore preparations,
    IProcessLaunchLinkReceiptStore receipts,
    IProcessLaunchAuthorityPolicy authorityPolicy) {
    public async Task<ProcessLaunchLinkReceipt> GetStatusAsync(ProcessLaunchAdmissionId admissionId, CancellationToken cancellationToken = default) {
        var saved = await preparations.GetAsync(admissionId, cancellationToken)
            ?? throw new InvalidOperationException("The process launch admission was not found.");
        if (saved.LinkDeliveryState != ProcessLaunchLinkDeliveryState.Delivered) {
            return new(admissionId, saved.LinkDeliveryState, saved.DeliveredLinkId) { ConflictReason = saved.LinkConflictReason };
        }
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var exists = await HasOriginalLinkAsync(context, saved, cancellationToken);
        return new(admissionId, exists ? ProcessLaunchLinkDeliveryState.Delivered : ProcessLaunchLinkDeliveryState.Removed, saved.DeliveredLinkId);
    }

    public async Task<ProcessLaunchLinkReceipt> DeliverAsync(ProcessLaunchAdmissionId admissionId, CancellationToken cancellationToken = default) {
        var preparation = await preparations.GetAsync(admissionId, cancellationToken)
            ?? throw new InvalidOperationException("The process launch admission was not found.");
        var target = preparation.Preparation.LinkTarget
            ?? throw new InvalidOperationException("The process launch did not request a native Structure link.");
        var admittedProject = preparation.Preparation.InitialCommit.Mutation.State.ProjectAdmission
            ?? throw new InvalidOperationException("The process launch has no saved project lifetime for its native link.");
        IProcessLaunchAuthorityLease? acquired = null;
        ProcessLaunchAuthorityRejectedException? sourceDenied = null;
        if (preparation.LinkDeliveryState == ProcessLaunchLinkDeliveryState.Pending) {
            try {
                var source = preparation.Preparation.Authority
                    ?? throw new ProcessLaunchAuthorityRejectedException("The pending process link has no saved source authority.");
                acquired = await authorityPolicy.AcquireAsync(source, source, cancellationToken);
            } catch (ProcessLaunchAuthorityRejectedException exception) {
                sourceDenied = exception;
            }
        }
        await using var authorityLease = acquired;
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        await using var mutation = await SerializableMutationScope.BeginAsync(context, ProjectMutationScopeKeys.ForProject(target.ProjectId), cancellationToken);
        using var coordination = transactions.Enter(context);
        var saved = await receipts.RequireForDeliveryAsync(admissionId, preparation.PreparationFingerprint, cancellationToken);
        if (saved.LinkDeliveryState != ProcessLaunchLinkDeliveryState.Pending) {
            if (saved.LinkDeliveryState == ProcessLaunchLinkDeliveryState.Delivered && !await HasOriginalLinkAsync(context, saved, cancellationToken)) {
                var removed = await receipts.StageRemovedAsync(admissionId, saved.PreparationFingerprint, cancellationToken);
                await mutation.CommitAsync(cancellationToken);
                coordination.Dispose();
                return removed;
            }
            return new(admissionId, saved.LinkDeliveryState, saved.DeliveredLinkId) { ConflictReason = saved.LinkConflictReason };
        }

        try {
            if (sourceDenied is not null) {
                throw sourceDenied;
            }
            if (admittedProject.ProjectId != target.ProjectId || saved.Preparation.Authority is null || authorityLease is null) {
                throw new ProcessLaunchAuthorityRejectedException("The accepted process link has no held matching project authority.");
            }
            await projectAdmissions.RequireForMutationAsync(new(admittedProject.DatabaseProfileId, admittedProject.ProjectId, admittedProject.LifetimeId), cancellationToken);
            await authorityLease.RequireForMutationAsync(target, cancellationToken);
            await targets.RequireForMutationAsync(target, cancellationToken);
        } catch (Exception exception) when (exception is ProjectWriteAdmissionRejectedException or ProcessLaunchAuthorityRejectedException or ProcessLaunchIntentConflictException ||
                exception is ProjectStructureAgentException { StatusCode: 404 }) {
            var reason = exception switch {
                ProjectWriteAdmissionRejectedException => ProcessLaunchLinkConflictReason.ProjectRetired,
                ProcessLaunchAuthorityRejectedException => ProcessLaunchLinkConflictReason.AuthorityDenied,
                _ => ProcessLaunchLinkConflictReason.SourceBindingChanged
            };
            var conflict = await receipts.StageConflictAsync(admissionId, saved.PreparationFingerprint, reason, cancellationToken);
            await mutation.CommitAsync(cancellationToken);
            coordination.Dispose();
            return conflict with { ObservationException = exception };
        }
        var runId = saved.Preparation.InitialCommit.Mutation.State.RunId;
        var targetNodeKey = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(runId.Value);
        var linkId = await relations.StageAcceptedProcessLinkAsync(context, target.ProjectId, target.SourceNodeKey, targetNodeKey, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        var receipt = await receipts.StageDeliveredAsync(admissionId, saved.PreparationFingerprint, linkId, cancellationToken);
        await mutation.CommitAsync(cancellationToken);
        coordination.Dispose();
        return receipt;
    }

    private static Task<bool> HasOriginalLinkAsync(WorkbenchDbContext context, ProcessPreparedLaunchSnapshot saved, CancellationToken cancellationToken) {
        if (saved.DeliveredLinkId is not { } linkId || saved.Preparation.LinkTarget is not { } target) {
            throw new InvalidOperationException("The retained process link receipt has incomplete target evidence.");
        }
        var targetNodeKey = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(saved.Preparation.InitialCommit.Mutation.State.RunId.Value);
        return context.Set<ProjectObjectLinkRecord>().AsNoTracking().AnyAsync(link => link.Id == linkId &&
            link.ProjectId == target.ProjectId && link.SourceNodeKey == target.SourceNodeKey && link.TargetNodeKey == targetNodeKey &&
            link.LinkKind == ProjectObjectLinkKind.Uses && !link.IsSystemManaged, cancellationToken);
    }
}
