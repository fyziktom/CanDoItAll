using System.Text;
using System.Text.Json;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectDefinitionRecoveryCodecTests {
    private readonly ProjectDefinitionRecoveryCodec codec = new();
    private static readonly ProjectDefinitionLimits Limits = ProjectDefinitionLimits.VersionOneMaximum;

    [Theory]
    [InlineData(ProjectDefinitionCommandKind.Create)]
    [InlineData(ProjectDefinitionCommandKind.Replace)]
    [InlineData(ProjectDefinitionCommandKind.UpdateScalars)]
    public void Frozen_submission_roundtrip_keeps_raw_values_original_expectation_and_int64_identity(ProjectDefinitionCommandKind kind) {
        var submission = Submission(kind);
        var frozen = codec.Freeze(submission, Limits);
        var restored = codec.ReadSubmission(frozen.Payload, Limits);
        Assert.Equal(frozen.Payload, restored.Payload);
        Assert.Equal(submission.Operation, restored.Submission.Operation);
        Assert.Equal(long.MaxValue, restored.Submission.Operation.Epoch.Value);
        Assert.Contains("  Draft name  ", restored.Payload);
        Assert.Equal(submission.Kind, restored.Submission.Kind);
        Assert.Equal(submission.TargetProjectId, restored.Submission.TargetProjectId);
    }

    [Fact]
    public void Accepted_payload_is_the_existing_owner_canonical_encoding_and_blank_option_omission_is_valid() {
        var submission = Assert.IsType<ProjectDefinitionSubmission.Replace>(Submission(ProjectDefinitionCommandKind.Replace));
        var receipt = Accepted(submission);
        var encoded = codec.EncodeAccepted(submission, receipt, Limits);
        Assert.Equal(Encoding.UTF8.GetString(ProjectDefinitionOperationCodec.EncodeAccepted(receipt.Result, Limits).ToArray()), encoded.Payload);
        var restored = codec.ReadAccepted(submission, encoded.Payload, Limits);
        Assert.Equal(encoded.Payload, restored.Payload);
        Assert.Empty(Assert.IsType<ProjectDefinitionAcceptedReceipt.Full>(restored.Receipt).Result.Acknowledgement.Options);
    }

    [Fact]
    public void Accepted_mapping_must_preserve_original_child_identity_and_cover_nonblank_rows() {
        var submission = Assert.IsType<ProjectDefinitionSubmission.Replace>(Submission(ProjectDefinitionCommandKind.Replace));
        var valid = Accepted(submission);
        var changedId = valid.Result.Acknowledgement with {
            Phases = [valid.Result.Acknowledgement.Phases[0] with { Id = Guid.NewGuid() }]
        };
        Assert.Throws<ArgumentException>(() => codec.EncodeAccepted(submission, WithAck(valid, changedId), Limits));
        Assert.Throws<ArgumentException>(() => codec.EncodeAccepted(submission,
            WithAck(valid, valid.Result.Acknowledgement with { Phases = [] }), Limits));
        var nonblank = new ProjectDefinitionSubmission.Replace(new(submission.Operation, submission.Version,
            new(submission.Request.Command.Expected, submission.Request.Command.Scalars, submission.Request.Command.Phases,
                [new(null, ProjectOptionCategory.Other, "Required option", "")], null)));
        Assert.Throws<ArgumentException>(() => codec.EncodeAccepted(nonblank, valid, Limits));
    }

    [Fact]
    public void Accepted_result_cannot_change_operation_target_or_reuse_original_revision() {
        var submission = Assert.IsType<ProjectDefinitionSubmission.Replace>(Submission(ProjectDefinitionCommandKind.Replace));
        var valid = Accepted(submission);
        var original = submission.Request.Command.Expected;
        Assert.Throws<ArgumentException>(() => codec.EncodeAccepted(submission,
            WithAck(valid, valid.Result.Acknowledgement with { Definition = original }), Limits));
        var other = new ProjectDefinitionOperation(submission.Operation.Namespace, submission.Operation.Epoch,
            Guid.NewGuid(), submission.Operation.CallerBinding);
        var wrongOperation = new ProjectDefinitionAcceptedReceipt.Full(new(other, valid.Result.Version, valid.Result.Kind,
            valid.Result.AcceptedAtUtc, valid.Result.ReplayUntilUtc, valid.Result.Acknowledgement));
        Assert.Throws<ArgumentException>(() => codec.EncodeAccepted(submission, wrongOperation, Limits));
        var changedProject = new ProjectDefinitionExpectation(new(original.Project.DatabaseProfileId,
            Guid.NewGuid(), original.Project.LifetimeId), Guid.NewGuid());
        Assert.Throws<ArgumentException>(() => codec.EncodeAccepted(submission,
            WithAck(valid, valid.Result.Acknowledgement with { Definition = changedProject }), Limits));
    }

    [Fact]
    public void Corrupt_or_unsupported_stored_content_is_refused_without_repairing_the_submission() {
        var submission = Submission(ProjectDefinitionCommandKind.Replace);
        var frozen = codec.Freeze(submission, Limits);
        Assert.Throws<JsonException>(() => codec.ReadSubmission(frozen.Payload + " ", Limits));
        Assert.Throws<JsonException>(() => codec.ReadSubmission(frozen.Payload.Replace("\"kind\":2", "\"kind\":99", StringComparison.Ordinal), Limits));
        var future = new ProjectDefinitionSubmission.Replace(new(submission.Operation, new(2, 1),
            Assert.IsType<ProjectDefinitionSubmission.Replace>(submission).Request.Command));
        Assert.Throws<NotSupportedException>(() => codec.Freeze(future, Limits));
        Assert.Throws<ArgumentException>(() => codec.ReadSubmission(new string('x', Limits.MaxRawRequestBytes + 1), Limits));
    }

    [Fact]
    public void Stored_child_count_is_checked_before_materializing_command_rows() {
        var submission = Assert.IsType<ProjectDefinitionSubmission.Replace>(Submission(ProjectDefinitionCommandKind.Replace));
        var twoPhases = new ProjectDefinitionSubmission.Replace(new(submission.Operation, submission.Version,
            new(submission.Request.Command.Expected, submission.Request.Command.Scalars,
                [submission.Request.Command.Phases[0], new(null, "Second", "", ProjectPhaseStatus.Planned, null, null)], [], null)));
        var frozen = codec.Freeze(twoPhases, Limits);
        var limited = new ProjectDefinitionLimits(1, 1, Limits.MaxFieldUtf8Bytes, Limits.MaxRawRequestBytes,
            Limits.MaxCanonicalCommandBytes, Limits.MaxAcceptedResultBytes);
        Assert.Throws<JsonException>(() => codec.ReadSubmission(frozen.Payload, limited));
    }

    private static ProjectDefinitionSubmission Submission(ProjectDefinitionCommandKind kind) {
        var operation = new ProjectDefinitionOperation(new(Guid.NewGuid()), new(long.MaxValue), Guid.NewGuid(), new("pc1:" + new string('a', 64)));
        var expected = new ProjectDefinitionExpectation(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid());
        var scalars = new ProjectDefinitionScalars("  Draft name  ", "", "", ProjectStatus.Active, "", null);
        ProjectDefinitionPhaseInput[] phases = [new(kind == ProjectDefinitionCommandKind.Create ? null : Guid.NewGuid(),
            "Phase", "", ProjectPhaseStatus.Planned, null, null)];
        ProjectDefinitionOptionInput[] options = [new(null, ProjectOptionCategory.Other, " ", "")];
        return kind switch {
            ProjectDefinitionCommandKind.Create => new ProjectDefinitionSubmission.Create(new(operation, ProjectDefinitionRequestVersion.VersionOne,
                new(scalars, phases, options, null))),
            ProjectDefinitionCommandKind.Replace => new ProjectDefinitionSubmission.Replace(new(operation, ProjectDefinitionRequestVersion.VersionOne,
                new(expected, scalars, phases, options, null))),
            _ => new ProjectDefinitionSubmission.Scalars(new(operation, ProjectDefinitionRequestVersion.VersionOne, new(expected, scalars)))
        };
    }

    private static ProjectDefinitionAcceptedReceipt.Full Accepted(ProjectDefinitionSubmission.Replace submission) {
        var expected = submission.Request.Command.Expected;
        var acceptedAt = new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);
        return new(new(submission.Operation, ProjectDefinitionResultVersion.VersionOne, ProjectDefinitionCommandKind.Replace,
            acceptedAt, acceptedAt.AddDays(7), new(new(expected.Project, Guid.NewGuid()), "Draft name", "", "",
                ProjectStatus.Active, "", null, [new(0, submission.Request.Command.Phases[0].Id!.Value,
                    "Phase", "", ProjectPhaseStatus.Planned, null, null)], [], null)));
    }

    private static ProjectDefinitionAcceptedReceipt.Full WithAck(ProjectDefinitionAcceptedReceipt.Full original, ProjectEditorAcknowledgement acknowledgement)
        => new(new(original.Result.Operation, original.Result.Version, original.Result.Kind,
            original.Result.AcceptedAtUtc, original.Result.ReplayUntilUtc, acknowledgement));
}
