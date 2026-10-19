using System.Text;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectLifecycleOperationCodecTests {
    [Fact]
    public void All_four_acknowledgements_round_trip_the_original_operation_and_exact_accepted_result() {
        foreach (var accepted in AcceptedCases()) {
            var bytes = ProjectLifecycleOperationCodec.EncodeAccepted(accepted);
            var decoded = ProjectLifecycleOperationCodec.DecodeAccepted(bytes, accepted.Original.Operation);

            Assert.Equal(accepted.Original, decoded.Original);
            Assert.Equal(accepted.AcceptedAtUtc, decoded.AcceptedAtUtc);
            Assert.Equal(accepted.ReplayUntilUtc, decoded.ReplayUntilUtc);
            Assert.Equal(bytes, ProjectLifecycleOperationCodec.EncodeAccepted(decoded));
        }
    }

    [Fact]
    public void Original_revision_lifetime_edge_and_destination_are_semantic_intent() {
        var project = Project();
        var parent = new ProjectWriteAdmission(project.DatabaseProfileId, Guid.NewGuid(), Guid.NewGuid());
        var destination = new ProjectWriteAdmission(project.DatabaseProfileId, Guid.NewGuid(), Guid.NewGuid());
        var edge = new ProjectHierarchyEdgeExpectation(Guid.NewGuid(), parent, project);
        var operation = Operation(project.DatabaseProfileId);
        var commands = new ProjectLifecycleCommand[] {
            new ProjectLifecycleCommand.Delete(new(project, Guid.NewGuid())),
            new ProjectLifecycleCommand.Delete(new(project, Guid.NewGuid())),
            new ProjectLifecycleCommand.Delete(new(new(project.DatabaseProfileId, project.ProjectId, Guid.NewGuid()), Guid.NewGuid())),
            new ProjectLifecycleCommand.EnsureLink(parent, project),
            new ProjectLifecycleCommand.RemoveLink(edge),
            new ProjectLifecycleCommand.RemoveLink(new(Guid.NewGuid(), parent, project)),
            new ProjectLifecycleCommand.ReconnectLink(edge, destination),
            new ProjectLifecycleCommand.ReconnectLink(edge, new(project.DatabaseProfileId, Guid.NewGuid(), Guid.NewGuid()))
        };
        var digests = commands.Select(command => ProjectLifecycleOperationCodec.ComputeDigest(
            new(operation, ProjectLifecycleInterpretation.VersionOne, command), ProjectMutationCallerScope.LocalWorkspace,
            ProjectLifecycleEffectBinding.None).Hex).ToArray();

        Assert.Equal(commands.Length, digests.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void A_receipt_cannot_be_observed_as_a_different_original_operation_or_caller_partition() {
        var accepted = AcceptedCases()[0];
        var original = accepted.Original.Operation;
        var bytes = ProjectLifecycleOperationCodec.EncodeAccepted(accepted);
        var alternatives = new[] {
            new ProjectLifecycleOperation(Guid.NewGuid(), original.Namespace, original.Epoch, original.CallerBinding, original.Id),
            new ProjectLifecycleOperation(original.DatabaseProfileId, new(Guid.NewGuid()), original.Epoch, original.CallerBinding, original.Id),
            new ProjectLifecycleOperation(original.DatabaseProfileId, original.Namespace, new(original.Epoch.Value + 1), original.CallerBinding, original.Id),
            new ProjectLifecycleOperation(original.DatabaseProfileId, original.Namespace, original.Epoch,
                ProjectMutationCallerScope.OpenApi.Bind(original.Namespace), original.Id),
            new ProjectLifecycleOperation(original.DatabaseProfileId, original.Namespace, original.Epoch, original.CallerBinding, Guid.NewGuid())
        };
        foreach (var different in alternatives) {
            Assert.Throws<InvalidDataException>(() => ProjectLifecycleOperationCodec.DecodeAccepted(bytes, different));
        }
        Assert.Throws<ArgumentException>(() => ProjectLifecycleOperationCodec.ComputeDigest(accepted.Original,
            ProjectMutationCallerScope.OpenApi, ProjectLifecycleEffectBinding.None));
    }

    [Fact]
    public void Noncanonical_unknown_and_oversized_receipts_do_not_become_accepted_results() {
        var accepted = AcceptedCases()[1];
        var json = Encoding.UTF8.GetString(ProjectLifecycleOperationCodec.EncodeAccepted(accepted));
        var changed = new[] {
            json + " ",
            json.Insert(1, "\"unknown\":1,"),
            json.Insert(1, "\"resultVersion\":1,"),
            json.Replace("\"resultVersion\":1", "\"resultVersion\":2", StringComparison.Ordinal)
        };
        foreach (var value in changed) {
            Assert.Throws<InvalidDataException>(() => ProjectLifecycleOperationCodec.DecodeAccepted(Encoding.UTF8.GetBytes(value), accepted.Original.Operation));
        }
        Assert.Throws<InvalidDataException>(() => ProjectLifecycleOperationCodec.DecodeAccepted(
            new byte[ProjectLifecycleAccepted.MaximumPayloadBytes + 1], accepted.Original.Operation));
        Assert.Throws<ArgumentException>(() => new ProjectLifecycleAccepted(accepted.Original, 1,
            DateTimeOffset.MinValue, accepted.ReplayUntilUtc, accepted.Acknowledgement));
        Assert.Throws<ArgumentException>(() => new ProjectLifecycleAccepted(accepted.Original, 1,
            accepted.AcceptedAtUtc, accepted.ReplayUntilUtc, new ProjectLifecycleAcknowledgement.LinkRemoved()));
    }

    [Fact]
    public void Maximum_escaped_participant_references_fit_the_accepted_receipt_bound_and_remain_detached() {
        var accepted = AcceptedCases()[0];
        var deletion = Assert.IsType<ProjectLifecycleAcknowledgement.DeletionCommitted>(accepted.Acknowledgement);
        var obligations = Enumerable.Range(0, 16).Select(index => new ProjectDeletionObligation(
            new(index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + new string('\u0001', 62)), Guid.NewGuid())).ToArray();
        var completion = new ProjectDeletionCompletionBinding(deletion.Completion.Original, obligations);
        var maximum = new ProjectLifecycleAccepted(accepted.Original, 1, accepted.AcceptedAtUtc, accepted.ReplayUntilUtc,
            new ProjectLifecycleAcknowledgement.DeletionCommitted(completion));
        var bytes = ProjectLifecycleOperationCodec.EncodeAccepted(maximum);
        obligations[0] = new(new("changed"), Guid.NewGuid());

        Assert.InRange(bytes.Length, 1, ProjectLifecycleAccepted.MaximumPayloadBytes);
        var decoded = ProjectLifecycleOperationCodec.DecodeAccepted(bytes, maximum.Original.Operation);
        Assert.Equal(bytes, ProjectLifecycleOperationCodec.EncodeAccepted(decoded));
        Assert.Equal(16, Assert.IsType<ProjectLifecycleAcknowledgement.DeletionCommitted>(decoded.Acknowledgement).Completion.Obligations.Count);
    }

    [Fact]
    public void Governed_effect_identity_is_detached_and_cannot_be_omitted_or_used_by_a_human_caller() {
        var original = AcceptedCases()[1].Original;
        var caller = ProjectMutationCallerScope.FromAdmittedExecution(new(Enumerable.Repeat((byte)7, 32).ToArray()));
        var operation = new ProjectLifecycleOperation(original.Operation.DatabaseProfileId, original.Operation.Namespace,
            original.Operation.Epoch, caller.Bind(original.Operation.Namespace), original.Operation.Id);
        var governed = new ProjectLifecycleSubmission(operation, original.Interpretation, original.Command);
        var bytes = Enumerable.Repeat((byte)9, 32).ToArray();
        var effect = new ProjectLifecycleEffectBinding(ProjectLifecycleEffectKind.Agent, 1, bytes);
        var before = ProjectLifecycleOperationCodec.ComputeDigest(governed, caller, effect);
        bytes[0] = 10;

        Assert.Equal(before, ProjectLifecycleOperationCodec.ComputeDigest(governed, caller, effect));
        Assert.NotEqual(before, ProjectLifecycleOperationCodec.ComputeDigest(governed, caller,
            new(ProjectLifecycleEffectKind.Process, 1, Enumerable.Repeat((byte)9, 32).ToArray())));
        Assert.Throws<ArgumentException>(() => ProjectLifecycleOperationCodec.ComputeDigest(governed, caller, ProjectLifecycleEffectBinding.None));
        Assert.Throws<ArgumentException>(() => ProjectLifecycleOperationCodec.ComputeDigest(original, ProjectMutationCallerScope.LocalWorkspace, effect));
    }

    private static ProjectLifecycleAccepted[] AcceptedCases() {
        var project = Project();
        var parent = new ProjectWriteAdmission(project.DatabaseProfileId, Guid.NewGuid(), Guid.NewGuid());
        var destination = new ProjectWriteAdmission(project.DatabaseProfileId, Guid.NewGuid(), Guid.NewGuid());
        var operation = Operation(project.DatabaseProfileId);
        var edge = new ProjectHierarchyEdgeExpectation(Guid.NewGuid(), parent, project);
        var completion = new ProjectDeletionCompletionBinding(new(new(project.DatabaseProfileId, operation.Namespace,
            new(Guid.NewGuid())), project), [new(new("workbench"), Guid.NewGuid())]);
        var now = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
        return [
            Accepted(new ProjectLifecycleCommand.Delete(new(project, Guid.NewGuid())), new ProjectLifecycleAcknowledgement.DeletionCommitted(completion)),
            Accepted(new ProjectLifecycleCommand.EnsureLink(parent, project), new ProjectLifecycleAcknowledgement.LinkEnsured(Guid.NewGuid(), false)),
            Accepted(new ProjectLifecycleCommand.RemoveLink(edge), new ProjectLifecycleAcknowledgement.LinkRemoved()),
            Accepted(new ProjectLifecycleCommand.ReconnectLink(edge, destination), new ProjectLifecycleAcknowledgement.LinkReconnected(Guid.NewGuid(), false))
        ];

        ProjectLifecycleAccepted Accepted(ProjectLifecycleCommand command, ProjectLifecycleAcknowledgement acknowledgement)
            => new(new(operation, ProjectLifecycleInterpretation.VersionOne, command), 1, now, now.AddHours(1), acknowledgement);
    }

    private static ProjectWriteAdmission Project() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private static ProjectLifecycleOperation Operation(Guid profile) {
        var ns = new DatasetNamespaceId(Guid.NewGuid());
        return new(profile, ns, new(1), ProjectMutationCallerScope.LocalWorkspace.Bind(ns), Guid.NewGuid());
    }
}
