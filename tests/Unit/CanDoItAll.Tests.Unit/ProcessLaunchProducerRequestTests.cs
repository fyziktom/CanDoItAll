using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessLaunchProducerRequestTests {
    [Fact]
    public async Task Serialized_preparation_replay_restores_exact_server_context_target_and_original_lifetime() {
        var fixture = await CreateAsync();
        var caller = fixture.Authority with { ProjectAdmission = null, PolicyFingerprint = "current-operator-policy" };
        var replay = Assert.IsType<ProcessLaunchRequest>(await ProcessLaunchProducerRequests.FindReplayAsync(
            fixture.Input with { Execute = true }, caller, fixture.Store));
        Assert.Equal(fixture.Saved.Preparation.AdmissionId, replay.PreparedAdmissionId);
        Assert.NotEqual(Guid.Empty, replay.PreparedAdmissionId!.Value.Value);
        Assert.Equal(fixture.Authority.ProjectAdmission, replay.ProjectAdmission);
        Assert.Equal(fixture.Authority.ProjectAdmission, replay.Authority!.ProjectAdmission);
        Assert.Equal(caller.PolicyFingerprint, replay.Authority.PolicyFingerprint);
        Assert.Equal(fixture.Saved.Preparation.LinkTarget, replay.LinkTarget);
        Assert.Equal("original-server-session", replay.Variables["SessionId"]);
        Assert.Equal(fixture.Saved.Preparation.Request.ProducerInputFingerprint, replay.ProducerInputFingerprint);
        Assert.Equal(fixture.Saved.Preparation.RequestFingerprint, ProcessLaunchIntentFingerprint.Compute(replay));
        Assert.True(replay.Execute);
    }

    public enum ChangedInput { Variables, Project, Node, Definition, Readiness, DisplayRequester }

    [Theory]
    [InlineData(ChangedInput.Variables)]
    [InlineData(ChangedInput.Project)]
    [InlineData(ChangedInput.Node)]
    [InlineData(ChangedInput.Definition)]
    [InlineData(ChangedInput.Readiness)]
    [InlineData(ChangedInput.DisplayRequester)]
    public async Task Explicit_retry_rejects_changed_original_input_before_reusing_a_saved_context(ChangedInput change) {
        var fixture = await CreateAsync();
        var input = change switch {
            ChangedInput.Variables => fixture.Input with { Variables = new Dictionary<string, string> { ["Topic"] = "Changed" } },
            ChangedInput.Project => fixture.Input with { ProjectId = Guid.NewGuid() },
            ChangedInput.Node => fixture.Input with { ProjectNodeId = "another-node" },
            ChangedInput.Definition => fixture.Input with { DefinitionKey = "another-definition" },
            ChangedInput.Readiness => fixture.Input with { RunReadiness = !fixture.Input.RunReadiness },
            ChangedInput.DisplayRequester => fixture.Input with { RequestedBy = "another-display-requester" },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        await Assert.ThrowsAsync<ProcessLaunchIntentConflictException>(() => ProcessLaunchProducerRequests.FindReplayAsync(input, fixture.Authority, fixture.Store));
        var retained = Assert.IsType<ProcessPreparedLaunchSnapshot>(await fixture.Store.GetAsync(fixture.Saved.Preparation.AdmissionId));
        Assert.Equal(fixture.Saved.PreparationFingerprint, retained.PreparationFingerprint);
        Assert.Null(retained.AcceptedAtUtc);
    }

    [Fact]
    public async Task A_current_project_tuple_cannot_retarget_a_restored_preparation() {
        var fixture = await CreateAsync();
        var original = fixture.Authority.ProjectAdmission!;
        var caller = fixture.Authority with { ProjectAdmission = new(original.DatabaseProfileId, original.ProjectId, Guid.NewGuid()) };
        var replay = Assert.IsType<ProcessLaunchRequest>(await ProcessLaunchProducerRequests.FindReplayAsync(fixture.Input, caller, fixture.Store));
        Assert.Equal(original, replay.ProjectAdmission);
        Assert.NotEqual(caller.ProjectAdmission, replay.ProjectAdmission);
        Assert.Equal(fixture.Saved.Preparation.InitialCommit.Mutation.State.ProjectAdmission, replay.Authority!.ProjectAdmission);
    }

    [Fact]
    public async Task Concurrent_equivalent_input_reuses_the_winning_enrichment_and_review() {
        var fixture = await CreateAsync();
        var contender = ProcessLaunchProducerRequests.Restore(fixture.Saved, fixture.Authority, execute: false) with {
            Variables = new Dictionary<string, string>(fixture.Saved.Preparation.Request.Variables) { ["SessionId"] = "another-server-session" }
        };
        Assert.NotEqual(fixture.Saved.Preparation.RequestFingerprint, ProcessLaunchIntentFingerprint.Compute(contender));
        var winner = Assert.IsType<ProcessLaunchRequest>(await ProcessLaunchProducerRequests.FindConcurrentReplayAsync(contender, fixture.Store));
        Assert.Equal(fixture.Saved.Preparation.RequestFingerprint, ProcessLaunchIntentFingerprint.Compute(winner));
        Assert.Equal(fixture.Saved.Preparation.AdmissionId, winner.PreparedAdmissionId);
        Assert.Equal("original-server-session", winner.Variables["SessionId"]);
    }

    [Fact]
    public async Task Concurrent_conflict_with_different_original_input_is_not_recovered_as_a_retry() {
        var fixture = await CreateAsync();
        var changed = fixture.Input with { ProjectNodeId = "changed-original-target" };
        var contender = ProcessLaunchProducerRequests.Restore(fixture.Saved, fixture.Authority, execute: false) with {
            ProducerInputFingerprint = ProcessLaunchProducerRequests.InputFingerprint(changed, fixture.Authority)
        };
        Assert.Null(await ProcessLaunchProducerRequests.FindConcurrentReplayAsync(contender, fixture.Store));
        Assert.NotNull(await fixture.Store.GetAsync(fixture.Saved.Preparation.AdmissionId));
    }

    [Fact]
    public async Task Another_principal_kind_cannot_claim_the_local_UI_preparation_even_with_the_same_profile() {
        var fixture = await CreateAsync();
        var caller = fixture.Authority with { Principal = new ProcessLaunchPrincipal.AuthenticatedOperator("operator", DateTimeOffset.UtcNow.AddHours(1)) };
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProcessLaunchProducerRequests.FindReplayAsync(fixture.Input, caller, fixture.Store));
    }

    [Fact]
    public async Task Omitted_identity_preserves_deliberate_repetition_and_does_not_guess_an_existing_intent() {
        var fixture = await CreateAsync();
        var legacy = fixture.Input with { CallerIntentId = null, PreparedAdmissionId = null };
        Assert.Null(await ProcessLaunchProducerRequests.FindReplayAsync(legacy, fixture.Authority, fixture.Store));
        Assert.Null(await ProcessLaunchProducerRequests.FindReplayAsync(legacy, fixture.Authority, fixture.Store));
        Assert.NotNull(await fixture.Store.GetAsync(fixture.Saved.Preparation.AdmissionId));
    }

    [Fact]
    public async Task Older_preparation_without_original_input_hash_is_not_silently_rebound_by_the_new_API() {
        var fixture = await CreateAsync(includeProducerInput: false);
        await Assert.ThrowsAsync<ProcessLaunchIntentConflictException>(() => ProcessLaunchProducerRequests.FindReplayAsync(fixture.Input, fixture.Authority, fixture.Store));
        var restored = ProcessLaunchProducerRequests.Restore(fixture.Saved, fixture.Authority, execute: true);
        Assert.Equal(fixture.Saved.Preparation.AdmissionId, restored.PreparedAdmissionId);
        Assert.Null(restored.ProducerInputFingerprint);
    }

    [Fact]
    public void Equivalent_original_variable_order_and_a_renewed_authenticated_token_keep_the_input_identity() {
        var authority = new ProcessLaunchAuthority(new ProcessLaunchPrincipal.AuthenticatedOperator("same-subject", DateTimeOffset.UtcNow.AddHours(1)),
            Guid.NewGuid(), null, true, true, "policy");
        var request = ProcessPreparedLaunchFixture.Create(authority).Request with {
            Variables = new Dictionary<string, string> { ["b"] = "two", ["a"] = "one" }
        };
        var renewed = authority with { Principal = new ProcessLaunchPrincipal.AuthenticatedOperator("same-subject", DateTimeOffset.UtcNow.AddHours(2)) };
        Assert.Equal(ProcessLaunchProducerRequests.InputFingerprint(request, authority),
            ProcessLaunchProducerRequests.InputFingerprint(request with {
                Execute = true,
                Variables = new Dictionary<string, string> { ["a"] = "one", ["b"] = "two" }
            }, renewed));
    }

    private static async Task<Fixture> CreateAsync(bool includeProducerInput = true) {
        var profile = Guid.NewGuid();
        var authority = ProcessPreparedLaunchFixture.Local(profile, new(profile, Guid.NewGuid(), Guid.NewGuid()));
        var target = new ProcessLaunchLinkTarget(authority.ProjectAdmission!.ProjectId, "original-node", "sha256:original-binding");
        var prepared = ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid()), target);
        var input = prepared.Request with { Authority = null, ProjectAdmission = null, LinkTarget = null };
        var enriched = prepared.Request with {
            Variables = new Dictionary<string, string>(input.Variables) { ["SessionId"] = "original-server-session" },
            ProducerInputFingerprint = includeProducerInput ? ProcessLaunchProducerRequests.InputFingerprint(input, authority) : null
        };
        prepared = prepared with { Request = enriched, RequestFingerprint = ProcessLaunchIntentFingerprint.Compute(enriched) };
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        var store = new EfProcessPreparedLaunchStore(new Factory(options), options);
        var saved = await store.PrepareAsync(prepared);
        return new(input, authority, saved, store);
    }

    private sealed record Fixture(ProcessLaunchRequest Input, ProcessLaunchAuthority Authority,
        ProcessPreparedLaunchSnapshot Saved, EfProcessPreparedLaunchStore Store);

    private sealed class Factory(DbContextOptions<ProcessPersistenceDbContext> options) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => new(options);
    }
}
