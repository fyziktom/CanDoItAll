using CanDoItAll.CrmHr.UI.Crm;
using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Tests.Unit.CrmHr;

public sealed class ConnectionReconciliationTests {
    [Fact]
    public void Disjoint_fields_merge_locally_without_replacing_the_form_or_advancing_to_a_later_writer() {
        var account = Guid.NewGuid();
        var b = Row();
        var state = new ConnectionReconciliation();
        state.Open(Workspace(account, [b]));
        var rows = state.Draft;
        var local = Assert.Single(rows);
        local.Notes = "  Local notes  ";
        state.Input(new(local.Id!.Value, ConnectionField.Notes));
        var peer = b.Snapshot();
        peer.Role = CrmAccountConnectionRole.Sponsor;
        var current = Workspace(account, [peer], 1);
        state.Hint();
        state.SetReview(current, state.HintRevision, state.InputRevision);

        Assert.True(state.ApplyReview());

        Assert.Same(rows, state.Draft);
        Assert.Same(local, Assert.Single(state.Draft));
        Assert.Equal("  Local notes  ", local.Notes);
        Assert.Equal(CrmAccountConnectionRole.Sponsor, local.Role);
        Assert.Equal(current.ConnectionsOriginal!.Rows, state.Original!.Rows);
        Assert.Equal(current.ConnectionsOriginal.Projects, state.Original.Projects);
        Assert.True(state.IsDirty);
        Assert.True(state.CanSubmit);
    }

    [Fact]
    public void Third_writer_and_failed_refresh_keep_choices_but_block_the_previous_comparison() {
        var account = Guid.NewGuid();
        var b = Row();
        var state = new ConnectionReconciliation();
        state.Open(Workspace(account, [b]));
        state.Draft[0].Notes = "Local conflicting note";
        var peer = b.Snapshot();
        peer.Notes = "Peer conflicting note";
        var current = Workspace(account, [peer], 1);
        state.SetReview(current, state.HintRevision, state.InputRevision);
        var notes = state.Review!.Fields[b.Id!.Value].Notes;
        notes.SetResult("My combined work");
        state.Hint();
        Assert.False(state.CanApply);
        state.ReadingReview(true);
        state.ReadingReview(false);
        Assert.Same(notes, state.Review.Fields[b.Id.Value].Notes);
        Assert.False(state.CanApply);
        peer.Notes = "Third saved note";

        state.SetReview(Workspace(account, [peer], 2), state.HintRevision, state.InputRevision);

        Assert.Equal(MergeChoice.Custom, state.Review.Fields[b.Id.Value].Notes.Choice);
        Assert.Equal("My combined work", state.Review.Fields[b.Id.Value].Notes.Result);
        Assert.True(state.CanApply);
    }

    [Fact]
    public void Exact_acceptance_preserves_later_ABA_raw_text_and_new_rows_in_the_same_list() {
        var account = Guid.NewGuid();
        var b = Row();
        var state = new ConnectionReconciliation();
        state.Open(Workspace(account, [b]));
        var rows = state.Draft;
        var local = rows[0];
        local.Notes = "  Submitted raw note  ";
        var s = state.Begin()!;
        local.Notes = "Later";
        state.Input(new(local.Id!.Value, ConnectionField.Notes));
        local.Notes = "  Submitted raw note  ";
        state.Input(new(local.Id.Value, ConnectionField.Notes));
        var newRow = Row();
        rows.Add(newRow);
        var a = b.Snapshot();
        a.Notes = "Submitted raw note";
        var current = Workspace(account, [a], 1);

        state.Accept(s, new(account, [a], current.ConnectionsOriginal!));
        state.Complete(s);

        Assert.Same(rows, state.Draft);
        Assert.Same(local, rows[0]);
        Assert.Same(newRow, rows[1]);
        Assert.Equal("  Submitted raw note  ", local.Notes);
        Assert.True(state.IsDirty);
        Assert.True(state.CanSubmit);
    }

