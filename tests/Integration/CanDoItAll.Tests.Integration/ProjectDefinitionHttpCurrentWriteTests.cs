using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Api;
using static CanDoItAll.Tests.Integration.Api.ProjectDefinitionHttpCurrentWriteFixture;

namespace CanDoItAll.Tests.Integration.Api;

[Trait("Category", "HostPlatform")]
public sealed class ProjectDefinitionHttpCurrentWriteTests {
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Machine_revocation_during_a_real_project_guard_wait_rejects_without_any_published_change() {
        await using var fixture = await CreateAsync();
        var request = fixture.Replacement();
        var before = await fixture.StateAsync();
        using var response = await DuringProjectWaitAsync(fixture, request,
            () => fixture.Tokens.Inner.RevokeAsync(fixture.TokenRecord!.Id, fixture.Clock.GetUtcNow()));
        await fixture.RejectedAsync(response, request.Operation, HttpStatusCode.Forbidden, ProjectErrorCodes.DefinitionWriteDenied);
        await AssertRolledBackAsync(fixture, before);
        Assert.Equal(1, fixture.Tokens.CurrentReads);
        Assert.Equal(0, fixture.Users.SnapshotReads);
    }

    [Fact]
    public async Task Ordinary_session_revision_or_grant_loss_during_the_project_wait_invalidates_its_captured_generation() {
        foreach (var loseGrant in new[] { false, true }) {
            await using var fixture = await CreateAsync(ordinary: true);
            var request = fixture.Replacement();
            var before = await fixture.StateAsync();
            var user = fixture.User!;
            using var response = await DuringProjectWaitAsync(fixture, request, () => fixture.SaveUserAsync(user with {
                Version = user.Version + 1, AuthenticationRevision = user.AuthenticationRevision + 1,
                AllowedScopes = loseGrant ? [ApiAccessScopeNames.ReadProjects] : user.AllowedScopes
            }));
            await fixture.RejectedAsync(response, request.Operation, HttpStatusCode.Conflict, ProjectErrorCodes.DefinitionAuthorityChanged);
            await AssertRolledBackAsync(fixture, before);
            Assert.Equal(1, fixture.Users.SnapshotReads);
            Assert.Equal(1, fixture.Users.HeaderReads);
            Assert.Equal(0, fixture.Users.FullReads);
        }
    }

    [Fact]
    public async Task Final_current_write_runs_after_the_real_enlisted_lifetime_guard_and_before_commit() {
        await using var fixture = await CreateAsync();
        fixture.Reset(AuthorityPause.FinalLifetime);
        var request = fixture.Replacement();
        var before = await fixture.StateAsync();
        using var response = await DuringFinalGuardAsync(fixture, request, async () => {
            Assert.Equal(1, fixture.Tokens.CurrentReads);
            Assert.True(fixture.Probe.RootFlushed && fixture.Probe.SearchFlushed && fixture.Probe.ReceiptFlushed);
            Assert.Equal(0, fixture.Probe.CommitAttempts);
            await fixture.Tokens.Inner.RevokeAsync(fixture.TokenRecord!.Id, fixture.Clock.GetUtcNow());
        });
        await fixture.RejectedAsync(response, request.Operation, HttpStatusCode.Forbidden, ProjectErrorCodes.DefinitionWriteDenied);
        Assert.Equal(2, fixture.Tokens.CurrentReads);
        Assert.True(fixture.Probe.FinalLifetimeExecuted);
        await AssertRolledBackAsync(fixture, before);
    }

    [Fact]
    public async Task Open_and_authenticated_mode_changes_do_not_substitute_an_admitted_caller() {
        foreach (var open in new[] { false, true }) {
            await using var fixture = await CreateAsync(open: open);
            var request = fixture.Replacement();
            var before = await fixture.StateAsync();
            using var response = await DuringProjectWaitAsync(fixture, request, () => {
                fixture.Options.Authorization.Enabled = open;
                return Task.CompletedTask;
            });
            await fixture.RejectedAsync(response, request.Operation, HttpStatusCode.Conflict, ProjectErrorCodes.DefinitionCallerChanged);
            await AssertRolledBackAsync(fixture, before);
        }
    }

    [Fact]
    public async Task Replacing_registered_subject_or_user_binding_at_the_same_token_address_is_a_caller_conflict() {
        foreach (var ordinary in new[] { false, true }) {
            await using var fixture = await CreateAsync(ordinary: ordinary);
            var request = fixture.Replacement();
            var before = await fixture.StateAsync();
            var token = fixture.TokenRecord!;
            using var response = await DuringProjectWaitAsync(fixture, request, async () => {
                await fixture.Tokens.Inner.DeleteAsync(token.Id);
                fixture.Tokens.Inner.Register(token with {
                    Subject = ordinary ? token.Subject : "a-different-registered-caller",
                    UserId = ordinary ? Guid.NewGuid() : null
                });
            });
            await fixture.RejectedAsync(response, request.Operation, HttpStatusCode.Conflict, ProjectErrorCodes.DefinitionCallerChanged);
            await AssertRolledBackAsync(fixture, before);
        }
    }

