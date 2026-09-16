using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Tests.Unit.Runtime;

public sealed class CrmPlanningToolAdmissionTests {
    [Theory]
    [InlineData(CrmPlanningToolPolicy.Search, CrmPlanningToolPolicy.SearchCapability)]
    [InlineData(CrmPlanningToolPolicy.Summary, CrmPlanningToolPolicy.SummaryCapability)]
    public void Ordinary_planner_needs_the_exact_catalog_assignment_and_CRM_source_scope(string tool, string key) {
        var (agent, capability, governance) = Actor(key);
        Assert.NotEqual(HrAgentIdentity.AgentId, agent.Id);
        Assert.Equal(capability.Id, CrmPlanningToolPolicy.ResolveCapability(agent, [capability], governance, tool));
        Assert.Null(CrmPlanningToolPolicy.ResolveCapability(agent with { Capabilities = [] }, [capability], governance, tool));
        Assert.Null(CrmPlanningToolPolicy.ResolveCapability(agent, [capability with { Id = Guid.NewGuid() }], governance, tool));
        Assert.Null(CrmPlanningToolPolicy.ResolveCapability(agent with { ConfigurationJson = "{}" }, [capability], governance, tool));
        Assert.Null(CrmPlanningToolPolicy.ResolveCapability(agent with { IsTemplate = true }, [capability], governance, tool));
        Assert.Null(CrmPlanningToolPolicy.ResolveCapability(agent with {
            Permissions = agent.Permissions with { CanUseTools = false }
        }, [capability], governance, tool));
        Assert.Null(CrmPlanningToolPolicy.ResolveCapability(agent with {
            Capabilities = [agent.Capabilities[0], agent.Capabilities[0]]
        }, [capability], governance, tool));
    }

    [Fact]
    public void Saved_capability_and_operation_ceilings_cannot_be_widened_by_current_assignments() {
        var (agent, capability, original) = Actor(CrmPlanningToolPolicy.SearchCapability);
        var denied = new AgentExecutionGovernanceSnapshot(original.AuthorityId, agent.Id,
            original.DatabaseProfileId, original.DatabaseProfileGeneration, original.WorkspaceScope,
            true, false, original.PolicyVersion, original.PolicyFingerprint,
            [CrmPlanningToolPolicy.Summary], [CrmPlanningToolPolicy.SummaryCapability]);
        Assert.Null(CrmPlanningToolPolicy.ResolveCapability(agent, [capability], denied, CrmPlanningToolPolicy.Search));
        var readDenied = new AgentExecutionGovernanceSnapshot(original.AuthorityId, agent.Id,
            original.DatabaseProfileId, original.DatabaseProfileGeneration, original.WorkspaceScope,
            false, false, original.PolicyVersion, original.PolicyFingerprint);
        Assert.Null(CrmPlanningToolPolicy.ResolveCapability(agent, [capability], readDenied, CrmPlanningToolPolicy.Search));
    }

    [Theory]
    [InlineData(AgentRuntimeToolProviderPurpose.InteractiveChat, AgentChatTrustedSourceKinds.ProjectStructure, true)]
    [InlineData(AgentRuntimeToolProviderPurpose.InteractiveChat, CrmPlanningToolPolicy.ProjectsSourceKind, true)]
    [InlineData(AgentRuntimeToolProviderPurpose.InteractiveChat, "agents", false)]
    [InlineData(AgentRuntimeToolProviderPurpose.GovernedProcessAutomation, AgentChatTrustedSourceKinds.ProjectStructure, false)]
    [InlineData(AgentRuntimeToolProviderPurpose.AutoApprovedNonInteractive, AgentChatTrustedSourceKinds.ProjectStructure, false)]
    public void Attachment_requires_the_original_interactive_planning_surface(AgentRuntimeToolProviderPurpose purpose,
        string source, bool expected) {
        var (agent, capability, governance) = Actor(CrmPlanningToolPolicy.SearchCapability);
        var context = Context(agent, capability, governance) with {
            Purpose = purpose,
            ContextIntent = AgentRuntimeContextIntent.Empty with { SourceKind = source }
        };
        Assert.Equal(expected, CrmPlanningToolPolicy.CanAttach(context));
        Assert.False(CrmPlanningToolPolicy.CanAttach(context with { AdmittedToolSession = null }));
        Assert.False(CrmPlanningToolPolicy.CanAttach(context with { ToolAdmissionSupport = AgentToolAdmissionSupport.RequestScopedInput }));
    }

