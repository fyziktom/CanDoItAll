using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Processes.AgentChat;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class CanonicalAgentExecutionAuthorityResolverTests {
    public enum SandboxSource {
        Agents,
        Projects,
        Processes,
        LiveProcesses,
        Unknown
    }

    [Theory]
    [InlineData(SandboxSource.Agents)]
    [InlineData(SandboxSource.Projects)]
    [InlineData(SandboxSource.Processes)]
    [InlineData(SandboxSource.LiveProcesses)]
    [InlineData(SandboxSource.Unknown)]
    public async Task A_saved_sandbox_scope_is_still_rejected_as_a_new_UI_scope_claim(SandboxSource source) {
        var agent = CreateAgent(new AgentProjectStructureAccessSettings());
        var (resolver, request) = CreateSandboxResolver(source, agent);

        var admitted = await resolver.ResolveAsync(request);
        Assert.Equal(WorkspaceScopeDescriptor.Sandbox, admitted.WorkspaceScope);
        await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(() => resolver.ResolveAsync(
            request with { ObservedWorkspaceScope = admitted.WorkspaceScope }).AsTask());
    }

    [Theory]
    [InlineData(SandboxSource.Agents)]
    [InlineData(SandboxSource.Projects)]
    [InlineData(SandboxSource.Processes)]
    [InlineData(SandboxSource.LiveProcesses)]
    [InlineData(SandboxSource.Unknown)]
    public async Task Restored_captured_sandbox_authority_revalidates_without_publishing_a_new_UI_claim(SandboxSource source) {
        var agent = CreateAgent(new AgentProjectStructureAccessSettings());
        var (resolver, request) = CreateSandboxResolver(source, agent);
        var admitted = await resolver.ResolveAsync(request);
        var original = AgentExecutionGovernanceSnapshot.FromAuthority(admitted);
        var reference = CreateRevalidationReference(request);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var restored = JsonSerializer.Deserialize<AgentExecutionGovernanceSnapshot>(JsonSerializer.Serialize(original, json), json)!;
        var restoredReference = JsonSerializer.Deserialize<AgentTurnContextReference>(JsonSerializer.Serialize(reference, json), json)!;

        var revalidation = AgentExecutionAuthorityResolutionRequest.FromCaptured(restoredReference, restored);
        var current = await resolver.ResolveAsync(revalidation);

        Assert.Equal(reference, revalidation.Revalidation!.Source);
        Assert.Same(restored, revalidation.Revalidation.Authority);
        Assert.Equal(original.AuthorityId, revalidation.Revalidation.Authority.AuthorityId);
        Assert.Equal(original.PolicyFingerprint, revalidation.Revalidation.Authority.PolicyFingerprint);
        Assert.Equal(original.WorkspaceScope, revalidation.ObservedWorkspaceScope);
        Assert.Null(revalidation.UiAccessHint);
        Assert.Equal(admitted.AgentId, current.AgentId);
        Assert.Equal(admitted.DatabaseProfileId, current.DatabaseProfileId);
        Assert.Equal(admitted.DatabaseProfileGeneration, current.DatabaseProfileGeneration);
        Assert.Equal(admitted.WorkspaceScope, current.WorkspaceScope);
        Assert.Equal(admitted.PolicyVersion, current.PolicyVersion);
        Assert.Equal(admitted.PolicyFingerprint, current.PolicyFingerprint);
        Assert.Equal(admitted.ReadAllowed, current.ReadAllowed);
        Assert.Equal(source == SandboxSource.Agents, current.MutationAllowed);
    }

    [Fact]
    public async Task Agents_revalidation_uses_current_tool_permission_and_keeps_the_original_grant_unchanged() {
        var agent = CreateAgent(new AgentProjectStructureAccessSettings());
        AgentDefinition[] catalog = [agent];
        var resolver = CreateResolverWithProviders([new AgentFrameworkAgentsExecutionAuthorityProvider()], catalog);
        var request = CreateSandboxRequest(AgentFrameworkAgentsChatContextBuilder.SourceKind, "chat", agent.Id);
        var original = AgentExecutionGovernanceSnapshot.FromAuthority(await resolver.ResolveAsync(request));
        var captured = AgentExecutionAuthorityResolutionRequest.FromCaptured(CreateRevalidationReference(request), original);
        catalog[0] = agent with { Permissions = agent.Permissions with { CanUseTools = false } };

        var current = await resolver.ResolveAsync(captured);

        Assert.True(original.MutationAllowed);
        Assert.True(current.ReadAllowed);
        Assert.False(current.MutationAllowed);
        Assert.NotEqual(original.PolicyFingerprint, current.PolicyFingerprint);
        catalog[0] = agent with { Status = AgentLifecycleStatus.Draft };
        await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(() => resolver.ResolveAsync(captured).AsTask());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Revalidation_rejects_a_changed_original_profile_or_generation(bool generationChanged) {
        var agent = CreateAgent(new AgentProjectStructureAccessSettings());
        var (resolver, request) = CreateSandboxResolver(SandboxSource.Agents, agent);
        var admitted = await resolver.ResolveAsync(request);
        var original = new AgentExecutionGovernanceSnapshot(admitted.AuthorityId, admitted.AgentId,
            generationChanged ? ProfileId : Guid.NewGuid(),
            generationChanged ? new DatabaseProfileGeneration(2) : admitted.DatabaseProfileGeneration,
            admitted.WorkspaceScope, admitted.ReadAllowed, admitted.MutationAllowed,
            admitted.PolicyVersion, admitted.PolicyFingerprint);
        var revalidation = AgentExecutionAuthorityResolutionRequest.FromCaptured(CreateRevalidationReference(request), original);

        if (generationChanged) {
            await Assert.ThrowsAsync<InvalidOperationException>(() => resolver.ResolveAsync(revalidation).AsTask());
        } else {
            await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(() => resolver.ResolveAsync(revalidation).AsTask());
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Selected_project_sources_revalidate_the_exact_scope_and_current_read_grant(int sourceIndex) {
        IAgentExecutionSourceAuthorityProvider[] providers = [new ProjectsExecutionAuthorityProvider(),
            new ProjectStructureExecutionAuthorityProvider(), new ProcessesExecutionAuthorityProvider(),
            new LiveProcessesExecutionAuthorityProvider()];
        var provider = providers[sourceIndex];
        var agent = CreateAgent(new AgentProjectStructureAccessSettings { CanRead = true, AllowAllProjects = true });
        AgentDefinition[] catalog = [agent];
        var resolver = CreateResolverWithProviders([provider], catalog);
        var sourceId = sourceIndex >= 2 ? $"surface:project:{ProjectId:D}" : ProjectId.ToString("D");
        var request = new AgentExecutionAuthorityResolutionRequest(agent.Id, new(provider.SourceKind), new(sourceId),
            WorkspaceScopeDescriptor.Project(ProjectId.ToString("D")), new(1), UiAccessHint: null);
        var admitted = await resolver.ResolveAsync(request);
        var reference = CreateRevalidationReference(request);
        var original = AgentExecutionGovernanceSnapshot.FromAuthority(admitted);
        var current = await resolver.ResolveAsync(AgentExecutionAuthorityResolutionRequest.FromCaptured(reference, original));
        Assert.Equal(original.WorkspaceScope, current.WorkspaceScope);

        var wrongScope = new AgentExecutionGovernanceSnapshot(admitted.AuthorityId, admitted.AgentId, ProfileId,
            admitted.DatabaseProfileGeneration, WorkspaceScopeDescriptor.Sandbox, true, false,
            admitted.PolicyVersion, admitted.PolicyFingerprint);
        await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(() => resolver.ResolveAsync(
            AgentExecutionAuthorityResolutionRequest.FromCaptured(reference, wrongScope)).AsTask());

        catalog[0] = agent with { ConfigurationJson = AgentProjectStructureAccessMetadata.Write(null,
            new AgentProjectStructureAccessSettings()) };
        await Assert.ThrowsAsync<AgentChatContextAccessDeniedException>(() => resolver.ResolveAsync(
            AgentExecutionAuthorityResolutionRequest.FromCaptured(reference, original)).AsTask());
    }

    [Fact]
    public async Task Revalidation_rejects_a_retargeted_request_and_a_changed_canonical_scope() {
        var agent = CreateAgent(new AgentProjectStructureAccessSettings());
        var (resolver, request) = CreateSandboxResolver(SandboxSource.Agents, agent);
        var original = AgentExecutionGovernanceSnapshot.FromAuthority(await resolver.ResolveAsync(request));
        var captured = AgentExecutionAuthorityResolutionRequest.FromCaptured(CreateRevalidationReference(request), original);
        await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(() => resolver.ResolveAsync(
            captured with { SourceId = new("another-source") }).AsTask());

        var changedResolver = CreateResolverWithProviders([new ChangedScopeProvider(request.SourceKind.Value)], agent);
        await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(() => changedResolver.ResolveAsync(captured).AsTask());
    }

    private static (CanonicalAgentExecutionAuthorityResolver Resolver, AgentExecutionAuthorityResolutionRequest Request)
        CreateSandboxResolver(SandboxSource source, AgentDefinition agent) {
        IAgentExecutionSourceAuthorityProvider? provider = source switch {
            SandboxSource.Agents => new AgentFrameworkAgentsExecutionAuthorityProvider(),
            SandboxSource.Projects => new ProjectsExecutionAuthorityProvider(),
            SandboxSource.Processes => new ProcessesExecutionAuthorityProvider(),
            SandboxSource.LiveProcesses => new LiveProcessesExecutionAuthorityProvider(),
            SandboxSource.Unknown => null,
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        var sourceId = source == SandboxSource.Projects ? ProjectsAgentChatContextBuilder.WorkspaceSourceId : "surface:global";
        return (CreateResolverWithProviders(provider is null ? [] : [provider], agent),
            CreateSandboxRequest(provider?.SourceKind ?? "unregistered-revalidation-test", sourceId, agent.Id));
    }

    private static AgentExecutionAuthorityResolutionRequest CreateSandboxRequest(string sourceKind, string sourceId, Guid agentId)
        => new(agentId, new(sourceKind), new(sourceId), ObservedWorkspaceScope: null, new(1), UiAccessHint: null);

    private static AgentTurnContextReference CreateRevalidationReference(AgentExecutionAuthorityResolutionRequest request)
        => new(AgentTurnContextId.Create(), AgentContextEpochId.Create(), request.SourceKind, request.SourceId,
            "test", "source", 1, "captured-source-digest", DateTimeOffset.UtcNow);

    private sealed class ChangedScopeProvider(string sourceKind) : IAgentExecutionSourceAuthorityProvider {
        public string SourceKind => sourceKind;
        public ValueTask<AgentExecutionSourceAuthorityDecision> ResolveAsync(AgentExecutionSourceAuthorityRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new AgentExecutionSourceAuthorityDecision(
                WorkspaceScopeDescriptor.Project(ProjectId.ToString("D")), true, false, AgentExecutionAuthorityPolicyVersions.Canonical));
    }
}
