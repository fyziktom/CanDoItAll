using AngleSharp.Html.Dom;
using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;
using ProviderConnectorKeys = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ProviderProfilesSeamTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Core_read_failure_hides_form_and_retry_keeps_the_selected_target(bool catalogFails) {
        var reads = new Reads();
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<IProviderProfilesReads>(reads));
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-name-input']");
        if (catalogFails) {
            reads.Catalog = _ => throw new InvalidOperationException("Catalog offline");
            await cut.Find("[data-testid='providers-refresh']").ClickAsync();
        } else {
            reads.Editor = (_, _) => throw new InvalidOperationException("Editor offline");
            await Node(cut, "Second").ClickAsync();
        }
        cut.WaitForElement("[data-testid='providers-load-failed']");
        Assert.Equal(catalogFails ? "First" : "Second", cut.Find("h2.cda-title-xl").TextContent);
        Assert.Empty(cut.FindAll("form"));
        Assert.Empty(cut.FindAll("[data-testid='providers-save']"));
        reads.Catalog = _ => Task.FromResult(reads.Snapshot);
        Guid? retriedId = null;
        reads.Editor = (id, _) => {
            retriedId = id;
            return Task.FromResult(reads.Draft(id));
        };
        await cut.Find("[data-testid='providers-load-retry']").ClickAsync();
        cut.WaitForElement("[data-testid='providers-name-input']");
        Assert.Equal(catalogFails ? reads.First.Id : reads.Second.Id, retriedId);
        Assert.Empty(cut.FindAll("[data-testid='providers-load-failed']"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task New_or_another_selection_wins_over_a_pending_editor_read(bool fails, bool newDraft) {
        var reads = new Reads();
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<IProviderProfilesReads>(reads));
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-name-input']");
        var pending = new TaskCompletionSource<ProviderProfileEditorModel>();
        CancellationToken token = default;
        reads.Editor = (_, owner) => {
            token = owner;
            return pending.Task;
        };
        var old = Node(cut, "Second").ClickAsync();
        cut.WaitForElement("[data-testid='providers-editor-loading']");
        Assert.Equal("Second", cut.Find("h2.cda-title-xl").TextContent);
        reads.Editor = (id, _) => Task.FromResult(reads.Draft(id));
        if (newDraft) {
            await cut.Find("[data-testid='providers-new']").ClickAsync();
        } else {
            await Node(cut, "First").ClickAsync();
        }
        cut.WaitForElement("[data-testid='providers-name-input']").Change("Keep my latest draft");
        await cut.FindAll("button[role='tab']").Single(button => button.TextContent.Contains("Runtime", StringComparison.Ordinal)).ClickAsync();
        cut.Find("[data-testid='providers-suggested-models']").Change("Unsubmitted model text");
        Assert.Equal("Unsubmitted model text", cut.Find("[data-testid='providers-suggested-models']").GetAttribute("value"));
        if (newDraft) {
            await cut.Find("[data-testid='providers-refresh']").ClickAsync();
            Assert.Equal("Unsubmitted model text", cut.Find("[data-testid='providers-suggested-models']").GetAttribute("value"));
        }
        await cut.InvokeAsync(() => {
            if (fails) {
                pending.SetException(new InvalidOperationException("Stale failure"));
            } else {
                pending.SetResult(reads.Draft(reads.Second.Id));
            }
        });
        await old;
        Assert.True(token.IsCancellationRequested);
        Assert.Equal("Unsubmitted model text", cut.Find("[data-testid='providers-suggested-models']").GetAttribute("value"));
        var model = Assert.IsType<ProviderProfileEditorModel>(cut.FindComponent<ProviderProfileEditorForm>().Instance.Context.Model);
        Assert.Equal(newDraft ? null : reads.First.Id, model.Id);
        Assert.Equal("Keep my latest draft", model.Name);
        Assert.Empty(harness.Context.Services.GetRequiredService<NotificationService>().Messages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposal_cancels_inflight_read_without_late_notifications(bool catalogRead) {
        var reads = new Reads();
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<IProviderProfilesReads>(reads));
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-name-input']");
        var catalog = new TaskCompletionSource<ProviderProfilesCatalog>();
        var editor = new TaskCompletionSource<ProviderProfileEditorModel>();
        CancellationToken token = default;
        reads.Catalog = owner => {
            token = owner;
            return catalog.Task;
        };
        reads.Editor = (_, owner) => {
            token = owner;
            return editor.Task;
        };
        var loading = catalogRead ? cut.Find("[data-testid='providers-refresh']").ClickAsync() : Node(cut, "Second").ClickAsync();
        cut.WaitForElement("[data-testid='providers-editor-loading']");
        await cut.InvokeAsync(cut.Instance.Dispose);
        Assert.True(token.IsCancellationRequested);
        await cut.InvokeAsync(() => {
            if (catalogRead) {
                catalog.SetException(new InvalidOperationException("Disposed catalog"));
            } else {
                editor.SetException(new InvalidOperationException("Disposed editor"));
            }
        });
        await loading;
        Assert.Empty(harness.Context.Services.GetRequiredService<NotificationService>().Messages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Secret_partial_failure_retains_saved_option_and_source_managed_actions(bool sourceManaged) {
        var reads = new Reads();
        reads.Catalog = _ => Task.FromResult(new ProviderProfilesCatalog(
            [reads.First with { ConnectorPluginKey = sourceManaged ? ProviderConnectorKeys.SharedImport : string.Empty }],
            new([], "Secret metadata offline")));
        reads.Editor = (id, _) => Task.FromResult(new ProviderProfileEditorModel { Id = id, Name = "Saved", ApiKeyEnvironmentVariable = "secret:retained" });
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<IProviderProfilesReads>(reads));
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-secret-warning']");
        var input = (IHtmlSelectElement)cut.Find("[data-testid='providers-api-key-input']");
        Assert.Equal("secret:retained", input.Value);
        Assert.Contains(input.Options, option => option.Value == "secret:retained");
        Assert.Equal(!sourceManaged, cut.FindComponent<ProviderProfileEditorForm>().Instance.CanManage);
        Assert.Equal(sourceManaged, cut.Find("fieldset").HasAttribute("disabled"));
        Assert.Equal(sourceManaged ? 0 : 1, cut.FindAll("[data-testid='providers-save']").Count);
    }

    [Theory]
    [InlineData("Connection")]
    [InlineData("Prices")]
    [InlineData("Runtime")]
    [InlineData("Thinking")]
    [InlineData("Sharing")]
    [InlineData("History")]
    public async Task Typed_section_order_renders_the_expected_section_and_preserves_context(string label) {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-name-input']");
        var context = cut.FindComponent<ProviderProfileEditorForm>().Instance.Context;
        cut.Find("[data-testid='providers-name-input']").Change("Unsaved across sections");
        Assert.Equal(new[] { "Connection", "Prices", "Runtime", "Thinking", "Sharing", "History" },
            cut.FindAll("button[role='tab']").Select(button => button.QuerySelector(".cad-tabs__tab-text")!.TextContent.Trim()));
        await cut.InvokeAsync(() => cut.FindAll("button[role='tab']").Single(button => button.QuerySelector(".cad-tabs__tab-text")!.TextContent.Trim() == label).Click());
        cut.WaitForAssertion(() => Assert.Equal("true", cut.FindAll("button[role='tab']").Single(button => button.QuerySelector(".cad-tabs__tab-text")!.TextContent.Trim() == label).GetAttribute("aria-selected")));
        await cut.InvokeAsync(() => cut.FindAll("button[role='tab']").Single(button => button.QuerySelector(".cad-tabs__tab-text")!.TextContent.Trim() == "Connection").Click());
        Assert.Same(context, cut.FindComponent<ProviderProfileEditorForm>().Instance.Context);
        Assert.Equal("Unsaved across sections", ((ProviderProfileEditorModel)context.Model).Name);
    }

    private static AngleSharp.Dom.IElement Node(IRenderedComponent<AgentProviderProfilesPanel> cut, string name)
        => cut.FindAll("[data-testid='providers-tree-provider']").Single(node => node.TextContent.Contains(name, StringComparison.Ordinal));

    private sealed class Reads : IProviderProfilesReads {
        public ProviderProfile First { get; } = Provider("First");
        public ProviderProfile Second { get; } = Provider("Second");
        public ProviderProfilesCatalog Snapshot => new([First, Second], new([]));
        public Func<CancellationToken, Task<ProviderProfilesCatalog>>? Catalog { get; set; }
        public Func<Guid, CancellationToken, Task<ProviderProfileEditorModel>>? Editor { get; set; }
        public ProviderProfileEditorModel Draft(Guid id) => new() { Id = id, Name = id == First.Id ? "First" : "Second" };
        public Task<ProviderProfilesCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default)
            => Catalog?.Invoke(cancellationToken) ?? Task.FromResult(Snapshot);
        public Task<ProviderProfileEditorModel> LoadEditorAsync(Guid providerId, CancellationToken cancellationToken = default)
            => Editor?.Invoke(providerId, cancellationToken) ?? Task.FromResult(Draft(providerId));
        private static ProviderProfile Provider(string name) => new(Guid.NewGuid(), name, ProviderKind.OpenAi,
            string.Empty, string.Empty, "model", ProviderTransportKind.Responses, true, true, true, false, true,
            "{}", string.Empty, string.Empty, null, ["model"]);
    }
}
