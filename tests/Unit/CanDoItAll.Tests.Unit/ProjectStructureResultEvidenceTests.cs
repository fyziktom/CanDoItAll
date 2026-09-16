using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit.ProjectStructure;

public sealed class ProjectStructureResultEvidenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_collection_read_never_relabels_an_old_result_with_a_new_or_removed_lifetime(bool removed) {
        var original = Admission();
        var capture = Scope([original], collection: true);
        await capture.CaptureAsync(original.ProjectId, CancellationToken.None);
        using (capture.Bind()) {
            ProjectStructureResultEvidenceScope.RecordResult(new[] { Summary(original.ProjectId) });
        }
        var current = removed ? Map() : Map(new ProjectWriteAdmission(original.DatabaseProfileId, original.ProjectId, Guid.NewGuid()));
        var result = capture.Finish(current);
        Assert.Equal(ProjectStructureDisclosureState.LifetimeChangedDuringOperation, result.State);
        Assert.Equal(original, Assert.Single(result.Targets));
    }

    [Fact]
    public async Task A_project_created_between_collection_brackets_cannot_supply_missing_original_provenance() {
        var created = Admission();
        var capture = Scope([], collection: true);
        await capture.CaptureAsync(created.ProjectId, CancellationToken.None);
        using (capture.Bind()) {
            ProjectStructureResultEvidenceScope.RecordResult(new[] { Summary(created.ProjectId) });
        }
        var result = capture.Finish(Map(created));
        Assert.Equal(ProjectStructureDisclosureState.MissingOriginalLifetime, result.State);
        Assert.Empty(result.Targets);
        Assert.Empty(result.DirectAccessProjectIds);
    }

    [Fact]
    public async Task Hierarchy_projection_retains_related_lifetimes_without_inventing_direct_project_grants() {
        var parent = Admission();
        var child = Admission(parent.DatabaseProfileId);
        var capture = Scope([parent, child], collection: true);
        await capture.CaptureAsync(parent.ProjectId, CancellationToken.None);
        using (capture.Bind()) {
            ProjectStructureResultEvidenceScope.RecordResult(new ProjectHierarchySnapshot(parent.ProjectId, [], [Summary(child.ProjectId)]));
        }
        var result = capture.Finish(Map(parent, child));
        Assert.Equal(ProjectStructureDisclosureState.Complete, result.State);
        Assert.Equal(2, result.Targets.Length);
        Assert.Equal(parent.ProjectId, Assert.Single(result.DirectAccessProjectIds));
    }

    [Fact]
    public async Task Reserved_child_and_parent_are_recorded_before_the_actual_create_callback() {
        var parent = Admission();
        var child = Admission(parent.DatabaseProfileId);
        var capture = Scope([parent]);
        using (capture.Bind()) {
            ProjectStructureResultEvidenceScope.RecordReservation(new(Guid.NewGuid(), parent.DatabaseProfileId,
                child.ProjectId, child.LifetimeId, Guid.NewGuid(), parent.ProjectId, parent.LifetimeId));
            await Task.Yield();
            ProjectStructureResultEvidenceScope.RecordResult(Summary(child.ProjectId));
        }
        var result = capture.Finish(Map(parent, child));
        Assert.Equal(ProjectStructureDisclosureState.Complete, result.State);
        Assert.Equal(2, result.Targets.Length);
        Assert.Equal(2, result.DirectAccessProjectIds.Length);
        Assert.Contains(child, result.Targets);
    }

    [Fact]
    public void An_actual_mutation_admission_cannot_overwrite_the_previously_captured_lifetime() {
        var original = Admission();
        var replacement = new ProjectWriteAdmission(original.DatabaseProfileId, original.ProjectId, Guid.NewGuid());
        var capture = Scope([original]);
        using (capture.Bind()) {
            ProjectStructureResultEvidenceScope.RecordAdmission(replacement);
            ProjectStructureResultEvidenceScope.RecordResult(new OperationAck(true));
        }
        var result = capture.Finish(Map(replacement));
        Assert.Equal(ProjectStructureDisclosureState.LifetimeChangedDuringOperation, result.State);
        Assert.Equal(original, Assert.Single(result.Targets));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Historical_analytics_and_supplied_snapshots_never_gain_current_lifetime_provenance(bool analytics) {
        var original = Admission();
        var capture = Scope([original], collection: true);
        using (capture.Bind()) {
            if (analytics) {
                ProjectStructureResultEvidenceScope.RecordResult(new ProjectStructureAgentAnalyticsResponse([
                    new("structure.read", original.ProjectId, null, null, true, 1, 0, null, DateTimeOffset.UtcNow)]));
            } else {
                ProjectStructureResultEvidenceScope.RecordResult(new ProjectStructureReadToolData(original.ProjectId,
                    "Historical", [], [], [], ProjectStructureReadSource.InvocationSnapshot));
            }
        }
        Assert.Equal(ProjectStructureDisclosureState.HistoricalSourceUnavailable, capture.Finish(Map(original)).State);
    }

    [Fact]
    public async Task Nested_invocations_do_not_capture_each_others_target_or_result() {
        var outerProject = Admission();
        var innerProject = Admission(outerProject.DatabaseProfileId);
        var outer = Scope([outerProject]);
        var inner = Scope([innerProject]);
        using (outer.Bind()) {
            ProjectStructureResultEvidenceScope.RecordAdmission(outerProject);
            using (inner.Bind()) {
                await Task.Yield();
                ProjectStructureResultEvidenceScope.RecordAdmission(innerProject);
                ProjectStructureResultEvidenceScope.RecordResult(new OperationAck(true));
            }
            ProjectStructureResultEvidenceScope.RecordResult(new OperationAck(true));
        }
        Assert.Equal(outerProject, Assert.Single(outer.Finish(Map(outerProject)).Targets));
        Assert.Equal(innerProject, Assert.Single(inner.Finish(Map(innerProject)).Targets));
    }

    [Fact]
    public async Task A_canonical_read_retains_project_keys_in_link_endpoints_and_parent_references() {
        var parent = Admission();
        var related = Admission(parent.DatabaseProfileId);
        var capture = Scope([parent, related], collection: true);
        await capture.CaptureAsync(parent.ProjectId, CancellationToken.None);
        using (capture.Bind()) {
            ProjectStructureResultEvidenceScope.RecordResult(new ProjectStructureReadToolData(parent.ProjectId, "Current", [],
                [new($"project:{parent.ProjectId:D}", $"project-child:{related.ProjectId:D}", default, true)], []));
        }
        Assert.Contains(related, capture.Finish(Map(parent, related)).Targets);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Invalid_owner_evidence_is_rejected_before_any_cached_disclosure(int change) {
        var project = Admission();
        var evidence = new ProjectStructureDisclosureEvidence(ProjectStructureToolPolicy.ProjectStructureRead,
            project.DatabaseProfileId, Guid.NewGuid(), ProjectStructureDisclosureState.Complete, [project], [project.ProjectId]);
        evidence = change switch {
            0 => evidence with { State = (ProjectStructureDisclosureState)999 },
            1 => evidence with { Targets = [project, project] },
            2 => evidence with { DirectAccessProjectIds = [Guid.NewGuid()] },
            _ => evidence with { Targets = [null!] }
        };
        var envelope = AgentToolProtocolEnvelope.Create("workbench-structure-result-authority", 1, JsonSerializer.Serialize(evidence));
        Assert.Throws<InvalidDataException>(() => ProjectStructureDisclosureEvidenceCodec.Read(envelope));
    }

    [Fact]
    public void Valid_owner_evidence_round_trips_only_immutable_identity_and_authority_facts() {
        var project = Admission();
        var evidence = new ProjectStructureDisclosureEvidence(ProjectStructureToolPolicy.ProjectStructureRead,
            project.DatabaseProfileId, Guid.NewGuid(), ProjectStructureDisclosureState.Complete, [project], [project.ProjectId]);
        var encoded = ProjectStructureDisclosureEvidenceCodec.Write(evidence);
        var decoded = ProjectStructureDisclosureEvidenceCodec.Read(encoded);
        Assert.Equal(evidence.ToolName, decoded.ToolName);
        Assert.Equal(evidence.DatabaseProfileId, decoded.DatabaseProfileId);
        Assert.Equal(evidence.AgentId, decoded.AgentId);
        Assert.Equal(evidence.Targets.ToArray(), decoded.Targets.ToArray());
        Assert.Equal(evidence.DirectAccessProjectIds.ToArray(), decoded.DirectAccessProjectIds.ToArray());
        Assert.DoesNotContain("prompt", encoded.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectWriteAdmission Admission(Guid? profileId = null) => new(profileId ?? Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private static ProjectStructureResultEvidenceScope Scope(ProjectWriteAdmission[] initial, bool collection = false)
        => new(ProjectStructureToolPolicy.ProjectStructureRead, initial.FirstOrDefault()?.DatabaseProfileId ?? Guid.NewGuid(),
            Guid.NewGuid(), (_, _) => throw new InvalidOperationException("Pure evidence tests must not resolve a later lifetime."), Map(initial), collection);

    private static Dictionary<Guid, ProjectWriteAdmission> Map(params ProjectWriteAdmission[] values)
        => values.ToDictionary(value => value.ProjectId);

    private static ProjectSummary Summary(Guid id) => new(id, "Retained project", ProjectStatus.Active, string.Empty, 0, 0, 0, DateTimeOffset.UtcNow);
}
