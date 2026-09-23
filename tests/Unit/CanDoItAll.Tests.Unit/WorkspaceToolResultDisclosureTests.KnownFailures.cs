using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class WorkspaceToolResultDisclosureTests {
    [Theory]
    [InlineData(ToolContractCatalog.WorkspaceWriteFile)]
    [InlineData(ToolContractCatalog.WorkspaceReadFile)]
    public async Task A_rejection_with_no_effect_keeps_disclosure_authority_for_an_approval_replay(string toolName) {
        var root = TestFileSystem.CreateTemporaryRoot("workspace-known-failure");
        var outside = TestFileSystem.CreateTemporaryRoot("workspace-known-failure-outside");
        RuntimeCapabilityState? state = null;
        try {
            var source = new CurrentSource();
            using var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection()
                .AddSingleton<IAgentWorkspaceToolResultSource>(source).BuildServiceProvider();
            (state, source.Agent) = await ComposeWithActorAsync(services, root);
            var tool = Assert.IsAssignableFrom<AIFunction>(Assert.Single(state.Tools, item => item.Name == toolName));
            var arguments = toolName == ToolContractCatalog.WorkspaceWriteFile
                ? new AIFunctionArguments { ["path"] = "../escaped.txt", ["content"] = "never written" }
                : new AIFunctionArguments { ["path"] = Path.Combine(outside, "private.txt") };

            using var effect = AgentToolInvocationEffectScope.Begin();
            JsonElement saved;
            var typedFailure = false;
            try {
                var returned = await tool.InvokeAsync(arguments);
                saved = returned is JsonElement element ? element : JsonSerializer.SerializeToElement(returned);
                Assert.False(saved.GetProperty("succeeded").GetBoolean());
                Assert.True(effect.RejectedBeforeEffect);
            } catch (Exception exception) when (exception is IAgentToolFailureEffectEvidence {
                EffectState: AgentToolEffectState.None or AgentToolEffectState.NotCommitted
            } failure) {
                typedFailure = true;
                saved = JsonSerializer.SerializeToElement(new AgentToolFailureResult(false, failure.ErrorCode, failure.SafeMessage,
                    failure.CanRetryWithCorrectedInput) { EffectState = failure.EffectState });
            }

            Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(root)!, "escaped.txt")));
            Assert.Null(effect.CommittedEffect);
            var evidence = WorkspaceToolResultEvidence.Read(effect.DisclosureEvidence!);
            Assert.Equal(WorkspaceToolResultEvidenceState.KnownFailure, evidence.State);
            Assert.Equal(toolName, evidence.ToolName);

            var metadata = Assert.Single(state.RuntimeToolMetadata, item => item.ToolName == toolName);
            var payload = new AgentToolPreparedPayload(toolName, 1, AgentToolProtocolEnvelope.ComputeDigest(toolName), "{}",
                toolName == ToolContractCatalog.WorkspaceReadFile ? AgentToolProposalEffect.Read : AgentToolProposalEffect.Mutation,
                AgentToolProposalRecovery.RevalidateAndRead);
            var disclosure = new AgentToolResultDisclosure(new AgentToolBusinessIntentId(Guid.NewGuid()), payload,
                typedFailure ? AgentToolEffectState.None : AgentToolEffectState.NotCommitted, saved, effect.DisclosureEvidence) {
                IsTypedFailure = typedFailure
            };
            await using (var lease = await metadata.AuthorizeResultDisclosureAsync!(disclosure, default)) {
                Assert.NotNull(lease);
            }

            var uncertain = await Assert.ThrowsAsync<AgentToolAdmissionException>(() =>
                metadata.AuthorizeResultDisclosureAsync!(disclosure with { EffectState = AgentToolEffectState.Unknown }, default).AsTask());
            Assert.Equal("workspace.result-authority-unavailable", uncertain.Code);

            source.Agent = source.Agent with {
                ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write("{}", new() {
                    Profile = AgentWorkspaceToolProfileKind.Custom, CanReadFiles = false, CanWriteFiles = false
                })
            };
            var revoked = await Assert.ThrowsAsync<AgentToolAdmissionException>(() =>
                metadata.AuthorizeResultDisclosureAsync!(disclosure, default).AsTask());
            Assert.Equal("workspace.result-disclosure-denied", revoked.Code);
        } finally {
            if (state is not null) {
                Assert.Empty(await state.DisposeAcquiredResourcesAsync());
            }
            TestFileSystem.DeleteDirectoryWithRetry(root);
            TestFileSystem.DeleteDirectoryWithRetry(outside);
        }
    }

    private static async Task<(RuntimeCapabilityState State, AgentDefinition Actor)> ComposeWithActorAsync(
        IServiceProvider services, string root) {
        var seed = SandboxWorkspaceSeedFactory.Create().ToCatalog();
        var actor = seed.Agents.First(item => item.ProviderProfileId.HasValue);
        var provider = seed.Providers.Single(item => item.Id == actor.ProviderProfileId);
        actor = actor with {
            Permissions = actor.Permissions with { CanUseTools = true },
            ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write("{}", new() {
                Profile = AgentWorkspaceToolProfileKind.Custom, CanReadFiles = true, CanWriteFiles = true
            }),
            Capabilities = []
        };
        var state = await RuntimeCapabilityComposer.CreateDefault(root, services).CreateCapabilityStateCoreAsync(actor, provider,
            provider.DefaultModel, [], [], (_, _, _) => Task.CompletedTask, default, false, WorkspaceScopeDescriptor.Sandbox,
            AgentRuntimeContextIntent.Empty with { Purpose = AgentRuntimeContextPurpose.InteractiveChat, WorkspaceToolsEnabled = true,
                ToolCapabilitiesEnabled = true, RuntimeToolProvidersEnabled = false }, WorkspaceRuntimeServicesTestFactory.Create(root),
            admittedToolSession: new(Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid())));
        return (state, actor);
    }

    private sealed class CurrentSource : IAgentWorkspaceToolResultSource {
        private static readonly AgentToolProtocolEnvelope Captured = AgentToolProtocolEnvelope.Create("fixture-source", 1, "{}");

        internal AgentDefinition Agent { get; set; } = null!;

        public ValueTask<AgentToolProtocolEnvelope> CaptureAsync(AgentRuntimeToolProviderContext context,
            WorkspaceScopeDescriptor workspaceScope, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Captured);

        public ValueTask<AgentToolProtocolEnvelope> CompleteAsync(AgentToolProtocolEnvelope original,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(original);

        public ValueTask<IAgentWorkspaceToolResultReadLease> AcquireReadAsync(AgentRuntimeToolProviderContext context,
            WorkspaceScopeDescriptor workspaceScope, AgentToolProtocolEnvelope original, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IAgentWorkspaceToolResultReadLease>(new CurrentLease(Agent));
    }

    private sealed class CurrentLease(AgentDefinition agent) : IAgentWorkspaceToolResultReadLease {
        public AgentDefinition Agent { get; } = agent;
        public ImmutableArray<CapabilityCatalogItem> Capabilities { get; } = [];
        public void RequireCurrent() {
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
