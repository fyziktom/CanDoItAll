using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework.Hosting;

internal enum ScenarioAutomationMode
{
    Automated,
    Guided
}

internal sealed record ScenarioHarnessDefinition(
    string Id,
    string Title,
    string Summary,
    string Prompt,
    ScenarioAutomationMode Mode,
    bool RequiresApproval,
    IReadOnlyList<string> CoverageTags,
    IReadOnlyList<string> AssetPaths,
    string EvidenceNote);

internal sealed record ScenarioHarnessContext(
    Guid ProviderId,
    Guid AgentId);

internal sealed record ScenarioHarnessSnapshot(
    ScenarioHarnessDefinition Definition,
    ExecutionRunRecord? LatestRun,
    ExecutionRunDetail? LatestDetail,
    string ResponsePreview,
    string ResponseArtifactPath,
    IReadOnlyList<ExecutionRunRecord> RecentRuns)
{
    public bool HasRun => LatestRun is not null;

    public bool HasPendingApprovals =>
        (LatestDetail?.Approvals.Any(item => item.Status == ExecutionApprovalStatus.Pending) ?? false) ||
        (LatestRun?.PendingApprovals.Count > 0);

    public string LatestStatusLabel => LatestRun is null
        ? "Not run yet"
        : LatestRun.State switch
        {
            ExecutionState.Completed => "Completed",
            ExecutionState.WaitingOnTool => "Waiting on approval",
            ExecutionState.Failed => "Failed",
            _ => LatestRun.State.ToString()
        };
}

internal static class ScenarioHarnessCatalog
{
    public const string ProviderBaseUrl = "scenario://harness";
    public const string ProviderName = "Scenario Harness Provider";
    public const string AgentName = "Scenario Harness Operator";
    public const string AgentTag = "scenario-harness";

