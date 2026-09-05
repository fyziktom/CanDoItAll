using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentEditorReadCompositionTests {
    [Fact]
    public async Task Registered_reader_loads_existing_identity_version_and_reference_adapters() {
        await using var host = await AgentUiAdapterTestHost.CreateAsync();
        await using var scope = host.App.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agent = (await workspace.ListAgentsAsync(false)).First();
        var reads = Assert.IsType<AgentEditorReads>(scope.ServiceProvider.GetRequiredService<IAgentEditorReads>());
        var access = Assert.IsType<AgentEditorAccessQuery>(scope.ServiceProvider.GetRequiredService<IAgentEditorAccessQuery>());
        var result = await reads.LoadAsync(new(agent.Id), initialProviders: []);
        Assert.Equal(agent.Id, result.Draft.Id);
        Assert.Equal(agent.Name, result.Draft.Name);
        Assert.Equal(agent.UpdatedAtUtc, result.Draft.ExpectedUpdatedAtUtc);
        Assert.Empty(result.Providers.Items);
        Assert.Null(result.Providers.Error);
        Assert.Null(result.Secrets.Error);
        Assert.Equal(await access.ReadSecretsAsync(), result.Secrets.Items);
        Assert.Equal(await access.ReadProjectsAsync(), await reads.ReadProjectsAsync());
    }

    [Fact]
    public async Task Registered_reader_returns_independent_drafts_for_new_and_existing_targets() {
        await using var host = await AgentUiAdapterTestHost.CreateAsync();
        await using var scope = host.App.Services.CreateAsyncScope();
        var reads = scope.ServiceProvider.GetRequiredService<IAgentEditorReads>();
        var first = await reads.LoadAsync(AgentEditorTarget.Create, initialProviders: []);
        var second = await reads.LoadAsync(AgentEditorTarget.Create, initialProviders: []);
        Assert.Null(first.Draft.Id);
        Assert.Null(first.Draft.ExpectedUpdatedAtUtc);
        Assert.NotSame(first.Draft, second.Draft);
        first.Draft.Name = "Independent new draft";
        Assert.Empty(second.Draft.Name);
        var agent = first.Agents.First();
        var existing = await reads.LoadAsync(new(agent.Id), initialProviders: []);
        Assert.Equal(agent.Id, existing.Draft.Id);
        Assert.Equal(agent.UpdatedAtUtc, existing.Draft.ExpectedUpdatedAtUtc);
    }
}
