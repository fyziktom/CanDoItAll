using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Editor = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;
using RuntimeProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

internal sealed record ProviderDiagnosticObservation(EditingScope Scope, Guid ProviderId,
    Guid ExpectedConcurrencyToken, ProviderConfigurationFingerprint Fingerprint);

internal sealed class ProviderDiagnosticPersistence(IDbContextFactory<ProvidersDbContext> factory,
    ICanonicalRuntimeDatabase database, ProviderEditingWrites editing, ProviderProfileMapper mapper,
    IProviderProfileService profiles,
    IEnumerable<IProviderProfileCommitObserver> observers, ProviderMutationWitness witnesses) {
    private const string MutationScopePrefix = "provider-health";
    private EditingScope Scope => new(EditingScopeKind.Dataset, database.Profile.Profile.Id, database.Generation);

    public ProviderDiagnosticObservation Observe(RuntimeProfile profile, Guid? expectedConcurrencyToken) {
        if (expectedConcurrencyToken is not { } token || token == Guid.Empty) {
            throw new ProviderProfileValidationException("A saved provider configuration is required before persisting a diagnostic.");
        }
        return new(Scope, profile.Id, token, ProviderConfigurationFingerprintFactory.Create(profile));
    }

    public async Task RecordHealthAsync(ProviderDiagnosticObservation observation, ProviderHealthResult result,
        DateTimeOffset checkedAtUtc, CancellationToken cancellationToken, Func<Guid, CancellationToken, Task> reconcile) {
        if (observation.Scope != Scope) {
            throw new UnauthorizedAccessException("The provider diagnostic belongs to a different dataset or activation.");
        }
        await editing.RequireAsync(Scope.Id, observation.ProviderId, cancellationToken);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var mutation = await SerializableMutationScope.BeginAsync(db,
            $"{MutationScopePrefix}:{Scope.Id:D}:{observation.ProviderId:D}", cancellationToken);
        var row = await db.Set<ProviderProfile>().SingleOrDefaultAsync(provider => provider.Id == observation.ProviderId, cancellationToken);
        if (row is null || row.ConcurrencyToken != observation.ExpectedConcurrencyToken ||
            ProviderConfigurationFingerprintFactory.Create(mapper.Map(row)) != observation.Fingerprint) {
            throw new ProviderProfileConcurrencyException(observation.ProviderId);
        }
        if (row.LastHealthCheckAtUtc > checkedAtUtc) {
            return;
        }
        row.LastHealthStatus = result.Success ? SharedProviderPublicHealthMapper.HealthyStatus : SharedProviderPublicHealthMapper.UnhealthyStatus;
        row.LastHealthCheckAtUtc = checkedAtUtc;
        Editor accepted;
        try {
            await editing.RequireAsync(Scope.Id, observation.ProviderId, cancellationToken);
            await db.SaveDiagnosticChangesAsync(cancellationToken);
            accepted = profiles.CreateEditor(mapper.Map(row));
            accepted.ExpectedConcurrencyToken = row.ConcurrencyToken;
            await editing.RequireAsync(Scope.Id, observation.ProviderId, cancellationToken);
            await mutation.CommitAsync(cancellationToken);
        } catch (UnauthorizedAccessException) {
            throw;
        } catch (Exception exception) when (exception is DbUpdateConcurrencyException || SerializableMutationScope.IsConflict(exception)) {
            throw new ProviderProfileConcurrencyException(observation.ProviderId, exception);
        } catch (Exception exception) {
            throw new ProviderMutationUnconfirmedException(witnesses.Sign(new(Guid.NewGuid(), observation.ProviderId,
                ProviderMutationKind.HealthPersistence, observation.ExpectedConcurrencyToken)), exception);
        }
        try {
            await mutation.DisposeAsync();
            await ProviderCommitEffects.RunAsync(observers
                .Select(observer => (Func<Task>)(() => observer.ProviderHealthChangedAsync(observation.ProviderId, CancellationToken.None)))
                .Append(() => reconcile(observation.ProviderId, CancellationToken.None)));
        } catch (Exception exception) {
            throw new ProviderMutationCommittedException(new(observation.ProviderId, ProviderCatalogProjectionOperationKind.Upsert,
                observation.ExpectedConcurrencyToken, accepted), "Provider health is saved, but a secondary update requires reconciliation.", exception);
        }
    }
}