    public static IReadOnlyList<ScenarioHarnessDefinition> Definitions { get; } =
    [
        new(
            "SC01",
            "Email PDF Summary",
            "Convert a captured email packet into summary evidence and action notes.",
            "SC01 - Convert the bundled meeting-email PDF into executive notes, tasks, risks, and unresolved questions.",
            ScenarioAutomationMode.Guided,
            false,
            ["document", "artifacts", "receipts"],
            ["Generated through integrated proof protocol."],
            "Guided proof only. Keep the evidence tied to the integrated execution report rather than a fake seeded result."),
        new(
            "SC02",
            "BOM Versus Quote",
            "Compare BOM and supplier quote workbooks and preserve the variance evidence.",
            "SC02 - Compare the BOM workbook to the supplier quote workbook and save a variance report with supporting evidence.",
            ScenarioAutomationMode.Guided,
            false,
            ["spreadsheet", "artifacts", "comparison"],
            ["Generated through integrated proof protocol."],
            "Guided proof only. Keep workbook evidence attached to the execution report."),
        new(
            "SC03",
            "Generated Blazor App Delivery",
            "Generate a Blazor Web App, replace the home page with a small interactive surface, and build it through the controlled command surface.",
            "SC03 - Generate a .NET 10 Blazor Web App, turn the home page into a compact work-item surface, write a README, and build it with the standard dotnet recipe surface.",
            ScenarioAutomationMode.Automated,
            false,
            ["coding", "file", "dotnet"],
            ["Generated inline input/spec.md"],
            "Run from the integrated scenarios tab or through the process-centric proof. Verify project files, README, and controlled build receipts."),
        new(
            "SC04",
            "Approval Pause And Resume",
            "Pause on a guarded mutation, persist approval state, then resume into a controlled follow-up action.",
            "SC04 - Trigger the approval-gated output mutation, wait for approval, then continue and capture the approval-proof artifact and receipt trail.",
            ScenarioAutomationMode.Automated,
            true,
            ["approval", "checkpoint", "resume"],
            ["Generated inline approval task note"],
            "Run from the integrated scenarios tab and verify the pending approval, continuation, checkpoints, and follow-up receipts."),
        new(
            "SC05",
            "Python Analysis With Artifacts",
            "Analyze a CSV dataset and preserve report, JSON summary, and chart evidence.",
            "SC05 - Analyze a bundled CSV file, compute totals by dimension, and save markdown, JSON, and chart artifacts.",
            ScenarioAutomationMode.Guided,
            false,
            ["python", "artifacts", "chart"],
            ["Generated through integrated proof protocol."],
            "Guided proof only. Keep the evidence attached to the execution report."),
        new(
            "SC06",
            "PowerShell Repo Inventory",
            "Run a low-risk repository inventory task through the reviewed PowerShell recipe boundary.",
            "SC06 - Execute the low-risk PowerShell inventory task, summarize markdown and project coverage, and save the report artifacts.",
            ScenarioAutomationMode.Guided,
            false,
            ["powershell", "receipts", "artifacts"],
            ["Generated through integrated proof protocol."],
            "Guided proof only. Preserve the PowerShell receipts in the execution report."),
        new(
            "SC07",
            "Restart And Resume",
            "Reuse the approval scenario while explicitly restarting the app before approving the persisted checkpoint.",
            "SC07 - Validate restart and resume by starting SC04, restarting the app, reopening the integrated shell, and approving the still-pending run.",
            ScenarioAutomationMode.Guided,
            true,
            ["restart", "durability", "approval"],
            ["Reuse SC04 approval evidence."],
            "Manual-only protocol. The point is durable recovery, not fake automation."),
        new(
            "SC08",
            "Provider-Native Versus Local Comparison",
            "Compare provider-native hosted tools against the local controlled path and keep unsupported cases honest.",
            "SC08 - Compare provider-native hosted tool behavior versus the equivalent local controlled path and preserve the difference honestly.",
            ScenarioAutomationMode.Guided,
            false,
            ["provider-native", "comparison", "proof"],
            ["Execution report only."],
            "Manual-only protocol. Do not fake support where the provider does not actually offer it."),
        new(
            "SC09",
            "Launch Staffing And Approval",
            "Validate staffing recommendations, role selection, and approval routing through the process launch UI.",
            "SC09 - Create a launch plan, resolve role candidates, submit it for approval, and verify the approval trail through the integrated processes shell.",
            ScenarioAutomationMode.Guided,
            false,
            ["process-launch", "staffing", "approval"],
            ["Process launch UI proof."],
            "Guided process-centric proof. This must run through `/processes`, not through seeded final state."),
        new(
            "SC10",
            "Generated App Delivery Review",
            "Inspect a generated app delivery and produce a review artifact from the integrated runtime.",
            "SC10 - Review the generated Blazor app project, confirm the app assets exist, and save a concise review report.",
            ScenarioAutomationMode.Automated,
            false,
            ["review", "artifacts", "generated-app"],
            ["Depends on a generated Blazor app project."],
            "Automated when a generated app project already exists. In the process proof it should run as the review step after SC03."),
        new(
            "SC11",
            "Multi-Agent App Delivery Process",
            "Run the full process-centric app delivery workflow across staffing, approval, agent execution, and messaging.",
            "SC11 - Define and execute the multi-agent app delivery process through launch, approval, execution, messaging, and completion.",
            ScenarioAutomationMode.Guided,
            false,
            ["process", "multi-agent", "app-delivery"],
            ["Playwright proof route through `/processes` and `/agents`."],
            "Guided process-centric proof. This is the true closure scenario for the integrated system.")
    ];

