using System.Text.Json;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class SharedProviderStateModelTests
{
    private static readonly DateTimeOffset InitialTimestamp =
        new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly SharedProviderPublicationId PublicationId =
        new(Guid.Parse("11111111-2222-3333-4444-555555555555"));

    private static readonly SharedProviderSourceInstanceId SourceInstanceId =
        new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

    [Fact]
    public void PublicationCreationDefaultsToUnpublishedAndKeepsPublicIdentitySeparate()
    {
        var profileId = Guid.Parse("aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb");

        var publication = SharedProviderPublicationTransitions.Create(
            profileId,
            PublicationId,
            InitialTimestamp);

        Assert.NotEqual(Guid.Empty, publication.Id);
        Assert.Equal(profileId, publication.ProviderProfileId);
        Assert.Equal(PublicationId, publication.PublicId);
        Assert.False(publication.IsPublished);
        Assert.Equal(InitialTimestamp, publication.CreatedAtUtc);
        Assert.Equal(InitialTimestamp, publication.UpdatedAtUtc);
    }

    [Fact]
    public void PublicationCreationRejectsEmptyOrExposedProfileIdentity()
    {
        var profileId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        Assert.Throws<ArgumentException>(() => SharedProviderPublicationTransitions.Create(
            Guid.Empty,
            PublicationId,
            InitialTimestamp));
        Assert.Throws<ArgumentException>(() => SharedProviderPublicationTransitions.Create(
            profileId,
            new SharedProviderPublicationId(profileId),
            InitialTimestamp));
        Assert.Throws<ArgumentException>(() => SharedProviderPublicationTransitions.Create(
            profileId,
            default,
            InitialTimestamp));
    }

    [Fact]
    public void PublicationTransitionsAreIdempotentAndPreserveStableIdentity()
    {
        var publication = CreatePublication();
        var publishedAt = InitialTimestamp.AddMinutes(1);
        var unpublishedAt = InitialTimestamp.AddMinutes(2);

        SharedProviderPublicationTransitions.Publish(publication, publishedAt);
        SharedProviderPublicationTransitions.Publish(publication, publishedAt);

        Assert.True(publication.IsPublished);
        Assert.Equal(PublicationId, publication.PublicId);
        Assert.Equal(publishedAt, publication.UpdatedAtUtc);

        SharedProviderPublicationTransitions.Unpublish(publication, unpublishedAt);
        SharedProviderPublicationTransitions.Unpublish(publication, unpublishedAt);

        Assert.False(publication.IsPublished);
        Assert.Equal(PublicationId, publication.PublicId);
        Assert.Equal(unpublishedAt, publication.UpdatedAtUtc);
    }

    [Fact]
    public void SourceCreationCanonicalizesAddressAndDefaultsToNeverSynchronized()
    {
        var secretId = Guid.Parse("11111111-aaaa-bbbb-cccc-222222222222");

        var source = SharedProviderSourceTransitions.Create(
            "  Central workspace  ",
            "https://CENTRAL.example.test:443/tenant/",
            secretId,
            allowInsecurePrivateNetwork: false,
            isEnabled: true,
            InitialTimestamp);

        Assert.Equal("Central workspace", source.Name);
        Assert.Equal("https://central.example.test/tenant/", source.BaseUri);
        Assert.Equal(secretId, source.ApiTokenSecretId);
        Assert.True(source.IsEnabled);
        Assert.False(source.AllowInsecurePrivateNetwork);
        Assert.Equal(SharedProviderSourceStatus.NeverSynchronized, source.Status);
        Assert.Null(source.RemoteInstanceId);
        Assert.Null(source.LastCatalogETag);
        Assert.Null(source.LastSyncAtUtc);
    }

    [Fact]
    public void SourceCreationRejectsUnsafeAddressOrMissingSecretReference()
    {
        string[] unsafeAddresses =
        [
            "https://user:credential@central.example.test",
            "https://central.example.test?private=value",
            "https://central.example.test#fragment",
            "ftp://central.example.test",
            "/relative"
        ];

        foreach (var unsafeAddress in unsafeAddresses)
        {
            Assert.Throws<ArgumentException>(() => SharedProviderSourceTransitions.Create(
                "Central",
                unsafeAddress,
                Guid.NewGuid(),
                allowInsecurePrivateNetwork: false,
                isEnabled: true,
                InitialTimestamp));
        }

        Assert.Throws<ArgumentException>(() => SharedProviderSourceTransitions.Create(
            "Central",
            "https://central.example.test",
            Guid.Empty,
            allowInsecurePrivateNetwork: false,
            isEnabled: true,
            InitialTimestamp));
    }

    [Fact]
    public void SourceConfigurationEditPreservesPinnedIdentityAndInvalidatesConditionalCache()
    {
        var source = CreateSynchronizedSource();
        var updatedAt = InitialTimestamp.AddHours(1);
        var updatedSecretId = Guid.Parse("33333333-aaaa-bbbb-cccc-444444444444");

        SharedProviderSourceTransitions.UpdateConfiguration(
            source,
            "Updated central",
            "https://new-central.example.test/base",
            updatedSecretId,
            allowInsecurePrivateNetwork: true,
            isEnabled: false,
            updatedAt);

        Assert.Equal("Updated central", source.Name);
        Assert.Equal("https://new-central.example.test/base/", source.BaseUri);
        Assert.Equal(updatedSecretId, source.ApiTokenSecretId);
        Assert.False(source.IsEnabled);
        Assert.True(source.AllowInsecurePrivateNetwork);
        Assert.Equal(SourceInstanceId, source.RemoteInstanceId);
        Assert.Null(source.LastCatalogETag);
        Assert.Equal(SharedProviderSourceStatus.NeverSynchronized, source.Status);
        Assert.Equal(updatedAt, source.UpdatedAtUtc);
    }

    [Fact]
    public void FirstSuccessfulCatalogPinsRemoteIdentityAndEntityTag()
    {
        var source = CreateSource();
        var entityTag = CreateEntityTag('a');
        var synchronizedAt = InitialTimestamp.AddMinutes(3);

        var result = SharedProviderSourceTransitions.ApplySuccessfulCatalog(
            source,
            SourceInstanceId,
            entityTag,
            synchronizedAt);

        Assert.Equal(SharedProviderCatalogIdentityAcceptance.Accepted, result);
        Assert.Equal(SourceInstanceId, source.RemoteInstanceId);
        Assert.Equal(entityTag, source.LastCatalogETag);
        Assert.Equal(SharedProviderSourceStatus.Available, source.Status);
        Assert.Equal(synchronizedAt, source.LastSyncAtUtc);
        Assert.Equal(200, source.LastStatusCode);
        Assert.Equal("Catalog synchronized.", source.LastStatusMessage);
    }

    [Fact]
    public void DifferentCatalogIdentityBlocksReconciliationAndPreservesTrustedPin()
    {
        var source = CreateSynchronizedSource();
        var originalTag = source.LastCatalogETag;
        var differentIdentity = new SharedProviderSourceInstanceId(
            Guid.Parse("ffffffff-bbbb-cccc-dddd-eeeeeeeeeeee"));

        var result = SharedProviderSourceTransitions.ApplySuccessfulCatalog(
            source,
            differentIdentity,
            CreateEntityTag('b'),
            InitialTimestamp.AddMinutes(5));

        Assert.Equal(SharedProviderCatalogIdentityAcceptance.IdentityMismatch, result);
        Assert.Equal(SourceInstanceId, source.RemoteInstanceId);
        Assert.Equal(originalTag, source.LastCatalogETag);
        Assert.Equal(SharedProviderSourceStatus.SourceIdentityMismatch, source.Status);
        Assert.Equal(409, source.LastStatusCode);
    }

    [Fact]
    public void SourceFailureKeepsTrustedIdentityAndConditionalEntityTag()
    {
        var source = CreateSynchronizedSource();
        var originalTag = source.LastCatalogETag;
        var failedAt = InitialTimestamp.AddMinutes(6);

        SharedProviderSourceTransitions.ApplyFailure(
            source,
            SharedProviderSourceStatus.SourceOffline,
            503,
            "Central source unavailable.",
            failedAt);

        Assert.Equal(SourceInstanceId, source.RemoteInstanceId);
        Assert.Equal(originalTag, source.LastCatalogETag);
        Assert.Equal(SharedProviderSourceStatus.SourceOffline, source.Status);
        Assert.Equal(failedAt, source.LastSyncAtUtc);
        Assert.Equal("Central source unavailable.", source.LastStatusMessage);
        Assert.Throws<ArgumentException>(() => SharedProviderSourceTransitions.ApplyFailure(
            source,
            SharedProviderSourceStatus.Available,
            200,
            "Not a failure.",
            failedAt));
    }

    [Fact]
    public void ImportCreationSelectsAvailableRemotePublicationWithStableLocalProfile()
    {
        var sourceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var profileId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var remote = CreateRemotePublicationState("Remote OpenAI");

        var import = SharedProviderImportTransitions.Create(
            sourceId,
            profileId,
            remote,
            InitialTimestamp);

        Assert.NotEqual(Guid.Empty, import.Id);
        Assert.Equal(sourceId, import.SourceId);
        Assert.Equal(profileId, import.ProviderProfileId);
        Assert.Equal(PublicationId, import.RemotePublicationId);
        Assert.Equal(SharedProviderSelectionState.Selected, import.SelectionState);
        Assert.Equal(SharedProviderAvailabilityState.Available, import.AvailabilityState);
        Assert.Equal(InitialTimestamp, import.LastSeenAtUtc);
        Assert.Equal(InitialTimestamp, import.LastSyncAtUtc);
        using var snapshot = JsonDocument.Parse(import.RemoteCatalogSnapshotJson);
        Assert.Equal(
            SharedProviderProtocol.CurrentSchemaVersion,
            snapshot.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(
            PublicationId.ToString(),
            snapshot.RootElement
                .GetProperty("publication")
                .GetProperty("publicationId")
                .GetString());
    }

    [Fact]
    public void AvailableReconciliationUpdatesRemoteFieldsWithoutChangingLocalIdentityOrIntent()
    {
        var import = CreateImport();
        var importId = import.Id;
        var profileId = import.ProviderProfileId;
        var reconciledAt = InitialTimestamp.AddHours(2);
        var updatedRemote = CreateRemotePublicationState("Renamed remote provider");

        SharedProviderImportTransitions.ReconcileAvailable(import, updatedRemote, reconciledAt);

        Assert.Equal(importId, import.Id);
        Assert.Equal(profileId, import.ProviderProfileId);
        Assert.Equal(SharedProviderSelectionState.Selected, import.SelectionState);
        Assert.Equal("Renamed remote provider", import.RemoteDisplayName);
        Assert.Equal(updatedRemote.Revision, import.RemoteRevision);
        Assert.Equal(updatedRemote.CatalogSnapshotJson, import.RemoteCatalogSnapshotJson);
        Assert.Equal(SharedProviderAvailabilityState.Available, import.AvailabilityState);
        Assert.Equal(reconciledAt, import.LastSeenAtUtc);
        Assert.Equal(reconciledAt, import.LastSyncAtUtc);
    }

    [Fact]
    public void AuthoritativeMissingPreservesImportAndLocalProviderIdentity()
    {
        var import = CreateImport();
        var importId = import.Id;
        var profileId = import.ProviderProfileId;

        SharedProviderImportTransitions.MarkAuthoritativelyAbsent(
            import,
            SharedProviderAvailabilityState.Missing,
            InitialTimestamp.AddHours(3));

        Assert.Equal(importId, import.Id);
        Assert.Equal(profileId, import.ProviderProfileId);
        Assert.Equal(PublicationId, import.RemotePublicationId);
        Assert.Equal(SharedProviderSelectionState.Selected, import.SelectionState);
        Assert.Equal(SharedProviderAvailabilityState.Missing, import.AvailabilityState);
    }

    [Fact]
    public void AuthoritativeUnpublishIsDistinctFromMissingAndRejectsTransientState()
    {
        var import = CreateImport();

        SharedProviderImportTransitions.MarkAuthoritativelyAbsent(
            import,
            SharedProviderAvailabilityState.Unpublished,
            InitialTimestamp.AddHours(3));

        Assert.Equal(SharedProviderAvailabilityState.Unpublished, import.AvailabilityState);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SharedProviderImportTransitions.MarkAuthoritativelyAbsent(
                import,
                SharedProviderAvailabilityState.SourceOffline,
                InitialTimestamp.AddHours(4)));
    }

    [Fact]
    public void ReappearanceReconcilesExistingImportInsteadOfCreatingNewIdentity()
    {
        var import = CreateImport();
        var importId = import.Id;
        var profileId = import.ProviderProfileId;
        SharedProviderImportTransitions.MarkAuthoritativelyAbsent(
            import,
            SharedProviderAvailabilityState.Missing,
            InitialTimestamp.AddHours(3));

        SharedProviderImportTransitions.ReconcileAvailable(
            import,
            CreateRemotePublicationState("Returned provider"),
            InitialTimestamp.AddHours(4));

        Assert.Equal(importId, import.Id);
        Assert.Equal(profileId, import.ProviderProfileId);
        Assert.Equal(SharedProviderAvailabilityState.Available, import.AvailabilityState);
        Assert.Equal("Returned provider", import.RemoteDisplayName);
    }

    [Fact]
    public void TransientFailureNeverConvertsImportToAuthoritativeMissing()
    {
        var import = CreateImport();
        var lastSeenAt = import.LastSeenAtUtc;

        SharedProviderImportTransitions.MarkTransientlyUnavailable(
            import,
            SharedProviderAvailabilityState.SourceOffline,
            InitialTimestamp.AddMinutes(15));

        Assert.Equal(SharedProviderAvailabilityState.SourceOffline, import.AvailabilityState);
        Assert.Equal(lastSeenAt, import.LastSeenAtUtc);
        Assert.NotEqual(SharedProviderAvailabilityState.Missing, import.AvailabilityState);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SharedProviderImportTransitions.MarkTransientlyUnavailable(
                import,
                SharedProviderAvailabilityState.Missing,
                InitialTimestamp.AddMinutes(16)));
    }

    [Fact]
    public void RetireAndReactivatePreserveRemoteAndLocalIdentity()
    {
        var import = CreateImport();
        var importId = import.Id;
        var profileId = import.ProviderProfileId;

        SharedProviderImportTransitions.Retire(import, InitialTimestamp.AddHours(5));
        Assert.Equal(SharedProviderSelectionState.Retired, import.SelectionState);

        SharedProviderImportTransitions.Reactivate(import, InitialTimestamp.AddHours(6));

        Assert.Equal(importId, import.Id);
        Assert.Equal(profileId, import.ProviderProfileId);
        Assert.Equal(PublicationId, import.RemotePublicationId);
        Assert.Equal(SharedProviderSelectionState.Selected, import.SelectionState);
    }

    [Fact]
    public void InvocationShapeIsMetadataOnlyAndFinalizationIsTruthfulAndIdempotent()
    {
        var invocation = CreateInvocation();
        var propertyNames = typeof(SharedProviderInvocationRecord)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();
        string[] forbiddenFragments =
        [
            "Body",
            "Prompt",
            "Response",
            "Image",
            "Attachment",
            "ToolArgument",
            "Secret",
            "Authorization"
        ];
        var completion = new SharedProviderInvocationCompletion(
            SharedProviderInvocationOutcome.Succeeded,
            InitialTimestamp.AddSeconds(2),
            FailureCategory: null,
            InputTokenCount: 12,
            OutputTokenCount: 8,
            UsageCompleteness: SharedProviderMetadataCompleteness.Complete,
            Price: 0.0123m,
            PricingCompleteness: SharedProviderMetadataCompleteness.Complete);

        Assert.DoesNotContain(propertyNames, propertyName =>
            propertyName != nameof(SharedProviderInvocationRecord.ImageCount) &&
            forbiddenFragments.Any(fragment =>
                propertyName.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(
            nameof(SharedProviderInvocationRecord.ImageCount),
            Assert.Single(propertyNames, propertyName =>
                propertyName.Contains("Image", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(
            typeof(SharedProviderInvocationCompletion).GetConstructors(),
            constructor => constructor.GetParameters().Length == 8);
        Assert.Equal(
            8,
            typeof(SharedProviderInvocationCompletion).GetMethod("Deconstruct")!.GetParameters().Length);
        Assert.True(ProviderUsageWorkloadSelection.SharedProviderRelays.Includes(
            ProviderUsageWorkloadKind.SharedProviderRelay));
        Assert.True(ProviderUsageWorkloadSelection.All.Includes(
            ProviderUsageWorkloadKind.SharedProviderRelay));
        Assert.False(ProviderUsageWorkloadSelection.Both.Includes(
            ProviderUsageWorkloadKind.SharedProviderRelay));
        Assert.Equal(
            (int)ProviderUsageWorkloadKind.SharedProviderRelay,
            (int)ProviderUsageConsumerKind.SharedProviderRelay);

        SharedProviderInvocationTransitions.Finalize(invocation, completion);
        SharedProviderInvocationTransitions.Finalize(invocation, completion);

        Assert.Equal(SharedProviderInvocationOutcome.Succeeded, invocation.Outcome);
        Assert.Equal(2_000, invocation.DurationMilliseconds);
        Assert.Equal(12, invocation.InputTokenCount);
        Assert.Equal(8, invocation.OutputTokenCount);
        Assert.Equal(0.0123m, invocation.Price);
        Assert.Throws<InvalidOperationException>(() => SharedProviderInvocationTransitions.Finalize(
            invocation,
            completion with
            {
                OutputTokenCount = 9
            }));
        var incompleteInvocation = CreateInvocation();
        var incompleteCompletion = new SharedProviderInvocationCompletion(
            SharedProviderInvocationOutcome.Failed,
            InitialTimestamp.AddSeconds(1),
            SharedProviderFailureCategory.UpstreamFailure,
            InputTokenCount: null,
            OutputTokenCount: null,
            UsageCompleteness: SharedProviderMetadataCompleteness.Unavailable,
            Price: null,
            PricingCompleteness: SharedProviderMetadataCompleteness.Unavailable);

        SharedProviderInvocationTransitions.Finalize(incompleteInvocation, incompleteCompletion);

        Assert.Null(incompleteInvocation.InputTokenCount);
        Assert.Null(incompleteInvocation.OutputTokenCount);
        Assert.Null(incompleteInvocation.Price);
        Assert.Equal(
            SharedProviderMetadataCompleteness.Unavailable,
            incompleteInvocation.UsageCompleteness);

        var imageCompletion = completion with
        {
            InputTokenCount = null,
            OutputTokenCount = null,
            Price = null,
            PricingCompleteness = SharedProviderMetadataCompleteness.Unavailable,
            ImageCount = 2
        };
        var imageInvocation = CreateInvocation(
            SharedProviderRelayOperation.ImageGenerations,
            "request-image");

        SharedProviderInvocationTransitions.Finalize(imageInvocation, imageCompletion);
        SharedProviderInvocationTransitions.Finalize(imageInvocation, imageCompletion);

        Assert.Equal(2, imageInvocation.ImageCount);
        Assert.Null(imageInvocation.InputTokenCount);
        Assert.Null(imageInvocation.OutputTokenCount);
        Assert.Throws<InvalidOperationException>(() => SharedProviderInvocationTransitions.Finalize(
            imageInvocation,
            imageCompletion with { ImageCount = 3 }));
        Assert.Throws<ArgumentException>(() => SharedProviderInvocationTransitions.Finalize(
            CreateInvocation(requestId: "request-chat-image"),
            imageCompletion));
        Assert.Throws<ArgumentException>(() => SharedProviderInvocationTransitions.Finalize(
            CreateInvocation(
                SharedProviderRelayOperation.ImageGenerations,
                "request-image-tokens"),
            completion));
        Assert.Throws<ArgumentException>(() => SharedProviderInvocationTransitions.Finalize(
            CreateInvocation(requestId: "request-partial-pair"),
            completion with { UsageCompleteness = SharedProviderMetadataCompleteness.Partial }));
        Assert.Throws<ArgumentOutOfRangeException>(() => SharedProviderInvocationTransitions.Finalize(
            CreateInvocation(
                SharedProviderRelayOperation.ImageGenerations,
                "request-zero-images"),
            imageCompletion with { ImageCount = 0 }));

        var interruptedImageInvocation = CreateInvocation(
            SharedProviderRelayOperation.ImageGenerations,
            "request-interrupted-image");
        Assert.True(SharedProviderInvocationTransitions.RecoverInterruptedFinalization(
            interruptedImageInvocation,
            InitialTimestamp.AddSeconds(3)));
        Assert.Null(interruptedImageInvocation.ImageCount);
        Assert.Equal(
            SharedProviderMetadataCompleteness.Unavailable,
            interruptedImageInvocation.UsageCompleteness);
    }

    [Fact]
    public void ServiceIdentityUsesOneStableRowAndTypedPublicIdentity()
    {
        var first = SharedProviderServiceIdentity.Create(
            SourceInstanceId,
            InitialTimestamp);
        var second = SharedProviderServiceIdentity.Create(
            SourceInstanceId,
            InitialTimestamp.AddHours(1));

        Assert.Equal(SharedProviderServiceIdentity.SingletonId, first.Id);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(SourceInstanceId, first.PublicId);
        Assert.Equal(SourceInstanceId, second.PublicId);
        Assert.Throws<ArgumentException>(() => SharedProviderServiceIdentity.Create(
            default,
            InitialTimestamp));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Relay_late_actual_completion_reconciles_cancellation_or_recovery(bool recovered) {
        var invocation = CreateInvocation();
        if (recovered) {
            SharedProviderInvocationTransitions.RecoverInterruptedFinalization(invocation, InitialTimestamp.AddSeconds(5));
        } else {
            SharedProviderInvocationTransitions.Finalize(invocation, new(SharedProviderInvocationOutcome.Cancelled,
                InitialTimestamp.AddSeconds(1), SharedProviderFailureCategory.Cancelled, null, null,
                SharedProviderMetadataCompleteness.Unavailable, null, SharedProviderMetadataCompleteness.Unavailable));
        }
        var actual = new SharedProviderInvocationCompletion(SharedProviderInvocationOutcome.Succeeded,
            InitialTimestamp.AddSeconds(2), null, 10, 5, SharedProviderMetadataCompleteness.Complete,
            0.01m, SharedProviderMetadataCompleteness.Complete);
        SharedProviderInvocationTransitions.Finalize(invocation, actual);
        SharedProviderInvocationTransitions.Finalize(invocation, actual);
        Assert.Equal(SharedProviderInvocationOutcome.Succeeded, invocation.Outcome);
        Assert.Equal(10, invocation.InputTokenCount);
        Assert.Equal(0.01m, invocation.Price);
        SharedProviderInvocationTransitions.Finalize(invocation, actual with {
            Outcome = SharedProviderInvocationOutcome.Cancelled, FailureCategory = SharedProviderFailureCategory.Cancelled,
            InputTokenCount = null, OutputTokenCount = null, UsageCompleteness = SharedProviderMetadataCompleteness.Unavailable,
            Price = null, PricingCompleteness = SharedProviderMetadataCompleteness.Unavailable
        });
        Assert.Equal(SharedProviderInvocationOutcome.Succeeded, invocation.Outcome);
    }

    private static ProviderSharePublication CreatePublication()
        => SharedProviderPublicationTransitions.Create(
            Guid.Parse("aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb"),
            PublicationId,
            InitialTimestamp);

    private static SharedProviderSource CreateSource()
        => SharedProviderSourceTransitions.Create(
            "Central",
            "https://central.example.test",
            Guid.Parse("11111111-aaaa-bbbb-cccc-222222222222"),
            allowInsecurePrivateNetwork: false,
            isEnabled: true,
            InitialTimestamp);

    private static SharedProviderSource CreateSynchronizedSource()
    {
        var source = CreateSource();
        SharedProviderSourceTransitions.ApplySuccessfulCatalog(
            source,
            SourceInstanceId,
            CreateEntityTag('a'),
            InitialTimestamp.AddMinutes(1));
        return source;
    }

    private static SharedProviderImport CreateImport()
        => SharedProviderImportTransitions.Create(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            CreateRemotePublicationState("Remote OpenAI"),
            InitialTimestamp);

    private static SharedProviderRemotePublicationState CreateRemotePublicationState(string displayName)
    {
        var routingModelId = SharedProviderRoutingModelIdCodec.Create(PublicationId, "gpt-4.1");
        var publication = new SharedProviderCatalogPublication(
            PublicationId,
            new SharedProviderPublicRevision($"sha256:{new string('0', 64)}"),
            displayName,
            SharedProviderPurpose.Chat,
            SharedProviderTransport.OpenAiCompatible,
            routingModelId,
            [
                new SharedProviderCatalogModel(
                    routingModelId,
                    "GPT 4.1",
                    [
                        SharedProviderCapability.ChatCompletions,
                        SharedProviderCapability.Streaming
                    ])
            ],
            new SharedProviderCatalogHealth(SharedProviderHealthState.Available));

        return SharedProviderRemotePublicationState.Create(
            publication with
            {
                Revision = SharedProviderCanonicalRevision.ComputePublication(publication)
            });
    }

    private static SharedProviderInvocationRecord CreateInvocation(
        SharedProviderRelayOperation operation = SharedProviderRelayOperation.ChatCompletions,
        string requestId = "request-001")
    {
        var routingModelId = SharedProviderRoutingModelIdCodec.Create(PublicationId, "gpt-4.1");
        return SharedProviderInvocationTransitions.Create(
            requestId,
            PublicationId,
            Guid.Parse("aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb"),
            "subject-123",
            new AccessContextReference("project:42"),
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            "correlation-123",
            operation,
            routingModelId,
            "gpt-4.1",
            InitialTimestamp,
            InitialTimestamp.AddDays(30));
    }

    private static SharedProviderCatalogEntityTag CreateEntityTag(char hashCharacter)
        => new($"\"sha256:{new string(hashCharacter, 64)}\"");
}
