using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using Microsoft.Extensions.Logging.Abstractions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureNodeEditingTests {
    [Fact]
    public async Task Apply_and_Undo_change_only_the_local_draft_and_keep_the_accepted_current_native_baseline() {
        await using var fixture = new Fixture();
        await fixture.Session.OpenAsync();
        await fixture.CompareAsync();
        Assert.Equal("peer first\nmiddle\nlocal last", fixture.Session.Review!.Notes.Result);
        CanvasWorkbenchComposerApplyRequest? applied = null;

        Assert.True(await fixture.Session.ApplyAsync(request => {
            applied = request;
            return Task.FromResult(new CanvasWorkbenchComposerApplyResult(CanvasWorkbenchComposerApplyStatus.Applied, 3));
        }));

        Assert.Equal(fixture.Current, fixture.Session.Baseline);
        Assert.Equal(2, applied!.ExpectedRevision);
        Assert.Equal("peer first\nmiddle\nlocal last", applied.EditRequest!.Notes);
        Assert.True(applied.EditRequest.HasExplicitTitle);
        Assert.Equal(fixture.Current.Title, applied.EditRequest.Title);
        Assert.True(fixture.Session.CanUndo);
        await fixture.Session.UndoAsync(request => {
            Assert.Equal(3, request.ExpectedRevision);
            Assert.Equal(fixture.Draft.EditRequest, request.EditRequest);
            return Task.FromResult(new CanvasWorkbenchComposerApplyResult(CanvasWorkbenchComposerApplyStatus.Applied, 4));
        });
        Assert.Equal(fixture.Current, fixture.Session.Baseline);
        Assert.False(fixture.Session.CanUndo);
        Assert.Equal(3, fixture.Reads);
    }

    [Fact]
    public async Task A_third_writer_blocks_Apply_and_retains_custom_comparison_input() {
        await using var fixture = new Fixture();
        await fixture.Session.OpenAsync();
        await fixture.CompareAsync();
        var review = fixture.Session.Review!;
        review.Title.SetResult("My retained result");
        review.RecordInput();
        fixture.Current = fixture.Current with { Notes = "Third writer" };

        var applied = await fixture.Session.ApplyAsync(_ => throw new InvalidOperationException("Must not mutate controls."));

        Assert.False(applied);
        Assert.Same(review, fixture.Session.Review);
        Assert.Equal("My retained result", review.Title.Result);
        Assert.Equal(fixture.Original, fixture.Session.Baseline);
        Assert.Contains("changed again", fixture.Session.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Unfinished_input_and_unknown_submission_never_read_or_rearm_an_editor(bool pending, bool canApply) {
        await using var fixture = new Fixture();
        await fixture.Session.OpenAsync();

        await fixture.Session.CompareAsync(fixture.Draft with { HasPendingInput = pending, CanApply = canApply }, fixture.ReadDraftAsync);

        Assert.Equal(0, fixture.Reads);
        Assert.Null(fixture.Session.Review);
    }

    [Fact]
    public async Task Input_changed_during_a_current_read_does_not_publish_an_obsolete_comparison() {
        await using var fixture = new Fixture();
        await fixture.Session.OpenAsync();
        var draft = fixture.Draft;
        fixture.AfterRead = () => fixture.Draft = draft with { Revision = draft.Revision + 1 };

        await fixture.Session.CompareAsync(draft, fixture.ReadDraftAsync);

        Assert.Null(fixture.Session.Review);
        Assert.Null(fixture.Session.Current);
        Assert.Contains("changed during", fixture.Session.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Access_or_opening_retirement_during_a_held_read_never_exposes_current_values(bool retire) {
        await using var fixture = new Fixture();
        await fixture.Session.OpenAsync();
        fixture.AfterRead = () => {
            if (retire) {
                fixture.Active = false;
            } else {
                fixture.Access.Allowed = false;
            }
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(fixture.CompareAsync);

        Assert.Null(fixture.Session.Current);
        Assert.Null(fixture.Session.Review);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Session.AuthorizeWriteAsync(default).AsTask());
    }

    [Fact]
    public async Task A_recreated_node_with_the_same_public_key_is_not_an_ordinary_merge() {
        await using var fixture = new Fixture();
        await fixture.Session.OpenAsync();
        fixture.Current = fixture.Current with { RecordId = Guid.NewGuid() };

        await Assert.ThrowsAsync<ProjectStructureEditConflictException>(fixture.CompareAsync);

        Assert.Null(fixture.Session.Current);
        Assert.Null(fixture.Session.Review);
    }

    [Fact]
    public async Task An_unchanged_inline_body_keeps_its_real_custom_title_instead_of_an_empty_transport_title() {
        await using var fixture = new Fixture();
        fixture.Draft = fixture.Draft with { EditRequest = new(fixture.Original.Id, "", fixture.Original.Notes) };
        await fixture.Session.OpenAsync();

        await fixture.CompareAsync();

        Assert.Equal(fixture.Original.Title, fixture.Session.Review!.Title.Local);
        Assert.Equal(fixture.Current.Title, fixture.Session.Review.Title.Result);
        Assert.True(fixture.Session.Review.CanApply);
    }

    [Fact]
    public async Task Unowned_live_progress_and_layout_do_not_become_a_global_node_form_conflict() {
        await using var fixture = new Fixture();
        await fixture.Session.OpenAsync();
        await fixture.CompareAsync();
        fixture.Current = fixture.Current with { ProgressPercent = 70, Status = "Running", X = 150, Priority = 3 };

        Assert.True(await fixture.Session.ApplyAsync(_ => Task.FromResult(
            new CanvasWorkbenchComposerApplyResult(CanvasWorkbenchComposerApplyStatus.Applied, 3))));
    }

    [Fact]
    public async Task Full_node_forms_preserve_uncontrolled_metadata_and_unchanged_native_date_precision() {
        await using var fixture = new Fixture();
        var start = new DateTimeOffset(2026, 11, 2, 12, 34, 56, TimeSpan.Zero).AddTicks(1234560);
        var node = fixture.Original with { ObjectType = ProjectObjectType.Meeting, ObjectSubtype = "online",
            StartUtc = start, EndUtc = start.AddHours(1), MetadataJson = "{\"future\":{\"keep\":true}}" };
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(node.ObjectType, node.ObjectSubtype, out var definition));
        var fields = ProjectStructureCanvasCatalog.BuildComposerAction(definition).InputFields
            .Where(field => ProjectStructureNodeEditor.SupportsEditingField(field.Key)).ToArray();
        var form = new ProjectStructureNodeForm(new(fields), definition);
        var projected = form.Values(node);
        var submitted = new CanvasWorkbenchCreateActionRequest($"edit:{definition.ActionId}", node.Id, node.X, node.Y,
            node.ParentId, "Changed title", node.Subtitle, node.Notes, "edit", "dialog", node.ObjectSubtype, null, form.Schema.Write(projected));

        var update = form.Compose(node, submitted);

        Assert.Equal("Changed title", update.Title);
        Assert.Equal(start, update.StartUtc);
        Assert.Equal(start.AddHours(1), update.EndUtc);
        using var json = System.Text.Json.JsonDocument.Parse(update.MetadataJson!);
        Assert.True(json.RootElement.GetProperty("future").GetProperty("keep").GetBoolean());
        Assert.False(form.MatchesOpening(new(Guid.NewGuid(), submitted, null), node));
        Assert.True(form.MatchesOpening(new(Guid.NewGuid(), submitted with { Title = node.Title }, null), node));
    }

    [Fact]
    public async Task Authority_revocation_retires_an_already_displayed_current_comparison_without_another_read() {
        await using var fixture = new Fixture();
        var retired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await fixture.Session.OpenAsync(() => {
            retired.TrySetResult();
            return Task.CompletedTask;
        });
        await fixture.CompareAsync();
        Assert.NotNull(fixture.Session.Current);
        Assert.Equal(0, fixture.Changes.ActiveSubscriptions);

        fixture.Access.Grant!.Revoke();
        await retired.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Null(fixture.Session.Current);
        Assert.Null(fixture.Session.Review);
        Assert.Equal(1, fixture.Reads);
    }

    [Fact]
    public async Task Hints_mark_only_potential_staleness_and_a_hint_during_read_is_not_acknowledged_by_that_read() {
        await using var fixture = new Fixture();
        await fixture.Session.OpenAsync();
        await fixture.CompareAsync();
        var stream = Guid.NewGuid();
        fixture.Session.ObserveHint(new(stream, 1));
        Assert.True(fixture.Session.CurrentMayBeStale);
        Assert.Equal(1, fixture.Reads);
        await fixture.CompareAsync();
        Assert.False(fixture.Session.CurrentMayBeStale);
        fixture.AfterRead = () => fixture.Session.ObserveHint(new(stream, 2));

        await fixture.CompareAsync();

        Assert.True(fixture.Session.CurrentMayBeStale);
        Assert.Equal(3, fixture.Reads);
        Assert.Equal(0, fixture.Changes.ActiveSubscriptions);
    }

    private sealed class RecordingAccess : IEditingUiAccess {
        private readonly EditingUiTestAccess inner = new();
        public EditingUiGrant? Grant { get; private set; }
        public bool Allowed { get => inner.Allowed; set => inner.Allowed = value; }
        public async Task<EditingUiGrant> OpenAsync(EditingTopic topic, CancellationToken token) {
            Grant = await inner.OpenAsync(topic, token);
            return Grant;
        }
        public ValueTask ValidateAsync(EditingUiGrant grant, bool write, CancellationToken token) => inner.ValidateAsync(grant, write, token);
    }

    private sealed class Fixture : IAsyncDisposable {
        public ProjectStructureNode Original { get; }
        public ProjectStructureNode Current { get; set; }
        public CanvasWorkbenchComposerDraft Draft { get; set; }
        public ProjectStructureNodeEditingSession Session { get; }
        public RecordingAccess Access { get; } = new();
        public EditingChangeFeed Changes { get; } = new();
        public Action? AfterRead { get; set; }
        public bool Active { get; set; } = true;
        public int Reads { get; private set; }

        public Fixture() {
            var project = new ProjectWriteAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            Original = new("custom:note", null, ProjectObjectType.Note, "", "first", "", "", "first\nmiddle\nlast", "", "", null,
                "", "", "", 0, 0, new("rect", "", "", "Note"), [], "", 0, "", "", "", [], 0, ProjectId: project.ProjectId) { RecordId = Guid.NewGuid() };
            Current = Original with { Title = "peer first", Notes = "peer first\nmiddle\nlast" };
            Draft = new(Guid.NewGuid(), 2, false, true, null, new(Original.Id, Original.Title, "first\nmiddle\nlocal last"));
            Session = new(Draft.OpeningId, project, Original, new(new([], inlineNote: true), null),
                new EditingObservation(Changes, Access, new(new(EditingScopeKind.Dataset, project.DatabaseProfileId, 1), EditingOwner.ProjectStructure),
                    NullLogger<EditingObservation>.Instance), _ => {
                    Reads++;
                    AfterRead?.Invoke();
                    return Task.FromResult(Current);
                }, () => Active);
        }

        public Task<CanvasWorkbenchComposerDraft?> ReadDraftAsync() => Task.FromResult<CanvasWorkbenchComposerDraft?>(Draft);
        public Task CompareAsync() => Session.CompareAsync(Draft, ReadDraftAsync);
        public ValueTask DisposeAsync() => Session.DisposeAsync();
    }
}
