using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static string ResolveMissingConcreteImplementationProofSummary(
        DispatchCandidate candidate,
        ExecutionRunDetail detail)
    {
        if (!RequiresConcreteImplementationProof(candidate))
        {
            return string.Empty;
        }

        if (ResolveProcessMockArtifactProjections(detail.Run.SerializedSessionStateJson)
            .Any(projection => CanSatisfyConcreteImplementationProofWithProcessMock(candidate, projection)))
        {
            return string.Empty;
        }

        var successfulReceipts = detail.ToolReceipts
            .Where(receipt => !IsFailedToolReceipt(receipt))
            .ToList();
        var concreteReadReceipt = ResolveLatestReceipt(
            successfulReceipts,
            "workspace_read_file",
            requireConcreteProductPath: true,
            requireConcreteDeliverableOrSourcePath: true);
        if (concreteReadReceipt is null)
        {
            return "the current attempt did not read any concrete product deliverable, source, or project file";
        }

        var concreteMutationReceipts = successfulReceipts
            .Where(receipt => IsConcreteProductMutationToolName(NormalizeToolToken(receipt.ToolName)))
            .Where(IsConcreteProductMutationReceipt)
            .ToList();

        var latestMutationReceipt = concreteMutationReceipts
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
        if (latestMutationReceipt is not null)
        {
            if (IsReceiptAfter(latestMutationReceipt, concreteReadReceipt))
            {
                return "workspace_read_file ran before the latest concrete product mutation";
            }

            var latestBootstrapReceipt = concreteMutationReceipts
                .Where(receipt => IsImplementationBootstrapToolName(NormalizeToolToken(receipt.ToolName)))
                .OrderByDescending(receipt => receipt.CompletedAtUtc)
                .ThenByDescending(receipt => receipt.StartedAtUtc)
                .FirstOrDefault();
            if (latestBootstrapReceipt is not null &&
                !successfulReceipts.Any(receipt =>
                    ConcreteProductSourceWriteToolNames.Contains(NormalizeToolToken(receipt.ToolName)) &&
                    IsReceiptAfter(receipt, latestBootstrapReceipt) &&
                    HasConcreteProductDeliverableOrSourcePath(receipt)))
            {
                return "the latest scaffold or bootstrap tool was not followed by a concrete product deliverable, source, or project file write";
            }

            var latestValidationReceipt = ResolveLatestRequiredImplementationValidationReceipt(
                candidate,
                successfulReceipts);
            if (latestValidationReceipt is not null &&
                IsReceiptAfter(latestMutationReceipt, latestValidationReceipt))
            {
                return $"{latestValidationReceipt.ToolName} ran before the latest concrete product mutation";
            }
        }

        return string.Empty;
    }

    private static ToolExecutionReceiptRecord? ResolveLatestRequiredImplementationValidationReceipt(
        DispatchCandidate candidate,
        IReadOnlyList<ToolExecutionReceiptRecord> successfulReceipts)
    {
        var requiredToolNames = ResolveRequiredToolNames(candidate)
            .ToHashSet(StringComparer.Ordinal);
        if (requiredToolNames.Count == 0)
        {
            return null;
        }

        return successfulReceipts
            .Where(receipt =>
            {
                var normalizedToolName = NormalizeToolToken(receipt.ToolName);
                return requiredToolNames.Contains(normalizedToolName) &&
                       IsImplementationValidationToolName(normalizedToolName);
            })
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
    }

    private static ToolExecutionReceiptRecord? ResolveLatestReceipt(
        IEnumerable<ToolExecutionReceiptRecord> receipts,
        string normalizedToolName,
        bool requireConcreteProductPath,
        bool requireConcreteDeliverableOrSourcePath)
    {
        return receipts
            .Where(receipt => string.Equals(NormalizeToolToken(receipt.ToolName), normalizedToolName, StringComparison.Ordinal))
            .Where(receipt => !requireConcreteProductPath || HasConcreteProductPath(receipt))
            .Where(receipt => !requireConcreteDeliverableOrSourcePath || HasConcreteProductDeliverableOrSourcePath(receipt))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
    }

    private static bool IsConcreteProductMutationReceipt(ToolExecutionReceiptRecord receipt)
    {
        var toolName = NormalizeToolToken(receipt.ToolName);
        if (string.Equals(toolName, "workspace_write_file", StringComparison.Ordinal) ||
            string.Equals(toolName, "workspace_append_file", StringComparison.Ordinal))
        {
            return HasConcreteProductDeliverableOrSourcePath(receipt);
        }

        return HasConcreteProductPath(receipt);
    }

    private static bool IsConcreteProductMutationToolName(string normalizedToolName)
    {
        return ConcreteProductMutationToolNames.Contains(normalizedToolName) ||
               IsImplementationBootstrapToolName(normalizedToolName);
    }

    private static bool IsImplementationBootstrapToolName(string normalizedToolName)
    {
        return normalizedToolName.StartsWith("workspace_", StringComparison.Ordinal) &&
               normalizedToolName.EndsWith("_new", StringComparison.Ordinal);
    }

    private static bool IsImplementationValidationToolName(string normalizedToolName)
    {
        return normalizedToolName.EndsWith("_build", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_test", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_run", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_publish", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_validate", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_lint", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_check", StringComparison.Ordinal) ||
               normalizedToolName.StartsWith("browser_", StringComparison.Ordinal);
    }

    private static bool HasConcreteProductPath(ToolExecutionReceiptRecord receipt)
    {
        return ResolveWorkspacePathsFromToolRequest(receipt.RequestSummary)
            .Any(IsConcreteProductPath);
    }

    private static bool HasConcreteProductDeliverableOrSourcePath(ToolExecutionReceiptRecord receipt)
    {
        return ResolveWorkspacePathsFromToolRequest(receipt.RequestSummary)
            .Any(IsConcreteProductDeliverableOrSourcePath);
    }

    private static IReadOnlyList<string> ResolveWorkspacePathsFromToolRequest(string requestSummary)
    {
        if (string.IsNullOrWhiteSpace(requestSummary))
        {
            return [];
        }

        var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in WorkspacePathInToolRequestRegex.Matches(requestSummary))
        {
            var candidatePath = match.Groups["path"].Value;
            if (TryMapWorkspacePathForPrompt(candidatePath, out var promptPath))
            {
                paths.Add(promptPath);
            }
        }

        return paths.ToList();
    }

    private static bool TryMapWorkspacePathForPrompt(string path, out string promptPath)
    {
        promptPath = string.Empty;
        var normalized = path.Trim().TrimEnd(',', ';', '.', ')', ']', '}').Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (normalized.StartsWith($"{ExternalTargetAliasRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            promptPath = normalized;
            return true;
        }

        if (normalized.Length < 3 || !char.IsLetter(normalized[0]) || normalized[1] != ':' || normalized[2] != '/')
        {
            return false;
        }

        var driveLetter = char.ToUpperInvariant(normalized[0]);
        var remainder = normalized.Length == 3
            ? string.Empty
            : normalized[3..].Trim('/');
        promptPath = string.IsNullOrWhiteSpace(remainder)
            ? $"{ExternalTargetAliasRoot}/{driveLetter}"
            : $"{ExternalTargetAliasRoot}/{driveLetter}/{remainder}";
        return true;
    }

    private static bool IsConcreteProductDeliverableOrSourcePath(string promptPath)
    {
        if (!IsConcreteProductPath(promptPath))
        {
            return false;
        }

        var extension = Path.GetExtension(promptPath);
        return IsImplementationDeliverableOrSourceExtension(extension);
    }

    private static bool IsImplementationDeliverableOrSourceExtension(string extension)
    {
        return IsCodeOrProjectExtension(extension) ||
               extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".docx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".xls", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tsv", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".pptx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".ppt", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConcreteProductPath(string promptPath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length > 0 &&
               !IsManagedRootSegment(segments[0]) &&
               !segments.Any(IsNonProductPathSegment);
    }

    private static bool IsNonProductPathSegment(string segment)
    {
        return IsManagedRootSegment(segment) ||
               string.Equals(segment, ".playwright-mcp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReceiptAfter(ToolExecutionReceiptRecord candidate, ToolExecutionReceiptRecord baseline)
    {
        return candidate.CompletedAtUtc > baseline.CompletedAtUtc ||
               candidate.CompletedAtUtc == baseline.CompletedAtUtc &&
               candidate.StartedAtUtc > baseline.StartedAtUtc;
    }
}