    public static bool TryGetDefinition(string? scenarioId, out ScenarioHarnessDefinition definition)
    {
        definition = Definitions.FirstOrDefault(item => string.Equals(item.Id, scenarioId, StringComparison.OrdinalIgnoreCase))
            ?? Definitions[0];

        return Definitions.Any(item => string.Equals(item.Id, scenarioId, StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed class ScenarioHarnessService(ICanDoItAllAgentWorkspaceFactory workspaceFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public IReadOnlyList<ScenarioHarnessDefinition> Definitions => ScenarioHarnessCatalog.Definitions;

    public async Task<ScenarioHarnessContext> EnsureScenarioCatalogAsync(CancellationToken cancellationToken = default)
    {
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var providers = await workspaceService.ListProvidersAsync(cancellationToken);
        var provider = providers.FirstOrDefault(item =>
            string.Equals(item.BaseUrl, ScenarioHarnessCatalog.ProviderBaseUrl, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            var providerEditor = await workspaceService.GetProviderEditorAsync(cancellationToken: cancellationToken);
            providerEditor.Name = ScenarioHarnessCatalog.ProviderName;
            providerEditor.Kind = ProviderKind.OpenAi;
            providerEditor.BaseUrl = ScenarioHarnessCatalog.ProviderBaseUrl;
            providerEditor.ApiKeyEnvironmentVariable = string.Empty;
            providerEditor.DefaultModel = "scenario-local";
            providerEditor.Transport = ProviderTransportKind.Responses;
            providerEditor.IsEnabled = true;
            providerEditor.SupportsStreaming = false;
            providerEditor.SupportsTools = true;
            providerEditor.PreferFrameworkManagedChatHistory = true;
            providerEditor.SupportsBackgroundResponses = false;
            providerEditor.ConfigurationJson = "{}";
            providerEditor.Notes = "Deterministic scenario provider for integrated AgentFramework proof.";
            providerEditor.SuggestedModels =
            [
                "scenario-local"
            ];

            var providerId = await workspaceService.SaveProviderAsync(providerEditor, cancellationToken);
            providers = await workspaceService.ListProvidersAsync(cancellationToken);
            provider = providers.First(item => item.Id == providerId);
        }

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken);
        var agent = agents.FirstOrDefault(item =>
            item.ProviderProfileId == provider.Id &&
            item.Tags.Contains(ScenarioHarnessCatalog.AgentTag, StringComparer.OrdinalIgnoreCase));

        if (agent is null)
        {
            var agentEditor = await workspaceService.GetAgentEditorAsync(cancellationToken: cancellationToken);
            agentEditor.Name = ScenarioHarnessCatalog.AgentName;
            agentEditor.RoleTitle = "Scenario Operator";
            agentEditor.Summary = "Runs deterministic AgentFramework proof scenarios through the real execution, approval, artifact, and receipt seams.";
            agentEditor.Instructions = "Run only the explicit scenario prompts. Keep outputs concise and always preserve durable evidence.";
            agentEditor.Status = AgentLifecycleStatus.Active;
            agentEditor.ProviderProfileId = provider.Id;
            agentEditor.Model = "scenario-local";
            agentEditor.Workload = AgentWorkloadKind.Programming;
            agentEditor.ChatHistoryMode = AgentChatHistoryMode.ProviderDefault;
            agentEditor.Temperature = 0d;
            agentEditor.RequirePerServiceCallChatHistoryPersistence = false;
            agentEditor.EnableBackgroundResponses = false;
            agentEditor.ConfigurationJson = JsonSerializer.Serialize(
                new
                {
                    scenarioHarness = true
                },
                JsonOptions);
            agentEditor.IsTemplate = false;
            agentEditor.TemplateKey = string.Empty;
            agentEditor.Permissions = AgentPermissionsPolicy.Default with
            {
                CanUseTools = true,
                RequiresApprovalForExternalCalls = true,
                AutoApproveExternalCallsByDefault = false
            };
            agentEditor.SelectedCapabilityIds = [];
            agentEditor.Tags =
            [
                ScenarioHarnessCatalog.AgentTag,
                "agentframework-full-integration"
            ];

            var agentId = await workspaceService.SaveAgentAsync(agentEditor, cancellationToken);
            agents = await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken);
            agent = agents.First(item => item.Id == agentId);
        }

        return new ScenarioHarnessContext(provider.Id, agent.Id);
    }

    public async Task<ScenarioHarnessSnapshot> LoadScenarioSnapshotAsync(
        Guid agentId,
        string scenarioId,
        CancellationToken cancellationToken = default)
    {
        var definition = ResolveDefinition(scenarioId);
        return await LoadScenarioSnapshotAsync(agentId, definition, preferredExecutionRunId: null, cancellationToken);
    }

    private async Task<ScenarioHarnessSnapshot> LoadScenarioSnapshotAsync(
        Guid agentId,
        ScenarioHarnessDefinition definition,
        Guid? preferredExecutionRunId,
        CancellationToken cancellationToken)
    {
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var recentRuns = definition.Mode == ScenarioAutomationMode.Guided
            ? []
            : await workspaceService.ListExecutionRunsAsync(
                new ExecutionRunQuery(
                    AgentId: agentId,
                    SourceKind: "scenario-harness",
                    SourceId: definition.Id,
                    Take: 6),
                cancellationToken);

        ExecutionRunDetail? latestDetail = null;
        ExecutionRunRecord? latestRun = null;

        if (preferredExecutionRunId.HasValue)
        {
            latestDetail = await workspaceService.GetExecutionRunDetailAsync(preferredExecutionRunId.Value, cancellationToken);
            latestRun = latestDetail.Run;
        }
        else
        {
            latestRun = recentRuns.FirstOrDefault();
            latestDetail = latestRun is null
                ? null
                : await workspaceService.GetExecutionRunDetailAsync(latestRun.Id, cancellationToken);
        }

        var responseArtifactPath = latestDetail?.Artifacts
            .FirstOrDefault(item => item.RelativePath.EndsWith("/response.md", StringComparison.OrdinalIgnoreCase))
            ?.RelativePath
            ?? string.Empty;

        var responsePreview = string.Empty;
        if (!string.IsNullOrWhiteSpace(responseArtifactPath))
        {
            var fileService = new WorkspaceFileService(workspaceFactory.GetWorkspaceRoot(), workspaceFactory.GetOrganizationScope());
            var readResult = fileService.ReadTextFile(responseArtifactPath, 8000);
            if (readResult.Succeeded)
            {
                responsePreview = readResult.Content;
            }
        }

        return new ScenarioHarnessSnapshot(
            definition,
            latestRun,
            latestDetail,
            responsePreview,
            responseArtifactPath,
            recentRuns);
    }

    public async Task<ScenarioHarnessSnapshot> RunScenarioAsync(
        Guid agentId,
        string scenarioId,
        CancellationToken cancellationToken = default)
    {
        var definition = ResolveDefinition(scenarioId);
        if (definition.Mode != ScenarioAutomationMode.Automated)
        {
            throw new InvalidOperationException($"{definition.Id} is a guided proof scenario and must be executed through the documented integrated flow.");
        }

        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var correlationId = $"{definition.Id.ToLowerInvariant()}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        var runResult = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                AgentId: agentId,
                Prompt: definition.Prompt,
                InitialActivityOperationId: AgentExecutionOperationId.New(),
                Context: new ExecutionInvocationContext(
                    SourceKind: "scenario-harness",
                    SourceId: definition.Id,
                    CorrelationId: correlationId,
                    CausationId: string.Empty,
                    RequestedBy: "integrated-scenario-harness",
                    RequestedByKind: "interactive",
                    MetadataJson: JsonSerializer.Serialize(
                        new
                        {
                            scenarioId = definition.Id,
                            mode = definition.Mode.ToString()
                        },
                        JsonOptions))),
            cancellationToken);

        return await LoadScenarioSnapshotAsync(agentId, definition, runResult.ExecutionRunId, cancellationToken);
    }

    public async Task<ScenarioHarnessSnapshot> ContinueScenarioAsync(
        Guid agentId,
        string scenarioId,
        Guid executionRunId,
        bool approved,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default)
    {
        var definition = ResolveDefinition(scenarioId);
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        await workspaceService.ContinueExecutionRunAsync(
            executionRunId,
            AgentExecutionOperationId.New(),
            approved,
            autoApprovePendingToolCalls,
            cancellationToken);

        return await LoadScenarioSnapshotAsync(agentId, definition, executionRunId, cancellationToken);
    }

    private static ScenarioHarnessDefinition ResolveDefinition(string scenarioId)
    {
        if (!ScenarioHarnessCatalog.TryGetDefinition(scenarioId, out var definition))
        {
            throw new InvalidOperationException($"Unknown scenario '{scenarioId}'.");
        }

        return definition;
    }
}
