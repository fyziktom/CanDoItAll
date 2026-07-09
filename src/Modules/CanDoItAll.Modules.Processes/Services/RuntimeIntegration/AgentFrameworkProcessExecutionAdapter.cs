using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;


internal sealed partial class AgentFrameworkProcessExecutionAdapter : IProcessExecutionAdapter, IProcessStepExecutionDriver
{
    internal const string SubprocessLaunchToolName = "project_structure_process_subprocess_launch";
    private readonly ICanDoItAllAgentWorkspaceFactory workspaceFactory;
    private readonly IAgentReferenceDataProvider agentReferenceDataProvider;
    private readonly IProcessRuntimeStepAssignmentStore assignmentStore;
    private readonly IProcessRuntimeStateStore stateStore;
    private readonly IWorkspaceFileService workspaceFiles;
    private readonly IReadOnlyList<IProcessSubprocessLaunchCoordinator> subprocessLaunchCoordinators;
    private readonly IProcessRuntimeToolPreflightService? runtimeToolPreflightService;
    private readonly IParentSubprocessArtifactBridge parentSubprocessArtifactBridge;
    private readonly IReadOnlyList<IProcessRuntimeOwnedStepExecutor> runtimeOwnedStepExecutors;

    public AgentFrameworkProcessExecutionAdapter(
        ICanDoItAllAgentWorkspaceFactory workspaceFactory,
        IAgentReferenceDataProvider agentReferenceDataProvider,
        IProcessRuntimeStepAssignmentStore assignmentStore,
        IProcessRuntimeStateStore stateStore,
        IWorkspaceFileService workspaceFiles,
        IEnumerable<IProcessSubprocessLaunchCoordinator>? subprocessLaunchCoordinators = null,
        IProcessRuntimeToolPreflightService? runtimeToolPreflightService = null,
        IParentSubprocessArtifactBridge? parentSubprocessArtifactBridge = null,
        IEnumerable<IProcessRuntimeOwnedStepExecutor>? runtimeOwnedStepExecutors = null)
    {
        this.workspaceFactory = workspaceFactory;
        this.agentReferenceDataProvider = agentReferenceDataProvider;
        this.assignmentStore = assignmentStore;
        this.stateStore = stateStore;
        this.workspaceFiles = workspaceFiles;
        this.subprocessLaunchCoordinators = subprocessLaunchCoordinators?.ToArray() ?? [];
        this.runtimeToolPreflightService = runtimeToolPreflightService;
        this.parentSubprocessArtifactBridge = parentSubprocessArtifactBridge ??
            new ParentSubprocessArtifactBridge(assignmentStore, stateStore, workspaceFiles);
        this.runtimeOwnedStepExecutors = runtimeOwnedStepExecutors?.ToArray() ?? [];
    }

    public ProcessExecutionAdapterDescriptor Descriptor => StandardProcessAdapterDescriptors.WorkflowAdapter;

    ProcessStepExecutionDriverDescriptor IProcessStepExecutionDriver.Descriptor => new(
        StandardProcessAdapterDriverIds.Workflow,
        Descriptor,
        Descriptor.Strategy);

