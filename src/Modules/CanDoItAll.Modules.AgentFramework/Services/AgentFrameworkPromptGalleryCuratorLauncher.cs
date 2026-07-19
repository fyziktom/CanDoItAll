using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Prompts;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class AgentFrameworkPromptGalleryCuratorLauncher(
    IAgentChatLauncher agentChatLauncher,
    IAgentChatContextRegistry contextRegistry,
    NavigationManager navigation) : IPromptGalleryCuratorLauncher
{
    public bool IsAvailable => true;

    public IPromptGalleryCuratorContextLease ActivateContext()
    {
        var surface = PromptGalleryAgentChatContextBuilder.Build();
        var scopeLease = contextRegistry.ActivateScope(
            surface.ToScope(AgentChatContextScopeId.Create()));
        return new ContextLease(scopeLease, navigation);
    }

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        _ = await agentChatLauncher.StartNewChatAsync(
            PromptsCuratorAgentIdentity.AgentId,
            cancellationToken);
    }

    private sealed class ContextLease(
        IAgentChatContextScopeLease scopeLease,
        NavigationManager navigation) : IPromptGalleryCuratorContextLease
    {
        private IAgentChatContextScopeLease? activeScopeLease = scopeLease;

        public void SynchronizeNavigation()
        {
            var lease = activeScopeLease ?? throw new ObjectDisposedException(nameof(ContextLease));
            lease.SynchronizeNavigation(
                AgentChatNavigationIdentity.CreateForLocation(
                    navigation.BaseUri,
                    navigation.Uri));
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref activeScopeLease, null)?.Dispose();
        }
    }
}
