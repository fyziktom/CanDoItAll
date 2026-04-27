using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.AgentFramework.Hosting;

internal sealed class ProcessMockAgentRuntime(
    IAgentRuntime inner,
    IWorkspaceFileService fileService,
    IOptions<ProcessMockAgentOptions> options) : IAgentRuntime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public Task<ProviderHealthResult> TestProviderAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken = default)
    {
        if (!ProcessMockAgentCatalog.IsProcessMockProvider(provider))
        {
            return inner.TestProviderAsync(provider, cancellationToken);
        }

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

    public Task<ProviderTestChatResult> RunProviderTestChatAsync(
        ProviderProfile provider,
        ProviderTestChatRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ProcessMockAgentCatalog.IsProcessMockProvider(provider))
        {
            return inner.RunProviderTestChatAsync(provider, request, cancellationToken);
        }

        EnsureEnabled();

        return Task.FromResult(new ProviderTestChatResult(
            Model: ProcessMockAgentCatalog.Model,
            ResponseText: "Process mock provider is deterministic. Use role-specific mock agents in a calculator process to exercise QA repair loops.",
            InputTokens: 16,
            OutputTokens: 22));
    }

    public Task<OllamaModelfileResult> CreateOrUpdateOllamaModelAsync(
        ProviderProfile provider,
        OllamaModelfileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ProcessMockAgentCatalog.IsProcessMockProvider(provider))
        {
            return inner.CreateOrUpdateOllamaModelAsync(provider, request, cancellationToken);
        }

        throw new InvalidOperationException("The process mock provider does not support Ollama model creation.");
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
        if (!ProcessMockAgentCatalog.IsProcessMockProvider(provider))
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
            _ => throw new InvalidOperationException($"Unsupported process mock role '{roleKey}'.")
        };

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
            SerializedSessionStateJson: JsonSerializer.Serialize(
                new
                {
                    processMockAgent = true,
                    roleKey,
                    state.RunKey,
                    state.ArtifactRoot,
                    outcome.BranchOutcomeKey,
                    artifacts = outcome.Artifacts.Select(artifact => new
                    {
                        artifact.RelativePath,
                        artifact.ContentSignalText
                    }).ToArray()
                },
                JsonOptions),
            PendingApprovals: [])
        {
            FinalizerInvocations = BuildProcessStepOutcomeFinalizerInvocations(structuredOutput, outcome.ResponseText)
        };
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
        if (!ProcessMockAgentCatalog.IsProcessMockProvider(provider))
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

        throw new InvalidOperationException("Process mock agents do not use pending approval continuations.");
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
            OutputRoot: $"{ProcessMockAgentCatalog.OutputRoot}/{runKey}");
    }

    private ProcessMockRuntimeOutcome ExecuteProductOwner(ProcessMockRuntimeState state)
    {
        fileService.CreateDirectory(state.ArtifactRoot);
        var scopePath = $"{state.ArtifactRoot}/01-scope.md";
        var markdown =
            """
            # Calculator Scope

            Build a simple calculator app with add, subtract, multiply, and divide operations.

            ## Acceptance Criteria
            - Addition, subtraction, multiplication, and division are supported.
            - Divide by zero produces an explicit validation failure.
            - QA must reject an implementation that lacks divide-by-zero handling.
            """;
        fileService.WriteTextFile(scopePath, markdown, overwrite: true);

        return BuildOutcome(
            "Scope captured for the deterministic calculator process.",
            "Completed",
            "Calculator scope and acceptance criteria were written.",
            null,
            "Product owner mock scope artifact saved.",
            [CreateArtifact(scopePath, "calculator scope artifact acceptance criteria arithmetic divide zero")]);
    }

    private ProcessMockRuntimeOutcome ExecuteArchitect(ProcessMockRuntimeState state)
    {
        fileService.CreateDirectory(state.ArtifactRoot);
        var architecturePath = $"{state.ArtifactRoot}/02-architecture.md";
        var markdown =
            """
            # Calculator Architecture

            Use a small application boundary:

            - `CalculatorEngine` owns arithmetic rules.
            - UI orchestration calls the engine and displays validation feedback.
            - QA verifies divide-by-zero behavior before release.
            """;
        fileService.WriteTextFile(architecturePath, markdown, overwrite: true);

        return BuildOutcome(
            "Architecture captured for the deterministic calculator process.",
            "Completed",
            "Calculator architecture guidance was written.",
            null,
            "Architect mock handoff artifact saved.",
            [CreateArtifact(architecturePath, "calculator architecture artifact boundary implementation qa expectations")]);
    }

    private ProcessMockRuntimeOutcome ExecuteDeveloper(ProcessMockRuntimeState state)
    {
        fileService.CreateDirectory(state.ArtifactRoot);
        fileService.CreateDirectory($"{state.OutputRoot}/CalculatorApp");

        var implementationPath = $"{state.ArtifactRoot}/03-implementation.md";
        var enginePath = $"{state.OutputRoot}/CalculatorApp/CalculatorEngine.cs";
        fileService.WriteTextFile(enginePath, FirstPassCalculatorEngine, overwrite: true);
        var artifacts = new List<ProcessMockRuntimeArtifact>
        {
            CreateArtifact(
                implementationPath,
                "calculator first implementation artifact deliverable deterministic defect")
        };

        var markdown =
            $"""
            # Calculator Implementation

            The first-pass calculator engine was written to `{enginePath}`.

            ## Known Mock Defect
            This deterministic first pass intentionally lacks explicit divide-by-zero handling so QA can send the work back for repair.
            """;
        fileService.WriteTextFile(implementationPath, markdown, overwrite: true);
        var responseSummary = "First-pass calculator implementation completed with the deterministic QA defect.";
        var requiredArtifactSections = BuildImplementationRequiredArtifactSections(state, repaired: false, artifacts);
        if (!string.IsNullOrWhiteSpace(requiredArtifactSections))
        {
            responseSummary = requiredArtifactSections;
        }

        return BuildOutcome(
            responseSummary,
            "Completed",
            "First-pass calculator implementation artifact was written.",
            null,
            "Developer mock implementation artifact saved.",
            artifacts);
    }

    private ProcessMockRuntimeOutcome ExecuteQa(ProcessMockRuntimeState state)
    {
        return IsApprovalQaPass(state.OriginalPrompt)
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

            QA rejects the first-pass calculator implementation.

            ## Blocking Defect
            `CalculatorEngine.Divide` divides directly by the denominator and does not explicitly reject zero. Repair is required before approval.
            """;
        fileService.WriteTextFile(findingPath, markdown, overwrite: true);

        return BuildOutcome(
            "QA rejected the first-pass calculator implementation and selected the repair branch.",
            "Completed",
            "Divide-by-zero handling is missing; repair is required.",
            ProcessMockAgentCatalog.BranchRepairsRequired,
            "QA mock rejection artifact saved.",
            [CreateArtifact(findingPath, "calculator qa rejection artifact finding repair branch reason")]);
    }

    private ProcessMockRuntimeOutcome ExecuteRepairDeveloper(ProcessMockRuntimeState state)
    {
        fileService.CreateDirectory(state.ArtifactRoot);
        fileService.CreateDirectory($"{state.OutputRoot}/CalculatorApp");

        var repairPath = $"{state.ArtifactRoot}/05-repair.md";
        var enginePath = $"{state.OutputRoot}/CalculatorApp/CalculatorEngine.cs";
        fileService.WriteTextFile(enginePath, RepairedCalculatorEngine, overwrite: true);
        var artifacts = new List<ProcessMockRuntimeArtifact>
        {
            CreateArtifact(
                repairPath,
                "calculator repair artifact implementation divide zero fix")
        };

        var markdown =
            $"""
            # Calculator Repair

            The calculator engine was repaired at `{enginePath}`.

            ## Repair
            `CalculatorEngine.Divide` now throws `DivideByZeroException` when the denominator is zero.
            """;
        fileService.WriteTextFile(repairPath, markdown, overwrite: true);
        var responseSummary = "Calculator divide-by-zero repair completed.";
        var requiredArtifactSections = BuildImplementationRequiredArtifactSections(state, repaired: true, artifacts);
        if (!string.IsNullOrWhiteSpace(requiredArtifactSections))
        {
            responseSummary = requiredArtifactSections;
        }

        return BuildOutcome(
            responseSummary,
            "Completed",
            "Divide-by-zero repair artifact was written.",
            null,
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

            QA approves the repaired calculator implementation.

            ## Verified Behavior
            - Arithmetic operations remain supported.
            - Divide by zero is handled explicitly.
            - Repair evidence is ready for release notes.
            """;
        fileService.WriteTextFile(approvalPath, markdown, overwrite: true);

        return BuildOutcome(
            "QA approved the repaired calculator implementation and selected the approval branch.",
            "Completed",
            "Repaired calculator implementation passed QA.",
            ProcessMockAgentCatalog.BranchApproved,
            "QA mock approval artifact saved.",
            [CreateArtifact(approvalPath, "calculator qa approval artifact repaired implementation release")]);
    }

    private ProcessMockRuntimeOutcome ExecuteReleaseManager(ProcessMockRuntimeState state)
    {
        fileService.CreateDirectory(state.ArtifactRoot);
        var releasePath = $"{state.ArtifactRoot}/07-release-notes.md";
        var markdown =
            """
            # Calculator Release Notes

            The calculator process completed with a deterministic QA repair loop.

            ## Release Summary
            - Scope and architecture were captured.
            - First implementation was rejected by QA.
            - Repair developer fixed divide-by-zero behavior.
            - QA approved the repaired output.
            """;
        fileService.WriteTextFile(releasePath, markdown, overwrite: true);

        return BuildOutcome(
            "Release notes captured after deterministic QA approval.",
            "Completed",
            "Release notes were written after QA approval.",
            null,
            "Release manager mock artifact saved.",
            [CreateArtifact(releasePath, "calculator release notes artifact qa approval repair evidence")]);
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

    private string BuildImplementationRequiredArtifactSections(
        ProcessMockRuntimeState state,
        bool repaired,
        List<ProcessMockRuntimeArtifact> artifacts)
    {
        var sections = new List<string>();
        if (PromptRequiresArtifact(state.OriginalPrompt, "Implementation change set"))
        {
            var changeSetPath = repaired
                ? $"{state.ArtifactRoot}/05-implementation-change-set.md"
                : $"{state.ArtifactRoot}/03-implementation-change-set.md";
            var changeSetMarkdown =
                $$"""
                # Implementation Change Set

                ## Touched Surface Inventory
                - `{{state.OutputRoot}}/CalculatorApp/CalculatorEngine.cs` contains the calculator arithmetic implementation.

                ## Tests And Validation
                - Deterministic process mock validation stands in for the implementation agent proof path.
                - The change set is linked to calculator arithmetic tests and migration notes by this governed artifact.

                ## Migration Notes
                - No schema or data migration is introduced by the calculator implementation.
                """;
            fileService.WriteTextFile(changeSetPath, changeSetMarkdown, overwrite: true);
            artifacts.Add(CreateArtifact(
                changeSetPath,
                "implementation change set tests migration notes touched surface inventory"));
            sections.Add(
                """
                ## Implementation change set
                Touched surface inventory: CalculatorEngine owns Add, Subtract, Multiply, and Divide behavior for the calculator app.
                Tests and validation: deterministic process mock validation covers the implementation lane and links the change set to test proof.
                Migration notes: no schema, persistent data, or backfill changes are part of this implementation.
                """);
        }

        if (PromptRequiresArtifact(state.OriginalPrompt, "Migration and rollout preparation checklist"))
        {
            var checklistPath = repaired
                ? $"{state.ArtifactRoot}/05-migration-rollout-preparation-checklist.md"
                : $"{state.ArtifactRoot}/03-migration-rollout-preparation-checklist.md";
            var checklistMarkdown =
                """
                # Migration And Rollout Preparation Checklist

                ## Data Changes
                - No data migration required.
                - No schema migration, seed update, backfill, or data rollback step is required.

                ## Operational Preconditions
                - Implementation validation must pass before rollout.
                - QA must verify calculator arithmetic and divide-by-zero behavior before release.

                ## Rollback Steps
                - Revert the implementation change set or restore the previous project state.
                - No data rollback is required because no persistent data changes are introduced.
                """;
            fileService.WriteTextFile(checklistPath, checklistMarkdown, overwrite: true);
            artifacts.Add(CreateArtifact(
                checklistPath,
                "migration rollout preparation checklist data changes operational preconditions rollback steps no data migration required"));
            sections.Add(
                """
                ## Migration and rollout preparation checklist
                Data changes: no data migration required; no schema migration, seed update, backfill, or data rollback is needed.
                Operational preconditions: implementation validation must pass and QA must verify calculator arithmetic plus divide-by-zero behavior.
                Rollback steps: revert the implementation change set or restore the previous project state; no data rollback is required.
                """);
        }

        return string.Join(Environment.NewLine + Environment.NewLine, sections);
    }

    private static bool PromptRequiresArtifact(string prompt, string artifactTitle)
    {
        return prompt.Contains(artifactTitle, StringComparison.OrdinalIgnoreCase);
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
        string responseText)
    {
        if (structuredOutput?.OutputType != typeof(ProcessStepOutcomeResult))
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

    private static bool IsApprovalQaPass(string prompt)
    {
        return prompt.Contains("qa recheck", StringComparison.OrdinalIgnoreCase) ||
               prompt.Contains("recheck repaired", StringComparison.OrdinalIgnoreCase) ||
               prompt.Contains("repaired calculator implementation", StringComparison.OrdinalIgnoreCase) ||
               prompt.Contains("approve repaired", StringComparison.OrdinalIgnoreCase);
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

    private const string FirstPassCalculatorEngine =
        """
        namespace CalculatorApp;

        public sealed class CalculatorEngine
        {
            public decimal Add(decimal left, decimal right)
            {
                return left + right;
            }

            public decimal Subtract(decimal left, decimal right)
            {
                return left - right;
            }

            public decimal Multiply(decimal left, decimal right)
            {
                return left * right;
            }

            public decimal Divide(decimal left, decimal right)
            {
                return left / right;
            }
        }
        """;

    private const string RepairedCalculatorEngine =
        """
        namespace CalculatorApp;

        public sealed class CalculatorEngine
        {
            public decimal Add(decimal left, decimal right)
            {
                return left + right;
            }

            public decimal Subtract(decimal left, decimal right)
            {
                return left - right;
            }

            public decimal Multiply(decimal left, decimal right)
            {
                return left * right;
            }

            public decimal Divide(decimal left, decimal right)
            {
                if (right == 0)
                {
                    throw new DivideByZeroException("Cannot divide by zero.");
                }

                return left / right;
            }
        }
        """;

    private sealed record ProcessMockRuntimeState(
        string OriginalPrompt,
        string RuntimeSessionKey,
        string RunKey,
        string ArtifactRoot,
        string OutputRoot);

    private sealed record ProcessMockRuntimeArtifact(
        string RelativePath,
        string ContentSignalText);

    private sealed record ProcessMockRuntimeOutcome(
        string ResponseText,
        string ProgressSummary,
        string? BranchOutcomeKey,
        int ToolCalls,
        IReadOnlyList<ProcessMockRuntimeArtifact> Artifacts);
}
