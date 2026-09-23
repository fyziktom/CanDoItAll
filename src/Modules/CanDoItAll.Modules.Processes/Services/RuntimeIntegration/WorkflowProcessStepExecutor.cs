using System.Runtime.ExceptionServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;

namespace CanDoItAll.Modules.Processes;

internal interface IProcessWorkflowStepExecutor
{
    ValueTask<ProcessExecutionAdapterResult> ExecuteAsync(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepExecutionContract stepContract,
        CancellationToken cancellationToken = default,
        Func<CancellationToken, ValueTask<ProcessExecutionAdapterResult?>>? beforeLaunch = null,
        ProcessDispatchClaimIdentity dispatchClaimIdentity = default);
}

internal sealed class WorkflowProcessStepExecutor(
    IWorkflowLaunchService launchService,
    IWorkflowRuntimeManager runtimeManager,
    ProcessExecutionResultConverter resultConverter,
    IWorkflowMappedProcessSourceAuthority? mappedSource = null,
    IWorkflowProcessAssignmentRunQuery? assignmentRuns = null) : IProcessWorkflowStepExecutor
{
    private static readonly JsonSerializerOptions EventJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async ValueTask<ProcessExecutionAdapterResult> ExecuteAsync(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepExecutionContract stepContract,
        CancellationToken cancellationToken = default,
        Func<CancellationToken, ValueTask<ProcessExecutionAdapterResult?>>? beforeLaunch = null,
        ProcessDispatchClaimIdentity dispatchClaimIdentity = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(stepContract);

        if (!ProcessLaunchExecutorKinds.IsWorkflow(assignment.ExecutorKind))
        {
            return Failed(
                "process.adapter.workflow_executor_kind_invalid",
                $"Step '{assignment.StepKey}' is not bound to the Workflow executor kind.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:{assignment.ExecutorKind}");
        }

        if (assignment.WorkflowBinding is not { } binding)
        {
            return Failed(
                "process.adapter.workflow_binding_missing",
                $"Step '{assignment.StepKey}' does not have a typed workflow binding.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:workflow-binding-missing");
        }

        if (binding.OutputMapping != ProcessWorkflowOutputMappingKind.ProcessStepOutcome)
        {
            return Failed(
                "process.adapter.workflow_output_mapping_unsupported",
                $"Step '{assignment.StepKey}' uses unsupported workflow output mapping '{binding.OutputMapping}'.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:output-mapping:{binding.OutputMapping}");
        }

        if (HasUnsupportedProcessArtifactContract(assignment, stepContract))
        {
            return Failed(
                "process.adapter.workflow_artifact_mapping_unsupported",
                $"Step '{assignment.StepKey}' declares process artifact, subprocess, or tool-receipt behavior that the workflow driver cannot map safely.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:{stepContract.ContractHash}:workflow-artifact-contract");
        }

        if (dispatchClaimIdentity.Value == Guid.Empty || mappedSource is null || assignmentRuns is null) {
            throw new InvalidOperationException("Mapped Workflow dispatch requires its actual Process claim, source authority and exact owner receipt query.");
        }
        var origin = await mappedSource.CaptureAsync(new(assignment.RunId.Value), new(assignment.StepInstanceId.Value),
            dispatchClaimIdentity.Value, stepContract.ContractHash, cancellationToken).ConfigureAwait(false);
        if (origin.Dispatch.WorkflowId.Value != binding.WorkflowId.Value || origin.Dispatch.RequestedVersionId?.Value != binding.WorkflowVersionId?.Value) {
            throw new InvalidOperationException("The actual mapped Workflow dispatch selected a different executor from the supplied assignment.");
        }
        var childRuns = await assignmentRuns.FindAsync(new(assignment.RunId.Value), new(assignment.StepInstanceId.Value), cancellationToken).ConfigureAwait(false);
        if (childRuns.Count > 1) {
            return Failed("process.adapter.workflow_child_ambiguous",
                $"Step '{assignment.StepKey}' has multiple retained Workflow children; refusing to select one or launch another.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:multiple-children");
        }
        if (childRuns.Count == 1) {
            RequireSameChild(childRuns[0], assignment, binding, origin);
            return await MapRunAsync(assignment, stepContract, childRuns[0], cancellationToken).ConfigureAwait(false);
        }

        if (beforeLaunch is not null &&
            await beforeLaunch(cancellationToken).ConfigureAwait(false) is { } blocked)
        {
            return blocked;
        }

        var intent = new WorkflowLaunchIntent(
            CreateSelection(binding),
            WorkflowLaunchMode.Production,
            origin,
            CreateInputJson(assignment, stepContract),
            WorkflowLaunchCompletionPolicy.WaitForStopped,
            new WorkflowLaunchIdempotency.CallerSupplied(CreateIdempotencyKey(assignment)));
        WorkflowLaunchResult launch;
        try {
            launch = await launchService.LaunchAsync(intent, cancellationToken).ConfigureAwait(false);
        } catch (Exception originalFailure) when (!cancellationToken.IsCancellationRequested) {
            IReadOnlyList<WorkflowRunSnapshot> retained;
            try {
                retained = await assignmentRuns.FindAsync(new(assignment.RunId.Value), new(assignment.StepInstanceId.Value), cancellationToken).ConfigureAwait(false);
            } catch {
                ExceptionDispatchInfo.Capture(originalFailure).Throw();
                throw;
            }
            if (retained.Count != 1 || retained[0].Origin is not WorkflowLaunchOrigin.ProcessDispatchAssignment receipt ||
                    !origin.Dispatch.HasSameIntent(receipt.Dispatch)) {
                throw;
            }
            RequireSameChild(retained[0], assignment, binding, origin);
            return await MapRunAsync(assignment, stepContract, retained[0], cancellationToken).ConfigureAwait(false);
        }
        if (!IsVerifiedChild(launch.Run, assignment, binding))
        {
            return Failed(
                "process.adapter.workflow_launch_identity_mismatch",
                $"Workflow launch returned run '{launch.Run.RunId.Value:D}' with workflow, version, or process origin that does not match step '{assignment.StepKey}'.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:{launch.Run.RunId}:{launch.Run.WorkflowId}:{launch.Run.VersionId}:{launch.Run.Origin?.Kind.ToString() ?? "missing"}");
        }

        RequireSameChild(launch.Run, assignment, binding, origin);
        return await MapRunAsync(assignment, stepContract, launch.Run, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ProcessExecutionAdapterResult> MapRunAsync(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepExecutionContract stepContract,
        WorkflowRunSnapshot run,
        CancellationToken cancellationToken)
    {
        return run.State switch
        {
            WorkflowRunState.NotStarted or
            WorkflowRunState.Running or
            WorkflowRunState.WaitingForInput or
            WorkflowRunState.Idle => throw new ProcessRuntimeDispatchDeferredException(
                $"Step '{assignment.StepKey}' is waiting for workflow run '{run.RunId.Value:D}' in state '{run.State}'."),
            WorkflowRunState.Completed => await MapCompletedRunAsync(
                assignment,
                stepContract,
                run,
                cancellationToken).ConfigureAwait(false),
            WorkflowRunState.Failed => Failed(
                "process.adapter.workflow_child_failed",
                $"Workflow run '{run.RunId.Value:D}' failed for step '{assignment.StepKey}'. Inspect restricted workflow evidence for backend details.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:{run.RunId}:failed:{run.Summary}"),
            WorkflowRunState.Cancelled => Canceled(
                "process.adapter.workflow_child_cancelled",
                $"Workflow run '{run.RunId.Value:D}' was cancelled for step '{assignment.StepKey}'.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:{run.RunId}:cancelled"),
            _ => Failed(
                "process.adapter.workflow_child_state_unsupported",
                $"Workflow run '{run.RunId.Value:D}' has unsupported state '{run.State}'.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:{run.RunId}:{run.State}")
        };
    }

    private async ValueTask<ProcessExecutionAdapterResult> MapCompletedRunAsync(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepExecutionContract stepContract,
        WorkflowRunSnapshot run,
        CancellationToken cancellationToken)
    {
        var events = await runtimeManager.ListEventsAsync(run.RunId, cancellationToken).ConfigureAwait(false);
        var outputEvent = events
            .Where(workflowEvent => workflowEvent.Kind == WorkflowEventKind.ExecutorCompleted)
            .OrderByDescending(workflowEvent => workflowEvent.CreatedAtUtc)
            .ThenByDescending(workflowEvent => workflowEvent.Id)
            .FirstOrDefault();
        if (outputEvent is null)
        {
            return Failed(
                "process.adapter.workflow_output_missing",
                $"Completed workflow run '{run.RunId.Value:D}' has no executor output event to map to a process result.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:{run.RunId}:output-missing");
        }

        WorkflowEventPayloadEnvelope? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>(
                outputEvent.PayloadJson,
                EventJsonOptions);
        }
        catch (JsonException exception)
        {
            return Failed(
                "process.adapter.workflow_output_envelope_invalid",
                $"Workflow run '{run.RunId.Value:D}' returned an invalid output event envelope.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:{run.RunId}:{exception.Message}");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.InlineJson))
        {
            return Failed(
                "process.adapter.workflow_output_missing",
                $"Workflow run '{run.RunId.Value:D}' did not expose an inline process outcome payload.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:{run.RunId}:inline-output-missing");
        }

        if (payload.InlineTruncated)
        {
            return Failed(
                "process.adapter.workflow_output_externalized_unsupported",
                $"Workflow run '{run.RunId.Value:D}' externalized or truncated its process outcome; artifact-backed process output mapping is not supported.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:{run.RunId}:{payload.Reference}:truncated");
        }

        var validation = AgentOutputJson.DeserializeAndValidate(
            payload.InlineJson,
            new ProcessStepOutcomeValidator());
        if (!validation.Succeeded || validation.Output is null)
        {
            return Failed(
                "process.adapter.workflow_output_invalid",
                FormatValidationErrors(validation.Validation.Errors),
                BuildValidationEvidence(validation.RawOutputHash, validation.Validation.Errors));
        }

        return resultConverter.ToAdapterResult(
            assignment,
            validation.Output,
            validation.RawOutputHash,
            toolReceipts: [],
            currentExecutionRunId: run.RunId.Value,
            stepContract: stepContract);
    }

    private static bool IsVerifiedChild(WorkflowRunSnapshot run, ProcessRuntimeStepAssignment assignment, ProcessWorkflowExecutorBinding binding) {
        if (run.WorkflowId.Value != binding.WorkflowId.Value || binding.WorkflowVersionId is { } versionId && run.VersionId.Value != versionId.Value) {
            return false;
        }
        return run.Origin switch {
            WorkflowLaunchOrigin.ProcessAssignment old => old.ProcessRun.Value == assignment.RunId.Value && old.Assignment.Value == assignment.StepInstanceId.Value,
            WorkflowLaunchOrigin.ProcessDispatchAssignment mapped => mapped.Dispatch.ProcessRun.Value == assignment.RunId.Value &&
                mapped.Dispatch.Assignment.Value == assignment.StepInstanceId.Value,
            _ => false
        };
    }

    private static void RequireSameChild(WorkflowRunSnapshot run, ProcessRuntimeStepAssignment assignment,
        ProcessWorkflowExecutorBinding binding, WorkflowLaunchOrigin.ProcessDispatchAssignment origin) {
        if (!IsVerifiedChild(run, assignment, binding) || run.Origin is WorkflowLaunchOrigin.ProcessDispatchAssignment mapped &&
                !origin.Dispatch.HasSameIntent(mapped.Dispatch)) {
            throw new InvalidOperationException($"Mapped Process step '{assignment.StepInstanceId}' already has a different original Workflow contract; reconciliation is required.");
        }
    }

    private static WorkflowDefinitionSelection CreateSelection(ProcessWorkflowExecutorBinding binding)
        => binding.WorkflowVersionId is { } versionId
            ? new WorkflowDefinitionSelection.ExactSavedVersion(
                new WorkflowId(binding.WorkflowId.Value),
                new WorkflowVersionId(versionId.Value))
            : new WorkflowDefinitionSelection.LatestActive(new WorkflowId(binding.WorkflowId.Value));

    private static WorkflowLaunchIdempotencyKey CreateIdempotencyKey(
        ProcessRuntimeStepAssignment assignment)
        => new($"process-assignment:{assignment.RunId.Value:N}:{assignment.StepInstanceId.Value:N}");

    private static string CreateInputJson(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepExecutionContract stepContract)
    {
        var input = new WorkflowProcessAssignmentInputEnvelope(
            WorkflowProcessAssignmentInputEnvelope.CurrentSchemaVersion,
            new ProcessWorkflowRunId(assignment.RunId.Value),
            new ProcessWorkflowAssignmentId(assignment.StepInstanceId.Value),
            assignment.StepKey,
            assignment.RoleKey,
            assignment.Prompt,
            stepContract.ContractHash,
            assignment.LaunchVariables);
        return JsonSerializer.Serialize(input, AgentOutputJson.SerializerOptions);
    }

    private static bool HasUnsupportedProcessArtifactContract(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepExecutionContract stepContract)
        => assignment.ProducedArtifactSlotIds.Count > 0 ||
           assignment.RequiredArtifactSlotIds.Count > 0 ||
           stepContract.RequiredArtifacts.Count > 0 ||
           stepContract.ExpectedProducedArtifacts.Count > 0 ||
           stepContract.RequiredRuntimeToolNames.Count > 0 ||
           stepContract.ArtifactDescriptors.Count > 0 ||
           stepContract.SubprocessArtifactMappings.Count > 0;
}