    [Fact]
    public void Unknown_outcome_and_retired_opening_cannot_be_cleared_by_current_reads_or_receipts() {
        var account = Guid.NewGuid();
        var b = Row();
        var state = new ConnectionReconciliation();
        var current = Workspace(account, [b]);
        state.Open(current);
        var s = state.Begin()!;
        state.Unknown(s);
        state.Complete(s);
        state.Observe(current);
        state.SetReview(current, state.HintRevision, state.InputRevision);
        Assert.True(state.IsUnknown);
        Assert.False(state.CanApply);
        Assert.Null(state.Begin());
        state.Open(current);
        var reopened = state.Draft;
        state.Draft[0].Notes = "Current opening";

        state.Accept(s, new(account, [b], current.ConnectionsOriginal!));

        Assert.Same(reopened, state.Draft);
        Assert.Equal("Current opening", state.Draft[0].Notes);
        Assert.Null(state.Accepted);
    }

    [Fact]
    public void A_removed_row_cannot_be_restored_by_local_choice_or_undo() {
        var account = Guid.NewGuid();
        var b = Row();
        var state = new ConnectionReconciliation();
        state.Open(Workspace(account, [b]));
        state.Draft[0].Notes = "Local edit of removed row";
        state.SetReview(Workspace(account, [], 1), state.HintRevision, state.InputRevision);
        var row = Assert.Single(state.Review!.Rows);
        row.Field.UseLocal();
        Assert.False(state.CanApply);
        row.Field.UseCurrent();

        Assert.True(state.ApplyReview());

        Assert.Empty(state.Draft);
        Assert.False(state.CanUndo);
    }

    [Fact]
    public void A_replaced_project_lifetime_requires_explicit_reselection_and_a_union_above_budget_uses_complete_snapshots() {
        var account = Guid.NewGuid();
        var project = new CanDoItAll.Modules.Projects.ProjectWriteAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var b = Row();
        b.ProjectIds = [project.ProjectId];
        b.ProjectAdmissions[project.ProjectId] = project;
        var local = b.Snapshot();
        var peer = b.Snapshot();
        peer.ProjectAdmissions[project.ProjectId] = new(project.DatabaseProfileId, project.ProjectId, Guid.NewGuid());
        var review = new ConnectionMergeReview(account, [b], [local], Workspace(account, [peer]), null);
        Assert.False(review.CanApply);
        Assert.Contains("lifetime", review.GroupError, StringComparison.OrdinalIgnoreCase);
        var original = Enumerable.Range(0, 1000).Select(_ => Row()).ToArray();

        var large = new ConnectionMergeReview(account, original, original.Append(Row()).ToArray(),
            Workspace(account, original.Append(Row()).ToArray()), null);

        Assert.Empty(large.Rows);
        Assert.NotNull(large.WholeCollection);
        large.WholeCollection.UseCurrent();
        Assert.Equal(1001, large.Candidate().Count);
    }

    private static CrmAccountConnectionEditorModel Row() => new() {
        Id = Guid.NewGuid(), RelatedPartyId = Guid.NewGuid(), Notes = "Original notes"
    };

    private static CrmAccountWorkspaceModel Workspace(Guid account, IReadOnlyList<CrmAccountConnectionEditorModel> rows, int version = 0) {
        var created = new DateTimeOffset(2031, 1, 2, 3, 4, 5, TimeSpan.Zero);
        return new(account, "Account", "", PartyLifecycleStatus.Active, [], [], "", "", new() { AccountPartyId = account },
            rows.Select(row => new CrmAccountConnectedRecordItemModel(row.Id!.Value, row.RelatedPartyId, "Directory record", PartyType.Person,
                row.Role, row.IsPrimary, row.Notes, row.ProjectIds.Select(id => new CrmAccountConnectionProjectItemModel(id, "Project",
                    CanDoItAll.Modules.Projects.ProjectStatus.Active) { Admission = row.ProjectAdmissions.GetValueOrDefault(id) }).ToArray())).ToArray(), [], 0) {
            ConnectionsOriginal = new(account, rows.Select(row => new CrmAccountConnectionOriginal(row.Id!.Value, row.RelatedPartyId,
                row.Role, row.IsPrimary, row.Notes, created, created.AddSeconds(version))).ToArray(),
                rows.SelectMany(row => row.ProjectIds.Select(id => new CrmAccountConnectionProjectOriginal(id, row.Id!.Value, id, created))).ToArray())
        };
    }
}
