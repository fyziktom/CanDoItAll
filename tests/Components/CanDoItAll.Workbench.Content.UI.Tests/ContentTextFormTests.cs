using System.Text;
using AngleSharp.Html.Dom;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Workbench.Content.UI;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.WorkbenchContent;

public sealed class ContentTextFormTests {
    [Theory]
    [InlineData(ContentTextKind.Text)]
    [InlineData(ContentTextKind.Json)]
    [InlineData(ContentTextKind.Markdown)]
    [InlineData(ContentTextKind.Mermaid)]
    [InlineData(ContentTextKind.Log)]
    public async Task Every_text_kind_submits_frozen_draft_and_gates_duplicate_events(ContentTextKind kind) {
        using var context = Context();
        var entered = new TaskCompletionSource<ContentTextSubmission>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource<ContentTextOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var cut = Render(context, kind, async (submission, _) => {
            calls++;
            entered.TrySetResult(submission);
            return await finish.Task;
        });
        await cut.InvokeAsync(() => {
            cut.Find("[data-testid='project-structure-text-asset-title']").Input("  Exact title  ");
            cut.Find("[data-testid='project-structure-text-asset-subtitle']").Input("  Exact context  ");
            cut.Find("[data-testid='project-structure-text-asset-notes']").Input("  Descriptive notes  ");
            cut.Find("[data-testid='project-structure-text-asset-content']").Input("  Raw content\nnext line  ");
        });
        var pending = Submit(cut);
        var accepted = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-text-asset-notes']").Input("Later attempted edit"));
        await Submit(cut);
        Assert.Equal(1, calls);
        Assert.Equal(("Exact title", "Exact context", "Descriptive notes", "  Raw content\nnext line  "),
            (accepted.Title, accepted.Subtitle, accepted.Notes, accepted.Content));
        finish.SetResult(new(ContentTextOutcomeKind.Unconfirmed, "Observe original write"));
        await pending;
        await Submit(cut);
        Assert.Equal(1, calls);
        Assert.Contains("Observe original write", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("  Raw content\nnext line  ", ((IHtmlTextAreaElement)cut.Find("[data-testid='project-structure-text-asset-content']")).Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delayed_outcome_or_error_cannot_publish_into_replacement_opening(bool fail) {
        using var context = Context();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource<ContentTextOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completions = 0;
        var cut = Render(context, ContentTextKind.Text, async (_, _) => {
            entered.SetResult();
            return await finish.Task;
        }, _ => completions++);
        var pending = Submit(cut);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => cut.Render(parameters => parameters.Add(component => component.OpeningId, Guid.NewGuid())
            .Add(component => component.InitialDraft, new("Successor", "New context", "New notes"))));
        if (fail) {
            finish.SetException(new IOException("Retired ordinary error"));
        } else {
            finish.SetResult(new(ContentTextOutcomeKind.Committed, "Original accepted", new("original-node", Guid.NewGuid(), "original.txt")));
        }
        await pending;
        Assert.Equal(0, completions);
        Assert.Equal("Successor", ((IHtmlInputElement)cut.Find("[data-testid='project-structure-text-asset-title']")).Value);
        Assert.DoesNotContain("Original accepted", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid='project-structure-text-asset-validation-error']"));
        Assert.False(cut.Find("[data-testid='project-structure-text-asset-submit']").HasAttribute("disabled"));
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(17, 17)]
    [InlineData(8, 4)]
    [InlineData(4, 8)]
    public async Task Actual_upload_stream_is_bounded_and_must_match_declared_length(long declared, int actual) {
        using var context = Context();
        var calls = 0;
        var cut = Render(context, ContentTextKind.Text, (_, _) => {
            calls++;
            return Task.FromResult(new ContentTextOutcome(ContentTextOutcomeKind.Prepared, "Prepared"));
        }, maximum: 16);
        await Upload(cut, new FixtureFile(new byte[actual], declared));
        await Submit(cut);
        Assert.Equal(0, calls);
        Assert.NotEmpty(cut.Find("[data-testid='project-structure-text-asset-validation-error']").TextContent);
    }

    [Fact]
    public async Task Real_upload_event_preserves_exact_bytes_and_file_descriptor() {
        using var context = Context();
        ContentTextSubmission? captured = null;
        var cut = Render(context, ContentTextKind.Json, (submission, _) => {
            captured = submission;
            return Task.FromResult(new ContentTextOutcome(ContentTextOutcomeKind.Prepared, "Prepared"));
        });
        var bytes = new byte[] { 0xef, 0xbb, 0xbf }.Concat(Encoding.UTF8.GetBytes("{\"value\":\"\u03c0\"}")).ToArray();
        await Upload(cut, new FixtureFile(bytes, bytes.Length));
        await Submit(cut);
        Assert.NotNull(captured);
        Assert.Equal(ContentTextSource.UploadExisting, captured.Source);
        Assert.Equal("fixture.txt", captured.Upload!.FileName);
        Assert.Equal("application/octet-stream", captured.Upload.ContentType);
        Assert.Equal(bytes, captured.Upload.Bytes.ToArray());
    }

    [Fact]
    public async Task Cancelling_while_reading_never_dispatches_or_reports_persistence() {
        using var context = Context();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var cancelled = 0;
        var cut = Render(context, ContentTextKind.Text, (_, _) => {
            calls++;
            return Task.FromResult(new ContentTextOutcome(ContentTextOutcomeKind.Prepared, "Prepared"));
        });
        await cut.InvokeAsync(() => cut.Render(parameters => parameters.Add(component => component.Cancelled, () => cancelled++)));
        await Upload(cut, new FixtureFile([1, 2], 2, entered, release));
        var pending = Submit(cut);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-text-asset-cancel']").ClickAsync(new MouseEventArgs()));
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        release.TrySetResult();
        Assert.Equal(1, cancelled);
        Assert.Equal(0, calls);
        Assert.Empty(cut.FindAll("[data-testid='content-text-outcome']"));
    }

    [Theory]
    [InlineData(ContentTextOutcomeKind.Prepared)]
    [InlineData(ContentTextOutcomeKind.Committed)]
    [InlineData(ContentTextOutcomeKind.PartialCommit)]
    public async Task Accepted_outcome_retains_exact_receipt_and_phase(ContentTextOutcomeKind kind) {
        using var context = Context();
        var receipt = kind == ContentTextOutcomeKind.Prepared ? null : new ContentAssetReceipt("native-node", Guid.NewGuid(), "native.txt");
        var expected = new ContentTextOutcome(kind, "Exact phase", receipt);
        ContentTextOutcome? completed = null;
        var cut = Render(context, ContentTextKind.Text, (_, _) => Task.FromResult(expected), value => completed = value);
        await Submit(cut);
        Assert.Same(expected, completed);
        if (receipt is not null) {
            Assert.Contains(receipt.NodeId, cut.Markup, StringComparison.Ordinal);
            Assert.Contains(receipt.RecordId!.Value.ToString("D"), cut.Markup, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Cancellation_after_dispatch_requires_observation_before_another_create() {
        using var context = Context();
        using var lifetime = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var cut = context.Render<ContentTextForm>(parameters => parameters
            .Add(component => component.OpeningId, Guid.NewGuid())
            .Add(component => component.Definition, new(ContentTextKind.Text, "Text", "Title", "Title", "Context", "Context", "Notes", "Notes",
                "file.txt", "Content", ".txt", "Choose file", "Create", 1024))
            .Add(component => component.InitialDraft, new("Interrupted", string.Empty, string.Empty))
            .Add(component => component.CancellationToken, lifetime.Token)
            .Add(component => component.Submit, async (_, cancellationToken) => {
                calls++;
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new(ContentTextOutcomeKind.Committed, "Unreachable");
            }));
        var pending = Submit(cut);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await lifetime.CancelAsync();
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        await Submit(cut);
        Assert.Equal(1, calls);
        Assert.Contains("Observe the original target", cut.Find("[data-testid='content-text-outcome']").TextContent, StringComparison.Ordinal);
        Assert.True(cut.Find("[data-testid='project-structure-text-asset-submit']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Rejected_submission_keeps_draft_and_allows_an_explicit_corrected_submission() {
        using var context = Context();
        var submissions = new List<ContentTextSubmission>();
        var cut = Render(context, ContentTextKind.Json, (submission, _) => {
            submissions.Add(submission);
            return Task.FromResult(new ContentTextOutcome(submissions.Count == 1 ? ContentTextOutcomeKind.Rejected : ContentTextOutcomeKind.Prepared,
                submissions.Count == 1 ? "Native JSON validation refused input" : "Prepared"));
        });
        await Submit(cut);
        Assert.Contains("Native JSON validation refused input", cut.Markup, StringComparison.Ordinal);
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-text-asset-content']").Input("{\"corrected\":true}"));
        await Submit(cut);
        Assert.Equal(2, submissions.Count);
        Assert.NotEqual(submissions[0].SubmissionId, submissions[1].SubmissionId);
        Assert.Equal("{\"corrected\":true}", submissions[1].Content);
    }

    [Fact]
    public void Leaf_does_not_reference_native_module_or_infrastructure() {
        var references = typeof(ContentTextForm).Assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
        Assert.DoesNotContain(references, name => name!.StartsWith("CanDoItAll.Modules", StringComparison.Ordinal) ||
            name.Contains("Infrastructure", StringComparison.Ordinal) || name.Contains("EntityFramework", StringComparison.Ordinal) || name.Contains("Processes", StringComparison.Ordinal));
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static IRenderedComponent<ContentTextForm> Render(BunitContext context, ContentTextKind kind,
        Func<ContentTextSubmission, CancellationToken, Task<ContentTextOutcome>> submit, Action<ContentTextOutcome>? completed = null, int maximum = 1024)
        => context.Render<ContentTextForm>(parameters => parameters.Add(component => component.OpeningId, Guid.NewGuid())
            .Add(component => component.Definition, new(kind, kind.ToString(), "Title", "Title", "Context", "Context", "Notes", "Notes",
                "fixture.txt", "Raw content", ".txt", "Choose existing text", "Create", maximum))
            .Add(component => component.InitialDraft, new("Initial title", "Initial context", "Initial notes"))
            .Add(component => component.Submit, submit).Add(component => component.Completed, completed ?? (_ => { })));

    private static Task Submit(IRenderedComponent<ContentTextForm> cut)
        => cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-text-asset-submit']").ClickAsync(new MouseEventArgs()));

    private static async Task Upload(IRenderedComponent<ContentTextForm> cut, IBrowserFile file) {
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-text-asset-source-upload']").ClickAsync(new MouseEventArgs()));
        await cut.InvokeAsync(() => cut.FindComponent<InputFile>().Instance.OnChange.InvokeAsync(new InputFileChangeEventArgs([file])));
    }

    private sealed class FixtureFile(byte[] bytes, long declaredSize, TaskCompletionSource? entered = null, TaskCompletionSource? release = null) : IBrowserFile {
        public string Name => "fixture.txt";
        public string ContentType => "application/octet-stream";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => declaredSize;
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
            => new FixtureStream(bytes, entered, release);
    }

    private sealed class FixtureStream(byte[] bytes, TaskCompletionSource? entered, TaskCompletionSource? release) : MemoryStream(bytes, writable: false) {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
            entered?.TrySetResult();
            if (release is not null) {
                await release.Task.WaitAsync(cancellationToken);
            }
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }
}
