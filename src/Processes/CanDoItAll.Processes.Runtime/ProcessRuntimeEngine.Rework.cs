using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public sealed partial class ProcessRuntimeEngine
{
    private static ProcessRuntimeMutation RequestStepRework(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        RequestStepReworkCommand command)
    {
        ValidateArguments(state, context);
        ArgumentNullException.ThrowIfNull(command);

        var step = FindStep(state, command.StepInstanceId);
        if (step is null)
        {
            return ProcessRuntimeMutation.Rejected(
                state,
                "Runtime.StepMissing",
                $"Step '{command.StepInstanceId}' was not found in run '{state.RunId}'.");
        }

        if (!step.IsExecutable)
        {
            return ProcessRuntimeMutation.Rejected(
                state,
                "Runtime.StepNotExecutable",
                "Only executable process steps can be requested for rework.");
        }

        var blockedRecoveryAuthorizationIssue = command.BlockedRecoveryAuthorization is null
            ? null
            : ProcessRuntimeBlockedRecoveryAuthorizationRules.FindIssue(
                state,
                step.StepInstanceId,
                command.BlockedRecoveryAuthorization);
        if (blockedRecoveryAuthorizationIssue is not null)
        {
            return ProcessRuntimeMutation.Rejected(
                state,
                "Runtime.BlockedRecoveryAuthorizationRejected",
                blockedRecoveryAuthorizationIssue);
        }

        if (state.Status is ProcessRuntimeStatus.Completed or ProcessRuntimeStatus.Cancelled)
        {
            return ProcessRuntimeMutation.Rejected(
                state,
                "Runtime.TerminalRunImmutable",
                $"Run '{state.RunId}' is terminal and cannot be reworked.");
        }

        if (step.Status == ProcessRuntimeStepStatus.Ready &&
            state.Status == ProcessRuntimeStatus.Active &&
            step.ActiveClaimToken is null)
        {
            return Duplicate(state);
        }

        if (step.Status is ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running ||
            step.ActiveClaimToken is not null)
        {
            return ProcessRuntimeMutation.Rejected(
                state,
                "Runtime.StepHasActiveClaim",
                "A claimed or running step cannot be reworked until its dispatch claim is released or expired.");
        }

        var completedUpstreamReworkAuthorized =
            step.Status == ProcessRuntimeStepStatus.Completed &&
            command.BlockedRecoveryAuthorization is
            {
                RecoveryRouteKind: ProcessRecoveryRouteKind.UpstreamStepRework,
                Phase: ProcessRuntimeBlockedRecoveryPhase.UpstreamProducer
            };
        if (step.Status == ProcessRuntimeStepStatus.Completed && !completedUpstreamReworkAuthorized)
        {
            return ProcessRuntimeMutation.Rejected(
                state,
                "Runtime.CompletedStepReworkUnauthorized",
                "A completed step can be reworked only by an exact blocked-recovery authorization from the current upstream rework receipt.");
        }

        if (!completedUpstreamReworkAuthorized &&
            step.Status is not (ProcessRuntimeStepStatus.Waiting or ProcessRuntimeStepStatus.Blocked or ProcessRuntimeStepStatus.Failed))
        {
            return ProcessRuntimeMutation.Rejected(
                state,
                "Runtime.StepNotReworkable",
                $"Step status '{step.Status}' is not reworkable.");
        }

        if (step.Status is ProcessRuntimeStepStatus.Waiting or ProcessRuntimeStepStatus.Blocked &&
            !BlockedStepCanBeReworked(state, step))
        {
            return ProcessRuntimeMutation.Rejected(
                state,
                "Runtime.BlockedStepNotActionable",
                "The blocked step still has unresolved dependencies or missing required artifacts; rework the upstream failed or blocked step instead.");
        }

        if (step.Status != ProcessRuntimeStepStatus.Waiting && HasOpenClaims(state))
        {
            return ProcessRuntimeMutation.Rejected(
                state,
                "Runtime.RunHasOpenClaims",
                "Run rework is rejected while any dispatch claim is still open.");
        }

        var reworkedStep = step with
        {
            Status = ProcessRuntimeStepStatus.Ready,
            AttemptNumber = step.Status == ProcessRuntimeStepStatus.Waiting ? step.AttemptNumber : 0,
            ActiveClaimToken = null,
            CompletedResultKey = null
        };
        var next = state with
        {
            Status = ProcessRuntimeStatus.Active,
            Steps = ReplaceStep(state, reworkedStep),
            BlockedRecoveryActions = AppendBlockedRecoveryAction(
                state.BlockedRecoveryActions,
                step.StepInstanceId,
                command.BlockedRecoveryAuthorization,
                context.OccurredAtUtc),
            UpdatedAtUtc = context.OccurredAtUtc
        };

        var events = new List<ProcessRuntimeEventEnvelope>
        {
            CreateEvent(
                next,
                context,
                ProcessRuntimeEventTypes.StepReworkRequested,
                ComputePayloadHash($"rework:{state.RunId}:{step.StepInstanceId}:{command.Reason.Trim()}"))
        };

        if (state.Status != ProcessRuntimeStatus.Active)
        {
            events.Add(CreateEvent(
                next,
                context,
                ProcessRuntimeEventTypes.ProcessRunReactivated,
                ComputePayloadHash($"reactivated:{state.RunId}:{step.StepInstanceId}:{state.Status}")));
        }

        events.Add(CreateEvent(
            next,
            context,
            ProcessRuntimeEventTypes.StepReady,
            step.StepInstanceId.ToString()));

        return Applied(next, events);
    }

    private static bool BlockedStepCanBeReworked(
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepState step)
    {
        return ProcessRuntimeArtifactContracts.DependenciesSatisfied(state, step) &&
               ProcessRuntimeArtifactContracts.RequiredArtifactsAvailable(state, step);
    }

    private static IReadOnlyList<ProcessRuntimeBlockedRecoveryActionReceipt> AppendBlockedRecoveryAction(
        IReadOnlyList<ProcessRuntimeBlockedRecoveryActionReceipt> actions,
        ProcessStepInstanceId targetStepInstanceId,
        ProcessRuntimeBlockedRecoveryAuthorization? authorization,
        DateTimeOffset appliedAtUtc)
    {
        if (authorization is null)
        {
            return actions;
        }

        return
        [
            .. actions,
            new ProcessRuntimeBlockedRecoveryActionReceipt(
                authorization.SourceResultIdempotencyKey,
                authorization.SourceBlockedStepInstanceId,
                targetStepInstanceId,
                authorization.DiagnosticFingerprint,
                authorization.RecoveryRouteKind,
                authorization.Phase,
                appliedAtUtc)
        ];
    }

    private static string ComputePayloadHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