    [Fact]
    public async Task Exact_token_expiry_while_waiting_is_not_extended_by_the_JWT_validation_clock_skew() {
        await using var fixture = await CreateAsync();
        var request = fixture.Replacement();
        var before = await fixture.StateAsync();
        using var response = await DuringProjectWaitAsync(fixture, request, () => {
            fixture.Clock.Set(fixture.TokenRecord!.ExpiresAtUtc);
            return Task.CompletedTask;
        });
        await fixture.RejectedAsync(response, request.Operation, HttpStatusCode.Forbidden, ProjectErrorCodes.DefinitionWriteDenied);
        await AssertRolledBackAsync(fixture, before);
        Assert.Equal(0, fixture.Tokens.CurrentReads);
    }

    [Fact]
    public async Task Unrelated_authority_change_requires_exact_envelope_readmission_and_preserves_the_original_business_expectation() {
        foreach (var interveningEdit in new[] { false, true }) {
            await using var fixture = await CreateAsync(ordinary: true);
            var other = await fixture.AddUserAsync("other");
            fixture.Reset();
            var request = fixture.Replacement();
            var originalEnvelope = JsonSerializer.Serialize(request, Json);
            var before = await fixture.StateAsync();
            using (var response = await DuringProjectWaitAsync(fixture, request, () => fixture.Users.Inner.SaveAsync(other with {
                Version = other.Version + 1, Enabled = false
            }, other.Version))) {
                await fixture.RejectedAsync(response, request.Operation, HttpStatusCode.Conflict, ProjectErrorCodes.DefinitionAuthorityChanged);
            }
            await AssertRolledBackAsync(fixture, before);
            if (interveningEdit) {
                using var independent = await fixture.SendAsync(fixture.Replacement("Independent accepted edit"));
                await AcceptedAsync(independent);
            }
            fixture.Reset();
            using var retry = await fixture.SendAsync(request);
            Assert.Equal(originalEnvelope, JsonSerializer.Serialize(request, Json));
            if (interveningEdit) {
                await fixture.RejectedAsync(retry, request.Operation, HttpStatusCode.Conflict, ProjectErrorCodes.DefinitionConflict);
                Assert.Equal(0, fixture.Probe.CommitAttempts);
            } else {
                var accepted = await AcceptedAsync(retry);
                Assert.Equal(request.Operation, accepted.Receipt.Operation);
                Assert.Equal(request.Command.Scalars.Name, accepted.Receipt.Acknowledgement.Name);
                Assert.Equal(1, fixture.Probe.Commits);
            }
        }
    }

    [Fact]
    public async Task Cosmetic_low_level_account_publication_preserves_generation_and_the_admitted_write() {
        await using var fixture = await CreateAsync(ordinary: true);
        var generation = (await fixture.Users.Inner.ReadSnapshotAsync()).Generation;
        var user = fixture.User!;
        var request = fixture.Replacement();
        using var response = await DuringProjectWaitAsync(fixture, request, async () => {
            await fixture.SaveUserAsync(user with { Version = user.Version + 1, DisplayName = "Cosmetic display change" });
            Assert.Equal(generation, (await fixture.Users.Inner.ReadSnapshotAsync()).Generation);
        });
        var accepted = await AcceptedAsync(response);
        Assert.Equal(request.Command.Scalars.Name, accepted.Receipt.Acknowledgement.Name);
        Assert.Equal(1, fixture.Probe.CommitAttempts);
        Assert.Equal(1, fixture.Probe.Commits);
        Assert.Equal(1, fixture.Users.SnapshotReads);
        Assert.Equal(2, fixture.Users.HeaderReads);
        Assert.Equal(0, fixture.Users.FullReads);
        Assert.Equal(0, fixture.Users.ReadsUnderGuard);
    }

    [Fact]
    public async Task Real_account_generation_change_between_observations_rolls_back_flushed_root_children_search_and_receipt() {
        await using var fixture = await CreateAsync(ordinary: true);
        fixture.Reset(AuthorityPause.FinalLifetime);
        var request = fixture.Replacement();
        var before = await fixture.StateAsync();
        var user = fixture.User!;
        using var response = await DuringFinalGuardAsync(fixture, request, async () => {
            Assert.Equal(1, fixture.Users.HeaderReads);
            Assert.True(fixture.Probe.RootFlushed && fixture.Probe.SearchFlushed && fixture.Probe.ReceiptFlushed);
            await fixture.SaveUserAsync(user with { Version = user.Version + 1, AuthenticationRevision = user.AuthenticationRevision + 1 });
        });
        await fixture.RejectedAsync(response, request.Operation, HttpStatusCode.Conflict, ProjectErrorCodes.DefinitionAuthorityChanged);
        Assert.Equal(2, fixture.Users.HeaderReads);
        Assert.True(fixture.Probe.FinalLifetimeExecuted);
        await AssertRolledBackAsync(fixture, before);
    }

