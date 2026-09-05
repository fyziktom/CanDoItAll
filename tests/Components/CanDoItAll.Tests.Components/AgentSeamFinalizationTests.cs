using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentSeamFinalizationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Saving_new_agent_while_initial_catalog_load_is_pending_does_not_leave_catalog_loading(bool initialFails) {
        var initial = new TaskCompletionSource<AgentCatalogSnapshot>();
        var operations = new OverlappingCatalogOperations(initial.Task);
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton<IAgentCatalogOperations>(operations));
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        operations.Reload = async token => new(await workspace.ListAgentsAsync(false, token), [], new Dictionary<Guid, bool>());
        var dialogs = harness.Context.Render<DialogHost>();
        var catalog = harness.Context.Render<AgentCatalogHost>(parameters => parameters
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>()));
        await catalog.Find("[data-testid='agents-catalog-new']").ClickAsync();
        var editor = dialogs.WaitForComponent<AgentDetailsDialog>();
        editor.WaitForElement("[data-testid='agents-catalog-name']").Change("Saved during initial catalog read");
        await editor.Find("form").SubmitAsync();
        var savedId = editor.Instance.CurrentTarget.AgentId;
        Assert.NotNull(savedId);

        try {
            catalog.WaitForAssertion(() => {
                var panel = catalog.FindComponent<AgentCatalogPanel>().Instance;
                Assert.False(panel.IsLoading);
                Assert.Contains(panel.Snapshot.Agents, agent => agent.Id == savedId);
                Assert.Equal(savedId, panel.Selection.AgentId);
            });
            await catalog.InvokeAsync(() => {
                if (initialFails) {
                    initial.SetException(new InvalidOperationException("Stale initial read failed."));
                } else {
                    initial.SetResult(AgentCatalogSnapshot.Empty);
                }
            });
            catalog.Render();
            catalog.WaitForAssertion(() => {
                Assert.False(catalog.FindComponent<AgentCatalogPanel>().Instance.IsLoading);
                Assert.Contains(catalog.FindComponent<AgentCatalogPanel>().Instance.Snapshot.Agents, agent => agent.Id == savedId);
                Assert.Equal(2, operations.Loads);
            });
            Assert.DoesNotContain(harness.Context.Services.GetRequiredService<NotificationService>().Messages,
                message => message.Detail?.Contains("Stale initial read", StringComparison.Ordinal) == true);
        } finally {
            initial.TrySetResult(AgentCatalogSnapshot.Empty);
            await harness.Context.Services.GetRequiredService<DialogService>().CloseAsync();
        }
    }

    [Theory]
    [InlineData(AgentEditorSection.Identity, false)]
    [InlineData(AgentEditorSection.Identity, true)]
    [InlineData(AgentEditorSection.Capabilities, false)]
    [InlineData(AgentEditorSection.Capabilities, true)]
    [InlineData(AgentEditorSection.Runtime, false)]
    [InlineData(AgentEditorSection.Runtime, true)]
    public async Task Disposing_or_replacing_editor_session_cancels_owned_nested_dialogs_but_preserves_unrelated_dialogs(
        AgentEditorSection section, bool replaceSession) {
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddAgentEditorReadFixture());
        var reads = harness.Context.Services.GetRequiredService<AgentEditorReadFixture>();
        reads.Load = (target, _) => Task.FromResult(reads.Result(new() { Id = target.AgentId, Name = "Nested dialog owner" }));
        var dialogs = harness.Context.Services.GetRequiredService<DialogService>();
        var unrelated = dialogs.OpenAsync<AgentTeamDetailsDialog>("Independent team editor");
        var editor = harness.Context.Render<AgentDetailsDialog>(parameters => parameters
            .Add(component => component.AgentId, Guid.NewGuid())
            .Add(component => component.Section, section));
        var action = section switch {
            AgentEditorSection.Identity => editor.WaitForElement("[data-testid='agents-catalog-delete']").ClickAsync(),
            AgentEditorSection.Capabilities => editor.WaitForElement("[data-testid='agents-details-new-tool']").ClickAsync(),
            AgentEditorSection.Runtime => editor.WaitForElement("[data-testid='agents-catalog-auto-approval']").ChangeAsync(true),
            _ => throw new ArgumentOutOfRangeException(nameof(section))
        };
        editor.WaitForAssertion(() => Assert.Equal(2, dialogs.Dialogs.Count));
        var owned = dialogs.Dialogs.Last();
        if (replaceSession) {
            editor.Render(parameters => parameters.Add(component => component.AgentId, Guid.NewGuid()));
        } else {
            await editor.InvokeAsync(editor.Instance.Dispose);
        }

        try {
            Assert.True(owned.Result.IsCanceled);
            Assert.False(unrelated.IsCompleted);
            Assert.Equal(typeof(AgentTeamDetailsDialog), Assert.Single(dialogs.Dialogs).ComponentType);
            await action;
            Assert.Empty(harness.Context.Services.GetRequiredService<NotificationService>().Messages);
        } finally {
            await dialogs.CloseAsync(owned);
            await dialogs.CloseAsync();
            await action;
        }
    }

    private sealed class OverlappingCatalogOperations(Task<AgentCatalogSnapshot> initial) : IAgentCatalogOperations {
        public int Loads { get; private set; }
        public Func<CancellationToken, Task<AgentCatalogSnapshot>> Reload { get; set; } = default!;
        public Task<AgentCatalogSnapshot> LoadAsync(AgentCatalogLoadRequest request, CancellationToken cancellationToken = default)
            => ++Loads == 1 ? initial : Reload(cancellationToken);
        public Task DeleteTeamAsync(Guid teamId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateMembersAsync(Guid teamId, IReadOnlyList<Guid> agentIds, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
