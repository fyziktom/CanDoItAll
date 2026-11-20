using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace.Pages.Components;
using CanDoItAll.RemoteInstances;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

[Trait("Category", "HostPlatform")]
public sealed class RemoteAgentTemplateDialogTests {
    [Fact]
    public async Task A_late_first_catalog_cannot_replace_the_selected_second_instance() {
        var first = Summary("First catalog only");
        var second = Summary("Second catalog only");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new TemplateReader(async (address, path) => {
            Assert.Equal(RemoteAgentTemplateRoutes.Catalog, path);
            if (address.Host == "first.example.com") {
                entered.SetResult();
                await release.Task;
                return new[] { first };
            }
            return new[] { second };
        });
        await using var harness = await CreateAsync(reader);
        var sourceA = await CreateSourceAsync(harness, "first");
        var sourceB = await CreateSourceAsync(harness, "second");
        var cut = await OpenAsync(harness);
        var picker = cut.FindComponent<RemoteInstancePicker>();
        var pending = cut.InvokeAsync(() => picker.Instance.SelectedChanged.InvokeAsync(new(sourceA.Id, sourceA.ConcurrencyToken)));
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await cut.InvokeAsync(() => picker.Instance.SelectedChanged.InvokeAsync(new(sourceB.Id, sourceB.ConcurrencyToken)));
            Assert.Contains(second.Name, cut.Find("[data-testid=remote-agent-template-select]").TextContent, StringComparison.Ordinal);
        } finally {
            release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(15));
        var selectedCatalog = cut.Find("[data-testid=remote-agent-template-select]").TextContent;
        Assert.Contains(second.Name, selectedCatalog, StringComparison.Ordinal);
        Assert.DoesNotContain(first.Name, selectedCatalog, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid=remote-agent-template-error]"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_changed_publisher_or_catalog_revision_leaves_import_disabled_and_keeps_the_local_catalog(bool changedPublisher) {
        var entry = Summary("Remote technical template");
        var changed = new RemoteAgentTemplate(entry.Id, entry.Name, "Reviewer", entry.Summary, "Review reports.",
            AgentWorkloadKind.General, [], [], entry.UpdatedAtUtc) {
            PublisherId = changedPublisher ? Guid.NewGuid() : entry.PublisherId,
            CatalogRevision = changedPublisher ? entry.CatalogRevision : entry.CatalogRevision + 1
        };
        var reader = new TemplateReader((_, path) => Task.FromResult<object>(path switch {
            RemoteAgentTemplateRoutes.Catalog => new[] { entry },
            _ when path == RemoteAgentTemplateRoutes.Details(entry.Id) => changed,
            _ => throw new InvalidOperationException("Unexpected template route.")
        }));
        await using var harness = await CreateAsync(reader);
        var source = await CreateSourceAsync(harness, "revision");
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var local = await workspace.SaveAgentDefinitionAsync(new() { Name = "Locally managed review agent" });
        var cut = await OpenAsync(harness);
        cut.Find("[data-testid=remote-instance-select]").Change(source.Id.ToString());
        cut.WaitForElement($"[data-testid=remote-agent-template-select] option[value='{entry.Id:D}']");
        cut.Find("[data-testid=remote-agent-template-select]").Change(entry.Id.ToString());
        cut.WaitForAssertion(() => Assert.Contains("changed", cut.Find("[data-testid=remote-agent-template-error]").TextContent, StringComparison.OrdinalIgnoreCase));
        Assert.True(cut.Find("[data-testid=remote-agent-template-import]").HasAttribute("disabled"));
        var agents = await workspace.ListAgentsAsync();
        Assert.Contains(agents, agent => agent.Id == local.Id);
        Assert.DoesNotContain(agents, agent => agent.Name == entry.Name);
    }

    [Fact]
    public async Task A_failed_remote_read_preserves_the_local_agent_and_the_operator_can_switch_sources() {
        var online = Summary("Reachable template");
        var reader = new TemplateReader((address, _) => address.Host == "offline.example.com"
            ? Task.FromException<object>(new HttpRequestException("test transport unavailable"))
            : Task.FromResult<object>(new[] { online }));
        await using var harness = await CreateAsync(reader);
        var offline = await CreateSourceAsync(harness, "offline");
        var reachable = await CreateSourceAsync(harness, "reachable");
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var local = await workspace.SaveAgentDefinitionAsync(new() { Name = "Local accounting agent" });
        var cut = await OpenAsync(harness);
        void AssertLocalCardVisible() => Assert.Contains(cut.FindAll("[data-testid=agents-catalog-card-shell]"),
            card => card.TextContent.Contains(local.Name, StringComparison.Ordinal));
        cut.WaitForAssertion(AssertLocalCardVisible);
        cut.Find("[data-testid=remote-instance-select]").Change(offline.Id.ToString());
        cut.WaitForElement("[data-testid=remote-agent-template-error]");
        AssertLocalCardVisible();
        cut.Find("[data-testid=remote-instance-select]").Change(reachable.Id.ToString());
        cut.WaitForElement($"[data-testid=remote-agent-template-select] option[value='{online.Id:D}']");
        Assert.Empty(cut.FindAll("[data-testid=remote-agent-template-error]"));
    }

    private static RemoteAgentTemplateSummary Summary(string name) => new(Guid.NewGuid(), name, "Review template", DateTimeOffset.UtcNow, 0) {
        PublisherId = Guid.NewGuid(), CatalogRevision = 1
    };

    private static Task<ComponentTestHarness> CreateAsync(TemplateReader reader) =>
        ComponentTestHarness.CreateAsync(services => services.AddSingleton<IRemoteInstanceJsonReader>(reader));

    private static async Task<RemoteInstanceConnection> CreateSourceAsync(ComponentTestHarness harness, string name) {
        var credential = await harness.Context.Services.GetRequiredService<SecretService>().SaveAsync(new SecretEditorModel {
            Name = $"{name} remote credential",
            Kind = SecretKind.ApiKey,
            SecretValue = "component-remote-test-token"
        });
        Assert.True(credential.IsSuccess);
        return await harness.Context.Services.GetRequiredService<IRemoteInstanceRegistry>().CreateAsync(Guid.NewGuid(),
            new(name, new Uri($"https://{name}.example.com/"), credential.Value, true, false));
    }

    private static async Task<IRenderedComponent<AgentCatalogHost>> OpenAsync(ComponentTestHarness harness) {
        var cut = harness.Context.Render<AgentCatalogHost>(parameters => parameters.Add(host => host.SkipCatalogRepair, true));
        await cut.WaitForElement("[data-testid=agents-catalog-import]").ClickAsync();
        cut.WaitForElement("[data-testid=remote-instance-select] option:not([value=''])");
        return cut;
    }

    private sealed class TemplateReader(Func<Uri, string, Task<object>> read) : IRemoteInstanceJsonReader {
        public async Task<T> ReadAsync<T>(Uri baseUri, RemoteInstanceNetworkPolicy networkPolicy,
            RemoteInstanceAccessToken credential, string relativeApiPath, CancellationToken cancellationToken) {
            Assert.Equal(RemoteInstanceNetworkPolicy.PublicOnly, networkPolicy);
            Assert.True(credential.UseValue(value => value == "component-remote-test-token"));
            return Assert.IsType<T>(await read(baseUri, relativeApiPath));
        }
    }
}
