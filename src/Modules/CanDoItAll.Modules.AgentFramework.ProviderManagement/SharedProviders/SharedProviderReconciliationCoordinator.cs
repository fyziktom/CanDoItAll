using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Editing;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class SharedProviderReconciliationCoordinator
{
    public const string ImportedConnectorPluginKey = ProviderConnectorKeys.SharedImport;

    public const string ImportedConfigurationSchemaVersion = "1.0";
    private readonly IDbContextFactory<ProvidersDbContext> dbContextFactory;
    private readonly IClock clock;
    private readonly CoordinatedDatabaseTransaction transactions;
    private readonly IReadOnlyList<IProviderProfileCommitObserver> providerProfileCommitObservers;
    private readonly ProviderEditingWrites? editing;
    private readonly ICanonicalRuntimeDatabase? database;
    private readonly ProviderEditingChanges? changes;

    public SharedProviderReconciliationCoordinator(
        IDbContextFactory<ProvidersDbContext> dbContextFactory,
        IClock clock,
        IEnumerable<IProviderProfileCommitObserver> providerProfileCommitObservers,
        CoordinatedDatabaseTransaction transactions,
        ProviderEditingWrites? editing = null,
        ICanonicalRuntimeDatabase? database = null,
        ProviderEditingChanges? changes = null)
    {
        this.dbContextFactory = dbContextFactory;
        this.clock = clock;
        this.transactions = transactions;
        this.providerProfileCommitObservers = providerProfileCommitObservers.ToArray();
        this.editing = editing;
        this.database = database;
        this.changes = changes;
    }

    public async Task<SharedProviderReconciliationResult> ReconcileAsync(
        SharedProviderReconciliationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExpectedSourceConcurrencyToken is null || request.OriginalSelection is null) {
            throw new SharedProviderConcurrencyException(nameof(SharedProviderSource), request.SourceId);
        }
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await SerializableMutationScope.BeginAsync(
            dbContext,
            $"shared-provider-source:{request.SourceId:D}",
            cancellationToken);
        using var coordination = transactions.Enter(dbContext);
        var source = await dbContext.Set<SharedProviderSource>()
            .SingleOrDefaultAsync(item => item.Id == request.SourceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Shared-provider source '{request.SourceId:D}' was not found.");
        if (request.ExpectedSourceConcurrencyToken is { } expectedSourceConcurrencyToken &&
            source.ConcurrencyToken != expectedSourceConcurrencyToken)
        {
            throw new SharedProviderConcurrencyException(
                nameof(SharedProviderSource),
                source.Id);
        }

        var imports = await dbContext.Set<SharedProviderImport>()
            .Where(import => import.SourceId == source.Id)
            .ToListAsync(cancellationToken);
        if (!request.OriginalSelection.Matches(source.ConcurrencyToken,
            imports.Select(import => KeyValuePair.Create(import.Id, import.ConcurrencyToken)))) {
            throw new SharedProviderConcurrencyException(nameof(SharedProviderSource), source.Id);
        }
        var now = clock.GetUtcNow();
        var identityAcceptance = SharedProviderSourceTransitions.ApplySuccessfulCatalog(
            source,
            request.Catalog.SourceInstanceId,
            request.EntityTag,
            now);
        if (identityAcceptance == SharedProviderCatalogIdentityAcceptance.IdentityMismatch)
        {
            foreach (var import in imports)
            {
                SharedProviderImportTransitions.MarkTransientlyUnavailable(
                    import,
                    SharedProviderAvailabilityState.SourceIdentityMismatch,
                    now);
            }

            await SaveAndCommitAsync(dbContext, mutationScope, source.Id, cancellationToken);

            coordination.Dispose();
            var affectedIds = imports.Select(import => import.ProviderProfileId).Distinct().ToArray();
            var mismatch = await SharedProviderCommitEffects.NotifySavedAsync(
                new(SharedProviderChangeKind.SourceAvailability, affectedIds, remoteOwnedFieldsChanged: false),
                providerProfileCommitObservers);
            return new SharedProviderReconciliationResult(
                SharedProviderReconciliationOutcome.SourceIdentityMismatch, affectedIds) { Change = mismatch };
        }

        var plan = SharedProviderReconciliationPlanner.Create(
            imports,
            request.Catalog,
            request.SelectedPublicationIds,
            request.SelectionMode);
        var importsById = imports.ToDictionary(import => import.Id);
        var existingProviderIds = plan.Decisions
            .Where(decision => decision.ProviderProfileId.HasValue)
            .Select(decision => decision.ProviderProfileId!.Value)
            .Distinct()
            .ToArray();
        var profilesById = existingProviderIds.Length == 0
            ? new Dictionary<Guid, ProviderProfile>()
            : await dbContext.Set<ProviderProfile>()
                .Where(profile => existingProviderIds.Contains(profile.Id))
                .ToDictionaryAsync(profile => profile.Id, cancellationToken);
        var remoteFieldsApplied = false;
        var affectedProviderIds = new HashSet<Guid>();
        var retiredProviderIds = new HashSet<Guid>();
        foreach (var decision in plan.Decisions)
        {
            switch (decision.Kind)
            {
                case SharedProviderReconciliationDecisionKind.Create:
                {
                    var publication = decision.RemotePublication!;
                    remoteFieldsApplied = true;
                    var createdProfile = CreateImportedProfile(source, publication);
                    var createdImport = SharedProviderImportTransitions.Create(
                        source.Id,
                        createdProfile.Id,
                        SharedProviderRemotePublicationState.Create(publication),
                        now);
                    dbContext.Add(createdProfile);
                    dbContext.Add(createdImport);
                    imports.Add(createdImport);
                    affectedProviderIds.Add(createdProfile.Id);
                    break;
                }
                case SharedProviderReconciliationDecisionKind.Refresh:
                {
                    var import = importsById[decision.ImportId!.Value];
                    var publication = decision.RemotePublication!;
                    SharedProviderImportTransitions.ReconcileAvailable(
                        import,
                        SharedProviderRemotePublicationState.Create(publication),
                        now);
                    remoteFieldsApplied = true;
                    ApplyRemoteOwnedProfileFields(
                        profilesById[import.ProviderProfileId],
                        source,
                        publication);
                    affectedProviderIds.Add(import.ProviderProfileId);
                    break;
                }
                case SharedProviderReconciliationDecisionKind.MarkMissing:
                {
                    var import = importsById[decision.ImportId!.Value];
                    SharedProviderImportTransitions.MarkAuthoritativelyAbsent(
                        import,
                        SharedProviderAvailabilityState.Missing,
                        now);
                    affectedProviderIds.Add(import.ProviderProfileId);
                    break;
                }
                case SharedProviderReconciliationDecisionKind.Reactivate:
                {
                    var import = importsById[decision.ImportId!.Value];
                    SharedProviderImportTransitions.Reactivate(import, now);
                    affectedProviderIds.Add(import.ProviderProfileId);
                    break;
                }
                case SharedProviderReconciliationDecisionKind.Retire:
                {
                    var import = importsById[decision.ImportId!.Value];
                    SharedProviderImportTransitions.Retire(import, now);
                    affectedProviderIds.Add(import.ProviderProfileId);
                    retiredProviderIds.Add(import.ProviderProfileId);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(decision),
                        decision.Kind,
                        "Unknown shared-provider reconciliation decision.");
            }
        }

        await SaveAndCommitAsync(dbContext, mutationScope, source.Id, cancellationToken);
        var accepted = new SharedProviderSourceObservation(source.ConcurrencyToken,
            imports.Select(import => KeyValuePair.Create(import.Id, import.ConcurrencyToken)));

        coordination.Dispose();
        var committedProviderIds = affectedProviderIds.Order().ToArray();
        var change = await SharedProviderCommitEffects.NotifySavedAsync(
            new(SharedProviderChangeKind.Reconciliation, committedProviderIds, retiredProviderIds,
                remoteOwnedFieldsChanged: remoteFieldsApplied, catalogMembershipMayHaveChanged: true),
            providerProfileCommitObservers);
        return new SharedProviderReconciliationResult(
            SharedProviderReconciliationOutcome.Applied,
            committedProviderIds)
        {
            RetiredProviderProfileIds = retiredProviderIds.Order().ToArray(),
            Change = change,
            AcceptedObservation = accepted
        };
    }

    private static ProviderProfile CreateImportedProfile(
        SharedProviderSource source,
        SharedProviderCatalogPublication publication)
    {
        var profile = new ProviderProfile
        {
            Name = publication.DisplayName,
            ProviderKind = ProviderKind.OpenAi,
            ConnectorPluginKey = ImportedConnectorPluginKey,
            ConfigSchemaVersion = ImportedConfigurationSchemaVersion,
            IsEnabled = true,
            TimeoutSeconds = 45,
            ExtraSettingsJson = "{}"
        };
        ApplyRemoteOwnedProfileFields(profile, source, publication);
        return profile;
    }

    private static void ApplyRemoteOwnedProfileFields(
        ProviderProfile profile,
        SharedProviderSource source,
        SharedProviderCatalogPublication publication)
    {
        var defaultModel = publication.Models.Single(model => model.Id == publication.DefaultModelId);
        var capabilities = defaultModel.Capabilities.ToHashSet();
        profile.BaseUrl = SharedProviderRoutes.ResolveOpenAiBase(new Uri(source.BaseUri)).AbsoluteUri;
        profile.ApiKeySecretId = source.ApiTokenSecretId;
        profile.DefaultModel = publication.DefaultModelId.Value;
        profile.SupportsStreaming = capabilities.Contains(SharedProviderCapability.Streaming);
        profile.SupportsToolCalling = capabilities.Contains(SharedProviderCapability.FunctionTools);
        profile.SupportsStructuredOutput = capabilities.Contains(SharedProviderCapability.StructuredOutput);
        profile.SupportsVision = capabilities.Contains(SharedProviderCapability.VisionInput);
    }

    private async Task SaveAndCommitAsync(
        ProvidersDbContext dbContext,
        SerializableMutationScope mutationScope,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        try
        {
            await RequireOriginalAsync(sourceId, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await RequireOriginalAsync(sourceId, cancellationToken);
            await mutationScope.CommitAsync(cancellationToken);
            changes?.SourcesChanged(sourceId);
        }
        catch (Exception exception) when (
            SerializableMutationScope.IsConflict(exception) ||
            SharedProviderPersistenceConflictClassifier.IsReconciliationIdentityConflict(exception))
        {
            throw new SharedProviderConcurrencyException(
                nameof(SharedProviderSource),
                sourceId,
                exception);
        }
    }

    private ValueTask RequireOriginalAsync(Guid sourceId, CancellationToken cancellationToken) => editing is null
        ? ValueTask.CompletedTask
        : editing.RequireAsync(database?.Profile.Profile.Id ?? throw new InvalidOperationException("Source synchronization requires the canonical dataset."),
            sourceId, cancellationToken, EditingOwner.SharedProviderSource);

}
