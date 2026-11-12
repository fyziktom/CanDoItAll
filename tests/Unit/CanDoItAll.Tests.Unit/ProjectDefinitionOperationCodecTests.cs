using System.Collections;
using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectDefinitionOperationCodecTests {
    private static readonly ProjectDefinitionLimits Limits = ProjectDefinitionLimits.VersionOneMaximum;
    private static readonly DatasetNamespaceId Namespace = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly Guid OperationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly ProjectMutationCallerScope Caller = ProjectMutationCallerScope.OpenApi;
    private static readonly ProjectDefinitionScalars Scalars = new("Project", "", "", ProjectStatus.Draft, "", null);
    private static readonly DateTimeOffset AcceptedAt = DateTimeOffset.Parse("2026-10-16T12:00:00Z");
    private static readonly DateTimeOffset ReplayUntil = DateTimeOffset.Parse("2026-10-23T12:00:00Z");

    [Fact]
    public void Caller_binding_separates_dataset_mode_and_verified_subject_with_unambiguous_case_sensitive_encoding() {
        var first = ProjectMutationCallerScope.FromVerifiedSubject("issuer", "Subject");
        var rotatedCredential = ProjectMutationCallerScope.FromVerifiedSubject("issuer", "Subject");

        Assert.Equal(first.Bind(Namespace), rotatedCredential.Bind(Namespace));
        Assert.NotEqual(first.Bind(Namespace), ProjectMutationCallerScope.FromVerifiedSubject("issuer", "subject").Bind(Namespace));
        Assert.NotEqual(ProjectMutationCallerScope.FromVerifiedSubject("ab", "c").Key,
            ProjectMutationCallerScope.FromVerifiedSubject("a", "bc").Key);
        Assert.NotEqual(Caller.Bind(Namespace), ProjectMutationCallerScope.LocalWorkspace.Bind(Namespace));
        Assert.NotEqual(Caller.Bind(Namespace), Caller.Bind(new(Guid.Parse("99999999-9999-9999-9999-999999999999"))));
        Assert.True(first.Matches(Namespace, rotatedCredential.Bind(Namespace)));
        Assert.False(first.Matches(Namespace, Caller.Bind(Namespace)));
        Assert.Throws<ArgumentException>(() => ProjectMutationCallerScope.FromVerifiedSubject("issuer", new string('a', 513)));
    }

    [Fact]
    public void Trusted_keys_and_effect_bindings_copy_bytes_and_require_supported_nonempty_governed_identity() {
        var bytes = Enumerable.Repeat((byte)17, 32).ToArray();
        var key = new ProjectCallerKey(bytes);
        var effect = new ProjectDefinitionEffectBinding(ProjectDefinitionEffectKind.Agent, 1, bytes);
        var caller = ProjectMutationCallerScope.FromAdmittedExecution(key);
        bytes[0] = 0;
        key.ToArray()[1] = 0;
        effect.ToArray()[2] = 0;

        Assert.All(key.ToArray(), item => Assert.Equal((byte)17, item));
        Assert.All(effect.ToArray(), item => Assert.Equal((byte)17, item));
        Assert.Equal(ProjectMutationCallerKind.GovernedExecution, caller.Kind);
        Assert.Throws<ArgumentException>(() => new ProjectCallerKey(new byte[31]));
        Assert.Throws<ArgumentException>(() => ProjectMutationCallerScope.FromAdmittedExecution(new(new byte[32])));
        Assert.Throws<ArgumentException>(() => new ProjectDefinitionEffectBinding(ProjectDefinitionEffectKind.Agent, 2, bytes));
        Assert.Throws<ArgumentException>(() => new ProjectDefinitionEffectBinding(ProjectDefinitionEffectKind.None, 1, bytes));
    }

    [Fact]
    public void Normalized_intent_copies_rows_and_never_normalizes_or_refreshes_expectations() {
        var original = Definition();
        var phases = new List<ProjectDefinitionPhaseInput> { new(null, "Phase", "Goal", ProjectPhaseStatus.Planned, null, null) };
        var intent = Intent(original, phases: phases);
        phases.Clear();

        Assert.Single(intent.Phases);
        Assert.Same(original, intent.Expected);
        Assert.Throws<ArgumentException>(() => Intent(scalars: Scalars with { Name = " Project " }));
        Assert.Throws<ArgumentException>(() => Intent(scalars: Scalars with { TargetDateUtc = new DateTime(2026, 10, 16) }));
    }

    [Fact]
    public void Version_one_digest_matches_explicit_canonical_golden_bytes() {
        var operation = Operation();
        var expected = $$$"""{"domain":"projects:definition-intent:v1","schema":1,"normalization":1,"kind":2,"operation":{"namespace":"11111111-1111-1111-1111-111111111111","epoch":1,"id":"22222222-2222-2222-2222-222222222222","callerBinding":"{{{operation.CallerBinding.Value}}}"},"callerKind":2,"callerKey":"{{{Caller.Key.Hex}}}","expected":{"databaseProfileId":"33333333-3333-3333-3333-333333333333","projectId":"44444444-4444-4444-4444-444444444444","lifetimeId":"55555555-5555-5555-5555-555555555555","revision":"66666666-6666-6666-6666-666666666666"},"scalars":{"name":"Project","description":"","objective":"","status":0,"currentPhase":"","targetDateUtc":null},"externalIdentity":null,"phases":[],"options":[],"creation":null,"effect":{"kind":0,"version":1,"digest":"0000000000000000000000000000000000000000000000000000000000000000"}}""";

        var digest = ProjectDefinitionOperationCodec.ComputeDigest(Intent(), operation, Caller, Limits, out var canonicalByteLength);

        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(expected)), digest.ToArray());
        Assert.Equal(Encoding.UTF8.GetByteCount(expected), canonicalByteLength);
    }

    [Fact]
    public void Canonical_length_preserves_escaped_unicode_payload_at_the_exact_byte_boundary() {
        var intent = Intent(scalars: Scalars with { Name = "é\"東京", Description = "Line\n\\value" });
        var digest = ProjectDefinitionOperationCodec.ComputeDigest(intent, Operation(), Caller, Limits, out var canonicalByteLength);

        var exact = ProjectDefinitionOperationCodec.ComputeDigest(intent, Operation(), Caller,
            WithCanonicalLimit(canonicalByteLength), out var exactLength);

        Assert.Equal(digest, exact);
        Assert.Equal(canonicalByteLength, exactLength);
        Assert.Throws<ProjectDefinitionLimitException>(() => ProjectDefinitionOperationCodec.ComputeDigest(intent, Operation(), Caller,
            WithCanonicalLimit(canonicalByteLength - 1), out _));

        static ProjectDefinitionLimits WithCanonicalLimit(int maximumBytes)
            => new(Limits.MaxPhaseCount, Limits.MaxOptionCount, Limits.MaxFieldUtf8Bytes,
                Limits.MaxRawRequestBytes, maximumBytes, Limits.MaxAcceptedResultBytes);
    }

    [Fact]
    public void Equivalent_owner_normalized_values_have_identical_digest_and_limits_are_not_semantic_content() {
        var first = ProjectDefinitionRules.Normalize(Scalars with { Name = "  Project  ", Description = "  " }).Value!;
        var second = ProjectDefinitionRules.Normalize(Scalars).Value!;
        var smallerDeployment = new ProjectDefinitionLimits(1000, 1000, 60_000, 1_900_000, 900_000, 1_900_000);

        var digest = ProjectDefinitionOperationCodec.ComputeDigest(Intent(scalars: first), Operation(), Caller, Limits);
        var same = ProjectDefinitionOperationCodec.ComputeDigest(Intent(scalars: second), Operation(), Caller, smallerDeployment);

        Assert.Equal(digest, same);
    }

    [Fact]
    public void Original_expectation_blank_slot_order_operation_and_effect_binding_change_digest() {
        var option = new ProjectDefinitionOptionInput(null, ProjectOptionCategory.Language, "C#", "");
        var blank = new ProjectDefinitionOptionInput(null, ProjectOptionCategory.Language, "", "");
        var original = Digest(Intent(options: [blank, option]));
        var changedRevision = new ProjectDefinitionExpectation(Definition().Project, Guid.Parse("99999999-9999-9999-9999-999999999999"));

        Assert.NotEqual(original, Digest(Intent(options: [option, blank])));
        Assert.NotEqual(original, Digest(Intent(changedRevision, options: [blank, option])));
        Assert.NotEqual(original, Digest(Intent(options: [blank, option], effect: new(ProjectDefinitionEffectKind.Agent, 1, Enumerable.Repeat((byte)1, 32).ToArray()))));
        Assert.NotEqual(original, ProjectDefinitionOperationCodec.ComputeDigest(Intent(options: [blank, option]),
            new(Namespace, new(2), OperationId, Caller.Bind(Namespace)), Caller, Limits));
    }

    [Fact]
    public void Governed_digest_requires_effect_binding_separately_from_stable_caller_partition() {
        var key = new ProjectCallerKey(Enumerable.Repeat((byte)17, 32).ToArray());
        var caller = ProjectMutationCallerScope.FromAdmittedExecution(key);
        var operation = new ProjectDefinitionOperation(Namespace, new(1), OperationId, caller.Bind(Namespace));
        var first = new ProjectDefinitionEffectBinding(ProjectDefinitionEffectKind.Process, 1, Enumerable.Repeat((byte)1, 32).ToArray());
        var second = new ProjectDefinitionEffectBinding(ProjectDefinitionEffectKind.Process, 1, Enumerable.Repeat((byte)2, 32).ToArray());

        Assert.Throws<ArgumentException>(() => ProjectDefinitionOperationCodec.ComputeDigest(Intent(), operation, caller, Limits));
        Assert.NotEqual(ProjectDefinitionOperationCodec.ComputeDigest(Intent(effect: first), operation, caller, Limits),
            ProjectDefinitionOperationCodec.ComputeDigest(Intent(effect: second), operation, caller, Limits));
        Assert.Throws<ArgumentException>(() => ProjectDefinitionOperationCodec.ComputeDigest(Intent(effect: first), operation, Caller, Limits));
    }

    [Fact]
    public void Creation_digest_binds_supplied_reservation_but_has_no_generated_target_or_child_identity() {
        var parentId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var create = new ProjectDefinitionNormalizedIntent(ProjectDefinitionRequestVersion.VersionOne, ProjectDefinitionCommandKind.Create,
            null, Scalars, [new(null, "Phase", "", ProjectPhaseStatus.Planned, null, null)], [], null, null,
            ProjectDefinitionEffectBinding.None, Limits);
        var parent = new ProjectDefinitionNormalizedIntent(ProjectDefinitionRequestVersion.VersionOne, ProjectDefinitionCommandKind.Create,
            null, Scalars, create.Phases, [], null, new(null, parentId, null), ProjectDefinitionEffectBinding.None, Limits);
        var reservation = new ProjectCreationReservation(OperationId, Definition().Project.DatabaseProfileId,
            Definition().Project.ProjectId, Definition().Project.LifetimeId, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), null, null);
        var reserved = new ProjectDefinitionNormalizedIntent(ProjectDefinitionRequestVersion.VersionOne, ProjectDefinitionCommandKind.Create,
            null, Scalars, create.Phases, [], null, new(reservation.ProjectId, null, reservation), ProjectDefinitionEffectBinding.None, Limits);

        Assert.Null(create.Phases[0].Id);
        Assert.Null(create.Creation);
        Assert.NotEqual(Digest(create), Digest(parent));
        Assert.NotEqual(Digest(create), Digest(reserved));
        Assert.NotEqual(Digest(parent), Digest(reserved));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Full_and_scalar_results_round_trip_exact_canonical_golden_bytes(bool scalar) {
        var operation = Operation();
        var kind = scalar ? ProjectDefinitionCommandKind.UpdateScalars : ProjectDefinitionCommandKind.Replace;
        var full = Full();
        var bytes = scalar ? ProjectDefinitionOperationCodec.EncodeAccepted(new ProjectDefinitionAccepted<ProjectDefinitionScalarsAcknowledgement>(operation,
                ProjectDefinitionResultVersion.VersionOne, kind, AcceptedAt, ReplayUntil, new(Definition(), Scalars, null)), Limits)
            : ProjectDefinitionOperationCodec.EncodeAccepted(full, Limits);
        var children = scalar ? "" : ",\"phases\":[],\"options\":[]";
        var expected = $$$"""{"resultVersion":1,"kind":{{{(int)kind}}},"operation":{"namespace":"11111111-1111-1111-1111-111111111111","epoch":1,"id":"22222222-2222-2222-2222-222222222222","callerBinding":"{{{operation.CallerBinding.Value}}}"},"acceptedAtUtc":"2026-10-16T12:00:00.0000000Z","replayUntilUtc":"2026-10-23T12:00:00.0000000Z","acknowledgement":{"definition":{"databaseProfileId":"33333333-3333-3333-3333-333333333333","projectId":"44444444-4444-4444-4444-444444444444","lifetimeId":"55555555-5555-5555-5555-555555555555","revision":"66666666-6666-6666-6666-666666666666"},"scalars":{"name":"Project","description":"","objective":"","status":0,"currentPhase":"","targetDateUtc":null},"externalIdentity":null{{{children}}}}}""";

        Assert.Equal(expected, Encoding.UTF8.GetString(bytes.ToArray()));
        var decoded = ProjectDefinitionOperationCodec.DecodeAccepted(ProjectDefinitionResultVersion.VersionOne, bytes.ToArray(), Limits);
        var roundTrip = decoded switch {
            ProjectDefinitionDecodedReceipt.Full value => ProjectDefinitionOperationCodec.EncodeAccepted(value.Result, Limits),
            ProjectDefinitionDecodedReceipt.Scalars value => ProjectDefinitionOperationCodec.EncodeAccepted(value.Result, Limits),
            _ => throw new InvalidOperationException("Unexpected decoded receipt.")
        };
        Assert.Equal(bytes.ToArray(), roundTrip.ToArray());
    }

    [Fact]
    public void Stored_full_result_preserves_exact_ids_mapping_holes_and_accepted_values() {
        var phases = new[] { new ProjectPhaseAcknowledgement(0, Guid.Parse("77777777-7777-7777-7777-777777777777"), "Phase", "Goal", ProjectPhaseStatus.Active, null, null) };
        var options = new[] { new ProjectOptionAcknowledgement(2, Guid.Parse("88888888-8888-8888-8888-888888888888"), ProjectOptionCategory.Language, "C#", "Original accepted") };
        var original = Full(phases, options);
        var bytes = ProjectDefinitionOperationCodec.EncodeAccepted(original, Limits);

        var decoded = Assert.IsType<ProjectDefinitionDecodedReceipt.Full>(ProjectDefinitionOperationCodec.DecodeAccepted(
            ProjectDefinitionResultVersion.VersionOne, bytes.ToArray(), Limits)).Result;

        Assert.Equal(original.Operation, decoded.Operation);
        Assert.Equal(original.Acknowledgement.Definition, decoded.Acknowledgement.Definition);
        Assert.Equal(phases, decoded.Acknowledgement.Phases);
        Assert.Equal(options, decoded.Acknowledgement.Options);
        Assert.Equal(AcceptedAt, decoded.AcceptedAtUtc);
    }

    [Fact]
    public void Replayed_outcome_and_caller_mutation_of_returned_bytes_cannot_change_stored_result() {
        var accepted = Full();
        var encoded = ProjectDefinitionOperationCodec.EncodeAccepted(accepted, Limits);
        var replay = new ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted(accepted, true);
        var tamperedCopy = encoded.ToArray();
        tamperedCopy[0] = 0;

        Assert.Equal(encoded.ToArray(), ProjectDefinitionOperationCodec.EncodeAccepted(replay.Result, Limits).ToArray());
        Assert.NotEqual(tamperedCopy, encoded.ToArray());
    }

    [Theory]
    [InlineData("\"resultVersion\":1", "\"resultVersion\":2")]
    [InlineData("\"kind\":2", "\"kind\":19")]
    [InlineData("\"revision\":\"66666666-6666-6666-6666-666666666666\"", "\"revision\":\"00000000-0000-0000-0000-000000000000\"")]
    [InlineData("\"status\":0", "\"status\":98")]
    [InlineData("\"name\":\"Project\"", "\"name\":\" Project \"")]
    [InlineData("\"name\":\"Project\"", "\"name\":\"Project\",\"unknown\":true")]
    public void Decoder_rejects_invalid_noncanonical_or_unsupported_stored_content(string original, string replacement) {
        var bytes = ProjectDefinitionOperationCodec.EncodeAccepted(Full(), Limits).ToArray();
        var corrupt = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace(original, replacement, StringComparison.Ordinal));

        Assert.Throws<ProjectDefinitionReceiptDecodeException>(() => ProjectDefinitionOperationCodec.DecodeAccepted(
            ProjectDefinitionResultVersion.VersionOne, corrupt, Limits));
    }

    [Fact]
    public void Decoder_rejects_duplicate_mapping_and_keeps_request_version_independent_of_result_codec() {
        var phases = new[] { new ProjectPhaseAcknowledgement(0, Guid.Parse("77777777-7777-7777-7777-777777777777"), "Phase", "", ProjectPhaseStatus.Planned, null, null) };
        var accepted = ProjectDefinitionOperationCodec.EncodeAccepted(Full(phases), Limits);
        var corrupt = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(accepted.ToArray()).Replace("\"submittedIndex\":0", "\"submittedIndex\":1", StringComparison.Ordinal));

        Assert.Throws<ProjectDefinitionReceiptDecodeException>(() => ProjectDefinitionOperationCodec.DecodeAccepted(
            ProjectDefinitionResultVersion.VersionOne, corrupt, Limits));
        Assert.Throws<NotSupportedException>(() => ProjectDefinitionOperationCodec.DecodeAccepted(new(2), accepted.ToArray(), Limits));
        Assert.Throws<NotSupportedException>(() => new ProjectDefinitionNormalizedIntent(new(1, 2), ProjectDefinitionCommandKind.Replace,
            Definition(), Scalars, [], [], null, null, ProjectDefinitionEffectBinding.None, Limits));
    }

    [Fact]
    public void Bounds_check_count_before_enumeration_and_aggregate_many_individually_valid_fields_before_copy() {
        Assert.Throws<ProjectDefinitionLimitException>(() => ProjectDefinitionInputBounds.Validate(Scalars,
            new UnreadableList<ProjectDefinitionPhaseInput>(1025), [], null, Limits));
        var goal = new string('x', 40_000);
        var phases = Enumerable.Range(0, 64).Select(_ => new ProjectDefinitionPhaseInput(null, "Phase", goal, ProjectPhaseStatus.Planned, null, null)).ToArray();

        Assert.Throws<ProjectDefinitionLimitException>(() => ProjectDefinitionInputBounds.Validate(Scalars, phases, [], null, Limits));
        Assert.Throws<ProjectDefinitionLimitException>(() => Intent(phases: phases));
    }

    [Fact]
    public void Bounds_count_utf8_bytes_and_reject_malformed_utf16_explicitly() {
        Assert.Throws<ProjectDefinitionLimitException>(() => ProjectDefinitionInputBounds.Validate(
            Scalars with { Description = new string('\u00e9', 32_769) }, [], [], null, Limits));
        Assert.Throws<ProjectDefinitionInputException>(() => ProjectDefinitionInputBounds.Validate(
            Scalars with { Description = "\ud800" }, [], [], null, Limits));
    }

    [Fact]
    public void Version_one_limits_preserve_the_existing_thousand_option_shape_without_truncation() {
        var options = Enumerable.Range(0, 1000).Select(index => new ProjectDefinitionOptionInput(null,
            ProjectOptionCategory.Language, $"Option {index}", "Notes")).ToArray();
        var intent = Intent(options: options);

        Assert.Equal(1000, intent.Options.Count);
        Assert.Equal(options[^1], intent.Options[^1]);
        Assert.Equal(32, Digest(intent).ToArray().Length);
    }

    [Fact]
    public void Canonical_command_and_result_bounds_reject_before_returning_partial_bytes() {
        var limits = new ProjectDefinitionLimits(1024, 1024, 65_536, 2_097_152, 128, 128);

        Assert.Throws<ProjectDefinitionLimitException>(() => ProjectDefinitionOperationCodec.ComputeDigest(Intent(), Operation(), Caller, limits));
        Assert.Throws<ProjectDefinitionLimitException>(() => ProjectDefinitionOperationCodec.EncodeAccepted(Full(), limits));
        Assert.Throws<ProjectDefinitionReceiptDecodeException>(() => ProjectDefinitionOperationCodec.DecodeAccepted(
            ProjectDefinitionResultVersion.VersionOne, new byte[129], limits));
    }

    [Fact]
    public void Issued_version_hard_limits_can_decode_accepted_content_after_deployment_limits_tighten() {
        var full = Full();
        var bytes = ProjectDefinitionOperationCodec.EncodeAccepted(full, Limits);
        var tightened = new ProjectDefinitionLimits(100, 100, 512, 1024, 1024, 128);

        Assert.Throws<ProjectDefinitionReceiptDecodeException>(() => ProjectDefinitionOperationCodec.DecodeAccepted(
            ProjectDefinitionResultVersion.VersionOne, bytes.ToArray(), tightened));
        Assert.IsType<ProjectDefinitionDecodedReceipt.Full>(ProjectDefinitionOperationCodec.DecodeAccepted(
            ProjectDefinitionResultVersion.VersionOne, bytes.ToArray(), ProjectDefinitionLimits.VersionOneMaximum));
    }

    private static ProjectDefinitionOperation Operation() => new(Namespace, new(1), OperationId, Caller.Bind(Namespace));

    private static ProjectDefinitionExpectation Definition()
        => new(new(Guid.Parse("33333333-3333-3333-3333-333333333333"), Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Guid.Parse("55555555-5555-5555-5555-555555555555")), Guid.Parse("66666666-6666-6666-6666-666666666666"));

    private static ProjectDefinitionNormalizedIntent Intent(ProjectDefinitionExpectation? expected = null,
        ProjectDefinitionScalars? scalars = null, IReadOnlyList<ProjectDefinitionPhaseInput>? phases = null,
        IReadOnlyList<ProjectDefinitionOptionInput>? options = null, ProjectDefinitionEffectBinding? effect = null)
        => new(ProjectDefinitionRequestVersion.VersionOne, ProjectDefinitionCommandKind.Replace, expected ?? Definition(),
            scalars ?? Scalars, phases ?? [], options ?? [], null, null, effect ?? ProjectDefinitionEffectBinding.None, Limits);

    private static ProjectDefinitionDigest Digest(ProjectDefinitionNormalizedIntent intent)
        => ProjectDefinitionOperationCodec.ComputeDigest(intent, Operation(), Caller, Limits);

    private static ProjectDefinitionAccepted<ProjectEditorAcknowledgement> Full(IReadOnlyList<ProjectPhaseAcknowledgement>? phases = null,
        IReadOnlyList<ProjectOptionAcknowledgement>? options = null)
        => new(Operation(), ProjectDefinitionResultVersion.VersionOne, ProjectDefinitionCommandKind.Replace, AcceptedAt, ReplayUntil,
            new(Definition(), Scalars.Name, Scalars.Description, Scalars.Objective, Scalars.Status, Scalars.CurrentPhase,
                Scalars.TargetDateUtc, phases ?? [], options ?? [], null));

    private sealed class UnreadableList<T>(int count) : IReadOnlyList<T> {
        public int Count => count;
        public T this[int index] => throw new InvalidOperationException("The count must be rejected before reading any item.");
        public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException("The count must be rejected before enumerating.");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
