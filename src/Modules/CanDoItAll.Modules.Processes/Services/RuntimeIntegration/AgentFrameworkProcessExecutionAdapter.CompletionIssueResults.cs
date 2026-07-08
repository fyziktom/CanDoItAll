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

internal sealed partial class AgentFrameworkProcessExecutionAdapter
{
    private static ProcessExecutionAdapterResult NeedsManagerForCompletionIssue(
        ProcessRuntimeStepAssignment assignment,
        string rawOutputHash,
        ProcessCompletionIssue issue)
    {
        return NeedsManagerForCompletionIssues(
            assignment,
            rawOutputHash,
            new ProcessCompletionGateEvaluation([issue]));
    }

    private static ProcessExecutionAdapterResult NeedsManagerForCompletionIssues(
        ProcessRuntimeStepAssignment assignment,
        string rawOutputHash,
        ProcessCompletionGateEvaluation evaluation)
    {
        if (evaluation.IsSatisfied)
        {
            throw new InvalidOperationException("Completion issue result requires at least one completion gate issue.");
        }

        var issues = evaluation.OrderedIssues;
        var primaryIssue = issues[0];
        var requestedArtifactSlots = ResolveRequestedArtifactSlots(assignment, issues);
        return new ProcessExecutionAdapterResult(
            StrategyOutcome.NeedsManager,
            [],
            requestedArtifactSlots
                .Select(slotId => new RequestedArtifactRef(
                    slotId,
                    ComputeHash($"{rawOutputHash}:requested:{slotId}:{primaryIssue.Code}")))
                .ToArray(),
            issues
                .Select(issue => new ProcessExecutionAdapterDiagnostic(
                    new StrategyDiagnosticCode(issue.Code),
                    StrategyDiagnosticSensitivity.Normal,
                    ComputeHash(issue.Evidence),
                    issue.Summary,
                    RestrictedEvidenceReference: null,
                    issue.RetrySafety,
                    issue.Idempotency))
                .ToArray(),
            issues
                .Select(issue => new ManagerSignal(
                    new ManagerSignalCode(issue.Code),
                    ComputeHash($"{rawOutputHash}:manager:{issue.Code}:{issue.Evidence}"),
                    issue.Summary))
                .ToArray(),
            BuildCompletionGateSummary(issues),
            ComputeHash($"{rawOutputHash}:completion-gates:{string.Join("|", issues.Select(issue => $"{issue.Code}:{issue.Evidence}"))}"));
    }

    private static IReadOnlyList<ArtifactSlotId> ResolveRequestedArtifactSlots(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<ProcessCompletionIssue> issues)
    {
        var requestedArtifactSlots = issues
            .SelectMany(issue => issue.RequestedArtifactSlotIds)
            .Distinct()
            .ToArray();
        if (requestedArtifactSlots.Length > 0)
        {
            return requestedArtifactSlots;
        }

        return assignment.ProducedArtifactSlotIds.Count > 0
            ? assignment.ProducedArtifactSlotIds
            : assignment.RequiredArtifactSlotIds;
    }

    private static string BuildCompletionGateSummary(IReadOnlyList<ProcessCompletionIssue> issues)
    {
        if (issues.Count == 1)
        {
            return issues[0].Summary;
        }

        var primaryIssue = issues[0];
        var secondarySummaries = issues
            .Skip(1)
            .Select(issue => $"- {issue.Code}: {issue.Summary}");
        return $"Completion gates are unsatisfied. Primary issue: {primaryIssue.Summary}{Environment.NewLine}{Environment.NewLine}Additional completion gate issue(s):{Environment.NewLine}{string.Join(Environment.NewLine, secondarySummaries)}";
    }
}