    [Fact]
    public async Task Malformed_current_header_preserves_known_precommit_rejection_even_after_actual_owner_disposal_failure() {
        foreach (var cleanupFailure in new[] { false, true }) {
            await using var fixture = await CreateAsync(ordinary: true);
            var request = fixture.Replacement();
            var before = await fixture.StateAsync();
            fixture.Probe.FailDisposal = cleanupFailure;
            fixture.Probe.FailRecoveryAfterUnavailable = cleanupFailure;
            using var response = await DuringProjectWaitAsync(fixture, request,
                () => File.WriteAllTextAsync(fixture.AccountsPath, "{\"untrusted-private-content\":\"malformed\"}"));
            await fixture.RejectedAsync(response, request.Operation, HttpStatusCode.ServiceUnavailable, ProjectErrorCodes.DefinitionAuthorityUnavailable);
            Assert.Equal(cleanupFailure, fixture.Probe.DisposalFailed);
            Assert.Equal(0, fixture.Probe.RecoveryQueries);
            Assert.Equal(cleanupFailure ? 1 : 0, fixture.Probe.CleanupFailureTypes.Count);
            Assert.Equal(1, fixture.Users.HeaderReads);
            Assert.Equal(1, fixture.Users.SnapshotReads);
            Assert.Equal(0, fixture.Users.FullReads);
            Assert.Equal(0, fixture.Users.Preparations);
            Assert.Equal(0, fixture.Tokens.Preparations);
            Assert.Equal("{\"untrusted-private-content\":\"malformed\"}", await File.ReadAllTextAsync(fixture.AccountsPath));
            await AssertRolledBackAsync(fixture, before);
        }
    }

