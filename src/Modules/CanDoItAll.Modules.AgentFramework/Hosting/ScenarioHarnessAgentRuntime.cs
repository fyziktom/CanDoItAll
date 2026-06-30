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

    [GeneratedRegex(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])")]
    private static partial Regex AnsiEscapeSequenceRegex();

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

    public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
        ProviderProfile provider,
        ProviderModelMaintenanceEditorRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsScenarioProvider(provider))
        {
            return inner.CreateOrUpdateProviderModelAsync(provider, request, cancellationToken);
        }

        throw new InvalidOperationException("The scenario harness provider does not support provider model maintenance.");
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
        AgentStructuredOutputContract? structuredOutput = null,
        AgentRuntimeExecutionOptions? executionOptions = null)
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
                structuredOutput,
                executionOptions);
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
            var responseText = FormatScenarioResponse(
                new ScenarioExecutionOutcome(
                    ResponseText: $"{definition.Id} is a guided scenario. Follow the integrated proof note instead of expecting automatic execution.",
                    ResponseMarkdown: guidedResponse,
                    ToolCalls: 1),
                structuredOutput);

            return new AgentRuntimeResponse(
                ResponseText: responseText,
                InputTokens: EstimateTokens(prompt),
                OutputTokens: EstimateTokens(responseText),
                ToolCalls: 1,
                RuntimeSessionKey: state.RuntimeSessionKey,
            SerializedSessionStateJson: JsonSerializer.Serialize(state with { Status = "guided" }, JsonOptions),
            PendingApprovals: [])
        {
            FinalizerInvocations = BuildProcessStepOutcomeFinalizerInvocations(structuredOutput, executionOptions, responseText),
            ToolInvocationTraces = BuildProcessStepOutcomeToolInvocationTraces(structuredOutput, executionOptions)
        };
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

        return await ExecuteScenarioAsync(definition, state, progressCallback, structuredOutput, executionOptions);
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
        AgentStructuredOutputContract? structuredOutput = null,
        AgentRuntimeExecutionOptions? executionOptions = null)
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
                structuredOutput,
                executionOptions);
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

        return await ExecuteScenarioAsync(definition, state with { Status = "approved" }, progressCallback, structuredOutput, executionOptions);
    }

    private async Task<AgentRuntimeResponse> ExecuteScenarioAsync(
        ScenarioHarnessDefinition definition,
        ScenarioRuntimeState state,
        Func<ExecutionState, string, string, Task> progressCallback,
        AgentStructuredOutputContract? structuredOutput,
        AgentRuntimeExecutionOptions? executionOptions)
    {
        await progressCallback(
            ExecutionState.Running,
            "Scenario setup",
            $"Preparing {definition.Id} inputs and output folders.");
        EnsureScenarioDirectories(state);

        var outcome = definition.Id switch
        {
            "SC03" => await ExecuteGeneratedBlazorAppAsync(state, progressCallback),
            "SC04" => await ExecuteApprovalScenarioAsync(state, progressCallback),
            "SC10" => await ExecuteGeneratedAppReviewAsync(state, progressCallback),
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
            PendingApprovals: [])
        {
            FinalizerInvocations = BuildProcessStepOutcomeFinalizerInvocations(structuredOutput, executionOptions, responseText),
            ToolInvocationTraces = BuildProcessStepOutcomeToolInvocationTraces(structuredOutput, executionOptions)
        };
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
        AgentStructuredOutputContract? structuredOutput,
        AgentRuntimeExecutionOptions? executionOptions)
    {
        var effectiveStructuredOutput = executionOptions?.StructuredOutput ?? structuredOutput;
        var finalizerMode = executionOptions?.FinalizerMode ?? AgentFinalizerMode.Disabled;
        if (effectiveStructuredOutput?.OutputType != typeof(ProcessStepOutcomeResult) ||
            finalizerMode == AgentFinalizerMode.Disabled)
        {
            return [];
        }

        var timestamp = DateTimeOffset.UtcNow;
        return
        [
            new AgentToolInvocationTrace(
                AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName,
                ToolInvocationClassification.Read,
                Sequence: 1,
                StartedAtUtc: timestamp,
                CompletedAtUtc: timestamp,
                Succeeded: true,
                FailureMessage: string.Empty)
        ];
    }

    private async Task<ScenarioExecutionOutcome> ExecuteGeneratedBlazorAppAsync(
        ScenarioRuntimeState state,
        Func<ExecutionState, string, string, Task> progressCallback)
    {
        var specPath = $"{state.InputRoot}/spec.md";
        var projectRoot = $"{state.OutputRoot}/GeneratedBlazorApp";
        var projectPath = $"{projectRoot}/GeneratedBlazorApp.csproj";

        fileService.WriteTextFile(specPath, GeneratedBlazorAppSpecMarkdown, overwrite: true);

        await progressCallback(
            ExecutionState.Running,
            "Project generation",
            "Creating a fresh Blazor Web App with the controlled dotnet template recipe.");
        var dotnetNewResult = await commandExecutionService.DotnetNew(
            "blazor",
            "GeneratedBlazorApp",
            parentDirectory: state.OutputRoot,
            timeoutSeconds: 300);
        EnsureSucceeded(dotnetNewResult.Succeeded, dotnetNewResult.Message + Environment.NewLine + dotnetNewResult.StderrPreview);

        fileService.WriteTextFile($"{projectRoot}/Components/Pages/Home.razor", GeneratedBlazorAppHomeRazor, overwrite: true);
        fileService.WriteTextFile($"{projectRoot}/README.md", GeneratedBlazorAppReadme, overwrite: true);

        await progressCallback(
            ExecutionState.Running,
            "Project build",
            "Building the generated Blazor app through the controlled command surface.");
        var buildResult = await commandExecutionService.DotnetBuild(
            projectPath,
            configuration: "Debug",
            noRestore: false,
            workingDirectory: projectRoot,
            timeoutSeconds: 600);
        EnsureSucceeded(buildResult.Succeeded, buildResult.Message + Environment.NewLine + buildResult.StderrPreview);

        var reportMarkdown =
            $"""
            # SC03 Generated Blazor App Delivery

            The scenario generated a Blazor Web App under `{projectRoot}` and replaced the home page with a small interactive work-item surface.

            ## Included outputs
            - Spec: `{specPath}`
            - App page: `{projectRoot}/Components/Pages/Home.razor`
            - Run guide: `{projectRoot}/README.md`
            - Build target: `{projectPath}`

            ## Build result
            {TrimForMarkdown(buildResult.StdoutPreview, 1200)}
            """;

        fileService.WriteTextFile($"{state.ArtifactRoot}/generation-report.md", reportMarkdown, overwrite: true);

        return new ScenarioExecutionOutcome(
            ResponseText: "SC03 completed with a generated Blazor app project and a successful controlled build receipt.",
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

    private async Task<ScenarioExecutionOutcome> ExecuteGeneratedAppReviewAsync(
        ScenarioRuntimeState state,
        Func<ExecutionState, string, string, Task> progressCallback)
    {
        await progressCallback(
            ExecutionState.Running,
            "Generated app review",
            "Inspecting the generated app delivery to produce a durable review report.");

        var projectRoot = ResolveGeneratedAppProjectRoot(state);
        var relativeProjectRoot = Path.GetRelativePath(workspaceRoot, projectRoot).Replace('\\', '/');
        var homePath = Path.Combine(projectRoot, "Components", "Pages", "Home.razor");
        var readmePath = Path.Combine(projectRoot, "README.md");
        var projectPath = Path.Combine(projectRoot, "GeneratedBlazorApp.csproj");

        if (!File.Exists(homePath) || !File.Exists(readmePath) || !File.Exists(projectPath))
        {
            throw new InvalidOperationException("SC10 could not find the generated app project to review.");
        }

        var homeContent = await File.ReadAllTextAsync(homePath);
        var readmeContent = await File.ReadAllTextAsync(readmePath);
        var findings = BuildGeneratedAppFindings(homeContent, readmeContent);
        var reportMarkdown =
            $"""
            # SC10 App Delivery Review

            The generated app delivery was inspected successfully.

            ## Project
            - Root: `{relativeProjectRoot}`
            - Project file: `{NormalizeRelativeWorkspacePath(projectPath)}`
            - App page: `{NormalizeRelativeWorkspacePath(homePath)}`
            - README: `{NormalizeRelativeWorkspacePath(readmePath)}`

            ## Review findings
            {string.Join(Environment.NewLine, findings.Select(item => $"- {item}"))}
            """;

        fileService.WriteTextFile($"{state.ArtifactRoot}/review-report.md", reportMarkdown, overwrite: true);

        return new ScenarioExecutionOutcome(
            ResponseText: "SC10 completed with a durable app delivery review report.",
            ResponseMarkdown: reportMarkdown,
            ToolCalls: 3);
    }

    private string ResolveGeneratedAppProjectRoot(ScenarioRuntimeState state)
    {
        if (!string.IsNullOrWhiteSpace(state.ProcessRunId))
        {
            var processScopedRoot = ToAbsoluteWorkspacePath(
                $"{ProcessScenarioOutputRoot}/{CreateProcessStorageKey(state.ProcessRunId)}/sc03/w/GeneratedBlazorApp");
            if (File.Exists(Path.Combine(processScopedRoot, "GeneratedBlazorApp.csproj")))
            {
                return processScopedRoot;
            }
        }

        var standaloneRoot = ToAbsoluteWorkspacePath($"{StandaloneScenarioOutputRoot}/sc03");
        if (Directory.Exists(standaloneRoot))
        {
            var latestProject = Directory.EnumerateFiles(standaloneRoot, "GeneratedBlazorApp.csproj", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(latestProject))
            {
                return Path.GetDirectoryName(latestProject)!;
            }
        }

        throw new InvalidOperationException("No generated app project is available for SC10.");
    }

    private static IReadOnlyList<string> BuildGeneratedAppFindings(string homeContent, string readmeContent)
    {
        var findings = new List<string>();
        if (homeContent.Contains("Add item", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add("The app page includes an action that adds a work item.");
        }

        if (homeContent.Contains("Mark first done", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add("The app page includes a state-changing completion action.");
        }

        if (homeContent.Contains("items.Count", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add("The app page includes dynamic list state.");
        }

        if (readmeContent.Contains("dotnet run", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add("The README explains how to run the generated app.");
        }

        if (findings.Count == 0)
        {
            findings.Add("The app delivery exists, but the review did not find the expected interaction markers.");
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
            : AnsiEscapeSequenceRegex().Replace(content, string.Empty);

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