    public ValueTask<ProcessExecutionAdapterResult> ExecuteStepAsync(
        ProcessExecutionAdapterRequest request,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(request, cancellationToken);

    public async ValueTask<ProcessExecutionAdapterResult> ExecuteAsync(
        ProcessExecutionAdapterRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.StepId is not { } stepId)
        {
            return Failed("process.adapter.step_missing", "The process execution adapter requires a concrete step id.", "step-missing");
        }

        var assignment = await assignmentStore.LoadAsync(request.RunId, stepId, cancellationToken).ConfigureAwait(false);
        if (assignment is null)
        {
            return Failed("process.adapter.assignment_missing", $"No runtime assignment exists for step '{stepId}'.", stepId.ToString());
        }

        if (await TryResolveExistingSubprocessBridgeAsync(
                assignment,
                cancellationToken).ConfigureAwait(false) is { } existingBridgeResult)
        {
            return existingBridgeResult;
        }

        if (await TryLaunchMappedSubprocessAsync(
                assignment,
                assignmentStore,
                stateStore,
                cancellationToken).ConfigureAwait(false) is { } launchedSubprocessResult)
        {
            return launchedSubprocessResult;
        }

        if (!string.Equals(assignment.ExecutorKind, ProcessLaunchExecutorKinds.Agent, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParse(assignment.ExecutorId, out var agentId))
        {
            return Failed(
                "process.adapter.executor_invalid",
                $"Step '{assignment.StepKey}' has invalid executor binding '{assignment.ExecutorKind}:{assignment.ExecutorId}'.",
                assignment.ExecutorId);
        }

        var referenceData = await agentReferenceDataProvider
            .GetAsync(new AgentReferenceDataRequest(AgentReferenceDataSections.Agents), cancellationToken)
            .ConfigureAwait(false);
        var agent = referenceData.Agents.FirstOrDefault(candidate => candidate.Id == agentId);
        if (agent is null)
        {
            return NeedsManager(
                "process.adapter.executor_agent_missing",
                $"Step '{assignment.StepKey}' is assigned to missing agent '{agentId}'.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:{agentId}");
        }

        var readiness = AgentProcessReadinessEvaluator.Evaluate(agent, CreateRuntimeReadinessRequest(assignment));
        if (!readiness.IsExecutionReady || !readiness.HasRoleFit)
        {
            return NeedsManager(
                "process.adapter.executor_readiness_failed",
                $"Step '{assignment.StepKey}' cannot run with assigned agent '{agent.Name}': {readiness.ReadinessSummary}",
                $"{assignment.RunId}:{assignment.StepInstanceId}:{agentId}:{readiness.ReadinessHash}");
        }

        if (runtimeToolPreflightService is not null)
        {
            var runtimeToolPreflight = await runtimeToolPreflightService
                .EvaluateAsync(
                    new ProcessRuntimeToolPreflightRequest(
                        assignment,
                        agent,
                        ResolvePreflightRequiredRuntimeToolNames(assignment, request.StepContract)),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!runtimeToolPreflight.IsSatisfied)
            {
                var issue = CreateRuntimeToolPreflightIssue(assignment, runtimeToolPreflight);
                return NeedsManagerForCompletionIssue(
                    assignment,
                    ComputeHash(issue.Evidence),
                    issue);
            }
        }

        if (await TryExecuteRuntimeOwnedStepAsync(
                assignment,
                cancellationToken).ConfigureAwait(false) is { } runtimeOwnedStepResult)
        {
            return runtimeOwnedStepResult;
        }

        try
        {
            var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
            var metadataJson = BuildProcessExecutionMetadata(assignment);
            var promptStepContract = ResolvePromptStepContract(assignment, request.StepContract);
            var result = await workspaceService
                .ExecuteRunAsync(
                    new ExecutionRunRequest(
                        agentId,
                        ProcessStepContractPromptBuilder.Build(
                            assignment.Prompt,
                            promptStepContract,
                            assignment.LaunchVariables,
                            assignment.StepKey),
                        Context: new ExecutionInvocationContext(
                            SourceKind: ProcessMockAgentCatalog.ProcessSourceKind,
                            SourceId: assignment.StepKey,
                            CorrelationId: request.RunId.ToString(),
                            CausationId: stepId.ToString(),
                            RequestedBy: "process-runtime",
                            RequestedByKind: "system",
                            MetadataJson: metadataJson,
                            ProcessRunId: request.RunId.ToString(),
                            ProcessStepId: stepId.ToString(),
                            Policy: new ExecutionInvocationPolicy(
                                MaxStructuredOutputRepairAttempts: ExecutionInvocationMetadata.DefaultGovernedRepairAttempts)),
                        AutoApprovePendingToolCalls: true,
                        StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult),
                    cancellationToken)
                .ConfigureAwait(false);

            if (await TryResolveExistingSubprocessBridgeAsync(
                    assignment,
                    cancellationToken).ConfigureAwait(false) is { } bridgeResultAfterExecution)
            {
                return bridgeResultAfterExecution;
            }

            if (TryBuildRetryableAgentTransientExecutionIssue(assignment, result, out var transientExecutionIssue))
            {
                return NeedsManagerForCompletionIssue(
                    assignment,
                    ComputeHash($"{result.ExecutionRunId:D}:{result.Metric.Outcome}:{result.ResponseText}"),
                    transientExecutionIssue);
            }

            var validation = AgentOutputJson.DeserializeAndValidate(
                result.ResponseText,
                new ProcessStepOutcomeValidator());

            if (!validation.Succeeded || validation.Output is null)
            {
                return Failed(
                    "process.adapter.output_invalid",
                    FormatValidationErrors(validation.Validation.Errors),
                    validation.RawOutputHash);
            }

            if (await TryResolveDeferredOrCompletedSubprocessOutputAsync(
                    assignment,
                    validation.Output,
                    assignmentStore,
                    stateStore,
                    cancellationToken).ConfigureAwait(false) is { } subprocessResult)
            {
                return subprocessResult;
            }

            var executionDetail = await workspaceService
                .GetExecutionRunDetailAsync(result.ExecutionRunId, cancellationToken)
                .ConfigureAwait(false);

            var materialization = MaterializeManagedOutcomeArtifactIfNeeded(
                assignment,
                validation.Output,
                result.ExecutionRunId,
                executionDetail.ToolReceipts);
            if (materialization.Issue is { } materializationIssue)
            {
                return NeedsManagerForCompletionIssue(assignment, validation.RawOutputHash, materializationIssue);
            }

            if (await TryResolveDeferredOrCompletedSubprocessOutputAsync(
                    assignment,
                    materialization.Output,
                    assignmentStore,
                    stateStore,
                    cancellationToken).ConfigureAwait(false) is { } materializedSubprocessResult)
            {
                return materializedSubprocessResult;
            }

            if (await TryResolveExistingSubprocessBridgeAsync(
                    assignment,
                    cancellationToken).ConfigureAwait(false) is { } materializedBridgeResult)
            {
                return materializedBridgeResult;
            }

            var completionToolReceipts = await LoadStepCompletionToolReceiptsAsync(
                    workspaceService,
                    assignment,
                    result.ExecutionRunId,
                    materialization.ToolReceipts,
                    cancellationToken)
                .ConfigureAwait(false);

            if (ValidateManagedArtifactBodyReferences(
                    assignment,
                    materialization.Output,
                    completionToolReceipts) is { } ungroundedArtifactReferenceIssue)
            {
                return NeedsManagerForCompletionIssue(
                    assignment,
                    validation.RawOutputHash,
                    ungroundedArtifactReferenceIssue);
            }

            var completionGateEvaluation = CompletionGateEvaluator.Evaluate(new ProcessCompletionGateContext(
                assignment,
                materialization.Output,
                completionToolReceipts,
                result.ExecutionRunId));
            if (!completionGateEvaluation.IsSatisfied)
            {
                if (AppendRuntimeGateFindingsForRoutedCompletionIssue(
                        assignment,
                        materialization.Output,
                        result.ExecutionRunId,
                        completionGateEvaluation) is { } runtimeGateFindingsIssue)
                {
                    return NeedsManagerForCompletionIssue(
                        assignment,
                        validation.RawOutputHash,
                        runtimeGateFindingsIssue);
                }

                if (TryCreateRoutedCompletionIssueResult(
                        assignment,
                        materialization.Output,
                        validation.RawOutputHash,
                        completionGateEvaluation,
                        producedArtifactContentHashes: null,
                        out var routedCompletionIssueResult))
                {
                    return routedCompletionIssueResult;
                }

                return NeedsManagerForCompletionIssues(
                    assignment,
                    validation.RawOutputHash,
                    completionGateEvaluation);
            }

            if (AcceptManagedOutcomeArtifactIfNeeded(
                    assignment,
                    materialization,
                    result.ExecutionRunId,
                    completionToolReceipts,
                    out var acceptedCompletionToolReceipts) is { } acceptanceIssue)
            {
                return NeedsManagerForCompletionIssue(
                    assignment,
                    validation.RawOutputHash,
                    acceptanceIssue);
            }

            var producedArtifactContentHashes = BuildProducedArtifactContentHashes(
                assignment,
                materialization.Output,
                out var producedArtifactReadbackIssue);
            if (producedArtifactReadbackIssue is not null)
            {
                return NeedsManagerForCompletionIssue(
                    assignment,
                    validation.RawOutputHash,
                    producedArtifactReadbackIssue);
            }

            return ToAdapterResult(
                assignment,
                materialization.Output,
                validation.RawOutputHash,
                acceptedCompletionToolReceipts,
                result.ExecutionRunId,
                producedArtifactContentHashes);
        }
        catch (ProcessRuntimeDispatchDeferredException)
        {
            throw;
        }
        catch (AgentExecutionCancelledException exception)
        {
            return Canceled(
                "process.adapter.agent_execution_cancelled",
                $"Agent execution was cancelled for step '{assignment.StepKey}': {exception.Message}",
                $"{exception.ExecutionRunId:N}:{exception.ProcessRunId}:{exception.Message}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (await ParentSubprocessArtifactBridge.TryResolveExistingPendingChildRunAsync(
                    assignment,
                    assignmentStore,
                    stateStore,
                    CancellationToken.None).ConfigureAwait(false) is { } pendingChildRunId)
            {
                throw CreatePendingChildRunDeferredException(assignment, pendingChildRunId);
            }

            if (TryBuildRetryableAgentOutputContractIssue(assignment, exception, out var outputContractIssue))
            {
                return NeedsManagerForCompletionIssue(
                    assignment,
                    ComputeHash(exception.GetType().FullName + ":" + exception.Message),
                    outputContractIssue);
            }

            if (TryBuildRetryableAgentTransientExecutionIssue(assignment, exception, out var transientExecutionIssue))
            {
                return NeedsManagerForCompletionIssue(
                    assignment,
                    ComputeHash(exception.GetType().FullName + ":" + exception.Message),
                    transientExecutionIssue);
            }

            return Failed(
                "process.adapter.agent_execution_failed",
                $"Agent execution failed for step '{assignment.StepKey}': {exception.Message}",
                ComputeHash(exception.GetType().FullName + ":" + exception.Message));
        }
    }

    private static ProcessStepExecutionContract ResolvePromptStepContract(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepExecutionContract stepContract)
    {
        var requiredRuntimeToolNames = ResolvePreflightRequiredRuntimeToolNames(assignment, stepContract);
        return requiredRuntimeToolNames.SequenceEqual(stepContract.RequiredRuntimeToolNames, StringComparer.OrdinalIgnoreCase)
            ? stepContract
            : stepContract with
            {
                RequiredRuntimeToolNames = requiredRuntimeToolNames
            };
    }

}
