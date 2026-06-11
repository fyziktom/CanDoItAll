using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework.Hosting;

internal sealed partial class ProcessMockAgentRuntime
{
    private string BuildSerializedSessionState(
        string roleKey,
        ProcessMockRuntimeState state,
        ProcessMockRuntimeOutcome outcome,
        IReadOnlyList<string> inspectedArtifactPaths,
        IReadOnlyList<string> requiredToolNames)
    {
        var callContents = new List<Dictionary<string, object?>>();
        var resultContents = new List<Dictionary<string, object?>>();
        var browserOutputFilesByToolName = WriteRequiredBrowserOutputFiles(state, requiredToolNames);
        var callSequence = 1;
        foreach (var artifactPath in inspectedArtifactPaths)
        {
            var statCallId = $"stat-{callSequence}";
            var readCallId = $"read-{callSequence}";
            callContents.Add(CreateFunctionCall(statCallId, WorkspaceStatPathToolName, artifactPath));
            callContents.Add(CreateFunctionCall(readCallId, WorkspaceReadFileToolName, artifactPath));
            resultContents.Add(CreateFunctionResult(
                statCallId,
                new Dictionary<string, object?>
                {
                    ["succeeded"] = true,
                    ["path"] = artifactPath,
                    ["exists"] = true
                }));
            resultContents.Add(CreateFunctionResult(
                readCallId,
                new Dictionary<string, object?>
                {
                    ["succeeded"] = true,
                    ["path"] = artifactPath,
                    ["content"] = $"Process mock inspected inherited artifact {artifactPath}."
                }));
            callSequence++;
        }

        foreach (var toolName in requiredToolNames)
        {
            var callId = $"required-tool-{callSequence}";
            if (browserOutputFilesByToolName.TryGetValue(toolName, out var browserOutputFile))
            {
                callContents.Add(CreateFunctionCallWithFilename(callId, toolName, browserOutputFile));
                resultContents.Add(CreateFunctionResult(callId, BuildBrowserToolResult(toolName, browserOutputFile)));
                callSequence++;
                continue;
            }

            callContents.Add(CreateFunctionCall(callId, toolName));
            resultContents.Add(CreateFunctionResult(
                callId,
                new Dictionary<string, object?>
                {
                    ["succeeded"] = true,
                    ["summary"] = $"Process mock satisfied required current-run tool receipt for {toolName}."
                }));
            callSequence++;
        }

        return JsonSerializer.Serialize(
            new
            {
                processMockAgent = true,
                roleKey,
                state.RunKey,
                state.ArtifactRoot,
                state.ProcessCooperationMode,
                state.WorkspaceToolProfileOverride,
                outcome.BranchOutcomeKey,
                artifacts = outcome.Artifacts.Select(artifact => new
                {
                    artifact.RelativePath,
                    artifact.ContentSignalText
                }).ToArray(),
                stateBag = new Dictionary<string, object?>
                {
                    ["InMemoryChatHistoryProvider"] = new
                    {
                        messages = new object[]
                        {
                            new
                            {
                                role = "assistant",
                                contents = callContents.ToArray()
                            },
                            new
                            {
                                role = "tool",
                                contents = resultContents.ToArray()
                            }
                        }
                    }
                }
            },
            JsonOptions);
    }

    private IReadOnlyDictionary<string, string> WriteRequiredBrowserOutputFiles(
        ProcessMockRuntimeState state,
        IReadOnlyList<string> requiredToolNames)
    {
        var outputFilesByToolName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var toolName in requiredToolNames)
        {
            var outputFile = ResolveBrowserOutputFile(state, toolName);
            if (string.IsNullOrWhiteSpace(outputFile))
            {
                continue;
            }

            if (string.Equals(toolName, BrowserScreenshotToolName, StringComparison.Ordinal))
            {
                WriteWorkspaceBytes(outputFile, MockBrowserScreenshotPngBytes);
            }
            else
            {
                WriteWorkspaceText(outputFile, BuildBrowserOutputText(toolName));
            }

            outputFilesByToolName[toolName] = outputFile;
        }

