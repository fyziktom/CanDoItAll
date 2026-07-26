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

using static CanDoItAll.Modules.Processes.ProcessAgentExecutionRecoveryPolicy;
using static CanDoItAll.Modules.Processes.ProcessCompletionIssueResultFactory;
using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionMetadataBuilder;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultConverter;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactService;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactFormatter;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactOutcomeParser;
using static CanDoItAll.Modules.Processes.ProcessOutcomeGroundingValidator;
using static CanDoItAll.Modules.Processes.ProcessSubprocessCompletionPolicy;
using static CanDoItAll.Modules.Processes.ProcessSubprocessState;

namespace CanDoItAll.Modules.Processes;


internal sealed class AgentFrameworkProcessStepExecutor : IAgentFrameworkProcessStepExecutor
{
    private readonly ICanDoItAllAgentWorkspaceFactory workspaceFactory;
    private readonly IAgentReferenceDataProvider agentReferenceDataProvider;
    private readonly IProcessRuntimeStepAssignmentStore assignmentStore;
    private readonly IProcessRuntimeStateStore stateStore;
    private readonly IProcessWorkflowStepExecutor workflowStepExecutor;
    private readonly IProcessRuntimeToolPreflightService runtimeToolPreflightService;
    private readonly ProcessRuntimeOwnedStepCoordinator runtimeOwnedStepCoordinator;
    private readonly ProcessSubprocessCoordinator subprocessCoordinator;
    private readonly ProcessStepCompletionCoordinator completionCoordinator;
    private readonly ProcessSubprocessContractResolver subprocessContractResolver;
    private readonly ProcessParentSubprocessArtifactContextHydrator parentArtifactContextHydrator;
    private readonly ProcessExecutionMetadataComposer executionMetadataComposer;

    public AgentFrameworkProcessStepExecutor(
        ICanDoItAllAgentWorkspaceFactory workspaceFactory,
        IAgentReferenceDataProvider agentReferenceDataProvider,
        IProcessRuntimeStepAssignmentStore assignmentStore,
        IProcessRuntimeStateStore stateStore,
        IProcessWorkflowStepExecutor workflowStepExecutor,
        IProcessRuntimeToolPreflightService runtimeToolPreflightService,
        ProcessRuntimeOwnedStepCoordinator runtimeOwnedStepCoordinator,
        ProcessSubprocessCoordinator subprocessCoordinator,
        ProcessStepCompletionCoordinator completionCoordinator,
        ProcessSubprocessContractResolver subprocessContractResolver,
        ProcessParentSubprocessArtifactContextHydrator parentArtifactContextHydrator,
        ProcessExecutionMetadataComposer executionMetadataComposer)
    {
        this.workspaceFactory = workspaceFactory;
        this.agentReferenceDataProvider = agentReferenceDataProvider;
        this.assignmentStore = assignmentStore;
        this.stateStore = stateStore;
        this.workflowStepExecutor = workflowStepExecutor ??
            throw new ArgumentNullException(nameof(workflowStepExecutor));
        this.runtimeToolPreflightService = runtimeToolPreflightService ??
            throw new ArgumentNullException(nameof(runtimeToolPreflightService));
        this.runtimeOwnedStepCoordinator = runtimeOwnedStepCoordinator;
        this.subprocessCoordinator = subprocessCoordinator;
        this.completionCoordinator = completionCoordinator;
        this.subprocessContractResolver = subprocessContractResolver;
        this.parentArtifactContextHydrator = parentArtifactContextHydrator;
        this.executionMetadataComposer = executionMetadataComposer ??
            throw new ArgumentNullException(nameof(executionMetadataComposer));
    }

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

        if (ProcessLaunchExecutorKinds.IsWorkflow(assignment.ExecutorKind))
        {
            return await workflowStepExecutor
                .ExecuteAsync(assignment, request.StepContract, cancellationToken)
                .ConfigureAwait(false);
        }

        if (await subprocessCoordinator.TryResolveExistingSubprocessBridgeAsync(
                assignment,
                cancellationToken,
                request.StepContract).ConfigureAwait(false) is { } existingBridgeResult)
        {
            return existingBridgeResult;
        }

        if (await subprocessCoordinator.TryLaunchMappedSubprocessAsync(
                assignment,
                assignmentStore,
                stateStore,
                cancellationToken,
                request.StepContract).ConfigureAwait(false) is { } launchedSubprocessResult)
        {
            return launchedSubprocessResult;
        }

        if (await runtimeOwnedStepCoordinator.TryExecuteRuntimeOwnedStepAsync(
                assignment,
                cancellationToken,
                request.StepContract).ConfigureAwait(false) is { } runtimeOwnedStepResult)
        {
            return runtimeOwnedStepResult;
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

        IAgentFrameworkWorkspaceService? workspaceService = null;
        try
        {
            workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
            var metadataJson = executionMetadataComposer.Compose(assignment);
            var parentArtifactContext = parentArtifactContextHydrator.Hydrate(assignment);
            if (parentArtifactContext.Issue is { } parentArtifactContextIssue)
            {
                return NeedsManagerForCompletionIssue(
                    assignment,
                    ComputeHash(parentArtifactContextIssue.Evidence),
                    parentArtifactContextIssue);
            }

            var executionPrompt = string.IsNullOrWhiteSpace(parentArtifactContext.PromptContribution)
                ? assignment.Prompt
                : $"{assignment.Prompt}{Environment.NewLine}{Environment.NewLine}{parentArtifactContext.PromptContribution}";
            var promptStepContract = ResolvePromptStepContract(assignment, request.StepContract);
            var subprocessContract = subprocessContractResolver.TryResolve(assignment, out var resolvedSubprocessContract)
                ? resolvedSubprocessContract
                : null;
            var result = await workspaceService
                .ExecuteRunAsync(
                    new ExecutionRunRequest(
                        agentId,
                        ProcessStepContractPromptBuilder.Build(
                            executionPrompt,
                            promptStepContract,
                            assignment.LaunchVariables,
                            assignment.StepKey,
                            subprocessContract),
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
                                MaxStructuredOutputRepairAttempts: ExecutionInvocationMetadata.DefaultGovernedRepairAttempts,
                                AllowRequiredFinalizerStructuredOutputRecovery: true)),
                        AutoApprovePendingToolCalls: true,
                        StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult),
                    cancellationToken)
                .ConfigureAwait(false);

