using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Memory;
using CanDoItAll.AgentFramework.Memory.Context;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Mock;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

[Trait("Category", "HostPlatform")]
public sealed class AgentEditorMemoryRuntimeTests : AgentMemorySettingsPanelTestBase {
    [Fact]
    public async Task UI_saved_binding_dispatches_real_memory_query_and_disabled_provider_is_refused() {
        await using var harness = await ComponentTestHarness.CreateAsync(options: new TestHarnessOptions {
            ConfigurationOverrides = new Dictionary<string, string?> { ["Memory:Providers:DeterministicMock:Enabled"] = "true" }
        });
        var services = harness.Context.Services;
        var profiles = services.GetRequiredService<IMemoryProviderProfileStore>();
        var profile = CreateProvider("editor.runtime-memory", "Editor runtime memory", true);
        await profiles.UpsertAsync(profile, DateTimeOffset.UtcNow);
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var id = await workspace.SaveAgentAsync(new() {
            Name = "UI memory runtime proof",
            MemoryAccess = new() {
                AllowedCapabilityIds = [MemoryCapabilityIds.ContextQuerySync],
                AllowedSourceScopes = [MemorySourceScope.Manual, MemorySourceScope.Project]
            }
        });
        var cut = harness.Context.Render<AgentDetailsDialog>(p => p.Add(x => x.AgentId, id)
            .Add(x => x.InitialProviders, Array.Empty<ProviderProfile>()).Add(x => x.Section, AgentEditorSection.Memory));
        cut.WaitForElement("[data-testid='agents-catalog-memory-new-provider'] option[value='editor.runtime-memory']");
        cut.Find("[data-testid='agents-catalog-memory-mode']").Change(nameof(AgentMemoryInvocationMode.Automatic));
        cut.Find("[data-testid='agents-catalog-memory-required']").Change(true);
        cut.Find("[data-testid='agents-catalog-crm-source-read']").Change(true);
        cut.Find("[data-testid='agents-catalog-memory-new-alias']").Input("review");
        cut.Find("[data-testid='agents-catalog-memory-new-provider']").Change(profile.InstanceId.Value);
        cut.Find("[data-testid='agents-catalog-memory-new-requirement']").Change(nameof(AgentMemoryProviderRequirement.Required));
        await cut.Find("[data-testid='agents-catalog-memory-add-binding']").ClickAsync();
        await cut.Find("form").SubmitAsync();
        var saved = await workspace.GetAgentEditorAsync(id);
        Assert.Equal("review", Assert.Single(saved.MemoryAccess.ProviderBindings).Alias.Value);
        Assert.Contains(MemorySourceScope.Crm, saved.MemoryAccess.AllowedSourceScopes);
        Assert.Contains(MemorySourceScope.Project, saved.MemoryAccess.AllowedSourceScopes);
        var definition = Assert.Single(await workspace.ListAgentsAsync(false), agent => agent.Id == id);
        var contributor = new MemoryAgentContextContributor(services.GetRequiredService<IMemoryOperationHandler>(), TimeProvider.System);
        var driver = services.GetRequiredService<DeterministicMockMemoryProviderDriver>();
        var before = driver.DispatchCount;
        var request = new AgentContextContributionRequest(definition,
            AgentDetailsDialogAvatarGenerationTests.CreateImageProvider() with { Purpose = ProviderProfilePurpose.Chat },
            [new(AgentContextMessageRole.User, "Recall editor fixture context")],
            new(AgentContextExecutionMode.InteractiveChat, false, WorkspaceScopeDescriptor.Organization("editor-fixture")));
        var provided = await contributor.ContributeAsync(request);
        Assert.Equal(AgentContextContributionStatus.Provided, provided.Status);
        Assert.Contains("Mock memory context", Assert.Single(provided.Messages).Text);
        Assert.Equal(before + 1, driver.DispatchCount);
        await profiles.UpsertAsync(profile with { IsEnabled = false }, DateTimeOffset.UtcNow);
        var refused = await contributor.ContributeAsync(request);
        Assert.Equal(AgentContextContributionStatus.Failed, refused.Status);
        Assert.Equal(before + 1, driver.DispatchCount);
        var retained = await workspace.GetAgentEditorAsync(id);
        Assert.Equal(saved.ExpectedUpdatedAtUtc, retained.ExpectedUpdatedAtUtc);
        Assert.Equal(saved.MemoryAccess.ProviderBindings, retained.MemoryAccess.ProviderBindings);
        Assert.Equal(saved.MemoryAccess.AllowedSourceScopes, retained.MemoryAccess.AllowedSourceScopes);
    }
}
