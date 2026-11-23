using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Editing;
using CanDoItAll.Modules.AgentFramework;
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
            Assert.Contains(second.Name, cut.Find("[data-testid=remote-template-list]").TextContent, StringComparison.Ordinal);
        } finally {
            release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(15));
        var selectedCatalog = cut.Find("[data-testid=remote-template-list]").TextContent;
        Assert.Contains(second.Name, selectedCatalog, StringComparison.Ordinal);
        Assert.DoesNotContain(first.Name, selectedCatalog, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid=remote-template-error]"));
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
        cut.WaitForElement(Checkbox(entry)).Change(true);
        cut.WaitForAssertion(() => Assert.Contains("changed", cut.Find("[data-testid=remote-template-item-error]").TextContent, StringComparison.OrdinalIgnoreCase));
        Assert.True(cut.Find("[data-testid=remote-template-import]").HasAttribute("disabled"));
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
        cut.WaitForElement("[data-testid=remote-template-error]");
        AssertLocalCardVisible();
        cut.Find("[data-testid=remote-instance-select]").Change(reachable.Id.ToString());
        cut.WaitForElement(Checkbox(online));
        Assert.Empty(cut.FindAll("[data-testid=remote-template-error]"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Batch_import_shows_avatars_before_selection_and_preserves_each_model_review(bool applyToAll, bool secondNeedsTools) {
        var avatar = AgentAvatarImageCatalog.BundledAvatarUrls[1];
        var first = Summary("First published author") with { AvatarImageUrl = avatar };
        var second = first with { Id = Guid.NewGuid(), Name = "Second published author" };
        var published = new[] { first, second };
        var retrySecond = false;
        var reader = new TemplateReader((_, path) => {
            if (path == RemoteAgentTemplateRoutes.Catalog) {
                return Task.FromResult<object>(published);
            }
            var row = published.Single(row => path == RemoteAgentTemplateRoutes.Details(row.Id));
            var document = Document(row) with {
                PreferredModel = new(row.Name, ProviderProfilePurpose.Chat, Tools: row.Id == second.Id && secondNeedsTools),
                CatalogRevision = row.Id == second.Id && retrySecond ? row.CatalogRevision + 1 : row.CatalogRevision
            };
            return Task.FromResult<object>(document);
        });
        await using var harness = await CreateAsync(reader);
        var source = await CreateSourceAsync(harness, "regional");
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        const string primary = "regional-primary";
        const string alternate = "regional-alternate";
        Task<Guid> AddProviderAsync(string name, bool tools) => workspace.SaveProviderAsync(new() {
            Name = name, Kind = ProviderKind.Ollama, Transport = ProviderTransportKind.ChatCompletions,
            BaseUrl = "http://127.0.0.1:11434", DefaultModel = primary, SuggestedModels = [primary, alternate], SupportsTools = tools,
            ModelPrices = [new() { Model = alternate, TariffKind = ProviderTariffKind.ExplicitFree }]
        });
        var provider = await AddProviderAsync("Regional substitute", false);
        var secondProvider = secondNeedsTools ? await AddProviderAsync("Regional tool provider", true) : provider;
        var cut = await OpenAsync(harness);
        cut.Find("[data-testid=remote-instance-select]").Change(source.Id.ToString());
        cut.WaitForElement(Checkbox(second));
        foreach (var row in published) {
            var image = cut.Find($"[data-template-id='{row.Id:D}'] img[data-testid=remote-template-avatar]");
            Assert.Equal(avatar, image.GetAttribute("src"));
            Assert.Equal(row.Name, image.GetAttribute("alt"));
            Assert.False(cut.Find(Checkbox(row)).HasAttribute("checked"));
        }
        foreach (var row in published) {
            cut.Find(Checkbox(row)).Change(true);
        }
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid=remote-template-import]").HasAttribute("disabled")));
        await cut.Find("[data-testid=remote-template-import]").ClickAsync();
        var dialog = cut.WaitForElement("[data-testid=remote-template-model-dialog]");
        Assert.Contains(first.Name, dialog.TextContent, StringComparison.Ordinal);
        Assert.True(cut.Find("[data-testid=remote-template-model-confirm]").HasAttribute("disabled"));
        Assert.Equal("_blank", Assert.Single(dialog.QuerySelectorAll("a"), link =>
            link.TextContent.Contains("Manage provider connections", StringComparison.Ordinal)).GetAttribute("target"));
        await cut.Find("[data-testid=remote-template-model-refresh]").ClickAsync();
        await cut.Find("[data-testid=remote-template-model-cancel]").ClickAsync();
        Assert.Empty(cut.FindAll("[data-testid=remote-template-model-dialog]"));
        Assert.All(published, row => Assert.True(cut.Find(Checkbox(row)).HasAttribute("checked")));

        await cut.Find("[data-testid=remote-template-import]").ClickAsync();
        cut.Find("[data-testid=remote-template-provider]").Change(provider.ToString("D"));
        cut.Find("[data-testid=remote-template-model]").Change(alternate);
        if (applyToAll) {
            retrySecond = !secondNeedsTools;
            await cut.Find("[data-testid=remote-template-model-all]").ClickAsync();
        } else {
            await cut.Find("[data-testid=remote-template-model-confirm]").ClickAsync();
        }
        if (!applyToAll || secondNeedsTools) {
            Assert.Contains(second.Name, cut.Find("[data-testid=remote-template-model-dialog]").TextContent, StringComparison.Ordinal);
            Assert.Empty(cut.FindAll("[data-testid=remote-template-accepted]"));
            if (secondNeedsTools) {
                Assert.Empty(cut.FindAll($"[data-testid=remote-template-provider] option[value='{provider:D}']"));
            }
            cut.Find("[data-testid=remote-template-provider]").Change(secondProvider.ToString("D"));
            cut.Find("[data-testid=remote-template-model]").Change(primary);
            await cut.Find("[data-testid=remote-template-model-confirm]").ClickAsync();
        }
        if (retrySecond) {
            cut.WaitForElement($"[data-template-id='{second.Id:D}'] [data-testid=remote-template-item-error]");
            Assert.True(cut.Find(Checkbox(first)).HasAttribute("disabled"));
            Assert.False(cut.Find(Checkbox(second)).HasAttribute("disabled"));
            Assert.Single(cut.FindAll("[data-testid=remote-template-accepted]"));
            retrySecond = false;
            await cut.Find("[data-testid=remote-template-import]").ClickAsync();
        }
        cut.WaitForAssertion(() => {
            Assert.Empty(cut.FindAll("[data-testid=remote-template-item-error]").Select(message => message.TextContent));
            Assert.Equal(2, cut.FindAll("[data-testid=remote-template-accepted]").Count);
        });
        var agents = await workspace.ListAgentsAsync();
        foreach (var row in published) {
            var local = Assert.Single(agents, agent => agent.Name == row.Name);
            Assert.NotEqual(row.Id, local.Id);
            Assert.Equal(avatar, local.AvatarImageUrl);
            Assert.Equal(row.Id == first.Id ? provider : secondProvider, local.ProviderProfileId);
            Assert.Equal(row.Id == first.Id || applyToAll && !secondNeedsTools ? alternate : primary, local.Model);
        }
    }

    private static string Checkbox(RemoteAgentTemplateSummary row) =>
        $"[data-template-id='{row.Id:D}'] [data-testid=remote-template-select]";

    [Fact]
    public async Task Retrying_a_lost_confirmation_returns_the_saved_local_identity() {
        var entry = Summary("A saved agent with a lost confirmation");
        var reader = new TemplateReader((_, path) => Task.FromResult<object>(path == RemoteAgentTemplateRoutes.Catalog
            ? new[] { entry } : Document(entry)));
        await using var harness = await CreateAsync(reader);
        var source = await CreateSourceAsync(harness, "retry");
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var provider = await workspace.SaveProviderAsync(new() {
            Name = "Recovery model", Kind = ProviderKind.Ollama, Transport = ProviderTransportKind.ChatCompletions,
            BaseUrl = "http://127.0.0.1:11434", DefaultModel = "recovery"
        });
        await using var opening = new AgentConfigurationOpening(harness.Context.Services.GetRequiredService<AgentConfigurationChanges>(),
            EditingOwner.AgentDefinition, null, Guid.NewGuid(), () => true);
        await opening.StartAsync(_ => Task.CompletedTask, () => Task.CompletedTask, catalog: true);
        var attempts = new List<Guid>();
        async Task<Guid> WriteAsync(Func<CancellationToken, Task<Guid>> write, CancellationToken token) {
            var id = await opening.WriteAsync(write, token);
            attempts.Add(id);
            if (attempts.Count == 1) {
                throw new IOException("The response was lost after the write.");
            }
            return id;
        }
        var cut = harness.Context.Render<RemoteTemplateImportDialog>(parameters => parameters
            .Add(dialog => dialog.Kind, RemoteTemplateKind.Agent)
            .Add(dialog => dialog.Authorize, opening.AuthorizeAsync)
            .Add(dialog => dialog.Write, WriteAsync));
        cut.WaitForElement($"[data-testid=remote-instance-select] option[value='{source.Id:D}']");
        cut.Find("[data-testid=remote-instance-select]").Change(source.Id.ToString("D"));
        cut.WaitForElement(Checkbox(entry)).Change(true);
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid=remote-template-import]").HasAttribute("disabled")));
        await cut.Find("[data-testid=remote-template-import]").ClickAsync();
        cut.WaitForElement("[data-testid=remote-template-provider]").Change(provider.ToString("D"));
        cut.Find("[data-testid=remote-template-model]").Change("recovery");
        await cut.Find("[data-testid=remote-template-model-confirm]").ClickAsync();
        Assert.Contains("could not be confirmed", cut.WaitForElement("[data-testid=remote-template-item-error]").TextContent, StringComparison.Ordinal);
        var saved = Assert.Single(await workspace.ListAgentsAsync(), agent => agent.Name == entry.Name);
        Assert.Equal(saved.Id, Assert.Single(attempts));
        await cut.Find("[data-testid=remote-template-import]").ClickAsync();
        cut.WaitForElement("[data-testid=remote-template-accepted]");
        Assert.Equal(new[] { saved.Id, saved.Id }, attempts);
        Assert.Empty(cut.FindAll("[data-testid=remote-template-item-error]"));
        Assert.Equal(saved.Id, Assert.Single(await workspace.ListAgentsAsync(), agent => agent.Name == entry.Name).Id);
    }

    private static RemoteAgentTemplate Document(RemoteAgentTemplateSummary row) => new(row.Id, row.Name, "Reviewer", row.Summary,
        "Review reports.", AgentWorkloadKind.General, [], [], row.UpdatedAtUtc) {
        PublisherId = row.PublisherId, CatalogRevision = row.CatalogRevision, AvatarImageUrl = row.AvatarImageUrl
    };

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
