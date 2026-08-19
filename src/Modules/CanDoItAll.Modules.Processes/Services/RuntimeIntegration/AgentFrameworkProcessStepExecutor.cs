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
using CanDoItAll.Processes.Contracts;
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

        if (request.DispatchClaimIdentity.Value == Guid.Empty)
        {
            return Failed(
                "process.adapter.dispatch_claim_missing",
                "The process execution adapter requires a concrete dispatch claim identity.",
                "dispatch-claim-missing");
        }

        if (request.StepId is not { } stepId)
        {
            return Failed("process.adapter.step_missing", "The process execution adapter requires a concrete step id.", "step-missing");
        }

        var assignment = await assignmentStore.LoadAsync(request.RunId, stepId, cancellationToken).ConfigureAwait(false);
        if (assignment is null)
        {
            return Failed("process.adapter.assignment_missing", $"No runtime assignment exists for step '{stepId}'.", stepId.ToString());
        }

        if (!ProcessRequiredRuntimeToolNames.IsValidBoundedContract(
                request.StepContract.RequiredRuntimeToolNames))
        {
            var invalidContractPreflight = await runtimeToolPreflightService
                .EvaluateStepHostCapabilitiesAsync(
                    request.StepContract.RequiredRuntimeToolNames,
                    request.StepContract.RequiredRuntimeToolNames,
                    request.StepContract.RequiredHostCapabilities,
                    cancellationToken)
                .ConfigureAwait(false);
            return CreateHostCapabilityFailureResult(assignment, invalidContractPreflight);
        }

        var currentRuntimeToolNames = ResolvePreflightRequiredRuntimeToolNames(
            assignment,
            request.StepContract);
        if (!currentRuntimeToolNames.SequenceEqual(
                request.StepContract.RequiredRuntimeToolNames,
                StringComparer.OrdinalIgnoreCase))
        {
            return AttachHostCapabilityEvidence(
                CreateRuntimeToolContractChangedResult(assignment),
                request.DispatchHostCapabilityEvidence);
        }

        if (await subprocessCoordinator.TryResolveExistingSubprocessBridgeAsync(
                assignment,
                cancellationToken,
                request.StepContract).ConfigureAwait(false) is { } existingBridgeResult)
        {
            return existingBridgeResult;
        }

        ProcessHostCapabilityEvaluationEvidence? dispatchHostCapabilityEvidence =
            request.DispatchHostCapabilityEvidence;

        ProcessExecutionAdapterResult CompleteWithDispatchHostEvidence(ProcessExecutionAdapterResult result)
            => AttachHostCapabilityEvidence(result, dispatchHostCapabilityEvidence);

        async ValueTask<ProcessExecutionAdapterResult?> EvaluateHostCapabilityGateAsync(
            CancellationToken gateCancellationToken)
        {
            var preflight = await runtimeToolPreflightService
                .EvaluateStepHostCapabilitiesAsync(
                    request.StepContract.RequiredRuntimeToolNames,
                    request.StepContract.RequiredRuntimeToolNames,
                    request.StepContract.RequiredHostCapabilities,
                    gateCancellationToken)
                .ConfigureAwait(false);
            if (!ProcessHostCapabilityEvidencePolicy.TryMerge(
                    dispatchHostCapabilityEvidence,
                    preflight.HostCapabilityEvidence,
                    out var mergedHostCapabilityEvidence))
            {
                var inconsistentEvidence = ProcessHostCapabilityEvidencePolicy.CreateUnstableEvidence(
                    dispatchHostCapabilityEvidence,
                    preflight.HostCapabilityEvidence);
                var inconsistentPreflight = new ProcessRuntimeToolPreflightResult(
                    false,
                    ["host-capability-snapshot-changed"],
                    "Process host capability facts changed between dispatch checks. The step was rejected before execution; retry after the host profile is stable.")
                {
                    HostCapabilityEvidence = inconsistentEvidence
                };
                return CreateHostCapabilityFailureResult(assignment, inconsistentPreflight);
            }

            dispatchHostCapabilityEvidence = mergedHostCapabilityEvidence;
            if (!preflight.IsSatisfied)
            {
                return CreateHostCapabilityFailureResult(assignment, preflight);
            }

            return null;
        }

        if (ProcessLaunchExecutorKinds.IsWorkflow(assignment.ExecutorKind))
        {
            var workflowResult = await workflowStepExecutor
                .ExecuteAsync(
                    assignment,
                    request.StepContract,
                    cancellationToken,
                    EvaluateHostCapabilityGateAsync)
                .ConfigureAwait(false);
            return CompleteWithDispatchHostEvidence(workflowResult);
        }

        if (await EvaluateHostCapabilityGateAsync(cancellationToken).ConfigureAwait(false) is { } hostCapabilityFailure)
        {
            return hostCapabilityFailure;
        }

        if (await subprocessCoordinator.TryLaunchMappedSubprocessAsync(
                assignment,
                assignmentStore,
                stateStore,
                cancellationToken,
                request.StepContract).ConfigureAwait(false) is { } launchedSubprocessResult)
        {
            return CompleteWithDispatchHostEvidence(launchedSubprocessResult);
        }

        if (await runtimeOwnedStepCoordinator.TryExecuteRuntimeOwnedStepAsync(
                assignment,
                cancellationToken,
                request.StepContract).ConfigureAwait(false) is { } runtimeOwnedStepResult)
        {
            return CompleteWithDispatchHostEvidence(runtimeOwnedStepResult);
        }

        if (!string.Equals(assignment.ExecutorKind, ProcessLaunchExecutorKinds.Agent, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParse(assignment.ExecutorId, out var agentId))
        {
            return CompleteWithDispatchHostEvidence(Failed(
                "process.adapter.executor_invalid",
                $"Step '{assignment.StepKey}' has invalid executor binding '{assignment.ExecutorKind}:{assignment.ExecutorId}'.",
                assignment.ExecutorId));
        }

        var referenceData = await agentReferenceDataProvider
            .GetAsync(new AgentReferenceDataRequest(AgentReferenceDataSections.Agents), cancellationToken)
            .ConfigureAwait(false);
        var agent = referenceData.Agents.FirstOrDefault(candidate => candidate.Id == agentId);
        if (agent is null)
        {
            return CompleteWithDispatchHostEvidence(NeedsManager(
                "process.adapter.executor_agent_missing",
                $"Step '{assignment.StepKey}' is assigned to missing agent '{agentId}'.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:{agentId}"));
        }

        var readiness = AgentProcessReadinessEvaluator.Evaluate(agent, CreateRuntimeReadinessRequest(assignment));
        if (!readiness.IsExecutionReady || !readiness.HasRoleFit)
        {
            return CompleteWithDispatchHostEvidence(NeedsManager(
                "process.adapter.executor_readiness_failed",
                $"Step '{assignment.StepKey}' cannot run with assigned agent '{agent.Name}': {readiness.ReadinessSummary}",
                $"{assignment.RunId}:{assignment.StepInstanceId}:{agentId}:{readiness.ReadinessHash}"));
        }

        IAgentFrameworkWorkspaceService? workspaceService = null;
        try
        {
            var runtimeToolPreflight = await runtimeToolPreflightService
                .EvaluateAsync(
                    new ProcessRuntimeToolPreflightRequest(
                        assignment,
                        agent,
                        request.StepContract.RequiredRuntimeToolNames,
                        CapabilityCatalogResolver: ResolveAttachedCapabilityCatalogAsync),
                    cancellationToken)
                .ConfigureAwait(false);
            var gateHostCapabilityEvidence = dispatchHostCapabilityEvidence;
            if (!ProcessHostCapabilityEvidencePolicy.TryMerge(
                    gateHostCapabilityEvidence,
                    runtimeToolPreflight.HostCapabilityEvidence,
                    out var mergedHostCapabilityEvidence))
            {
                var inconsistentEvidence = ProcessHostCapabilityEvidencePolicy.CreateUnstableEvidence(
                    gateHostCapabilityEvidence,
                    runtimeToolPreflight.HostCapabilityEvidence);
                var inconsistentPreflight = new ProcessRuntimeToolPreflightResult(
                    false,
                    ["host-capability-snapshot-changed"],
                    "Process host capability facts changed between dispatch checks. The step was rejected before agent execution; retry after the host profile is stable.")
                {
                    HostCapabilityEvidence = inconsistentEvidence
                };
                return CreateHostCapabilityFailureResult(assignment, inconsistentPreflight);
            }

            dispatchHostCapabilityEvidence = mergedHostCapabilityEvidence;

            if (!runtimeToolPreflight.IsSatisfied)
            {
                var issue = CreateRuntimeToolPreflightIssue(assignment, runtimeToolPreflight);
                return CompleteWithDispatchHostEvidence(NeedsManagerForCompletionIssue(
                    assignment,
                    ComputeHash(issue.Evidence),
                    issue));
            }

            workspaceService ??= workspaceFactory.GetOrganizationWorkspaceService();
            var metadataJson = executionMetadataComposer.ComposeClaimedExecution(
                assignment,
                request.DispatchClaimIdentity,
                dispatchHostCapabilityEvidence);
            var parentArtifactContext = parentArtifactContextHydrator.Hydrate(assignment);
            if (parentArtifactContext.Issue is { } parentArtifactContextIssue)
            {
                return CompleteWithDispatchHostEvidence(NeedsManagerForCompletionIssue(
                    assignment,
                    ComputeHash(parentArtifactContextIssue.Evidence),
                    parentArtifactContextIssue));
            }

            var executionPrompt = string.IsNullOrWhiteSpace(parentArtifactContext.PromptContribution)
                ? assignment.Prompt
                : $"{assignment.Prompt}{Environment.NewLine}{Environment.NewLine}{parentArtifactContext.PromptContribution}";
            var promptStepContract = request.StepContract;
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
                        AgentExecutionOperationId.New(),
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
                return CompleteWithDispatchHostEvidence(bridgeResultAfterExecution);
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
                return CompleteWithDispatchHostEvidence(NeedsManagerForCompletionIssue(
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
                });
            }

            var validation = AgentOutputJson.DeserializeAndValidate(
                result.ResponseText,
                new ProcessStepOutcomeValidator());

            if (!validation.Succeeded || validation.Output is null)
            {
                return CompleteWithDispatchHostEvidence(Failed(
                    "process.adapter.output_invalid",
                    FormatValidationErrors(validation.Validation.Errors),
                    BuildValidationEvidence(validation.RawOutputHash, validation.Validation.Errors)));
            }

            if (await subprocessCoordinator.TryResolveDeferredOrCompletedSubprocessOutputAsync(
                    assignment,
                    validation.Output,
                    assignmentStore,
                    stateStore,
                    cancellationToken,
                    request.StepContract).ConfigureAwait(false) is { } subprocessResult)
            {
                return CompleteWithDispatchHostEvidence(subprocessResult);
            }

            var executionDetail = await workspaceService
                .GetExecutionRunDetailAsync(result.ExecutionRunId, cancellationToken)
                .ConfigureAwait(false);

            var materialization = completionCoordinator.Materialize(
                assignment,
                validation.Output,
                result.ExecutionRunId,
                executionDetail.ToolReceipts,
                request.StepContract);
            if (materialization.Issue is { } materializationIssue)
            {
                return CompleteWithDispatchHostEvidence(
                    NeedsManagerForCompletionIssue(assignment, validation.RawOutputHash, materializationIssue));
            }

            if (await subprocessCoordinator.TryResolveDeferredOrCompletedSubprocessOutputAsync(
                    assignment,
                    materialization.Output,
                    assignmentStore,
                    stateStore,
                    cancellationToken,
                    request.StepContract).ConfigureAwait(false) is { } materializedSubprocessResult)
            {
                return CompleteWithDispatchHostEvidence(materializedSubprocessResult);
            }

            if (await subprocessCoordinator.TryResolveExistingSubprocessBridgeAsync(
                    assignment,
                    cancellationToken,
                    request.StepContract).ConfigureAwait(false) is { } materializedBridgeResult)
            {
                return CompleteWithDispatchHostEvidence(materializedBridgeResult);
            }

            var completionToolReceipts = await completionCoordinator.LoadCompletionToolReceiptsAsync(
                    workspaceService,
                    assignment,
                    result.ExecutionRunId,
                    materialization.ToolReceipts,
                    cancellationToken)
                .ConfigureAwait(false);

            return CompleteWithDispatchHostEvidence(completionCoordinator.Complete(
                assignment,
                materialization,
                validation.RawOutputHash,
                result.ExecutionRunId,
                completionToolReceipts,
                appendRuntimeGateFindings: true,
                stepContract: request.StepContract));
        }
        catch (ProcessRuntimeDispatchDeferredException)
        {
            throw;
        }
        catch (AgentExecutionCancelledException exception)
        {
            return CompleteWithDispatchHostEvidence(Canceled(
                "process.adapter.agent_execution_cancelled",
                $"Agent execution was cancelled for step '{assignment.StepKey}'. Review the restricted execution log using the recorded evidence hash if more detail is required.",
                $"{exception.ExecutionRunId:N}:{exception.ProcessRunId}:{exception.Message}"));
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
                return CompleteWithDispatchHostEvidence(NeedsManagerForCompletionIssue(
                    assignment,
                    ComputeHash(exception.GetType().FullName + ":" + exception.Message),
                    outputContractIssue));
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

                return CompleteWithDispatchHostEvidence(NeedsManagerForCompletionIssue(
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
                });
            }

            return CompleteWithDispatchHostEvidence(Failed(
                "process.adapter.agent_execution_failed",
                $"Agent execution failed for step '{assignment.StepKey}'. Review the restricted execution log using the recorded evidence hash if more detail is required.",
                ComputeHash(exception.GetType().FullName + ":" + exception.Message)));
        }

        async ValueTask<IReadOnlyList<CapabilityCatalogItem>> ResolveAttachedCapabilityCatalogAsync(
            CancellationToken token)
        {
            workspaceService ??= workspaceFactory.GetOrganizationWorkspaceService();
            var capabilityCatalog = await workspaceService
                .ListCapabilitiesAsync(token)
                .ConfigureAwait(false);
            var assignedCapabilityIds = agent.Capabilities
                .Select(capability => capability.CapabilityId)
                .ToHashSet();
            return capabilityCatalog
                .Where(capability => assignedCapabilityIds.Contains(capability.Id))
                .ToArray();
        }
    }

    private static ProcessExecutionAdapterResult CreateHostCapabilityFailureResult(
        ProcessRuntimeStepAssignment assignment,
        ProcessRuntimeToolPreflightResult preflight)
    {
        var issue = CreateRuntimeToolPreflightIssue(assignment, preflight);
        return AttachHostCapabilityEvidence(
            NeedsManagerForCompletionIssue(
                assignment,
                ComputeHash(issue.Evidence),
                issue),
            preflight.HostCapabilityEvidence);
    }

    private static ProcessExecutionAdapterResult CreateRuntimeToolContractChangedResult(
        ProcessRuntimeStepAssignment assignment)
    {
        var issue = new ProcessCompletionIssue(
            "process.adapter.runtime_tool_contract_changed",
            $"Step '{assignment.StepKey}' has runtime-tool requirements that differ from its immutable process plan. Repair or reseal the assignment before retrying.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-tool-contract-changed",
            [],
            ProcessDiagnosticRetrySafety.UnsafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Unknown);
        return NeedsManagerForCompletionIssue(
            assignment,
            ComputeHash(issue.Evidence),
            issue);
    }

    private static ProcessExecutionAdapterResult AttachHostCapabilityEvidence(
        ProcessExecutionAdapterResult result,
        ProcessHostCapabilityEvaluationEvidence? evidence)
        => evidence is null
            ? result
            : result with { HostCapabilityEvidence = evidence };

}
