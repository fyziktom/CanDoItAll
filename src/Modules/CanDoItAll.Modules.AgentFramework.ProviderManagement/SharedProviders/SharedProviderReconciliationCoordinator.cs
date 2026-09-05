using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class SharedProviderReconciliationCoordinator
{
    public const string ImportedConnectorPluginKey = ProviderConnectorKeys.SharedImport;

    public const string ImportedConfigurationSchemaVersion = "1.0";
    private readonly IDbContextFactory<AppDbContext> dbContextFactory;
    private readonly IClock clock;
    private readonly IReadOnlyList<IProviderProfileCommitObserver> providerProfileCommitObservers;

    public SharedProviderReconciliationCoordinator(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IClock clock,
        IEnumerable<IProviderProfileCommitObserver> providerProfileCommitObservers)
    {
        this.dbContextFactory = dbContextFactory;
        this.clock = clock;
        this.providerProfileCommitObservers = providerProfileCommitObservers.ToArray();
    }

    public async Task<SharedProviderReconciliationResult> ReconcileAsync(
        SharedProviderReconciliationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await SerializableMutationScope.BeginAsync(
            dbContext,
            $"shared-provider-source:{request.SourceId:D}",
            cancellationToken);
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
            Change = change
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

    private static async Task SaveAndCommitAsync(
        AppDbContext dbContext,
        SerializableMutationScope mutationScope,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await mutationScope.CommitAsync(cancellationToken);
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

}
