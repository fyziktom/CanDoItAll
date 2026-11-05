using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.SharedKernel;
using EditorModel = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;
using RuntimeProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;
using RuntimeKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using CanDoItAll.Editing;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ProviderEditorOperationsTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_acceptance_requires_explicit_current_review_and_never_replays_the_previous_submission(bool unconfirmed) {
        var reads = new Reads();
        using var session = await NewSession(reads);
        var commands = new Commands(reads.Id) {
            Save = (submission, _) => {
                reads.Id = submission.CreateRequest().Id!.Value;
                return Task.FromResult(new ProviderWriteResult(unconfirmed ? ProviderWriteDisposition.Unconfirmed : ProviderWriteDisposition.Committed, reads.Id));
            }
        };
        var operations = new ProviderEditorOperations(session, commands);
        await operations.SaveAsync();
        session.Draft.Name = "Retained local name";
        session.Draft.ConfigurationJson = "{unfinished";
        session.Editor.PendingTag = "unfinished tag";
        var context = session.EditContext;
        Assert.True(operations.CanReviewCurrent);

        await operations.ReviewCurrentAsync();
        Assert.Null(operations.ApplyCurrentReview());
        Assert.True(operations.WritesBlocked);
        session.RecoveryReview!.KeepDraft = true;
        var feedback = operations.ApplyCurrentReview();

        Assert.NotNull(feedback);
        Assert.Same(context, session.EditContext);
        Assert.Equal("Retained local name", session.Draft.Name);
        Assert.Equal("{unfinished", session.Draft.ConfigurationJson);
        Assert.Equal("unfinished tag", session.Editor.PendingTag);
        Assert.Equal(reads.Token, session.Original!.ExpectedConcurrencyToken);
        Assert.Equal(reads.Token, session.Draft.ExpectedConcurrencyToken);
        Assert.Equal(reads.Id, session.Draft.Id);
        Assert.Equal(unconfirmed ? 1 : 0, session.Recovery.ReviewedUnconfirmed.Count);
        Assert.False(operations.IsWriteUnconfirmed);
        Assert.Equal(1, commands.Writes);
        await operations.SaveAsync();
        Assert.Equal(1, commands.Writes);
        session.Draft.ConfigurationJson = "{}";
        commands.Save = (submission, _) => {
            Assert.Equal(ProviderMutationKind.Update, submission.Attempt.Kind);
            Assert.Equal(reads.Token, submission.CreateRequest().ExpectedConcurrencyToken);
            return Task.FromResult(new ProviderWriteResult(ProviderWriteDisposition.Conflict));
        };
        await operations.SaveAsync();
        Assert.Equal(2, commands.Writes);
        Assert.True(session.NeedsComparison);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Recovery_review_is_invalidated_by_a_newer_hint_or_input(bool hint) {
        var reads = new Reads();
        using var session = await NewSession(reads);
        var commands = new Commands(reads.Id);
        var operations = new ProviderEditorOperations(session, commands);
        await operations.SaveAsync();
        await operations.ReviewCurrentAsync();
        session.RecoveryReview!.KeepDraft = true;
        if (hint) {
            session.RequireComparison();
        } else {
            session.EditContext.NotifyFieldChanged(new(session.Draft, nameof(EditorModel.Name)));
        }
        Assert.Null(operations.ApplyCurrentReview());
        Assert.True(session.RecoveryReview.IsStale);
        Assert.True(operations.WritesBlocked);
        Assert.Equal(1, commands.Writes);
    }

    [Fact]
    public async Task Unavailable_recovery_read_preserves_the_attempt_and_draft() {
        var reads = new Reads();
        using var session = await NewSession(reads);
        var commands = new Commands(reads.Id) {
            Save = (_, _) => Task.FromResult(new ProviderWriteResult(ProviderWriteDisposition.Unconfirmed))
        };
        var operations = new ProviderEditorOperations(session, commands);
        await operations.SaveAsync();
        var context = session.EditContext;
        var attempt = session.Recovery.Find(null);
        reads.FailEditor = true;

        await Assert.ThrowsAsync<IOException>(() => operations.ReviewCurrentAsync());

        Assert.Same(context, session.EditContext);
        Assert.Same(attempt, session.Recovery.Find(null));
        Assert.Null(session.RecoveryReview);
        Assert.True(operations.WritesBlocked);
        Assert.Equal(1, commands.Writes);
    }

    [Fact]
    public async Task Recovery_keeps_the_native_receipt_intact_and_correlates_the_local_dispatch_separately() {
        var reads = new Reads();
        using var session = await NewSession(reads);
        Guid dispatchId = default;
        ProviderMutationAttempt? native = null;
        var commands = new Commands(reads.Id) {
            Save = (submission, _) => {
                dispatchId = submission.Attempt!.AttemptId;
                native = new(Guid.NewGuid(), submission.Attempt.ProviderId, ProviderMutationKind.Create,
                    Guid.Empty, Guid.NewGuid(), "native-fingerprint", "opaque-native-witness");
                return Task.FromResult(new ProviderWriteResult(ProviderWriteDisposition.Unconfirmed, native.ProviderId, Attempt: native));
            },
            Verify = (attempt, _) => {
                Assert.Same(native, attempt);
                return Task.FromResult(new ProviderMutationVerification(ProviderVerificationDisposition.StillUnconfirmed, attempt.ProviderId));
            }
        };
        var operations = new ProviderEditorOperations(session, commands);
        await operations.SaveAsync();
        var retained = session.Recovery.Find(null)!;
        Assert.Same(native, retained.Attempt);
        Assert.Equal(dispatchId, retained.DispatchId);
        Assert.NotEqual(dispatchId, native!.AttemptId);
        await operations.VerifyUnconfirmedAsync();
        Assert.True(operations.IsWriteUnconfirmed);
        Assert.Equal(1, commands.Writes);
    }

    [Fact]
    public async Task Explicit_diagnostic_acknowledgement_retains_uncertainty_and_the_original_draft_without_replay() {
        var reads = new Reads();
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshAsync();
        var context = session.EditContext;
        session.Draft.Name = "Unsaved configuration";
        session.Editor.PendingTag = "unfinished tag";
        var commands = new Commands(reads.Id) {
            Health = (_, _, _) => Task.FromResult(new ProviderHealthCheckOutcome(null,
                new(ProviderWriteDisposition.Unconfirmed, reads.Id)))
        };
        var operations = new ProviderEditorOperations(session, commands);
        await operations.CheckHealthAsync();
        Assert.True(operations.WritesBlocked);
        Assert.True(operations.CanAcknowledgeDiagnostic);

        var feedback = await operations.AcknowledgeDiagnosticAsync();

        Assert.Equal(ProviderFeedbackKind.Warning, feedback!.Kind);
        Assert.Contains("remains unconfirmed", feedback.Title, StringComparison.Ordinal);
        Assert.Same(context, session.EditContext);
        Assert.Equal("Unsaved configuration", session.Draft.Name);
        Assert.Equal("unfinished tag", session.Editor.PendingTag);
        Assert.Equal(reads.Token, session.Draft.ExpectedConcurrencyToken);
        Assert.Equal(ProviderMutationKind.HealthPersistence, Assert.Single(session.Recovery.AcknowledgedDiagnostics).Kind);
        Assert.False(operations.WritesBlocked);
        Assert.Equal(1, commands.HealthChecks);
        Assert.Equal(0, commands.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pending_provider_recovery_cannot_rebind_to_another_actor_or_dataset(bool anotherActor) {
        var recovery = new ProviderEditorRecovery();
        var address = new ProviderRecoveryAddress(new(EditingScopeKind.Dataset, Guid.NewGuid()), "original-principal");
        recovery.Bind(address);
        var attempt = new ProviderMutationAttempt(Guid.NewGuid(), Guid.NewGuid(), ProviderMutationKind.Create, Guid.Empty);
        var entry = new ProviderUnresolvedAttempt(attempt, null, new(new EditorModel()), ProviderEditorSection.Connection);
        recovery.Begin(attempt);
        recovery.Retain(entry);
        var other = anotherActor ? address with { PrincipalStamp = "another-principal" }
            : address with { Scope = address.Scope with { Id = Guid.NewGuid() } };

        Assert.Throws<UnauthorizedAccessException>(() => recovery.Bind(other));
        recovery.Bind(address);
        Assert.Same(entry, recovery.Find(null));
        Assert.False(recovery.AcknowledgeDiagnostic(entry));
        Assert.Same(entry, recovery.Find(null));
    }

    [Fact]
    public async Task A_peer_hint_during_comparison_read_cannot_authorize_the_old_snapshot() {
        var reads = new Reads();
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshAsync();
        var pending = new TaskCompletionSource<EditorModel>(TaskCreationOptions.RunContinuationsAsynchronously);
        reads.PendingEditor = pending.Task;
        var comparison = session.CompareAsync();
        session.RequireComparison();
        pending.SetResult(new() { Id = reads.Id, Name = "Older read", ExpectedConcurrencyToken = reads.Token });
        await comparison;
        Assert.Null(session.Comparison);
        Assert.True(session.NeedsComparison);
        Assert.False(session.ApplyComparison());
        Assert.Equal("Canonical", session.Draft.Name);
    }

    [Fact]
    public void Exact_acceptance_adopts_owner_price_rows_while_preserving_unchanged_existing_row_identity() {
        var row = new ProviderModelTokenPriceEditorModel { Model = "first", InputPerMillionTokensUsd = 1 };
        var draft = new EditorModel { ModelPrices = [row, new() { Model = "retired" }] };
        var submission = ProviderEditorSubmission.Capture(draft);
        submission.Reconcile(draft, new() {
            ModelPrices = [new() { Model = "first", InputPerMillionTokensUsd = 2 }, new() { Model = "accepted new", InputPerMillionTokensUsd = 3 }]
        });
        Assert.Equal(["first", "accepted new"], draft.ModelPrices.Select(price => price.Model));
        Assert.Same(row, draft.ModelPrices[0]);
        Assert.Equal(2, row.InputPerMillionTokensUsd);
        Assert.Equal(3, draft.ModelPrices[1].InputPerMillionTokensUsd);
    }

    [Fact]
    public async Task Save_acknowledges_its_exact_accepted_version_and_never_a_later_peer_read() {
        var reads = new Reads();
        using var session = await NewSession(reads);
        var accepted = ProviderEditorSubmission.Copy(session.Draft);
        accepted.Id = reads.Id;
        accepted.Name = "Accepted normalized name";
        accepted.ExpectedConcurrencyToken = Guid.NewGuid();
        var commands = new Commands(reads.Id) {
            Save = (_, _) => {
                session.Draft.Notes = "Typed while saving";
                return Task.FromResult(new ProviderWriteResult(ProviderWriteDisposition.Committed, reads.Id,
                    ConcurrencyToken: accepted.ExpectedConcurrencyToken, Accepted: accepted));
            }
        };
        var context = session.EditContext;

        await new ProviderEditorOperations(session, commands).SaveAsync();

        Assert.Same(context, session.EditContext);
        Assert.Equal("Accepted normalized name", session.Draft.Name);
        Assert.Equal("Typed while saving", session.Draft.Notes);
        Assert.Equal(accepted.ExpectedConcurrencyToken, session.Draft.ExpectedConcurrencyToken);
        Assert.True(session.NeedsComparison);
        Assert.Equal(accepted.ExpectedConcurrencyToken, session.Original!.ExpectedConcurrencyToken);
    }

    [Fact]
    public async Task A_known_commit_without_accepted_values_never_adopts_a_later_read_or_enables_another_save() {
        var reads = new Reads();
        using var session = await NewSession(reads);
        var commands = new Commands(reads.Id);
        var operations = new ProviderEditorOperations(session, commands);
        var name = session.Draft.Name;

        await operations.SaveAsync();
        await operations.SaveAsync();

        Assert.Equal(reads.Id, session.Draft.Id);
        Assert.Equal(name, session.Draft.Name);
        Assert.Null(session.Draft.ExpectedConcurrencyToken);
        Assert.True(session.MissingAcceptedSnapshot);
        Assert.True(operations.WritesBlocked);
        Assert.Equal(1, commands.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Provider_comparison_rebases_to_current_without_losing_its_local_context_or_unfinished_input(bool incomplete) {
        var reads = new Reads();
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshAsync();
        var original = session.Original!;
        var context = session.EditContext;
        session.Draft.Notes = "My local notes";
        session.Editor.PendingTag = "unfinished 東京";
        if (incomplete) {
            session.Draft.ConfigurationJson = "{unclosed";
            session.Editor.Notify(nameof(session.Draft.ConfigurationJson));
        }
        reads.Token = Guid.NewGuid();
        reads.Name = "Peer name";
        session.RequireComparison();

        await session.CompareAsync();
        var review = Assert.IsType<CanDoItAll.AgentFramework.Providers.UI.ProviderEditorComparison>(session.Comparison);
        Assert.Equal(incomplete, review.HasIncompleteSettings);
        if (incomplete) {
            Assert.False(review.CanApply);
            review.KeepIncompleteSettings = true;
        }
        Assert.True(session.ApplyComparison());

        Assert.Same(context, session.EditContext);
        Assert.Equal("Peer name", session.Draft.Name);
        Assert.Equal("My local notes", session.Draft.Notes);
        Assert.Equal("", session.Original!.Notes);
        Assert.Equal(reads.Token, session.Original.ExpectedConcurrencyToken);
        Assert.NotEqual(original.ExpectedConcurrencyToken, session.Original.ExpectedConcurrencyToken);
        Assert.Equal("unfinished 東京", session.Editor.PendingTag);
        Assert.Equal(incomplete ? "{unclosed" : original.ConfigurationJson, session.Draft.ConfigurationJson);
        Assert.Equal(incomplete, session.EditContext.GetValidationMessages().Any());
        Assert.False(session.NeedsComparison);
    }

    [Fact]
    public async Task Typing_even_the_same_value_invalidates_an_open_provider_comparison() {
        var reads = new Reads();
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshAsync();
        session.RequireComparison();
        await session.CompareAsync();
        session.Editor.Notify(nameof(session.Draft.Name));

        Assert.True(session.Comparison!.IsStale);
        Assert.False(session.ApplyComparison());
        Assert.True(session.NeedsComparison);
    }

    [Fact]
    public void Confirmation_reconciles_unchanged_fields_without_erasing_later_notes_or_price_input() {
        var row = new ProviderModelTokenPriceEditorModel { Model = " model ", InputPerMillionTokensUsd = 1, OutputPerMillionTokensUsd = 2 };
        var draft = new EditorModel { Name = " Provider ", DefaultModel = " model ", ModelPrices = [row] };
        var submission = ProviderEditorSubmission.Capture(draft);
        var accepted = ProviderEditorSubmission.Copy(draft);
        accepted.Id = Guid.NewGuid();
        accepted.ExpectedConcurrencyToken = Guid.NewGuid();
        accepted.Name = "Provider";
        accepted.DefaultModel = "model";
        accepted.ModelPrices[0].Model = "model";
        accepted.ModelPrices[0].OutputPerMillionTokensUsd = 3;
        draft.Notes = "Typed after Save";
        row.InputPerMillionTokensUsd = 7;

        submission.Reconcile(draft, accepted);

        Assert.Equal("Provider", draft.Name);
        Assert.Equal("model", draft.DefaultModel);
        Assert.Equal("Typed after Save", draft.Notes);
        Assert.Same(row, Assert.Single(draft.ModelPrices));
        Assert.Equal("model", row.Model);
        Assert.Equal(7, row.InputPerMillionTokensUsd);
        Assert.Equal(3, row.OutputPerMillionTokensUsd);
        Assert.Equal(accepted.ExpectedConcurrencyToken, draft.ExpectedConcurrencyToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Known_commit_binds_identity_and_retry_only_reconciles(bool secondaryWarning) {
        var reads = new Reads();
        using var session = await NewSession(reads);
        var commands = new Commands(reads.Id) {
            Save = (_, _) => Task.FromResult(new ProviderWriteResult(ProviderWriteDisposition.Committed,
                reads.Id, secondaryWarning ? "Secondary update pending." : null, reads.Token,
                Accepted: new() { Id = reads.Id, Name = "Canonical", DefaultModel = "model", ExpectedConcurrencyToken = reads.Token }))
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
        Assert.Equal(rejection == ProviderWriteDisposition.Conflict, operations.WritesBlocked);
        Assert.Null(session.Draft.Id);
        if (rejection == ProviderWriteDisposition.Conflict) {
            Assert.True(session.NeedsComparison);
            return;
        }
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
        var callbacks = 0;
        using var delayed = token.Register(() => callbacks++);
        Assert.Equal(1, callbacks);
        Assert.True(token.WaitHandle.WaitOne(0));
        if (fails) {
            pending.SetException(new IOException("Late failure"));
        } else {
            pending.SetResult(new(ProviderWriteDisposition.Committed, reads.Id));
        }
        Assert.Null(await save);
        Assert.Throws<ObjectDisposedException>(() => token.WaitHandle);
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
        pending.SetResult(new(ProviderWriteDisposition.Committed, reads.Id, ConcurrencyToken: reads.Token,
            Accepted: new() { Id = reads.Id, Name = "Canonical", DefaultModel = "model", ExpectedConcurrencyToken = reads.Token }));
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
    public async Task A_create_without_a_publication_witness_stays_blocked_after_absence_verification() {
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
        Assert.False(operations.HasVerifiedRetry);
        Assert.True(operations.IsWriteUnconfirmed);
        Assert.Null(session.Draft.Id);
        await operations.RetryVerifiedAsync();
        await operations.RetryVerifiedAsync();
        Assert.Equal(1, commands.Writes);
        Assert.Single(ids);
        Assert.All(names, name => Assert.Equal(submittedName, name));
        Assert.Equal("Later unsaved name", session.Draft.Name);
        Assert.Null(session.Draft.Id);
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
    public async Task An_unconfirmed_absent_candidate_keeps_its_identity_and_blocks_an_explicit_retry_or_save() {
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
        Assert.Equal(1, commands.Writes);
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
        public Guid Token { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Canonical";
        public bool FailEditor { get; set; }
        public Task<EditorModel>? PendingEditor { get; set; }
        public Task<ProviderProfilesCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ProviderProfilesCatalog([new RuntimeProfile(Id, "Canonical", RuntimeKind.OpenAi,
                string.Empty, string.Empty, "model", ProviderTransportKind.Responses, true, true, true, false, true,
                "{}", string.Empty, string.Empty, null, ["model"])], new([])));
        public Task<EditorModel> LoadEditorAsync(Guid providerId, CancellationToken cancellationToken = default)
            => FailEditor ? Task.FromException<EditorModel>(new IOException("Read failed"))
                : PendingEditor ?? Task.FromResult(new EditorModel { Id = Id, Name = Name, DefaultModel = "model", ExpectedConcurrencyToken = Token });
    }

    private sealed class Commands(Guid providerId) : IProviderEditorCommands {
        public Func<ProviderMutationAttempt, CancellationToken, Task<ProviderMutationVerification>>? Verify { get; set; }
        public int Writes { get; private set; }
        public int Deletes { get; private set; }
        public int Reconciliations { get; private set; }
        public int HealthChecks { get; private set; }
        public Func<Guid, bool, CancellationToken, Task<ProviderHealthCheckOutcome>>? Health { get; set; }
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

        public Task<ProviderWriteResult> DeleteAsync(ProviderMutationAttempt attempt, CancellationToken cancellationToken)
            => DeleteAsync(attempt.ProviderId, cancellationToken);
        public Task<ProviderHealthCheckOutcome> CheckHealthAsync(Guid id, bool sourceManaged, CancellationToken cancellationToken) {
            HealthChecks++;
            return Health?.Invoke(id, sourceManaged, cancellationToken) ?? Task.FromResult(new ProviderHealthCheckOutcome(new(true, "Healthy", []), sourceManaged ? null : new(ProviderWriteDisposition.Committed, id)));
        }
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
