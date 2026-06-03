using System.Diagnostics;
using System.Text.Json;
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
using System.Text;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private void CleanupKeptAliveDotnetRunProcesses(
        DispatchCandidate candidate,
        ExecutionRunDetail detail)
    {
        var startupReceipts = ResolveKeptAliveDotnetRunStartupReceipts(detail);
        if (startupReceipts.Count == 0)
        {
            return;
        }

        foreach (var receipt in startupReceipts)
        {
            var stoppedProcessIds = StopRecordedProcessTree(receipt.AppProcessTreeIds);
            var aliasCleanupResults = DismountStaticWebAssetsAliasMappings(receipt.StaticWebAssetsAliasMappings);
            var dismountedAliasDrives = aliasCleanupResults
                .Where(result => result.Status == StaticWebAssetsAliasCleanupStatus.Dismounted)
                .Select(result => result.Mapping.Drive)
                .ToArray();
            LogStaticWebAssetsAliasCleanupIssues(candidate, detail, aliasCleanupResults);

            if (stoppedProcessIds.Count > 0 || dismountedAliasDrives.Length > 0)
            {
                logger.LogInformation(
                    "Cleaned kept-alive workspace_dotnet_run runtime resources for process run {RunId}, step {StepRunId}, execution run {ExecutionRunId}. ProcessIds={ProcessIds}; StaticWebAssetsAliases={StaticWebAssetsAliases}",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    detail.Run.Id,
                    string.Join(",", stoppedProcessIds),
                    string.Join(",", dismountedAliasDrives));
            }
        }
    }

    private IReadOnlyList<DotnetRunStartupReceipt> ResolveKeptAliveDotnetRunStartupReceipts(ExecutionRunDetail detail)
    {
        var workspaceRoot = workspacePathResolver.ResolveWorkspaceRoot();
        var receipts = new List<DotnetRunStartupReceipt>();
        foreach (var artifact in detail.Artifacts)
        {
            if (!string.Equals(artifact.ProducedBy, "workspace_dotnet_run", StringComparison.OrdinalIgnoreCase) ||
                !artifact.DisplayName.Contains("stdout", StringComparison.OrdinalIgnoreCase) ||
                !TryResolveArtifactFullPath(workspaceRoot, artifact.RelativePath, out var fullPath, out _) ||
                !File.Exists(fullPath))
            {
                continue;
            }

            if (TryReadDotnetRunStartupReceipt(fullPath, out var startupReceipt) &&
                startupReceipt.KeepAlive &&
                !startupReceipt.CleanupAttempted &&
                startupReceipt.HasCleanupTargets &&
                startupReceipt.LifetimeScope == WorkspaceProcessLifetimeScope.ExecutionRun)
            {
                receipts.Add(startupReceipt);
            }
        }

        return receipts;
    }

    private static bool TryReadDotnetRunStartupReceipt(
        string stdoutArtifactPath,
        out DotnetRunStartupReceipt receipt)
    {
        receipt = DotnetRunStartupReceipt.Empty;
        string text;
        try
        {
            text = File.ReadAllText(stdoutArtifactPath, Encoding.UTF8);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var start = text.IndexOf('{', StringComparison.Ordinal);
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(text[start..(end + 1)]);
            var root = document.RootElement;
            var keepAlive = TryReadBoolean(root, "keepAlive");
            if (!keepAlive)
            {
                return false;
            }

            receipt = new DotnetRunStartupReceipt(
                KeepAlive: keepAlive,
                LifetimeScope: TryReadLifetimeScope(root, "lifetimeScope"),
                CleanupAttempted: TryReadBoolean(root, "cleanupAttempted"),
                AppProcessTreeIds: TryReadIntArray(root, "appProcessTreeIds"),
                StaticWebAssetsAliasMappings: TryReadStaticWebAssetsAliasMappings(root, "staticWebAssetsAliasMappings"));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IReadOnlyList<int> StopRecordedProcessTree(IReadOnlyList<int> processIds)
    {
        if (OperatingSystem.IsBrowser())
        {
            return [];
        }

        var stoppedProcessIds = new List<int>();
        foreach (var processId in processIds.Distinct().Reverse())
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5_000);
                stoppedProcessIds.Add(processId);
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
        }

        return stoppedProcessIds;
    }

    private static IReadOnlyList<StaticWebAssetsAliasCleanupResult> DismountStaticWebAssetsAliasMappings(
        IReadOnlyList<StaticWebAssetsAliasMapping> mappings)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var results = new List<StaticWebAssetsAliasCleanupResult>();
        var processedDrives = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in mappings)
        {
            if (!mapping.Mounted)
            {
                results.Add(new StaticWebAssetsAliasCleanupResult(
                    mapping,
                    StaticWebAssetsAliasCleanupStatus.SkippedNotMounted,
                    string.Empty));
                continue;
            }

            var normalizedMapping = TryNormalizeSubstDrive(mapping.Drive, out var normalizedDrive)
                ? mapping with { Drive = normalizedDrive }
                : mapping;
            if (!processedDrives.Add(normalizedMapping.Drive))
            {
                continue;
            }

            var currentTarget = string.IsNullOrWhiteSpace(normalizedDrive)
                ? null
                : ResolveCurrentSubstDriveTarget(normalizedDrive);
            var status = ClassifyStaticWebAssetsAliasCleanup(normalizedMapping, currentTarget);
            if (status != StaticWebAssetsAliasCleanupStatus.ReadyToDismount)
            {
                results.Add(new StaticWebAssetsAliasCleanupResult(normalizedMapping, status, string.Empty));
                continue;
            }

            if (TryDismountSubstDrive(normalizedDrive, out var failureMessage))
            {
                results.Add(new StaticWebAssetsAliasCleanupResult(
                    normalizedMapping,
                    StaticWebAssetsAliasCleanupStatus.Dismounted,
                    string.Empty));
                continue;
            }

            results.Add(new StaticWebAssetsAliasCleanupResult(
                normalizedMapping,
                StaticWebAssetsAliasCleanupStatus.Failed,
                failureMessage));
        }

        return results;
    }

    internal static StaticWebAssetsAliasCleanupStatus ClassifyStaticWebAssetsAliasCleanup(
        StaticWebAssetsAliasMapping mapping,
        string? currentSubstTarget)
    {
        if (!mapping.Mounted)
        {
            return StaticWebAssetsAliasCleanupStatus.SkippedNotMounted;
        }

        if (!TryNormalizeSubstDrive(mapping.Drive, out _))
        {
            return StaticWebAssetsAliasCleanupStatus.SkippedInvalidDrive;
        }

        if (string.IsNullOrWhiteSpace(mapping.WorkspaceRoot))
        {
            return StaticWebAssetsAliasCleanupStatus.SkippedMissingWorkspaceRoot;
        }

        if (string.IsNullOrWhiteSpace(currentSubstTarget))
        {
            return StaticWebAssetsAliasCleanupStatus.SkippedNoCurrentMapping;
        }

        return PathsReferToSameDirectory(currentSubstTarget, mapping.WorkspaceRoot)
            ? StaticWebAssetsAliasCleanupStatus.ReadyToDismount
            : StaticWebAssetsAliasCleanupStatus.SkippedMappingMismatch;
    }

    internal static string? ResolveSubstDriveTargetFromOutput(string substOutput, string drive)
    {
        if (string.IsNullOrWhiteSpace(substOutput) ||
            !TryNormalizeSubstDrive(drive, out var normalizedDrive))
        {
            return null;
        }

        foreach (var line in substOutput.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = line.IndexOf("=>", StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                continue;
            }

            var alias = line[..separatorIndex].Trim();
            if (alias.Length < 2 ||
                char.ToUpperInvariant(alias[0]) != normalizedDrive[0] ||
                alias[1] != ':')
            {
                continue;
            }

            var target = line[(separatorIndex + 2)..].Trim();
            return string.IsNullOrWhiteSpace(target)
                ? null
                : target;
        }

        return null;
    }

    private void LogStaticWebAssetsAliasCleanupIssues(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        IReadOnlyList<StaticWebAssetsAliasCleanupResult> cleanupResults)
    {
        foreach (var result in cleanupResults.Where(result => ShouldWarnStaticWebAssetsAliasCleanup(result.Status)))
        {
            logger.LogWarning(
                "Skipped kept-alive workspace_dotnet_run static web assets alias cleanup for process run {RunId}, step {StepRunId}, execution run {ExecutionRunId}. Drive={Drive}; WorkspaceRoot={WorkspaceRoot}; Status={Status}; Message={Message}",
                candidate.Run.Id,
                candidate.StepRun.Id,
                detail.Run.Id,
                result.Mapping.Drive,
                result.Mapping.WorkspaceRoot,
                result.Status,
                result.Message);
        }
    }

    private static bool ShouldWarnStaticWebAssetsAliasCleanup(StaticWebAssetsAliasCleanupStatus status)
        => status is StaticWebAssetsAliasCleanupStatus.SkippedInvalidDrive
            or StaticWebAssetsAliasCleanupStatus.SkippedMissingWorkspaceRoot
            or StaticWebAssetsAliasCleanupStatus.SkippedMappingMismatch
            or StaticWebAssetsAliasCleanupStatus.Failed;

    private static string? ResolveCurrentSubstDriveTarget(string drive)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "subst",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            if (!process.Start())
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(5_000) || process.ExitCode != 0)
            {
                return null;
            }

            return ResolveSubstDriveTargetFromOutput(output, drive);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static bool TryDismountSubstDrive(string drive, out string failureMessage)
    {
        failureMessage = string.Empty;
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "subst",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            process.StartInfo.ArgumentList.Add(drive);
            process.StartInfo.ArgumentList.Add("/d");

            if (!process.Start())
            {
                failureMessage = $"Unable to start subst cleanup for drive '{drive}'.";
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(5_000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }
                catch (System.ComponentModel.Win32Exception)
                {
                }

                failureMessage = $"Timed out while removing subst drive '{drive}'.";
                return false;
            }

            if (process.ExitCode == 0)
            {
                return true;
            }

            var diagnostic = string.Join(" ", [output, error]).Trim();
            failureMessage = string.IsNullOrWhiteSpace(diagnostic)
                ? $"subst exited with code {process.ExitCode} while removing drive '{drive}'."
                : diagnostic;
            return false;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            failureMessage = ex.Message;
            return false;
        }
    }

    private static bool PathsReferToSameDirectory(string firstPath, string secondPath)
    {
        var first = NormalizePathForComparison(firstPath);
        var second = NormalizePathForComparison(secondPath);
        return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePathForComparison(string path)
    {
        var normalized = path.Trim().Trim('"');
        try
        {
            normalized = Path.GetFullPath(normalized);
        }
        catch (ArgumentException)
        {
        }
        catch (NotSupportedException)
        {
        }
        catch (PathTooLongException)
        {
        }

        return normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool TryNormalizeSubstDrive(string drive, out string normalizedDrive)
    {
        normalizedDrive = string.Empty;
        var trimmed = drive.Trim();
        if (trimmed.Length != 2 ||
            trimmed[1] != ':' ||
            !IsAsciiLetter(trimmed[0]))
        {
            return false;
        }

        normalizedDrive = $"{char.ToUpperInvariant(trimmed[0])}:";
        return true;
    }

    private static bool IsAsciiLetter(char value)
        => value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool TryReadBoolean(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => false
        };
    }

    private static IReadOnlyList<int> TryReadIntArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<int>();
        foreach (var element in value.EnumerateArray())
        {
            if (element.TryGetInt32(out var processId) && processId > 0)
            {
                items.Add(processId);
            }
        }

        return items;
    }

    private static IReadOnlyList<StaticWebAssetsAliasMapping> TryReadStaticWebAssetsAliasMappings(
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var mappings = new List<StaticWebAssetsAliasMapping>();
        foreach (var element in value.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var mapping = new StaticWebAssetsAliasMapping(
                TryReadString(element, "drive"),
                TryReadString(element, "workspaceRoot"),
                TryReadBoolean(element, "mounted"));
            if (string.IsNullOrWhiteSpace(mapping.Drive) &&
                string.IsNullOrWhiteSpace(mapping.WorkspaceRoot) &&
                !mapping.Mounted)
            {
                continue;
            }

            mappings.Add(mapping);
        }

        return mappings;
    }

    private static string TryReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
            _ => string.Empty
        };
    }

    private static WorkspaceProcessLifetimeScope TryReadLifetimeScope(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return WorkspaceProcessLifetimeScope.ExecutionRun;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String when Enum.TryParse<WorkspaceProcessLifetimeScope>(value.GetString(), ignoreCase: true, out var parsed) => parsed,
            JsonValueKind.Number when value.TryGetInt32(out var numeric) && Enum.IsDefined(typeof(WorkspaceProcessLifetimeScope), numeric) => (WorkspaceProcessLifetimeScope)numeric,
            _ => WorkspaceProcessLifetimeScope.ExecutionRun
        };
    }

    private sealed record DotnetRunStartupReceipt(
        bool KeepAlive,
        WorkspaceProcessLifetimeScope LifetimeScope,
        bool CleanupAttempted,
        IReadOnlyList<int> AppProcessTreeIds,
        IReadOnlyList<StaticWebAssetsAliasMapping> StaticWebAssetsAliasMappings)
    {
        public static DotnetRunStartupReceipt Empty { get; } = new(false, WorkspaceProcessLifetimeScope.ExecutionRun, false, [], []);

        public bool HasCleanupTargets => AppProcessTreeIds.Count > 0 || StaticWebAssetsAliasMappings.Any(mapping => mapping.Mounted);
    }

    internal sealed record StaticWebAssetsAliasMapping(
        string Drive,
        string WorkspaceRoot,
        bool Mounted);

    internal enum StaticWebAssetsAliasCleanupStatus
    {
        ReadyToDismount,
        Dismounted,
        SkippedNotMounted,
        SkippedInvalidDrive,
        SkippedMissingWorkspaceRoot,
        SkippedNoCurrentMapping,
        SkippedMappingMismatch,
        Failed
    }

    private sealed record StaticWebAssetsAliasCleanupResult(
        StaticWebAssetsAliasMapping Mapping,
        StaticWebAssetsAliasCleanupStatus Status,
        string Message);
}