        return outputFilesByToolName;
    }

    private void WriteWorkspaceText(string relativePath, string content)
    {
        var result = fileService.WriteTextFile(relativePath, content, overwrite: true);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Process mock failed to write browser artifact '{relativePath}': {result.Message}");
        }

        var normalizedRelativePath = WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
        var normalizedResultPath = WorkspaceScopeDescriptor.NormalizeRelativePath(result.Path);
        if (!string.Equals(normalizedRelativePath, normalizedResultPath, StringComparison.OrdinalIgnoreCase))
        {
            WriteUnscopedWorkspaceText(normalizedRelativePath, content);
        }
    }

    private void WriteWorkspaceBytes(string relativePath, byte[] content)
    {
        var reservation = fileService.WriteTextFile(relativePath, string.Empty, overwrite: true);
        if (!reservation.Succeeded)
        {
            throw new InvalidOperationException(
                $"Process mock failed to reserve browser artifact '{relativePath}': {reservation.Message}");
        }

        WriteUnscopedWorkspaceBytes(reservation.Path, content);

        var normalizedRelativePath = WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
        var normalizedReservationPath = WorkspaceScopeDescriptor.NormalizeRelativePath(reservation.Path);
        if (!string.Equals(normalizedRelativePath, normalizedReservationPath, StringComparison.OrdinalIgnoreCase))
        {
            WriteUnscopedWorkspaceBytes(normalizedRelativePath, content);
        }
    }

    private void WriteUnscopedWorkspaceText(string relativePath, string content)
    {
        var fullPath = ResolveWorkspaceRelativeFullPath(relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException($"Process mock browser artifact path '{relativePath}' does not resolve to a directory.");
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, content);
    }

    private void WriteUnscopedWorkspaceBytes(string relativePath, byte[] content)
    {
        var fullPath = ResolveWorkspaceRelativeFullPath(relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException($"Process mock browser artifact path '{relativePath}' does not resolve to a directory.");
        }

        Directory.CreateDirectory(directory);
        File.WriteAllBytes(fullPath, content);
    }

    private string ResolveWorkspaceRelativeFullPath(string relativePath)
    {
        var normalizedWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        var rootWithSeparator = normalizedWorkspaceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                                Path.DirectorySeparatorChar;
        var normalizedRelativePath = WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
        var fullPath = Path.GetFullPath(Path.Combine(
            normalizedWorkspaceRoot,
            normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, normalizedWorkspaceRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Process mock browser artifact path '{relativePath}' resolves outside the workspace root.");
        }

        return fullPath;
    }

    private static string ResolveBrowserOutputFile(ProcessMockRuntimeState state, string toolName)
    {
        var fileName = toolName switch
        {
            BrowserScreenshotToolName => "mock-screenshot.png",
            BrowserSnapshotToolName => "mock-snapshot.yml",
            BrowserConsoleMessagesToolName => "mock-console.log",
            BrowserEvaluateToolName => "mock-browser-state.json",
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(fileName)
            ? string.Empty
            : $"artifacts/process-runs/{state.RunKey}/browser/{fileName}";
    }

    private static string BuildBrowserOutputText(string toolName)
    {
        return toolName switch
        {
            BrowserSnapshotToolName =>
                """
                page:
                  title: "Process mock browser evidence"
                  url: "http://127.0.0.1/process-mock"
                  visibleText:
                    - "Requested application state is visible."
                    - "Acceptance criteria are represented in this deterministic mock snapshot."
                """,
            BrowserConsoleMessagesToolName =>
                """
                Browser console status: clean.
                No active JavaScript runtime diagnostics captured by the deterministic process mock.
                """,
            BrowserEvaluateToolName =>
                """
                {"processMockBrowserState":"accepted","visibleState":"requested application state is visible"}
                """,
            _ => "Process mock browser output captured."
        };
    }

    private static Dictionary<string, object?> BuildBrowserToolResult(string toolName, string outputFile)
    {
        var summary = toolName switch
        {
            BrowserScreenshotToolName => "Screenshot saved.",
            BrowserSnapshotToolName => "Snapshot saved.",
            BrowserConsoleMessagesToolName => "Browser console status captured.",
            BrowserEvaluateToolName => "Browser state captured.",
            _ => "Browser output saved."
        };

        return new Dictionary<string, object?>
        {
            ["succeeded"] = true,
            ["filename"] = outputFile,
            ["summary"] = summary
        };
    }

    private static Dictionary<string, object?> CreateFunctionCall(
        string callId,
        string toolName,
        string artifactPath)
    {
        return new Dictionary<string, object?>
        {
            ["$type"] = "functionCall",
            ["callId"] = callId,
            ["name"] = toolName,
            ["arguments"] = new Dictionary<string, object?>
            {
                ["path"] = artifactPath
            }
        };
    }

    private static Dictionary<string, object?> CreateFunctionCall(
        string callId,
        string toolName)
    {
        return new Dictionary<string, object?>
        {
            ["$type"] = "functionCall",
            ["callId"] = callId,
            ["name"] = toolName,
            ["arguments"] = new Dictionary<string, object?>()
        };
    }

    private static Dictionary<string, object?> CreateFunctionCallWithFilename(
        string callId,
        string toolName,
        string fileName)
    {
        return new Dictionary<string, object?>
        {
            ["$type"] = "functionCall",
            ["callId"] = callId,
            ["name"] = toolName,
            ["arguments"] = new Dictionary<string, object?>
            {
                ["filename"] = fileName
            }
        };
    }

    private static Dictionary<string, object?> CreateFunctionResult(
        string callId,
        Dictionary<string, object?> result)
    {
        return new Dictionary<string, object?>
        {
            ["$type"] = "functionResult",
            ["callId"] = callId,
            ["result"] = result
        };
    }

    private static IReadOnlyList<string> ResolveArtifactInspectionPaths(string prompt)
    {
        var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var inUpstreamArtifactsSection = false;
        foreach (var line in prompt.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            if (string.Equals(line.Trim(), "Upstream artifacts:", StringComparison.OrdinalIgnoreCase))
            {
                inUpstreamArtifactsSection = true;
                continue;
            }

            if (inUpstreamArtifactsSection && string.IsNullOrWhiteSpace(line))
            {
                inUpstreamArtifactsSection = false;
                continue;
            }

            if (!inUpstreamArtifactsSection && !MentionsInheritedArtifactInspection(line))
            {
                continue;
            }

            foreach (Match match in ManagedWorkspacePathRegex.Matches(line))
            {
                var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(match.Groups["path"].Value);
                if (IsConcreteManagedInspectionPath(normalizedPath))
                {
                    paths.Add(normalizedPath);
                }
            }
        }

        return paths.ToList();
    }

    private static bool MentionsInheritedArtifactInspection(string line)
    {
        return line.Contains("upstream durable", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("upstream artifact", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("inherited implementation artifact", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("inherited evidence", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConcreteManagedInspectionPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var slashIndex = path.IndexOf('/');
        return slashIndex > 0 && slashIndex < path.Length - 1;
    }

    private static ProcessMockRuntimeArtifact CreateArtifact(
        string relativePath,
        string contentSignalText)
    {
        return new ProcessMockRuntimeArtifact(relativePath, contentSignalText);
    }

    private static string BuildStructuredOutcome(
        string status,
        string reason,
        string? branchOutcomeKey,
        string responseSummary,
        IReadOnlyList<ProcessMockRuntimeArtifact> artifacts)
    {
        var evidenceRefs = artifacts
            .Select(artifact => artifact.RelativePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();
        var payload = new ProcessStepOutcomeResult
        {
            Status = Enum.Parse<ProcessStepOutcomeStatus>(status, ignoreCase: true),
            Reason = reason,
            BranchOutcomeKey = branchOutcomeKey ?? string.Empty,
            EvidenceRefs = evidenceRefs,
            NextActions = [],
            HumanReadableSummaryMarkdown = responseSummary
        };

        return JsonSerializer.Serialize(payload, AgentOutputJson.SerializerOptions);
    }

    private static IReadOnlyList<AgentFinalizerInvocation> BuildProcessStepOutcomeFinalizerInvocations(
        AgentStructuredOutputContract? structuredOutput,
        AgentRuntimeExecutionOptions? executionOptions,
        string responseText)
    {
        var effectiveStructuredOutput = executionOptions?.StructuredOutput ?? structuredOutput;
        var finalizerMode = executionOptions?.FinalizerMode ?? AgentFinalizerMode.Disabled;
        if (effectiveStructuredOutput?.OutputType != typeof(ProcessStepOutcomeResult) ||
            finalizerMode == AgentFinalizerMode.Disabled)
        {
            return [];
        }

        return
        [
            new AgentFinalizerInvocation(
                AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName,
                responseText,
                Sequence: 1)
        ];
    }

    private static IReadOnlyList<AgentToolInvocationTrace> BuildProcessStepOutcomeToolInvocationTraces(
        IReadOnlyList<string> requiredToolNames,
        AgentStructuredOutputContract? structuredOutput,
        AgentRuntimeExecutionOptions? executionOptions)
    {
        var effectiveStructuredOutput = executionOptions?.StructuredOutput ?? structuredOutput;
        var finalizerMode = executionOptions?.FinalizerMode ?? AgentFinalizerMode.Disabled;
        var includeFinalizerTrace = effectiveStructuredOutput?.OutputType == typeof(ProcessStepOutcomeResult) &&
                                    finalizerMode != AgentFinalizerMode.Disabled;
        if (!includeFinalizerTrace && requiredToolNames.Count == 0)
        {
            return [];
        }

        var timestamp = DateTimeOffset.UtcNow;
        var traces = new List<AgentToolInvocationTrace>();
        foreach (var toolName in requiredToolNames)
        {
            traces.Add(new AgentToolInvocationTrace(
                toolName,
                AgentToolInvocationPolicyMetadata.Classify(toolName),
                Sequence: traces.Count + 1,
                StartedAtUtc: timestamp,
                CompletedAtUtc: timestamp,
                Succeeded: true,
                FailureMessage: string.Empty));
        }

        if (includeFinalizerTrace &&
            !requiredToolNames.Contains(AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName, StringComparer.Ordinal))
        {
            traces.Add(new AgentToolInvocationTrace(
                AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName,
                ToolInvocationClassification.Read,
                Sequence: traces.Count + 1,
                StartedAtUtc: timestamp,
                CompletedAtUtc: timestamp,
                Succeeded: true,
                FailureMessage: string.Empty));
        }

        return traces;
    }

    private static IReadOnlyList<string> ResolvePromptRequiredToolNames(string prompt)
    {
        var toolNames = new List<string>();
        foreach (var rawLine in prompt.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith(
                    "- Completion of this step is gated on successful current-run tool receipts for:",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (Match match in RequiredToolNameRegex.Matches(line))
            {
                var toolName = match.Groups["toolName"].Value.Trim();
                if (!string.IsNullOrWhiteSpace(toolName))
                {
                    toolNames.Add(toolName);
                }
            }
        }

        return toolNames
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

}
