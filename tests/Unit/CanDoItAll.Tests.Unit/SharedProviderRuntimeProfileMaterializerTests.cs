using System.Text.Json;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Tests.Unit;

using AgentFrameworkProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using AgentFrameworkProviderPurpose = CanDoItAll.AgentFramework.Models.ProviderProfilePurpose;
using AgentFrameworkProviderTransport = CanDoItAll.AgentFramework.Models.ProviderTransportKind;

public sealed class SharedProviderRuntimeProfileMaterializerTests
{
    private const string ImportedSchemaVersion = "1.0";
    private const string SensitiveStatusMarker = "central-token-value-must-not-escape";
    private static readonly Guid SourceId =
        Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid SecretId =
        Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid ProfileId =
        Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
    private static readonly Guid ImportId =
        Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
    private static readonly SharedProviderSourceInstanceId SourceInstanceId =
        new(Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"));
    private static readonly SharedProviderPublicationId PublicationId =
        new(Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff"));

    [Fact]
    public void Materialize_ResponsesChat_MapsCanonicalEffectiveProfileWithoutSecretValue()
    {
        var graph = CreateGraph();
        graph.Profile.Name = "Locally renamed alias";

        var result = Materialize(graph);

        var profile = AssertAvailable(result);
        Assert.Equal("Locally renamed alias", profile.Name);
        Assert.Equal(AgentFrameworkProviderKind.OpenAi, profile.Kind);
        Assert.Equal(
            "https://central.example.test/reverse-proxy/api/shared-providers/openai/v1",
            profile.BaseUri.AbsoluteUri);
        Assert.Equal(SecretId, profile.SourceTokenSecretReferenceId);
        Assert.Equal(
            SharedProviderSourceNetworkPolicy.PublicOnly,
            profile.NetworkPolicy);
        Assert.Equal(SourceId, profile.SourceId);
        Assert.Equal(SourceInstanceId, profile.SourceInstanceId);
        Assert.Equal(ImportId, profile.ImportId);
        Assert.Equal(PublicationId, profile.PublicationId);
        Assert.Equal(AgentFrameworkProviderTransport.Responses, profile.Transport);
        Assert.Equal(AgentFrameworkProviderPurpose.Chat, profile.Purpose);
        Assert.True(profile.IsEnabled);
        Assert.True(profile.SupportsStreaming);
        Assert.True(profile.SupportsTools);
        Assert.True(profile.SupportsParallelTools);
        Assert.True(profile.SupportsStructuredOutput);
        Assert.True(profile.SupportsVision);
        Assert.False(profile.SupportsBase64Images);
        Assert.True(profile.PreferFrameworkManagedChatHistory);
        Assert.False(profile.SupportsBackgroundResponses);
        Assert.Equal(
            SharedProviderReconciliationCoordinator.ImportedConnectorPluginKey,
            profile.ConnectorPluginKey);
        Assert.Contains("shared", profile.Tags);
        Assert.Contains($"source:{SourceId:D}", profile.Tags);
        Assert.Contains($"publication:{PublicationId}", profile.Tags);
        Assert.DoesNotContain(
            SensitiveStatusMarker,
            JsonSerializer.Serialize(result),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Materialize_ChatCompletions_MapsHistoryModeAndPrivateNetworkPolicy()
    {
        var graph = CreateGraph(
            sourceBaseUri: "http://10.20.30.40/client/",
            allowPrivateNetwork: true,
            modelCapabilities:
            [
                [
                    SharedProviderCapability.ChatCompletions,
                    SharedProviderCapability.Streaming,
                    SharedProviderCapability.FunctionTools
                ]
            ]);

        var profile = AssertAvailable(Materialize(graph));

        Assert.Equal(AgentFrameworkProviderTransport.ChatCompletions, profile.Transport);
        Assert.Equal(
            "http://10.20.30.40/client/api/shared-providers/openai/v1",
            profile.BaseUri.AbsoluteUri);
        Assert.Equal(
            SharedProviderSourceNetworkPolicy.AllowPrivateNetwork,
            profile.NetworkPolicy);
        Assert.True(profile.PreferFrameworkManagedChatHistory);
        Assert.False(profile.SupportsBackgroundResponses);
        Assert.True(profile.SupportsStreaming);
        Assert.True(profile.SupportsTools);
        Assert.False(profile.SupportsParallelTools);
    }

    [Fact]
    public void Materialize_ImageGeneration_MapsPurposeAndImageCapabilities()
    {
        var graph = CreateGraph(
            purpose: SharedProviderPurpose.ImageGeneration,
            modelCapabilities:
            [
                [
                    SharedProviderCapability.ImageGenerations,
                    SharedProviderCapability.Base64Json
                ]
            ]);

        var profile = AssertAvailable(Materialize(graph));

        Assert.Equal(AgentFrameworkProviderKind.OpenAi, profile.Kind);
        Assert.Equal(AgentFrameworkProviderTransport.Responses, profile.Transport);
        Assert.Equal(AgentFrameworkProviderPurpose.ImageGeneration, profile.Purpose);
        Assert.True(profile.SupportsBase64Images);
        Assert.False(profile.SupportsStreaming);
        Assert.False(profile.SupportsTools);
        Assert.False(profile.SupportsStructuredOutput);
        Assert.False(profile.SupportsVision);
        Assert.False(profile.SupportsBackgroundResponses);
        Assert.Contains("image-generation", profile.Tags);
    }

    [Fact]
    public void Materialize_MultipleModels_PreservesCatalogAndUsesSafeCapabilityIntersection()
    {
        var graph = CreateGraph(
            modelCapabilities:
            [
                [
                    SharedProviderCapability.Responses,
                    SharedProviderCapability.Streaming,
                    SharedProviderCapability.FunctionTools,
                    SharedProviderCapability.StructuredOutput,
                    SharedProviderCapability.VisionInput
                ],
                [SharedProviderCapability.Responses]
            ]);

        var profile = AssertAvailable(Materialize(graph));

        Assert.Equal(2, profile.Models.Count);
        Assert.Equal(
            graph.Import.RemoteDefaultModelId,
            profile.DefaultModelId);
        Assert.All(profile.Models, model =>
            Assert.Contains(SharedProviderCapability.Responses, model.Capabilities));
        Assert.Contains(
            SharedProviderCapability.VisionInput,
            profile.Models.Single(model =>
                model.Id == profile.DefaultModelId).Capabilities);
        Assert.False(profile.SupportsStreaming);
        Assert.False(profile.SupportsTools);
        Assert.False(profile.SupportsStructuredOutput);
        Assert.False(profile.SupportsVision);
    }

    [Fact]
    public void Materialize_DegradedPublication_RemainsAvailableWithValidatedProjection()
    {
        var graph = CreateGraph(health: SharedProviderHealthState.Degraded);

        var result = Materialize(graph);

        var profile = AssertAvailable(result);
        Assert.Equal(SharedProviderRuntimeProfileAvailability.Available, result.Availability);
        Assert.Contains("available", profile.Tags);
    }

    [Fact]
    public void Materialize_LocalProfileDisabled_RetainsAliasAndDisabledIntent()
    {
        var graph = CreateGraph();
        graph.Profile.Name = "My disabled shared alias";
        graph.Profile.IsEnabled = false;

        var result = Materialize(graph);

        var profile = AssertUnavailableWithProjection(
            result,
            SharedProviderRuntimeProfileAvailability.LocalProfileDisabled);
        Assert.Equal("My disabled shared alias", profile.Name);
        Assert.False(profile.IsEnabled);
        Assert.Contains("local-profile-disabled", profile.Tags);
    }

    [Fact]
    public void Materialize_SourceDisabledRetainsProjection_ButNeverSynchronizedDoesNot()
    {
        var graph = CreateGraph();
        graph.Source.IsEnabled = false;

        var disabled = Materialize(graph);

        AssertUnavailableWithProjection(
            disabled,
            SharedProviderRuntimeProfileAvailability.SourceDisabled);

        graph.Source.IsEnabled = true;
        graph.Source.Status = SharedProviderSourceStatus.NeverSynchronized;
        graph.Source.RemoteInstanceId = null;
        var neverSynchronized = Materialize(graph);
        AssertUnavailableWithoutProjection(
            neverSynchronized,
            SharedProviderRuntimeProfileAvailability.SourceNeverSynchronized);
    }

    [Fact]
    public void Materialize_SourceOffline_RetainsLastValidatedProjection()
    {
        var graph = CreateGraph();
        graph.Source.Status = SharedProviderSourceStatus.SourceOffline;

        var result = Materialize(graph);

        var profile = AssertUnavailableWithProjection(
            result,
            SharedProviderRuntimeProfileAvailability.SourceOffline);
        Assert.Contains("source-offline", profile.Tags);
    }

    [Fact]
    public void Materialize_AuthorizationFailed_RetainsLastValidatedProjection()
    {
        var graph = CreateGraph();
        graph.Source.Status = SharedProviderSourceStatus.AuthorizationFailed;

        var result = Materialize(graph);

        var profile = AssertUnavailableWithProjection(
            result,
            SharedProviderRuntimeProfileAvailability.AuthorizationFailed);
        Assert.Contains("authorization-failed", profile.Tags);
    }

    [Fact]
    public void Materialize_SourceIdentityMismatch_RetainsOnlyPreviouslyValidatedProjection()
    {
        var graph = CreateGraph();
        graph.Source.Status = SharedProviderSourceStatus.SourceIdentityMismatch;

        var result = Materialize(graph);

        var profile = AssertUnavailableWithProjection(
            result,
            SharedProviderRuntimeProfileAvailability.SourceIdentityMismatch);
        Assert.Equal(SourceInstanceId, profile.SourceInstanceId);
        Assert.Contains("source-identity-mismatch", profile.Tags);
    }

    [Fact]
    public void Materialize_IncompatibleContract_RetainsLastValidatedProjection()
    {
        var graph = CreateGraph();
        graph.Source.Status = SharedProviderSourceStatus.IncompatibleContract;

        var result = Materialize(graph);

        AssertUnavailableWithProjection(
            result,
            SharedProviderRuntimeProfileAvailability.IncompatibleContract);
    }

    [Fact]
    public void Materialize_RetiredImport_RetainsProjectionButCannotInvoke()
    {
        var graph = CreateGraph();
        graph.Import.SelectionState = SharedProviderSelectionState.Retired;

        var result = Materialize(graph);

        var profile = AssertUnavailableWithProjection(
            result,
            SharedProviderRuntimeProfileAvailability.Retired);
        Assert.Contains("retired", profile.Tags);
    }

    [Fact]
    public void Materialize_UnpublishedImport_RetainsProjectionButCannotInvoke()
    {
        var graph = CreateGraph();
        graph.Import.AvailabilityState = SharedProviderAvailabilityState.Unpublished;

        var result = Materialize(graph);

        AssertUnavailableWithProjection(
            result,
            SharedProviderRuntimeProfileAvailability.Unpublished);
    }

    [Fact]
    public void Materialize_MissingImport_RetainsProjectionButCannotInvoke()
    {
        var graph = CreateGraph();
        graph.Import.AvailabilityState = SharedProviderAvailabilityState.Missing;

        var result = Materialize(graph);

        AssertUnavailableWithProjection(
            result,
            SharedProviderRuntimeProfileAvailability.Missing);
    }

    [Fact]
    public void Materialize_RemoteUnavailableHealth_RetainsProjectionButCannotInvoke()
    {
        var graph = CreateGraph(health: SharedProviderHealthState.Unavailable);

        var result = Materialize(graph);

        var profile = AssertUnavailableWithProjection(
            result,
            SharedProviderRuntimeProfileAvailability.PublicationUnavailable);
        Assert.Contains("publication-unavailable", profile.Tags);
    }

    [Fact]
    public void Materialize_MissingOrMismatchedRelationship_ProducesNoProjection()
    {
        var graph = CreateGraph();

        AssertUnavailableWithoutProjection(
            Materializer.Materialize(null, graph.Import, graph.Source),
            SharedProviderRuntimeProfileAvailability.ProviderProfileMissing);
        AssertUnavailableWithoutProjection(
            Materializer.Materialize(graph.Profile, null, graph.Source),
            SharedProviderRuntimeProfileAvailability.ImportMissing);
        AssertUnavailableWithoutProjection(
            Materializer.Materialize(graph.Profile, graph.Import, null),
            SharedProviderRuntimeProfileAvailability.SourceMissing);

        graph.Import.ProviderProfileId = Guid.NewGuid();
        AssertUnavailableWithoutProjection(
            Materialize(graph),
            SharedProviderRuntimeProfileAvailability.RelationshipMismatch);
    }

    [Fact]
    public void Materialize_TamperedSnapshotOrDuplicatedImportFields_ProducesNoProjection()
    {
        var malformed = CreateGraph();
        malformed.Import.RemoteCatalogSnapshotJson = "{}";

        AssertUnavailableWithoutProjection(
            Materialize(malformed),
            SharedProviderRuntimeProfileAvailability.SnapshotInvalid);

        var mismatchedRevision = CreateGraph();
        mismatchedRevision.Import.RemoteRevision = new SharedProviderPublicRevision(
            $"{SharedProviderPublicRevision.Prefix}{new string('a', SharedProviderPublicRevision.HashLength)}");
        AssertUnavailableWithoutProjection(
            Materialize(mismatchedRevision),
            SharedProviderRuntimeProfileAvailability.SnapshotInvalid);

        var mismatchedPurpose = CreateGraph();
        mismatchedPurpose.Import.RemotePurpose = SharedProviderPurpose.ImageGeneration;
        AssertUnavailableWithoutProjection(
            Materialize(mismatchedPurpose),
            SharedProviderRuntimeProfileAvailability.SnapshotInvalid);
    }

    [Fact]
    public void Materialize_ForgedDerivedProfileCaches_ProducesNoProjection()
    {
        Action<ProviderProfile>[] mutations =
        [
            profile => profile.BaseUrl = "https://attacker.example.test/v1",
            profile => profile.ApiKeySecretId = Guid.NewGuid(),
            profile => profile.DefaultModel = "forged-model",
            profile => profile.SupportsStreaming = false,
            profile => profile.SupportsToolCalling = false,
            profile => profile.SupportsStructuredOutput = false,
            profile => profile.SupportsVision = false
        ];

        foreach (var mutate in mutations)
        {
            var graph = CreateGraph();
            mutate(graph.Profile);

            AssertUnavailableWithoutProjection(
                Materialize(graph),
                SharedProviderRuntimeProfileAvailability.ProfileCacheIntegrityMismatch);
        }
    }

    private static SharedProviderRuntimeProfileMaterializer Materializer { get; } = new();

    private static SharedProviderRuntimeProfileMaterializationResult Materialize(
        TestGraph graph)
        => Materializer.Materialize(graph.Profile, graph.Import, graph.Source);

    private static SharedProviderEffectiveRuntimeProfile AssertAvailable(
        SharedProviderRuntimeProfileMaterializationResult result)
    {
        Assert.True(result.IsAvailable);
        Assert.Equal(SharedProviderRuntimeProfileAvailability.Available, result.Availability);
        return Assert.IsType<SharedProviderEffectiveRuntimeProfile>(result.Profile);
    }

    private static SharedProviderEffectiveRuntimeProfile AssertUnavailableWithProjection(
        SharedProviderRuntimeProfileMaterializationResult result,
        SharedProviderRuntimeProfileAvailability availability)
    {
        Assert.False(result.IsAvailable);
        Assert.Equal(availability, result.Availability);
        return Assert.IsType<SharedProviderEffectiveRuntimeProfile>(result.Profile);
    }

    private static void AssertUnavailableWithoutProjection(
        SharedProviderRuntimeProfileMaterializationResult result,
        SharedProviderRuntimeProfileAvailability availability)
    {
        Assert.False(result.IsAvailable);
        Assert.Equal(availability, result.Availability);
        Assert.Null(result.Profile);
    }

    private static TestGraph CreateGraph(
        SharedProviderPurpose purpose = SharedProviderPurpose.Chat,
        SharedProviderHealthState health = SharedProviderHealthState.Available,
        string sourceBaseUri = "https://central.example.test/reverse-proxy/",
        bool allowPrivateNetwork = false,
        IReadOnlyList<IReadOnlyList<SharedProviderCapability>>? modelCapabilities = null)
    {
        modelCapabilities ??=
        [
            [
                SharedProviderCapability.Responses,
                SharedProviderCapability.Streaming,
                SharedProviderCapability.FunctionTools,
                SharedProviderCapability.ParallelFunctionTools,
                SharedProviderCapability.StructuredOutput,
                SharedProviderCapability.VisionInput
            ]
        ];
        var models = modelCapabilities
            .Select((capabilities, index) => new SharedProviderCatalogModel(
                SharedProviderRoutingModelIdCodec.Create(
                    PublicationId,
                    $"central-model-{index + 1}"),
                $"Remote model {index + 1}",
                Array.AsReadOnly(capabilities.ToArray())))
            .ToArray();
        var publication = new SharedProviderCatalogPublication(
            PublicationId,
            new SharedProviderPublicRevision(
                $"{SharedProviderPublicRevision.Prefix}{new string('0', SharedProviderPublicRevision.HashLength)}"),
            "Remote publication name",
            purpose,
            SharedProviderTransport.OpenAiCompatible,
            models[0].Id,
            Array.AsReadOnly(models),
            new SharedProviderCatalogHealth(health));
        publication = publication with
        {
            Revision = SharedProviderCanonicalRevision.ComputePublication(publication)
        };

        var source = SharedProviderSourceTransitions.Create(
            "Central source",
            sourceBaseUri,
            SecretId,
            allowPrivateNetwork,
            isEnabled: true,
            new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero));
        source.Id = SourceId;
        source.Status = SharedProviderSourceStatus.Available;
        source.RemoteInstanceId = SourceInstanceId;
        source.LastStatusMessage = SensitiveStatusMarker;

        var defaultCapabilities = publication.Models[0].Capabilities;
        var profile = new ProviderProfile
        {
            Id = ProfileId,
            Name = "Local shared alias",
            ProviderKind = ProviderKind.OpenAi,
            ConnectorPluginKey =
                SharedProviderReconciliationCoordinator.ImportedConnectorPluginKey,
            ConfigSchemaVersion = ImportedSchemaVersion,
            BaseUrl = SharedProviderRoutes.ResolveOpenAiBase(
                new Uri(source.BaseUri)).AbsoluteUri,
            ApiKeySecretId = SecretId,
            DefaultModel = publication.DefaultModelId.Value,
            TimeoutSeconds = 45,
            IsEnabled = true,
            SupportsStreaming = defaultCapabilities.Contains(
                SharedProviderCapability.Streaming),
            SupportsToolCalling = defaultCapabilities.Contains(
                SharedProviderCapability.FunctionTools),
            SupportsStructuredOutput = defaultCapabilities.Contains(
                SharedProviderCapability.StructuredOutput),
            SupportsVision = defaultCapabilities.Contains(
                SharedProviderCapability.VisionInput),
            ExtraSettingsJson = "{}",
            LastHealthStatus = SensitiveStatusMarker
        };
        var import = SharedProviderImportTransitions.Create(
            source.Id,
            profile.Id,
            SharedProviderRemotePublicationState.Create(publication),
            new DateTimeOffset(2026, 8, 25, 12, 1, 0, TimeSpan.Zero));
        import.Id = ImportId;
        return new TestGraph(profile, import, source);
    }

    private sealed record TestGraph(
        ProviderProfile Profile,
        SharedProviderImport Import,
        SharedProviderSource Source);
}
