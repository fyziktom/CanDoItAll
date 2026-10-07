using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Workbench.Planning.UI;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

internal sealed class ProjectTaskDialogSession(Func<bool> isCurrent, Func<Task> readProject, ILogger logger,
    IReadOnlyList<string>? originalTaskIds = null) {
    private readonly Guid operationId = Guid.NewGuid();
    private bool busy;
    public PlanningTaskSaveResult? Result { get; private set; }

    public async Task<PlanningTaskSaveResult> SubmitAsync(Func<Task> save) {
        if (busy || Result is { CanRetry: false }) {
            return Result ?? Rejected("This save is already in progress.");
        }
        if (!isCurrent()) {
            return Rejected("The original project view is no longer current. Reopen the task from its project.", readback: true);
        }
        busy = true;
        Result = null;
        try {
            await save();
            if (Result is null) {
                throw new InvalidOperationException("The native task save returned no outcome.");
            }
        } catch (Exception failure) {
            logger.LogWarning("Task dialog operation {OperationId} stopped with {FailureType}; known status {Status}.",
                operationId, failure.GetType().Name, Result?.Status);
            Result = DescribeFailure(failure);
        } finally {
            busy = false;
        }
        return Result with { OperationId = operationId };
    }

    public void Reject(string message, bool readback = false) => Result = Rejected(message, readback);

    public void TaskCommitted(IReadOnlyList<string> taskIds, ProjectStructureTaskEstimateRefreshResult pricing,
        bool assignmentChanged, bool rowOrderChanged = false) {
        Result = new(PlanningTaskSaveStatus.Committed,
            $"Task saved.{ProjectStructureTaskPricingFeedback.BuildNotificationSuffix(pricing)}", taskIds.ToArray(), [], [
                new(PlanningTaskPhase.Task, PlanningTaskPhaseState.Committed, "Task fields were committed."),
                new(PlanningTaskPhase.Assignment, assignmentChanged ? PlanningTaskPhaseState.Committed : PlanningTaskPhaseState.NotAttempted,
                    assignmentChanged ? "The direct assignment was committed." : "Direct assignments were preserved."),
                new(PlanningTaskPhase.Pricing, PlanningTaskPhaseState.Committed,
                    $"Native pricing: {pricing.Status}. {ProjectStructureTaskPricingFeedback.BuildNotificationSuffix(pricing).Trim()}"),
                new(PlanningTaskPhase.RowOrder, rowOrderChanged ? PlanningTaskPhaseState.Committed : PlanningTaskPhaseState.NotAttempted,
                    rowOrderChanged ? "Gantt row placement was committed." : "No row-order write was requested.")
            ]) { OperationId = operationId };
    }

    public void AttachmentCommitted(ProjectStructureTaskResourceAttachResult attachment) {
        var known = Result ?? throw new InvalidOperationException("Task data must commit before its additive attachment.");
        Result = known with {
            Message = $"Task and resource saved.{ProjectStructureTaskPricingFeedback.BuildNotificationSuffix(attachment.Pricing)}",
            ResourceNodeIds = ResourceIds(attachment.CreatedNodeId, attachment.LinkTargetNodeId),
            Phases = [.. known.Phases,
                new(PlanningTaskPhase.Attachment, PlanningTaskPhaseState.Committed, "The selected definition attachment was committed."),
                new(PlanningTaskPhase.Pricing, PlanningTaskPhaseState.Committed, $"Attachment pricing: {attachment.Pricing.Status}.")]
        };
    }

    public async Task RefreshAfterCommitAsync() {
        if (!isCurrent()) {
            Result = Result! with { RequiresReadback = true,
                Message = $"{Result!.Message} The original project view changed; reopen it to review these identities." };
            return;
        }
        await readProject();
    }

    public async Task<PlanningTaskSaveResult> ReadbackAsync() {
        var known = Result ?? Rejected("No task operation has been submitted.");
        if (busy || !isCurrent()) {
            return known with { Message = $"{known.Message} Read the original project; this editor cannot refresh a replacement view." };
        }
        busy = true;
        try {
            await readProject();
            Result = known with { RequiresReadback = false, AllowsCorrectedSubmission = false,
                Message = $"{known.Message} Current project read completed. Compare the known identities, close this draft, and reopen the task before another write." };
        } catch (Exception failure) {
            logger.LogWarning("Task dialog readback {OperationId} failed with {FailureType}.", operationId, failure.GetType().Name);
            Result = known with { RequiresReadback = true, Message = $"{known.Message} The project read failed; no write was replayed." };
        } finally {
            busy = false;
        }
        return Result;
    }

    private PlanningTaskSaveResult DescribeFailure(Exception failure) {
        if (failure is ProjectStructureNodeCreatedWithFollowUpFailureException created) {
            return new(PlanningTaskSaveStatus.Partial,
                "The task was created, but its canvas follow-up did not complete. Assignment was not attempted. Read the project; do not create it again.",
                [created.CreatedNode.Id], [], [
                    new(PlanningTaskPhase.Task, PlanningTaskPhaseState.Committed, "Task creation returned its exact identity."),
                    new(PlanningTaskPhase.Assignment, PlanningTaskPhaseState.NotAttempted, "Assignment did not start.")
                ], true);
        }
        if ((failure as ProjectStructureTaskCreationException ?? failure.InnerException as ProjectStructureTaskCreationException) is { } creation) {
            var state = creation.CompensationSucceeded ? PlanningTaskPhaseState.Compensated : PlanningTaskPhaseState.Unconfirmed;
            return new(creation.CompensationSucceeded ? PlanningTaskSaveStatus.Rejected : PlanningTaskSaveStatus.Unknown,
                creation.Message, [creation.TaskNodeId], ResourceIds(creation.ResourceNodeId, creation.ResourceLinkTargetNodeId), [
                    new(PlanningTaskPhase.Task, state, creation.CompensationSucceeded ? "The created task was removed." : "Task removal could not be confirmed."),
                    new(creation.Stage == ProjectStructureTaskCreationFailureStage.RowOrdering ? PlanningTaskPhase.RowOrder : PlanningTaskPhase.Attachment,
                        PlanningTaskPhaseState.Unconfirmed, $"The {creation.Stage} phase did not return success."),
                    new(PlanningTaskPhase.Compensation, state, creation.CompensationSucceeded ? "Task compensation completed." : "Task compensation requires review.")
                ], !creation.CompensationSucceeded);
        }
        if (FindCompensation(failure) is { } compensation) {
            return new(compensation.Succeeded && compensation.IsCreate ? PlanningTaskSaveStatus.Rejected : PlanningTaskSaveStatus.Unknown,
                compensation.Succeeded
                    ? "The operation did not complete. Native compensation completed; review the original task before another edit."
                    : "The operation did not complete and compensation could not be confirmed. Read the original project before another write.",
                [compensation.TaskNodeId], [], [
                    new(PlanningTaskPhase.Task, compensation.Succeeded
                        ? compensation.IsCreate ? PlanningTaskPhaseState.Compensated : PlanningTaskPhaseState.Rejected
                        : PlanningTaskPhaseState.Unconfirmed, "Task outcome is retained separately from assignment recovery."),
                    new(PlanningTaskPhase.Assignment, compensation.AssignmentRestored ? PlanningTaskPhaseState.Compensated : PlanningTaskPhaseState.Unconfirmed,
                        compensation.AssignmentRestored ? "The original assignment was restored." : "Assignment recovery is not confirmed separately."),
                    new(PlanningTaskPhase.Pricing, compensation.PricingRestored ? PlanningTaskPhaseState.Compensated : PlanningTaskPhaseState.Unconfirmed,
                        compensation.PricingRestored ? "Original pricing was restored." : "Pricing recovery is not confirmed separately."),
                    new(PlanningTaskPhase.Compensation, compensation.Succeeded ? PlanningTaskPhaseState.Compensated : PlanningTaskPhaseState.Unconfirmed,
                        compensation.Succeeded ? "Native compensation completed." : "Native compensation did not complete.")
                ], !compensation.Succeeded || !compensation.IsCreate);
        }
        if (Result is { } known) {
            if (failure is ProjectStructureAgentException { Details: ProjectStructureTaskAttachmentFailureFacts facts }) {
                return known with {
                    Status = PlanningTaskSaveStatus.Partial, RequiresReadback = true, Message = failure.Message,
                    ResourceNodeIds = ResourceIds(facts.CreatedNodeId, facts.LinkTargetNodeId),
                    Phases = [.. known.Phases,
                        new(PlanningTaskPhase.Attachment, facts.CompensationSucceeded switch {
                            true => PlanningTaskPhaseState.Compensated, false => PlanningTaskPhaseState.Unconfirmed, null => PlanningTaskPhaseState.Committed
                        }, facts.CompensationSucceeded == true ? "The new attachment was removed." : "The attachment identity is retained for review."),
                        new(PlanningTaskPhase.Pricing, facts.CompensationSucceeded.HasValue ? PlanningTaskPhaseState.Rejected : PlanningTaskPhaseState.Unconfirmed,
                            facts.CompensationSucceeded.HasValue ? "Attachment pricing was rejected." : "Attachment pricing may have committed."),
                        new(PlanningTaskPhase.Compensation, facts.CompensationSucceeded switch {
                            true => PlanningTaskPhaseState.Compensated, false => PlanningTaskPhaseState.Unconfirmed, null => PlanningTaskPhaseState.NotAttempted
                        }, facts.CompensationSucceeded is null ? "No attachment compensation was attempted after an uncertain commit." : "Attachment compensation was attempted.")]
                };
            }
            return known with { RequiresReadback = true,
                Status = known.Phases.Any(phase => phase.Phase == PlanningTaskPhase.Attachment) ? known.Status : PlanningTaskSaveStatus.Partial,
                Message = $"{known.Message} Follow-up work did not complete. Known committed identities are retained; read the project before another write." };
        }
        if (failure is ProjectWriteAdmissionRejectedException or ProjectStructureGanttMutationException or
            ProjectStructureTaskDetailsException { Code: ProjectStructureTaskDetailsErrorCode.InvalidRequest or ProjectStructureTaskDetailsErrorCode.ConcurrencyConflict } or
            ProjectStructureTaskApplicationException { Code: ProjectStructureTaskApplicationErrorCode.InvalidRequest or ProjectStructureTaskApplicationErrorCode.ConcurrencyConflict } ||
            failure is ProjectStructureAgentException { EffectState: AgentToolEffectState.None or AgentToolEffectState.NotCommitted }) {
            return Rejected("The native owner rejected this change. The draft is retained. Read the project and reopen the editor to use its current authority.", true);
        }
        return new(PlanningTaskSaveStatus.Unknown,
            "The save could not be confirmed. The draft is retained. Read the original project before another write; do not repeat creation.",
            originalTaskIds?.ToArray() ?? [], [], [new(PlanningTaskPhase.Task, PlanningTaskPhaseState.Unconfirmed, "No successful task receipt was received.")], true);
    }

    private static ProjectStructureTaskCompensationFacts? FindCompensation(Exception failure)
        => failure is ProjectStructureTaskApplicationException { Compensation: { } facts } ? facts :
            failure is ProjectStructureGanttMutationException { Compensation: { } ganttFacts } ? ganttFacts :
            failure.InnerException is { } inner ? FindCompensation(inner) : null;

    private static IReadOnlyList<string> ResourceIds(string? created, string? linked)
        => new[] { created, linked }.OfType<string>().Distinct(StringComparer.Ordinal).ToArray();

    private static PlanningTaskSaveResult Rejected(string message, bool readback = false)
        => new(PlanningTaskSaveStatus.Rejected, message, [], [], [], readback);
}
