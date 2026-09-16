using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class DatabaseTransferContractTests {
    [Fact]
    public void Public_handler_request_contains_only_profile_and_selection_data() {
        Assert.Equal(new[] { "ReplaceExisting", "SourceProfile", "TargetProfile" }, typeof(DatabaseTransferOperation).GetProperties().Select(property => property.Name).Order());
        Assert.False(typeof(DatabaseTransferOwnerSession).IsPublic);
        Assert.DoesNotContain(typeof(ProjectTransferTargetInspectionRunner).GetMethods(), method => method.Name == "Begin");
        foreach (var method in typeof(IDatabaseTransferHandler).GetMethods().Where(method => !method.IsSpecialName)) {
            Assert.Equal(typeof(DatabaseTransferOperation), method.GetParameters()[0].ParameterType);
            Assert.DoesNotContain(method.GetParameters(), parameter => typeof(DbContext).IsAssignableFrom(parameter.ParameterType) || parameter.ParameterType == typeof(IServiceProvider));
        }
    }

    [Fact]
    public void Work_history_keeps_original_payload_bytes_and_previous_import_provenance() {
        var row = Row();
        var snapshot = ProjectWorkAssignmentHistoryRecord.Capture(row, Guid.NewGuid());
        var raw = " \n" + snapshot.PayloadJson + " \n";
        var firstImport = new RetainedEvidenceImport(Guid.NewGuid(), Guid.NewGuid());
        var retained = new ProjectWorkAssignmentHistoryRecord(snapshot with { PayloadJson = raw, ImportedHistory = firstImport });
        Assert.Equal(raw, retained.ToTransfer().PayloadJson);
        Assert.Equal(firstImport, retained.ToTransfer().ImportedHistory);
        var data = new ProjectTransferDataSet { WorkAssignmentHistory = [retained.ToTransfer()] };
        data.PrepareForTargetImport(Guid.NewGuid(), Guid.NewGuid());
        var next = Assert.Single(data.WorkAssignmentHistory);
        Assert.Equal(raw, next.PayloadJson);
        Assert.Equal(firstImport, next.ImportedHistory!.Previous);
        var payload = ProjectWorkAssignmentHistoryRecord.Validate(next);
        Assert.Equal(ProjectPartyAssignmentRole.WorkItemAssignee, payload.Role);
        Assert.Equal(row.PartyOrganizationAffiliationId, payload.PartyOrganizationAffiliationId);
        Assert.Equal(row.OpportunityId, payload.OpportunityId);
        Assert.Equal(row.AllocationPercent, payload.AllocationPercent);
        Assert.Equal(row.Notes, payload.Notes);
        Assert.Null(payload.ProjectLifetimeId);
    }

    [Fact]
    public void Work_snapshot_identity_distinguishes_profiles_and_changed_rows_without_rewriting_the_original_ID() {
        var row = Row();
        var profile = Guid.NewGuid();
        var first = ProjectWorkAssignmentHistoryRecord.Capture(row, profile);
        Assert.Equal(first, ProjectWorkAssignmentHistoryRecord.Capture(row, profile));
        Assert.NotEqual(first.EvidenceId, ProjectWorkAssignmentHistoryRecord.Capture(row, Guid.NewGuid()).EvidenceId);
        row.Notes += " changed";
        var changed = ProjectWorkAssignmentHistoryRecord.Capture(row, profile);
        Assert.NotEqual(first.EvidenceId, changed.EvidenceId);
        Assert.Equal(row.Id, ProjectWorkAssignmentHistoryRecord.Validate(changed).Id);
    }

    [Fact]
    public void Work_history_requires_explicit_disposition_and_rejects_a_foreign_assignment_role() {
        var snapshot = ProjectWorkAssignmentHistoryRecord.Capture(Row(), Guid.NewGuid());
        Assert.Throws<InvalidDataException>(() => new ProjectWorkAssignmentHistoryRecord(snapshot));
        var payload = System.Text.Json.Nodes.JsonNode.Parse(snapshot.PayloadJson)!;
        payload["role"] = nameof(ProjectPartyAssignmentRole.Manager);
        Assert.Throws<InvalidDataException>(() => ProjectWorkAssignmentHistoryRecord.Validate(snapshot with { PayloadJson = payload.ToJsonString() }));
        Assert.Throws<InvalidDataException>(() => ProjectWorkAssignmentHistoryRecord.Validate(snapshot with { EvidenceId = Guid.Empty }));
    }

    private static ProjectWorkAssignmentRecord Row() => new() {
        ProjectId = Guid.NewGuid(), PartyId = Guid.NewGuid(), PartyOrganizationAffiliationId = Guid.NewGuid(),
        NodeKey = "work:original", PhaseName = "Original phase", OpportunityId = Guid.NewGuid(), AllocationPercent = 17.25m,
        StartsAtUtc = new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.Zero), EndsAtUtc = new DateTimeOffset(2025, 2, 3, 4, 5, 6, TimeSpan.Zero),
        IsPrimary = true, Source = "original", Notes = "  original notes\n retained  "
    };
}