            if (await subprocessCoordinator.TryResolveExistingSubprocessBridgeAsync(
                    assignment,
                    cancellationToken,
                    request.StepContract).ConfigureAwait(false) is { } bridgeResultAfterExecution)
            {
                return bridgeResultAfterExecution;
            }

            if (TryBuildRetryableAgentTransientExecutionIssue(assignment, result, out var transientExecutionIssue))
            {
                transientExecutionIssue = await AttestRetryableAgentTransientExecutionIssueAsync(
                        assignment,
                        workspaceService,
                        result.ExecutionRunId,
                        result.Metric,
                        $"executionRunId={result.ExecutionRunId:D}; outcome={result.Metric.Outcome}; provider={result.Metric.ProviderName}; model={result.Metric.Model}; detail={result.ResponseText}",
                        cancellationToken)
                    .ConfigureAwait(false);
                return NeedsManagerForCompletionIssue(
                    assignment,
                    string.Equals(
                        transientExecutionIssue.Code,
                        ProcessExecutionAdapterDiagnosticCodes.AgentTransientExecutionBeforeSideEffects,
                        StringComparison.Ordinal)
                        ? ComputeHash(transientExecutionIssue.Evidence)
                        : ComputeHash($"{result.ExecutionRunId:D}:{result.Metric.Outcome}:{result.ResponseText}"),
                    transientExecutionIssue) with
                {
                    ExecutionRunId = new ProcessExecutionRunId(result.ExecutionRunId)
                };
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

            if (await subprocessCoordinator.TryResolveDeferredOrCompletedSubprocessOutputAsync(
                    assignment,
                    validation.Output,
                    assignmentStore,
                    stateStore,
                    cancellationToken,
                    request.StepContract).ConfigureAwait(false) is { } subprocessResult)
            {
                return subprocessResult;
            }

            var executionDetail = await workspaceService
                .GetExecutionRunDetailAsync(result.ExecutionRunId, cancellationToken)
                .ConfigureAwait(false);

            var materialization = completionCoordinator.Materialize(
                assignment,
                validation.Output,
                result.ExecutionRunId,
                executionDetail.ToolReceipts);
            if (materialization.Issue is { } materializationIssue)
            {
                return NeedsManagerForCompletionIssue(assignment, validation.RawOutputHash, materializationIssue);
            }

            if (await subprocessCoordinator.TryResolveDeferredOrCompletedSubprocessOutputAsync(
                    assignment,
                    materialization.Output,
                    assignmentStore,
                    stateStore,
                    cancellationToken,
                    request.StepContract).ConfigureAwait(false) is { } materializedSubprocessResult)
            {
                return materializedSubprocessResult;
            }

            if (await subprocessCoordinator.TryResolveExistingSubprocessBridgeAsync(
                    assignment,
                    cancellationToken,
                    request.StepContract).ConfigureAwait(false) is { } materializedBridgeResult)
            {
                return materializedBridgeResult;
            }

            var completionToolReceipts = await completionCoordinator.LoadCompletionToolReceiptsAsync(
                    workspaceService,
                    assignment,
                    result.ExecutionRunId,
                    materialization.ToolReceipts,
                    cancellationToken)
                .ConfigureAwait(false);

            return completionCoordinator.Complete(
                assignment,
                materialization,
                validation.RawOutputHash,
                result.ExecutionRunId,
                completionToolReceipts,
                appendRuntimeGateFindings: true,
                stepContract: request.StepContract);
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
                if (exception is AgentRunFailedException agentRunFailedException &&
                    workspaceService is not null)
                {
                    transientExecutionIssue = await AttestRetryableAgentTransientExecutionIssueAsync(
                            assignment,
                            workspaceService,
                            agentRunFailedException.ExecutionRunId,
                            immediateMetric: null,
                            $"{exception.GetType().Name}: {exception.Message}",
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }

                return NeedsManagerForCompletionIssue(
                    assignment,
                    string.Equals(
                        transientExecutionIssue.Code,
                        ProcessExecutionAdapterDiagnosticCodes.AgentTransientExecutionBeforeSideEffects,
                        StringComparison.Ordinal)
                        ? ComputeHash(transientExecutionIssue.Evidence)
                        : ComputeHash(exception.GetType().FullName + ":" + exception.Message),
                    transientExecutionIssue) with
                {
                    ExecutionRunId = exception is AgentRunFailedException failedException &&
                                     failedException.ExecutionRunId != Guid.Empty
                        ? new ProcessExecutionRunId(failedException.ExecutionRunId)
                        : null
                };
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
