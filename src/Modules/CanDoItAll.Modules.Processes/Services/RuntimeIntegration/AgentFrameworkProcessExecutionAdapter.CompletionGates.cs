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
    private static ProcessCompletionGateEvaluation EvaluateCompletionGates(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        Guid? currentExecutionRunId)
    {
        var issues = new List<ProcessCompletionIssue>();
        AddIssue(issues, ValidateGroundedOutcomeReferences(assignment, output, toolReceipts));
        AddIssue(issues, ValidateProductMutationCompletion(assignment, output));
        AddIssue(issues, ValidateProductMutationWriteReceipt(assignment, output, toolReceipts));
        AddIssue(issues, ValidateRequiredProductToolReceipts(assignment, toolReceipts));
        AddIssue(issues, ValidateRequiredProcessToolReceipts(assignment, toolReceipts, currentExecutionRunId));
        AddIssue(issues, ValidateRequiredProductStateCompletion(assignment, output));
        AddIssue(issues, ValidateCompletedOutcomeDoesNotDeclareBlockers(assignment, output));
        AddIssue(issues, ValidateManagedArtifactCompletion(assignment, output));
        AddIssue(issues, ValidateManagedArtifactWriteReceipt(assignment, toolReceipts));

        return new ProcessCompletionGateEvaluation(issues);
    }

    private static void AddIssue(List<ProcessCompletionIssue> issues, ProcessCompletionIssue? issue)
    {
        if (issue is not null &&
            !issues.Any(existing =>
                string.Equals(existing.Code, issue.Code, StringComparison.Ordinal) &&
                string.Equals(existing.Evidence, issue.Evidence, StringComparison.Ordinal)))
        {
            issues.Add(issue);
        }
    }

    private static IReadOnlyList<ProcessCompletionIssue> OrderCompletionGateIssues(
        IReadOnlyList<ProcessCompletionIssue> issues)
    {
        return issues
            .OrderBy(GetCompletionGateIssuePriority)
            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(issue => issue.Evidence, StringComparer.Ordinal)
            .ToArray();
    }

    private static int GetCompletionGateIssuePriority(ProcessCompletionIssue issue)
    {
        if (issue.RetrySafety == ProcessDiagnosticRetrySafety.UnsafeToRetry)
        {
            return 0;
        }

        return issue.Code switch
        {
            "process.adapter.product_required_tool_receipt_missing" => 10,
            "process.adapter.required_tool_receipt_missing" => 11,
            "process.adapter.product_mutation_receipt_missing" => 12,
            "process.adapter.product_required_file_content_missing" => 20,
            "process.adapter.product_required_file_content_check_invalid" => 21,
            "process.adapter.product_required_output_missing" => 30,
            "process.adapter.product_required_output_path_invalid" => 31,
            "process.adapter.product_output_missing" => 32,
            "process.adapter.product_output_evidence_missing" => 33,
            "process.adapter.produced_artifact_evidence_missing" => 40,
            "process.adapter.produced_artifact_write_receipt_missing" => 41,
            "process.adapter.managed_artifact_materialization_failed" => 42,
            "process.adapter.managed_artifact_outcome_append_failed" => 43,
            "process.adapter.managed_artifact_acceptance_append_failed" => 44,
            "process.adapter.managed_artifact_readback_failed" => 45,
            "process.adapter.ungrounded_outcome_reference" => 50,
            "process.adapter.ungrounded_managed_artifact_reference" => 51,
            "process.adapter.completed_outcome_declares_unresolved_blocker" => 60,
            _ => 100
        };
    }
}
