using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

/// <summary>
/// Task resource attachment request: a workflow or process definition to attach to a canonical task, the task's
/// execution state as last read and, for a workflow, its run input settings. Persons and agents are assigned with the
/// task update instead.
/// </summary>
/// <param name="Resource">
/// Workflow (with its exact <c>versionId</c>) or process definition to attach; required. Person and Agent are rejected
/// with HTTP 400 <c>TaskAttachedResourceKindInvalid</c>.
/// </param>
/// <param name="CurrentExecution">
/// Execution state of the task from the caller's latest read, taken from <c>workItem.executionState</c>,
/// <c>workItem.actualStartedAtUtc</c> and <c>workItem.actualEndedAtUtc</c> in its <c>metadataJson</c>; required. It
/// is checked against the stored state before the task is repriced.
/// </param>
/// <param name="WorkflowInputSettings">
/// Run input settings for a workflow resource; null uses the defaults. Sending them for a process is rejected with HTTP
/// 400 <c>TaskWorkflowInputSettingsResourceKindInvalid</c>.
/// </param>
public sealed record ProjectStructureTaskResourceAttachRequest(
    [property: JsonRequired] ProjectStructureTaskResourceSelection Resource,
    [property: JsonRequired] ProjectTaskExecutionSnapshot CurrentExecution,
    ProjectStructureWorkflowInputSettings? WorkflowInputSettings = null) {
    /// <summary>
    /// Project write admission returned as <c>expectedProjectAdmission</c> by the structure read, sent back unchanged.
    /// Required: when it is omitted, null or names another project, the request is rejected with HTTP 409
    /// <c>ProjectLifetimeRefreshRequired</c> and nothing is attached.
    /// </summary>
    public ProjectWriteAdmission? ExpectedProjectAdmission { get; init; }
}

/// <summary>Result of a committed task resource attachment, with the repricing it caused.</summary>
/// <param name="Resource">The attached resource.</param>
/// <param name="Pricing">How the task's estimate was repriced for the resource.</param>
/// <param name="CreatedNodeId">
/// Identifier of the node created under the task for the resource (for example a workflow node); null when no node was
/// created.
/// </param>
public sealed record ProjectStructureTaskResourceAttachResult(
    ProjectStructureTaskResourceSelection Resource,
    ProjectStructureTaskEstimateRefreshResult Pricing,
    string? CreatedNodeId = null) {
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LinkTargetNodeId { get; init; }
}

public sealed record ProjectStructureTaskAttachmentFailureFacts(
    string TaskNodeId,
    ProjectStructureTaskResourceKind ResourceKind,
    string? CreatedNodeId,
    string? LinkTargetNodeId,
    bool? CompensationSucceeded,
    string FailureType,
    string? CompensationFailureType = null);


