using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class FileSandboxWorkspacePreparedValuesTests {
    private static readonly DateTimeOffset CapturedAt = new(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(2));
    private static readonly JsonSerializerOptions SerializerOptions = new FileSandboxWorkspaceJsonStore().SerializerOptions;
    private const string OpaqueJson = "{ \"text\": \"Příliš <&>\\n\", \"unknown\": [3,2,1] }";

    [Fact]
    public void Catalog_preparation_detaches_nested_caller_aliases_without_changing_stored_values() {
        var aliases = new MutableAliases();
        var catalog = Catalog(aliases);
        var expected = Bytes(catalog);
        var prepared = FileSandboxWorkspacePreparedValues.Freeze(catalog);
        Assert.Equal(expected, Bytes(prepared));

        aliases.Mutate();

        Assert.NotEqual(expected, Bytes(catalog));
        Assert.Equal(expected, Bytes(prepared));
        Assert.Equal(catalog.CatalogDataRevision, prepared.CatalogDataRevision);
        var agent = Assert.Single(prepared.Agents);
        var provider = Assert.Single(prepared.Providers);
        Assert.Equal(OpaqueJson, agent.ConfigurationJson);
        Assert.Equal(OpaqueJson, provider.ConfigurationJson);
        RejectMutation(prepared.Agents);
        RejectMutation(agent.Permissions.AllowedSecrets!);
        RejectMutation(agent.Capabilities);
        RejectMutation(agent.Tags);
        RejectMutation(provider.SuggestedModels);
        RejectMutation(provider.ModelCatalog);
        RejectMutation(provider.ModelPrices);
        RejectMutation(provider.Tags);
        RejectMutation(provider.ModelThinkingEffortCapabilities);
        RejectMutation(Assert.Single(provider.ModelThinkingEffortCapabilities).AllowedEfforts);
        RejectMutation(Assert.Single(prepared.Capabilities).Tags);
        RejectMutation(Assert.Single(prepared.AgentTeams).AgentIds);
        RejectMutation(Assert.Single(prepared.AgentPackageImportOperations).Receipt.UnresolvedPrerequisites);
        RejectMutation(Assert.Single(prepared.AgentPackageImportOperations).Receipt.Warnings);
        RejectMutation(Assert.Single(prepared.AgentExternalProvisioningOperations).Receipt.Warnings);
    }

    [Fact]
    public void Run_chat_detail_state_and_document_preparations_retain_their_exact_values_after_alias_mutation() {
        var aliases = new MutableAliases();
        var catalog = Catalog(aliases);
        var detail = Detail(aliases);
        var state = new SandboxWorkspaceExecutionState("retained-version", aliases.List(detail.ChatSession!),
            detail.ExecutionLog, detail.Metrics) {
            ExecutionRuns = aliases.List(detail.Run),
            ExecutionApprovals = detail.Approvals,
            ExecutionArtifacts = detail.Artifacts,
            ExecutionWorkflowCheckpoints = detail.Checkpoints,
            ToolExecutionReceipts = detail.ToolReceipts,
            ProviderUsageObservations = detail.UsageObservations
        };
        var document = SandboxWorkspaceDocument.Combine(catalog, state) with { Version = " \t" };
        var runBytes = Bytes(detail.Run);
        var chatBytes = Bytes(detail.ChatSession!);
        var detailBytes = Bytes(detail);
        var stateBytes = Bytes(state);
        var documentBytes = Bytes(document);
        var run = FileSandboxWorkspacePreparedValues.Freeze(detail.Run);
        var chat = FileSandboxWorkspacePreparedValues.Freeze(detail.ChatSession!);
        var preparedDetail = FileSandboxWorkspacePreparedValues.Freeze(detail);
        var preparedState = FileSandboxWorkspacePreparedValues.Freeze(state);
        var preparedDocument = FileSandboxWorkspacePreparedValues.Freeze(document);

        aliases.Mutate();

        Assert.Equal(runBytes, Bytes(run));
        Assert.Equal(chatBytes, Bytes(chat));
        Assert.Equal(detailBytes, Bytes(preparedDetail));
        Assert.Equal(stateBytes, Bytes(preparedState));
        Assert.Equal(documentBytes, Bytes(preparedDocument));
        Assert.Equal(" \t", preparedDocument.Version);
        Assert.Equal(CapturedAt.Offset, run.CreatedAtUtc.Offset);
        Assert.Same(detail.Run.ToolAdmission, run.ToolAdmission);
        Assert.Same(detail.EffectContinuation, preparedDetail.EffectContinuation);
        Assert.Same(detail.Run.EntryAgentRequestCompatibilityEvidence, run.EntryAgentRequestCompatibilityEvidence);
        Assert.True(run.ToolAdmission!.ProviderDispatches.IsDefault);
        RejectMutation(run.PendingApprovals);
        RejectMutation(chat.Messages);
        RejectMutation(chat.Compatibility!.PendingApprovals);
        RejectMutation(preparedDetail.Approvals);
        RejectMutation(preparedDetail.Artifacts);
        RejectMutation(preparedDetail.Checkpoints);
        RejectMutation(Assert.Single(preparedDetail.Checkpoints).PendingApprovalIds);
        RejectMutation(preparedDetail.ToolReceipts);
        RejectMutation(preparedDetail.UsageObservations);
        RejectMutation(preparedState.ExecutionRuns);
        RejectMutation(preparedState.ChatSessions);
        RejectMutation(preparedDocument.Metrics);
        RejectMutation(preparedDocument.ExecutionLog);
    }

    [Fact]
    public void Preparation_preserves_invalid_and_optional_null_shapes_for_existing_validation() {
        var aliases = new MutableAliases();
        var original = Catalog(aliases);
        var agent = original.Agents[0] with {
            Permissions = original.Agents[0].Permissions with { AllowedSecrets = null },
            Tags = null!
        };
        var catalog = original with { Agents = [agent, null!], Providers = null! };
        var prepared = FileSandboxWorkspacePreparedValues.Freeze(catalog);
        Assert.Null(prepared.Providers);
        Assert.Null(prepared.Agents[0].Permissions.AllowedSecrets);
        Assert.Null(prepared.Agents[0].Tags);
        Assert.Null(prepared.Agents[1]);
        Assert.Equal(Bytes(catalog), Bytes(prepared));

        var detail = Detail(aliases);
        var run = detail.Run with { PendingApprovals = null! };
        var chat = detail.ChatSession! with {
            Messages = null!,
            Compatibility = detail.ChatSession!.Compatibility! with { PendingApprovals = null! }
        };
        var state = SandboxWorkspaceExecutionState.Empty with { ExecutionRuns = null!, ChatSessions = [chat] };
        Assert.Null(FileSandboxWorkspacePreparedValues.Freeze(run).PendingApprovals);
        Assert.Null(FileSandboxWorkspacePreparedValues.Freeze(chat).Messages);
        Assert.Null(FileSandboxWorkspacePreparedValues.Freeze(chat).Compatibility!.PendingApprovals);
        Assert.Null(FileSandboxWorkspacePreparedValues.Freeze(state).ExecutionRuns);
        Assert.Null(FileSandboxWorkspacePreparedValues.Freeze(detail with { Run = null! }).Run);
        Assert.Null(FileSandboxWorkspacePreparedValues.Freeze(SandboxWorkspaceDocument.Empty with { Agents = null! }).Agents);

        Assert.Throws<ArgumentNullException>(() => FileSandboxWorkspacePreparedValues.Freeze((SandboxWorkspaceCatalog)null!));
        Assert.Throws<ArgumentNullException>(() => FileSandboxWorkspacePreparedValues.Freeze((SandboxWorkspaceDocument)null!));
        Assert.Throws<ArgumentNullException>(() => FileSandboxWorkspacePreparedValues.Freeze((SandboxWorkspaceExecutionState)null!));
        Assert.Throws<ArgumentNullException>(() => FileSandboxWorkspacePreparedValues.Freeze((ExecutionRunDetail)null!));
        Assert.Throws<ArgumentNullException>(() => FileSandboxWorkspacePreparedValues.Freeze((ExecutionRunRecord)null!));
        Assert.Throws<ArgumentNullException>(() => FileSandboxWorkspacePreparedValues.Freeze((ChatSessionRecord)null!));
    }

    private static SandboxWorkspaceCatalog Catalog(MutableAliases aliases) {
        var capability = new CapabilityCatalogItem(Guid.NewGuid(), CapabilityKind.Tool, "fixture-tool", "Tool", "Description",
            "fixture-endpoint", OpaqueJson, CapabilityProofStatus.Verified, "Proof", CapturedAt, false) {
            Tags = aliases.Array("capability-tag")
        };
        var agent = new AgentDefinition(Guid.NewGuid(), "Agent", "Role", "Summary", "Instructions", AgentLifecycleStatus.Active,
            Guid.NewGuid(), "model", AgentWorkloadKind.General, AgentChatHistoryMode.FrameworkManaged, 0.25, true, false,
            OpaqueJson, false, "fixture-agent", AgentPermissionsPolicy.Default with {
                AllowedSecrets = aliases.List(new AgentAllowedSecretReference(Guid.NewGuid(), "Reference", AgentSecretPurposes.GeneralAgentRequest))
            }, aliases.List(new AgentCapabilityAssignment(capability.Id, capability.Key, capability.Kind, capability.ProofStatus,
                CapturedAt, "Assignment")), aliases.Array("agent-tag"), CapturedAt, CapturedAt) {
            AvatarImageUrl = "fixture-avatar"
        };
        var provider = new ProviderProfile(agent.ProviderProfileId!.Value, "Provider", ProviderKind.OpenAi, "https://provider.invalid",
            "fixture-reference", "model", ProviderTransportKind.Responses, true, true, true, true, false, OpaqueJson,
            "Notes", "Health", CapturedAt, aliases.List("model")) {
            ConnectorPluginKey = "fixture-provider",
            ModelCatalog = aliases.List(new ProviderModelDisplayMetadata("model", "Model")),
            ModelPrices = aliases.List(new ProviderModelTokenPrice("model", 1.2m, 0.3m, 2.4m) { CacheWritePerMillionTokensUsd = 0.7m }),
            Tags = aliases.Array("provider-tag"),
            ModelThinkingEffortCapabilities = aliases.List(new ProviderModelThinkingEffortCapability("model",
                AgentThinkingEffortSupportStatus.Supported, AgentThinkingEffortCapabilitySource.Configured,
                aliases.List(AgentReasoningEffortLevel.Low, AgentReasoningEffortLevel.High))),
            ModelSelectionConstraint = new(aliases.List("model"))
        };
        var receipt = new AgentPackageImportReceipt(agent.Id, AgentPackageImportMode.Create, "fixture-key", "package-hash",
            "schema", "imported-version", "configuration-hash", aliases.List("unresolved"), aliases.Array("package-warning"), false);
        var externalReceipt = new AgentExternalProvisioningReceipt("fixture", "key", agent.Id, "configuration-version", true,
            false, false, aliases.List("external-warning"));
        return new("retained-catalog", aliases.List(agent), aliases.List(provider), aliases.List(capability),
            aliases.List(new AgentMemoryRecord(Guid.NewGuid(), agent.Id, MemoryKind.Fact, "Memory", "Content", "Source", 3, OpaqueJson, CapturedAt))) {
            CatalogDataRevision = new(17),
            AgentTeams = aliases.List(new AgentTeamDefinition(Guid.NewGuid(), "Team", "Description", aliases.List(agent.Id), CapturedAt, CapturedAt)),
            AgentPackageImportOperations = aliases.List(new AgentPackageImportOperationRecord("import-key", "request", receipt, CapturedAt)),
            AgentExternalBindings = aliases.List(new AgentExternalBindingRecord("fixture", "key", agent.Id, "configuration-version", false, CapturedAt)),
            AgentExternalProvisioningOperations = aliases.List(new AgentExternalProvisioningOperationRecord("external-key", "request", externalReceipt, CapturedAt))
        };
    }

    private static ExecutionRunDetail Detail(MutableAliases aliases) {
        var runId = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var approval = new PendingToolApprovalRecord("approval", "call", "tool", "fixture", "Details", OpaqueJson);
        var journal = new AgentToolJournalRecord(1, 11,
            new(new(runId, chatId, new(Guid.NewGuid())), agentId, AgentRuntimeContextPurpose.InteractiveChat,
                new(Guid.NewGuid(), "fixture-profile", new(0))), [], [], Support: AgentToolAdmissionSupport.RequestScopedInput);
        journal.Validate();
        var run = new ExecutionRunRecord(runId, agentId, chatId, "Run", "fixture", "source", "correlation", "causation", "caller",
            "fixture", OpaqueJson, "Input", "Result", "Provider", "model", ExecutionState.WaitingOnTool, null, CapturedAt,
            CapturedAt, CapturedAt, null, "runtime", OpaqueJson, aliases.List(approval), Revision: 13) {
            ToolAdmission = journal,
            HistoryCaller = new(HistoryAuthenticationKind.Unknown),
            EntryAgentRequestCompatibilityEvidence = new(1, ProviderKind.OpenAi, null, ProviderTransportKind.Responses, "model", "model",
                ProviderInvocationFeatures.None, null, null, ProviderRequestCompatibilityDisposition.Preserved, ProviderModelParameterAdjustment.None)
        };
        var chat = new ChatSessionRecord(chatId, agentId, "Chat", CapturedAt, CapturedAt,
            aliases.List(new ChatMessageRecord(Guid.NewGuid(), ChatMessageRole.User, "Original <&> Příliš", CapturedAt, 7)),
            runId, new("runtime", OpaqueJson, aliases.List(approval), true));
        return new(run, chat,
            aliases.List(new ExecutionLogEntry(Guid.NewGuid(), agentId, chatId, CapturedAt, ExecutionState.WaitingOnTool, "phase", "Message") { ExecutionRunId = runId }),
            aliases.List(new AgentRunMetric(Guid.NewGuid(), agentId, chatId, CapturedAt, RunOutcome.Succeeded, "Provider", "model", 23, 4, 5, 1) {
                ExecutionRunId = runId, CostUsd = 0.017m, CachedInputTokens = 2
            })) {
            Approvals = aliases.List(new ExecutionApprovalRecord("approval", runId, "call", "tool", "fixture", "Details", OpaqueJson,
                ExecutionApprovalStatus.Pending, CapturedAt, null, "source-kind", "source-id", "Notes")),
            Artifacts = aliases.List(new ExecutionArtifactRecord(Guid.NewGuid(), runId, "fixture", "Artifact", "fixture.txt", "text/plain", "tool", "Summary", CapturedAt)),
            Checkpoints = aliases.List(new ExecutionWorkflowCheckpointRecord(Guid.NewGuid(), runId, "workflow-session", "checkpoint", "fixture",
                ExecutionState.WaitingOnTool, aliases.Array("approval"), CapturedAt, null, "correlation", "source-kind", "source-id",
                "process-run", "process-step", "scheduler", "message", "trace", "span")),
            ToolReceipts = aliases.List(new ToolExecutionReceiptRecord(Guid.NewGuid(), runId, "family", "tool", "risk", "approval", "isolation",
                "Request", "fixture-workspace", "Exit", CapturedAt, CapturedAt)),
            UsageObservations = aliases.List(new ProviderUsageObservation(Guid.NewGuid(), CapturedAt, "Provider", ProviderKind.OpenAi, "model",
                ProviderTransportKind.Responses, ProviderUsageSourcePhases.AgentRuntime, ProviderUsageObservationStatus.Observed, 4, 2, 5, 1, 9, 1) {
                ExecutionRunId = runId, RawUsageJson = OpaqueJson, HistoryEvidence = new(new(Guid.NewGuid()), false, [])
            }),
            EffectContinuation = new(runId, chatId, run.Revision, journal.Revision, AgentToolContinuationRequirement.None,
                new(0, 0, 0, 0, 0, 0, 0), 0, 0, AgentEffectReconciliationStop.SnapshotRead, AgentEffectCheckpointPersistence.Confirmed, [])
        };
    }

    private static byte[] Bytes<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);

    private static void RejectMutation<T>(IReadOnlyList<T> values) =>
        Assert.Throws<NotSupportedException>(() => Assert.IsAssignableFrom<IList<T>>(values).Clear());

    private sealed class MutableAliases {
        private readonly List<Action> mutations = [];

        internal List<T> List<T>(params T[] values) {
            var list = values.ToList();
            mutations.Add(list.Clear);
            return list;
        }

        internal T[] Array<T>(params T[] values) {
            mutations.Add(() => System.Array.Clear(values));
            return values;
        }

        internal void Mutate() {
            foreach (var mutation in mutations) {
                mutation();
            }
        }
    }
}
