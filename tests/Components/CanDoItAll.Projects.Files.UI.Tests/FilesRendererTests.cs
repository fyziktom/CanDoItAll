using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileBrowser.Components;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.Projects.Files.UI;
using CanDoItAll.Projects.Files.UiSandbox;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectsFilesUi;

public sealed class FilesRendererTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Successful_empty_scenario_uses_the_real_empty_source_set_contract(bool dialog) {
        using var context = Context();
        await using var surface = new FilesScenarioSurface(new object(), FilesScenario.Empty);
        var cut = Render(context, dialog, surface.State);
        if (dialog) {
            Assert.Contains("Files · Fixture project", cut.FindComponent<Dialog>().Instance.Title);
        }
        Assert.Contains(dialog ? "No project file sources" : "No projects match the shared filters", cut.Markup);
        Assert.Null(surface.State.OpenError);
        Assert.Empty(surface.Browser.Snapshot.Sources);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Both_renderers_present_loading_error_retry_and_feedback(bool dialog) {
        using var context = Context();
        int retries = 0;
        var state = new ProjectFilesViewState { IsLoading = true };
        var cut = Render(context, dialog, state);
        Assert.Contains(dialog ? "Opening project files" : "Resolving project file sources", cut.Markup);
        state = state with { IsLoading = false, OpenError = "Known source failure", Retry = EventCallback.Factory.Create(new object(), () => retries++) };
        cut = Render(context, dialog, state);
        Assert.Contains("Known source failure", cut.Markup);
        await context.Renderer.Dispatcher.InvokeAsync(() => cut.FindAll("button").Single(button => button.TextContent == "Retry").ClickAsync(new()));
        Assert.Equal(1, retries);
        Assert.Empty(cut.FindAll(".ft-file-browser"));
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("README.md")]
    [InlineData("data.json")]
    [InlineData("diagram.mermaid")]
    [InlineData("diagram.svg")]
    [InlineData("image.png")]
    [InlineData("manual.pdf")]
    public async Task Each_real_fixture_renders_read_only_in_both_surfaces(string fileName) {
        foreach (bool dialog in new[] { false, true }) {
            using var context = Context();
            var fixture = FilesFixtureCatalog.Create().Single(item => item.Name == fileName);
            await using var content = new FilesFixtureContent(fixture);
            var cut = Render(context, dialog, new() { Preview = new(content.Request, content, FilesFixtureComposition.Create(), default) });
            cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<FileInteraction>()));
            var interaction = cut.FindComponent<FileInteraction>().Instance;
            Assert.False(interaction.AllowModeSwitch);
            Assert.Equal(4 * 1024 * 1024, interaction.MaximumContentBytes);
            Assert.NotNull(interaction.Composition);
            if (fileName is "README.md" or "diagram.mermaid") {
                string viewer = fileName == "README.md" ? "interaction-markdown-view" : "files-fixture-mermaid";
                cut.WaitForAssertion(() => Assert.Single(cut.FindAll($"[data-testid='{viewer}']")));
            }
            Assert.True(cut.Find("[data-testid='interaction-mode-edit']").HasAttribute("disabled"));
            Assert.DoesNotContain("No compatible", cut.Markup);
            Assert.Contains(fileName, cut.Markup);
            await context.DisposeRenderedComponentsAsync();
            await using var lease = await content.OpenReadAsync(new(content.File));
            using var bytes = new MemoryStream();
            await lease.Stream.CopyToAsync(bytes);
            Assert.Equal(fixture.Bytes, bytes.ToArray());
            Assert.Equal(0, content.Releases);
        }
    }

    [Fact]
    public async Task Browser_component_removal_preserves_session_and_independent_preview_until_owner_retirement() {
        using var context = Context();
        await using var surface = new FilesScenarioSurface(new object(), FilesScenario.Representative);
        var cut = context.Render<ProjectFilesPortfolioPaneView>(p => p.Add(x => x.State, surface.State));
        cut.WaitForAssertion(() => Assert.Equal(7, surface.Browser.Snapshot.Items.Count));
        var item = surface.Browser.Snapshot.Items.First();
        await surface.State.Browse!.ItemInvoked.InvokeAsync(new(item, FileBrowserInvocationKind.Keyboard));
        cut.Render(p => p.Add(x => x.State, surface.State));
        Assert.Empty(cut.FindAll(".ft-file-browser"));
        var preview = surface.State.Preview!;
        await surface.Browser.RefreshAsync();
        Assert.Equal(7, surface.Browser.Snapshot.Items.Count);
        await context.DisposeRenderedComponentsAsync();
        await using (var lease = await preview.ContentSource.OpenReadAsync(new(preview.Request.File))) {
            Assert.True(lease.Length > 0);
        }
        await surface.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => preview.ContentSource.OpenReadAsync(new(preview.Request.File)).AsTask());
    }

    [Fact]
    public async Task Real_scenario_browser_pages_and_progressive_search_stay_bounded() {
        await using var surface = new FilesScenarioSurface(new object(), FilesScenario.Large);
        await surface.Browser.InitializeAsync();
        Assert.Equal(50, surface.Browser.Snapshot.Items.Count);
        Assert.True(surface.Browser.Snapshot.HasMore);
        await surface.Browser.LoadMoreAsync();
        Assert.Equal(100, surface.Browser.Snapshot.Items.Count);
        await surface.Browser.SearchAsync("notes-219", FileBrowserSearchScope.Progressive);
        Assert.Equal("notes-219.txt", Assert.Single(surface.Browser.Snapshot.Items).Name);
        Assert.Equal(220, surface.Browser.Snapshot.Search!.ScannedItems);
        Assert.True(surface.Browser.Snapshot.Search.RetainedBytes <= 2L * 1024 * 1024);
    }

    [Fact]
    public async Task Removed_source_does_not_revive_after_a_held_real_browser_completion() {
        await using var surface = new FilesScenarioSurface(new object(), FilesScenario.Representative);
        await surface.Browser.InitializeAsync();
        var oldSource = surface.Browser.Snapshot.CurrentSource!.Id;
        surface.HoldNextRead();
        var pending = surface.Browser.RefreshAsync().AsTask();
        var replacing = surface.RemoveSourceAsync();
        surface.ReleaseReads();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        await replacing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(surface.Browser.Snapshot.Sources, source => source.Id == oldSource);
        Assert.NotEqual(oldSource, surface.Browser.Snapshot.CurrentSource!.Id);
        Assert.All(surface.Browser.Snapshot.Items, item => Assert.Equal(surface.Browser.Snapshot.CurrentSource.Id, item.Key.SourceId));
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton(FilesFixtureComposition.Create());
        return context;
    }

    private static IRenderedComponent<ComponentBase> Render(BunitContext context, bool dialog, ProjectFilesViewState state)
        => dialog
            ? context.Render<ProjectFilesDialogView>(p => p.Add(x => x.IsOpen, true).Add(x => x.ProjectName, "Fixture project").Add(x => x.State, state))
            : context.Render<ProjectFilesPortfolioPaneView>(p => p.Add(x => x.State, state));
}
