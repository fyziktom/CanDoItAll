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

using static CanDoItAll.Modules.Processes.ProcessCompletionRuleParser;
using static CanDoItAll.Modules.Processes.ProcessProductCompletionRuleParser;
using static CanDoItAll.Modules.Processes.ProcessProductRootResolver;
using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionMetadataBuilder;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactService;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactFormatter;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactOutcomeParser;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessExecutionResultFactory
{
    internal static ProcessExecutionAdapterResult Failed(
        string code,
        string summary,
        string evidence)
    {
        return new ProcessExecutionAdapterResult(
            StrategyOutcome.Failed,
            [],
            [],
            [
                new ProcessExecutionAdapterDiagnostic(
                    new StrategyDiagnosticCode(code),
                    StrategyDiagnosticSensitivity.Normal,
                    ComputeHash(evidence),
                    summary,
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.Unknown,
                    ProcessDiagnosticIdempotencyClassification.Unknown)
            ],
            [],
            summary,
            ComputeHash($"{code}:{evidence}"));
    }

    internal static ProcessExecutionAdapterResult Canceled(
        string code,
        string summary,
        string evidence)
    {
        var evidenceHash = ComputeHash(evidence);
        return new ProcessExecutionAdapterResult(
            StrategyOutcome.Canceled,
            [],
            [],
            [
                new ProcessExecutionAdapterDiagnostic(
                    new StrategyDiagnosticCode(code),
                    StrategyDiagnosticSensitivity.Normal,
                    evidenceHash,
                    summary,
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.UnsafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            [],
            summary,
            ComputeHash($"{code}:{evidence}"));
    }

    internal static ProcessExecutionAdapterResult NeedsManager(
        string code,
        string summary,
        string evidence)
    {
        var evidenceHash = ComputeHash(evidence);
        return new ProcessExecutionAdapterResult(
            StrategyOutcome.NeedsManager,
            [],
            [],
            [
                new ProcessExecutionAdapterDiagnostic(
                    new StrategyDiagnosticCode(code),
                    StrategyDiagnosticSensitivity.Normal,
                    evidenceHash,
                    summary,
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.UnsafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            [
                new ManagerSignal(
                    new ManagerSignalCode(code),
                    evidenceHash,
                    summary)
            ],
            summary,
            ComputeHash($"{code}:{evidence}"));
    }

    internal static AgentProcessRoleReadinessRequest CreateRuntimeReadinessRequest(ProcessRuntimeStepAssignment assignment)
    {
        return new AgentProcessRoleReadinessRequest(
            assignment.StepKey,
            assignment.StepKey,
            assignment.RoleKey,
            assignment.RoleResourceKey,
            assignment.RoleDisplayName,
            NormalizeOperations(assignment.AllowedOperations),
            assignment.OperationTargetScope,
            ResolveRuntimeReadinessRequiredToolNames(assignment),
            PreferredSpecializationTags: ProcessExecutorSpecializationPolicy.Resolve(assignment.LaunchVariables));
    }

    internal static IReadOnlyList<string> ResolveRuntimeReadinessRequiredToolNames(ProcessRuntimeStepAssignment assignment)
    {
        var launchContextToolNames = ProcessRequiredRuntimeToolNames
            .FromProductCompletionRequiredToolReceipts(ResolveProductCompletionRequiredToolReceipts(
                assignment.LaunchVariables,
                assignment.StepKey))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return launchContextToolNames
            .Concat(ProcessRequiredRuntimeToolNames.FromCapabilityScope(assignment.CapabilityScope, launchContextToolNames))
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(toolName => toolName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static string FormatValidationErrors(IReadOnlyList<AgentOutputValidationError> errors)
    {
        if (errors.Count == 0)
        {
            return "Agent output did not satisfy the process step outcome contract.";
        }

        return string.Join("; ", errors.Select(error => $"{error.Code}: {error.Message}"));
    }

    internal static string ComputeHash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    internal static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

}