public sealed class ProjectStructureTaskResourceAttachmentService(
    ProjectStructureTaskResourceService resourceService,
    ProjectStructureTaskPricingCommitService pricingCommitService,
    ILogger<ProjectStructureTaskResourceAttachmentService> logger)
{
    public const string CompensationFailedErrorCode =
        "TaskResourceAttachmentCompensationFailed";

    public Task<ProjectStructureTaskResourceAttachResult> AttachAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceAttachRequest request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw InvalidAttachRequest(
                400,
                "TaskResourceAttachRequestRequired",
                "A task resource attachment request is required.");
        }

        var expected = ProjectAssignmentAdmission.Require(projectId, request.ExpectedProjectAdmission);
        if (agent.ExpectedProjectAdmission != expected) {
            throw new InvalidOperationException("Task attachment requires its captured native mutation context.");
        }
        ValidateRequiredRequestValues(request.Resource, request.CurrentExecution);
        return AttachCoreAsync(
            projectId,
            taskNodeId,
            request.Resource,
            request.CurrentExecution,
            request.CurrentExecution,
            request.WorkflowInputSettings,
            agent,
            cancellationToken);
    }

    internal Task<ProjectStructureTaskResourceAttachResult> AttachAfterTransitionAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceSelection resource,
        ProjectTaskExecutionSnapshot previousExecution,
        ProjectTaskExecutionSnapshot expectedCurrentExecution,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredRequestValues(resource, previousExecution);
        if (expectedCurrentExecution is null)
        {
            throw InvalidAttachRequest(
                400,
                "TaskExecutionSnapshotRequired",
                "The current task execution snapshot is required.");
        }

        return AttachCoreAsync(
            projectId,
            taskNodeId,
            resource,
            previousExecution,
            expectedCurrentExecution,
            workflowInputSettings: null,
            agent,
            cancellationToken);
    }

    private async Task<ProjectStructureTaskResourceAttachResult> AttachCoreAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceSelection resource,
        ProjectTaskExecutionSnapshot previousExecution,
        ProjectTaskExecutionSnapshot expectedCurrentExecution,
        ProjectStructureWorkflowInputSettings? workflowInputSettings,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken)
    {
        ValidateRequiredRequestValues(resource, previousExecution);
        if (expectedCurrentExecution is null)
        {
            throw InvalidAttachRequest(
                400,
                "TaskExecutionSnapshotRequired",
                "The current task execution snapshot is required.");
        }

        ArgumentNullException.ThrowIfNull(agent);
        ProjectStructureTaskResourceSelectionPolicy.ValidateDefinitionAttachment(resource);
        var normalizedWorkflowInputSettings =
            ProjectStructureTaskResourceSelectionPolicy.ValidateAndNormalizeWorkflowInputSettings(
            resource,
            workflowInputSettings);

        var pricingPlan = await pricingCommitService.PrepareAfterTransitionAsync(
            projectId,
            taskNodeId,
            resource,
            previousExecution,
            expectedCurrentExecution,
            cancellationToken, agent);
        ProjectStructureTaskResourceAttachment? attachment = null;
        try
        {
            attachment = await resourceService.AttachAsync(
                projectId,
                taskNodeId,
                resource,
                normalizedWorkflowInputSettings,
                agent,
                cancellationToken);
            ValidateAttachmentReceipt(resource, attachment);
            var pricing = await pricingCommitService.CommitAsync(
                pricingPlan,
                cancellationToken);
            return new ProjectStructureTaskResourceAttachResult(
                resource,
                pricing,
                attachment.CreatedNodeId) { LinkTargetNodeId = attachment.LinkTargetNodeId };
        }
        catch (ProjectStructureTaskPricingCommitUnconfirmedException failure) when (attachment is not null) {
            throw AttachmentFailure(taskNodeId, attachment, failure, compensationSucceeded: null);
        }
        catch (Exception failure) when (attachment is not null) {
            try {
                await CompensateAsync(projectId, taskNodeId, attachment, agent, CancellationToken.None);
            } catch (Exception compensationFailure) {
                throw AttachmentFailure(taskNodeId, attachment, failure, compensationSucceeded: false, compensationFailure);
            }
            throw AttachmentFailure(taskNodeId, attachment, failure, compensationSucceeded: true);
        }
    }

    private static ProjectStructureAgentException InvalidAttachRequest(int statusCode, string errorCode, string message)
        => ProjectStructureAgentException.CreateAgentVisible(statusCode, errorCode, message,
            canRetryWithCorrectedInput: true, effectState: AgentToolEffectState.None);

    private static ProjectStructureAgentException AttachmentFailure(string taskNodeId,
        ProjectStructureTaskResourceAttachment attachment, Exception failure, bool? compensationSucceeded,
        Exception? compensationFailure = null) {
        var message = compensationSucceeded switch {
            true => "The resource was attached, but pricing was rejected. The new attachment was removed. Read the task before retrying.",
            false => "The resource was attached, pricing failed, and removal could not be confirmed. Read the task before another change.",
            null => "The resource was attached, but its pricing commit could not be confirmed. The attachment was retained. Read the task before another change."
        };
        return ProjectStructureAgentException.CreateMapped(compensationSucceeded == true ? 409 : 500,
            compensationSucceeded switch {
                true => "TaskResourceAttachmentPricingConflict",
                false => CompensationFailedErrorCode,
                null => "TaskResourceAttachmentPricingUnconfirmed"
            }, message,
            new ProjectStructureTaskAttachmentFailureFacts(taskNodeId, attachment.Kind, attachment.CreatedNodeId,
                attachment.LinkTargetNodeId, compensationSucceeded, failure.GetType().Name, compensationFailure?.GetType().Name),
            isSafeToExpose: true, canRetryWithCorrectedInput: false,
            compensationFailure is null ? failure : new AggregateException(failure, compensationFailure),
            effectState: compensationSucceeded == true ? AgentToolEffectState.NotCommitted : AgentToolEffectState.Unknown);
    }

    private static void ValidateRequiredRequestValues(
        ProjectStructureTaskResourceSelection? resource,
        ProjectTaskExecutionSnapshot? execution)
    {
        if (resource is null)
        {
            throw InvalidAttachRequest(
                400,
                "TaskResourceRequired",
                "A task resource is required.");
        }

        if (execution is null)
        {
            throw InvalidAttachRequest(
                400,
                "TaskExecutionSnapshotRequired",
                "The current task execution snapshot is required.");
        }
    }

    internal static void ValidateAttachmentReceipt(
        ProjectStructureTaskResourceSelection resource,
        ProjectStructureTaskResourceAttachment attachment)
    {
        if (attachment.Kind != resource.Kind)
        {
            throw new InvalidOperationException(
                $"The task resource attachment receipt kind '{attachment.Kind}' does not match requested kind '{resource.Kind}'.");
        }

        switch (resource.Kind)
        {
            case ProjectStructureTaskResourceKind.Workflow when
                string.IsNullOrWhiteSpace(attachment.CreatedNodeId):
                throw new InvalidOperationException(
                    "The workflow attachment did not return its created project-structure node id.");
            case ProjectStructureTaskResourceKind.Process when
                string.IsNullOrWhiteSpace(attachment.LinkTargetNodeId):
                throw new InvalidOperationException(
                    "The process attachment did not return its linked process-definition node id.");
        }
    }

    private async Task CompensateAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceAttachment attachment,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken)
    {
        try
        {
            await resourceService.DetachAsync(
                projectId,
                taskNodeId,
                attachment,
                agent,
                cancellationToken);
            logger.LogWarning(
                "Rolled back task resource attachment after pricing failed. ProjectId={ProjectId} TaskId={TaskId} ResourceKind={ResourceKind}",
                Mask(projectId),
                Mask(taskNodeId),
                attachment.Kind);
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Task resource attachment rollback failed with {FailureType}. ProjectId={ProjectId} TaskId={TaskId} ResourceKind={ResourceKind}",
                exception.GetType().Name, Mask(projectId),
                Mask(taskNodeId),
                attachment.Kind);
            throw;
        }
    }

    private static string Mask(Guid value)
    {
        var formatted = value.ToString("N");
        return $"{formatted[..6]}...{formatted[^4..]}";
    }

    private static string Mask(string value)
        => value.Length <= 12 ? value : $"{value[..6]}...{value[^4..]}";
}
