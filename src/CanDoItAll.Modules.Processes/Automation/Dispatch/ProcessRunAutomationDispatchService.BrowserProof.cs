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
    private static string ResolveInvalidBrowserProofSummary(
        DispatchCandidate candidate,
        ExecutionRunDetail detail)
    {
        if (!RequiresConcreteBrowserProof(candidate))
        {
            return string.Empty;
        }

        if (ContainsSerializedPowerShellErrorRecord(detail.Run.SerializedSessionStateJson))
        {
            return "the launch helper reported PowerShell errors on stderr despite a successful tool result";
        }

        var browserWorkingDirectory = ResolveProviderNativeBrowserWorkingDirectory(detail);
        if (string.IsNullOrWhiteSpace(browserWorkingDirectory))
        {
            return string.Empty;
        }

        var outputsByToolName = ResolveSuccessfulSessionToolOutputFiles(detail.Run.SerializedSessionStateJson ?? string.Empty);
        if (!outputsByToolName.TryGetValue("browser_snapshot", out var snapshotFiles) ||
            snapshotFiles.Count == 0)
        {
            return string.Empty;
        }

        foreach (var snapshotFile in snapshotFiles)
        {
            if (!TryReadBrowserOutputText(browserWorkingDirectory, snapshotFile, out var snapshotText))
            {
                continue;
            }

            if (ContainsStarterTemplateBrowserProof(snapshotText))
            {
                return "browser proof captured starter-template content instead of the requested application";
            }

            if (ContainsRuntimeErrorBrowserProof(snapshotText))
            {
                return "browser proof captured an application runtime error instead of the requested application";
            }
        }

        return string.Empty;
    }

    private static bool ContainsSerializedPowerShellErrorRecord(string? serializedSessionStateJson)
    {
        if (string.IsNullOrWhiteSpace(serializedSessionStateJson))
        {
            return false;
        }

        return serializedSessionStateJson.Contains("Cannot overwrite variable PID because it is read-only or constant", StringComparison.OrdinalIgnoreCase) ||
               serializedSessionStateJson.Contains("WriteError:", StringComparison.OrdinalIgnoreCase) ||
               serializedSessionStateJson.Contains("ParserError:", StringComparison.OrdinalIgnoreCase) ||
               serializedSessionStateJson.Contains("RuntimeException:", StringComparison.OrdinalIgnoreCase) ||
               serializedSessionStateJson.Contains("FullyQualifiedErrorId", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadBrowserOutputText(
        string browserWorkingDirectory,
        string relativeOutputPath,
        out string text)
    {
        text = string.Empty;
        if (!TryResolveSafeBrowserOutputPath(browserWorkingDirectory, relativeOutputPath, out var fullPath) ||
            !File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(fullPath);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var buffer = new char[MaxBrowserSnapshotInspectionCharacters];
            var length = reader.ReadBlock(buffer, 0, buffer.Length);
            text = new string(buffer, 0, length);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryResolveSafeBrowserOutputPath(
        string browserWorkingDirectory,
        string relativeOutputPath,
        out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(browserWorkingDirectory) ||
            string.IsNullOrWhiteSpace(relativeOutputPath) ||
            Path.IsPathRooted(relativeOutputPath))
        {
            return false;
        }

        var root = Path.GetFullPath(browserWorkingDirectory);
        var candidate = Path.GetFullPath(Path.Combine(root, relativeOutputPath.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }

    private static bool ContainsStarterTemplateBrowserProof(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("Hello, world!", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Welcome to your new app.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsRuntimeErrorBrowserProof(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("Application error", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("An error has occurred", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Unhandled exception", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("HTTP ERROR 500", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("blazor-error-ui", StringComparison.OrdinalIgnoreCase);
    }

}
