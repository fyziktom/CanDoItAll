using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafSkillSourcePreparationTests {
    [Fact]
    public void Unmarked_payload_retains_the_complete_legacy_serialization_and_identity() {
        var original = LegacyPayload();
        Assert.Same(original, MafContextToolSourceContract.Bind(original, null));
        Assert.Equal(JsonSerializer.Serialize(new {
            original.ToolName, original.SemanticVersion, original.Digest, original.ArgumentsJson, original.Effect, original.Recovery
        }), JsonSerializer.Serialize(original));
        Assert.Equal(original, JsonSerializer.Deserialize<AgentToolPreparedPayload>(JsonSerializer.Serialize(original)));
    }

    [Fact]
    public void Source_marker_changes_the_approved_digest_without_changing_SDK_arguments_or_effect_policy() {
        var original = LegacyPayload();
        var first = MafContextToolSourceContract.Bind(original, AgentToolProtocolEnvelope.Create("fixture-source", 1, "{\"root\":\"original\"}"));
        var second = MafContextToolSourceContract.Bind(original, AgentToolProtocolEnvelope.Create("fixture-source", 1, "{\"root\":\"replacement\"}"));
        Assert.Equal(2, first.SemanticVersion);
        Assert.Equal(original.ArgumentsJson, first.ArgumentsJson);
        Assert.Equal(original.Effect, first.Effect);
        Assert.Equal(original.Recovery, first.Recovery);
        Assert.NotEqual(original.Digest, first.Digest);
        Assert.NotEqual(first.Digest, second.Digest);
        Assert.Equal(first, JsonSerializer.Deserialize<AgentToolPreparedPayload>(JsonSerializer.Serialize(first)));
    }

    [Fact]
    public void Previous_reader_cannot_accept_v2_even_if_it_ignores_the_unknown_source_field() {
        var original = LegacyPayload();
        var current = MafContextToolSourceContract.Bind(original, AgentToolProtocolEnvelope.Create("fixture-source", 1, "{}"));
        var old = JsonSerializer.Deserialize<PreviousPayload>(JsonSerializer.Serialize(current))!;
        Assert.Equal(2, old.SemanticVersion);
        Assert.NotEqual(original.SemanticVersion, old.SemanticVersion);
        Assert.NotEqual(original.Digest, old.Digest);
        Assert.NotEqual(original, new AgentToolPreparedPayload(old.ToolName, old.SemanticVersion, old.Digest,
            old.ArgumentsJson, old.Effect, old.Recovery));
    }

    [Fact]
    public void Source_marker_requires_version_two_and_has_an_independent_UTF8_bound() {
        var original = LegacyPayload();
        var small = AgentToolProtocolEnvelope.Create("fixture-source", 1, "{}");
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgentToolPreparedPayload(original.ToolName, 1, original.Digest,
            original.ArgumentsJson, original.Effect, original.Recovery, small));
        var large = AgentToolProtocolEnvelope.Create("fixture-source", 1,
            JsonSerializer.Serialize(new string('é', AgentToolPreparedPayload.MaximumSourcePreparationUtf8Bytes)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MafContextToolSourceContract.Bind(original, large));
    }

    [Fact]
    public void Configuration_hash_is_canonical_and_binds_root_inline_content_and_original_capability_identity() {
        var id = Guid.NewGuid();
        var first = Capability(id, "{\"skillRoot\":\"original\",\"inlineSkill\":{\"name\":\"skill\",\"instructions\":\"original\"}}");
        var reordered = first with { ConfigurationJson = "{\"inlineSkill\":{\"instructions\":\"original\",\"name\":\"skill\"},\"skillRoot\":\"original\"}" };
        var configuration = new AgentRuntimeConfiguration();
        var access = new AgentWorkspaceToolAccessSettings();
        var digest = MafSkillSourcePreparation.ConfigurationDigest(configuration, [first], [], access);
        Assert.Equal(digest, MafSkillSourcePreparation.ConfigurationDigest(configuration, [reordered], [], access));
        Assert.NotEqual(digest, MafSkillSourcePreparation.ConfigurationDigest(configuration, [first with { Id = Guid.NewGuid() }], [], access));
        Assert.NotEqual(digest, MafSkillSourcePreparation.ConfigurationDigest(configuration,
            [first with { ConfigurationJson = first.ConfigurationJson.Replace("original", "replacement", StringComparison.Ordinal) }], [], access));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Installed_SDK_keeps_candidate_order_cache_deduplication_and_exact_tool_contracts(bool approval) {
        var catalog = SandboxWorkspaceSeedFactory.Create().ToCatalog();
        var actor = catalog.Agents.First(item => item.ProviderProfileId.HasValue);
        var context = new AgentRuntimeToolProviderContext(actor, catalog.Providers.Single(item => item.Id == actor.ProviderProfileId), [],
            false, AgentRuntimeToolProviderPurpose.InteractiveChat, "fixture", AgentRuntimeContextIntent.Empty, new Dictionary<string, string>());
        var source = new ScopeSource(actor);
        var configuration = AgentToolProtocolEnvelope.ComputeDigest("configuration");
        var preparation = new MafSkillSourcePreparation(source, context, Path.GetTempPath(), WorkspaceScopeDescriptor.Sandbox,
            configuration, _ => configuration);
        AgentSkill[] originals = [new AgentInlineSkill("duplicate", "first", "first instructions"),
            new AgentInlineSkill("duplicate", "second", "second instructions")];
        var firstCapability = Capability(Guid.NewGuid(), "{}");
        var secondCapability = Capability(Guid.NewGuid(), "{}");
        var counted = new CountingSource([MafPreparedSkill.Inline(originals[0], firstCapability, preparation),
            MafPreparedSkill.Inline(originals[1], secondCapability, preparation)]);
        var registration = new MafContextToolRegistration([
            new(AgentSkillsProvider.LoadSkillToolName, false), new(AgentSkillsProvider.ReadSkillResourceToolName, false),
            new(AgentSkillsProvider.RunSkillScriptToolName, approval)], preparation);
        using var baseline = Configure(new AgentSkillsProviderBuilder().UseSkills(originals), approval).Build();
        using var wrapped = new MafSkillsContextProvider(Configure(new AgentSkillsProviderBuilder().UseSource(counted)
            .UseFilter((skill, _) => preparation.Observe(skill)), approval).Build(), registration);
        using var client = new UnusedClient();
        var agent = new ChatClientAgent(client);
#pragma warning disable MAAI001
        var invoking = new AIContextProvider.InvokingContext(agent, await agent.CreateSessionAsync(), new AIContext());
#pragma warning restore MAAI001
        var expected = await baseline.InvokingAsync(invoking);
        var actual = await wrapped.InvokingAsync(invoking);
        var arguments = JsonSerializer.SerializeToElement(new { skillName = "duplicate" });
        var marker = preparation.Prepare(AgentSkillsProvider.LoadSkillToolName, arguments);
        Assert.Equal(expected.Instructions, actual.Instructions);
        Assert.Equal(expected.Tools!.OfType<AIFunction>().Select(Contract), actual.Tools!.OfType<AIFunction>().Select(Contract));
        var entries = MafSkillSourceEvidence.Read(marker).Candidates;
        Assert.Equal(new[] { firstCapability.Id, secondCapability.Id }, entries.Select(item => item.Origin.CapabilityId!.Value));
        Assert.Equal(2, entries.Length);
        await wrapped.InvokingAsync(invoking);
        Assert.Equal(marker, preparation.Prepare(AgentSkillsProvider.LoadSkillToolName, arguments));
        Assert.Equal(1, counted.Reads);
        Assert.Equal(2, source.Reads);
        Assert.Equal(0, source.ActiveLeases);
    }

    private static object Contract(AIFunction function) => new {
        function.Name, function.Description, Schema = function.JsonSchema.GetRawText(), Return = function.ReturnJsonSchema?.GetRawText(),
        Approval = function.GetService<ApprovalRequiredAIFunction>() is not null
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Source_size_limit_applies_to_the_proposed_skill_and_never_prevents_context_attachment(bool duplicateNames) {
        var catalog = SandboxWorkspaceSeedFactory.Create().ToCatalog();
        var actor = catalog.Agents.First(item => item.ProviderProfileId.HasValue);
        var context = new AgentRuntimeToolProviderContext(actor, catalog.Providers.Single(item => item.Id == actor.ProviderProfileId), [],
            false, AgentRuntimeToolProviderPurpose.InteractiveChat, "fixture", AgentRuntimeContextIntent.Empty, new Dictionary<string, string>());
        var configuration = AgentToolProtocolEnvelope.ComputeDigest("configuration");
        var preparation = new MafSkillSourcePreparation(new ScopeSource(actor), context, Path.GetTempPath(), WorkspaceScopeDescriptor.Sandbox,
            configuration, _ => configuration);
        var skills = Enumerable.Range(0, MafSkillSourceEvidence.MaximumCandidates + 1).Select(index =>
            MafPreparedSkill.Inline(new AgentInlineSkill(duplicateNames ? "selected" : $"skill-{index}", "Description", "Instructions"),
                Capability(Guid.NewGuid(), "{}"), preparation)).ToArray();
        var registration = new MafContextToolRegistration([
            new(AgentSkillsProvider.LoadSkillToolName, false), new(AgentSkillsProvider.ReadSkillResourceToolName, false),
            new(AgentSkillsProvider.RunSkillScriptToolName, false)], preparation);
        using var provider = new MafSkillsContextProvider(Configure(new AgentSkillsProviderBuilder().UseSkills(skills)
            .UseFilter((skill, _) => preparation.Observe(skill)), false).Build(), registration);
        using var client = new UnusedClient();
        var agent = new ChatClientAgent(client);
#pragma warning disable MAAI001
        var result = await provider.InvokingAsync(new(agent, await agent.CreateSessionAsync(), new AIContext()));
#pragma warning restore MAAI001
        Assert.Equal(3, result.Tools!.Count());
        var arguments = JsonSerializer.SerializeToElement(new { skillName = duplicateNames ? "selected" : "skill-0" });
        if (duplicateNames) {
            Assert.Equal("tool-admission.source-preparation-unavailable",
                Assert.Throws<AgentToolAdmissionException>(() => preparation.Prepare(AgentSkillsProvider.LoadSkillToolName, arguments)).Code);
        } else {
            var marker = preparation.Prepare(AgentSkillsProvider.LoadSkillToolName, arguments);
            Assert.Single(MafSkillSourceEvidence.Read(marker).Candidates);
            Assert.True(System.Text.Encoding.UTF8.GetByteCount(marker.PayloadJson) < AgentToolPreparedPayload.MaximumSourcePreparationUtf8Bytes);
        }
    }

    private static AgentSkillsProviderBuilder Configure(AgentSkillsProviderBuilder builder, bool approval)
        => builder.UseOptions(options => {
            options.DisableLoadSkillApproval = true;
            options.DisableReadSkillResourceApproval = true;
            options.DisableRunSkillScriptApproval = !approval;
        });

    private static CapabilityCatalogItem Capability(Guid id, string json) => new(id, CapabilityKind.Skill, "skill", "Skill", "Description", "",
        json, CapabilityProofStatus.Verified, "", DateTimeOffset.UtcNow, true);

    private static AgentToolPreparedPayload LegacyPayload() {
        var arguments = JsonSerializer.SerializeToElement(new { skillName = "skill", scriptName = "run" });
        var name = AgentSkillsProvider.RunSkillScriptToolName;
        return new(name, 1, MafToolProtocolCodec.Digest(new { Name = name, Arguments = arguments }),
            MafToolProtocolCodec.Canonicalize(arguments), AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.ReconcileBeforeRetry);
    }

    private sealed record PreviousPayload(string ToolName, int SemanticVersion, AgentToolSemanticDigest Digest,
        string ArgumentsJson, AgentToolProposalEffect Effect, AgentToolProposalRecovery Recovery);

    private sealed class CountingSource(IList<AgentSkill> skills) : AgentSkillsSource {
        internal int Reads { get; private set; }
        public override Task<IList<AgentSkill>> GetSkillsAsync(AgentSkillsSourceContext context, CancellationToken cancellationToken = default) {
            Reads++;
            return Task.FromResult(skills);
        }
    }

    private sealed class ScopeSource(AgentDefinition agent) : IAgentWorkspaceToolResultSource {
        internal int Reads { get; private set; }
        internal int ActiveLeases { get; private set; }
        public ValueTask<AgentToolProtocolEnvelope> CaptureAsync(AgentRuntimeToolProviderContext context, WorkspaceScopeDescriptor scope,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(AgentToolProtocolEnvelope.Create("fixture-scope", 1, "{}"));
        public ValueTask<AgentToolProtocolEnvelope> CompleteAsync(AgentToolProtocolEnvelope original, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(original);
        public ValueTask<IAgentWorkspaceToolResultReadLease> AcquireReadAsync(AgentRuntimeToolProviderContext context, WorkspaceScopeDescriptor scope,
            AgentToolProtocolEnvelope original, CancellationToken cancellationToken = default) {
            Reads++;
            ActiveLeases++;
            return ValueTask.FromResult<IAgentWorkspaceToolResultReadLease>(new Lease(this, agent));
        }
        private sealed class Lease(ScopeSource owner, AgentDefinition agent) : IAgentWorkspaceToolResultReadLease {
            public AgentDefinition Agent => agent;
            public ImmutableArray<CapabilityCatalogItem> Capabilities => [];
            public void RequireCurrent() => Assert.Equal(1, owner.ActiveLeases);
            public ValueTask DisposeAsync() {
                owner.ActiveLeases--;
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class UnusedClient : IChatClient {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("SDK context inspection does not call a model.");
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("SDK context inspection does not call a model.");
        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
        public void Dispose() { }
    }
}
