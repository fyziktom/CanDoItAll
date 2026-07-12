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

using static CanDoItAll.Modules.Processes.ProcessAgentRightsDiagnosticPolicy;
using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionMetadataBuilder;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactEvidence;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactService;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessManagedArtifactFormatter
{
    internal static string BuildManagedOutcomeArtifactContent(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        string primaryRef)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {assignment.StepKey} Process Step Outcome");
        builder.AppendLine();
        builder.AppendLine(ManagedOutcomeArtifactCapturedHeading);
        builder.AppendLine();
        builder.AppendLine("The process runtime captured this managed artifact from a schema-valid structured process step outcome. Completion gates have not accepted this output yet.");
        builder.AppendLine();
        builder.AppendLine($"- Run id: {assignment.RunId.Value:D}");
        builder.AppendLine($"- Step id: {assignment.StepInstanceId.Value:D}");
        builder.AppendLine($"- Step key: {assignment.StepKey}");
        builder.AppendLine($"- Executor: {assignment.ExecutorDisplayName}");
        builder.AppendLine($"- Status: {output.Status}");
        builder.AppendLine($"- Primary managed ref: {primaryRef}");
        builder.AppendLine($"- Staged at UTC: {DateTimeOffset.UtcNow:u}");
        builder.AppendLine();
        builder.AppendLine("## Reason");
        builder.AppendLine();
        builder.AppendLine(output.Reason.Trim());
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(output.BranchOutcomeKey) ||
            !string.IsNullOrWhiteSpace(output.BranchOutcomeTitle))
        {
            builder.AppendLine("## Branch Outcome");
            builder.AppendLine();
            if (!string.IsNullOrWhiteSpace(output.BranchOutcomeKey))
            {
                builder.AppendLine($"- Key: {output.BranchOutcomeKey.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(output.BranchOutcomeTitle))
            {
                builder.AppendLine($"- Title: {output.BranchOutcomeTitle.Trim()}");
            }

            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(output.HumanReadableSummaryMarkdown))
        {
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.AppendLine(output.HumanReadableSummaryMarkdown.Trim());
            builder.AppendLine();
        }

        AppendList(builder, "Agent Evidence Refs", output.EvidenceRefs);
        AppendAcceptanceCriteriaEvidence(builder, "Acceptance Criteria Evidence", output.AcceptanceCriteriaEvidence);
        AppendList(builder, "Next Actions", output.NextActions);
        return builder.ToString();
    }

    internal static string BuildManagedOutcomeArtifactAppendixContent(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        string primaryRef)
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine(ManagedOutcomeArtifactCapturedHeading);
        builder.AppendLine();
        builder.AppendLine("The process runtime appended this section after capturing a schema-valid structured process step outcome. Completion gates must pass before this artifact is accepted as a produced slot.");
        builder.AppendLine();
        builder.AppendLine($"- Run id: {assignment.RunId.Value:D}");
        builder.AppendLine($"- Step id: {assignment.StepInstanceId.Value:D}");
        builder.AppendLine($"- Step key: {assignment.StepKey}");
        builder.AppendLine($"- Executor: {assignment.ExecutorDisplayName}");
        builder.AppendLine($"- Status: {output.Status}");
        builder.AppendLine($"- Primary managed ref: {primaryRef}");
        builder.AppendLine($"- Appended at UTC: {DateTimeOffset.UtcNow:u}");
        builder.AppendLine();
        builder.AppendLine("### Reason");
        builder.AppendLine();
        builder.AppendLine(output.Reason.Trim());
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(output.BranchOutcomeKey) ||
            !string.IsNullOrWhiteSpace(output.BranchOutcomeTitle))
        {
            builder.AppendLine("### Branch Outcome");
            builder.AppendLine();
            if (!string.IsNullOrWhiteSpace(output.BranchOutcomeKey))
            {
                builder.AppendLine($"- Key: {output.BranchOutcomeKey.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(output.BranchOutcomeTitle))
            {
                builder.AppendLine($"- Title: {output.BranchOutcomeTitle.Trim()}");
            }

            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(output.HumanReadableSummaryMarkdown))
        {
            builder.AppendLine("### Summary");
            builder.AppendLine();
            builder.AppendLine(output.HumanReadableSummaryMarkdown.Trim());
            builder.AppendLine();
        }

        AppendList(builder, "Agent Evidence Refs", output.EvidenceRefs);
        AppendAcceptanceCriteriaEvidence(builder, "Acceptance Criteria Evidence", output.AcceptanceCriteriaEvidence);
        AppendList(builder, "Next Actions", output.NextActions);
        return builder.ToString();
    }

    internal static string BuildManagedOutcomeArtifactAcceptanceContent(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        string primaryRef)
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine(ManagedOutcomeArtifactAcceptedHeading);
        builder.AppendLine();
        builder.AppendLine("The process runtime appended this section after all completion gates accepted the staged structured outcome. Produced artifact slots may now be promoted to parent and consumer contexts.");
        builder.AppendLine();
        builder.AppendLine($"- Run id: {assignment.RunId.Value:D}");
        builder.AppendLine($"- Step id: {assignment.StepInstanceId.Value:D}");
        builder.AppendLine($"- Step key: {assignment.StepKey}");
        builder.AppendLine($"- Status: {output.Status}");
        builder.AppendLine($"- Primary managed ref: {primaryRef}");
        builder.AppendLine($"- Accepted at UTC: {DateTimeOffset.UtcNow:u}");
        builder.AppendLine();
        AppendList(builder, "Accepted Produced Artifact Slots", assignment.ProducedArtifactSlotIds.Select(slotId => slotId.Value.ToString("D")).ToArray());
        return builder.ToString();
    }

    internal static void AppendList(
        StringBuilder builder,
        string heading,
        IReadOnlyList<string> values)
    {
        var items = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();
        if (items.Length == 0)
        {
            return;
        }

        builder.AppendLine($"## {heading}");
        builder.AppendLine();
        foreach (var item in items)
        {
            builder.AppendLine($"- {item}");
        }

        builder.AppendLine();
    }

    internal static void AppendAcceptanceCriteriaEvidence(
        StringBuilder builder,
        string heading,
        IReadOnlyList<ProcessAcceptanceCriterionEvidence> evidence)
    {
        var items = (evidence ?? [])
            .Where(item => item is not null && !string.IsNullOrWhiteSpace(item.CriterionId))
            .ToArray();
        if (items.Length == 0)
        {
            return;
        }

        builder.AppendLine($"## {heading}");
        builder.AppendLine();
        builder.AppendLine("| Criterion | Status | Summary | Evidence refs |");
        builder.AppendLine("| --- | --- | --- | --- |");
        foreach (var item in items)
        {
            var refs = string.Join(
                "; ",
                (item.EvidenceRefs ?? [])
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Select(reference => reference.Trim()));
            builder.AppendLine($"| {EscapeTableCell(item.CriterionId)} | {item.Status} | {EscapeTableCell(item.Summary)} | {EscapeTableCell(refs)} |");
        }

        builder.AppendLine();
    }

    private static string EscapeTableCell(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value
                .Replace("|", "\\|", StringComparison.Ordinal)
                .ReplaceLineEndings(" ")
                .Trim();

    internal static ToolExecutionReceiptRecord CreateManagedOutcomeArtifactReceipt(
        Guid executionRunId,
        string primaryRef,
        string writeMessage,
        string toolName = "workspace_write_file",
        string requestSummary = "Process runtime staged schema-valid structured step outcome.")
        => new(
            Guid.NewGuid(),
            executionRunId,
            "process-runtime",
            toolName,
            "ManagedProcessArtifact",
            "NotRequired",
            requestSummary,
            primaryRef,
            ".",
            $"Succeeded: {writeMessage}",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
}
