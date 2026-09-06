using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.SharedKernel;
using EditorModel = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;
using RuntimeProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;
using RuntimeKind = CanDoItAll.AgentFramework.Models.ProviderKind;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ProviderEditorOperationsTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Known_commit_binds_identity_and_retry_only_reconciles(bool secondaryWarning) {
        var reads = new Reads();
        using var session = await NewSession(reads);
        var commands = new Commands(reads.Id) {
            Save = (_, _) => Task.FromResult(new ProviderWriteResult(ProviderWriteDisposition.Committed,
                reads.Id, secondaryWarning ? "Secondary update pending." : null))
        };
        var operations = new ProviderEditorOperations(session, commands);
        var context = session.EditContext;
        reads.FailEditor = true;
        await operations.SaveAsync();
        Assert.Equal(reads.Id, session.State.ProviderId);
        Assert.Equal(reads.Id, session.Draft.Id);
        Assert.True(operations.HasPendingReconciliation);
        Assert.True(operations.WritesBlocked);
        Assert.False(operations.IsBusy);
        reads.FailEditor = false;
        await operations.RetryReconciliationAsync();
        Assert.Equal(1, commands.Writes);
        Assert.Equal(secondaryWarning ? 1 : 0, commands.Reconciliations);
        Assert.False(operations.HasPendingReconciliation);
        Assert.Same(context, session.EditContext);
        Assert.Equal(reads.Token, session.Draft.ExpectedConcurrencyToken);
    }

    [Theory]
    [InlineData(ProviderWriteDisposition.Rejected)]
    [InlineData(ProviderWriteDisposition.Conflict)]
    public async Task Known_rejection_preserves_correctable_draft(ProviderWriteDisposition rejection) {
        var reads = new Reads();
        using var session = await NewSession(reads);
        var commands = new Commands(reads.Id) {
            Save = (_, _) => Task.FromResult(new ProviderWriteResult(rejection, Message: "Rejected"))
        };
        var operations = new ProviderEditorOperations(session, commands);
        await operations.SaveAsync();
        Assert.False(operations.WritesBlocked);
        Assert.Null(session.Draft.Id);
        session.Draft.Name = "Corrected";
        commands.Save = (_, _) => Task.FromResult(new ProviderWriteResult(ProviderWriteDisposition.Committed, reads.Id));
        await operations.SaveAsync();
        Assert.Equal(2, commands.Writes);
        Assert.Equal(reads.Id, session.Draft.Id);
    }

    [Fact]
    public async Task Unconfirmed_write_blocks_blind_replay() {
        var reads = new Reads();
        using var session = await NewSession(reads);
        var commands = new Commands(reads.Id) {
            Save = (_, _) => Task.FromResult(new ProviderWriteResult(ProviderWriteDisposition.Unconfirmed))
        };
        var operations = new ProviderEditorOperations(session, commands);
        await operations.SaveAsync();
        await operations.SaveAsync();
        Assert.Equal(1, commands.Writes);
        Assert.True(operations.IsWriteUnconfirmed);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task New_or_disposal_cancels_pending_save_and_fences_late_completion(bool dispose, bool fails) {
        var reads = new Reads();
        using var session = await NewSession(reads);
        var pending = new TaskCompletionSource<ProviderWriteResult>();
        CancellationToken token = default;
        var commands = new Commands(reads.Id) { Save = (_, owner) => {
            token = owner;
            return pending.Task;
        } };
        var operations = new ProviderEditorOperations(session, commands);
        var save = operations.SaveAsync();
        if (dispose) {
            session.Dispose();
        } else {
            await session.NewAsync();
            session.Draft.Name = "New target";
        }
        Assert.True(token.IsCancellationRequested);
        if (fails) {
            pending.SetException(new IOException("Late failure"));
        } else {
            pending.SetResult(new(ProviderWriteDisposition.Committed, reads.Id));
        }
        Assert.Null(await save);
        Assert.False(operations.IsBusy);
        Assert.False(operations.HasPendingReconciliation);
        if (!dispose) {
            Assert.Equal("New target", session.Draft.Name);
            Assert.Null(session.Draft.Id);
        }
    }

    [Fact]
    public async Task Late_previous_save_cannot_clear_new_target_busy_state() {
        var reads = new Reads();
        using var session = await NewSession(reads);
        var first = new TaskCompletionSource<ProviderWriteResult>();
        var second = new TaskCompletionSource<ProviderWriteResult>();
        var commands = new Commands(reads.Id) { Save = (_, _) => first.Task };
        var operations = new ProviderEditorOperations(session, commands);
        var old = operations.SaveAsync();
        await session.NewAsync();
        session.Draft.DefaultModel = "model";
        commands.Save = (_, _) => second.Task;
        var current = operations.SaveAsync();
        first.SetResult(new(ProviderWriteDisposition.Committed, reads.Id));
        Assert.Null(await old);
        Assert.True(operations.IsBusy);
        second.SetResult(new(ProviderWriteDisposition.Rejected));
        await current;
        Assert.False(operations.IsBusy);
    }

    [Fact]
    public async Task Later_draft_edits_survive_successful_save_reconciliation() {
        var reads = new Reads();
        using var session = await NewSession(reads);
        var pending = new TaskCompletionSource<ProviderWriteResult>();
        var commands = new Commands(reads.Id) { Save = (_, _) => pending.Task };
        var operations = new ProviderEditorOperations(session, commands);
        var context = session.EditContext;
        var save = operations.SaveAsync();
        session.Draft.Name = "Later";
        pending.SetResult(new(ProviderWriteDisposition.Committed, reads.Id));
        await save;
        Assert.Equal("Later", session.Draft.Name);
        Assert.Same(context, session.EditContext);
        Assert.Equal(reads.Token, session.Draft.ExpectedConcurrencyToken);
    }

    [Fact]
    public async Task Delete_commit_warning_keeps_deleted_target_and_retry_does_not_delete_again() {
        var reads = new Reads();
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshAsync();
        var commands = new Commands(reads.Id) {
            Delete = (_, _) => Task.FromResult(new ProviderWriteResult(ProviderWriteDisposition.Committed, reads.Id, "Projection pending"))
        };
        var operations = new ProviderEditorOperations(session, commands);
        await operations.DeleteAsync();
        Assert.Equal(reads.Id, session.State.ProviderId);
        Assert.Equal(ProviderProfilesLoadState.Failed, session.EditorLoadState);
        await operations.RetryReconciliationAsync();
        Assert.Equal(1, commands.Deletes);
        Assert.Equal(1, commands.Reconciliations);
    }

    [Fact]
    public async Task Health_reconciliation_does_not_replace_an_unsaved_local_draft() {
        var reads = new Reads();
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshAsync();
        session.Draft.Name = "Unsaved health draft";
        var operations = new ProviderEditorOperations(session, new Commands(reads.Id));
        await operations.CheckHealthAsync();
        Assert.Equal("Unsaved health draft", session.Draft.Name);
        Assert.Equal(reads.Token, session.Draft.ExpectedConcurrencyToken);
    }

    [Fact]
    public async Task Discovery_does_not_apply_to_a_changed_draft() {
        var reads = new Reads();
        using var session = await NewSession(reads);
        var pending = new TaskCompletionSource<Result<ProviderModelPricingRefreshResult>>();
        var commands = new Commands(reads.Id) { Discover = (_, _) => pending.Task };
        var operations = new ProviderEditorOperations(session, commands);
        var discovery = operations.DiscoverModelsAsync();
        session.Draft.Name = "Keep later edit";
        pending.SetResult(Result<ProviderModelPricingRefreshResult>.Success(new([], 1, 0, 1, "Loaded") { Models = ["remote"] }));
        Assert.Equal(ProviderFeedbackKind.Warning, (await discovery)!.Kind);
        Assert.Equal("Keep later edit", session.Draft.Name);
        Assert.Empty(session.Draft.ModelPrices);
        Assert.DoesNotContain("remote", session.Draft.SuggestedModels);
    }

    [Fact]
    public async Task Unconfirmed_first_save_exposes_stable_candidate_identity() {
        var reads = new Reads();
        using var session = await NewSession(reads);
        Guid? candidate = null;
        var commands = new Commands(reads.Id) {
            Save = (submission, _) => {
                candidate = submission.CreateRequest().Id;
                return Task.FromResult(new ProviderWriteResult(ProviderWriteDisposition.Unconfirmed));
            }
        };
        var operations = new ProviderEditorOperations(session, commands);
        await operations.SaveAsync();
        Assert.NotNull(candidate);
        Assert.NotEqual(Guid.Empty, candidate);
        Assert.Null(session.Draft.Id);
        Assert.True(operations.IsWriteUnconfirmed);
    }

    [Fact]
    public async Task Selection_and_new_do_not_discard_unresolved_protection() {
        var reads = new Reads();
        using var session = await NewSession(reads);
        var commands = new Commands(reads.Id) {
            Save = (_, _) => Task.FromResult(new ProviderWriteResult(ProviderWriteDisposition.Unconfirmed))
        };
        var operations = new ProviderEditorOperations(session, commands);
        await operations.SaveAsync();
        await session.SelectAsync(reads.Id);
        await session.NewAsync();
        session.Draft.DefaultModel = "model";
        await operations.SaveAsync();
        Assert.Equal(1, commands.Writes);
        Assert.True(operations.IsWriteUnconfirmed);
    }

    [Fact]
    public async Task Verification_binds_committed_candidate_without_replaying_save() {
        var reads = new Reads();
        using var session = await NewSession(reads);
        var commands = new Commands(reads.Id) {
            Save = (submission, _) => {
                reads.Id = submission.CreateRequest().Id!.Value;
                return Task.FromResult(new ProviderWriteResult(ProviderWriteDisposition.Unconfirmed));
            },
            Verify = (attempt, _) => Task.FromResult(new ProviderMutationVerification(
                ProviderVerificationDisposition.Committed, attempt.ProviderId, reads.Token))
        };
        var operations = new ProviderEditorOperations(session, commands);
        var context = session.EditContext;
        await operations.SaveAsync();
        Assert.Null(session.Draft.Id);
        await operations.VerifyUnconfirmedAsync();
        Assert.Equal(reads.Id, session.Draft.Id);
        Assert.Equal(reads.Token, session.Draft.ExpectedConcurrencyToken);
        Assert.Same(context, session.EditContext);
        Assert.False(operations.WritesBlocked);
        Assert.Equal(1, commands.Writes);
        Assert.Equal(1, commands.Reconciliations);
    }

    [Fact]
    public async Task Verified_absence_allows_same_candidate_controlled_retry() {
        var reads = new Reads();
        using var session = await NewSession(reads);
        var ids = new List<Guid>();
        var names = new List<string>();
        var commands = new Commands(reads.Id) {
            Save = (submission, _) => {
                var request = submission.CreateRequest();
                reads.Id = request.Id!.Value;
                ids.Add(reads.Id);
                names.Add(request.Name);
                return Task.FromResult(new ProviderWriteResult(ids.Count == 1
                    ? ProviderWriteDisposition.Unconfirmed : ProviderWriteDisposition.Committed, reads.Id));
            },
            Verify = (attempt, _) => Task.FromResult(new ProviderMutationVerification(
                ProviderVerificationDisposition.DefinitelyNotCommitted, attempt.ProviderId))
        };
        var operations = new ProviderEditorOperations(session, commands);
        var submittedName = session.Draft.Name;
        await operations.SaveAsync();
        session.Draft.Name = "Later unsaved name";
        await operations.VerifyUnconfirmedAsync();
        Assert.True(operations.HasVerifiedRetry);
        Assert.Null(session.Draft.Id);
        await operations.RetryVerifiedAsync();
        await operations.RetryVerifiedAsync();
        Assert.Equal(2, commands.Writes);
        Assert.Equal(ids[0], ids[1]);
        Assert.All(names, name => Assert.Equal(submittedName, name));
        Assert.Equal("Later unsaved name", session.Draft.Name);
        Assert.Equal(ids[0], session.Draft.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_verification_remains_unresolved(bool throws) {
        var reads = new Reads();
        using var session = await NewSession(reads);
        var commands = new Commands(reads.Id) {
            Save = (_, _) => Task.FromResult(new ProviderWriteResult(ProviderWriteDisposition.Unconfirmed)),
            Verify = (attempt, _) => throws
                ? Task.FromException<ProviderMutationVerification>(new IOException("Synthetic unavailable canonical database."))
                : Task.FromResult(new ProviderMutationVerification(ProviderVerificationDisposition.StillUnconfirmed, attempt.ProviderId))
        };
        var operations = new ProviderEditorOperations(session, commands);
        await operations.SaveAsync();
        await operations.VerifyUnconfirmedAsync();
        await operations.SaveAsync();
        Assert.True(operations.IsWriteUnconfirmed);
        Assert.Equal(1, commands.Writes);
        Assert.Null(session.Draft.Id);
    }

    [Fact]
    public async Task Later_edits_and_edit_context_survive_verification() {
        var reads = new Reads();
        using var session = await NewSession(reads);
        var verification = new TaskCompletionSource<ProviderMutationVerification>();
        var commands = new Commands(reads.Id) {
            Save = (submission, _) => {
                reads.Id = submission.CreateRequest().Id!.Value;
                return Task.FromResult(new ProviderWriteResult(ProviderWriteDisposition.Unconfirmed));
            },
            Verify = (_, _) => verification.Task
        };
        var operations = new ProviderEditorOperations(session, commands);
        var context = session.EditContext;
        await operations.SaveAsync();
        session.SelectSection(ProviderEditorSection.Runtime);
        var pending = operations.VerifyUnconfirmedAsync();
        session.Draft.Name = "Edited while verifying";
        verification.SetResult(new(ProviderVerificationDisposition.Committed, reads.Id, reads.Token));
        await pending;
        Assert.Same(context, session.EditContext);
        Assert.Equal("Edited while verifying", session.Draft.Name);
        Assert.Equal(ProviderEditorSection.Runtime, session.State.Section);
        Assert.Equal(reads.Id, session.Draft.Id);
        Assert.Equal(1, commands.Writes);
    }

    [Fact]
    public async Task Recreated_provider_owner_keeps_unresolved_attempt() {
        var reads = new Reads();
        var recovery = new ProviderEditorRecovery();
        using var first = new ProviderProfilesSession(reads, recovery);
        await first.RefreshAsync();
        await first.NewAsync();
        first.Draft.DefaultModel = "model";
        var context = first.EditContext;
        var commands = new Commands(reads.Id) {
            Save = (_, _) => Task.FromResult(new ProviderWriteResult(ProviderWriteDisposition.Unconfirmed))
        };
        await new ProviderEditorOperations(first, commands).SaveAsync();
        first.Dispose();
        using var reopened = new ProviderProfilesSession(reads, recovery);
        await reopened.RefreshAsync();
        await reopened.NewAsync();
        var operations = new ProviderEditorOperations(reopened, commands);
        Assert.Same(context, reopened.EditContext);
        await operations.SaveAsync();
        Assert.True(operations.IsWriteUnconfirmed);
        Assert.Equal(1, commands.Writes);
    }

    [Fact]
    public async Task Candidate_conflict_after_absence_requires_verification_before_new_identity() {
        var reads = new Reads();
        using var session = await NewSession(reads);
        var commands = new Commands(reads.Id) {
            Save = (_, _) => Task.FromResult(new ProviderWriteResult(ProviderWriteDisposition.Unconfirmed)),
            Verify = (attempt, _) => Task.FromResult(new ProviderMutationVerification(
                ProviderVerificationDisposition.DefinitelyNotCommitted, attempt.ProviderId))
        };
        var operations = new ProviderEditorOperations(session, commands);
        await operations.SaveAsync();
        var candidate = operations.CandidateProviderId;
        await operations.VerifyUnconfirmedAsync();
        commands.Save = (_, _) => Task.FromResult(new ProviderWriteResult(ProviderWriteDisposition.Conflict));
        await operations.RetryVerifiedAsync();
        await operations.SaveAsync();
        Assert.Equal(2, commands.Writes);
        Assert.True(operations.IsWriteUnconfirmed);
        Assert.Equal(candidate, operations.CandidateProviderId);
    }

    private static async Task<ProviderProfilesSession> NewSession(Reads reads) {
        var session = new ProviderProfilesSession(reads);
        await session.RefreshAsync();
        await session.NewAsync();
        session.Draft.DefaultModel = "model";
        return session;
    }

    private sealed class Reads : IProviderProfilesReads {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid Token { get; } = Guid.NewGuid();
        public bool FailEditor { get; set; }
        public Task<ProviderProfilesCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ProviderProfilesCatalog([new RuntimeProfile(Id, "Canonical", RuntimeKind.OpenAi,
                string.Empty, string.Empty, "model", ProviderTransportKind.Responses, true, true, true, false, true,
                "{}", string.Empty, string.Empty, null, ["model"])], new([])));
        public Task<EditorModel> LoadEditorAsync(Guid providerId, CancellationToken cancellationToken = default)
            => FailEditor ? Task.FromException<EditorModel>(new IOException("Read failed"))
                : Task.FromResult(new EditorModel { Id = Id, Name = "Canonical", DefaultModel = "model", ExpectedConcurrencyToken = Token });
    }

    private sealed class Commands(Guid providerId) : IProviderEditorCommands {
        public Func<ProviderMutationAttempt, CancellationToken, Task<ProviderMutationVerification>>? Verify { get; set; }
        public int Writes { get; private set; }
        public int Deletes { get; private set; }
        public int Reconciliations { get; private set; }
        public Func<ProviderEditorSubmission, CancellationToken, Task<ProviderWriteResult>>? Save { get; set; }
        public Func<Guid, CancellationToken, Task<ProviderWriteResult>>? Delete { get; set; }
        public Func<ProviderEditorSubmission, CancellationToken, Task<Result<ProviderModelPricingRefreshResult>>>? Discover { get; set; }
        public Task<ProviderWriteResult> SaveAsync(ProviderEditorSubmission submission, CancellationToken cancellationToken) {
            Writes++;
            return Save?.Invoke(submission, cancellationToken) ?? Task.FromResult(new ProviderWriteResult(ProviderWriteDisposition.Committed, providerId));
        }
        public Task<ProviderWriteResult> DeleteAsync(Guid id, CancellationToken cancellationToken) {
            Deletes++;
            return Delete!.Invoke(id, cancellationToken);
        }
        public Task<ProviderHealthCheckOutcome> CheckHealthAsync(Guid id, bool sourceManaged, CancellationToken cancellationToken)
            => Task.FromResult(new ProviderHealthCheckOutcome(new(true, "Healthy", []), sourceManaged ? null : new(ProviderWriteDisposition.Committed, id)));
        public Task<Result<ProviderModelPricingRefreshResult>> DiscoverModelsAsync(ProviderEditorSubmission submission, CancellationToken cancellationToken)
            => Discover!.Invoke(submission, cancellationToken);
        public Task<ProviderMutationVerification> VerifyAsync(ProviderMutationAttempt attempt, CancellationToken cancellationToken)
            => Verify?.Invoke(attempt, cancellationToken) ?? throw new InvalidOperationException("This test does not request verification.");
        public Task ReconcileAsync(Guid id, CancellationToken cancellationToken) {
            Reconciliations++;
            return Task.CompletedTask;
        }
    }
}
