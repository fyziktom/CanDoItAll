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

using static CanDoItAll.Modules.Processes.ProcessCompletionIssueResultFactory;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultConverter;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactService;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactFormatter;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactOutcomeParser;
using static CanDoItAll.Modules.Processes.ProcessOutcomeGroundingValidator;
using static CanDoItAll.Modules.Processes.ProcessSubprocessCompletionPolicy;
using static CanDoItAll.Modules.Processes.ProcessSubprocessState;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessSubprocessCoordinator(
    IEnumerable<IProcessSubprocessLaunchCoordinator> subprocessLaunchCoordinators,
    IParentSubprocessArtifactBridge parentSubprocessArtifactBridge,
    ProcessStepCompletionCoordinator completionCoordinator)
{
    private readonly IReadOnlyList<IProcessSubprocessLaunchCoordinator> subprocessLaunchCoordinators = subprocessLaunchCoordinators.ToArray();
    internal async ValueTask<ProcessExecutionAdapterResult?> TryLaunchMappedSubprocessAsync(
        ProcessRuntimeStepAssignment assignment,
        IProcessRuntimeStepAssignmentStore assignmentStore,
        IProcessRuntimeStateStore stateStore,
        CancellationToken cancellationToken)
    {
        if (!RequiresSubprocessLaunch(assignment))
        {
            return null;
        }

        if (!ProcessRuntimeLaunchVariables.TryReadProcessStepSubprocessDefinitionKey(
                assignment.LaunchVariables,
                out var subprocessDefinitionKey))
        {
            var issue = CreateSubprocessLaunchDefinitionMissingIssue(assignment);
            return NeedsManagerForCompletionIssue(
                assignment,
                ComputeHash(issue.Evidence),
                issue);
        }

        if (subprocessLaunchCoordinators.FirstOrDefault() is not { } coordinator)
        {
            var issue = CreateSubprocessLaunchCoordinatorUnavailableIssue(assignment, subprocessDefinitionKey);
            return NeedsManagerForCompletionIssue(
                assignment,
                ComputeHash(issue.Evidence),
                issue);
        }

        var launch = await coordinator
            .TryLaunchAsync(
                new ProcessSubprocessLaunchCoordinatorRequest(
                    assignment,
                    subprocessDefinitionKey),
                cancellationToken)
            .ConfigureAwait(false);
        if (launch is null)
        {
            var issue = CreateSubprocessLaunchNotHandledIssue(assignment, subprocessDefinitionKey);
            return NeedsManagerForCompletionIssue(
                assignment,
                ComputeHash(issue.Evidence),
                issue);
        }

        if (launch.ChildRunId is { } childRunId &&
            IsActiveSubprocessLaunchStage(launch.Stage))
        {
            throw CreatePendingChildRunDeferredException(assignment, childRunId);
        }

        var rawOutputHash = ComputeHash(
            $"{assignment.RunId}:{assignment.StepInstanceId}:coordinated-subprocess-launch:{launch.DefinitionKey}:{launch.ChildRunId}:{launch.Stage}:{launch.ParentDeferredOutcomeJson}");
        if (string.IsNullOrWhiteSpace(launch.ParentDeferredOutcomeJson))
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                CreateSubprocessLaunchCoordinatorMissingOutcomeIssue(assignment, launch));
        }

        var validation = AgentOutputJson.DeserializeAndValidate(
            launch.ParentDeferredOutcomeJson,
            new ProcessStepOutcomeValidator());
        if (!validation.Succeeded || validation.Output is null)
        {
            return Failed(
                "process.adapter.subprocess_launch_output_invalid",
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

        var subprocessLaunchReceipt = CreateCoordinatedSubprocessLaunchReceipt(assignment, launch);
        var materialization = completionCoordinator.Materialize(
            assignment,
            validation.Output,
            subprocessLaunchReceipt.ExecutionRunId,
            [subprocessLaunchReceipt]);
        if (materialization.Issue is { } materializationIssue)
        {
            return NeedsManagerForCompletionIssue(assignment, validation.RawOutputHash, materializationIssue);
        }

        return completionCoordinator.Complete(
            assignment,
            materialization,
            validation.RawOutputHash,
            subprocessLaunchReceipt.ExecutionRunId,
            materialization.ToolReceipts);
    }

    internal static bool IsActiveSubprocessLaunchStage(string stage)
    {
        return !Enum.TryParse<ProcessLaunchStage>(stage, ignoreCase: true, out var parsed) ||
               parsed is ProcessLaunchStage.Running or ProcessLaunchStage.Planned;
    }

    internal async ValueTask<ProcessExecutionAdapterResult?> TryResolveExistingSubprocessBridgeAsync(
        ProcessRuntimeStepAssignment assignment,
        CancellationToken cancellationToken)
    {
        var result = await parentSubprocessArtifactBridge
            .ResolveExistingAsync(assignment, cancellationToken)
            .ConfigureAwait(false);
        return TranslateSubprocessBridgeResult(assignment, result);
    }

    private ProcessExecutionAdapterResult? TranslateSubprocessBridgeResult(
        ProcessRuntimeStepAssignment assignment,
        ParentSubprocessArtifactBridgeResult result)
    {
        return result.Kind switch
        {
            ParentSubprocessArtifactBridgeResultKind.NotSubprocess or
                ParentSubprocessArtifactBridgeResultKind.NoMatchingChildRun => null,
            ParentSubprocessArtifactBridgeResultKind.ChildActive when result.ChildRunId is { } childRunId =>
                throw CreatePendingChildRunDeferredException(assignment, childRunId),
            ParentSubprocessArtifactBridgeResultKind.AcceptedChildOutputBridged when result.AcceptedOutcome is { } acceptedOutcome =>
                CompleteSynthesizedSubprocessOutcome(assignment, acceptedOutcome),
            ParentSubprocessArtifactBridgeResultKind.ContractMissing =>
                NeedsManagerForCompletionIssue(
                    assignment,
                    ComputeHash($"{assignment.RunId}:{assignment.StepInstanceId}:subprocess-contract-missing"),
                    CreateSubprocessContractMissingIssue(assignment)),
            ParentSubprocessArtifactBridgeResultKind.NoGoChildOutputFound when result.ChildRunId is { } childRunId =>
                BuildSubprocessBridgeIssueResult(
                    assignment,
                    CreateSubprocessChildNoGoIssue(assignment, childRunId, result.EvidenceRefs)),
            ParentSubprocessArtifactBridgeResultKind.ChildCompletedWithoutAcceptedOutput
                when result.ChildRunId is { } childRunId && result.Contract is { } contract =>
                BuildSubprocessBridgeIssueResult(
                    assignment,
                    CreateSubprocessChildAcceptedOutputMissingIssue(assignment, childRunId, contract)),
            ParentSubprocessArtifactBridgeResultKind.ChildStoppedBlocked
                when result.ChildRunId is { } childRunId && result.StoppedChild is { } stoppedChild =>
                BuildSubprocessBridgeIssueResult(
                    assignment,
                    CreateSubprocessChildStoppedIssue(assignment, childRunId, stoppedChild, failed: false)),
            ParentSubprocessArtifactBridgeResultKind.ChildStoppedFailed
                when result.ChildRunId is { } childRunId && result.StoppedChild is { } stoppedChild =>
                BuildSubprocessBridgeIssueResult(
                    assignment,
                    CreateSubprocessChildStoppedIssue(assignment, childRunId, stoppedChild, failed: true)),
            _ => null
        };
    }

    private ProcessExecutionAdapterResult BuildSubprocessBridgeIssueResult(
        ProcessRuntimeStepAssignment assignment,
        ProcessCompletionIssue issue)
        => NeedsManagerForCompletionIssue(
            assignment,
            ComputeHash(issue.Evidence),
            issue);

    private ProcessExecutionAdapterResult CompleteSynthesizedSubprocessOutcome(
        ProcessRuntimeStepAssignment assignment,
        ParentSubprocessBridgedOutcome completedChildOutcome)
    {
        var materialization = completionCoordinator.Materialize(
            assignment,
            completedChildOutcome.Output,
            completedChildOutcome.SyntheticExecutionRunId,
            completedChildOutcome.ToolReceipts);
        if (materialization.Issue is { } materializationIssue)
        {
            return NeedsManagerForCompletionIssue(assignment, completedChildOutcome.RawOutputHash, materializationIssue);
        }

        return completionCoordinator.Complete(
            assignment,
            materialization,
            completedChildOutcome.RawOutputHash,
            completedChildOutcome.SyntheticExecutionRunId,
            materialization.ToolReceipts);
    }

    internal async ValueTask<ProcessExecutionAdapterResult?> TryResolveDeferredOrCompletedSubprocessOutputAsync(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IProcessRuntimeStepAssignmentStore assignmentStore,
        IProcessRuntimeStateStore stateStore,
        CancellationToken cancellationToken)
    {
        var bridgeResult = await parentSubprocessArtifactBridge
            .ResolveFromOutputAsync(assignment, output, cancellationToken)
            .ConfigureAwait(false);
        return TranslateSubprocessBridgeResult(assignment, bridgeResult);
    }

}
