using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Resources.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ResourcesPageAgentChatContextTests
{
    [Fact]
    public async Task Missing_explicit_project_keeps_the_resources_context_failed()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var registry = harness.Context.Services.GetRequiredService<IAgentChatContextRegistry>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var missingProjectId = Guid.NewGuid();
        navigation.NavigateTo(navigation.GetUriWithQueryParameter("projectId", missingProjectId));

        var cut = harness.Context.RenderComponent<ResourcesPage>();

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(AgentChatContextAccessState.Failed, snapshot.Scope.AccessState);
            Assert.Null(snapshot.Scope.SurfacePosition?.PrimarySelection);
            Assert.DoesNotContain(
                snapshot.Scope.SurfacePosition?.SelectedEntities ?? [],
                entity => entity.Kind == "project");
        });

        await Assert.ThrowsAsync<AgentChatContextUnavailableException>(
            async () => await registry.CaptureAsync());
    }
}
