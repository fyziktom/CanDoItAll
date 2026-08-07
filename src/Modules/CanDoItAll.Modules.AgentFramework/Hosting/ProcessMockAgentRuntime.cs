using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.AgentFramework.Hosting;

/// <summary>
/// Deterministic process-mock interception core. SB10 introduced <see cref="ProcessMockExecutionDecorator"/>
/// and <see cref="ProcessMockDiagnosticsDecorator"/> as the port-level classes that decide whether a
/// request targets the process-mock provider; this type owns exactly the deterministic mock bodies
/// those decorators call into. It no longer implements the broad legacy runtime interface (deleted
/// SB18) or holds an inner fallback runtime — decorators own the provider-matching branch and the
/// inner-port fallback themselves.
/// </summary>
internal sealed partial class ProcessMockAgentRuntime(
    IWorkspaceFileService fileService,
    string workspaceRoot,
    IOptions<ProcessMockAgentOptions> options)
{
    private const string WorkspaceStatPathToolName = "workspace_stat_path";
    private const string WorkspaceReadFileToolName = "workspace_read_file";
    private const string BrowserScreenshotToolName = "browser_take_screenshot";
    private const string BrowserSnapshotToolName = "browser_snapshot";
    private const string BrowserConsoleMessagesToolName = "browser_console_messages";
    private const string BrowserEvaluateToolName = "browser_evaluate";

    [GeneratedRegex(
        @"(?<path>\b(?:artifacts|output|integration-map|data)/[^\s`'""<>()\[\]{},;]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ManagedWorkspacePathRegex();

    [GeneratedRegex(
        @"^-\s+(?<title>.+?)\s+\[(?<kind>[^\]]+)\](?:\s+required)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RequiredArtifactLineRegex();

    [GeneratedRegex(
        @"^-\s+(?:(?<key>[^\s()]+)\s+\((?<title>[^)]+)\)|(?<titleOnly>[^:]+))(?:\:\s*(?<description>.*))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BranchOutcomeLineRegex();

    [GeneratedRegex(@"`(?<toolName>[^`]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex RequiredToolNameRegex();

    private static readonly byte[] MockBrowserScreenshotPngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    internal Task<ProviderHealthResult> CreateProcessMockProviderHealthResultAsync()
    {
        return Task.FromResult(options.Value.Enabled
            ? new ProviderHealthResult(
                Success: true,
                Summary: "Process mock provider is enabled for deterministic process automation flow tuning.",
                SuggestedModels:
                [
                    ProcessMockAgentCatalog.Model
                ])
            : new ProviderHealthResult(
                Success: false,
                Summary: $"Process mock provider is disabled. Set {ProcessMockAgentOptions.SectionName}:Enabled to true before executing mock agents.",
                SuggestedModels: []));
    }

    internal Task<ProviderTestChatResult> CreateProcessMockProviderTestChatResultAsync()
    {
        EnsureEnabled();

        return Task.FromResult(new ProviderTestChatResult(
            Model: ProcessMockAgentCatalog.Model,
            ResponseText: "Process mock provider is deterministic. Use role-specific mock agents in a generic delivery process to exercise QA repair loops.",
            InputTokens: 16,
            OutputTokens: 22));
    }

    internal static InvalidOperationException CreateModelMaintenanceUnsupportedError()
        => new("The process mock provider does not support provider model maintenance.");

    internal static InvalidOperationException CreateApprovalContinuationUnsupportedError()
        => new("Process mock agents do not use pending approval continuations.");

    internal async Task<AgentRuntimeResponse> ExecuteProcessMockRunAsync(
        AgentDefinition agent,
        string prompt,
        string? runtimeSessionKey,
        Func<ExecutionState, string, string, Task> progressCallback,
        AgentStructuredOutputContract? structuredOutput,
        AgentRuntimeExecutionOptions? executionOptions)
    {
        EnsureEnabled();
        var roleKey = ProcessMockAgentCatalog.ResolveRoleKey(agent);
        if (string.IsNullOrWhiteSpace(roleKey))
        {
            throw new InvalidOperationException($"Process mock agent '{agent.Name}' is missing a '{ProcessMockAgentCatalog.RoleTagPrefix}' role tag.");
        }

        await progressCallback(
            ExecutionState.Running,
            "Process mock execution",
            $"Running deterministic process mock role '{roleKey}'.");

        var state = CreateState(prompt, runtimeSessionKey);
        var outcome = roleKey switch
        {
            ProcessMockAgentRoleKeys.ProductOwner => ExecuteProductOwner(state),
            ProcessMockAgentRoleKeys.Architect => ExecuteArchitect(state),
            ProcessMockAgentRoleKeys.Developer => ExecuteDeveloper(state),
            ProcessMockAgentRoleKeys.Qa => ExecuteQa(state),
            ProcessMockAgentRoleKeys.RepairDeveloper => ExecuteRepairDeveloper(state),
            ProcessMockAgentRoleKeys.ReleaseManager => ExecuteReleaseManager(state),
            ProcessMockAgentRoleKeys.BusinessStrategist => ExecuteBusinessStrategist(state),
            ProcessMockAgentRoleKeys.FinancialStrategist => ExecuteFinancialStrategist(state),
            ProcessMockAgentRoleKeys.MarketingSpecialist => ExecuteMarketingSpecialist(state),
            _ => throw new InvalidOperationException($"Unsupported process mock role '{roleKey}'.")
        };
        var inspectedArtifactPaths = ResolveArtifactInspectionPaths(prompt);
        var requiredToolNames = ResolvePromptRequiredToolNames(prompt);

        await progressCallback(
            ExecutionState.Persisting,
            "Process mock artifacts",
            $"Saved deterministic mock artifacts under {state.ArtifactRoot}.");
        await progressCallback(
            ExecutionState.Completed,
            "Process mock complete",
            outcome.ProgressSummary);

        return new AgentRuntimeResponse(
            ResponseText: outcome.ResponseText,
            InputTokens: EstimateTokens(prompt),
            OutputTokens: EstimateTokens(outcome.ResponseText),
            ToolCalls: outcome.ToolCalls,
            RuntimeSessionKey: state.RuntimeSessionKey,
            SerializedSessionStateJson: BuildSerializedSessionState(roleKey, state, outcome, inspectedArtifactPaths, requiredToolNames),
            PendingApprovals: [])
        {
            FinalizerInvocations = BuildProcessStepOutcomeFinalizerInvocations(structuredOutput, executionOptions, outcome.ResponseText),
            ToolInvocationTraces = BuildProcessStepOutcomeToolInvocationTraces(requiredToolNames, structuredOutput, executionOptions)
        };
    }

    private ProcessMockRuntimeState CreateState(string prompt, string? runtimeSessionKey)
    {
        var auditScope = WorkspaceExecutionAuditContext.Current;
        var runKey = ResolveRunKey(auditScope);
        return new ProcessMockRuntimeState(
            OriginalPrompt: prompt,
            RuntimeSessionKey: string.IsNullOrWhiteSpace(runtimeSessionKey)
                ? $"process-mock-{runKey}-{Guid.NewGuid():N}"
                : runtimeSessionKey,
            RunKey: runKey,
            ArtifactRoot: $"{ProcessMockAgentCatalog.ArtifactRoot}/{runKey}",
            OutputRoot: $"{ProcessMockAgentCatalog.OutputRoot}/{runKey}",
            ProcessCooperationMode: auditScope?.ProcessCooperationMode?.ToString() ?? string.Empty,
            WorkspaceToolProfileOverride: auditScope?.WorkspaceToolProfileOverride is null
                ? string.Empty
                : AgentWorkspaceToolAccessProfiles.GetProfileKey(auditScope.WorkspaceToolProfileOverride.Value));
    }

    private ProcessMockRuntimeOutcome ExecuteProductOwner(ProcessMockRuntimeState state)
    {
        fileService.CreateDirectory(state.ArtifactRoot);
        var scopePath = $"{state.ArtifactRoot}/01-scope.md";
        var markdown =
            """
            # Generic Delivery Scope

            Build a small validation component that normalizes a user-provided name.

            ## Acceptance Criteria
            - Non-empty names are trimmed before use.
            - Blank names produce an explicit validation failure.
            - QA must reject an implementation that accepts blank input.
        """;
        fileService.WriteTextFile(scopePath, markdown, overwrite: true);

        var artifacts = new List<ProcessMockRuntimeArtifact>
        {
            CreateArtifact(scopePath, "mock sample scope artifact acceptance criteria validation blank input")
        };
        var promptSections = BuildPromptRequiredArtifactSections(state, "01", artifacts);
        var responseSummary = MergeSummary(
            "Scope captured for the deterministic mock delivery process.",
            promptSections);

        return BuildOutcome(
            responseSummary,
            "Completed",
            "Mock scope and acceptance criteria were written.",
            ResolveAcceptingBranchOutcomeKey(state.OriginalPrompt),
            "Product owner mock scope artifact saved.",
            artifacts);
    }

    private ProcessMockRuntimeOutcome ExecuteArchitect(ProcessMockRuntimeState state)
    {
        fileService.CreateDirectory(state.ArtifactRoot);
        var architecturePath = $"{state.ArtifactRoot}/02-architecture.md";
        var markdown =
            """
            # Generic Delivery Architecture

            Use a small application boundary:

            - `ValidationEngine` owns input validation and normalization rules.
            - UI orchestration calls the engine and displays validation feedback.
            - QA verifies blank-input behavior before release.
        """;
        fileService.WriteTextFile(architecturePath, markdown, overwrite: true);

        var artifacts = new List<ProcessMockRuntimeArtifact>
        {
            CreateArtifact(architecturePath, "mock sample architecture artifact boundary implementation qa expectations")
        };
        var promptSections = BuildPromptRequiredArtifactSections(state, "02", artifacts);
        var responseSummary = MergeSummary(
            "Architecture captured for the deterministic mock delivery process.",
            promptSections);

        return BuildOutcome(
            responseSummary,
            "Completed",
            "Mock architecture guidance was written.",
            ResolveAcceptingBranchOutcomeKey(state.OriginalPrompt),
            "Architect mock handoff artifact saved.",
            artifacts);
    }

    private ProcessMockRuntimeOutcome ExecuteDeveloper(ProcessMockRuntimeState state)
    {
        fileService.CreateDirectory(state.ArtifactRoot);
        fileService.CreateDirectory($"{state.OutputRoot}/MockApp");

        var implementationPath = $"{state.ArtifactRoot}/03-implementation.md";
        var enginePath = $"{state.OutputRoot}/MockApp/ValidationEngine.cs";
        fileService.WriteTextFile(enginePath, FirstPassValidationEngine, overwrite: true);
        var artifacts = new List<ProcessMockRuntimeArtifact>
        {
            CreateArtifact(
                implementationPath,
                "mock sample first implementation artifact deliverable deterministic defect")
        };

        var markdown =
            $"""
            # Mock Implementation

            The first-pass validation engine was written to `{enginePath}`.

            ## Known Mock Defect
            This deterministic first pass intentionally accepts blank input so QA can send the work back for repair.
            """;
        fileService.WriteTextFile(implementationPath, markdown, overwrite: true);
        var responseSummary = "First-pass mock implementation completed with the deterministic QA defect.";
        var requiredArtifactSections = BuildImplementationRequiredArtifactSections(state, repaired: false, artifacts);
        if (!string.IsNullOrWhiteSpace(requiredArtifactSections))
        {
            responseSummary = requiredArtifactSections;
        }

        var promptSections = BuildPromptRequiredArtifactSections(state, "03", artifacts);
        responseSummary = MergeSummary(responseSummary, promptSections);

        return BuildOutcome(
            responseSummary,
            "Completed",
            "First-pass mock implementation artifact was written.",
            ResolveAcceptingBranchOutcomeKey(state.OriginalPrompt),
            "Developer mock implementation artifact saved.",
            artifacts);
    }

    private ProcessMockRuntimeOutcome ExecuteQa(ProcessMockRuntimeState state)
    {
        return ShouldApproveQaPrompt(state.OriginalPrompt)
            ? ExecuteQaApproval(state)
            : ExecuteQaRejection(state);
    }

    private ProcessMockRuntimeOutcome ExecuteQaRejection(ProcessMockRuntimeState state)
    {
        fileService.CreateDirectory(state.ArtifactRoot);
        var findingPath = $"{state.ArtifactRoot}/04-qa-finding.md";
        var markdown =
            """
            # QA Finding

            QA rejects the first-pass mock implementation.

            ## Blocking Defect
            `ValidationEngine.NormalizeName` trims input but does not explicitly reject blank values. Repair is required before approval.
        """;
        fileService.WriteTextFile(findingPath, markdown, overwrite: true);

        var artifacts = new List<ProcessMockRuntimeArtifact>
        {
            CreateArtifact(findingPath, "mock sample qa rejection artifact finding repair branch reason")
        };
        var promptSections = BuildPromptRequiredArtifactSections(state, "04", artifacts);
        var responseSummary = MergeSummary(
            "QA rejected the first-pass mock implementation and selected the repair branch.",
            promptSections);

        return BuildOutcome(
            responseSummary,
            "Completed",
            "Blank-input handling is missing; repair is required.",
            ResolveRepairBranchOutcomeKey(state.OriginalPrompt) ?? ProcessMockAgentCatalog.BranchRepairsRequired,
            "QA mock rejection artifact saved.",
            artifacts);
    }

    private ProcessMockRuntimeOutcome ExecuteRepairDeveloper(ProcessMockRuntimeState state)
    {
        fileService.CreateDirectory(state.ArtifactRoot);
        fileService.CreateDirectory($"{state.OutputRoot}/MockApp");

        var repairPath = $"{state.ArtifactRoot}/05-repair.md";
        var enginePath = $"{state.OutputRoot}/MockApp/ValidationEngine.cs";
        fileService.WriteTextFile(enginePath, RepairedValidationEngine, overwrite: true);
        var artifacts = new List<ProcessMockRuntimeArtifact>
        {
            CreateArtifact(
                repairPath,
                "mock sample repair artifact implementation blank input fix")
        };

        var markdown =
            $"""
            # Mock Repair

            The validation engine was repaired at `{enginePath}`.

            ## Repair
            `ValidationEngine.NormalizeName` now throws `ArgumentException` when the input is blank.
            """;
        fileService.WriteTextFile(repairPath, markdown, overwrite: true);
        var responseSummary = "Blank-input validation repair completed.";
        var requiredArtifactSections = BuildImplementationRequiredArtifactSections(state, repaired: true, artifacts);
        if (!string.IsNullOrWhiteSpace(requiredArtifactSections))
        {
            responseSummary = requiredArtifactSections;
        }

        var promptSections = BuildPromptRequiredArtifactSections(state, "05", artifacts);
        responseSummary = MergeSummary(responseSummary, promptSections);

        return BuildOutcome(
            responseSummary,
            "Completed",
            "Blank-input repair artifact was written.",
            ResolveAcceptingBranchOutcomeKey(state.OriginalPrompt),
            "Repair developer mock artifact saved.",
            artifacts);
    }

    private ProcessMockRuntimeOutcome ExecuteQaApproval(ProcessMockRuntimeState state)
    {
        fileService.CreateDirectory(state.ArtifactRoot);
        var approvalPath = $"{state.ArtifactRoot}/06-qa-approval.md";
        var markdown =
            """
            # QA Approval

            QA approves the repaired mock implementation.

            ## Verified Behavior
            - Non-empty values are normalized.
            - Blank input is handled explicitly.
            - Repair evidence is ready for release notes.
        """;
        fileService.WriteTextFile(approvalPath, markdown, overwrite: true);

        var artifacts = new List<ProcessMockRuntimeArtifact>
        {
            CreateArtifact(approvalPath, "mock sample qa approval artifact repaired implementation release")
        };
        var promptSections = BuildPromptRequiredArtifactSections(state, "06", artifacts);
        var responseSummary = MergeSummary(
            "QA approved the repaired mock implementation and selected the approval branch.",
            promptSections);

        return BuildOutcome(
            responseSummary,
            "Completed",
            "Repaired mock implementation passed QA.",
            ResolveAcceptingBranchOutcomeKey(state.OriginalPrompt) ?? ProcessMockAgentCatalog.BranchApproved,
            "QA mock approval artifact saved.",
            artifacts);
    }

    private ProcessMockRuntimeOutcome ExecuteReleaseManager(ProcessMockRuntimeState state)
    {
        fileService.CreateDirectory(state.ArtifactRoot);
        var releasePath = $"{state.ArtifactRoot}/07-release-notes.md";
        var markdown =
            """
            # Generic Delivery Release Notes

            The mock delivery process completed with a deterministic QA repair loop.

            ## Release Summary
            - Scope and architecture were captured.
            - First implementation was rejected by QA.
            - Repair developer fixed blank-input behavior.
            - QA approved the repaired output.
        """;
        fileService.WriteTextFile(releasePath, markdown, overwrite: true);

        var artifacts = new List<ProcessMockRuntimeArtifact>
        {
            CreateArtifact(releasePath, "mock sample release notes artifact qa approval repair evidence")
        };
        var promptSections = BuildPromptRequiredArtifactSections(state, "07", artifacts);
        var responseSummary = MergeSummary(
            "Release notes captured after deterministic QA approval.",
            promptSections);

        return BuildOutcome(
            responseSummary,
            "Completed",
            "Release notes were written after QA approval.",
            ResolveAcceptingBranchOutcomeKey(state.OriginalPrompt),
            "Release manager mock artifact saved.",
            artifacts);
    }

    private ProcessMockRuntimeOutcome ExecuteBusinessStrategist(ProcessMockRuntimeState state)
    {
        fileService.CreateDirectory(state.ArtifactRoot);
        var strategyPath = $"{state.ArtifactRoot}/08-business-strategy.md";
        var markdown =
            """
            # Business Strategy Plan

            The business strategist captured planning assumptions, product evidence, plan synthesis, integrated review, and approved handoff details.

            ## Business Planning Controls
            - Facts, assumptions, open questions, and risks are kept distinct.
            - Product evidence is reviewed before the business plan is synthesized.
            - Integrated review records the decision status and next actions.
            """;
        fileService.WriteTextFile(strategyPath, markdown, overwrite: true);

        var artifacts = new List<ProcessMockRuntimeArtifact>
        {
            CreateArtifact(
                strategyPath,
                "business strategy intake brief product evidence assessment business plan integrated business plan review approved plan handoff assumption inventory")
        };
        var promptSections = BuildPromptRequiredArtifactSections(state, "08", artifacts);
        var responseSummary = MergeSummary(
            "Business strategy artifacts were written for deterministic business-plan automation.",
            promptSections);

        return BuildOutcome(
            responseSummary,
            "Completed",
            "Business planning artifact was written.",
            ResolveAcceptingBranchOutcomeKey(state.OriginalPrompt),
            "Business strategist mock artifact saved.",
            artifacts);
    }

    private ProcessMockRuntimeOutcome ExecuteFinancialStrategist(ProcessMockRuntimeState state)
    {
        fileService.CreateDirectory(state.ArtifactRoot);
        var financialPath = $"{state.ArtifactRoot}/09-financial-model.md";
        var markdown =
            """
            # Financial Model And Sensitivity Note

            The financial strategist captured drivers, assumptions, ranges, data gaps, and sensitivity findings for the business plan.

            ## Financial Planning Controls
            - Forecasts identify drivers and confidence ranges.
            - Missing source data remains visible.
            - Downstream review can reuse the model assumptions.
            """;
        fileService.WriteTextFile(financialPath, markdown, overwrite: true);

        var artifacts = new List<ProcessMockRuntimeArtifact>
        {
            CreateArtifact(
                financialPath,
                "financial model sensitivity note drivers assumptions ranges data gaps business plan")
        };
        var promptSections = BuildPromptRequiredArtifactSections(state, "09", artifacts);
        var responseSummary = MergeSummary(
            "Financial planning artifact was written for deterministic business-plan automation.",
            promptSections);

        return BuildOutcome(
            responseSummary,
            "Completed",
            "Financial planning artifact was written.",
            ResolveAcceptingBranchOutcomeKey(state.OriginalPrompt),
            "Financial strategist mock artifact saved.",
            artifacts);
    }

    private ProcessMockRuntimeOutcome ExecuteMarketingSpecialist(ProcessMockRuntimeState state)
    {
        fileService.CreateDirectory(state.ArtifactRoot);
        var marketingPath = $"{state.ArtifactRoot}/10-marketing-plan.md";
        var markdown =
            """
            # Go-To-Market And Experiment Plan

            The marketing specialist captured audience, promise, channels, budget dependencies, metrics, and validation experiments.

            ## Marketing Planning Controls
            - Audience and channel assumptions are explicit.
            - Proof needs and validation experiments are named.
            - Marketing actions remain tied to business-plan constraints.
            """;
        fileService.WriteTextFile(marketingPath, markdown, overwrite: true);

        var artifacts = new List<ProcessMockRuntimeArtifact>
        {
            CreateArtifact(
                marketingPath,
                "go market experiment plan marketing audience promise channels metrics validation business plan")
        };
        var promptSections = BuildPromptRequiredArtifactSections(state, "10", artifacts);
        var responseSummary = MergeSummary(
            "Marketing planning artifact was written for deterministic business-plan automation.",
            promptSections);

        return BuildOutcome(
            responseSummary,
            "Completed",
            "Marketing planning artifact was written.",
            ResolveAcceptingBranchOutcomeKey(state.OriginalPrompt),
            "Marketing specialist mock artifact saved.",
            artifacts);
    }

    private static ProcessMockRuntimeOutcome BuildOutcome(
        string responseSummary,
        string status,
        string reason,
        string? branchOutcomeKey,
        string progressSummary,
        IReadOnlyList<ProcessMockRuntimeArtifact>? artifacts = null)
    {
        var responseText = BuildStructuredOutcome(status, reason, branchOutcomeKey, responseSummary, artifacts ?? []);
        return new ProcessMockRuntimeOutcome(
            ResponseText: responseText,
            ProgressSummary: progressSummary,
            BranchOutcomeKey: branchOutcomeKey,
            ToolCalls: 2,
            Artifacts: artifacts ?? []);
    }

    private static string ResolveRunKey(WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState? auditScope)
    {
        if (!string.IsNullOrWhiteSpace(auditScope?.ProcessRunId))
        {
            return NormalizeKey(auditScope.ProcessRunId);
        }

        if (auditScope is not null)
        {
            return NormalizeKey(auditScope.ExecutionRunId.ToString("N"));
        }

        return NormalizeKey(Guid.NewGuid().ToString("N"));
    }

    private static string NormalizeKey(string value)
    {
        var normalized = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return normalized.Length <= 16
            ? normalized
            : normalized[..16];
    }

    private static int EstimateTokens(string content)
        => Math.Max(1, (content ?? string.Empty).Length / 4);

    private void EnsureEnabled()
    {
        if (!options.Value.Enabled)
        {
            throw new InvalidOperationException($"Process mock agents are disabled. Set {ProcessMockAgentOptions.SectionName}:Enabled to true to execute them.");
        }
    }

    private const string FirstPassValidationEngine =
        """
        namespace MockApp;

        public sealed class ValidationEngine
        {
            public string NormalizeName(string value)
            {
                return value.Trim();
            }
        }
        """;

    private const string RepairedValidationEngine =
        """
        namespace MockApp;

        public sealed class ValidationEngine
        {
            public string NormalizeName(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("Value is required.", nameof(value));
                }

                return value.Trim();
            }
        }
        """;

    private sealed record ProcessMockRuntimeState(
        string OriginalPrompt,
        string RuntimeSessionKey,
        string RunKey,
        string ArtifactRoot,
        string OutputRoot,
        string ProcessCooperationMode,
        string WorkspaceToolProfileOverride);

    private sealed record ProcessMockRuntimeArtifact(
        string RelativePath,
        string ContentSignalText);

    private sealed record ProcessMockRuntimeOutcome(
        string ResponseText,
        string ProgressSummary,
        string? BranchOutcomeKey,
        int ToolCalls,
        IReadOnlyList<ProcessMockRuntimeArtifact> Artifacts);

    private sealed record PromptRequiredArtifact(
        string Title,
        string ArtifactKind,
        string ValidationRequirementSummary,
        string? ManagedPath,
        string? GovernedPath);

    private sealed record PromptBranchOutcome(
        string Key,
        string Title,
        string Description)
    {
        public string BranchOutcomeKey => string.IsNullOrWhiteSpace(Key) ? Title : Key;
    }

    private sealed class PromptRequiredArtifactBuilder(
        string title,
        string artifactKind)
    {
        public string Title { get; } = title;
        public string ArtifactKind { get; } = artifactKind;
        public string ValidationRequirementSummary { get; set; } = string.Empty;
        public string? ManagedPath { get; set; }
        public string? GovernedPath { get; set; }
    }
}
