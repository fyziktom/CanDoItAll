using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessPredecessorRecoveryTests {
    [Fact]
    public void Fixture_is_authentic_predecessor_serialization_with_unchanged_typed_and_byte_hashes() {
        using var provenance = JsonDocument.Parse(ProcessPredecessorFixture.ProvenanceJson);
        using var payload = JsonDocument.Parse(ProcessPredecessorFixture.PlanJson);
        Assert.False(payload.RootElement.TryGetProperty("executableDefinitions", out _));
        Assert.False(provenance.RootElement.GetProperty("HasExecutableDefinitionsProperty").GetBoolean());
        Assert.Equal(provenance.RootElement.GetProperty("PlanSha256").GetString(), Hash(ProcessPredecessorFixture.PlanJson));
        Assert.Equal(provenance.RootElement.GetProperty("CommitSha256").GetString(), Hash(ProcessPredecessorFixture.InitialCommitJson));
        var fixture = new Fixture();
        Assert.Null(fixture.Plan.ExecutableDefinitions);
        Assert.Null(fixture.State.ProjectAdmission);
        Assert.Equal(provenance.RootElement.GetProperty("PlanHash").GetString(), fixture.Plan.PlanHash);
        Assert.Equal(fixture.Plan.PlanHash, ProcessPlanHasher.Compute(fixture.Plan));
        Assert.Equal(ProcessPredecessorFixture.PlanJson, ProcessInstancePlanPersistenceMapper.ToEntity(fixture.Plan).PayloadJson);
    }

    [Theory]
    [InlineData(ProcessRuntimeStatus.Active)]
    [InlineData(ProcessRuntimeStatus.Completed)]
    [InlineData(ProcessRuntimeStatus.Blocked)]
    [InlineData(ProcessRuntimeStatus.Failed)]
    [InlineData(ProcessRuntimeStatus.Cancelled)]
    public async Task Matching_predecessor_without_authoritative_origin_requires_reconciliation_instead_of_replacement(ProcessRuntimeStatus status) {
        var fixture = new Fixture();
        fixture.State = fixture.State with { Status = status };
        var error = await Assert.ThrowsAsync<ProcessExistingLaunchReconciliationRequiredException>(() => fixture.Service().FindExistingLaunchAsync(fixture.Request()));
        Assert.Contains("origin", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, fixture.Writes);
        Assert.Equal(ProcessPredecessorFixture.PlanJson, ProcessInstancePlanPersistenceMapper.ToEntity(fixture.Plan).PayloadJson);
    }

    [Theory]
    [InlineData(ProcessRuntimeStatus.Active, false)]
    [InlineData(ProcessRuntimeStatus.Active, true)]
    [InlineData(ProcessRuntimeStatus.Completed, false)]
    [InlineData(ProcessRuntimeStatus.Completed, true)]
    public async Task Same_origin_reuses_the_immutable_predecessor_plan_and_assignments_without_current_template_meaning(ProcessRuntimeStatus status, bool normalizedKey) {
        var fixture = new Fixture();
        fixture.State = fixture.State with { Status = status, ProjectAdmission = fixture.CurrentAdmission };
        var request = fixture.Request();
        if (normalizedKey) {
            request = request with { DefinitionKey = "  DOTNET-RUNTIME-COMMAND-WRITEBACK  " };
        }

        var found = await fixture.Service().FindExistingLaunchAsync(request);

        Assert.NotNull(found);
        Assert.Equal(fixture.State.RunId, found.RunId);
        Assert.Equal(fixture.Plan.Header.PlanId, found.LaunchPlanId);
        Assert.Equal(fixture.Plan.PlanHash, found.LaunchPlan.PlanHash);
        Assert.Equal(ProcessPredecessorFixture.DefinitionKey, found.LaunchPlan.DefinitionKey);
        Assert.Equal(ProcessPredecessorFixture.DefinitionKey, found.LaunchPlan.DefinitionName);
        foreach (var step in found.LaunchPlan.Steps) {
            var assigned = fixture.Assignments.Single(item => item.StepInstanceId == step.StepInstanceId);
            Assert.Equal(assigned.StepKey, step.Title);
            Assert.Equal(assigned.RoleDisplayName, step.RoleDisplayName);
            Assert.Equal(assigned.ExecutorId, step.ExecutorId);
            Assert.Equal(assigned.AllowedOperations, step.AllowedOperations);
            Assert.Equal(assigned.OperationTargetScope, step.OperationTargetScope);
            Assert.Equal(assigned.CapabilityScope, step.CapabilityScope);
        }
        Assert.Equal(found.RunId, (await fixture.Service().FindExistingLaunchAsync(request))!.RunId);
        Assert.Equal(0, fixture.Writes);
    }

    [Theory]
    [InlineData(OtherOrigin.Project)]
    [InlineData(OtherOrigin.Lifetime)]
    [InlineData(OtherOrigin.Profile)]
    [InlineData(OtherOrigin.Global)]
    public async Task Authoritatively_different_origin_is_not_exposed_or_rebound(OtherOrigin origin) {
        var fixture = new Fixture();
        var current = fixture.CurrentAdmission;
        fixture.State = fixture.State with { ProjectAdmission = origin switch {
            OtherOrigin.Project => new(current.DatabaseProfileId, Guid.NewGuid(), current.LifetimeId),
            OtherOrigin.Lifetime => new(current.DatabaseProfileId, current.ProjectId, Guid.NewGuid()),
            OtherOrigin.Profile => new(Guid.NewGuid(), current.ProjectId, current.LifetimeId),
            _ => current
        } };
        Assert.Null(await fixture.Service().FindExistingLaunchAsync(fixture.Request() with { ProjectId = origin == OtherOrigin.Global ? null : current.ProjectId }));
        Assert.Equal(0, fixture.Writes);
    }

    [Fact]
    public async Task No_matching_candidate_remains_absent_without_inventing_a_legacy_origin() {
        var fixture = new Fixture { Match = false };
        Assert.Null(await fixture.Service().FindExistingLaunchAsync(fixture.Request()));
        Assert.Equal(0, fixture.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accepted_preparation_proves_missing_runtime_origin_and_preserves_its_original_review(bool project) {
        var fixture = new Fixture();
        fixture.RetainPreparation(project);
        var found = await fixture.Service().FindExistingLaunchAsync(fixture.Request() with { ProjectId = project ? fixture.CurrentAdmission.ProjectId : null });
        Assert.NotNull(found);
        Assert.Null(fixture.State.ProjectAdmission);
        Assert.Equal("Original retained review", found.LaunchPlan.DefinitionName);
        Assert.Equal(fixture.Saved!.Preparation.Review.Steps, found.LaunchPlan.Steps);
        Assert.Equal(fixture.State.RunId, found.RunId);
        Assert.Equal(0, fixture.Writes);
    }

    [Theory]
    [InlineData(BrokenEvidence.Unaccepted)]
    [InlineData(BrokenEvidence.Run)]
    [InlineData(BrokenEvidence.Plan)]
    [InlineData(BrokenEvidence.Authority)]
    [InlineData(BrokenEvidence.Admission)]
    [InlineData(BrokenEvidence.Review)]
    [InlineData(BrokenEvidence.InvalidAuthority)]
    public async Task Missing_or_contradictory_retained_evidence_is_an_explicit_refusal(BrokenEvidence broken) {
        var fixture = new Fixture();
        fixture.RetainPreparation(true);
        var saved = fixture.Saved!;
        var prepared = saved.Preparation;
        fixture.Saved = broken switch {
            BrokenEvidence.Unaccepted => saved with { AcceptedAtUtc = null },
            BrokenEvidence.Run => saved with { Preparation = prepared with { InitialCommit = prepared.InitialCommit with {
                Mutation = prepared.InitialCommit.Mutation with { State = prepared.InitialCommit.Mutation.State with { RunId = new(Guid.NewGuid()) } } } } },
            BrokenEvidence.Plan => saved with { Preparation = prepared with { InitialCommit = prepared.InitialCommit with {
                InitialPlan = prepared.InitialCommit.InitialPlan! with { PlanHash = "conflicting-retained-hash" } } } },
            BrokenEvidence.Authority => saved with { Preparation = prepared with { Authority = prepared.Authority! with {
                ProjectAdmission = new(fixture.DatabaseProfileId, fixture.CurrentAdmission.ProjectId, Guid.NewGuid()) } } },
            BrokenEvidence.Review => saved with { Preparation = prepared with { Review = prepared.Review with { DefinitionId = new(Guid.NewGuid()) } } },
            BrokenEvidence.InvalidAuthority => saved with { Preparation = prepared with { Authority = prepared.Authority! with { DatabaseProfileId = Guid.Empty } } },
            _ => null
        };
        await Assert.ThrowsAsync<ProcessExistingLaunchReconciliationRequiredException>(() => fixture.Service().FindExistingLaunchAsync(fixture.Request()));
        Assert.Equal(0, fixture.Writes);
    }

    [Fact]
    public void Incomplete_project_admission_is_rejected_by_the_existing_value_contract() {
        Assert.Throws<ArgumentException>(() => new ProcessProjectAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty));
    }

    [Theory]
    [InlineData(ProcessRuntimeStatus.Blocked)]
    [InlineData(ProcessRuntimeStatus.Failed)]
    [InlineData(ProcessRuntimeStatus.Cancelled)]
    public async Task Authoritative_stopped_children_keep_their_existing_rework_or_replacement_policy(ProcessRuntimeStatus status) {
        var fixture = new Fixture();
        fixture.State = fixture.State with { Status = status, ProjectAdmission = fixture.CurrentAdmission };
        Assert.Null(await fixture.Service().FindExistingLaunchAsync(fixture.Request()));
        var stopped = await fixture.Service().FindExistingLaunchAsync(fixture.Request() with { StoppedOnly = true });
        if (status == ProcessRuntimeStatus.Cancelled) {
            Assert.Null(stopped);
        } else {
            Assert.Equal(fixture.State.RunId, stopped!.RunId);
        }
        Assert.Equal(0, fixture.Writes);
    }

    [Fact]
    public async Task Subprocess_selection_normalizes_recorded_aliases_without_case_folding_opaque_source_values() {
        var fixture = new Fixture();
        fixture.State = fixture.State with { ProjectAdmission = fixture.CurrentAdmission };
        fixture.Assignments = fixture.Assignments.Select(item => item with { LaunchVariables = new Dictionary<string, string>(item.LaunchVariables) {
            [ProcessRuntimeLaunchVariables.SubprocessDefinitionKey] = ProcessPredecessorFixture.DefinitionKey,
            [ProcessRuntimeLaunchVariables.SubprocessLiveRunProfileKey] = "recorded-profile"
        } }).ToArray();
        var request = fixture.Request() with { DefinitionKey = " DOTNET-RUNTIME-COMMAND-WRITEBACK ", LiveRunProfileKey = " RECORDED-PROFILE " };
        request = request with { RequiredLaunchVariables = new Dictionary<string, string>(request.RequiredLaunchVariables) {
            [ProcessRuntimeLaunchVariables.SubprocessDefinitionKey] = request.DefinitionKey,
            [ProcessRuntimeLaunchVariables.SubprocessLiveRunProfileKey] = request.LiveRunProfileKey
        } };
        Assert.Equal(fixture.State.RunId, (await fixture.Service().FindExistingLaunchAsync(request))!.RunId);
        Assert.Null(await fixture.Service().FindExistingLaunchAsync(request with { RequiredLaunchVariables = new Dictionary<string, string> {
            [ProcessPredecessorFixture.ProbeKey] = "PREDECESSOR-RETAINED-RUN"
        } }));
        Assert.Null(await fixture.Service().FindExistingLaunchAsync(fixture.Request() with { LiveRunProfileKey = "different-profile" }));
    }

    [Fact]
    public async Task Explicit_profile_without_retained_profile_evidence_does_not_resolve_todays_profile() {
        var fixture = new Fixture();
        fixture.State = fixture.State with { ProjectAdmission = fixture.CurrentAdmission };
        var error = await Assert.ThrowsAsync<ProcessExistingLaunchReconciliationRequiredException>(() => fixture.Service()
            .FindExistingLaunchAsync(fixture.Request() with { LiveRunProfileKey = "missing-historical-profile" }));
        Assert.Contains("live-run profile", error.Message, StringComparison.Ordinal);
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public enum OtherOrigin { Project, Lifetime, Profile, Global }
    public enum BrokenEvidence { Unaccepted, Run, Plan, Authority, Admission, Review, InvalidAuthority }

    private sealed class Fixture : IProcessInstancePlanStore, IProcessRuntimeStateStore, IProcessRuntimeStepAssignmentStore,
        IProcessAuthoringContext, IProcessAuthoringStore, IProcessPreparedLaunchStore {
        public Fixture() {
            var commit = JsonSerializer.Deserialize<ProcessRuntimeCommitRequest>(ProcessPredecessorFixture.InitialCommitJson,
                ProcessInstancePlanPersistenceMapper.CreateSerializerOptions())!;
            Plan = commit.InitialPlan!;
            State = commit.Mutation.State;
            Assignments = commit.InitialAssignments!;
            Initial = commit;
        }
        public ProcessProjectAdmission CurrentAdmission { get; } = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        public ProcessInstancePlan Plan { get; }
        public ProcessRuntimeStateSnapshot State { get; set; }
        public IReadOnlyList<ProcessRuntimeStepAssignment> Assignments { get; set; }
        public ProcessRuntimeCommitRequest Initial { get; }
        public ProcessPreparedLaunchSnapshot? Saved { get; set; }
        public bool Match { get; init; } = true;
        public int Writes { get; private set; }
        public Guid DatabaseProfileId => CurrentAdmission.DatabaseProfileId;
        public string CallerId => "predecessor-recovery-unit";
        public ProcessExistingLaunchLookupRequest Request() => new(ProcessPredecessorFixture.DefinitionKey, null, CurrentAdmission.ProjectId,
            new Dictionary<string, string> { [ProcessPredecessorFixture.ProbeKey] = ProcessPredecessorFixture.ProbeValue });
        public ProcessLaunchApplicationService Service() {
            var templates = new ProcessTemplatePackLoader();
            ProcessExecutableDefinitionResolver resolver = new(this, this, new(this, this, templates), templates, null!);
            return new(templates, null!, null!, null!, this, null!, this, this, null!, null!, null!, null!, null!, null!, preparedLaunchStore: this, executableDefinitions: resolver);
        }
        public void RetainPreparation(bool project) {
            ProcessLaunchAdmissionId id = new(Guid.NewGuid());
            var admission = project ? CurrentAdmission : null;
            var initial = Initial with { OriginalState = Initial.OriginalState with { ProjectAdmission = admission, LaunchAdmissionId = id },
                Mutation = Initial.Mutation with { State = Initial.Mutation.State with { ProjectAdmission = admission, LaunchAdmissionId = id } } };
            var authority = ProcessPreparedLaunchFixture.Local(DatabaseProfileId, admission);
            ProcessLaunchRequest request = new(ProcessPredecessorFixture.DefinitionKey, null, null, admission?.ProjectId, null,
                CallerId, Assignments[0].LaunchVariables, false, false) { Authority = authority };
            var review = ProcessRetainedLaunchRecovery.LegacyReview(Plan, Assignments, []) with { DefinitionName = "Original retained review" };
            ProcessPreparedLaunch preparation = new(id, null, ProcessLaunchIntentFingerprint.Compute(request), authority, request,
                initial, review, null, DateTimeOffset.UnixEpoch);
            Saved = new(preparation, "trusted-store-fixture", 1, ProcessLaunchContinuationState.Started, DateTimeOffset.UnixEpoch, false,
                ProcessLaunchLinkDeliveryState.NotRequested, null, null);
            State = State with { LaunchAdmissionId = id };
        }
        public ValueTask<ProcessInstancePlan?> LoadAsync(ProcessInstancePlanId planId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<ProcessInstancePlan?>(planId == Plan.Header.PlanId ? Plan : null);
        public Task<ProcessRuntimeStateSnapshot?> LoadAsync(ProcessRunId runId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProcessRuntimeStateSnapshot?>(runId == State.RunId ? State : null);
        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> LoadByRunAsync(ProcessRunId runId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<ProcessRuntimeStepAssignment>>(runId == State.RunId ? Assignments : []);
        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> FindByLaunchVariablesAsync(IReadOnlyDictionary<string, string> requiredVariables, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<ProcessRuntimeStepAssignment>>(Match && Assignments.All(item => requiredVariables.All(pair => item.LaunchVariables.GetValueOrDefault(pair.Key) == pair.Value)) ? Assignments : []);
        public ValueTask<ProcessRuntimeStepAssignment?> LoadAsync(ProcessRunId runId, ProcessStepInstanceId stepInstanceId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Assignments.SingleOrDefault(item => item.RunId == runId && item.StepInstanceId == stepInstanceId));
        public ValueTask<PersistedProcessInstancePlan> PersistAsync(ProcessInstancePlan plan, CancellationToken cancellationToken = default) {
            Writes++;
            throw new InvalidOperationException("Lookup must not rewrite a predecessor plan.");
        }
        public ValueTask SaveAsync(IReadOnlyList<ProcessRuntimeStepAssignment> assignments, CancellationToken cancellationToken = default) {
            Writes++;
            throw new InvalidOperationException("Lookup must not rewrite predecessor assignments.");
        }
        public Task<ProcessAuthoringAddress> CaptureReadAddressAsync(ProcessWorkspaceShellScope scope, ProcessDefinitionCatalogItemKey key, CancellationToken cancellationToken)
            => Task.FromResult(new ProcessAuthoringAddress(DatabaseProfileId, scope.ProjectId ?? Guid.Empty,
                scope.ProjectId is null ? Guid.Empty : CurrentAdmission.LifetimeId, key.Value));
        public Task RequireLaunchableAsync(ProcessExecutableDefinitionClosure definitions, bool underMutationGate, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ProcessAuthoringReceipt?> GetOperationAsync(ProcessAuthoringAddress address, string callerId, Guid operationId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ProcessAuthoringSnapshot?> ReadAsync(ProcessAuthoringAddress address, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<ProcessAuthoringCatalogEntry>> ListAsync(Guid databaseProfileId, Guid projectId, Guid projectLifetimeId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ProcessAuthoringPublication?> ReadPublicationAsync(Guid publicationId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ProcessAuthoringReceipt?> RecoverAsync(ProcessAuthoringAddress address, string callerId, Guid operationId, string requestFingerprint, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ProcessAuthoringReceipt> CommitAsync(ProcessAuthoringCommit command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ProcessPreparedLaunchSnapshot?> GetAsync(ProcessLaunchAdmissionId admissionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Saved?.Preparation.AdmissionId == admissionId ? Saved : null);
        public Task<ProcessPreparedLaunchSnapshot?> FindByRunAsync(ProcessRunId runId, CancellationToken cancellationToken = default)
            => Task.FromResult(Saved?.Preparation.InitialCommit.Mutation.State.RunId == runId ? Saved : null);
        public Task<ProcessPreparedLaunchSnapshot?> FindByIntentAsync(ProcessLaunchIntentId intentId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ProcessPreparedLaunchSnapshot> PrepareAsync(ProcessPreparedLaunch preparation, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ProcessLaunchContinuationClaim?> ClaimContinuationAsync(ProcessLaunchAdmissionId admissionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> RenewContinuationAsync(ProcessLaunchContinuationClaim claim, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task CompleteContinuationAsync(ProcessLaunchContinuationClaim claim, ProcessLaunchContinuationState state, string? publicFailure, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<ProcessLaunchAdmissionId>> ListPendingContinuationsAsync(int take, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
