using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.AgentFramework.Hosting;

internal sealed class ProcessMockAgentRuntime(
    IAgentRuntime inner,
    IWorkspaceFileService fileService,
    IOptions<ProcessMockAgentOptions> options) : IAgentRuntime
{
    private const string WorkspaceStatPathToolName = "workspace_stat_path";
    private const string WorkspaceReadFileToolName = "workspace_read_file";

    private static readonly Regex ManagedWorkspacePathRegex = new(
        @"(?<path>\b(?:artifacts|output|integration-map|data)/[^\s`'""<>()\[\]{},;]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

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
            ResponseText: "Process mock provider is deterministic. Use role-specific mock agents in a generic delivery process to exercise QA repair loops.",
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
        AgentStructuredOutputContract? structuredOutput = null,
        AgentRuntimeExecutionOptions? executionOptions = null)
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
                structuredOutput,
                executionOptions);
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
        var inspectedArtifactPaths = string.Equals(roleKey, ProcessMockAgentRoleKeys.Qa, StringComparison.Ordinal)
            ? ResolveQaArtifactInspectionPaths(prompt)
            : [];

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
            SerializedSessionStateJson: BuildSerializedSessionState(roleKey, state, outcome, inspectedArtifactPaths),
            PendingApprovals: [])
        {
            FinalizerInvocations = BuildProcessStepOutcomeFinalizerInvocations(structuredOutput, executionOptions, outcome.ResponseText),
            ToolInvocationTraces = BuildProcessStepOutcomeToolInvocationTraces(structuredOutput, executionOptions)
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
        AgentStructuredOutputContract? structuredOutput = null,
        AgentRuntimeExecutionOptions? executionOptions = null)
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
                structuredOutput,
                executionOptions);
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

        return BuildOutcome(
            "Scope captured for the deterministic mock delivery process.",
            "Completed",
            "Mock scope and acceptance criteria were written.",
            null,
            "Product owner mock scope artifact saved.",
            [CreateArtifact(scopePath, "mock sample scope artifact acceptance criteria validation blank input")]);
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

        return BuildOutcome(
            "Architecture captured for the deterministic mock delivery process.",
            "Completed",
            "Mock architecture guidance was written.",
            null,
            "Architect mock handoff artifact saved.",
            [CreateArtifact(architecturePath, "mock sample architecture artifact boundary implementation qa expectations")]);
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

        return BuildOutcome(
            responseSummary,
            "Completed",
            "First-pass mock implementation artifact was written.",
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

            QA rejects the first-pass mock implementation.

            ## Blocking Defect
            `ValidationEngine.NormalizeName` trims input but does not explicitly reject blank values. Repair is required before approval.
            """;
        fileService.WriteTextFile(findingPath, markdown, overwrite: true);

        return BuildOutcome(
            "QA rejected the first-pass mock implementation and selected the repair branch.",
            "Completed",
            "Blank-input handling is missing; repair is required.",
            ProcessMockAgentCatalog.BranchRepairsRequired,
            "QA mock rejection artifact saved.",
            [CreateArtifact(findingPath, "mock sample qa rejection artifact finding repair branch reason")]);
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

        return BuildOutcome(
            responseSummary,
            "Completed",
            "Blank-input repair artifact was written.",
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

            QA approves the repaired mock implementation.

            ## Verified Behavior
            - Non-empty values are normalized.
            - Blank input is handled explicitly.
            - Repair evidence is ready for release notes.
            """;
        fileService.WriteTextFile(approvalPath, markdown, overwrite: true);

        return BuildOutcome(
            "QA approved the repaired mock implementation and selected the approval branch.",
            "Completed",
            "Repaired mock implementation passed QA.",
            ProcessMockAgentCatalog.BranchApproved,
            "QA mock approval artifact saved.",
            [CreateArtifact(approvalPath, "mock sample qa approval artifact repaired implementation release")]);
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

        return BuildOutcome(
            "Release notes captured after deterministic QA approval.",
            "Completed",
            "Release notes were written after QA approval.",
            null,
            "Release manager mock artifact saved.",
            [CreateArtifact(releasePath, "mock sample release notes artifact qa approval repair evidence")]);
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
                - `{{state.OutputRoot}}/MockApp/ValidationEngine.cs` contains the mock validation implementation.

                ## Tests And Validation
                - Deterministic process mock validation stands in for the implementation agent proof path.
                - The change set is linked to validation behavior tests and migration notes by this governed artifact.

                ## Migration Notes
                - No schema or data migration is introduced by the mock implementation.
                """;
            fileService.WriteTextFile(changeSetPath, changeSetMarkdown, overwrite: true);
            artifacts.Add(CreateArtifact(
                changeSetPath,
                "implementation change set tests migration notes touched surface inventory"));
            sections.Add(
                """
                ## Implementation change set
                Touched surface inventory: ValidationEngine owns name normalization and blank-input validation behavior for the mock implementation target.
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
                - QA must verify name normalization and blank-input behavior before release.

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
                Operational preconditions: implementation validation must pass and QA must verify name normalization plus blank-input behavior.
                Rollback steps: revert the implementation change set or restore the previous project state; no data rollback is required.
                """);
        }

        return string.Join(Environment.NewLine + Environment.NewLine, sections);
    }

    private static bool PromptRequiresArtifact(string prompt, string artifactTitle)
    {
        return prompt.Contains(artifactTitle, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSerializedSessionState(
        string roleKey,
        ProcessMockRuntimeState state,
        ProcessMockRuntimeOutcome outcome,
        IReadOnlyList<string> inspectedArtifactPaths)
    {
        var callContents = new List<Dictionary<string, object?>>();
        var resultContents = new List<Dictionary<string, object?>>();
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
                    ["content"] = $"Process mock QA inspected inherited artifact {artifactPath}."
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

    private static IReadOnlyList<string> ResolveQaArtifactInspectionPaths(string prompt)
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

    private static bool IsApprovalQaPass(string prompt)
    {
        return prompt.Contains("qa recheck", StringComparison.OrdinalIgnoreCase) ||
               prompt.Contains("recheck repaired", StringComparison.OrdinalIgnoreCase) ||
               prompt.Contains("repaired mock implementation", StringComparison.OrdinalIgnoreCase) ||
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
}
