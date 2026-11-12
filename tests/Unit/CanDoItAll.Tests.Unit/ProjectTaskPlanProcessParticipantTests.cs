using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectTaskPlanProcessParticipantTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Participant_reads_original_immutable_material_without_current_assignment_access(bool step) {
        var original = ProjectTaskPlanProcessCodecFixture.Create(step);
        var reader = new OriginalReader(original);
        var participant = new ProjectTaskPlanProcessParticipant(reader, reader, null!, null!);
        Assert.Equal(original.Basis, await participant.ReadOriginalBasisAsync(original.Origin, default));
        Assert.Equal(1, reader.ChildReads);
        Assert.Equal(1, reader.MaterialReads);
        var prepared = Assert.IsType<AgentPlanningResponsePreparation.Publication>(participant.PreparePublication(
            original.Decision, original.Basis, ProjectTaskPlanProcessCodecFixture.Draft));
        using var arguments = JsonDocument.Parse(prepared.Call.Payload.ArgumentsJson);
        Assert.Equal(prepared.Call.Payload, ProjectTaskPlanAgentCodec.Restore(arguments.RootElement, prepared.Call.Payload.SourcePreparation!));
        var captured = ProjectTaskPlanAgentCodec.Read<ProjectTaskPlanAgentSource>(prepared.Call.Payload.SourcePreparation!,
            ProjectTaskPlanAgentCodec.SourceFormat, AgentToolPreparedPayload.MaximumSourcePreparationUtf8Bytes);
        Assert.Equal(original.Basis.Value.Digest, captured.BasisDigest);
    }

    [Theory]
    [InlineData(MissingEvidence.Child)]
    [InlineData(MissingEvidence.Allocation)]
    [InlineData(MissingEvidence.Material)]
    [InlineData(MissingEvidence.MaterialStamp)]
    public async Task Missing_or_substituted_original_evidence_never_recaptures_current_material(MissingEvidence missing) {
        var original = ProjectTaskPlanProcessCodecFixture.Create();
        var reader = new OriginalReader(original);
        if (missing == MissingEvidence.Child) {
            reader.Child = null;
        } else if (missing == MissingEvidence.Allocation) {
            reader.Child = reader.Child! with { Admission = reader.Child.Admission with { AllocationId = new(Guid.NewGuid()) } };
        } else if (missing == MissingEvidence.Material) {
            reader.Material = null;
        } else {
            reader.Material = ProjectTaskPlanProcessCodecFixture.Create().Material;
        }
        var participant = new ProjectTaskPlanProcessParticipant(reader, reader, null!, null!);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => participant.ReadOriginalBasisAsync(original.Origin, default));
        Assert.Equal(missing is MissingEvidence.Child or MissingEvidence.Allocation ? 0 : 1, reader.MaterialReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shared_output_mapping_keeps_shape_and_limit_refusals_explicit(bool limit) {
        var preparation = ProjectTaskPlanPlanningContent.Prepare(() => {
            if (limit) {
                throw new ArgumentException("bound");
            }
            throw new JsonException("shape");
        });
        Assert.Equal(limit ? AgentPlanningOutputRefusal.LimitExceeded : AgentPlanningOutputRefusal.InvalidShape,
            Assert.IsType<AgentPlanningResponsePreparation.Refused>(preparation).Reason);
    }

    [Fact]
    public void Shared_output_mapping_does_not_hide_storage_failures() {
        var failure = new IOException("original-material-unavailable");
        Assert.Same(failure, Assert.Throws<IOException>(() => ProjectTaskPlanPlanningContent.Prepare(() => throw failure)));
    }

    [Fact]
    public void Description_preserves_exact_original_project_and_approved_tasks() {
        var original = ProjectTaskPlanProcessCodecFixture.Create();
        var prepared = original.Prepare();
        var command = ProjectTaskPlanProcessCodec.Resolve(prepared.Observation, original.Target.Session, prepared.Payload, prepared.Binding);
        Assert.Equal($"Original project: {original.Target.Project.ProjectId:D}\n\n1. Review\nDescription: Original task snapshot\nNotes: \nPriority: 2",
            ProjectTaskPlanPlanningContent.Describe(command.Command));
        Assert.Equal("project_task_plan", ProjectTaskPlanPlanningContent.Prompt.SchemaName);
        Assert.Contains("Do not repeat existing tasks.", ProjectTaskPlanPlanningContent.Prompt.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_stops_before_reading_material() {
        var original = ProjectTaskPlanProcessCodecFixture.Create();
        var reader = new OriginalReader(original);
        var participant = new ProjectTaskPlanProcessParticipant(reader, reader, null!, null!);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => participant.ReadOriginalBasisAsync(original.Origin, cancellation.Token));
        Assert.Equal(0, reader.MaterialReads);
    }

    public enum MissingEvidence { Child, Allocation, Material, MaterialStamp }

    private sealed class OriginalReader(ProjectTaskPlanProcessCodecFixture original) : IProcessDecisionChildReader, IProcessDecisionMaterialReader {
        internal ProcessDecisionChildObservation? Child { get; set; } = new(original.Parent.Child,
            new(original.Parent.Child.Address.Reference, 3, ProcessDecisionPhase.Active, original.Parent.Child.MaterialId,
                original.Parent.Child.BudgetId, 1, 1), null, null, true);
        internal ProcessDecisionMaterial? Material { get; set; } = original.Material;
        internal int ChildReads { get; private set; }
        internal int MaterialReads { get; private set; }

        public Task<ProcessDecisionChildObservation?> ReadAsync(ProcessDecisionChildAddress address, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(original.Parent.Child.Address, address);
            ChildReads++;
            return Task.FromResult(Child);
        }

        public Task<ProcessDecisionMaterial?> ReadAsync(ProcessDecisionMaterialScope scope, ProcessDecisionMaterialReference reference,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(original.Parent.Scope, scope);
            Assert.Equal(original.Parent.Material.Reference, reference);
            MaterialReads++;
            return Task.FromResult(Material);
        }
    }
}
