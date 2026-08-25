using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration;

using AgentFrameworkProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using AgentFrameworkProviderProfileEditorModel =
    CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;
using WorkspaceProviderKind = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderKind;
using WorkspaceProviderProfile = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile;

public sealed class SharedProviderBackendCheckpointIntegrationTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    private static readonly DateTimeOffset Timestamp = new(
        2026,
        8,
        25,
        12,
        0,
        0,
        TimeSpan.Zero);

    [Fact]
    public async Task Catalog_publication_defaults_off_filters_ineligible_and_supports_sanitized_conditional_get()
    {
        await using var fixtureLease =
            await FixtureLease<SharedProviderCatalogApiFixture>.CreateAsync();
        var catalogFixture = fixtureLease.Value;
        var eligibleProfile = CreateWorkspaceProfile("Eligible central profile");
        var unpublished = CreatePublication(eligibleProfile.Id, isPublished: false);
        Assert.False(unpublished.IsPublished);

        var ineligibleProfile = CreateWorkspaceProfile("Disabled central profile");
        var projection = SharedProviderCatalogProjector.Project(
            new SharedProviderSourceInstanceId(
                Guid.Parse("8caaf6e1-8bba-4058-8a54-0c83ce2d206c")),
            [
                new SharedProviderCatalogProjectionSource(
                    CreatePublication(eligibleProfile.Id, isPublished: true),
                    eligibleProfile,
                    EligibleChat("duplicate-upstream-model")),
                new SharedProviderCatalogProjectionSource(
                    CreatePublication(ineligibleProfile.Id, isPublished: true),
                    ineligibleProfile,
                    new SharedProviderPublicationEligibility(
                        SharedProviderPublicationEligibilityCode.ProfileDisabled,
                        "The provider profile must be enabled before it can be published.",
                        Purpose: null,
                        Transport: null,
                        Models: [])),
                new SharedProviderCatalogProjectionSource(
                    unpublished,
                    eligibleProfile,
                    EligibleChat("unpublished-upstream-model"))
            ]);

        Assert.Single(projection.Catalog.Providers);
        Assert.Single(projection.RoutingIndex);
        string projectedJson = SharedProviderProtocolJson.SerializeCatalog(projection.Catalog);
        Assert.DoesNotContain(eligibleProfile.BaseUrl, projectedJson, StringComparison.Ordinal);
        Assert.DoesNotContain(eligibleProfile.ApiKeySecretId!.Value.ToString("D"), projectedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("duplicate-upstream-model", projectedJson, StringComparison.Ordinal);

        using var response = await catalogFixture.Host.Client.GetAsync(SharedProviderRoutes.Catalog);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(
            SharedProviderApiTestData.ForbiddenPublicContent,
            forbidden => body.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        var entityTag = response.Headers.ETag;
        Assert.NotNull(entityTag);

        using var conditionalRequest = new HttpRequestMessage(
            HttpMethod.Get,
            SharedProviderRoutes.Catalog);
        conditionalRequest.Headers.IfNoneMatch.Add(entityTag);
        using var notModified = await catalogFixture.Host.Client.SendAsync(conditionalRequest);

        Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);
        Assert.Equal(string.Empty, await notModified.Content.ReadAsStringAsync());
        Assert.Equal(entityTag, notModified.Headers.ETag);
    }

    [Fact]
    public async Task Catalog_and_inference_scopes_are_enforced_independently()
    {
        await using var fixtureLease =
            await FixtureLease<SharedProviderAuthorizationFixture>.CreateAsync();
        var authorizationFixture = fixtureLease.Value;
        var catalogToken = IssueToken(
            authorizationFixture.Host,
            ApiAccessScopeNames.ReadSharedProviderCatalog,
            "checkpoint-catalog-reader");
        var invokeToken = IssueToken(
            authorizationFixture.Host,
            ApiAccessScopeNames.InvokeSharedProviders,
            "checkpoint-invoker");

        using var catalogAllowed = await SendAsync(
            authorizationFixture.Host.Client,
            HttpMethod.Get,
            SharedProviderRoutes.Catalog,
            catalogToken);
        using var inferenceDenied = await SendAsync(
            authorizationFixture.Host.Client,
            HttpMethod.Post,
            SharedProviderRoutes.ChatCompletions,
            catalogToken,
            ChatJson(SharedProviderApiTestData.RoutingModelId));
        using var catalogDenied = await SendAsync(
            authorizationFixture.Host.Client,
            HttpMethod.Get,
            SharedProviderRoutes.Catalog,
            invokeToken);
        using var inferenceAuthorized = await SendAsync(
            authorizationFixture.Host.Client,
            HttpMethod.Post,
            SharedProviderRoutes.ChatCompletions,
            invokeToken,
            ChatJson(SharedProviderApiTestData.RoutingModelId));

        Assert.Equal(HttpStatusCode.OK, catalogAllowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, inferenceDenied.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, catalogDenied.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, inferenceAuthorized.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, inferenceAuthorized.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inferenceAuthorized.StatusCode);
    }

    [Fact]
    public void Public_model_ids_route_duplicate_upstream_model_names_without_cross_routing()
    {
        const string upstreamModel = "same-upstream-model";
        var firstProfile = CreateWorkspaceProfile("First central profile");
        var secondProfile = CreateWorkspaceProfile("Second central profile");
        var firstPublication = CreatePublication(firstProfile.Id, isPublished: true);
        var secondPublication = CreatePublication(secondProfile.Id, isPublished: true);
        var projection = SharedProviderCatalogProjector.Project(
            new SharedProviderSourceInstanceId(
                Guid.Parse("0ee7ef3d-dc0f-4475-bc8d-dba638299a9f")),
            [
                new SharedProviderCatalogProjectionSource(
                    firstPublication,
                    firstProfile,
                    EligibleChat(upstreamModel)),
                new SharedProviderCatalogProjectionSource(
                    secondPublication,
                    secondProfile,
                    EligibleChat(upstreamModel))
            ]);

        Assert.Equal(2, projection.RoutingIndex.Count);
        var firstModel = SharedProviderRoutingModelIdCodec.Create(
            firstPublication.PublicId,
            upstreamModel);
        var secondModel = SharedProviderRoutingModelIdCodec.Create(
            secondPublication.PublicId,
            upstreamModel);
        Assert.NotEqual(firstModel, secondModel);
        Assert.Equal(firstProfile.Id, projection.RoutingIndex[firstModel].ProviderProfileId);
        Assert.Equal(secondProfile.Id, projection.RoutingIndex[secondModel].ProviderProfileId);
        Assert.Equal(upstreamModel, projection.RoutingIndex[firstModel].UpstreamModelId);
        Assert.Equal(upstreamModel, projection.RoutingIndex[secondModel].UpstreamModelId);
        Assert.NotEqual(
            projection.RoutingIndex[firstModel].PublicationId,
            projection.RoutingIndex[secondModel].PublicationId);
    }

    [Fact]
    public async Task Chat_completions_and_responses_traverse_real_hosts_and_deterministic_upstream()
    {
        await using var fixtureLease =
            await FixtureLease<SharedProviderOpenAiCompatibilityFixture>.CreateAsync();
        var relayFixture = fixtureLease.Value;
        relayFixture.OpenHarness.Reset();
        using var chatResponse = await PostAsync(
            relayFixture.OpenHost.Client,
            SharedProviderRoutes.ChatCompletions,
            ChatJson(SharedProviderRelayTestData.ChatModelId));
        using var responsesResponse = await PostAsync(
            relayFixture.OpenHost.Client,
            SharedProviderRoutes.Responses,
            ResponsesJson(SharedProviderRelayTestData.ChatModelId));

        Assert.Equal(HttpStatusCode.OK, chatResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, responsesResponse.StatusCode);
        Assert.Equal(
            [
                SharedProviderRelayOperation.ChatCompletions,
                SharedProviderRelayOperation.Responses
            ],
            relayFixture.OpenHarness.Accepted
                .Select(request => request.Operation)
                .ToArray());
        var normalizedResponses = Assert.Single(
            relayFixture.OpenHarness.Accepted,
            request => request.Operation == SharedProviderRelayOperation.Responses);
        using (var document = JsonDocument.Parse(normalizedResponses.CanonicalPayloadUtf8))
        {
            Assert.Equal(JsonValueKind.False, document.RootElement.GetProperty("store").ValueKind);
        }

        await using var upstream = DirectRelayFixture.Create(
            baseUri: new Uri("https://deterministic-upstream.example.test/reverse/v1"));
        var chatResult = await upstream.DispatchAsync(
            CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys.OpenAi,
            SharedProviderPurpose.Chat,
            SharedProviderRelayOperation.ChatCompletions,
            ChatJson(upstream.ModelId));
        var responsesResult = await upstream.DispatchAsync(
            CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys.OpenAi,
            SharedProviderPurpose.Chat,
            SharedProviderRelayOperation.Responses,
            ResponsesJson(upstream.ModelId));

        Assert.IsType<SharedProviderRelayDispatchResult.Buffered>(chatResult);
        Assert.IsType<SharedProviderRelayDispatchResult.Buffered>(responsesResult);
        var requests = upstream.Handler.Requests.ToArray();
        Assert.Equal(2, requests.Length);
        Assert.Equal("/reverse/v1/chat/completions", requests[0].Uri.AbsolutePath);
        Assert.Equal("/reverse/v1/responses", requests[1].Uri.AbsolutePath);
        Assert.Contains("\"store\":false", requests[1].Body, StringComparison.Ordinal);
        Assert.All(requests, request =>
        {
            Assert.Equal("Bearer central-secret", request.Authorization);
            Assert.Contains(
                $"\"model\":\"{SharedProviderRelayTestData.UpstreamModel}\"",
                request.Body,
                StringComparison.Ordinal);
            Assert.DoesNotContain(upstream.ModelId.Value, request.Body, StringComparison.Ordinal);
        });

        relayFixture.PersistedDispatcher.Reset();
        using var storeFalseResponse = await PostAsync(
            relayFixture.PersistedHost.Client,
            SharedProviderRoutes.Responses,
            ResponsesJson(
                relayFixture.PersistedResponsesModelId,
                "\"store\":false"));
        Assert.Equal(HttpStatusCode.OK, storeFalseResponse.StatusCode);
        var storeFalseDispatch = Assert.Single(relayFixture.PersistedDispatcher.Requests);
        using (var document = JsonDocument.Parse(
            storeFalseDispatch.Request.CanonicalPayloadUtf8))
        {
            Assert.Equal(JsonValueKind.False, document.RootElement.GetProperty("store").ValueKind);
        }

        relayFixture.PersistedDispatcher.Reset();
        await AssertOperationMismatchAsync(relayFixture.PersistedResponsesModelId);
        await AssertOperationMismatchAsync(relayFixture.PersistedImageRoutingModelId);
        Assert.Empty(relayFixture.PersistedDispatcher.Requests);

        async Task AssertOperationMismatchAsync(SharedProviderRoutingModelId modelId)
        {
            using var response = await PostAsync(
                relayFixture.PersistedHost.Client,
                SharedProviderRoutes.ChatCompletions,
                ChatJson(modelId));
            var responseBody = await response.Content.ReadAsStringAsync();
            var envelope = JsonSerializer.Deserialize<SharedProviderOpenAiErrorEnvelope>(responseBody);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.NotNull(envelope);
            Assert.Equal(SharedProviderOpenAiConstants.ConflictErrorType, envelope.Error.Type);
            Assert.Equal("shared_provider_operation_mismatch", envelope.Error.Code);
            Assert.Null(envelope.Error.Param);
            Assert.Equal(
                "The published model does not support this operation.",
                envelope.Error.Message);
            Assert.DoesNotContain(modelId.Value, responseBody, StringComparison.Ordinal);
            Assert.DoesNotContain(
                SharedProviderOpenAiCompatibilityFixture.PersistedResponsesUpstreamModel,
                responseBody,
                StringComparison.Ordinal);
            Assert.DoesNotContain("persisted-upstream.example.test", responseBody, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Both_streaming_surfaces_are_incremental_terminal_and_cancel_upstream()
    {
        await using var fixtureLease =
            await FixtureLease<SharedProviderStreamingApiFixture>.CreateAsync();
        var streamingFixture = fixtureLease.Value;
        await AssertIncrementalStreamAsync(
            streamingFixture,
            SharedProviderRoutes.ChatCompletions,
            SharedProviderRelayOperation.ChatCompletions,
            new SharedProviderRelayStreamFrame(
                eventName: null,
                "{\"id\":\"chatcmpl-checkpoint\",\"choices\":[{\"delta\":{\"content\":\"first\"}}]}"));
        await AssertIncrementalStreamAsync(
            streamingFixture,
            SharedProviderRoutes.Responses,
            SharedProviderRelayOperation.Responses,
            new SharedProviderRelayStreamFrame(
                "response.output_text.delta",
                "{\"type\":\"response.output_text.delta\",\"delta\":\"first\"}"));

        var relayStream = new CancellationAwareRelayStream(
            new SharedProviderRelayStreamFrame(
                eventName: null,
                "{\"id\":\"chatcmpl-cancel-checkpoint\",\"choices\":[{\"delta\":{\"content\":\"first\"}}]}"));
        streamingFixture.Relay.ConfigureResult(
            new SharedProviderRelayDispatchResult.Streaming(relayStream));
        using var requestCancellation = new CancellationTokenSource();
        using var request = CreatePost(SharedProviderRoutes.ChatCompletions, "{}");
        HttpResponseMessage? response = null;
        Stream? body = null;
        try
        {
            response = await streamingFixture.Host.Client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                requestCancellation.Token);
            body = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(
                body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            Assert.StartsWith(
                "data: {\"id\":\"chatcmpl-cancel-checkpoint\"",
                await reader.ReadLineAsync().WaitAsync(TestTimeout),
                StringComparison.Ordinal);

            requestCancellation.Cancel();
            await body.DisposeAsync();
            body = null;
            response.Dispose();
            response = null;

            await relayStream.CancellationObserved.WaitAsync(TestTimeout);
            await relayStream.Disposed.WaitAsync(TestTimeout);
            var completion = await relayStream.Completion.WaitAsync(TestTimeout);
            Assert.Equal(SharedProviderFailureCategory.Cancelled, completion.Failure?.Category);
        }
        finally
        {
            requestCancellation.Cancel();
            relayStream.ForceCancel();
            if (body is not null)
            {
                await body.DisposeAsync();
            }

            response?.Dispose();
        }
    }

    [Fact]
    public async Task Function_tools_round_trip_and_structured_output_obeys_public_capabilities()
    {
        await using var fixtureLease =
            await FixtureLease<SharedProviderOpenAiCompatibilityFixture>.CreateAsync();
        var relayFixture = fixtureLease.Value;
        await using var upstream = DirectRelayFixture.Create();
        string toolsPayload = ChatJson(
            upstream.ModelId,
            "\"tools\":[{\"type\":\"function\",\"function\":{\"name\":\"weather\",\"parameters\":{\"type\":\"object\",\"properties\":{\"city\":{\"type\":\"string\"}},\"required\":[\"city\"]}}}],\"tool_choice\":{\"type\":\"function\",\"function\":{\"name\":\"weather\"}}");
        string structuredPayload = ChatJson(
            upstream.ModelId,
            "\"response_format\":{\"type\":\"json_schema\",\"json_schema\":{\"name\":\"answer\",\"schema\":{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"string\"}},\"required\":[\"value\"]},\"strict\":true}}");

        Assert.IsType<SharedProviderRelayDispatchResult.Buffered>(await upstream.DispatchAsync(
            CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys.OpenAi,
            SharedProviderPurpose.Chat,
            SharedProviderRelayOperation.ChatCompletions,
            toolsPayload));
        Assert.IsType<SharedProviderRelayDispatchResult.Buffered>(await upstream.DispatchAsync(
            CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys.OpenAi,
            SharedProviderPurpose.Chat,
            SharedProviderRelayOperation.ChatCompletions,
            structuredPayload));
        var upstreamBodies = upstream.Handler.Requests.Select(item => item.Body).ToArray();
        Assert.Contains("\"name\":\"weather\"", upstreamBodies[0], StringComparison.Ordinal);
        Assert.Contains("\"required\":[\"city\"]", upstreamBodies[0], StringComparison.Ordinal);
        Assert.Contains("\"json_schema\"", upstreamBodies[1], StringComparison.Ordinal);

        await using (var scope = relayFixture.PersistedHost.App.Services.CreateAsyncScope())
        {
            var providerAdministration = scope.ServiceProvider.GetRequiredService<CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderAdministrationService>();
            var editor = await providerAdministration.GetProviderAsync(relayFixture.PersistedChatProfileId);
            Assert.True(editor.SupportsStructuredOutput);

            editor.SupportsStructuredOutput = false;
            var saveResult = await providerAdministration.SaveProviderAsync(editor);
            Assert.True(saveResult.IsSuccess);
            Assert.Equal(relayFixture.PersistedChatProfileId, saveResult.Value);

            var reloaded = await providerAdministration.GetProviderAsync(relayFixture.PersistedChatProfileId);
            Assert.False(reloaded.SupportsStructuredOutput);
        }

        using var catalogResponse = await relayFixture.PersistedHost.Client.GetAsync(
            SharedProviderRoutes.Catalog);
        Assert.Equal(HttpStatusCode.OK, catalogResponse.StatusCode);
        var catalog = SharedProviderProtocolJson.DeserializeCatalog(
            await catalogResponse.Content.ReadAsStringAsync());
        var persistedPublication = Assert.Single(
            catalog.Providers,
            item => item.PublicationId == relayFixture.PersistedChatPublicationId);
        var persistedModel = Assert.Single(
            persistedPublication.Models,
            item => item.Id == relayFixture.PersistedChatModelId);
        Assert.DoesNotContain(
            SharedProviderCapability.StructuredOutput,
            persistedModel.Capabilities);

        relayFixture.PersistedDispatcher.Reset();
        using var persistedRejected = await PostAsync(
            relayFixture.PersistedHost.Client,
            SharedProviderRoutes.ChatCompletions,
            ChatJson(
                relayFixture.PersistedChatModelId,
                "\"response_format\":{\"type\":\"json_object\"}"));
        Assert.Equal(HttpStatusCode.BadRequest, persistedRejected.StatusCode);
        Assert.Empty(relayFixture.PersistedDispatcher.Requests);

        relayFixture.OpenHarness.Reset();
        using var rejected = await PostAsync(
            relayFixture.OpenHost.Client,
            SharedProviderRoutes.ChatCompletions,
            ChatJson(
                SharedProviderRelayTestData.LimitedChatModelId,
                "\"response_format\":{\"type\":\"json_object\"}"));

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(0, relayFixture.OpenHarness.DispatchCount);
    }

    [Fact]
    public async Task Openai_and_comfyui_image_adapters_return_supported_formats()
    {
        await using var openAi = DirectRelayFixture.Create();
        var openAiResult = await openAi.DispatchAsync(
            CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys.OpenAi,
            SharedProviderPurpose.ImageGeneration,
            SharedProviderRelayOperation.ImageGenerations,
            ImagesJson(openAi.ModelId));
        string openAiBody = ReadBufferedBody(openAiResult);
        Assert.Equal("/v1/images/generations", Assert.Single(openAi.Handler.Requests).Uri.AbsolutePath);
        Assert.Equal("AQID", JsonDocument.Parse(openAiBody)
            .RootElement.GetProperty("data")[0].GetProperty("b64_json").GetString());

        var imageRelay = new RecordingImageCapabilityRelay(
            [new SharedProviderGeneratedImage(
                "image/png",
                new byte[] { 1, 2, 3, 4 },
                "safe prompt")]);
        await using var comfyUi = DirectRelayFixture.Create(imageRelay: imageRelay);
        var comfyResult = await comfyUi.DispatchAsync(
            CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys.ComfyUi,
            SharedProviderPurpose.ImageGeneration,
            SharedProviderRelayOperation.ImageGenerations,
            ImagesJson(comfyUi.ModelId));
        string comfyBody = ReadBufferedBody(comfyResult);

        Assert.Equal(Convert.ToBase64String([1, 2, 3, 4]), JsonDocument.Parse(comfyBody)
            .RootElement.GetProperty("data")[0].GetProperty("b64_json").GetString());
        Assert.DoesNotContain("file:", comfyBody, StringComparison.OrdinalIgnoreCase);
        Assert.Single(imageRelay.Requests);
        Assert.Empty(comfyUi.Handler.Requests);
    }

    [Fact]
    public async Task Two_clients_sync_idempotently_preserve_ids_and_coexist_with_personal_profiles()
    {
        await using var fixtureLease =
            await FixtureLease<SharedProviderHybridSelectionFixture>.CreateAsync();
        var hybridFixture = fixtureLease.Value;
        var clientB = await hybridFixture.GetSecondaryAsync();
        var sourceInstanceId = new SharedProviderSourceInstanceId(
            Guid.Parse("41d4ebec-d0ea-4cd9-886e-c547e0d0d383"));
        var publicationId = new SharedProviderPublicationId(
            Guid.Parse("f5f4f2a4-74b8-4596-a846-919a19ac0a78"));
        var catalog = CreateCatalog(publicationId, sourceInstanceId);
        var clientASeed = await SeedSharedAsync(hybridFixture.Primary, catalog, "Client A shared");
        var clientBSeed = await SeedSharedAsync(clientB, catalog, "Client B shared");
        var personalA = await SeedPersonalAsync(
            hybridFixture.Primary,
            "Client A personal",
            "personal-a-model");
        var personalB = await SeedPersonalAsync(
            clientB,
            "Client B personal",
            "personal-b-model");

        var repeatedA = await ReconcileAsync(
            hybridFixture.Primary,
            clientASeed.SourceId,
            catalog,
            Selection(publicationId));
        var repeatedB = await ReconcileAsync(
            clientB,
            clientBSeed.SourceId,
            catalog,
            Selection(publicationId));
        var stateA = await LoadSharedIdentityAsync(
            hybridFixture.Primary,
            clientASeed.SourceId,
            publicationId);
        var stateB = await LoadSharedIdentityAsync(
            clientB,
            clientBSeed.SourceId,
            publicationId);
        var providersA = await LoadProvidersAsync(
            hybridFixture.Primary,
            clientASeed.ProviderProfileId,
            personalA);
        var providersB = await LoadProvidersAsync(
            clientB,
            clientBSeed.ProviderProfileId,
            personalB);

        Assert.Equal(SharedProviderReconciliationOutcome.Applied, repeatedA.Outcome);
        Assert.Equal(SharedProviderReconciliationOutcome.Applied, repeatedB.Outcome);
        Assert.Empty(repeatedA.AffectedProviderProfileIds);
        Assert.Empty(repeatedB.AffectedProviderProfileIds);
        Assert.Equal(clientASeed.ImportId, stateA.ImportId);
        Assert.Equal(clientASeed.ProviderProfileId, stateA.ProviderProfileId);
        Assert.Equal(clientBSeed.ImportId, stateB.ImportId);
        Assert.Equal(clientBSeed.ProviderProfileId, stateB.ProviderProfileId);
        Assert.NotEqual(stateA.ImportId, stateB.ImportId);
        Assert.NotEqual(stateA.ProviderProfileId, stateB.ProviderProfileId);
        Assert.Equal(2, providersA.Count);
        Assert.Equal(2, providersB.Count);
        Assert.Contains(providersA, item =>
            item.Id == clientASeed.ProviderProfileId &&
            item.ConnectorPluginKey == SharedProviderReconciliationCoordinator.ImportedConnectorPluginKey);
        Assert.Contains(providersA, item =>
            item.Id == personalA && item.ConnectorPluginKey == CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys.OpenAi);
        Assert.Contains(providersB, item =>
            item.Id == clientBSeed.ProviderProfileId &&
            item.ConnectorPluginKey == SharedProviderReconciliationCoordinator.ImportedConnectorPluginKey);
        Assert.Contains(providersB, item =>
            item.Id == personalB && item.ConnectorPluginKey == CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys.OpenAi);
    }

    [Fact]
    public async Task Unpublish_outage_recovery_and_source_identity_mismatch_are_non_destructive_without_fallback()
    {
        await using var fixtureLease =
            await FixtureLease<SharedProviderHybridSelectionFixture>.CreateAsync();
        var hybridFixture = fixtureLease.Value;
        var initialCatalog = CreateCatalog();
        var shared = await SeedSharedAsync(
            hybridFixture.Primary,
            initialCatalog,
            "Checkpoint shared provider");
        var personalProviderId = await SeedPersonalAsync(
            hybridFixture.Primary,
            "Checkpoint personal provider",
            "personal-fallback-must-not-run");
        await SetDefaultProviderAsync(hybridFixture.Primary, personalProviderId);

        await MarkUnpublishedAsync(hybridFixture.Primary, shared.ImportId);
        var afterUnpublish = await LoadSharedIdentityAsync(
            hybridFixture.Primary,
            shared.SourceId,
            shared.PublicationId);

        Assert.Equal(SharedProviderAvailabilityState.Unpublished, afterUnpublish.AvailabilityState);
        Assert.Equal(shared.ImportId, afterUnpublish.ImportId);
        Assert.Equal(shared.ProviderProfileId, afterUnpublish.ProviderProfileId);

        var recovered = await ReconcileAsync(
            hybridFixture.Primary,
            shared.SourceId,
            initialCatalog,
            Selection(shared.PublicationId));
        var afterRecovery = await LoadSharedIdentityAsync(
            hybridFixture.Primary,
            shared.SourceId,
            shared.PublicationId);
        Assert.Equal(SharedProviderReconciliationOutcome.Applied, recovered.Outcome);
        Assert.Equal(SharedProviderAvailabilityState.Available, afterRecovery.AvailabilityState);
        Assert.Equal(afterUnpublish.ImportId, afterRecovery.ImportId);
        Assert.Equal(afterUnpublish.ProviderProfileId, afterRecovery.ProviderProfileId);

        await MarkSourceUnavailableAsync(
            hybridFixture.Primary,
            shared,
            SharedProviderSourceStatus.SourceOffline,
            SharedProviderAvailabilityState.SourceOffline);
        var outageFailure = await Assert.ThrowsAsync<ProviderRuntimeProfileUnavailableException>(() =>
            PrepareAsync(
                hybridFixture.Primary,
                shared.ProviderProfileId,
                shared.DefaultModelId.Value));
        Assert.Equal(shared.ProviderProfileId, outageFailure.ProviderId);
        Assert.NotEqual(personalProviderId, outageFailure.ProviderId);
        Assert.Equal(personalProviderId, await LoadDefaultProviderAsync(hybridFixture.Primary));

        var unavailableWorkspaceService = DispatchProxy.Create<
            IAgentFrameworkWorkspaceService,
            UnavailableProviderWorkspaceServiceProxy>();
        ((UnavailableProviderWorkspaceServiceProxy)(object)unavailableWorkspaceService).ProviderId =
            shared.ProviderProfileId;
        await using (var apiHost = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            configureServices: services =>
            {
                services.RemoveAll<IAgentFrameworkWorkspaceService>();
                services.AddSingleton(unavailableWorkspaceService);
            },
            useInMemoryDatabase: true))
        {
            using var unavailableResponse = await apiHost.Client.PostAsJsonAsync(
                $"/api/agents/providers/{shared.ProviderProfileId:D}/test-chat",
                new ProviderTestChatRequest(
                    string.Empty,
                    string.Empty,
                    [],
                    "Prove the fail-closed API boundary."));
            string unavailableBody = await unavailableResponse.Content.ReadAsStringAsync();
            using var unavailablePayload = JsonDocument.Parse(unavailableBody);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailableResponse.StatusCode);
            var unavailableErrors = unavailablePayload.RootElement.GetProperty("errors");
            Assert.Equal(1, unavailableErrors.GetArrayLength());
            var unavailableError = unavailableErrors[0];
            Assert.Equal(
                LlmChatErrorCodes.ProviderUnavailable,
                unavailableError.GetProperty("code").GetString());
            Assert.Equal(
                "The provider runtime profile is unavailable.",
                unavailableError.GetProperty("message").GetString());
            Assert.DoesNotContain(
                shared.ProviderProfileId.ToString("D"),
                unavailableBody,
                StringComparison.OrdinalIgnoreCase);
        }

        var outageRecovery = await ReconcileAsync(
            hybridFixture.Primary,
            shared.SourceId,
            initialCatalog,
            Selection(shared.PublicationId));
        var afterOutageRecovery = await LoadSharedIdentityAsync(
            hybridFixture.Primary,
            shared.SourceId,
            shared.PublicationId);
        var preparedAfterRecovery = await PrepareAsync(
            hybridFixture.Primary,
            shared.ProviderProfileId,
            shared.DefaultModelId.Value);
        Assert.Equal(SharedProviderReconciliationOutcome.Applied, outageRecovery.Outcome);
        Assert.Equal(
            SharedProviderAvailabilityState.Available,
            afterOutageRecovery.AvailabilityState);
        Assert.Equal(shared.ImportId, afterOutageRecovery.ImportId);
        Assert.Equal(shared.ProviderProfileId, preparedAfterRecovery.Blueprint.Provider.Id);

        await MarkIdentityMismatchAsync(hybridFixture.Primary, shared);
        var afterMismatch = await LoadSharedIdentityAsync(
            hybridFixture.Primary,
            shared.SourceId,
            shared.PublicationId);
        Assert.Equal(
            SharedProviderAvailabilityState.SourceIdentityMismatch,
            afterMismatch.AvailabilityState);
        Assert.Equal(afterRecovery.ImportId, afterMismatch.ImportId);
        Assert.Equal(afterRecovery.ProviderProfileId, afterMismatch.ProviderProfileId);
        Assert.Equal(initialCatalog.SourceInstanceId, afterMismatch.RemoteSourceInstanceId);
        Assert.Equal(shared.DefaultModelId, afterMismatch.DefaultModelId);
    }

    [Fact]
    public async Task Access_context_is_validated_audited_not_forwarded_and_audit_is_content_free()
    {
        await using var fixtureLease =
            await FixtureLease<SharedProviderOpenAiCompatibilityFixture>.CreateAsync();
        var relayFixture = fixtureLease.Value;
        relayFixture.SecureHarness.Reset();
        const string subject = "checkpoint-relay-subject";
        const string accessContext = "Tenant_42~Session:checkpoint";
        const string promptSentinel = "private-prompt-sentinel-checkpoint";
        var token = IssueToken(
            relayFixture.SecureHost,
            ApiAccessScopeNames.InvokeSharedProviders,
            subject);
        using var malformedRequest = CreatePost(
            SharedProviderRoutes.ChatCompletions,
            ChatJson(SharedProviderRelayTestData.ChatModelId));
        malformedRequest.Headers.Authorization = token;
        malformedRequest.Headers.TryAddWithoutValidation(
            SharedProviderHeaders.AccessContextReference,
            "invalid/context");
        using var malformedResponse = await relayFixture.SecureHost.Client.SendAsync(malformedRequest);
        Assert.Equal(HttpStatusCode.BadRequest, malformedResponse.StatusCode);

        using var request = CreatePost(
            SharedProviderRoutes.ChatCompletions,
            ChatJsonWithContent(
                SharedProviderRelayTestData.ChatModelId,
                promptSentinel));
        request.Headers.Authorization = token;
        request.Headers.TryAddWithoutValidation(
            SharedProviderHeaders.AccessContextReference,
            accessContext);
        request.Headers.TryAddWithoutValidation("Cookie", "private-cookie=value");
        using var response = await relayFixture.SecureHost.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var captured = Assert.Single(relayFixture.SecureHarness.Requests);
        Assert.Equal(subject, captured.Context.AuthenticatedSubject);
        Assert.Equal(accessContext, captured.Context.AccessContextReference?.Value);
        string normalized = Encoding.UTF8.GetString(
            Assert.Single(relayFixture.SecureHarness.Accepted).CanonicalPayloadUtf8.Span);
        Assert.DoesNotContain(subject, normalized, StringComparison.Ordinal);
        Assert.DoesNotContain(accessContext, normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("private-cookie", normalized, StringComparison.Ordinal);

        relayFixture.PersistedDispatcher.Reset();
        using var auditedRequest = CreatePost(
            SharedProviderRoutes.ChatCompletions,
            ChatJsonWithContent(
                relayFixture.PersistedChatModelId,
                promptSentinel));
        auditedRequest.Headers.TryAddWithoutValidation(
            SharedProviderHeaders.AccessContextReference,
            accessContext);
        using var auditedResponse = await relayFixture.PersistedHost.Client.SendAsync(auditedRequest);
        string requestId = Assert.Single(
            auditedResponse.Headers.GetValues(SharedProviderHeaders.RequestId));
        Assert.Equal(HttpStatusCode.OK, auditedResponse.StatusCode);
        await using (var scope = relayFixture.PersistedHost.App.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var dbContext = await factory.CreateDbContextAsync();
            var audit = await dbContext.Set<SharedProviderInvocationRecord>()
                .AsNoTracking()
                .SingleAsync(item => item.RequestId == requestId);
            Assert.Equal(accessContext, audit.AccessContextReference?.Value);
            Assert.Equal(SharedProviderInvocationOutcome.Succeeded, audit.Outcome);
            string auditJson = JsonSerializer.Serialize(audit);
            Assert.DoesNotContain(promptSentinel, auditJson, StringComparison.Ordinal);
            Assert.DoesNotContain(
                typeof(SharedProviderInvocationRecord).GetProperties(),
                property => property.Name.Contains("Body", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Contains("Payload", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Contains("Prompt", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Contains("ResponseContent", StringComparison.OrdinalIgnoreCase));
        }

        AssertSigningKeyFileConfigurationIsSafe();
    }

    private async Task AssertIncrementalStreamAsync(
        SharedProviderStreamingApiFixture streamingFixture,
        string route,
        SharedProviderRelayOperation expectedOperation,
        SharedProviderRelayStreamFrame firstFrame)
    {
        var relayStream = new GatedRelayStream(
            firstFrame,
            [new SharedProviderRelayStreamFrame(eventName: null, "[DONE]")]);
        streamingFixture.Relay.ConfigureResult(
            new SharedProviderRelayDispatchResult.Streaming(relayStream));
        using var request = CreatePost(route, "{}");
        using var response = await streamingFixture.Host.Client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead);
        await using var body = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(
            body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        try
        {
            if (firstFrame.EventName is { } eventName)
            {
                Assert.Equal(
                    $"event: {eventName}",
                    await reader.ReadLineAsync().WaitAsync(TestTimeout));
            }

            Assert.Equal(
                $"data: {firstFrame.Data}",
                await reader.ReadLineAsync().WaitAsync(TestTimeout));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(relayStream.Completion.IsCompleted);
            Assert.Equal(expectedOperation, Assert.Single(streamingFixture.Relay.Requests).Operation);

            relayStream.Release();
            string remainder = await reader.ReadToEndAsync().WaitAsync(TestTimeout);
            Assert.Contains("data: [DONE]", remainder, StringComparison.Ordinal);
            Assert.Null((await relayStream.Completion.WaitAsync(TestTimeout)).Failure);
            await relayStream.Disposed.WaitAsync(TestTimeout);
        }
        finally
        {
            relayStream.Release();
        }
    }

    private static AuthenticationHeaderValue IssueToken(
        ApiTestHost host,
        string scope,
        string subject)
    {
        var token = host.App.Services.GetRequiredService<IApiTokenService>()
            .IssueToken(new ApiTokenIssueRequest
            {
                Subject = subject,
                DisplayName = "Shared-provider backend checkpoint client",
                Scopes = [scope]
            });
        return new AuthenticationHeaderValue(token.TokenType, token.Token);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string route,
        AuthenticationHeaderValue authorization,
        string? payload = null)
    {
        using var request = new HttpRequestMessage(method, route)
        {
            Content = payload is null
                ? null
                : new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = authorization;
        return await client.SendAsync(request);
    }

    private static HttpRequestMessage CreatePost(string route, string payload)
        => new(HttpMethod.Post, route)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string route,
        string payload)
    {
        using var request = CreatePost(route, payload);
        return await client.SendAsync(request);
    }

    private static string ChatJson(
        SharedProviderRoutingModelId model,
        string? extra = null)
        => extra is null
            ? $$"""{"model":"{{model.Value}}","messages":[{"role":"user","content":"hello"}]}"""
            : $$"""{"model":"{{model.Value}}","messages":[{"role":"user","content":"hello"}],{{extra}}}""";

    private static string ResponsesJson(
        SharedProviderRoutingModelId model,
        string? extra = null)
        => extra is null
            ? $$"""{"model":"{{model.Value}}","input":"hello","instructions":"be concise"}"""
            : $$"""{"model":"{{model.Value}}","input":"hello","instructions":"be concise",{{extra}}}""";

    private static string ChatJsonWithContent(
        SharedProviderRoutingModelId model,
        string content)
        => JsonSerializer.Serialize(new
        {
            model = model.Value,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content
                }
            }
        });

    private static string ImagesJson(SharedProviderRoutingModelId model)
        => $$"""{"model":"{{model.Value}}","prompt":"a blue square","n":1,"size":"1024x1024","response_format":"b64_json","output_format":"png"}""";

    private static string ReadBufferedBody(SharedProviderRelayDispatchResult result)
        => Encoding.UTF8.GetString(
            Assert.IsType<SharedProviderRelayDispatchResult.Buffered>(result).PayloadUtf8.Span);

    private static ProviderSharePublication CreatePublication(
        Guid profileId,
        bool isPublished)
    {
        var publication = SharedProviderPublicationTransitions.Create(
            profileId,
            new SharedProviderPublicationId(Guid.NewGuid()),
            Timestamp);
        if (isPublished)
        {
            SharedProviderPublicationTransitions.Publish(publication, Timestamp);
        }

        return publication;
    }

    private static WorkspaceProviderProfile CreateWorkspaceProfile(string name)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            ProviderKind = WorkspaceProviderKind.OpenAi,
            ConnectorPluginKey = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys.OpenAi,
            ConfigSchemaVersion = "1.0",
            BaseUrl = "https://private-upstream.example.test/v1",
            ApiKeySecretId = Guid.NewGuid(),
            DefaultModel = "private-upstream-model",
            TimeoutSeconds = 30,
            IsEnabled = true,
            SupportsStreaming = true,
            SupportsToolCalling = true,
            SupportsStructuredOutput = true,
            SupportsVision = false,
            ExtraSettingsJson = "{\"private\":\"operator-only\"}"
        };

    private static SharedProviderPublicationEligibility EligibleChat(string upstreamModel)
        => new(
            SharedProviderPublicationEligibilityCode.Eligible,
            "The provider profile is eligible for publication.",
            SharedProviderPurpose.Chat,
            SharedProviderTransport.OpenAiCompatible,
            [
                new SharedProviderEligibleModel(
                    upstreamModel,
                    [
                        SharedProviderCapability.ChatCompletions,
                        SharedProviderCapability.Responses,
                        SharedProviderCapability.Streaming
                    ])
            ]);

    private static async Task<SharedSeed> SeedSharedAsync(
        TestApplication application,
        SharedProviderCatalogDocument? catalog = null,
        string? alias = null)
    {
        catalog ??= CreateCatalog();
        var publication = Assert.Single(catalog.Providers);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var now = DateTimeOffset.UtcNow;
        var secret = new SecretRecord
        {
            Name = $"Shared checkpoint source token {Guid.NewGuid():N}",
            Kind = SecretKind.Token,
            EncryptedPayload = "vault-reference:checkpoint-shared",
            Scope = "workspace",
            MetadataJson = "{}",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var source = SharedProviderSourceTransitions.Create(
            $"Shared checkpoint source {Guid.NewGuid():N}",
            "https://central.shared.example.test/tenant/client/",
            secret.Id,
            allowInsecurePrivateNetwork: false,
            isEnabled: true,
            timestampUtc: now);
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Add(secret);
            dbContext.Add(source);
            await dbContext.SaveChangesAsync();
        }

        var reconciliation = await scope.ServiceProvider
            .GetRequiredService<SharedProviderReconciliationCoordinator>()
            .ReconcileAsync(new SharedProviderReconciliationRequest(
                source.Id,
                catalog,
                SharedProviderCatalogEntityTag.FromRevision(catalog.CatalogRevision),
                Selection(publication.PublicationId),
                SharedProviderSelectionMode.Replace));
        Assert.Equal(SharedProviderReconciliationOutcome.Applied, reconciliation.Outcome);

        await using var editContext = await dbContextFactory.CreateDbContextAsync();
        var import = await editContext.Set<SharedProviderImport>()
            .SingleAsync(item =>
                item.SourceId == source.Id &&
                item.RemotePublicationId == publication.PublicationId);
        var profile = await editContext.Set<WorkspaceProviderProfile>()
            .SingleAsync(item => item.Id == import.ProviderProfileId);
        profile.Name = alias ?? profile.Name;
        await editContext.SaveChangesAsync();

        return new SharedSeed(
            source.Id,
            import.Id,
            profile.Id,
            publication.PublicationId,
            publication.DefaultModelId,
            catalog);
    }

    private static async Task<Guid> SeedPersonalAsync(
        TestApplication application,
        string name,
        string defaultModel)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var now = DateTimeOffset.UtcNow;
        var secret = new SecretRecord
        {
            Name = $"Personal checkpoint token {Guid.NewGuid():N}",
            Kind = SecretKind.ApiKey,
            EncryptedPayload = "vault-reference:checkpoint-personal",
            Scope = "workspace",
            MetadataJson = "{}",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Add(secret);
            await dbContext.SaveChangesAsync();
        }

        return await scope.ServiceProvider
            .GetRequiredService<IProviderProfileRegistry>()
            .SaveProviderAsync(new AgentFrameworkProviderProfileEditorModel
            {
                Name = name,
                Kind = AgentFrameworkProviderKind.OpenAi,
                BaseUrl = "https://personal.example.test/v1",
                ApiKeyEnvironmentVariable = $"secret:{secret.Id:D}",
                DefaultModel = defaultModel,
                Transport = ProviderTransportKind.Responses,
                Purpose = ProviderProfilePurpose.Chat,
                IsEnabled = true,
                SupportsStreaming = true,
                SupportsTools = true,
                PreferFrameworkManagedChatHistory = true,
                SupportsBackgroundResponses = false,
                ConfigurationJson = "{}"
            });
    }

    private static async Task<SharedProviderReconciliationResult> ReconcileAsync(
        TestApplication application,
        Guid sourceId,
        SharedProviderCatalogDocument catalog,
        IReadOnlySet<SharedProviderPublicationId> selectedPublicationIds)
    {
        await using var scope = application.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<SharedProviderReconciliationCoordinator>()
            .ReconcileAsync(new SharedProviderReconciliationRequest(
                sourceId,
                catalog,
                SharedProviderCatalogEntityTag.FromRevision(catalog.CatalogRevision),
                selectedPublicationIds,
                SharedProviderSelectionMode.AddOrReactivate));
    }

    private static async Task<PersistedSharedIdentity> LoadSharedIdentityAsync(
        TestApplication application,
        Guid sourceId,
        SharedProviderPublicationId publicationId)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        var source = await dbContext.Set<SharedProviderSource>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == sourceId);
        var import = await dbContext.Set<SharedProviderImport>()
            .AsNoTracking()
            .SingleAsync(item =>
                item.SourceId == sourceId &&
                item.RemotePublicationId == publicationId);
        return new PersistedSharedIdentity(
            import.Id,
            import.ProviderProfileId,
            import.AvailabilityState,
            import.RemoteDefaultModelId,
            source.RemoteInstanceId);
    }

    private static async Task<IReadOnlyList<CanDoItAll.AgentFramework.Models.ProviderProfile>>
        LoadProvidersAsync(
            TestApplication application,
            params Guid[] providerIds)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var providers = await scope.ServiceProvider
            .GetRequiredService<IProviderProfileRegistry>()
            .ListProvidersAsync();
        var selectedIds = providerIds.ToHashSet();
        return providers.Where(item => selectedIds.Contains(item.Id)).ToArray();
    }

    private static async Task SetDefaultProviderAsync(
        TestApplication application,
        Guid providerId)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        var settings = await dbContext.Set<WorkspaceSettings>().FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new WorkspaceSettings();
            dbContext.Add(settings);
        }

        settings.DefaultProviderProfileId = providerId;
        settings.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid?> LoadDefaultProviderAsync(TestApplication application)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        return await dbContext.Set<WorkspaceSettings>()
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => item.DefaultProviderProfileId)
            .FirstOrDefaultAsync();
    }

    private static async Task MarkSourceUnavailableAsync(
        TestApplication application,
        SharedSeed shared,
        SharedProviderSourceStatus sourceStatus,
        SharedProviderAvailabilityState availabilityState)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        var source = await dbContext.Set<SharedProviderSource>()
            .SingleAsync(item => item.Id == shared.SourceId);
        var import = await dbContext.Set<SharedProviderImport>()
            .SingleAsync(item => item.Id == shared.ImportId);
        var latest = source.UpdatedAtUtc >= import.UpdatedAtUtc
            ? source.UpdatedAtUtc
            : import.UpdatedAtUtc;
        SharedProviderSourceTransitions.ApplyFailure(
            source,
            sourceStatus,
            statusCode: 503,
            sanitizedMessage: "The shared source is temporarily unavailable.",
            timestampUtc: latest.AddTicks(1));
        SharedProviderImportTransitions.MarkTransientlyUnavailable(
            import,
            availabilityState,
            latest.AddTicks(1));
        await dbContext.SaveChangesAsync();
    }

    private static async Task MarkUnpublishedAsync(
        TestApplication application,
        Guid importId)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        var import = await dbContext.Set<SharedProviderImport>()
            .SingleAsync(item => item.Id == importId);
        SharedProviderImportTransitions.MarkAuthoritativelyAbsent(
            import,
            SharedProviderAvailabilityState.Unpublished,
            import.UpdatedAtUtc.AddTicks(1));
        await dbContext.SaveChangesAsync();
    }

    private static async Task MarkIdentityMismatchAsync(
        TestApplication application,
        SharedSeed shared)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        var source = await dbContext.Set<SharedProviderSource>()
            .SingleAsync(item => item.Id == shared.SourceId);
        var import = await dbContext.Set<SharedProviderImport>()
            .SingleAsync(item => item.Id == shared.ImportId);
        var transitionAt = (source.UpdatedAtUtc >= import.UpdatedAtUtc
            ? source.UpdatedAtUtc
            : import.UpdatedAtUtc).AddTicks(1);
        var acceptance = SharedProviderSourceTransitions.ApplySuccessfulCatalog(
            source,
            new SharedProviderSourceInstanceId(Guid.NewGuid()),
            SharedProviderCatalogEntityTag.FromRevision(shared.Catalog.CatalogRevision),
            transitionAt);
        Assert.Equal(
            SharedProviderCatalogIdentityAcceptance.IdentityMismatch,
            acceptance);
        SharedProviderImportTransitions.MarkTransientlyUnavailable(
            import,
            SharedProviderAvailabilityState.SourceIdentityMismatch,
            transitionAt);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<AgentExecutionPreparationSnapshot> PrepareAsync(
        TestApplication application,
        Guid providerId,
        string model)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceCatalogStore>();
        var agent = CreateAgent(providerId, model);
        await store.UpdateCatalogAsync(catalog => catalog with
        {
            Agents = catalog.Agents.Append(agent).ToArray()
        });
        await scope.ServiceProvider
            .GetRequiredService<IProviderRuntimeProfileSnapshotInitializer>()
            .InitializeAsync();
        var preparation = new AgentExecutionPreparationService(
            store,
            scope.ServiceProvider.GetRequiredService<IProviderRuntimeProfileSnapshotSource>(),
            scope.ServiceProvider.GetRequiredService<IAgentExecutionPreparationCache>(),
            scope.ServiceProvider.GetRequiredService<IAgentExecutionProfileGenerationSource>(),
            AgentExecutionActivityWorkspaceIdentity.CreateHostLifetime(
                WorkspaceScopeDescriptor.Organization("shared-provider-backend-checkpoint")));
        return await preparation.AcquireForAtomicConsumerAsync(agent.Id);
    }

    private static AgentDefinition CreateAgent(Guid providerId, string model)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            $"Checkpoint agent {Guid.NewGuid():N}",
            "Shared-provider checkpoint",
            "Validates exact provider selection.",
            "Use only the explicitly selected provider.",
            AgentLifecycleStatus.Active,
            providerId,
            model,
            AgentWorkloadKind.Programming,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private static SharedProviderCatalogDocument CreateCatalog(
        SharedProviderPublicationId? publicationId = null,
        SharedProviderSourceInstanceId? sourceInstanceId = null)
    {
        var resolvedPublicationId = publicationId ??
            new SharedProviderPublicationId(Guid.NewGuid());
        var defaultModelId = SharedProviderRoutingModelIdCodec.Create(
            resolvedPublicationId,
            "shared-upstream-model");
        var draftPublication = new SharedProviderCatalogPublication(
            resolvedPublicationId,
            PlaceholderRevision(),
            "Shared provider display",
            SharedProviderPurpose.Chat,
            SharedProviderTransport.OpenAiCompatible,
            defaultModelId,
            [
                new SharedProviderCatalogModel(
                    defaultModelId,
                    "Shared model display",
                    [
                        SharedProviderCapability.Responses,
                        SharedProviderCapability.Streaming,
                        SharedProviderCapability.FunctionTools,
                        SharedProviderCapability.StructuredOutput
                    ])
            ],
            new SharedProviderCatalogHealth(SharedProviderHealthState.Available));
        var publication = draftPublication with
        {
            Revision = SharedProviderCanonicalRevision.ComputePublication(draftPublication)
        };
        return CreateCatalogDocument(
            sourceInstanceId ?? new SharedProviderSourceInstanceId(Guid.NewGuid()),
            [publication]);
    }

    private static SharedProviderCatalogDocument CreateCatalogDocument(
        SharedProviderSourceInstanceId sourceInstanceId,
        IReadOnlyList<SharedProviderCatalogPublication> publications)
    {
        var draft = new SharedProviderCatalogDocument(
            SharedProviderProtocolVersion.Current,
            sourceInstanceId,
            PlaceholderRevision(),
            new SharedProviderProtocolDescriptor(SharedProviderRoutes.OpenAiBase),
            publications);
        return draft with
        {
            CatalogRevision = SharedProviderCanonicalRevision.ComputeCatalog(draft)
        };
    }

    private static SharedProviderPublicRevision PlaceholderRevision()
        => new(
            $"{SharedProviderPublicRevision.Prefix}{new string('0', SharedProviderPublicRevision.HashLength)}");

    private static IReadOnlySet<SharedProviderPublicationId> Selection(
        params SharedProviderPublicationId[] publicationIds)
        => publicationIds.ToHashSet();

    private static void AssertSigningKeyFileConfigurationIsSafe()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"candoitall-signing-key-checkpoint-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        const string secret = "checkpoint-signing-key-secret-32-bytes-minimum";
        try
        {
            string keyPath = Path.Combine(directory, "api-signing-key");
            File.WriteAllText(keyPath, secret + Environment.NewLine, new UTF8Encoding(false));
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Api:Authorization:SigningKeyFile"] = "api-signing-key"
                })
                .Build();

            ApiAuthorizationSigningKeyFileConfiguration.Apply(configuration, directory);
            Assert.Equal(secret, configuration["Api:Authorization:SigningKey"]);

            var conflict = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Api:Authorization:SigningKeyFile"] = keyPath,
                    ["Api:Authorization:SigningKey"] = "conflicting-inline-secret"
                })
                .Build();
            var conflictFailure = Assert.Throws<InvalidOperationException>(() =>
                ApiAuthorizationSigningKeyFileConfiguration.Apply(conflict, directory));
            Assert.DoesNotContain(secret, conflictFailure.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(
                "conflicting-inline-secret",
                conflictFailure.ToString(),
                StringComparison.Ordinal);

            var missing = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Api:Authorization:SigningKeyFile"] = "missing-signing-key"
                })
                .Build();
            var missingFailure = Assert.Throws<FileNotFoundException>(() =>
                ApiAuthorizationSigningKeyFileConfiguration.Apply(missing, directory));
            Assert.DoesNotContain(secret, missingFailure.ToString(), StringComparison.Ordinal);

            string oversizedPath = Path.Combine(directory, "oversized-signing-key");
            File.WriteAllText(
                oversizedPath,
                secret + new string('x', 4096),
                new UTF8Encoding(false));
            var oversized = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Api:Authorization:SigningKeyFile"] = oversizedPath
                })
                .Build();
            var oversizedFailure = Assert.Throws<InvalidOperationException>(() =>
                ApiAuthorizationSigningKeyFileConfiguration.Apply(oversized, directory));
            Assert.DoesNotContain(secret, oversizedFailure.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed record SharedSeed(
        Guid SourceId,
        Guid ImportId,
        Guid ProviderProfileId,
        SharedProviderPublicationId PublicationId,
        SharedProviderRoutingModelId DefaultModelId,
        SharedProviderCatalogDocument Catalog);

    private sealed record PersistedSharedIdentity(
        Guid ImportId,
        Guid ProviderProfileId,
        SharedProviderAvailabilityState AvailabilityState,
        SharedProviderRoutingModelId DefaultModelId,
        SharedProviderSourceInstanceId? RemoteSourceInstanceId);

    private class UnavailableProviderWorkspaceServiceProxy : DispatchProxy
    {
        public Guid ProviderId { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name !=
                nameof(IAgentFrameworkWorkspaceService.RunProviderTestChatAsync))
            {
                throw new InvalidOperationException(
                    $"Unexpected workspace service member '{targetMethod?.Name}'.");
            }

            Assert.NotNull(args);
            Assert.Equal(ProviderId, Assert.IsType<Guid>(args[0]));
            return Task.FromException<ProviderTestChatResult>(
                new ProviderRuntimeProfileUnavailableException(ProviderId));
        }
    }

    private sealed class FixtureLease<TFixture> : IAsyncDisposable
        where TFixture : IAsyncLifetime, new()
    {
        private FixtureLease(TFixture value)
        {
            Value = value;
        }

        public TFixture Value { get; }

        public static async Task<FixtureLease<TFixture>> CreateAsync()
        {
            var fixture = new TFixture();
            await fixture.InitializeAsync();
            return new FixtureLease<TFixture>(fixture);
        }

        public async ValueTask DisposeAsync()
        {
            await Value.DisposeAsync();
        }
    }
}
