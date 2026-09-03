using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using ProviderProfileEditorModel = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Modules.AgentFramework.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ProviderAdministrationLayoutTests {
    [Theory]
    [InlineData(AgentWorkspaceTabs.Providers)]
    [InlineData(AgentWorkspaceTabs.RequestHistory)]
    public async Task History_hosts_make_no_aggregate_or_history_reads_until_requested(string tab) {
        var history = new ProviderHistoryUiFixture();
        var usage = new RecordingUsageSource();
        var overviewReads = 0;
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IProviderRequestHistory>(history);
            services.RemoveAll<IProviderUsageProjectionSource>();
            services.AddSingleton<IProviderUsageProjectionSource>(usage);
            var workspaceFactory = services.Last(descriptor => descriptor.ServiceType == typeof(IAgentFrameworkWorkspaceService))
                .ImplementationFactory ?? throw new InvalidOperationException("Expected the existing workspace factory registration.");
            services.AddScoped<IAgentFrameworkWorkspaceService>(provider => {
                var proxy = DispatchProxy.Create<IAgentFrameworkWorkspaceService, RecordingWorkspaceProxy>();
                var recorder = (RecordingWorkspaceProxy)(object)proxy;
                recorder.Target = (IAgentFrameworkWorkspaceService)workspaceFactory(provider);
                recorder.OverviewRead = () => overviewReads++;
                return proxy;
            });
        });
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/agents?tab={tab}&usageScope=agents");
        var cut = harness.Context.Render<AgentsHomePage>();
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid='agents-hr-agent-open-header']").HasAttribute("disabled")), TimeSpan.FromSeconds(10));
        if (tab == AgentWorkspaceTabs.Providers) {
            cut.WaitForElement("[data-testid='providers-tree-provider']");
            OpenProviderTab(cut, "History");
        }
        cut.WaitForElement("[data-testid='history-search-form']");
        Assert.Empty(history.Queries);
        Assert.Equal(0, history.MetadataReads);
        Assert.Empty(history.ContentReads);
        Assert.Equal(0, usage.Reads);
        Assert.Equal(0, overviewReads);
        cut.FindAll("[data-testid='agents-shell-tabs'] button").Single(button => button.TextContent.Trim() == "Overview").Click();
        cut.WaitForElement("[data-testid='agents-overview-dashboard']");
        cut.WaitForDashboardLoaded();
        cut.WaitForAssertion(() => {
            Assert.Equal(1, usage.Reads);
            Assert.Equal(1, overviewReads);
        });
        Assert.Empty(history.Queries);
    }

    [Fact]
    public async Task History_submit_cannot_save_provider_edits_and_editor_context_survives_tab_switches() {
        var history = new ProviderHistoryUiFixture();
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<IProviderRequestHistory>(history));
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-tree-provider']");
        var context = cut.FindComponent<ProviderProfileEditorForm>().Instance.Context;
        var model = Assert.IsType<ProviderProfileEditorModel>(context.Model);
        var originalName = model.Name;
        cut.Find("[data-testid='providers-name-input']").Change("Unsaved provider edit");
        OpenProviderTab(cut, "History");
        var form = cut.WaitForElement("[data-testid='history-search-form']");
        Assert.Single(cut.FindAll("form"));
        Assert.Empty(cut.FindAll("[data-testid='providers-save']"));
        form.Submit();
        Assert.Single(history.Queries);
        var service = harness.Context.Services.GetRequiredService<IProviderRuntimeAdministrationService>();
        Assert.Equal(originalName, (await service.GetProviderEditorAsync(model.Id!.Value)).Name);
        OpenProviderTab(cut, "Connection");
        Assert.Same(context, cut.FindComponent<ProviderProfileEditorForm>().Instance.Context);
        Assert.Equal("Unsaved provider edit", model.Name);
        cut.Find("[data-testid='providers-new']").Click();
        OpenProviderTab(cut, "History");
        Assert.Contains("Save this provider first", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='history-search-form']"));
        Assert.Single(history.Queries);
    }

    private static void OpenProviderTab<T>(IRenderedComponent<T> cut, string name) where T : IComponent =>
        cut.FindAll("button[role='tab']").Single(button => button.TextContent.Contains(name, StringComparison.Ordinal)).Click();

    public class RecordingWorkspaceProxy : DispatchProxy {
        public IAgentFrameworkWorkspaceService Target { get; set; } = default!;
        public Action OverviewRead { get; set; } = default!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod!.Name == nameof(IAgentFrameworkWorkspaceService.GetAgentOverviewAsync)) {
                OverviewRead();
            }
            return targetMethod.Invoke(Target, args);
        }
    }

    private sealed class RecordingUsageSource : IProviderUsageProjectionSource {
        public string SourceName => nameof(RecordingUsageSource);
        public ProviderUsageWorkloadKind WorkloadKind => ProviderUsageWorkloadKind.Agent;
        internal int Reads { get; private set; }
        public ValueTask<ProviderUsageSourceResult> ReadAsync(CancellationToken cancellationToken = default) {
            Reads++;
            return ValueTask.FromResult(new ProviderUsageSourceResult(SourceName, WorkloadKind, ProviderUsageSourceState.Complete, [], DateTimeOffset.UtcNow));
        }
    }

    [Fact]
    public async Task Toolbar_is_icon_only_and_connections_load_only_when_opened() {
        var service = new RecordingSharedProviderManagementService(
            SharedProviderPublicationPanelTests.CreateLocalState(Guid.NewGuid(), false, true));
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddSingleton<ISharedProviderManagementService>(service));
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();

        foreach (var id in new[] { "providers-new", "providers-refresh", "providers-connections" }) {
            var button = cut.WaitForElement($"[data-testid='{id}']");
            Assert.False(string.IsNullOrWhiteSpace(button.GetAttribute("aria-label")));
            Assert.Empty(button.QuerySelectorAll(".cda-button__text"));
        }
        Assert.Empty(cut.FindAll("[data-testid='shared-provider-connections-dialog']"));
        Assert.Equal(0, service.ListSourcesCallCount);

        cut.Find("[data-testid='providers-connections']").Click();
        cut.WaitForElement("[data-testid='shared-provider-connections-dialog']");
        Assert.Equal(1, service.ListSourcesCallCount);
        cut.Find("[data-testid='shared-provider-source-add']").Click();
        cut.WaitForElement("[data-testid='shared-provider-source-dialog']");
    }

    [Fact]
    public async Task Publication_settings_do_not_render_or_load_source_connections() {
        var service = new RecordingSharedProviderManagementService(
            SharedProviderPublicationPanelTests.CreateLocalState(Guid.NewGuid(), false, true));
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddSingleton<ISharedProviderManagementService>(service));
        var cut = harness.Context.Render<SharedProviderManagementPanel>();

        Assert.Contains("No provider selected", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='shared-provider-source-add']"));
        Assert.Equal(0, service.ListSourcesCallCount);
    }

    [Fact]
    public async Task Compact_filter_can_be_cleared() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();

        cut.WaitForElement("[data-testid='providers-search']").Input("no-such-provider");
        cut.WaitForAssertion(() => Assert.StartsWith("0 /", cut.Find("[data-testid='providers-filter-count']").TextContent));
        cut.Find("[data-testid='providers-search-reset']").Click();
        cut.WaitForAssertion(() => Assert.Equal(string.Empty, cut.Find("[data-testid='providers-search']").GetAttribute("value") ?? string.Empty));
    }
}
