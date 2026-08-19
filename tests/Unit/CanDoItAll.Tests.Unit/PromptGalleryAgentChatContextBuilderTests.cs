using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class PromptGalleryAgentChatContextBuilderTests
{
    [Fact]
    public async Task Matching_gallery_positions_capture_curator_read_and_mutate_access()
    {
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var navigation = AgentChatNavigationIdentity.CreateForLocation(
            "http://localhost/",
            "http://localhost/prompt-gallery");
        using var workspaceLease = registry.RegisterWorkspacePosition(
            new AgentChatWorkspacePosition(
                "route:prompt-gallery",
                "Prompt Gallery",
                PromptGalleryAgentChatContextBuilder.Route,
                "page"),
            navigation);
        var surface = PromptGalleryAgentChatContextBuilder.Build();
        using var scopeLease = registry.ActivateScope(
            surface.ToScope(AgentChatContextScopeId.Create()));
        scopeLease.SynchronizeNavigation(navigation);

        var snapshot = Assert.IsType<AgentChatContextSnapshot>(
            await registry.CaptureAsync());

        Assert.Equal(PromptGalleryAgentChatContextBuilder.SourceKind, snapshot.Scope.Source.Kind.Value);
        Assert.Equal(PromptGalleryAgentChatContextBuilder.SourceId, snapshot.Scope.Source.Id.Value);
        Assert.Equal(PromptGalleryAgentChatContextBuilder.Route, snapshot.WorkspacePosition?.Route);
        Assert.Equal(PromptGalleryAgentChatContextBuilder.Route, snapshot.Scope.SurfacePosition?.Route);
        Assert.Equal(PromptGalleryAgentChatContextBuilder.Module, snapshot.Scope.SurfacePosition?.Module);
        Assert.Equal(PromptGalleryAgentChatContextBuilder.Surface, snapshot.Scope.SurfacePosition?.Surface);
        Assert.Equal(PromptGalleryAgentChatContextBuilder.View, snapshot.Scope.SurfacePosition?.View);
        Assert.Equal(AgentChatContextScopeAccessMode.Unrestricted, snapshot.Scope.AccessMode);

        var curatorAccess = Assert.Single(snapshot.Scope.AgentAccess);
        Assert.Equal(PromptsCuratorAgentIdentity.AgentId, curatorAccess.AgentId);
        Assert.True(curatorAccess.CanRead);
        Assert.True(curatorAccess.CanMutate);
        Assert.True(snapshot.CanRead(PromptsCuratorAgentIdentity.AgentId));
        Assert.True(snapshot.CanRead(Guid.NewGuid()));
    }
}
