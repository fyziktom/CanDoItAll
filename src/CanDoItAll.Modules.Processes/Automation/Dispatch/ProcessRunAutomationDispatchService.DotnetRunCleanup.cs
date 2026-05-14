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
            if (stoppedProcessIds.Count == 0)
            {
                continue;
            }

            logger.LogInformation(
                "Stopped kept-alive workspace_dotnet_run process tree for process run {RunId}, step {StepRunId}, execution run {ExecutionRunId}. ProcessIds={ProcessIds}",
                candidate.Run.Id,
                candidate.StepRun.Id,
                detail.Run.Id,
                string.Join(",", stoppedProcessIds));
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
                startupReceipt.AppProcessTreeIds.Count > 0 &&
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
                AppProcessTreeIds: TryReadIntArray(root, "appProcessTreeIds"));
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
        IReadOnlyList<int> AppProcessTreeIds)
    {
        public static DotnetRunStartupReceipt Empty { get; } = new(false, WorkspaceProcessLifetimeScope.ExecutionRun, false, []);
    }
}
