using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectStructureTaskResourceAttachRequest(
    [property: JsonRequired] ProjectStructureTaskResourceSelection Resource,
    [property: JsonRequired] ProjectTaskExecutionSnapshot CurrentExecution);

public sealed record ProjectStructureTaskResourceAttachResult(
    ProjectStructureTaskResourceSelection Resource,
    ProjectStructureTaskEstimateRefreshResult Pricing);

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
            throw new ProjectStructureAgentException(
                400,
                "TaskResourceAttachRequestRequired",
                "A task resource attachment request is required.");
        }

        ValidateRequiredRequestValues(request.Resource, request.CurrentExecution);
        return AttachCoreAsync(
            projectId,
            taskNodeId,
            request.Resource,
            request.CurrentExecution,
            request.CurrentExecution,
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
            throw new ProjectStructureAgentException(
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
            agent,
            cancellationToken);
    }

    private async Task<ProjectStructureTaskResourceAttachResult> AttachCoreAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceSelection resource,
        ProjectTaskExecutionSnapshot previousExecution,
        ProjectTaskExecutionSnapshot expectedCurrentExecution,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken)
    {
        ValidateRequiredRequestValues(resource, previousExecution);
        if (expectedCurrentExecution is null)
        {
            throw new ProjectStructureAgentException(
                400,
                "TaskExecutionSnapshotRequired",
                "The current task execution snapshot is required.");
        }

        ArgumentNullException.ThrowIfNull(agent);
        ProjectStructureTaskResourceSelectionPolicy.Validate(resource);
        if (resource.Kind is not (
                ProjectStructureTaskResourceKind.Workflow or
                ProjectStructureTaskResourceKind.Process))
        {
            throw new ProjectStructureAgentException(
                400,
                "TaskAttachedResourceKindInvalid",
                "Only a workflow or process can be attached through the typed task-resource path.");
        }

        var pricingPlan = await pricingCommitService.PrepareAfterTransitionAsync(
            projectId,
            taskNodeId,
            resource,
            previousExecution,
            expectedCurrentExecution,
            cancellationToken);
        ProjectStructureTaskResourceAttachment? attachment = null;
        try
        {
            attachment = await resourceService.AttachAsync(
                projectId,
                taskNodeId,
                resource,
                agent,
                cancellationToken);
            var pricing = await pricingCommitService.CommitAsync(
                pricingPlan,
                cancellationToken);
            return new ProjectStructureTaskResourceAttachResult(resource, pricing);
        }
        catch (OperationCanceledException cancellationFailure)
            when (cancellationToken.IsCancellationRequested)
        {
            if (attachment is not null)
            {
                try
                {
                    await CompensateAsync(
                        projectId,
                        taskNodeId,
                        attachment,
                        agent,
                        CancellationToken.None);
                }
                catch (Exception compensationFailure)
                {
                    throw BuildCompensationException(
                        resource,
                        cancellationFailure,
                        compensationFailure);
                }
            }

            throw;
        }
        catch (Exception failure) when (attachment is not null)
        {
            try
            {
                await CompensateAsync(
                    projectId,
                    taskNodeId,
                    attachment,
                    agent,
                    CancellationToken.None);
            }
            catch (Exception compensationFailure)
            {
                throw BuildCompensationException(
                    resource,
                    failure,
                    compensationFailure);
            }

            throw new ProjectStructureAgentException(
                409,
                "TaskResourceAttachmentPricingConflict",
                "The task changed before authoritative resource pricing could be committed. The new resource attachment was rolled back; reload and try again.",
                new
                {
                    ResourceKind = resource.Kind,
                    FailureType = failure.GetType().Name
                });
        }
    }

    private static ProjectStructureAgentException BuildCompensationException(
        ProjectStructureTaskResourceSelection resource,
        Exception failure,
        Exception compensationFailure)
    {
        return new ProjectStructureAgentException(
            500,
            CompensationFailedErrorCode,
            "The task resource was attached, pricing failed, and the attachment could not be rolled back. Reload and resolve the task resource before editing it again.",
            new
            {
                ResourceKind = resource.Kind,
                FailureType = failure.GetType().Name,
                CompensationFailureType = compensationFailure.GetType().Name
            });
    }

    private static void ValidateRequiredRequestValues(
        ProjectStructureTaskResourceSelection? resource,
        ProjectTaskExecutionSnapshot? execution)
    {
        if (resource is null)
        {
            throw new ProjectStructureAgentException(
                400,
                "TaskResourceRequired",
                "A task resource is required.");
        }

        if (execution is null)
        {
            throw new ProjectStructureAgentException(
                400,
                "TaskExecutionSnapshotRequired",
                "The current task execution snapshot is required.");
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
                exception,
                "Task resource attachment rollback failed. ProjectId={ProjectId} TaskId={TaskId} ResourceKind={ResourceKind}",
                Mask(projectId),
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
