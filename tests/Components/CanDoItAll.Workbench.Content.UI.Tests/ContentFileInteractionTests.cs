using System.Text;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.Workbench.Content.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.WorkbenchContent;

public sealed class ContentFileInteractionTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retired_notes_result_or_ordinary_error_cannot_publish_into_successor(bool fail) {
        using var context = Context();
        var finish = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Opening("First") with { ReadNotes = _ => finish.Task };
        var cut = context.Render<ContentFileInteractionDialog>(parameters => parameters.Add(component => component.Opening, first));
        var second = Opening("Second");
        await cut.InvokeAsync(() => cut.Render(parameters => parameters.Add(component => component.Opening, second)));
        if (fail) {
            finish.SetException(new IOException("First ordinary failure"));
        } else {
            finish.SetResult("First delayed notes");
        }
        cut.WaitForAssertion(() => {
            Assert.Contains("Second notes", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("First delayed notes", cut.Markup, StringComparison.Ordinal);
            Assert.Empty(cut.FindAll("[data-testid='content-file-notes-error']"));
        });
    }

    [Fact]
    public void Notes_authority_failure_is_explicit_and_never_becomes_file_content() {
        using var context = Context();
        var opening = Opening("Governed") with { ReadNotes = _ => Task.FromException<string?>(new UnauthorizedAccessException("Authority retired")) };
        var cut = context.Render<ContentFileInteractionDialog>(parameters => parameters.Add(component => component.Opening, opening));
        cut.WaitForAssertion(() => {
            Assert.NotEmpty(cut.Find("[data-testid='content-file-notes-error']").TextContent);
            Assert.Empty(cut.FindAll("[data-testid='project-structure-file-interaction-notes']"));
            Assert.Single(cut.FindComponents<FileInteraction>());
        });
    }

    [Fact]
    public async Task Pending_save_cannot_be_discarded_and_acknowledges_exact_revision() {
        using var context = Context();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closes = 0;
        var calls = 0;
        var opening = Opening("Editable") with {
            Close = EventCallback.Factory.Create(this, () => closes++),
            Save = async args => {
                calls++;
                entered.SetResult();
                await finish.Task;
                args.SetPersistedRevision(new("saved-r2"));
            }
        };
        var cut = context.Render<ContentFileInteractionDialog>(parameters => parameters.Add(component => component.Opening, opening));
        var file = cut.FindComponent<FileInteraction>();
        var args = SaveArgs(opening);
        var pending = cut.InvokeAsync(() => file.Instance.SaveRequested.InvokeAsync(args));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => cut.FindAll("button").Single(button => button.TextContent.Trim() == "Close").ClickAsync(new MouseEventArgs()));
        Assert.True(cut.Find("[data-testid='project-structure-interaction-close-confirm']").HasAttribute("disabled"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-interaction-close-confirm']").ClickAsync(new MouseEventArgs()));
        Assert.Equal(0, closes);
        finish.SetResult();
        await pending;
        Assert.Equal(new FileContentRevision("saved-r2"), args.PersistedRevision);
        Assert.Equal(1, calls);
        await cut.InvokeAsync(() => cut.FindAll("button").Single(button => button.TextContent.Trim() == "Close").ClickAsync(new MouseEventArgs()));
        Assert.Equal(1, closes);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Dirty_and_conflicted_sessions_require_explicit_discard(bool dirty, bool conflict) {
        using var context = Context();
        var closes = 0;
        var opening = Opening("Guard") with { Close = EventCallback.Factory.Create(this, () => closes++) };
        var cut = context.Render<ContentFileInteractionDialog>(parameters => parameters.Add(component => component.Opening, opening));
        var child = cut.FindComponent<FileInteraction>();
        await cut.InvokeAsync(() => child.Instance.StateChanged.InvokeAsync(State(opening, dirty, conflict)));
        await cut.InvokeAsync(() => cut.FindAll("button").Single(button => button.TextContent.Trim() == "Close").ClickAsync(new MouseEventArgs()));
        Assert.Equal(0, closes);
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-interaction-close-confirm']").ClickAsync(new MouseEventArgs()));
        Assert.Equal(1, closes);
    }

    [Fact]
    public async Task Stale_save_mode_state_and_close_callbacks_cannot_reach_successor() {
        using var context = Context();
        var firstCalls = 0;
        var secondCalls = 0;
        var first = Opening("First") with { Save = _ => { firstCalls++; return Task.CompletedTask; } };
        var cut = context.Render<ContentFileInteractionDialog>(parameters => parameters.Add(component => component.Opening, first));
        var oldChild = cut.FindComponent<FileInteraction>().Instance;
        var oldSave = oldChild.SaveRequested;
        var oldMode = oldChild.ModeChanged;
        var oldState = oldChild.StateChanged;
        var oldClose = cut.FindComponent<CanvasOverlayDialog>().Instance.Close;
        var second = Opening("Second") with { Close = EventCallback.Factory.Create(this, () => secondCalls++) };
        await cut.InvokeAsync(() => cut.Render(parameters => parameters.Add(component => component.Opening, second)));
        await cut.InvokeAsync(() => oldMode.InvokeAsync(FileInteractionMode.Edit));
        await cut.InvokeAsync(() => oldState.InvokeAsync(State(first, true, true)));
        await cut.InvokeAsync(() => oldClose.InvokeAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => cut.InvokeAsync(() => oldSave.InvokeAsync(SaveArgs(first))));
        Assert.Equal(0, firstCalls);
        Assert.Equal(0, secondCalls);
        Assert.Equal(second.Request, cut.FindComponent<FileInteraction>().Instance.Request);
        Assert.Empty(cut.FindAll("[data-testid='project-structure-interaction-close-guard']"));
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static ContentFileInteractionOpening Opening(string title) {
        var file = new FileReference("scenario", Guid.NewGuid().ToString("N"), "r1");
        var request = new FileInteractionRequest(file, title + ".txt", FileInteractionMode.View, "text/plain", Content.Bytes.Length, new("r1"));
        return new() {
            Title = title,
            Notice = "Governed scenario",
            Request = request,
            ContentSource = new Content(),
            Composition = new FileInteractionComponentBuilder().AddBuiltIns().Build(),
            MaximumContentBytes = 1024,
            ReadNotes = _ => Task.FromResult<string?>(title + " notes"),
            ChangeMode = mode => new(request.File, request.FileName, mode, request.MediaType, request.Size, request.ContentRevision)
        };
    }

    private static FileInteractionState State(ContentFileInteractionOpening opening, bool dirty, bool conflict)
        => new(opening.Request!.File, opening.Request.FileName, FileInteractionMode.View, FileInteractionLifecycleState.Loaded,
            1, dirty, false, conflict, false, false, false);

    private static FileInteractionSaveRequestedEventArgs SaveArgs(ContentFileInteractionOpening opening)
        => new(new(opening.Request!.File, 1, new BufferedFileSaveContent(Content.Bytes), new("r1"), "text/plain", "utf-8"));

    private sealed class Content : IFileContentSource {
        internal static readonly byte[] Bytes = Encoding.UTF8.GetBytes("Exact authorized content.");
        public ValueTask<FileContentLease> OpenReadAsync(FileContentReadRequest request, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new FileContentLease(new MemoryStream(Bytes, writable: false), "text/plain", Bytes.Length, new("r1")));
    }
}
