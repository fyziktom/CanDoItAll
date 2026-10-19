using System.Text;
using System.Text.Json.Nodes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectTaskPlanOperationContractTests {
    private static readonly DateTimeOffset Instant = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Caller_proposal_and_acknowledgement_capture_original_values_without_list_or_byte_aliases() {
        var key = Enumerable.Repeat((byte)7, 32).ToArray();
        var caller = new ProjectTaskPlanCaller(ProjectTaskPlanCallerKind.OrdinaryAgent, key);
        var command = Command(caller: caller);
        var binding = caller.Bind(command.Operation.Namespace);
        Array.Fill(key, (byte)9);
        Assert.Equal(binding, caller.Bind(command.Operation.Namespace));
        var source = new List<ProjectTaskPlanDraftItem> { new(" Task ", " Description ", " Notes ", 2) };
        var proposal = ProjectTaskPlanProposal.Normalize(new(1, source));
        source.Clear();
        Assert.Equal("Task", Assert.Single(proposal.Tasks).Title);
        var accepted = Accept(command);
        var tasks = accepted.Tasks.ToList();
        var copied = new ProjectTaskPlanAccepted(accepted.Operation, accepted.OriginalDefinition, accepted.Source,
            accepted.SemanticDigest, accepted.AcceptedAtUtc, accepted.ReplayUntilUtc, tasks);
        tasks.Clear();
        Assert.Equal(2, copied.Tasks.Count);
    }

    [Fact]
    public void Empty_identity_unknown_kind_version_and_noncanonical_digest_are_refused() {
        var command = Command();
        Assert.Throws<ArgumentException>(() => new ProjectTaskPlanOperation(command.Operation.Namespace, 0, Guid.NewGuid(), new('a', 64)));
        Assert.Throws<ArgumentException>(() => new ProjectTaskPlanOperation(command.Operation.Namespace, 1, Guid.Empty, new('a', 64)));
        Assert.Throws<ArgumentException>(() => new ProjectTaskPlanOperation(command.Operation.Namespace, 1, Guid.NewGuid(), new('A', 64)));
        Assert.Throws<ArgumentException>(() => new ProjectTaskPlanCaller((ProjectTaskPlanCallerKind)2, new byte[32]));
        Assert.Throws<ArgumentException>(() => new ProjectTaskPlanCaller(ProjectTaskPlanCallerKind.OrdinaryAgent, new byte[31]));
        Assert.Throws<ArgumentException>(() => new ProjectTaskPlanSource(Guid.NewGuid(), 0, Instant, ProjectTaskPlanSourceKind.OrdinaryAgentDecision, 1, new('b', 64)));
        Assert.Throws<ArgumentException>(() => new ProjectTaskPlanSource(Guid.NewGuid(), 1, default, ProjectTaskPlanSourceKind.OrdinaryAgentDecision, 1, new('b', 64)));
        Assert.Throws<ArgumentException>(() => new ProjectTaskPlanSource(Guid.NewGuid(), 1, Instant.ToOffset(TimeSpan.FromHours(1)), ProjectTaskPlanSourceKind.OrdinaryAgentDecision, 1, new('b', 64)));
        Assert.Throws<ArgumentException>(() => new ProjectTaskPlanCommand(command.Operation, command.Basis, command.Proposal,
            command.Policy, command.Source, schemaVersion: 2));
    }

    [Fact]
    public void Semantic_digest_includes_original_operation_basis_policy_source_and_proposal_order() {
        var command = Command();
        var original = ProjectTaskPlanOperationCodec.Digest(command);
        var alternatives = new[] {
            new ProjectTaskPlanCommand(new(command.Operation.Namespace, 1, Guid.NewGuid(), command.Operation.CallerBinding), command.Basis,
                command.Proposal, command.Policy, command.Source),
            new ProjectTaskPlanCommand(command.Operation, command.Basis, ProjectTaskPlanProposal.Normalize(new(1,
                [new("Second", "", "", 1), new("First", "", "", 1)])), command.Policy, command.Source),
            new ProjectTaskPlanCommand(command.Operation, command.Basis, command.Proposal,
                DecisionDataPolicy.PlanningDefault(TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(2)), command.Source),
            new ProjectTaskPlanCommand(command.Operation, command.Basis, command.Proposal, command.Policy,
                new(command.Source.DecisionId, 2, Instant, command.Source.Kind, 1, command.Source.Fingerprint)),
            new ProjectTaskPlanCommand(command.Operation, new(command.Basis.Namespace,
                new(new(command.Basis.Project.Expectation.Project, Guid.NewGuid()), command.Basis.Project.Scalars), Instant, []),
                command.Proposal, command.Policy, command.Source)
        };
        Assert.All(alternatives, changed => Assert.NotEqual(original, ProjectTaskPlanOperationCodec.Digest(changed)));
        var equivalent = new ProjectTaskPlanCommand(command.Operation, command.Basis,
            ProjectTaskPlanProposal.Normalize(new(1, [new(" First ", " ", " ", 1), new(" Second ", "", "", 1)])), command.Policy, command.Source);
        Assert.Equal(original, ProjectTaskPlanOperationCodec.Digest(equivalent));
    }

    [Fact]
    public void Original_capture_time_is_intent_identity_but_not_material_change_history() {
        var command = Command();
        var newer = new ProjectTaskPlanBasis(command.Basis.Namespace, command.Basis.Project, Instant.AddSeconds(1), []);
        Assert.True(ProjectTaskPlanBasisCodec.MaterialEquals(command.Basis, newer));
        Assert.NotEqual(ProjectTaskPlanOperationCodec.Digest(command), ProjectTaskPlanOperationCodec.Digest(
            new(command.Operation, newer, command.Proposal, command.Policy, command.Source)));
    }

    [Fact]
    public void Fixed_pool_registration_snapshots_timing_and_never_aliases_projects_or_accepts_unbounded_deadlines() {
        var options = new ProjectTaskPlanOperationOptions();
        var registration = WorkbenchTaskPlanFiniteOperations.Create(options);
        options.ReplayRetention = TimeSpan.FromDays(1);
        Assert.Equal(TimeSpan.FromDays(7), registration.Timing.ReplayRetention);
        Assert.NotEqual(ProjectFiniteOperationPool.Pool, registration.Pool);
        Assert.NotEqual(ProjectFiniteOperationPool.DefinitionFamily, Assert.Single(registration.Families).Family);
        foreach (var invalid in new[] {
            new ProjectTaskPlanOperationOptions { MaintenanceInterval = TimeSpan.FromSeconds(4) },
            new ProjectTaskPlanOperationOptions { MutationDeadline = TimeSpan.FromMinutes(2) },
            new ProjectTaskPlanOperationOptions { ReplayRetention = TimeSpan.Zero }
        }) {
            Assert.False(invalid.IsValid());
            Assert.Throws<ArgumentOutOfRangeException>(() => WorkbenchTaskPlanFiniteOperations.Create(invalid));
        }
    }

    [Fact]
    public void Accepted_codec_retains_exact_ordered_ids_defaults_and_bytes_independent_of_later_rows() {
        var command = Command();
        var accepted = Accept(command);
        var encoded = ProjectTaskPlanOperationCodec.EncodeAccepted(accepted);
        var decoded = ProjectTaskPlanOperationCodec.DecodeAccepted(encoded);
        Assert.Equal(accepted.Tasks, decoded.Tasks);
        Assert.Equal(accepted.Source, decoded.Source);
        Assert.Equal(accepted.OriginalDefinition, decoded.OriginalDefinition);
        Assert.Equal(encoded, ProjectTaskPlanOperationCodec.EncodeAccepted(decoded));
        Assert.Equal(new[] { 0, 1 }, decoded.Tasks.Select(task => task.Ordinal));
        Assert.All(decoded.Tasks, task => {
            Assert.Null(task.Fact.StartUtc);
            Assert.Null(task.Fact.DueUtc);
            Assert.Equal(ProjectTaskExecutionState.NotStarted, task.Fact.Execution.State);
        });
    }

    [Fact]
    public void Actual_receipt_payload_and_metadata_are_bounded_before_they_can_be_replayed() {
        Assert.Throws<ProjectTaskPlanReceiptDecodeException>(() => ProjectTaskPlanOperationCodec.DecodeAccepted(
            new byte[ProjectTaskPlanReceiptRecord.MaximumPayloadBytes + 1]));
        Assert.Throws<ProjectTaskPlanReceiptDecodeException>(() => ProjectTaskPlanOperationCodec.DecodeAccepted(ReadOnlyMemory<byte>.Empty));
        Assert.Throws<ProjectTaskPlanReceiptDecodeException>(() => ProjectTaskPlanOperationCodec.DecodeAccepted(new byte[] { 0xff }));
        var accepted = Accept(Command());
        var tasks = accepted.Tasks.ToArray();
        tasks[0] = tasks[0] with { MetadataJson = new('a', ProjectTaskPlanBasisLimits.MaximumFieldBytes + 1) };
        Assert.Throws<ProjectTaskPlanBasisException>(() => new ProjectTaskPlanAccepted(accepted.Operation, accepted.OriginalDefinition,
            accepted.Source, accepted.SemanticDigest, accepted.AcceptedAtUtc, accepted.ReplayUntilUtc, tasks));
        Assert.Throws<ArgumentException>(() => new ProjectTaskPlanAccepted(accepted.Operation, accepted.OriginalDefinition,
            accepted.Source, accepted.SemanticDigest, accepted.AcceptedAtUtc, accepted.ReplayUntilUtc, []));
        tasks = accepted.Tasks.ToArray();
        tasks[0] = tasks[0] with { Fact = tasks[0].Fact with { Execution = new(ProjectTaskExecutionState.Started, null, null) } };
        Assert.Throws<ProjectTaskPlanBasisException>(() => new ProjectTaskPlanAccepted(accepted.Operation, accepted.OriginalDefinition,
            accepted.Source, accepted.SemanticDigest, accepted.AcceptedAtUtc, accepted.ReplayUntilUtc, tasks));
    }

    [Fact]
    public void Duplicate_unknown_missing_non_utc_and_inconsistent_retained_fields_never_become_acceptance() {
        var encoded = Encoding.UTF8.GetString(ProjectTaskPlanOperationCodec.EncodeAccepted(Accept(Command())));
        var malformed = new[] {
            encoded.Insert(1, "\"version\":1,"), encoded.Insert(1, "\"unknown\":1,"),
            encoded.Replace("\"version\":1", "\"version\":3", StringComparison.Ordinal),
            encoded.Replace("2026-10-16T12:00:00+00:00", "2026-10-16T13:00:00+01:00", StringComparison.Ordinal)
        };
        foreach (var payload in malformed) {
            Assert.NotEqual(encoded, payload);
            Assert.Throws<ProjectTaskPlanReceiptDecodeException>(() => ProjectTaskPlanOperationCodec.DecodeAccepted(Encoding.UTF8.GetBytes(payload)));
        }
        var tree = JsonNode.Parse(encoded)!;
        tree["tasks"]![0]!["ordinal"] = 1;
        Assert.Throws<ProjectTaskPlanReceiptDecodeException>(() => ProjectTaskPlanOperationCodec.DecodeAccepted(Encoding.UTF8.GetBytes(tree.ToJsonString())));
        tree = JsonNode.Parse(encoded)!;
        tree.AsObject().Remove("source");
        Assert.Throws<ProjectTaskPlanReceiptDecodeException>(() => ProjectTaskPlanOperationCodec.DecodeAccepted(Encoding.UTF8.GetBytes(tree.ToJsonString())));
    }

    [Fact]
    public void Stored_result_requires_exact_indexed_identity_source_digest_and_issued_horizon() {
        var caller = Caller();
        var command = Command(caller: caller);
        var accepted = Accept(command);
        var receipt = ProjectTaskPlanOperationProtocol.CreateReceipt(accepted, caller);
        Assert.Equal(accepted.Tasks, ProjectTaskPlanOperationProtocol.Decode(receipt, command.Operation, caller, accepted.ReplayUntilUtc).Tasks);
        Action<ProjectTaskPlanReceiptRecord>[] corruptions = [
            row => row.DecisionGeneration++, row => row.OriginalDefinitionRevision = Guid.NewGuid(),
            row => row.SemanticDigest[0] ^= 1, row => row.SourceFingerprint[0] ^= 1, row => row.OperationId = Guid.NewGuid()
        ];
        foreach (var corrupt in corruptions) {
            var row = ProjectTaskPlanOperationProtocol.CreateReceipt(accepted, caller);
            corrupt(row);
            Assert.Throws<ProjectTaskPlanReceiptDecodeException>(() => ProjectTaskPlanOperationProtocol.Decode(row, command.Operation, caller, accepted.ReplayUntilUtc));
        }
        Assert.Throws<ProjectTaskPlanReceiptDecodeException>(() => ProjectTaskPlanOperationProtocol.Decode(receipt,
            command.Operation, caller, accepted.ReplayUntilUtc.AddSeconds(1)));
    }

    [Fact]
    public void Outcome_knowledge_cannot_label_commit_uncertainty_as_a_definite_rejection() {
        var command = Command();
        Assert.Throws<ArgumentException>(() => new ProjectTaskPlanOutcome.Rejected(command.Operation, ProjectTaskPlanRefusal.CommitUnconfirmed));
        Assert.Throws<ArgumentException>(() => new ProjectTaskPlanOutcome.Rejected(command.Operation, ProjectTaskPlanRefusal.Unavailable, true));
        Assert.Throws<ArgumentException>(() => new ProjectTaskPlanOutcome.Unconfirmed(command.Operation, ProjectTaskPlanRefusal.BasisChanged));
        Assert.Throws<ArgumentNullException>(() => new ProjectTaskPlanOutcome.Accepted(null!, false));
        var accepted = Accept(command);
        Assert.Same(accepted, new ProjectTaskPlanOutcome.Accepted(accepted, false, true).Acknowledgement);
        Assert.True(new ProjectTaskPlanOutcome.Rejected(command.Operation, ProjectTaskPlanRefusal.BasisChanged, true).RequiresReview);
    }

    [Theory]
    [InlineData(ProjectTaskPlanSource.WallTimeVersion)]
    [InlineData(ProjectTaskPlanSource.ActiveWorkVersion)]
    public void Each_supported_interpretation_preserves_exact_receipt_fields_and_encoded_payload(int version) {
        var caller = Caller();
        var command = Command(caller, version);
        var accepted = Accept(command);
        var receipt = ProjectTaskPlanOperationProtocol.CreateReceipt(accepted, caller);
        Assert.Equal(version, receipt.SchemaVersion);
        Assert.Equal(version, receipt.SourceVersion);
        Assert.Equal(version, receipt.ResultVersion);
        Assert.Equal(1, receipt.NormalizationVersion);
        var decoded = ProjectTaskPlanOperationProtocol.Decode(receipt, command.Operation, caller, accepted.ReplayUntilUtc);
        Assert.Equal(accepted.Tasks, decoded.Tasks);
        Assert.Equal(accepted.Source, decoded.Source);
        Assert.Equal(receipt.AcceptedPayload, ProjectTaskPlanOperationCodec.EncodeAccepted(decoded));
        receipt.ResultVersion = version == 1 ? 2 : 1;
        Assert.Throws<ProjectTaskPlanReceiptDecodeException>(() => ProjectTaskPlanOperationProtocol.Decode(
            receipt, command.Operation, caller, accepted.ReplayUntilUtc));
    }

    [Fact]
    public void Changed_work_interpretation_changes_semantics_without_creating_another_operation_address() {
        var caller = Caller();
        var first = Command(caller);
        var second = new ProjectTaskPlanCommand(first.Operation, first.Basis, first.Proposal, first.Policy,
            new(first.Source.DecisionId, first.Source.Generation, first.Source.DecisionStartedAtUtc, first.Source.Kind,
                ProjectTaskPlanSource.ActiveWorkVersion, first.Source.Fingerprint), schemaVersion: ProjectTaskPlanSource.ActiveWorkVersion);
        Assert.NotEqual(ProjectTaskPlanOperationCodec.Digest(first), ProjectTaskPlanOperationCodec.Digest(second));
        Assert.Equal(ProjectTaskPlanOperationProtocol.OperationKey(first.Operation, caller),
            ProjectTaskPlanOperationProtocol.OperationKey(second.Operation, caller));
    }

    [Fact]
    public void Mixed_result_and_source_versions_cannot_reinterpret_retained_acceptance() {
        foreach (var version in new[] { 1, 2 }) {
            var payload = JsonNode.Parse(ProjectTaskPlanOperationCodec.EncodeAccepted(Accept(Command(version: version))))!;
            payload["version"] = version == 1 ? 2 : 1;
            Assert.Throws<ProjectTaskPlanReceiptDecodeException>(() => ProjectTaskPlanOperationCodec.DecodeAccepted(
                Encoding.UTF8.GetBytes(payload.ToJsonString())));
        }
        Assert.Throws<ArgumentException>(() => new ProjectTaskPlanSource(Guid.NewGuid(), 1, Instant,
            ProjectTaskPlanSourceKind.OrdinaryAgentDecision, 3, new('b', 64)));
    }

    private static ProjectTaskPlanCaller Caller() => new(ProjectTaskPlanCallerKind.OrdinaryAgent, Enumerable.Repeat((byte)3, 32).ToArray());
    private static ProjectTaskPlanCommand Command(ProjectTaskPlanCaller? caller = null, int version = 1) {
        caller ??= Caller();
        var ns = new DatasetNamespaceId(Guid.NewGuid());
        var expected = new ProjectDefinitionExpectation(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid());
        var basis = new ProjectTaskPlanBasis(ns, new(expected, new("Project", "", "", ProjectStatus.Draft, "", null)), Instant, []);
        return new(new(ns, 1, Guid.NewGuid(), caller.Bind(ns)), basis,
            ProjectTaskPlanProposal.Normalize(new(1, [new("First", "", "", 1), new("Second", "", "", 1)])),
            DecisionDataPolicy.PlanningDefault(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(2)),
            new(Guid.NewGuid(), 1, Instant, ProjectTaskPlanSourceKind.OrdinaryAgentDecision, version, new('b', 64)), schemaVersion: version);
    }

    private static ProjectTaskPlanAccepted Accept(ProjectTaskPlanCommand command) {
        using var context = new WorkbenchDbContext(new DbContextOptionsBuilder<WorkbenchDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var batch = ProjectTaskPlanTaskStaging.StageNewPlainTasks(context, command.Basis.Project.Expectation.Project, command.Proposal,
            new(command.Basis, 0, ProjectTaskPlanBasisLimits.ScalarBytes(command.Basis.Project.Scalars)), Instant);
        return new(command.Operation, command.Basis.Project.Expectation, command.Source, ProjectTaskPlanOperationCodec.Digest(command),
            Instant, Instant.AddDays(8), batch.Tasks);
    }
}
