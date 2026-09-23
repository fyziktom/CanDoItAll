using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class SharedTargetVerificationTests {
    [Theory]
    [InlineData(true, true, SharedProviderTargetVerificationDisposition.Satisfied)]
    [InlineData(true, false, SharedProviderTargetVerificationDisposition.StillUnconfirmed)]
    [InlineData(false, false, SharedProviderTargetVerificationDisposition.Satisfied)]
    [InlineData(false, true, SharedProviderTargetVerificationDisposition.StillUnconfirmed)]
    public void Publication_verification_requires_exact_postcondition(bool publish, bool currentPublished,
        SharedProviderTargetVerificationDisposition expected) {
        var before = Local(!publish);
        var attempt = Attempt(before, publish ? SharedProviderTargetMutationKind.Publish : SharedProviderTargetMutationKind.Unpublish);
        var current = before with { Publication = before.Publication! with {
            IsPublished = currentPublished, ConcurrencyToken = Guid.NewGuid() } };
        Assert.Equal(expected, SharedProviderTargetVerification.Evaluate(attempt, current).Disposition);
    }

    [Theory]
    [InlineData(SharedProviderTargetMutationKind.Publish)]
    [InlineData(SharedProviderTargetMutationKind.Unpublish)]
    [InlineData(SharedProviderTargetMutationKind.ImportedSettings)]
    [InlineData(SharedProviderTargetMutationKind.Retirement)]
    public void Exact_before_state_allows_deliberate_retry_without_replay(SharedProviderTargetMutationKind kind) {
        var before = kind is SharedProviderTargetMutationKind.Publish or SharedProviderTargetMutationKind.Unpublish
            ? Local(kind == SharedProviderTargetMutationKind.Unpublish) : Imported();
        var request = before.Import is { } import ? new SharedProviderImportedProfileUpdateRequest(import.ImportId,
            import.ProviderProfileId, "Requested alias", false, import.ImportConcurrencyToken, import.ProviderConcurrencyToken) : null;
        var attempt = Attempt(before, kind, request);
        var result = SharedProviderTargetVerification.Evaluate(attempt, before);
        Assert.Equal(SharedProviderTargetVerificationDisposition.NotApplied, result.Disposition);
        Assert.Null(result.Change);
    }

    [Fact]
    public void First_publication_satisfaction_binds_permanent_identity() {
        var before = Local(false) with { Publication = null };
        var attempt = Attempt(before, SharedProviderTargetMutationKind.Publish);
        var current = Local(true) with { ProviderProfileId = before.ProviderProfileId };
        Assert.Equal(SharedProviderTargetVerificationDisposition.Satisfied,
            SharedProviderTargetVerification.Evaluate(attempt, current).Disposition);
    }

    [Theory]
    [InlineData("Requested alias", false, true)]
    [InlineData("Different alias", false, false)]
    [InlineData("Requested alias", true, false)]
    public void Imported_settings_verification_requires_exact_alias_and_enabled_state(string alias, bool enabled, bool satisfied) {
        var before = Imported();
        var import = before.Import!;
        var request = new SharedProviderImportedProfileUpdateRequest(import.ImportId, import.ProviderProfileId,
            "  Requested alias  ", false, import.ImportConcurrencyToken, import.ProviderConcurrencyToken);
        var attempt = Attempt(before, SharedProviderTargetMutationKind.ImportedSettings, request);
        var current = before with { Import = import with {
            LocalAlias = alias, IsEnabled = enabled, ImportConcurrencyToken = Guid.NewGuid(), ProviderConcurrencyToken = Guid.NewGuid() } };
        var result = SharedProviderTargetVerification.Evaluate(attempt, current);
        Assert.Equal(satisfied ? SharedProviderTargetVerificationDisposition.Satisfied : SharedProviderTargetVerificationDisposition.StillUnconfirmed,
            result.Disposition);
        Assert.Equal("Requested alias", Assert.IsType<SharedProviderTargetPostcondition.ImportedSettings>(attempt.Intended).LocalAlias);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Retirement_verification_requires_exact_import_identity_and_retired_state(bool correctId, bool retired) {
        var before = Imported();
        var attempt = Attempt(before, SharedProviderTargetMutationKind.Retirement);
        var current = before with { Import = before.Import! with {
            ImportId = correctId ? before.Import.ImportId : Guid.NewGuid(),
            SelectionState = retired ? SharedProviderSelectionState.Retired : SharedProviderSelectionState.Selected,
            ImportConcurrencyToken = Guid.NewGuid(), ProviderConcurrencyToken = Guid.NewGuid() } };
        var result = SharedProviderTargetVerification.Evaluate(attempt, current);
        Assert.Equal(correctId && retired ? SharedProviderTargetVerificationDisposition.Satisfied
            : SharedProviderTargetVerificationDisposition.StillUnconfirmed, result.Disposition);
        if (correctId && retired) {
            Assert.Contains(before.ProviderProfileId, result.Change!.RetiredProviderProfileIds);
        }
    }

    [Fact]
    public void Wrong_target_or_publication_identity_cannot_unlock() {
        var before = Local(false);
        var attempt = Attempt(before, SharedProviderTargetMutationKind.Publish);
        var differentProvider = before with { ProviderProfileId = Guid.NewGuid() };
        var differentPublication = before with { Publication = before.Publication! with {
            Id = Guid.NewGuid(), IsPublished = true, ConcurrencyToken = Guid.NewGuid() } };
        Assert.Equal(SharedProviderTargetVerificationDisposition.StillUnconfirmed,
            SharedProviderTargetVerification.Evaluate(attempt, differentProvider).Disposition);
        Assert.Equal(SharedProviderTargetVerificationDisposition.StillUnconfirmed,
            SharedProviderTargetVerification.Evaluate(attempt, differentPublication).Disposition);
    }

    [Fact]
    public void Already_satisfied_before_state_requires_no_semantic_delivery() {
        var before = Local(true);
        var result = SharedProviderTargetVerification.Evaluate(Attempt(before, SharedProviderTargetMutationKind.Publish), before);
        Assert.Equal(SharedProviderTargetVerificationDisposition.Satisfied, result.Disposition);
        Assert.Null(result.Change);
    }

    internal static SharedProviderProfileSharingSnapshot Local(bool published) => new(Guid.NewGuid(),
        SharedProviderProfileOwnership.Local,
        new(Guid.NewGuid(), new SharedProviderPublicationId(Guid.NewGuid()), published, Guid.NewGuid()), null, null);

    internal static SharedProviderProfileSharingSnapshot Imported() {
        var import = ProviderVerificationHardeningTests.Import();
        return new(import.ProviderProfileId, SharedProviderProfileOwnership.Imported, null, null, import);
    }

    internal static SharedProviderTargetAttempt Attempt(SharedProviderProfileSharingSnapshot before,
        SharedProviderTargetMutationKind kind, SharedProviderImportedProfileUpdateRequest? request = null) =>
        new SharedProviderRecovery().BeginTarget(before.ProviderProfileId, kind, before, request);
}
