using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit;

public sealed class MafContextProviderStateTests
{
    [Fact]
    public void Runtime_scope_resolution_uses_intent_or_transient_scope_before_host_fallback()
    {
        var intentScope = WorkspaceScopeDescriptor.Project("project-intent");
        var transientScope = WorkspaceScopeDescriptor.Project("project-transient");
        var intentOptions = CreateRuntimeOptions() with
        {
            ContextIntent = AgentRuntimeContextIntent.Empty with
            {
                WorkspaceScope = intentScope
            }
        };
        var transientOptions = CreateRuntimeOptions() with
        {
            TransientContext = new AgentRuntimeTransientContext(
                string.Empty,
                transientScope)
        };

        Assert.Equal(
            intentScope,
            MafRuntimeAgentFactory.ResolveContextWorkspaceScope(
                intentOptions,
                WorkspaceScopeDescriptor.Sandbox));
        Assert.Equal(
            transientScope,
            MafRuntimeAgentFactory.ResolveContextWorkspaceScope(
                transientOptions,
                WorkspaceScopeDescriptor.Sandbox));
    }

    [Fact]
    public void Runtime_scope_resolution_rejects_conflicting_internal_scope_authorities()
    {
        var options = CreateRuntimeOptions() with
        {
            ContextWorkspaceScope = WorkspaceScopeDescriptor.Project("project-a"),
            ContextIntent = AgentRuntimeContextIntent.Empty with
            {
                WorkspaceScope = WorkspaceScopeDescriptor.Project("project-b")
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MafRuntimeAgentFactory.ResolveContextWorkspaceScope(
                options,
                WorkspaceScopeDescriptor.Sandbox));

        Assert.Contains("conflicting workspace scopes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Scope_only_transient_context_is_not_attached_as_an_empty_model_message()
    {
        var state = new RuntimeCapabilityState();
        var scopeOnlyContext = new AgentRuntimeTransientContext(
            string.Empty,
            WorkspaceScopeDescriptor.Project("project-1"));

        MafRuntimeAgentFactory.AttachTransientContextProvider(
            state,
            scopeOnlyContext);

        Assert.Empty(state.ContextProviders);
    }

    [Fact]
    public void Populated_transient_context_is_attached_as_model_context()
    {
        var state = new RuntimeCapabilityState();
        var transientContext = new AgentRuntimeTransientContext(
            "Selected project node",
            WorkspaceScopeDescriptor.Project("project-1"));

        MafRuntimeAgentFactory.AttachTransientContextProvider(
            state,
            transientContext);

        var provider = Assert.Single(state.ContextProviders);
        Assert.Contains(
            StaticMessageContextProvider.TransientAgentChatStateKey,
            provider.StateKeys);
    }

    [Fact]
    public void Static_message_context_providers_use_unique_purpose_scoped_state_keys()
    {
        var capabilityId = Guid.NewGuid();
        AIContextProvider[] providers =
        [
            new StaticMessageContextProvider(
                new ChatMessage(ChatRole.System, "Configured context"),
                StaticMessageContextProvider.CreateCapabilityStateKey(capabilityId)),
            new StaticMessageContextProvider(
                new ChatMessage(ChatRole.User, "Transient UI context"),
                StaticMessageContextProvider.TransientAgentChatStateKey)
        ];

        Assert.Equal(
            [
                $"CanDoItAll.StaticContext.Capability.{capabilityId:N}",
                "CanDoItAll.StaticContext.TransientAgentChat"
            ],
            providers.SelectMany(provider => provider.StateKeys));
        Assert.Empty(FindDuplicateStateKeys(providers));
    }

    [Fact]
    public void Rag_context_providers_use_capability_scoped_state_keys()
    {
        var firstCapabilityId = Guid.NewGuid();
        var secondCapabilityId = Guid.NewGuid();
        var builder = new ContextCapabilityBuilder(
            Path.GetTempPath(),
            WorkspaceScopeDescriptor.Sandbox);
        var state = new RuntimeCapabilityState();

        builder.AddRagProvider(
            state,
            CreateRagCapability(firstCapabilityId, "first-rag"),
            new AgentRuntimeConfiguration());
        builder.AddRagProvider(
            state,
            CreateRagCapability(secondCapabilityId, "second-rag"),
            new AgentRuntimeConfiguration());

        Assert.Equal(
            [
                $"CanDoItAll.Rag.{firstCapabilityId:N}",
                $"CanDoItAll.Rag.{secondCapabilityId:N}"
            ],
            state.ContextProviders.SelectMany(provider => provider.StateKeys));
        Assert.Empty(FindDuplicateStateKeys(state.ContextProviders));
    }

    [Fact]
    public void Project_scoped_ambient_rag_remains_attached_before_current_project_media_exists()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            nameof(MafContextProviderStateTests),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceRoot);
        File.WriteAllText(
            Path.Combine(workspaceRoot, "project-structure-context-brief.md"),
            "stale shared context");

        try
        {
            var builder = new ContextCapabilityBuilder(
                workspaceRoot,
                WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")));
            var state = new RuntimeCapabilityState();

            var added = builder.AddRagProvider(
                state,
                CreateRagCapability(Guid.NewGuid(), "workspace-source-rag"),
                new AgentRuntimeConfiguration());

            Assert.True(added);
            Assert.Single(state.ContextProviders);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    private static CapabilityCatalogItem CreateRagCapability(
        Guid capabilityId,
        string key)
    {
        return new CapabilityCatalogItem(
            capabilityId,
            CapabilityKind.Rag,
            key,
            key,
            "Test RAG capability",
            string.Empty,
            "{}",
            CapabilityProofStatus.Verified,
            string.Empty,
            DateTimeOffset.UtcNow,
            IsBuiltIn: false);
    }

    private static AgentRuntimeExecutionOptions CreateRuntimeOptions()
        => new(
            StructuredOutput: null,
            FinalizerMode: AgentFinalizerMode.Disabled,
            RequireStructuredOutputValidation: false,
            MaxStructuredOutputRepairAttempts: 0);

    private static IReadOnlyList<string> FindDuplicateStateKeys(
        IEnumerable<AIContextProvider> providers)
    {
        return providers
            .SelectMany(provider => provider.StateKeys)
            .GroupBy(stateKey => stateKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
    }
}
