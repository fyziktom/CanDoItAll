using System.Text.Json;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class SharedProviderProtocolContractTests
{
    private static readonly SharedProviderSourceInstanceId SourceInstanceId =
        new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

    private static readonly SharedProviderPublicationId PublicationId =
        new(Guid.Parse("11111111-2222-3333-4444-555555555555"));

    private static readonly SharedProviderPublicRevision PlaceholderRevision =
        new($"sha256:{new string('a', 64)}");

    [Fact]
    public void RoutesExposeOnlyTheVersionedCatalogAndSupportedOpenAiSubset()
    {
        Assert.Equal("/api/shared-providers/v1/catalog", SharedProviderRoutes.Catalog);
        Assert.Equal("/api/shared-providers/openai/v1", SharedProviderRoutes.OpenAiBase);
        Assert.Equal("/api/shared-providers/openai/v1/models", SharedProviderRoutes.Models);
        Assert.Equal("/api/shared-providers/openai/v1/responses", SharedProviderRoutes.Responses);
        Assert.Equal("/api/shared-providers/openai/v1/chat/completions", SharedProviderRoutes.ChatCompletions);
        Assert.Equal("/api/shared-providers/openai/v1/images/generations", SharedProviderRoutes.ImageGenerations);
    }

    [Fact]
    public void RouteResolutionPreservesAReverseProxyBasePath()
    {
        var sourceBaseUri = new Uri("https://central.example.test/tenant/acme?private=value#section");

        var catalogUri = SharedProviderRoutes.ResolveCatalog(sourceBaseUri);
        var openAiBaseUri = SharedProviderRoutes.ResolveOpenAiBase(sourceBaseUri);

        Assert.Equal(
            "https://central.example.test/tenant/acme/api/shared-providers/v1/catalog",
            catalogUri.AbsoluteUri);
        Assert.Equal(
            "https://central.example.test/tenant/acme/api/shared-providers/openai/v1",
            openAiBaseUri.AbsoluteUri);
        Assert.Empty(catalogUri.Query);
        Assert.Empty(catalogUri.Fragment);
        Assert.Empty(openAiBaseUri.Query);
        Assert.Empty(openAiBaseUri.Fragment);
    }

    [Fact]
    public void CatalogSerializationUsesTheFrozenPublicNamesAndStringValues()
    {
        var catalog = CreateCatalog();
        var json = SharedProviderProtocolJson.SerializeCatalog(catalog);
        var publication = catalog.Providers[0];
        Assert.Equal(
            "sha256:bfd5f054f8954c4e27c528cbd946f1712cc5b99099455475bedc5ed2e573a044",
            publication.Revision.Value);
        Assert.Equal(
            "sha256:3d6412b25bcac91e302ab962367d4592083f5226f35f237704ff1ba46fa2b197",
            catalog.CatalogRevision.Value);
        var expected = $"{{\"schemaVersion\":\"1.0\",\"sourceInstanceId\":\"{SourceInstanceId}\",\"catalogRevision\":\"{catalog.CatalogRevision}\",\"protocols\":{{\"openAiCompatibleBasePath\":\"/api/shared-providers/openai/v1\"}},\"providers\":[{{\"publicationId\":\"{PublicationId}\",\"revision\":\"{publication.Revision}\",\"displayName\":\"Central OpenAI\",\"purpose\":\"chat\",\"transport\":\"openai-compatible\",\"defaultModelId\":\"{publication.DefaultModelId}\",\"models\":[{{\"id\":\"{publication.DefaultModelId}\",\"displayName\":\"GPT 4.1\",\"capabilities\":[\"chat-completions\",\"function-tools\",\"responses\",\"streaming\",\"structured-output\"]}}],\"health\":{{\"state\":\"available\"}}}}]}}";

        Assert.Equal(expected, json);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var provider = root.GetProperty("providers")[0];
        var model = provider.GetProperty("models")[0];

        Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
        Assert.Equal(SourceInstanceId.ToString(), root.GetProperty("sourceInstanceId").GetString());
        Assert.Equal(SharedProviderRoutes.OpenAiBase, root.GetProperty("protocols")
            .GetProperty("openAiCompatibleBasePath")
            .GetString());
        Assert.Equal("chat", provider.GetProperty("purpose").GetString());
        Assert.Equal("openai-compatible", provider.GetProperty("transport").GetString());
        Assert.Equal("chat-completions", model.GetProperty("capabilities")[0].GetString());
    }

    [Fact]
    public void CatalogRoundTripPreservesTypedPublicIdentitiesAndCapabilities()
    {
        var expected = CreateCatalog();

        var actual = SharedProviderProtocolJson.DeserializeCatalog(
            SharedProviderProtocolJson.SerializeCatalog(expected));

        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
        Assert.Equal(expected.SourceInstanceId, actual.SourceInstanceId);
        Assert.Equal(expected.CatalogRevision, actual.CatalogRevision);
        Assert.Equal(expected.Providers[0].PublicationId, actual.Providers[0].PublicationId);
        Assert.Equal(expected.Providers[0].Models[0].Id, actual.Providers[0].Models[0].Id);
        Assert.Equal(
            [
                SharedProviderCapability.ChatCompletions,
                SharedProviderCapability.FunctionTools,
                SharedProviderCapability.Responses,
                SharedProviderCapability.Streaming,
                SharedProviderCapability.StructuredOutput
            ],
            actual.Providers[0].Models[0].Capabilities);
    }

    [Fact]
    public void UnknownTopLevelMemberIsRejected()
    {
        var json = SharedProviderProtocolJson.SerializeCatalog(CreateCatalog());
        var withUnknownMember = json.Insert(json.Length - 1, ",\"unexpected\":true");

        Assert.Throws<JsonException>(() => SharedProviderProtocolJson.DeserializeCatalog(withUnknownMember));
    }

    [Fact]
    public void UnknownNestedMemberIsRejected()
    {
        var json = SharedProviderProtocolJson.SerializeCatalog(CreateCatalog());
        var withUnknownMember = json.Replace(
            "\"displayName\":\"GPT 4.1\"",
            "\"displayName\":\"GPT 4.1\",\"upstreamBaseUri\":\"https://private.example\"",
            StringComparison.Ordinal);

        Assert.Throws<JsonException>(() => SharedProviderProtocolJson.DeserializeCatalog(withUnknownMember));
        var publication = CreateCatalog().Providers[0];
        Assert.Throws<JsonException>(() =>
            SharedProviderCanonicalRevision.ComputePublication(
                publication with
                {
                    DisplayName = "Unsafe\r\nName"
                }));
        Assert.Throws<JsonException>(() =>
            SharedProviderCanonicalRevision.ComputePublication(
                publication with
                {
                    DisplayName = "Invalid \uD800 name"
                }));
    }

    [Fact]
    public void UnsupportedProtocolVersionIsRejected()
    {
        var json = SharedProviderProtocolJson.SerializeCatalog(CreateCatalog())
            .Replace("\"schemaVersion\":\"1.0\"", "\"schemaVersion\":\"2.0\"", StringComparison.Ordinal);

        Assert.Throws<JsonException>(() => SharedProviderProtocolJson.DeserializeCatalog(json));
    }

    [Fact]
    public void InvalidMissingOrIncoherentProtocolMembersAreRejected()
    {
        var validJson = SharedProviderProtocolJson.SerializeCatalog(CreateCatalog());
        string[] invalidDocuments =
        [
            validJson.Replace("\"purpose\":\"chat\"", "\"purpose\":0", StringComparison.Ordinal),
            validJson.Replace("\"purpose\":\"chat\"", "\"purpose\":\"audio\"", StringComparison.Ordinal),
            validJson.Replace("\"purpose\":\"chat\"", "\"purpose\":\"Chat\"", StringComparison.Ordinal),
            validJson.Replace(
                "\"purpose\":\"chat\"",
                "\"purpose\":\"chat\",\"purpose\":\"chat\"",
                StringComparison.Ordinal),
            validJson.Replace(
                "\"purpose\":\"chat\"",
                "\"purpose\":\"chat, image-generation\"",
                StringComparison.Ordinal),
            validJson.Replace("\"purpose\":\"chat\",", string.Empty, StringComparison.Ordinal),
            validJson.Replace("\"transport\":\"openai-compatible\",", string.Empty, StringComparison.Ordinal),
            validJson.Replace("\"health\":{\"state\":\"available\"}", "\"health\":{}", StringComparison.Ordinal),
            validJson.Replace(",\"health\":{\"state\":\"available\"}", string.Empty, StringComparison.Ordinal)
        ];

        Assert.All(
            invalidDocuments,
            json => Assert.Throws<JsonException>(() => SharedProviderProtocolJson.DeserializeCatalog(json)));

        var publication = CreateCatalog().Providers[0];
        var model = publication.Models[0];
        (SharedProviderPurpose Purpose, SharedProviderCapability[] Capabilities)[] incoherent =
        [
            (SharedProviderPurpose.Chat, [SharedProviderCapability.Streaming]),
            (
                SharedProviderPurpose.Chat,
                [SharedProviderCapability.Responses, SharedProviderCapability.ParallelFunctionTools]),
            (
                SharedProviderPurpose.Chat,
                [SharedProviderCapability.ChatCompletions, SharedProviderCapability.ImageGenerations]),
            (
                SharedProviderPurpose.ImageGeneration,
                [SharedProviderCapability.ImageGenerations, SharedProviderCapability.Streaming]),
            (SharedProviderPurpose.ImageGeneration, [SharedProviderCapability.Base64Json])
        ];
        Assert.All(
            incoherent,
            item => Assert.Throws<JsonException>(() =>
                SharedProviderCanonicalRevision.ComputePublication(
                    publication with
                    {
                        Purpose = item.Purpose,
                        Models =
                        [
                            model with
                            {
                                Capabilities = item.Capabilities
                            }
                        ]
                    })));

        var validImagePublication = publication with
        {
            Purpose = SharedProviderPurpose.ImageGeneration,
            Models =
            [
                model with
                {
                    Capabilities =
                    [
                        SharedProviderCapability.ImageGenerations,
                        SharedProviderCapability.Base64Json
                    ]
                }
            ]
        };
        Assert.StartsWith(
            SharedProviderPublicRevision.Prefix,
            SharedProviderCanonicalRevision.ComputePublication(validImagePublication).Value,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ModelRouteFromAnotherPublicationIsRejected()
    {
        var otherPublicationId = new SharedProviderPublicationId(
            Guid.Parse("99999999-8888-7777-6666-555555555555"));
        var catalog = CreateCatalog();
        var crossPublicationModelId = SharedProviderRoutingModelIdCodec.Create(otherPublicationId, "gpt-4.1");
        var publication = catalog.Providers[0];
        var invalidModel = publication.Models[0] with
        {
            Id = crossPublicationModelId
        };
        catalog = catalog with
        {
            Providers =
            [
                publication with
                {
                    DefaultModelId = crossPublicationModelId,
                    Models = [invalidModel]
                }
            ]
        };

        Assert.Throws<JsonException>(() => SharedProviderProtocolJson.SerializeCatalog(catalog));
    }

    [Fact]
    public void CanonicalRepresentationSortsInputsAndTracksPublicHealth()
    {
        var first = CreateCatalog().Providers[0];
        var firstAlternateModelId = SharedProviderRoutingModelIdCodec.Create(PublicationId, "gpt-4.1-mini");
        var firstAlternateModel = new SharedProviderCatalogModel(
            firstAlternateModelId,
            "GPT 4.1 Mini",
            [SharedProviderCapability.Responses, SharedProviderCapability.ChatCompletions]);
        first = first with
        {
            Models = [first.Models[0], firstAlternateModel]
        };

        var secondPublicationId = new SharedProviderPublicationId(
            Guid.Parse("99999999-8888-7777-6666-555555555555"));
        var secondModelId = SharedProviderRoutingModelIdCodec.Create(secondPublicationId, "model-b");
        var second = new SharedProviderCatalogPublication(
            secondPublicationId,
            new SharedProviderPublicRevision($"sha256:{new string('b', 64)}"),
            "Second provider",
            SharedProviderPurpose.Chat,
            SharedProviderTransport.OpenAiCompatible,
            secondModelId,
            [
                new SharedProviderCatalogModel(
                    secondModelId,
                    "Model B",
                    [SharedProviderCapability.Streaming, SharedProviderCapability.Responses])
            ],
            new SharedProviderCatalogHealth(SharedProviderHealthState.Available));
        var firstReordered = first with
        {
            Models =
            [
                firstAlternateModel with
                {
                    Capabilities = firstAlternateModel.Capabilities.Reverse().ToArray()
                },
                first.Models[0] with
                {
                    Capabilities = first.Models[0].Capabilities.Reverse().ToArray()
                }
            ],
            Health = new SharedProviderCatalogHealth(SharedProviderHealthState.Available)
        };
        var secondChangedHealth = second with
        {
            Models =
            [
                second.Models[0] with
                {
                    Capabilities = second.Models[0].Capabilities.Reverse().ToArray()
                }
            ],
            Health = new SharedProviderCatalogHealth(SharedProviderHealthState.Available)
        };
        var forward = WithComputedRevisions(CreateCatalog() with
        {
            Providers = [first, second]
        });
        var reversedAndChanged = WithComputedRevisions(CreateCatalog() with
        {
            Providers = [secondChangedHealth, firstReordered]
        });

        Assert.Equal(
            SharedProviderCanonicalRevision.ComputePublication(first),
            SharedProviderCanonicalRevision.ComputePublication(firstReordered));
        Assert.Equal(
            SharedProviderCanonicalRevision.ComputeCatalog(forward),
            SharedProviderCanonicalRevision.ComputeCatalog(reversedAndChanged));
        Assert.Equal(
            SharedProviderProtocolJson.SerializeCatalog(forward),
            SharedProviderProtocolJson.SerializeCatalog(reversedAndChanged));
        Assert.NotEqual(
            SharedProviderCanonicalRevision.ComputePublication(first),
            SharedProviderCanonicalRevision.ComputePublication(
                first with
                {
                    Health = new SharedProviderCatalogHealth(SharedProviderHealthState.Unavailable)
                }));
    }

    [Fact]
    public void PublicContractsRejectSecretsInvalidDefaultsAndIncoherentPorts()
    {
        var json = SharedProviderProtocolJson.SerializeCatalog(CreateCatalog());

        Assert.DoesNotContain("ProviderProfileId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ApiKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UpstreamBaseUri", json, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Serialize(default(SharedProviderPublicationId), SharedProviderProtocolJson.Options));
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Serialize(default(SharedProviderSourceInstanceId), SharedProviderProtocolJson.Options));
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Serialize(default(SharedProviderPublicRevision), SharedProviderProtocolJson.Options));
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Serialize(default(SharedProviderRoutingModelId), SharedProviderProtocolJson.Options));
        Assert.Throws<InvalidOperationException>(() => default(SharedProviderPublicationId).ToString());
        Assert.Throws<InvalidOperationException>(() => default(SharedProviderSourceInstanceId).ToString());
        Assert.Throws<InvalidOperationException>(() => default(SharedProviderPublicRevision).ToString());
        Assert.Throws<InvalidOperationException>(() => default(SharedProviderProtocolVersion).ToString());
        Assert.Throws<InvalidOperationException>(() => default(SharedProviderRoutingModelId).ToString());
        Assert.Throws<InvalidOperationException>(() => default(SharedProviderFailureCode).ToString());
        Assert.Throws<InvalidOperationException>(() => default(SharedProviderCatalogEntityTag).ToString());
        Assert.Throws<InvalidOperationException>(() => default(AccessContextReference).ToString());

        var support = new SharedProviderRelaySupportDescriptor(
            new HashSet<SharedProviderRelayOperation>
            {
                SharedProviderRelayOperation.ChatCompletions,
                SharedProviderRelayOperation.ImageGenerations
            },
            SharedProviderStreamingMode.ServerSentEvents,
            supportsFunctionTools: true,
            supportsParallelFunctionTools: true,
            supportsStructuredOutput: true,
            supportsVisionInput: true,
            supportsBase64Images: true,
            maximumRequestBytes: 1_024,
            maximumOutputTokens: 1_024,
            maximumImageCount: 2);
        Assert.Contains(SharedProviderRelayOperation.ChatCompletions, support.Operations);
        Assert.Throws<ArgumentException>(() => new SharedProviderRelaySupportDescriptor(
            new HashSet<SharedProviderRelayOperation>(),
            SharedProviderStreamingMode.None,
            false,
            false,
            false,
            false,
            false,
            1,
            1,
            1));
        Assert.Throws<ArgumentException>(() => new SharedProviderRelaySupportDescriptor(
            new HashSet<SharedProviderRelayOperation>
            {
                SharedProviderRelayOperation.Responses
            },
            SharedProviderStreamingMode.None,
            false,
            true,
            false,
            false,
            false,
            1,
            1,
            1));
        Assert.Throws<ArgumentException>(() => new SharedProviderRelaySupportDescriptor(
            new HashSet<SharedProviderRelayOperation>
            {
                SharedProviderRelayOperation.ChatCompletions
            },
            SharedProviderStreamingMode.None,
            false,
            false,
            false,
            false,
            true,
            1,
            1,
            1));

        var failure = new SharedProviderFailure(
            SharedProviderFailureCategory.Validation,
            new SharedProviderFailureCode("shared-provider.invalid"),
            "The request is invalid.",
            "model");
        Assert.Equal(SharedProviderFailureCategory.Validation, failure.Category);
        Assert.Throws<ArgumentException>(() => new SharedProviderFailure(
            SharedProviderFailureCategory.Validation,
            new SharedProviderFailureCode("shared-provider.invalid"),
            "Unsafe\r\nmessage"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SharedProviderFailure(
            SharedProviderFailureCategory.RateLimited,
            new SharedProviderFailureCode("shared-provider.rate-limited"),
            "Try later.",
            retryAfter: TimeSpan.FromSeconds(-1)));

        var entityTag = SharedProviderCatalogEntityTag.FromRevision(CreateCatalog().CatalogRevision);
        Assert.Throws<ArgumentException>(() => new SharedProviderCatalogFetchResult.Succeeded(
            CreateCatalog(),
            SharedProviderCatalogEntityTag.FromRevision(
                new SharedProviderPublicRevision($"sha256:{new string('f', 64)}"))));
        var mutableProviders = CreateCatalog().Providers.ToList();
        var mutableCatalog = CreateCatalog() with
        {
            Providers = mutableProviders
        };
        var succeeded = new SharedProviderCatalogFetchResult.Succeeded(mutableCatalog, entityTag);
        mutableProviders.Clear();
        Assert.Single(succeeded.Catalog.Providers);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<SharedProviderCatalogPublication>)succeeded.Catalog.Providers).Clear());

        var routingModelId = CreateCatalog().Providers[0].DefaultModelId;
        var inferenceRequest = new SharedProviderInferenceTransportRequest(
            new Uri("https://central.example.test/root"),
            SharedProviderRelayOperation.ChatCompletions,
            routingModelId,
            "{}",
            stream: true);
        Assert.True(inferenceRequest.Stream);
        Assert.Throws<ArgumentException>(() => new SharedProviderInferenceTransportRequest(
            new Uri("https://central.example.test/root"),
            SharedProviderRelayOperation.ChatCompletions,
            routingModelId,
            string.Empty,
            stream: false));
        Assert.Throws<ArgumentException>(() => new SharedProviderInferenceTransportRequest(
            new Uri("https://central.example.test/root"),
            SharedProviderRelayOperation.ImageGenerations,
            routingModelId,
            "{}",
            stream: true));
        Assert.Throws<ArgumentException>(() => new SharedProviderCatalogFetchRequest(
            new Uri("https://central.example.test/root"),
            SharedProviderSourceNetworkPolicy.PublicOnly,
            new SharedProviderCatalogAccessToken("token"),
            ifNoneMatch: default(SharedProviderCatalogEntityTag)));
    }

    [Fact]
    public void AbstractionsAssemblyHasNoWebEfWorkspaceOrProviderSdkDependency()
    {
        var references = typeof(SharedProviderCatalogDocument).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, name => name.Contains("AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Workspace", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("OpenAI", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, name => name.Contains("AgentFramework", StringComparison.Ordinal));
    }

    private static SharedProviderCatalogDocument CreateCatalog()
    {
        var resolvedModelId = SharedProviderRoutingModelIdCodec.Create(PublicationId, "gpt-4.1");
        var model = new SharedProviderCatalogModel(
            resolvedModelId,
            "GPT 4.1",
            [
                SharedProviderCapability.ChatCompletions,
                SharedProviderCapability.Responses,
                SharedProviderCapability.Streaming,
                SharedProviderCapability.FunctionTools,
                SharedProviderCapability.StructuredOutput
            ]);
        var publication = new SharedProviderCatalogPublication(
            PublicationId,
            PlaceholderRevision,
            "Central OpenAI",
            SharedProviderPurpose.Chat,
            SharedProviderTransport.OpenAiCompatible,
            resolvedModelId,
            [model],
            new SharedProviderCatalogHealth(SharedProviderHealthState.Available));

        return WithComputedRevisions(new SharedProviderCatalogDocument(
            SharedProviderProtocolVersion.Current,
            SourceInstanceId,
            PlaceholderRevision,
            new SharedProviderProtocolDescriptor(SharedProviderRoutes.OpenAiBase),
            [publication]));
    }

    private static SharedProviderCatalogDocument WithComputedRevisions(SharedProviderCatalogDocument catalog)
    {
        var publications = catalog.Providers
            .Select(publication => publication with
            {
                Revision = SharedProviderCanonicalRevision.ComputePublication(publication)
            })
            .ToArray();
        var withPublicationRevisions = catalog with
        {
            Providers = publications
        };

        return withPublicationRevisions with
        {
            CatalogRevision = SharedProviderCanonicalRevision.ComputeCatalog(withPublicationRevisions)
        };
    }
}
