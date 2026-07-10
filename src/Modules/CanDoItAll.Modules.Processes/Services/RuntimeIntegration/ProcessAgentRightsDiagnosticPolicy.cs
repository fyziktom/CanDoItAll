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

using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionMetadataBuilder;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessAgentRightsDiagnosticPolicy
{
    internal const string AgentRightsManagerRequestCode = "process.adapter.agent_rights_request";

    internal static bool TryBuildAgentRightsManagerRequest(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        out string managerRequest)
    {
        managerRequest = string.Empty;
        var issueText = FirstNonEmpty(
            output.Reason,
            output.HumanReadableSummaryMarkdown ?? string.Empty,
            string.Join(" ", output.NextActions));
        if (!LooksLikeRightsOrToolBoundary(issueText))
        {
            return false;
        }

        var deniedToolOrRight = ResolveDeniedToolOrRight(issueText);
        var operations = NormalizeOperations(assignment.AllowedOperations);
        var operationsSummary = operations.Count == 0
            ? "none declared"
            : string.Join(", ", operations);
        var scope = string.IsNullOrWhiteSpace(assignment.OperationTargetScope)
            ? "unspecified"
            : assignment.OperationTargetScope.Trim();
        var executor = string.IsNullOrWhiteSpace(assignment.ExecutorDisplayName)
            ? assignment.ExecutorId
            : assignment.ExecutorDisplayName.Trim();
        var mutationSummary = AllowsProductMutation(operations, assignment.OperationTargetScope)
            ? "product mutation allowed"
            : "product mutation not allowed";

        managerRequest =
            $"Manager action required: step '{assignment.StepKey}' in run '{assignment.RunId}' is assigned to '{executor}' but reported a tool/right boundary problem for {deniedToolOrRight}. Grant the missing right/tool to this agent or reassign the step to an agent that already has it, then retry the step. Required operation contract: allowed operations [{operationsSummary}], target scope '{scope}', {mutationSummary}.";
        return true;
    }

    internal static bool LooksLikeRightsOrToolBoundary(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsAny(
            text,
            "PolicyDenied",
            "blocked by policy",
            "missing tool",
            "tool is not part of the composed capability set",
            "not authorized to use tool",
            "permission",
            "permissions",
            "right",
            "rights",
            "capability",
            "access denied",
            "workspace boundary",
            "outside the current run boundary",
            "approval path",
            "denied tool");
    }

    internal static string ResolveDeniedToolOrRight(string text)
    {
        var quotedTool = Regex.Match(text, @"Tool '([^']+)'", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (quotedTool.Success)
        {
            return $"tool '{quotedTool.Groups[1].Value}'";
        }

        return "the denied or unavailable tool/right named in the blocker";
    }

    internal static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }
}
