using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class SharedProviderReconciliationTests
{
    private static readonly DateTimeOffset InitialTimestamp =
        new(2026, 8, 25, 6, 0, 0, TimeSpan.Zero);
    private static readonly Guid SourceId =
        Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid SecretId =
        Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly SharedProviderSourceInstanceId SourceInstanceId =
        new(Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc"));

    [Fact]
    public void Plan_NewCatalog_CreatesOnlySelectedPublication()
    {
        var selected = CreatePublication(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "Selected");
        var unselected = CreatePublication(
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            "Unselected");

        var plan = CreatePlan(
            [],
            CreateCatalog(selected, unselected),
            [selected.PublicationId]);

        var decision = Assert.Single(plan.Decisions);
        Assert.Equal(SharedProviderReconciliationDecisionKind.Create, decision.Kind);
        Assert.Equal(selected.PublicationId, decision.PublicationId);
        Assert.Null(decision.ImportId);
        Assert.Null(decision.ProviderProfileId);
    }

    [Fact]
    public void Plan_NewCatalog_IgnoresUnselectedPublication()
    {
        var publication = CreatePublication(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "Remote");

        var plan = CreatePlan([], CreateCatalog(publication), []);

        Assert.True(plan.IsNoOp);
    }

    [Fact]
    public void Plan_UnchangedSelectedImport_IsNoOp()
    {
        var publication = CreatePublication(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "Remote");
        var import = CreateImport(publication);

        var plan = CreatePlan(
            [import],
            CreateCatalog(publication),
            [publication.PublicationId]);

        Assert.True(plan.IsNoOp);
    }

    [Fact]
    public void Plan_ChangedRevision_RefreshesExistingIdentity()
    {
        var original = CreatePublication(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "Original");
        var renamed = CreatePublication(original.PublicationId.Value, "Renamed");
        var import = CreateImport(original);

        var plan = CreatePlan(
            [import],
            CreateCatalog(renamed),
            [renamed.PublicationId]);

        var decision = Assert.Single(plan.Decisions);
        Assert.Equal(SharedProviderReconciliationDecisionKind.Refresh, decision.Kind);
        Assert.Equal(import.Id, decision.ImportId);
        Assert.Equal(import.ProviderProfileId, decision.ProviderProfileId);
        Assert.Equal(renamed, decision.RemotePublication);
    }

    [Fact]
    public void Plan_TransientlyUnavailableImport_RefreshesOnReappearance()
    {
        var publication = CreatePublication(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "Remote");
        var import = CreateImport(publication);
        SharedProviderImportTransitions.MarkTransientlyUnavailable(
            import,
            SharedProviderAvailabilityState.SourceOffline,
            InitialTimestamp.AddMinutes(1));

        var plan = CreatePlan(
            [import],
            CreateCatalog(publication),
            [publication.PublicationId]);

        Assert.Equal(
            SharedProviderReconciliationDecisionKind.Refresh,
            Assert.Single(plan.Decisions).Kind);
    }

    [Fact]
    public void Plan_SelectedImportAbsent_MarksMissing()
    {
        var publication = CreatePublication(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "Remote");
        var import = CreateImport(publication);

        var plan = CreatePlan([import], CreateCatalog(), []);

        var decision = Assert.Single(plan.Decisions);
        Assert.Equal(SharedProviderReconciliationDecisionKind.MarkMissing, decision.Kind);
        Assert.Equal(import.Id, decision.ImportId);
        SharedProviderImportTransitions.MarkAuthoritativelyAbsent(
            import,
            SharedProviderAvailabilityState.Missing,
            InitialTimestamp.AddMinutes(1));

        var repeatedPlan = CreatePlan([import], CreateCatalog(), []);

        Assert.True(repeatedPlan.IsNoOp);
    }

    [Fact]
    public void Plan_RetiredImportAbsent_DoesNotMarkMissing()
    {
        var import = CreateImport(CreatePublication(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "Remote"));
        SharedProviderImportTransitions.Retire(import, InitialTimestamp.AddMinutes(1));

        var plan = CreatePlan([import], CreateCatalog(), []);

        Assert.True(plan.IsNoOp);
    }

    [Fact]
    public void Plan_ReplaceSelection_RetiresDeselectedImport()
    {
        var publication = CreatePublication(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "Remote");
        var import = CreateImport(publication);

        var plan = CreatePlan(
            [import],
            CreateCatalog(publication),
            [],
            SharedProviderSelectionMode.Replace);

        var decision = Assert.Single(plan.Decisions);
        Assert.Equal(SharedProviderReconciliationDecisionKind.Retire, decision.Kind);
        Assert.Equal(import.ProviderProfileId, decision.ProviderProfileId);
    }

    [Fact]
    public void Plan_ReplaceSelection_ReactivatesRetiredImport()
    {
        var publication = CreatePublication(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "Remote");
        var import = CreateImport(publication);
        SharedProviderImportTransitions.Retire(import, InitialTimestamp.AddMinutes(1));

        var plan = CreatePlan(
            [import],
            CreateCatalog(publication),
            [publication.PublicationId],
            SharedProviderSelectionMode.Replace);

        Assert.Equal(
            SharedProviderReconciliationDecisionKind.Reactivate,
            Assert.Single(plan.Decisions).Kind);
    }

    [Fact]
    public void Plan_AdditiveSelection_DoesNotRetireUnmentionedImport()
    {
        var publication = CreatePublication(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "Remote");
        var import = CreateImport(publication);

        var plan = CreatePlan(
            [import],
            CreateCatalog(publication),
            [],
            SharedProviderSelectionMode.AddOrReactivate);

        Assert.True(plan.IsNoOp);
    }

    [Fact]
    public void Plan_StaleUnknownSelection_ThrowsTypedConflict()
    {
        var unknown = new SharedProviderPublicationId(
            Guid.Parse("11111111-1111-4111-8111-111111111111"));

        var exception = Assert.Throws<SharedProviderSelectionConflictException>(() =>
            CreatePlan([], CreateCatalog(), [unknown], SharedProviderSelectionMode.Replace));

        Assert.Equal([unknown], exception.UnknownPublicationIds);
    }

    [Fact]
    public void Plan_RetiredAbsentSelection_ThrowsTypedConflict()
    {
        var publication = CreatePublication(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "Remote");
        var import = CreateImport(publication);
        SharedProviderImportTransitions.Retire(import, InitialTimestamp.AddMinutes(1));

        var exception = Assert.Throws<SharedProviderSelectionConflictException>(() =>
            CreatePlan(
                [import],
                CreateCatalog(),
                [publication.PublicationId],
                SharedProviderSelectionMode.Replace));

        Assert.Equal([publication.PublicationId], exception.UnknownPublicationIds);
    }

    [Fact]
    public void Plan_PreservedSelectedAbsentPublication_IsAllowedAndMarkedMissing()
    {
        var publication = CreatePublication(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "Remote");
        var import = CreateImport(publication);

        var plan = CreatePlan(
            [import],
            CreateCatalog(),
            [publication.PublicationId],
            SharedProviderSelectionMode.Replace);

        Assert.Equal(
            SharedProviderReconciliationDecisionKind.MarkMissing,
            Assert.Single(plan.Decisions).Kind);
    }

    [Fact]
    public void Plan_DuplicatePersistedIdentity_Throws()
    {
        var publication = CreatePublication(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "Remote");
        var first = CreateImport(publication);
        var second = CreateImport(publication);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreatePlan([first, second], CreateCatalog(publication), []));

        Assert.Contains("duplicate persisted import identities", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_Decisions_AreDeterministicallyOrdered()
    {
        var first = CreatePublication(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "First");
        var second = CreatePublication(
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            "Second");
        var missingImport = CreateImport(second);

        var plan = CreatePlan(
            [missingImport],
            CreateCatalog(first),
            [first.PublicationId, second.PublicationId],
            SharedProviderSelectionMode.Replace);

        Assert.Equal(
            [first.PublicationId, second.PublicationId],
            plan.Decisions.Select(decision => decision.PublicationId));
        Assert.Equal(
            [
                SharedProviderReconciliationDecisionKind.Create,
                SharedProviderReconciliationDecisionKind.MarkMissing
            ],
            plan.Decisions.Select(decision => decision.Kind));
    }

    [Fact]
    public void Plan_RemoteRefreshPrecedesRetirement()
    {
        var original = CreatePublication(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "Original");
        var renamed = CreatePublication(original.PublicationId.Value, "Renamed");
        var import = CreateImport(original);

        var plan = CreatePlan(
            [import],
            CreateCatalog(renamed),
            [],
            SharedProviderSelectionMode.Replace);

        Assert.Equal(
            [
                SharedProviderReconciliationDecisionKind.Refresh,
                SharedProviderReconciliationDecisionKind.Retire
            ],
            plan.Decisions.Select(decision => decision.Kind));
    }

    [Fact]
    public void Plan_MissingReappearanceRefreshPrecedesReactivation()
    {
        var publication = CreatePublication(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "Remote");
        var import = CreateImport(publication);
        SharedProviderImportTransitions.MarkAuthoritativelyAbsent(
            import,
            SharedProviderAvailabilityState.Missing,
            InitialTimestamp.AddMinutes(1));
        SharedProviderImportTransitions.Retire(import, InitialTimestamp.AddMinutes(2));

        var plan = CreatePlan(
            [import],
            CreateCatalog(publication),
            [publication.PublicationId],
            SharedProviderSelectionMode.Replace);

        Assert.Equal(
            [
                SharedProviderReconciliationDecisionKind.Refresh,
                SharedProviderReconciliationDecisionKind.Reactivate
            ],
            plan.Decisions.Select(decision => decision.Kind));
    }

    [Fact]
    public void SourceSetEnabled_PreservesPinnedIdentityAndIsIdempotent()
    {
        var source = CreateSynchronizedSource();
        var originalIdentity = source.RemoteInstanceId;
        var originalEntityTag = source.LastCatalogETag;
        var disabledAt = InitialTimestamp.AddMinutes(2);

        SharedProviderSourceTransitions.SetEnabled(source, isEnabled: false, disabledAt);
        SharedProviderSourceTransitions.SetEnabled(source, isEnabled: false, disabledAt.AddMinutes(1));

        Assert.False(source.IsEnabled);
        Assert.Equal(originalIdentity, source.RemoteInstanceId);
        Assert.Equal(originalEntityTag, source.LastCatalogETag);
        Assert.Equal(disabledAt, source.UpdatedAtUtc);
    }

    [Fact]
    public void SourceCreation_AllowsLoopbackHttpButRequiresPrivateNetworkPolicy()
    {
        var loopback = SharedProviderSourceTransitions.Create(
            "Local central",
            "http://127.0.0.1:5080/reverse-proxy/",
            SecretId,
            allowInsecurePrivateNetwork: false,
            isEnabled: true,
            InitialTimestamp);

        Assert.Equal("http://127.0.0.1:5080/reverse-proxy/", loopback.BaseUri);
        Assert.Throws<ArgumentException>(() => SharedProviderSourceTransitions.Create(
            "Private central",
            "http://10.20.30.40/reverse-proxy",
            SecretId,
            allowInsecurePrivateNetwork: false,
            isEnabled: true,
            InitialTimestamp));
        var trustedPrivate = SharedProviderSourceTransitions.Create(
            "Private central",
            "http://10.20.30.40/reverse-proxy",
            SecretId,
            allowInsecurePrivateNetwork: true,
            isEnabled: true,
            InitialTimestamp);
        Assert.Equal("http://10.20.30.40/reverse-proxy/", trustedPrivate.BaseUri);
    }

    [Fact]
    public void SourceResetTrustedIdentity_ConnectionTestDoesNotSeedConditionalSync()
    {
        var source = CreateSynchronizedSource();
        var originalBaseUri = source.BaseUri;
        var originalSecretId = source.ApiTokenSecretId;
        var resetAt = InitialTimestamp.AddMinutes(2);

        SharedProviderSourceTransitions.ResetTrustedIdentity(source, resetAt);

        Assert.Null(source.RemoteInstanceId);
        Assert.Null(source.LastCatalogETag);
        Assert.Null(source.LastSyncAtUtc);
        Assert.Null(source.LastStatusCode);
        Assert.Empty(source.LastStatusMessage);
        Assert.Equal(SharedProviderSourceStatus.NeverSynchronized, source.Status);
        Assert.Equal(originalBaseUri, source.BaseUri);
        Assert.Equal(originalSecretId, source.ApiTokenSecretId);
        Assert.Equal(resetAt, source.UpdatedAtUtc);

        var catalog = CreateCatalog();
        var acceptance = SharedProviderSourceTransitions.ApplySuccessfulConnectionTest(
            source,
            SourceInstanceId,
            SharedProviderCatalogEntityTag.FromRevision(catalog.CatalogRevision),
            resetAt.AddMinutes(1));

        Assert.Equal(SharedProviderCatalogIdentityAcceptance.Accepted, acceptance);
        Assert.Equal(SourceInstanceId, source.RemoteInstanceId);
        Assert.Null(source.LastCatalogETag);
        Assert.Equal("Catalog connection verified.", source.LastStatusMessage);
    }

    [Fact]
    public void SourceResetTrustedIdentity_RejectsBackwardTimestamp()
    {
        var source = CreateSynchronizedSource();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SharedProviderSourceTransitions.ResetTrustedIdentity(
                source,
                InitialTimestamp.AddMinutes(-1)));
        Assert.Equal(SourceInstanceId, source.RemoteInstanceId);
    }

    [Fact]
    public void SourceOperationResults_ExposeOnlyOutcomeAppropriatePayload()
    {
        var catalog = CreateCatalog();
        var entityTag = SharedProviderCatalogEntityTag.FromRevision(catalog.CatalogRevision);
        var providerId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
        var succeeded = SharedProviderSourceOperationResult.Succeeded(
            catalog,
            entityTag,
            [providerId],
            [providerId]);
        var notModified = SharedProviderSourceOperationResult.NotModified(entityTag);
        var disabledFailure = SharedProviderSourceOperationFailures.SourceDisabled();
        var disabled = SharedProviderSourceOperationResult.SourceDisabled(disabledFailure);

        Assert.Equal(SharedProviderSourceOperationOutcome.Succeeded, succeeded.Outcome);
        Assert.Same(catalog, succeeded.Catalog);
        Assert.Equal(entityTag, succeeded.EntityTag);
        Assert.Equal([providerId], succeeded.AffectedProviderProfileIds);
        Assert.Equal([providerId], succeeded.RetiredProviderProfileIds);
        Assert.Null(succeeded.Failure);
        Assert.Equal(SharedProviderSourceOperationOutcome.NotModified, notModified.Outcome);
        Assert.Null(notModified.Catalog);
        Assert.Empty(notModified.AffectedProviderProfileIds);
        Assert.Null(notModified.Failure);
        Assert.Equal(SharedProviderSourceOperationOutcome.SourceDisabled, disabled.Outcome);
        Assert.Same(disabledFailure, disabled.Failure);
        Assert.Null(disabled.Catalog);
        Assert.Null(disabled.EntityTag);
    }

    private static SharedProviderReconciliationPlan CreatePlan(
        IReadOnlyCollection<SharedProviderImport> imports,
        SharedProviderCatalogDocument catalog,
        SharedProviderPublicationId[] selectedPublicationIds,
        SharedProviderSelectionMode selectionMode = SharedProviderSelectionMode.AddOrReactivate)
        => SharedProviderReconciliationPlanner.Create(
            imports,
            catalog,
            selectedPublicationIds.ToHashSet(),
            selectionMode);

    private static SharedProviderImport CreateImport(
        SharedProviderCatalogPublication publication)
    {
        var import = SharedProviderImportTransitions.Create(
            SourceId,
            DeriveGuid(publication.PublicationId.Value, 0x40),
            SharedProviderRemotePublicationState.Create(publication),
            InitialTimestamp);
        import.Id = DeriveGuid(publication.PublicationId.Value, 0x80);
        return import;
    }

    private static Guid DeriveGuid(Guid value, byte mask)
    {
        var bytes = value.ToByteArray();
        bytes[0] ^= mask;
        return new Guid(bytes);
    }

    private static SharedProviderSource CreateSynchronizedSource()
    {
        var source = SharedProviderSourceTransitions.Create(
            "Central",
            "https://central.example.test/root",
            SecretId,
            allowInsecurePrivateNetwork: false,
            isEnabled: true,
            InitialTimestamp);
        var catalog = CreateCatalog();
        SharedProviderSourceTransitions.ApplySuccessfulCatalog(
            source,
            SourceInstanceId,
            SharedProviderCatalogEntityTag.FromRevision(catalog.CatalogRevision),
            InitialTimestamp.AddMinutes(1));
        return source;
    }

    private static SharedProviderCatalogPublication CreatePublication(
        Guid publicationValue,
        string displayName)
    {
        var publicationId = new SharedProviderPublicationId(publicationValue);
        var routingModelId = SharedProviderRoutingModelIdCodec.Create(
            publicationId,
            $"model-{publicationValue:N}");
        var publication = new SharedProviderCatalogPublication(
            publicationId,
            new SharedProviderPublicRevision($"sha256:{new string('0', 64)}"),
            displayName,
            SharedProviderPurpose.Chat,
            SharedProviderTransport.OpenAiCompatible,
            routingModelId,
            [
                new SharedProviderCatalogModel(
                    routingModelId,
                    "Remote model",
                    [
                        SharedProviderCapability.Responses,
                        SharedProviderCapability.Streaming
                    ])
            ],
            new SharedProviderCatalogHealth(SharedProviderHealthState.Available));
        return publication with
        {
            Revision = SharedProviderCanonicalRevision.ComputePublication(publication)
        };
    }

    private static SharedProviderCatalogDocument CreateCatalog(
        params SharedProviderCatalogPublication[] publications)
    {
        var catalog = new SharedProviderCatalogDocument(
            SharedProviderProtocolVersion.Current,
            SourceInstanceId,
            new SharedProviderPublicRevision($"sha256:{new string('0', 64)}"),
            new SharedProviderProtocolDescriptor(SharedProviderRoutes.OpenAiBase),
            publications);
        return catalog with
        {
            CatalogRevision = SharedProviderCanonicalRevision.ComputeCatalog(catalog)
        };
    }
}