    [Fact]
    public async Task Pending_final_authority_read_is_drained_on_budget_or_request_cancellation_and_releases_the_real_guard() {
        foreach (var callerCancels in new[] { false, true }) {
            await using var fixture = await CreateAsync(ordinary: true);
            var request = fixture.Replacement();
            var before = await fixture.StateAsync();
            fixture.Users.PauseFinalRead = true;
            using var cancellation = new CancellationTokenSource();
            var pending = fixture.SendAsync(request, cancellation.Token);
            try {
                await fixture.Users.Pending.Task.WaitAsync(Timeout);
                Assert.True(fixture.Probe.RootFlushed && fixture.Probe.SearchFlushed && fixture.Probe.ReceiptFlushed);
                if (callerCancels) {
                    cancellation.Cancel();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
                } else {
                    using var response = await pending;
                    await fixture.RejectedAsync(response, request.Operation, HttpStatusCode.ServiceUnavailable, ProjectErrorCodes.DefinitionAuthorityUnavailable);
                }
                await fixture.Users.Drained.Task.WaitAsync(Timeout);
                await fixture.Probe.Released.Task.WaitAsync(Timeout);
                Assert.True(fixture.Users.PendingCancelled);
                await AssertRolledBackAsync(fixture, before);
            } finally {
                fixture.Users.ReleasePending.TrySetResult();
                try {
                    using var drained = await pending;
                } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
                }
            }
            fixture.Reset();
            using var subsequent = await fixture.SendAsync(request);
            var accepted = await AcceptedAsync(subsequent);
            Assert.Equal(request.Operation, accepted.Receipt.Operation);
            Assert.Equal(1, fixture.Probe.Commits);
        }
    }

    [Fact]
    public async Task Historical_receipt_replay_skips_effect_observation_but_requires_initial_write_and_fresh_read_disclosure() {
        await using var fixture = await CreateAsync(ordinary: true);
        var request = fixture.Replacement("Historical accepted replacement");
        ProjectDefinitionAcceptedApiResponse<ProjectEditorAcknowledgement> original;
        using (var first = await fixture.SendAsync(request)) {
            original = await AcceptedAsync(first);
        }
        using (var later = await fixture.SendAsync(fixture.Replacement("Current independent definition", original.Receipt.Acknowledgement))) {
            await AcceptedAsync(later);
        }
        var currentState = await fixture.StateAsync();
        fixture.Reset();
        using (var replayResponse = await fixture.SendAsync(request)) {
            var replay = await AcceptedAsync(replayResponse);
            Assert.True(replay.Replayed);
            Assert.Equal(JsonSerializer.Serialize(original.Receipt, Json), JsonSerializer.Serialize(replay.Receipt, Json));
        }
        Assert.Equal(0, fixture.Tokens.CurrentReads);
        Assert.Equal(0, fixture.Users.HeaderReads);
        Assert.Equal(1, fixture.Users.SnapshotReads);
        Assert.True(fixture.Users.FullReads > 0);
        Assert.Equal(0, fixture.Users.ReadsUnderGuard);
        Assert.Equal(0, fixture.Probe.CommitAttempts);
        Assert.Equal(currentState, await fixture.StateAsync());

        var user = fixture.User!;
        await fixture.SaveUserAsync(user with {
            Version = user.Version + 1, AuthenticationRevision = user.AuthenticationRevision + 1,
            AllowedScopes = [ApiAccessScopeNames.ReadProjects]
        });
        await fixture.LoginAsync();
        fixture.Reset();
        using (var readOnlyWrite = await fixture.SendAsync(request)) {
            Assert.Equal(HttpStatusCode.Forbidden, readOnlyWrite.StatusCode);
        }
        var operation = request.Operation;
        using (var readOnlyLookup = await fixture.Host.Client.GetAsync(
            $"/api/projects/definition-operations/{operation.Namespace.Value:D}/{operation.Epoch.Value}/{operation.Id:D}?callerBinding={Uri.EscapeDataString(operation.CallerBinding.Value)}")) {
            Assert.Equal(HttpStatusCode.OK, readOnlyLookup.StatusCode);
            var body = await readOnlyLookup.Content.ReadFromJsonAsync<JsonElement>(Json);
            Assert.Equal("foundFull", body.GetProperty("status").GetString());
        }
        Assert.Equal(0, fixture.Tokens.CurrentReads);

        user = fixture.User!;
        await fixture.SaveUserAsync(user with {
            Version = user.Version + 1, AuthenticationRevision = user.AuthenticationRevision + 1, AllowedScopes = Grants
        });
        await fixture.LoginAsync();
        fixture.Reset(AuthorityPause.ReceiptObservation);
        user = fixture.User!;
        using (var denied = await DuringFinalGuardAsync(fixture, request, async () => {
            await fixture.SaveUserAsync(user with { Version = user.Version + 1, AuthenticationRevision = user.AuthenticationRevision + 1, Enabled = false });
        })) {
            await fixture.RejectedAsync(denied, request.Operation, HttpStatusCode.Forbidden, ProjectErrorCodes.DefinitionReceiptUnavailable);
        }
        Assert.Equal(0, fixture.Tokens.CurrentReads);
        Assert.Equal(0, fixture.Users.HeaderReads);
        Assert.True(fixture.Users.FullReads > 0);
        Assert.Equal(0, fixture.Probe.CommitAttempts);
        Assert.Equal(currentState, await fixture.StateAsync());
    }

    private static async Task<HttpResponseMessage> DuringProjectWaitAsync(ProjectDefinitionHttpCurrentWriteFixture fixture,
        ProjectDefinitionRequest<ReplaceProjectDefinitionCommand> request, Func<Task> change) {
        await using var guard = await fixture.HoldProjectAsync();
        var pending = fixture.SendAsync(request);
        var changed = false;
        HttpResponseMessage response;
        try {
            await guard.RequireBlockedAsync(fixture.Probe);
            await change();
            changed = true;
        } finally {
            await guard.DisposeAsync();
            response = await pending;
            if (!changed) {
                response.Dispose();
            }
        }
        return response;
    }

    private static async Task<HttpResponseMessage> DuringFinalGuardAsync(ProjectDefinitionHttpCurrentWriteFixture fixture,
        ProjectDefinitionRequest<ReplaceProjectDefinitionCommand> request, Func<Task> change) {
        var pending = fixture.SendAsync(request);
        var changed = false;
        HttpResponseMessage response;
        try {
            await fixture.Probe.Paused.Task.WaitAsync(Timeout);
            await change();
            changed = true;
        } finally {
            fixture.Probe.Release.TrySetResult();
            response = await pending;
            if (!changed) {
                response.Dispose();
            }
        }
        return response;
    }

    private static async Task AssertRolledBackAsync(ProjectDefinitionHttpCurrentWriteFixture fixture, string before) {
        Assert.Equal(0, fixture.Probe.CommitAttempts);
        Assert.Equal(0, fixture.Probe.Commits);
        Assert.Equal(0, fixture.Users.ReadsUnderGuard);
        Assert.False(fixture.Probe.GuardHeld);
        await using (var released = await fixture.HoldProjectAsync()) {
            Assert.False(fixture.Probe.GuardHeld);
        }
        Assert.Equal(before, await fixture.StateAsync());
    }
}
