using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework.Hosting;

internal sealed partial class ScenarioHarnessAgentRuntime(
    IAgentRuntime inner,
    string workspaceRoot,
    WorkspaceScopeDescriptor workspaceScope,
    IWorkspaceFileService fileService,
    IWorkspaceCommandExecutionService commandExecutionService) : IAgentRuntime
{
    private const string ProcessScenarioOutputRoot = "output/ps";
    private const string ProcessScenarioArtifactRoot = "artifacts/ps";
    private const string StandaloneScenarioOutputRoot = "output/sh";
    private const string StandaloneScenarioArtifactRoot = "artifacts/sh";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string workspaceRoot = Path.GetFullPath(workspaceRoot);
    private readonly WorkspaceScopeDescriptor workspaceScope = workspaceScope;

    public Task<ProviderHealthResult> TestProviderAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken = default)
    {
        if (!IsScenarioProvider(provider))
        {
            return inner.TestProviderAsync(provider, cancellationToken);
        }

        return Task.FromResult(new ProviderHealthResult(
            Success: true,
            Summary: "Scenario harness provider is available for deterministic integrated proof runs.",
            SuggestedModels:
            [
                "scenario-local"
            ]));
    }

    public Task<ProviderTestChatResult> RunProviderTestChatAsync(
        ProviderProfile provider,
        ProviderTestChatRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsScenarioProvider(provider))
        {
            return inner.RunProviderTestChatAsync(provider, request, cancellationToken);
        }

        return Task.FromResult(new ProviderTestChatResult(
            Model: "scenario-local",
            ResponseText: "Scenario harness provider is deterministic. Use SC03 and SC04 directly, then validate SC09-SC11 through the integrated `/processes` flow.",
            InputTokens: 18,
            OutputTokens: 28));
    }

    public Task<OllamaModelfileResult> CreateOrUpdateOllamaModelAsync(
        ProviderProfile provider,
        OllamaModelfileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsScenarioProvider(provider))
        {
            return inner.CreateOrUpdateOllamaModelAsync(provider, request, cancellationToken);
        }

        throw new InvalidOperationException("The scenario harness provider does not support Ollama model creation.");
    }

    public async Task<AgentRuntimeResponse> RunAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        string prompt,
        string? runtimeSessionKey,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken = default,
        bool suppressApprovalRequirements = false,
        AgentStructuredOutputContract? structuredOutput = null)
    {
        if (!IsScenarioProvider(provider))
        {
            return await inner.RunAsync(
                agent,
                provider,
                session,
                capabilities,
                memory,
                prompt,
                runtimeSessionKey,
                progressCallback,
                cancellationToken,
                suppressApprovalRequirements,
                structuredOutput);
        }

        var definition = ResolveDefinition(prompt);
        var state = CreateState(definition, prompt);

        if (definition.Mode != ScenarioAutomationMode.Automated)
        {
            var guidedResponse = BuildGuidedResponse(definition, state);
            fileService.WriteTextFile($"{state.ArtifactRoot}/response.md", guidedResponse, overwrite: true);
            await progressCallback(
                ExecutionState.Completed,
                "Guided proof",
                $"{definition.Id} is a guided scenario and must be completed through the integrated process proof.");

            return new AgentRuntimeResponse(
                ResponseText: $"{definition.Id} is a guided scenario. Follow the integrated proof note instead of expecting automatic execution.",
                InputTokens: EstimateTokens(prompt),
                OutputTokens: 22,
                ToolCalls: 1,
                RuntimeSessionKey: state.RuntimeSessionKey,
                SerializedSessionStateJson: JsonSerializer.Serialize(state with { Status = "guided" }, JsonOptions),
                PendingApprovals: []);
        }

        if (definition.RequiresApproval && !suppressApprovalRequirements)
        {
            await progressCallback(
                ExecutionState.WaitingOnTool,
                "Approval requested",
                $"{definition.Id} is paused until the guarded mutation is approved.");

            return new AgentRuntimeResponse(
                ResponseText: $"{definition.Id} is ready to continue but requires approval before the guarded mutation can run.",
                InputTokens: EstimateTokens(prompt),
                OutputTokens: 17,
                ToolCalls: 0,
                RuntimeSessionKey: state.RuntimeSessionKey,
                SerializedSessionStateJson: JsonSerializer.Serialize(state, JsonOptions),
                PendingApprovals:
                [
                    new PendingToolApprovalRecord(
                        ApprovalId: $"{definition.Id}-approval",
                        CallId: $"{definition.Id}-guarded-call",
                        ToolName: "workspace_pwsh_run_script",
                        ToolKind: "workspace-process",
                        Details: "Create approval-proof artifacts and enumerate the approved files through the controlled PowerShell recipe.",
                        ArgumentsJson: """{"scenarioId":"SC04","operation":"approval-proof"}""")
                ]);
        }

        return await ExecuteScenarioAsync(definition, state, progressCallback, structuredOutput);
    }

    public async Task<AgentRuntimeResponse> RespondToPendingApprovalsAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        bool approved,
        string? runtimeSessionKey,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken = default,
        bool suppressApprovalRequirements = false,
        AgentStructuredOutputContract? structuredOutput = null)
    {
        if (!IsScenarioProvider(provider))
        {
            return await inner.RespondToPendingApprovalsAsync(
                agent,
                provider,
                session,
                capabilities,
                memory,
                approved,
                runtimeSessionKey,
                progressCallback,
                cancellationToken,
                suppressApprovalRequirements,
                structuredOutput);
        }

        var state = ParseState(session.Compatibility?.SerializedSessionStateJson)
            ?? throw new InvalidOperationException("The scenario harness could not restore the pending approval state.");
        var definition = ResolveDefinition(state.ScenarioId);

        if (!approved)
        {
            var rejectionMarkdown =
                $"""
                # {definition.Id} Rejected

                The pending approval was rejected, so the guarded mutation did not execute.

                - Runtime session: `{state.RuntimeSessionKey}`
                - Scenario: `{definition.Title}`
                """;
            fileService.WriteTextFile($"{state.ArtifactRoot}/response.md", rejectionMarkdown, overwrite: true);

            await progressCallback(
                ExecutionState.Completed,
                "Approval rejected",
                $"{definition.Id} ended without executing the guarded mutation.");

            return new AgentRuntimeResponse(
                ResponseText: $"{definition.Id} was rejected. No approval-proof files were created.",
                InputTokens: 8,
                OutputTokens: 14,
                ToolCalls: 1,
                RuntimeSessionKey: state.RuntimeSessionKey,
                SerializedSessionStateJson: JsonSerializer.Serialize(state with { Status = "rejected" }, JsonOptions),
                PendingApprovals: []);
        }

        return await ExecuteScenarioAsync(definition, state with { Status = "approved" }, progressCallback, structuredOutput);
    }

    private async Task<AgentRuntimeResponse> ExecuteScenarioAsync(
        ScenarioHarnessDefinition definition,
        ScenarioRuntimeState state,
        Func<ExecutionState, string, string, Task> progressCallback,
        AgentStructuredOutputContract? structuredOutput)
    {
        await progressCallback(
            ExecutionState.Running,
            "Scenario setup",
            $"Preparing {definition.Id} inputs and output folders.");
        EnsureScenarioDirectories(state);

        var outcome = definition.Id switch
        {
            "SC03" => await ExecuteBlazorCalculatorAsync(state, progressCallback),
            "SC04" => await ExecuteApprovalScenarioAsync(state, progressCallback),
            "SC10" => await ExecuteCalculatorReviewAsync(state, progressCallback),
            _ => throw new InvalidOperationException($"Unsupported automated scenario '{definition.Id}'.")
        };

        fileService.WriteTextFile($"{state.ArtifactRoot}/response.md", outcome.ResponseMarkdown, overwrite: true);

        await progressCallback(
            ExecutionState.Persisting,
            "Evidence persisted",
            $"{definition.Id} saved response, artifacts, and receipts.");
        await progressCallback(
            ExecutionState.Completed,
            "Scenario complete",
            $"{definition.Id} completed successfully.");

        var responseText = FormatScenarioResponse(outcome, structuredOutput);
        return new AgentRuntimeResponse(
            ResponseText: responseText,
            InputTokens: EstimateTokens(state.OriginalPrompt),
            OutputTokens: EstimateTokens(responseText),
            ToolCalls: outcome.ToolCalls,
            RuntimeSessionKey: state.RuntimeSessionKey,
            SerializedSessionStateJson: JsonSerializer.Serialize(state with { Status = "completed" }, JsonOptions),
            PendingApprovals: []);
    }

    private static string FormatScenarioResponse(
        ScenarioExecutionOutcome outcome,
        AgentStructuredOutputContract? structuredOutput)
    {
        if (structuredOutput?.OutputType != typeof(ProcessStepOutcomeResult))
        {
            return outcome.ResponseText;
        }

        var result = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = outcome.ResponseText,
            EvidenceRefs = [],
            NextActions = [],
            HumanReadableSummaryMarkdown = outcome.ResponseMarkdown
        };
        return JsonSerializer.Serialize(result, AgentOutputJson.SerializerOptions);
    }

    private async Task<ScenarioExecutionOutcome> ExecuteBlazorCalculatorAsync(
        ScenarioRuntimeState state,
        Func<ExecutionState, string, string, Task> progressCallback)
    {
        var specPath = $"{state.InputRoot}/spec.md";
        var projectRoot = $"{state.OutputRoot}/ScenarioCalculator";
        var projectPath = $"{projectRoot}/ScenarioCalculator.csproj";

        fileService.WriteTextFile(specPath, CalculatorSpecMarkdown, overwrite: true);

        await progressCallback(
            ExecutionState.Running,
            "Project generation",
            "Creating a fresh Blazor Web App with the controlled dotnet template recipe.");
        var dotnetNewResult = await commandExecutionService.DotnetNew(
            "blazor",
            "ScenarioCalculator",
            parentDirectory: state.OutputRoot,
            timeoutSeconds: 300);
        EnsureSucceeded(dotnetNewResult.Succeeded, dotnetNewResult.Message + Environment.NewLine + dotnetNewResult.StderrPreview);

        fileService.WriteTextFile($"{projectRoot}/Components/Pages/Home.razor", CalculatorHomeRazor, overwrite: true);
        fileService.WriteTextFile($"{projectRoot}/README.md", CalculatorReadme, overwrite: true);

        await progressCallback(
            ExecutionState.Running,
            "Project build",
            "Building the generated calculator app through the controlled command surface.");
        var buildResult = await commandExecutionService.DotnetBuild(
            projectPath,
            configuration: "Debug",
            noRestore: false,
            workingDirectory: projectRoot,
            timeoutSeconds: 600);
        EnsureSucceeded(buildResult.Succeeded, buildResult.Message + Environment.NewLine + buildResult.StderrPreview);

        var reportMarkdown =
            $"""
            # SC03 Blazor Calculator Generation

            The scenario generated a Blazor Web App under `{projectRoot}` and replaced the home page with a four-operation calculator.

            ## Included outputs
            - Spec: `{specPath}`
            - Calculator page: `{projectRoot}/Components/Pages/Home.razor`
            - Run guide: `{projectRoot}/README.md`
            - Build target: `{projectPath}`

            ## Build result
            {TrimForMarkdown(buildResult.StdoutPreview, 1200)}
            """;

        fileService.WriteTextFile($"{state.ArtifactRoot}/generation-report.md", reportMarkdown, overwrite: true);

        return new ScenarioExecutionOutcome(
            ResponseText: "SC03 completed with a generated Blazor calculator project and a successful controlled build receipt.",
            ResponseMarkdown: reportMarkdown,
            ToolCalls: 6);
    }

    private async Task<ScenarioExecutionOutcome> ExecuteApprovalScenarioAsync(
        ScenarioRuntimeState state,
        Func<ExecutionState, string, string, Task> progressCallback)
    {
        var taskPath = $"{state.InputRoot}/approval-task.md";
        fileService.WriteTextFile(taskPath, ApprovalTaskMarkdown, overwrite: true);

        await progressCallback(
            ExecutionState.Running,
            "Approved mutation",
            "Creating the approval-proof artifact folder and the guarded markdown file.");
        fileService.CreateDirectory($"{state.OutputRoot}/approval-proof");
        fileService.WriteTextFile(
            $"{state.OutputRoot}/approval-proof/approved-action.md",
            ApprovalProofMarkdown,
            overwrite: true);

        var scriptPath = $"{state.OutputRoot}/list_created_files.ps1";
        fileService.WriteTextFile(scriptPath, ApprovalInventoryPowerShellScript, overwrite: true);

        await progressCallback(
            ExecutionState.Running,
            "Approved follow-up command",
            "Enumerating the created files through the controlled PowerShell recipe.");
        var executionResult = await commandExecutionService.PowerShellRunScript(
            scriptPath,
            [ToAbsoluteWorkspacePath($"{state.OutputRoot}/approval-proof")],
            workingDirectory: state.OutputRoot,
            timeoutSeconds: 300);
        EnsureSucceeded(executionResult.Succeeded, executionResult.Message + Environment.NewLine + executionResult.StderrPreview);

        var reportMarkdown =
            $"""
            # SC04 Approval Pause And Resume

            The guarded mutation completed after an approval continuation.

            ## Created files
            {TrimForMarkdown(executionResult.StdoutPreview, 1600)}

            ## Evidence
            - Approval task: `{taskPath}`
            - Approved markdown: `{state.OutputRoot}/approval-proof/approved-action.md`
            - PowerShell script: `{scriptPath}`
            """;

        fileService.WriteTextFile($"{state.ArtifactRoot}/approval-report.md", reportMarkdown, overwrite: true);

        return new ScenarioExecutionOutcome(
            ResponseText: "SC04 completed after approval. The approval-proof artifact and follow-up inventory report are saved under the scenario folders.",
            ResponseMarkdown: reportMarkdown,
            ToolCalls: 6);
    }

    private async Task<ScenarioExecutionOutcome> ExecuteCalculatorReviewAsync(
        ScenarioRuntimeState state,
        Func<ExecutionState, string, string, Task> progressCallback)
    {
        await progressCallback(
            ExecutionState.Running,
            "Calculator review",
            "Inspecting the generated calculator delivery to produce a durable review report.");

        var projectRoot = ResolveCalculatorProjectRoot(state);
        var relativeProjectRoot = Path.GetRelativePath(workspaceRoot, projectRoot).Replace('\\', '/');
        var homePath = Path.Combine(projectRoot, "Components", "Pages", "Home.razor");
        var readmePath = Path.Combine(projectRoot, "README.md");
        var projectPath = Path.Combine(projectRoot, "ScenarioCalculator.csproj");

        if (!File.Exists(homePath) || !File.Exists(readmePath) || !File.Exists(projectPath))
        {
            throw new InvalidOperationException("SC10 could not find the generated calculator project to review.");
        }

        var homeContent = await File.ReadAllTextAsync(homePath);
        var readmeContent = await File.ReadAllTextAsync(readmePath);
        var findings = BuildCalculatorFindings(homeContent, readmeContent);
        var reportMarkdown =
            $"""
            # SC10 Calculator Delivery Review

            The generated calculator delivery was inspected successfully.

            ## Project
            - Root: `{relativeProjectRoot}`
            - Project file: `{NormalizeRelativeWorkspacePath(projectPath)}`
            - Calculator page: `{NormalizeRelativeWorkspacePath(homePath)}`
            - README: `{NormalizeRelativeWorkspacePath(readmePath)}`

            ## Review findings
            {string.Join(Environment.NewLine, findings.Select(item => $"- {item}"))}
            """;

        fileService.WriteTextFile($"{state.ArtifactRoot}/review-report.md", reportMarkdown, overwrite: true);

        return new ScenarioExecutionOutcome(
            ResponseText: "SC10 completed with a durable calculator delivery review report.",
            ResponseMarkdown: reportMarkdown,
            ToolCalls: 3);
    }

    private string ResolveCalculatorProjectRoot(ScenarioRuntimeState state)
    {
        if (!string.IsNullOrWhiteSpace(state.ProcessRunId))
        {
            var processScopedRoot = ToAbsoluteWorkspacePath(
                $"{ProcessScenarioOutputRoot}/{CreateProcessStorageKey(state.ProcessRunId)}/sc03/w/ScenarioCalculator");
            if (File.Exists(Path.Combine(processScopedRoot, "ScenarioCalculator.csproj")))
            {
                return processScopedRoot;
            }
        }

        var standaloneRoot = ToAbsoluteWorkspacePath($"{StandaloneScenarioOutputRoot}/sc03");
        if (Directory.Exists(standaloneRoot))
        {
            var latestProject = Directory.EnumerateFiles(standaloneRoot, "ScenarioCalculator.csproj", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(latestProject))
            {
                return Path.GetDirectoryName(latestProject)!;
            }
        }

        throw new InvalidOperationException("No generated calculator project is available for SC10.");
    }

    private static IReadOnlyList<string> BuildCalculatorFindings(string homeContent, string readmeContent)
    {
        var findings = new List<string>();
        if (homeContent.Contains("Add", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add("The calculator page includes an addition action.");
        }

        if (homeContent.Contains("Subtract", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add("The calculator page includes a subtraction action.");
        }

        if (homeContent.Contains("Multiply", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add("The calculator page includes a multiplication action.");
        }

        if (homeContent.Contains("Divide", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add("The calculator page includes a division action.");
        }

        if (readmeContent.Contains("dotnet run", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add("The README explains how to run the generated calculator.");
        }

        if (findings.Count == 0)
        {
            findings.Add("The calculator delivery exists, but the review did not find the expected operator markers.");
        }

        return findings;
    }

    private static ScenarioHarnessDefinition ResolveDefinition(string promptOrId)
    {
        var scenarioId = ResolveScenarioId(promptOrId);
        if (!ScenarioHarnessCatalog.TryGetDefinition(scenarioId, out var definition))
        {
            throw new InvalidOperationException($"Unknown scenario prompt '{promptOrId}'.");
        }

        return definition;
    }

    private static string ResolveScenarioId(string promptOrId)
    {
        var trimmed = (promptOrId ?? string.Empty).Trim();
        foreach (var definition in ScenarioHarnessCatalog.Definitions)
        {
            if (trimmed.Contains(definition.Id, StringComparison.OrdinalIgnoreCase))
            {
                return definition.Id;
            }
        }

        throw new InvalidOperationException($"Unable to resolve a scenario id from '{trimmed}'.");
    }

    private ScenarioRuntimeState CreateState(ScenarioHarnessDefinition definition, string prompt)
    {
        var auditScope = WorkspaceExecutionAuditContext.Current;
        if (!string.IsNullOrWhiteSpace(auditScope?.ProcessRunId))
        {
            var scenarioKey = definition.Id.ToLowerInvariant();
            var processStorageKey = CreateProcessStorageKey(auditScope.ProcessRunId);
            return new ScenarioRuntimeState(
                ScenarioId: definition.Id,
                OriginalPrompt: prompt,
                RuntimeSessionKey: $"{scenarioKey}-{auditScope.ExecutionRunId:N}",
                RunKey: processStorageKey,
                InputRoot: $"{ProcessScenarioOutputRoot}/{processStorageKey}/{scenarioKey}/i",
                OutputRoot: $"{ProcessScenarioOutputRoot}/{processStorageKey}/{scenarioKey}/w",
                ArtifactRoot: $"{ProcessScenarioArtifactRoot}/{processStorageKey}/{scenarioKey}",
                Status: "pending",
                ProcessRunId: auditScope.ProcessRunId,
                ProcessStepId: auditScope.ProcessStepId);
        }

        var runKey = CreateStandaloneRunKey();
        return new ScenarioRuntimeState(
            ScenarioId: definition.Id,
            OriginalPrompt: prompt,
            RuntimeSessionKey: $"{definition.Id.ToLowerInvariant()}-{Guid.NewGuid():N}",
            RunKey: runKey,
            InputRoot: $"{StandaloneScenarioOutputRoot}/{definition.Id.ToLowerInvariant()}/{runKey}/i",
            OutputRoot: $"{StandaloneScenarioOutputRoot}/{definition.Id.ToLowerInvariant()}/{runKey}/w",
            ArtifactRoot: $"{StandaloneScenarioArtifactRoot}/{definition.Id.ToLowerInvariant()}/{runKey}",
            Status: "pending",
            ProcessRunId: string.Empty,
            ProcessStepId: string.Empty);
    }

    private static string CreateProcessStorageKey(string processRunId)
    {
        var normalized = processRunId.Replace("-", string.Empty, StringComparison.Ordinal).Trim();
        return normalized.Length <= 12
            ? normalized
            : normalized[..12];
    }

    private static string CreateStandaloneRunKey()
    {
        return $"{DateTimeOffset.UtcNow:yyMMddHHmmssfff}-{Guid.NewGuid():N}"[..24];
    }

    private string EnsureScenarioDirectories(ScenarioRuntimeState state)
    {
        fileService.CreateDirectory(state.InputRoot);
        fileService.CreateDirectory(state.OutputRoot);
        fileService.CreateDirectory(state.ArtifactRoot);
        return state.ArtifactRoot;
    }

    private static ScenarioRuntimeState? ParseState(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ScenarioRuntimeState>(json, JsonOptions);
    }

    private static bool IsScenarioProvider(ProviderProfile provider)
        => string.Equals(provider.BaseUrl, ScenarioHarnessCatalog.ProviderBaseUrl, StringComparison.OrdinalIgnoreCase);

    private string ToAbsoluteWorkspacePath(string relativePath)
    {
        var normalizedRelativePath = relativePath.Replace('\\', '/').TrimStart('/');
        var scopedPath = workspaceScope.IsDefaultSandbox
            ? normalizedRelativePath
            : normalizedRelativePath.StartsWith("artifacts/", StringComparison.OrdinalIgnoreCase)
                ? workspaceScope.CombineArtifactPath(normalizedRelativePath["artifacts/".Length..])
                : normalizedRelativePath.StartsWith("output/", StringComparison.OrdinalIgnoreCase)
                    ? workspaceScope.CombineOutputPath(normalizedRelativePath["output/".Length..])
                    : normalizedRelativePath.StartsWith("data/", StringComparison.OrdinalIgnoreCase)
                        ? workspaceScope.CombineDataPath(normalizedRelativePath["data/".Length..])
                        : normalizedRelativePath;
        return Path.GetFullPath(Path.Combine(workspaceRoot, scopedPath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private string NormalizeRelativeWorkspacePath(string fullPath)
    {
        return Path.GetRelativePath(workspaceRoot, fullPath).Replace('\\', '/');
    }

    private static int EstimateTokens(string content)
        => Math.Max(1, (content ?? string.Empty).Length / 4);

    private static void EnsureSucceeded(bool succeeded, string message)
    {
        if (!succeeded)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static string TrimForMarkdown(string content, int maxCharacters)
    {
        var normalized = StripAnsiEscapeSequences(content ?? string.Empty)
            .ReplaceLineEndings(Environment.NewLine)
            .Trim();
        if (normalized.Length <= maxCharacters)
        {
            return normalized;
        }

        return normalized[..maxCharacters] + Environment.NewLine + "[truncated]";
    }

    private static string StripAnsiEscapeSequences(string content)
        => string.IsNullOrEmpty(content)
            ? string.Empty
            : Regex.Replace(
                content,
                @"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])",
                string.Empty,
                RegexOptions.Compiled);

    private static string BuildGuidedResponse(ScenarioHarnessDefinition definition, ScenarioRuntimeState state)
    {
        return
            $"""
            # {definition.Id} Guided Proof

            {definition.EvidenceNote}

            - Mode: `{definition.Mode}`
            - Runtime session: `{state.RuntimeSessionKey}`
            - Prompt: `{definition.Prompt}`
            """;
    }

    private sealed record ScenarioRuntimeState(
        string ScenarioId,
        string OriginalPrompt,
        string RuntimeSessionKey,
        string RunKey,
        string InputRoot,
        string OutputRoot,
        string ArtifactRoot,
        string Status,
        string ProcessRunId,
        string ProcessStepId);

    private sealed record ScenarioExecutionOutcome(
        string ResponseText,
        string ResponseMarkdown,
        int ToolCalls);
}
