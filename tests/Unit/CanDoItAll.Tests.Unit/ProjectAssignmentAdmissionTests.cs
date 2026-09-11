using System.Text.Json;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectAssignmentAdmissionTests {
    [Fact]
    public void Replacement_snapshot_retains_scope_and_every_editor_field_when_original_changes() {
        var expected = new ProjectWriteAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var request = new ProjectPartyAssignmentUpsertRequest {
            AssignmentId = Guid.NewGuid(), ProjectId = expected.ProjectId, ExpectedProjectAdmission = expected,
            PartyId = Guid.NewGuid(), PartyAffiliationId = Guid.NewGuid(), Role = ProjectPartyAssignmentRole.WorkItemAssignee,
            NodeKey = "work-item:one", IsPrimary = true, AllocationPercent = 31.25m,
            StartsOn = new(2026, 9, 10), EndsOn = new(2026, 9, 12), Source = "editor", Notes = "original"
        };
        var original = JsonSerializer.Serialize(request);
        var snapshot = Assert.Single(ProjectAssignmentAdmission.Snapshot(expected.ProjectId, [request], expected));
        request.ProjectId = Guid.NewGuid();
        request.ExpectedProjectAdmission = new(Guid.NewGuid(), request.ProjectId, Guid.NewGuid());
        request.Notes = "redirected";
        request.PartyId = Guid.NewGuid();
        Assert.Equal(original, JsonSerializer.Serialize(snapshot));
    }

    [Fact]
    public void Empty_replacement_still_requires_explicit_matching_project_admission() {
        var expected = new ProjectWriteAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => ProjectAssignmentAdmission.Require(expected.ProjectId, null));
        Assert.Throws<InvalidOperationException>(() => ProjectAssignmentAdmission.Require(Guid.NewGuid(), expected));
        Assert.Empty(ProjectAssignmentAdmission.Snapshot(expected.ProjectId, [], expected));
    }

    [Fact]
    public void Nullable_reference_roundtrip_keeps_legacy_unbound_and_cannot_become_write_admission() {
        var reference = new ProjectAssignmentReference(Guid.NewGuid(), Guid.NewGuid(), null);
        var restored = JsonSerializer.Deserialize<ProjectAssignmentReference>(JsonSerializer.Serialize(reference));
        Assert.Equal(reference, restored);
        Assert.Throws<InvalidOperationException>(() => restored!.RequireBoundAdmission());
        Assert.Throws<InvalidOperationException>(() => reference.RequireProfile(Guid.NewGuid(), reference.ProjectId));
    }

    [Fact]
    public void Bound_cleanup_reference_roundtrip_retains_exact_incarnation() {
        var admission = new ProjectWriteAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var reference = ProjectAssignmentReference.From(admission);
        var restored = JsonSerializer.Deserialize<ProjectAssignmentReference>(JsonSerializer.Serialize(reference));
        Assert.Equal(admission, restored!.RequireBoundAdmission());
    }
}
