using ProviderProfileEditorModel = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ProviderVerificationHardeningTests {
    [Fact]
    public void Synchronize_verification_rejects_requested_subset() {
        var a = Import();
        var b = Import();
        var before = Source([]);
        var current = Applied(before, [a, b]);
        var attempt = new SharedProviderSourceMutationAttempt(before.Source.Id,
            SharedProviderSourceMutationKind.Synchronize, before, selection: [a.RemotePublicationId]);
        Assert.Equal(ProviderVerificationDisposition.StillUnconfirmed,
            SharedProviderSourceVerification.Evaluate(attempt, [current]).Disposition);
    }

    [Fact]
    public void Synchronize_verification_rejects_nonempty_current_for_empty_request() {
        var before = Source([]);
        var attempt = new SharedProviderSourceMutationAttempt(before.Source.Id,
            SharedProviderSourceMutationKind.Synchronize, before, selection: []);
        Assert.Equal(ProviderVerificationDisposition.StillUnconfirmed,
            SharedProviderSourceVerification.Evaluate(attempt, [Applied(before, [Import()])]).Disposition);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Terminal_provider_attempt_cannot_be_resurrected(bool remove) {
        var recovery = new ProviderEditorRecovery();
        var attempt = new ProviderMutationAttempt(Guid.NewGuid(), Guid.NewGuid(), ProviderMutationKind.Create, Guid.Empty);
        var entry = new ProviderUnresolvedAttempt(attempt, null, new EditContext(new ProviderProfileEditorModel()), ProviderEditorSection.Connection);
        recovery.Begin(attempt);
        recovery.Retain(entry);
        if (remove) {
            Assert.True(recovery.Remove(entry));
        } else {
            recovery.Complete(attempt);
        }
        recovery.Retain(entry);
        Assert.Null(recovery.Find(attempt.ProviderId));
    }

    [Fact]
    public void Synchronize_verification_requires_exact_selected_set() {
        var a = Import();
        var b = Import();
        var before = Source([]);
        var attempt = new SharedProviderSourceMutationAttempt(before.Source.Id,
            SharedProviderSourceMutationKind.Synchronize, before, selection: [b.RemotePublicationId, a.RemotePublicationId, a.RemotePublicationId]);
        Assert.Equal(ProviderVerificationDisposition.Committed,
            SharedProviderSourceVerification.Evaluate(attempt, [Applied(before, [a, b])]).Disposition);
        Assert.Equal(ProviderVerificationDisposition.StillUnconfirmed,
            SharedProviderSourceVerification.Evaluate(attempt, [Applied(before, [a])]).Disposition);
    }

    [Fact]
    public void Synchronize_verification_accepts_exact_empty_selection() {
        var before = Source([]);
        var attempt = new SharedProviderSourceMutationAttempt(before.Source.Id, SharedProviderSourceMutationKind.Synchronize, before);
        Assert.Equal(ProviderVerificationDisposition.Committed,
            SharedProviderSourceVerification.Evaluate(attempt, [Applied(before, [])]).Disposition);
    }

    [Fact]
    public void Synchronize_verification_ignores_retired_imports_in_selected_set() {
        var a = Import();
        var retired = Import() with { SelectionState = SharedProviderSelectionState.Retired };
        var before = Source([]);
        var attempt = new SharedProviderSourceMutationAttempt(before.Source.Id,
            SharedProviderSourceMutationKind.Synchronize, before, selection: [a.RemotePublicationId]);
        Assert.Equal(ProviderVerificationDisposition.Committed,
            SharedProviderSourceVerification.Evaluate(attempt, [Applied(before, [a, retired])]).Disposition);
        var empty = new SharedProviderSourceMutationAttempt(before.Source.Id, SharedProviderSourceMutationKind.Synchronize, before);
        Assert.Equal(ProviderVerificationDisposition.Committed,
            SharedProviderSourceVerification.Evaluate(empty, [Applied(before, [retired])]).Disposition);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Synchronize_verification_keeps_existing_revision_and_time_evidence(bool unchangedRevision, bool unchangedTime, bool neverSynchronized) {
        var before = Source([]);
        var attempt = new SharedProviderSourceMutationAttempt(before.Source.Id, SharedProviderSourceMutationKind.Synchronize, before);
        var current = Applied(before, []);
        current = current with { Source = current.Source with {
            ConcurrencyToken = unchangedRevision ? before.Source.ConcurrencyToken : current.Source.ConcurrencyToken,
            LastSyncAtUtc = unchangedTime ? before.Source.LastSyncAtUtc : current.Source.LastSyncAtUtc,
            Status = neverSynchronized ? SharedProviderSourceStatus.NeverSynchronized : current.Source.Status } };
        Assert.NotEqual(ProviderVerificationDisposition.Committed, SharedProviderSourceVerification.Evaluate(attempt, [current]).Disposition);
    }

    [Fact]
    public void Wrong_source_identity_does_not_establish_synchronization() {
        var before = Source([]);
        var attempt = new SharedProviderSourceMutationAttempt(before.Source.Id, SharedProviderSourceMutationKind.Synchronize, before);
        var current = Applied(before, []);
        current = current with { Source = current.Source with { Id = Guid.NewGuid() } };
        Assert.Equal(ProviderVerificationDisposition.StillUnconfirmed,
            SharedProviderSourceVerification.Evaluate(attempt, [current]).Disposition);
    }

    [Fact]
    public void Stale_provider_completion_cannot_clear_newer_attempt() {
        var recovery = new ProviderEditorRecovery();
        var old = new ProviderMutationAttempt(Guid.NewGuid(), Guid.NewGuid(), ProviderMutationKind.Update, Guid.NewGuid());
        var current = old with { AttemptId = Guid.NewGuid() };
        var context = new EditContext(new ProviderProfileEditorModel());
        var oldEntry = new ProviderUnresolvedAttempt(old, null, context, ProviderEditorSection.Runtime);
        var currentEntry = oldEntry with { Attempt = current };
        recovery.Begin(old);
        recovery.Retain(oldEntry);
        recovery.Begin(current);
        recovery.Retain(currentEntry);
        recovery.Complete(old);
        recovery.Retain(oldEntry);
        Assert.False(recovery.Remove(oldEntry));
        Assert.Same(currentEntry, recovery.Find(current.ProviderId));
    }

    internal static SharedProviderImportedProfileSnapshot Import() {
        var publication = new SharedProviderPublicationId(Guid.NewGuid());
        return new(Guid.NewGuid(), Guid.NewGuid(), "Fixture source", publication, Guid.NewGuid(),
            "Local alias", true, "Remote", SharedProviderPurpose.Chat, SharedProviderTransport.OpenAiCompatible,
            SharedProviderRoutingModelIdCodec.Create(publication, "model"), SharedProviderSelectionState.Selected,
            SharedProviderAvailabilityState.Available, [], Guid.NewGuid(), Guid.NewGuid());
    }

    internal static SharedProviderSourceManagementSnapshot Source(IReadOnlyList<SharedProviderImportedProfileSnapshot> imports) =>
        new(new(Guid.NewGuid(), "Source", new Uri("https://source.example.test"), Guid.NewGuid(), true,
            SharedProviderSourceNetworkPolicy.PublicOnly, SharedProviderSourceStatus.Available, null, null,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"), null, "", Guid.NewGuid()), imports);

    internal static SharedProviderSourceManagementSnapshot Applied(SharedProviderSourceManagementSnapshot before,
        IReadOnlyList<SharedProviderImportedProfileSnapshot> imports) => before with {
            Source = before.Source with { ConcurrencyToken = Guid.NewGuid(), LastSyncAtUtc = before.Source.LastSyncAtUtc!.Value.AddMinutes(1) },
            Imports = imports
        };
}
