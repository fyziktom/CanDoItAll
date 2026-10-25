using System.Text;
using CanDoItAll.Infrastructure;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectDeletionRecoveryCodecTests {
    [Fact]
    public void Legacy_release_requires_explicit_host_attestation_and_the_installation_source() {
        var original = Original();
        var draft = new ProjectDeletionOriginEvidence.LegacyInstallation(new string('a', 64));
        Assert.Throws<NotSupportedException>(() => ProjectDeletionReleasePersistence.Origin(draft));
        var attested = new ProjectDeletionOriginEvidence.LegacyInstallation(draft.InstallationPlanDigest,
            ProjectDeletionLegacyPrecondition.AllWriterAndFormerSourceOwnerHostsStopped);
        Assert.Equal(ProjectDeletionCompletionOrigin.LegacyInstallation, ProjectDeletionReleasePersistence.Origin(attested));
        Assert.Throws<ArgumentException>(() => new ProjectDeletionRecoveryEvidence(original, attested,
            new ProjectDeletionSourceRecovery.NoExternalSourceLease(), [original.Project], []));
        var source = new ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog(original.Project.DatabaseProfileId,
            HostBoundPathPolicy.BindCurrent(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "original-catalog")), DateTimeOffset.UtcNow));
        var evidence = new ProjectDeletionRecoveryEvidence(original, attested, source, [original.Project], []);
        var bytes = ProjectDeletionRecoveryCodec.Write(evidence);
        Assert.Equal(attested, ProjectDeletionRecoveryCodec.Read(bytes, original).Origin);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectDeletionOriginEvidence.LegacyInstallation(draft.InstallationPlanDigest,
            (ProjectDeletionLegacyPrecondition)999));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Complete_preparation_registration_is_retained_even_without_cleanup_obligations(bool empty) {
        var original = Original();
        var origin = new ProjectDeletionOriginEvidence.LegacyInstallation(new string('b', 64));
        ProjectDeletionPreparationRegistration[] participants = empty ? [] : [new(new("no-work-participant"), 7)];
        var evidence = new ProjectDeletionRecoveryEvidence(original, origin,
            new ProjectDeletionSourceRecovery.NoExternalSourceLease(), [original.Project], participants);
        byte[] encoded = ProjectDeletionRecoveryCodec.Write(evidence);
        var recovered = ProjectDeletionRecoveryCodec.Read(encoded, original);
        Assert.Equal(participants, recovered.RequirePreparationParticipants());
        Assert.Equal(encoded, ProjectDeletionRecoveryCodec.Write(recovered));
        if (!empty) {
            string changed = Encoding.UTF8.GetString(encoded).Replace("\"protocol\":7", "\"protocol\":0", StringComparison.Ordinal);
            Assert.Throws<InvalidDataException>(() => ProjectDeletionRecoveryCodec.Read(Encoding.UTF8.GetBytes(changed), original));
            Assert.Throws<ArgumentException>(() => new ProjectDeletionRecoveryEvidence(original, origin,
                evidence.Source, evidence.RequiredProjects, [participants[0], participants[0]]));
        }
        var old = new ProjectDeletionRecoveryEvidence(original, origin, evidence.Source, evidence.RequiredProjects);
        byte[] legacy = ProjectDeletionRecoveryCodec.Write(old);
        var retained = ProjectDeletionRecoveryCodec.Read(legacy, original);
        Assert.Equal(legacy, ProjectDeletionRecoveryCodec.Write(retained));
        Assert.Throws<NotSupportedException>(() => retained.RequirePreparationParticipants());
    }

    [Fact]
    public void Ordinary_origin_round_trip_retains_original_operation_revision_and_caller() {
        var original = Original();
        var submission = new ProjectLifecycleSubmission(new(original.Project.DatabaseProfileId, original.Completion.Namespace,
            new(7), new("pc1:" + new string('a', 64)), Guid.NewGuid()), ProjectLifecycleInterpretation.VersionOne,
            new ProjectLifecycleCommand.Delete(new(original.Project, Guid.NewGuid())));
        var evidence = new ProjectDeletionRecoveryEvidence(original, new ProjectDeletionOriginEvidence.Ordinary(submission),
            new ProjectDeletionSourceRecovery.NoExternalSourceLease(), [original.Project]);

        var encoded = ProjectDeletionRecoveryCodec.Write(evidence);
        var decoded = ProjectDeletionRecoveryCodec.Read(encoded, original);
        var ordinary = Assert.IsType<ProjectDeletionOriginEvidence.Ordinary>(decoded.Origin);
        Assert.Equal(submission, ordinary.Submission);
        Assert.Null(ordinary.CallerExclusion);
        Assert.Throws<NotSupportedException>(() => ordinary.RequireOperationKey());
        Assert.Equal(encoded, ProjectDeletionRecoveryCodec.Write(decoded));
        var another = new ProjectDeletionPreparationContext(new(original.Project.DatabaseProfileId,
            original.Completion.Namespace, new(Guid.NewGuid())), original.Project);
        Assert.Throws<InvalidDataException>(() => ProjectDeletionRecoveryCodec.Read(encoded, another));
    }

    [Theory]
    [InlineData(ProjectMutationCallerKind.AuthenticatedSubject)]
    [InlineData(ProjectMutationCallerKind.OpenApi)]
    [InlineData(ProjectMutationCallerKind.LocalWorkspace)]
    [InlineData(ProjectMutationCallerKind.GovernedExecution)]
    public void Ordinary_caller_exclusion_retains_the_exact_lock_and_refuses_changed_kind_key_or_binding(ProjectMutationCallerKind kind) {
        var caller = kind switch {
            ProjectMutationCallerKind.AuthenticatedSubject => ProjectMutationCallerScope.FromVerifiedSubject("fixture-issuer", "fixture-subject"),
            ProjectMutationCallerKind.OpenApi => ProjectMutationCallerScope.OpenApi,
            ProjectMutationCallerKind.LocalWorkspace => ProjectMutationCallerScope.LocalWorkspace,
            ProjectMutationCallerKind.GovernedExecution => ProjectMutationCallerScope.FromAdmittedExecution(new(Enumerable.Repeat((byte)0x7a, 32).ToArray())),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var original = Original();
        var operation = new ProjectLifecycleOperation(original.Project.DatabaseProfileId, original.Completion.Namespace,
            new(7), caller.Bind(original.Completion.Namespace), Guid.NewGuid());
        var submission = new ProjectLifecycleSubmission(operation, ProjectLifecycleInterpretation.VersionOne,
            new ProjectLifecycleCommand.Delete(new(original.Project, Guid.NewGuid())));
        var origin = new ProjectDeletionOriginEvidence.Ordinary(submission, new(caller.Kind, caller.Key));
        var evidence = new ProjectDeletionRecoveryEvidence(original, origin,
            new ProjectDeletionSourceRecovery.NoExternalSourceLease(), [original.Project]);
        var encoded = ProjectDeletionRecoveryCodec.Write(evidence);
        var recovered = Assert.IsType<ProjectDeletionOriginEvidence.Ordinary>(ProjectDeletionRecoveryCodec.Read(encoded, original).Origin);
        Assert.Equal(caller.Kind, recovered.CallerExclusion!.Kind);
        Assert.Equal(caller.Key, recovered.CallerExclusion.Key);
        Assert.Equal(FormattableString.Invariant(
            $"projects:lifecycle-operation:{operation.Namespace.Value:D}:7:{(int)kind}:{caller.Key.Hex}:{operation.Id:D}"), recovered.RequireOperationKey());
        Assert.Equal(ProjectMutationScopeKeys.ForLifecycleOperation(operation, caller.Kind, caller.Key), recovered.RequireOperationKey());
        Assert.Equal(encoded, ProjectDeletionRecoveryCodec.Write(new(original, recovered, evidence.Source, evidence.RequiredProjects)));
        var text = Encoding.UTF8.GetString(encoded);
        Assert.Throws<InvalidDataException>(() => ProjectDeletionRecoveryCodec.Read(Encoding.UTF8.GetBytes(
            text.Replace($"\"key\":\"{caller.Key.Hex}\"", "\"key\":\"00\"", StringComparison.Ordinal)), original));
        Assert.Throws<InvalidDataException>(() => ProjectDeletionRecoveryCodec.Read(Encoding.UTF8.GetBytes(
            text.Replace($"\"kind\":{(int)kind},\"key\"", "\"kind\":99,\"key\"", StringComparison.Ordinal)), original));
        var another = new ProjectLifecycleSubmission(new(operation.DatabaseProfileId, operation.Namespace, operation.Epoch,
            new("pc1:" + new string('f', 64)), operation.Id), submission.Interpretation, submission.Command);
        Assert.Throws<ArgumentException>(() => new ProjectDeletionOriginEvidence.Ordinary(another, recovered.CallerExclusion));
    }

    [Fact]
    public void Compensation_requires_exact_original_reservation_parent_and_fingerprint() {
        var original = Original();
        var parent = new ProjectWriteAdmission(original.Project.DatabaseProfileId, Guid.NewGuid(), Guid.NewGuid());
        var reservation = new ProjectCreationReservation(Guid.NewGuid(), original.Project.DatabaseProfileId,
            original.Project.ProjectId, original.Project.LifetimeId, Guid.NewGuid(), parent.ProjectId, parent.LifetimeId);
        var origin = new ProjectDeletionOriginEvidence.Compensation(original.Project, new string('b', 64), reservation);
        Assert.Throws<ArgumentException>(() => new ProjectDeletionRecoveryEvidence(original, origin,
            new ProjectDeletionSourceRecovery.NoExternalSourceLease(), [original.Project]));
        var evidence = new ProjectDeletionRecoveryEvidence(original, origin,
            new ProjectDeletionSourceRecovery.NoExternalSourceLease(), [parent, original.Project]);

        var decoded = ProjectDeletionRecoveryCodec.Read(ProjectDeletionRecoveryCodec.Write(evidence), original);
        var compensation = Assert.IsType<ProjectDeletionOriginEvidence.Compensation>(decoded.Origin);
        Assert.Equal(reservation, compensation.Reservation);
        Assert.Equal(origin.Fingerprint, compensation.Fingerprint);
        Assert.Throws<ArgumentException>(() => new ProjectDeletionOriginEvidence.Compensation(original.Project,
            origin.Fingerprint, reservation with { LifetimeId = Guid.NewGuid() }));
    }

    [Fact]
    public void Retained_unicode_source_is_exact_and_unknown_or_noncanonical_json_is_refused() {
        var original = Original();
        var path = HostBoundPathPolicy.BindCurrent(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "deletion-source-🧭")),
            DateTimeOffset.UtcNow);
        var source = new ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog(original.Project.DatabaseProfileId, path);
        var evidence = new ProjectDeletionRecoveryEvidence(original, new ProjectDeletionOriginEvidence.LegacyInstallation(new string('c', 64)),
            source, [original.Project]);
        var encoded = ProjectDeletionRecoveryCodec.Write(evidence);

        Assert.Equal(source, ProjectDeletionRecoveryCodec.Read(encoded, original).Source);
        var text = Encoding.UTF8.GetString(encoded);
        Assert.Throws<InvalidDataException>(() => ProjectDeletionRecoveryCodec.Read(Encoding.UTF8.GetBytes(text + " "), original));
        Assert.Throws<InvalidDataException>(() => ProjectDeletionRecoveryCodec.Read(Encoding.UTF8.GetBytes(
            text.Replace("\"version\":1", "\"version\":2", StringComparison.Ordinal)), original));
        Assert.Throws<InvalidDataException>(() => ProjectDeletionRecoveryCodec.Read(Encoding.UTF8.GetBytes(
            text.Insert(1, "\"unexpected\":0,")), original));
    }

    [Fact]
    public void Project_text_and_complete_encoded_bounds_refuse_without_truncating_original_evidence() {
        var original = Original();
        var projects = Enumerable.Range(0, ProjectDeletionRecoveryEvidence.MaximumProjects - 1)
            .Select(_ => new ProjectWriteAdmission(original.Project.DatabaseProfileId, Guid.NewGuid(), Guid.NewGuid()))
            .Append(original.Project).ToArray();
        var origin = new ProjectDeletionOriginEvidence.LegacyInstallation(new string('d', 64));
        var source = new ProjectDeletionSourceRecovery.NoExternalSourceLease();
        var evidence = new ProjectDeletionRecoveryEvidence(original, origin, source, projects);
        var encoded = ProjectDeletionRecoveryCodec.Write(evidence);
        Assert.Equal(projects.Length, ProjectDeletionRecoveryCodec.Read(encoded, original).RequiredProjects.Length);
        Assert.Throws<ArgumentException>(() => new ProjectDeletionRecoveryEvidence(original, origin, source,
            projects.Append(new(original.Project.DatabaseProfileId, Guid.NewGuid(), Guid.NewGuid())).ToArray()));
        Assert.Throws<InvalidDataException>(() => ProjectDeletionRecoveryCodec.Read(
            new byte[ProjectDeletionRecoveryCodec.MaximumEncodedBytes + 1], original));
        var path = HostBoundPathPolicy.BindCurrent(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "deletion-source-bound")), DateTimeOffset.UtcNow);
        path.HostBindingId = new string('x', ProjectDeletionRecoveryEvidence.MaximumSourceTextBytes + 1);
        var oversizedSource = new ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog(original.Project.DatabaseProfileId, path);
        Assert.Throws<ArgumentException>(() => new ProjectDeletionRecoveryEvidence(original, origin, oversizedSource, [original.Project]));
    }

    private static ProjectDeletionPreparationContext Original() {
        var profile = Guid.NewGuid();
        return new(new(profile, new DatasetNamespaceId(Guid.NewGuid()), new(Guid.NewGuid())),
            new(profile, Guid.NewGuid(), Guid.NewGuid()));
    }

    [Theory]
    [InlineData(ProjectDeletionExecutionWorkspaceKind.Sandbox, "")]
    [InlineData(ProjectDeletionExecutionWorkspaceKind.Project, "original-project")]
    public void Invocation_retains_both_original_locations_and_refuses_malformed_scope(ProjectDeletionExecutionWorkspaceKind kind, string key) {
        var original = Original();
        var catalogPath = HostBoundPathPolicy.BindCurrent(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "original-catalog")), DateTimeOffset.UtcNow);
        var executionPath = HostBoundPathPolicy.BindCurrent(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "original-execution")), DateTimeOffset.UtcNow);
        var source = new ProjectDeletionSourceRecovery.CanonicalInvocation(new(original.Project.DatabaseProfileId, catalogPath),
            new(executionPath), kind, key);
        var originalCatalog = source.Catalog.RootPath;
        var originalExecution = source.Execution.RootPath;
        catalogPath.Path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "replacement"));
        executionPath.Path = catalogPath.Path;
        source.Execution.ToPathRecord().Path = catalogPath.Path;
        var evidence = new ProjectDeletionRecoveryEvidence(original, new ProjectDeletionOriginEvidence.LegacyInstallation(new string('a', 64)),
            source, [original.Project]);
        var bytes = ProjectDeletionRecoveryCodec.Write(evidence);
        var restored = Assert.IsType<ProjectDeletionSourceRecovery.CanonicalInvocation>(ProjectDeletionRecoveryCodec.Read(bytes, original).Source);
        Assert.Equal(source, restored);
        Assert.Equal(originalCatalog, restored.Catalog.RootPath);
        Assert.Equal(originalExecution, restored.Execution.RootPath);
        Assert.Equal(bytes, ProjectDeletionRecoveryCodec.Write(ProjectDeletionRecoveryCodec.Read(bytes, original)));
        var encoded = Encoding.UTF8.GetString(bytes);
        Assert.Throws<InvalidDataException>(() => ProjectDeletionRecoveryCodec.Read(Encoding.UTF8.GetBytes(
            encoded.Replace($"\"scope\":{(int)kind}", "\"scope\":99", StringComparison.Ordinal)), original));
        Assert.Throws<ArgumentException>(() => new ProjectDeletionSourceRecovery.CanonicalInvocation(source.Catalog,
            source.Execution, kind, "../replacement"));
        Assert.Throws<ArgumentException>(() => new ProjectDeletionSourceRecovery.CanonicalInvocation(source.Catalog,
            source.Execution, ProjectDeletionExecutionWorkspaceKind.Project, ""));
        Assert.Throws<ArgumentException>(() => new ProjectDeletionSourceRecovery.CanonicalInvocation(source.Catalog,
            source.Execution, kind, new string('a', 257)));
    }
}