    [Theory]
    [InlineData(CrmPlanningToolPolicy.Search, CrmPlanningToolPolicy.SearchCapability)]
    [InlineData(CrmPlanningToolPolicy.Summary, CrmPlanningToolPolicy.SummaryCapability)]
    public void Prepared_source_round_trips_nonempty_identity_and_numeric_or_string_request_enums(string tool, string key) {
        var source = Source(key);
        var preparer = new CrmPlanningProposalPreparer(source);
        object request = tool == CrmPlanningToolPolicy.Search
            ? new CrmHrAgentSearchQuery("Engineer", CrmHrAgentRecordKind.Workforce, 4)
            : new CrmHrAgentItemReference(CrmHrAgentRecordKind.Workforce, Guid.NewGuid());
        var numeric = JsonSerializer.SerializeToElement(new { request }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var named = JsonSerializer.SerializeToElement(new { request }, CrmPlanningProposalPreparer.Json);
        var prepared = preparer.Prepare(tool, numeric);
        Assert.Equal(prepared, preparer.Prepare(tool, named));
        var restored = JsonSerializer.Deserialize<AgentToolPreparedPayload>(
            JsonSerializer.Serialize(prepared, CrmPlanningProposalPreparer.Json), CrmPlanningProposalPreparer.Json)!;
        var restoredSource = preparer.Require(restored);
        Assert.Equal(source, restoredSource);
        Assert.NotEqual(Guid.Empty, restoredSource.Session.ExecutionRunId);
        Assert.NotEqual(Guid.Empty, restoredSource.Session.ChatSessionId);
        Assert.NotEqual(Guid.Empty, restoredSource.Session.AuthorityId.Value);
        Assert.NotEqual(Guid.Empty, restoredSource.CapabilityId);
        Assert.Equal(2, restored.SemanticVersion);
        Assert.NotEqual(AgentToolProtocolEnvelope.ComputeDigest(restored.ArgumentsJson), restored.Digest);
    }

    [Fact]
    public void Same_arguments_cannot_replace_original_capability_provider_project_or_workspace_evidence() {
        var original = Source(CrmPlanningToolPolicy.SearchCapability);
        var arguments = JsonSerializer.SerializeToElement(new { request = new CrmHrAgentSearchQuery("Engineer") });
        var prepared = new CrmPlanningProposalPreparer(original).Prepare(CrmPlanningToolPolicy.Search, arguments);
        CrmPlanningReadSource[] changed = [
            original with { CapabilityId = Guid.NewGuid() },
            original with { ProviderId = Guid.NewGuid() },
            original with { Scope = WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")) },
            original with { WorkspaceSource = AgentToolProtocolEnvelope.Create("fixture-source", 1, "{\"lifetime\":2}") }
        ];
        foreach (var source in changed) {
            var contender = new CrmPlanningProposalPreparer(source);
            Assert.NotEqual(prepared.Digest, contender.Prepare(CrmPlanningToolPolicy.Search, arguments).Digest);
            Assert.Equal("crm-planning.read-denied", Assert.Throws<AgentToolAdmissionException>(() => contender.Require(prepared)).Code);
        }
        var legacy = new AgentToolPreparedPayload(prepared.ToolName, 1,
            AgentToolProtocolEnvelope.ComputeDigest(prepared.ArgumentsJson), prepared.ArgumentsJson,
            AgentToolProposalEffect.Read, AgentToolProposalRecovery.RevalidateAndRead);
        Assert.Equal("crm-planning.original-source-required", Assert.Throws<AgentToolAdmissionException>(() =>
            new CrmPlanningProposalPreparer(original).Require(legacy)).Code);
    }

    [Theory]
    [InlineData("{}", typeof(JsonException))]
    [InlineData("{\"request\":null}", typeof(ArgumentNullException))]
    [InlineData("{\"request\":{\"searchText\":\"Engineer\",\"unreviewedTarget\":\"other\"}}", typeof(JsonException))]
    public void Preparation_rejects_missing_or_unmapped_request_content(string json, Type exceptionType) {
        using var document = JsonDocument.Parse(json);
        var preparer = new CrmPlanningProposalPreparer(Source(CrmPlanningToolPolicy.SearchCapability));
        Assert.IsType(exceptionType, Record.Exception(() => preparer.Prepare(CrmPlanningToolPolicy.Search, document.RootElement)));
    }

    private static CrmPlanningReadSource Source(string key) {
        var (agent, capability, governance) = Actor(key);
        return new(new(Guid.NewGuid(), Guid.NewGuid(), governance.AuthorityId), agent.Id, agent.ProviderProfileId!.Value,
            governance.WorkspaceScope, key, capability.Id,
            AgentToolProtocolEnvelope.Create("fixture-source", 1, "{\"lifetime\":1}"));
    }

    private static (AgentDefinition Agent, CapabilityCatalogItem Capability, AgentExecutionGovernanceSnapshot Governance) Actor(string key) {
        var now = DateTimeOffset.UtcNow;
        var capability = new CapabilityCatalogItem(Guid.NewGuid(), CapabilityKind.Tool, key, key, string.Empty,
            string.Empty, "{}", CapabilityProofStatus.Verified, string.Empty, now, true);
        var agent = new AgentDefinition(Guid.NewGuid(), "Ordinary planner", "Planning", "Plans existing work.",
            "Read authorized planning facts.", AgentLifecycleStatus.Active, Guid.NewGuid(), "fixture-model",
            AgentWorkloadKind.General, AgentChatHistoryMode.FrameworkManaged, 0.2d, false, false,
            AgentMemoryAccessMetadata.Write("{}", new() { AllowedSourceScopes = [MemorySourceScope.Crm] }),
            false, string.Empty, AgentPermissionsPolicy.Default with { CanUseTools = true },
            [new(capability.Id, key, capability.Kind, capability.ProofStatus, now, string.Empty)], [], now, now);
        var governance = new AgentExecutionGovernanceSnapshot(AgentExecutionAuthorityId.Create(), agent.Id,
            Guid.NewGuid(), new(1), WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")),
            true, false, "fixture-policy", "fixture-fingerprint");
        return (agent, capability, governance);
    }

    private static AgentRuntimeToolProviderContext Context(AgentDefinition agent, CapabilityCatalogItem capability,
        AgentExecutionGovernanceSnapshot governance) {
        var provider = new ProviderProfile(agent.ProviderProfileId!.Value, "Fixture", ProviderKind.Ollama,
            "http://provider.example.test", string.Empty, agent.Model, ProviderTransportKind.ChatCompletions,
            true, false, true, true, false, "{}", string.Empty, string.Empty, null, [], ProviderProfilePurpose.Chat);
        return new(agent, provider, [capability], false, AgentRuntimeToolProviderPurpose.InteractiveChat, "fixture",
            AgentRuntimeContextIntent.Empty, new Dictionary<string, string>()) {
            Governance = governance, AdmittedToolSession = new(Guid.NewGuid(), Guid.NewGuid(), governance.AuthorityId),
            ToolAdmissionSupport = AgentToolAdmissionSupport.Recoverable
        };
    }
}
