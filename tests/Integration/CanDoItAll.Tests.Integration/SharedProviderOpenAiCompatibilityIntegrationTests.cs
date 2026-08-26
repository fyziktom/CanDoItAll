using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Security.Abstractions;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.SharedProviders.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using AgentFrameworkProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using PersistedProviderKind = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderKind;
using PersistedProviderProfile = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile;

namespace CanDoItAll.Tests.Integration;

public sealed class SharedProviderOpenAiCompatibilityIntegrationTests(
    SharedProviderOpenAiCompatibilityFixture fixture)
    : IClassFixture<SharedProviderOpenAiCompatibilityFixture>
{
    [Fact]
    public async Task PersistedProviderRelay_ResolvesRouteSecretAndFinalizesMetadataOnlyAudit()
    {
        fixture.PersistedDispatcher.Reset();
        const string accessContext = "persisted-relay-context";
        using var request = CreatePost(
            SharedProviderRoutes.ChatCompletions,
            ChatJson(fixture.PersistedChatModelId));
        request.Headers.TryAddWithoutValidation(
            SharedProviderHeaders.AccessContextReference,
            accessContext);

        using var chatResponse = await fixture.PersistedHost.Client.SendAsync(request);
        string chatBody = await chatResponse.Content.ReadAsStringAsync();

        using var responsesRequest = CreatePost(
            SharedProviderRoutes.Responses,
            ResponsesJson(fixture.PersistedResponsesModelId));
        responsesRequest.Headers.TryAddWithoutValidation(
            SharedProviderHeaders.AccessContextReference,
            accessContext);
        using var responsesResponse = await fixture.PersistedHost.Client.SendAsync(responsesRequest);
        string responsesBody = await responsesResponse.Content.ReadAsStringAsync();

        using var imageRequest = CreatePost(
            SharedProviderRoutes.ImageGenerations,
            ImagesJson(fixture.PersistedImageRoutingModelId));
        imageRequest.Headers.TryAddWithoutValidation(
            SharedProviderHeaders.AccessContextReference,
            accessContext);
        using var imageResponse = await fixture.PersistedHost.Client.SendAsync(imageRequest);
        string imageBody = await imageResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, chatResponse.StatusCode);
        Assert.Contains($"\"model\":\"{fixture.PersistedChatModelId.Value}\"", chatBody);
        Assert.DoesNotContain(
            SharedProviderOpenAiCompatibilityFixture.PersistedChatUpstreamModel,
            chatBody,
            StringComparison.Ordinal);
        AssertInferenceHeaders(chatResponse);
        Assert.Equal(HttpStatusCode.OK, responsesResponse.StatusCode);
        Assert.Contains(
            $"\"model\":\"{fixture.PersistedResponsesModelId.Value}\"",
            responsesBody);
        Assert.DoesNotContain(
            SharedProviderOpenAiCompatibilityFixture.PersistedResponsesUpstreamModel,
            responsesBody,
            StringComparison.Ordinal);
        AssertInferenceHeaders(responsesResponse);
        Assert.Equal(HttpStatusCode.OK, imageResponse.StatusCode);
        Assert.Contains("b64_json", imageBody, StringComparison.Ordinal);
        Assert.DoesNotContain("file:", imageBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("127.0.0.1", imageBody, StringComparison.Ordinal);
        AssertInferenceHeaders(imageResponse);
        string[] requestIds =
        [
            Assert.Single(chatResponse.Headers.GetValues(SharedProviderHeaders.RequestId)),
            Assert.Single(responsesResponse.Headers.GetValues(SharedProviderHeaders.RequestId)),
            Assert.Single(imageResponse.Headers.GetValues(SharedProviderHeaders.RequestId))
        ];

        var dispatchedRequests = fixture.PersistedDispatcher.Requests.ToArray();
        Assert.Equal(3, dispatchedRequests.Length);
        var dispatched = Assert.Single(
            dispatchedRequests,
            item => item.Request.Operation == SharedProviderRelayOperation.ChatCompletions);
        Assert.Equal(fixture.PersistedChatPublicationId, dispatched.Target.PublicationId);
        Assert.Equal(fixture.PersistedChatProfileId, dispatched.Target.ProviderProfileId);
        Assert.Equal(CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys.OpenAi, dispatched.Target.ConnectorPluginKey);
        Assert.Equal(new Uri("https://persisted-upstream.example.test/private/v1"), dispatched.Target.BaseUri);
        Assert.Equal(
            SharedProviderOpenAiCompatibilityFixture.PersistedChatUpstreamModel,
            dispatched.Target.UpstreamModelId);
        Assert.Equal(fixture.PersistedChatModelId, dispatched.Target.PublicModelId);
        Assert.Equal(TimeSpan.FromSeconds(37), dispatched.Target.Timeout);
        Assert.Equal(
            SharedProviderOpenAiCompatibilityFixture.PersistedCredential,
            Assert.IsType<SharedProviderRelayCredential>(dispatched.Target.Credential)
                .UseValue(value => value));
        Assert.Equal(SharedProviderRelayOperation.ChatCompletions, dispatched.Request.Operation);
        var dispatchedResponses = Assert.Single(
            dispatchedRequests,
            item => item.Request.Operation == SharedProviderRelayOperation.Responses);
        Assert.Equal(fixture.PersistedResponsesPublicationId, dispatchedResponses.Target.PublicationId);
        Assert.Equal(fixture.PersistedResponsesProfileId, dispatchedResponses.Target.ProviderProfileId);
        Assert.Equal(
            SharedProviderOpenAiCompatibilityFixture.PersistedResponsesUpstreamModel,
            dispatchedResponses.Target.UpstreamModelId);
        using (var responsesPayload = JsonDocument.Parse(
            dispatchedResponses.Request.CanonicalPayloadUtf8))
        {
            Assert.Equal(
                JsonValueKind.False,
                responsesPayload.RootElement.GetProperty("store").ValueKind);
        }

        var dispatchedImage = Assert.Single(
            dispatchedRequests,
            item => item.Request.Operation == SharedProviderRelayOperation.ImageGenerations);
        Assert.Equal(fixture.PersistedImagePublicationId, dispatchedImage.Target.PublicationId);
        Assert.Equal(fixture.PersistedImageProfileId, dispatchedImage.Target.ProviderProfileId);
        Assert.Equal(
            SharedProviderOpenAiCompatibilityFixture.PersistedImageModel,
            dispatchedImage.Target.UpstreamModelId);

        await using var scope = fixture.PersistedHost.App.Services.CreateAsyncScope();
        Assert.IsType<SharedProviderRelayApplicationService>(
            scope.ServiceProvider.GetRequiredService<ISharedProviderRelayApplicationService>());
        Assert.IsType<SharedProviderCatalogQueryService>(
            scope.ServiceProvider.GetRequiredService<ISharedProviderRoutingResolver>());
        Assert.IsType<SecretRuntimeResolver>(
            scope.ServiceProvider.GetRequiredService<ISecretRuntimeResolver>());
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var invocations = await dbContext.Set<SharedProviderInvocationRecord>()
            .AsNoTracking()
            .Where(record => requestIds.Contains(record.RequestId))
            .ToArrayAsync();

        Assert.Equal(3, invocations.Length);
        var chatInvocation = Assert.Single(invocations, invocation =>
            invocation.PublicationId == fixture.PersistedChatPublicationId &&
            invocation.ProviderProfileId == fixture.PersistedChatProfileId &&
            invocation.Operation == SharedProviderRelayOperation.ChatCompletions);
        var responsesInvocation = Assert.Single(invocations, invocation =>
            invocation.PublicationId == fixture.PersistedResponsesPublicationId &&
            invocation.ProviderProfileId == fixture.PersistedResponsesProfileId &&
            invocation.Operation == SharedProviderRelayOperation.Responses);
        var imageInvocation = Assert.Single(invocations, invocation =>
            invocation.PublicationId == fixture.PersistedImagePublicationId &&
            invocation.ProviderProfileId == fixture.PersistedImageProfileId &&
            invocation.Operation == SharedProviderRelayOperation.ImageGenerations);
        Assert.All(invocations, invocation =>
        {
            Assert.Equal("api-authorization-disabled", invocation.AuthenticatedSubject);
            Assert.Equal(accessContext, invocation.AccessContextReference?.Value);
            Assert.Equal(SharedProviderInvocationOutcome.Succeeded, invocation.Outcome);
            Assert.Null(invocation.FailureCategory);
            Assert.Null(invocation.Price);
            Assert.Equal(SharedProviderMetadataCompleteness.Unavailable, invocation.PricingCompleteness);
        });
        Assert.Equal(2, chatInvocation.InputTokenCount);
        Assert.Equal(3, chatInvocation.OutputTokenCount);
        Assert.Null(chatInvocation.ImageCount);
        Assert.Equal(SharedProviderMetadataCompleteness.Complete, chatInvocation.UsageCompleteness);
        Assert.Equal(4, responsesInvocation.InputTokenCount);
        Assert.Equal(5, responsesInvocation.OutputTokenCount);
        Assert.Null(responsesInvocation.ImageCount);
        Assert.Equal(SharedProviderMetadataCompleteness.Complete, responsesInvocation.UsageCompleteness);
        Assert.Null(imageInvocation.InputTokenCount);
        Assert.Null(imageInvocation.OutputTokenCount);
        Assert.Equal(1, imageInvocation.ImageCount);
        Assert.Equal(SharedProviderMetadataCompleteness.Complete, imageInvocation.UsageCompleteness);

        var usageSource = scope.ServiceProvider
            .GetServices<IProviderUsageProjectionSource>()
            .Single(source => string.Equals(
                source.SourceName,
                SharedProviderRelayUsageProjectionSource.SourceIdentity,
                StringComparison.Ordinal));
        var usage = await usageSource.ReadAsync();
        Assert.Equal(ProviderUsageSourceState.Complete, usage.State);
        var imageContribution = Assert.Single(
            usage.Contributions,
            contribution => contribution.ExecutionId == imageInvocation.RequestId);
        Assert.Equal(ProviderUsageCompleteness.Observed, imageContribution.UsageCompleteness);
        Assert.Equal(1, imageContribution.ImageCount);
        Assert.Equal(ProviderUsageTokenCounts.Empty, imageContribution.Tokens);
        var unavailableContribution = Assert.Single(
            usage.Contributions,
            contribution => contribution.ExecutionId == fixture.FreshInvocationRequestId);
        Assert.Equal(
            ProviderUsageCompleteness.UsageUnavailable,
            unavailableContribution.UsageCompleteness);
        Assert.Null(unavailableContribution.ImageCount);
        Assert.Equal(ProviderUsageTokenCounts.Empty, unavailableContribution.Tokens);
        var usageSnapshot = await new ProviderUsageQueryService([usageSource])
            .QueryAsync(ProviderUsageWorkloadSelection.SharedProviderRelays);
        var expectedImageCount = usage.Contributions.Sum(contribution => contribution.ImageCount ?? 0);
        var expectedTokenCount = usage.Contributions.Sum(contribution => contribution.Tokens.TotalTokens);
        Assert.True(expectedImageCount >= 1);
        Assert.Equal(expectedImageCount, usageSnapshot.Totals.ImageCount);
        Assert.Equal(expectedTokenCount, usageSnapshot.Totals.Tokens.TotalTokens);
    }

    [Fact]
    public async Task InterruptedInvocationRecovery_FinalizesOnlyStaleInProgressRecords()
    {
        await fixture.WaitForInterruptedInvocationRecoveryAsync();

        await using var scope = fixture.PersistedHost.App.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var invocations = await dbContext.Set<SharedProviderInvocationRecord>()
            .AsNoTracking()
            .Where(record =>
                record.RequestId == fixture.StaleInvocationRequestId ||
                record.RequestId == fixture.FreshInvocationRequestId ||
                record.RequestId == fixture.TerminalInvocationRequestId)
            .ToDictionaryAsync(record => record.RequestId, StringComparer.Ordinal);

        var stale = invocations[fixture.StaleInvocationRequestId];
        Assert.Equal(SharedProviderInvocationOutcome.Failed, stale.Outcome);
        Assert.Equal(SharedProviderFailureCategory.Unavailable, stale.FailureCategory);
        Assert.NotNull(stale.CompletedAtUtc);
        Assert.Null(stale.InputTokenCount);
        Assert.Null(stale.OutputTokenCount);
        Assert.Equal(SharedProviderMetadataCompleteness.Unavailable, stale.UsageCompleteness);
        Assert.Null(stale.Price);
        Assert.Equal(SharedProviderMetadataCompleteness.Unavailable, stale.PricingCompleteness);

        var fresh = invocations[fixture.FreshInvocationRequestId];
        Assert.Equal(SharedProviderInvocationOutcome.InProgress, fresh.Outcome);
        Assert.Null(fresh.CompletedAtUtc);
        Assert.Null(fresh.FailureCategory);

        var terminal = invocations[fixture.TerminalInvocationRequestId];
        Assert.Equal(SharedProviderInvocationOutcome.Succeeded, terminal.Outcome);
        Assert.Null(terminal.FailureCategory);
        Assert.Equal(2, terminal.InputTokenCount);
        Assert.Equal(3, terminal.OutputTokenCount);
        Assert.Equal(SharedProviderMetadataCompleteness.Complete, terminal.UsageCompleteness);
    }

    [Fact]
    public async Task ImageExecutionTargetResolver_RequiresExactCurrentEligiblePublication()
    {
        await using var scope = fixture.PersistedHost.App.Services.CreateAsyncScope();
        var resolver = scope.ServiceProvider
            .GetRequiredService<ISharedProviderImageExecutionTargetResolver>();
        var exactRequest = fixture.CreatePersistedImageRequest();

        var exact = await resolver.ResolveAsync(exactRequest);

        Assert.NotNull(exact);
        Assert.Equal(fixture.PersistedImageProfileId, exact.Profile.Id);
        Assert.Equal(
            SharedProviderOpenAiCompatibilityFixture.PersistedImageModel,
            exact.Profile.DefaultModel);
        Assert.Null(await resolver.ResolveAsync(exactRequest with
        {
            PublicationId = new SharedProviderPublicationId(
                Guid.Parse("42357d2a-b487-44ba-a781-45c3ae720eb8"))
        }));
        Assert.Null(await resolver.ResolveAsync(exactRequest with
        {
            ProviderProfileId = Guid.Parse("359b3124-c96f-40f1-a8b2-2bed00a05aaa")
        }));

        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        try
        {
            await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
            {
                var publication = await dbContext.Set<ProviderSharePublication>()
                    .SingleAsync(item => item.PublicId == fixture.PersistedImagePublicationId);
                SharedProviderPublicationTransitions.Unpublish(
                    publication,
                    publication.UpdatedAtUtc.AddSeconds(1));
                await dbContext.SaveChangesAsync();
            }

            Assert.Null(await resolver.ResolveAsync(exactRequest));

            await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
            {
                var publication = await dbContext.Set<ProviderSharePublication>()
                    .SingleAsync(item => item.PublicId == fixture.PersistedImagePublicationId);
                SharedProviderPublicationTransitions.Publish(
                    publication,
                    publication.UpdatedAtUtc.AddSeconds(1));
                await dbContext.SaveChangesAsync();
            }

            Assert.NotNull(await resolver.ResolveAsync(exactRequest));

            await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
            {
                var profile = await dbContext.Set<PersistedProviderProfile>()
                    .SingleAsync(item => item.Id == fixture.PersistedImageProfileId);
                profile.ApiKeySecretId = null;
                await dbContext.SaveChangesAsync();
            }

            Assert.Null(await resolver.ResolveAsync(exactRequest));
        }
        finally
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var publication = await dbContext.Set<ProviderSharePublication>()
                .SingleAsync(item => item.PublicId == fixture.PersistedImagePublicationId);
            if (!publication.IsPublished)
            {
                SharedProviderPublicationTransitions.Publish(
                    publication,
                    publication.UpdatedAtUtc.AddSeconds(1));
            }

            var profile = await dbContext.Set<PersistedProviderProfile>()
                .SingleAsync(item => item.Id == fixture.PersistedImageProfileId);
            profile.ApiKeySecretId = fixture.PersistedImageSecretId;
            await dbContext.SaveChangesAsync();
        }

        Assert.NotNull(await resolver.ResolveAsync(exactRequest));
    }

    [Fact]
    public async Task OpenAiAdapter_RoutesChatToStoredTargetAndRewritesPublicModel()
    {
        await using var relay = DirectRelayFixture.Create(
            baseUri: new Uri("https://upstream.example.test/reverse/v1"));

        var result = await relay.DispatchAsync(
            "provider.openai",
            SharedProviderPurpose.Chat,
            SharedProviderRelayOperation.ChatCompletions,
            ChatJson(relay.ModelId));

        var buffered = Assert.IsType<SharedProviderRelayDispatchResult.Buffered>(result);
        var request = Assert.Single(relay.Handler.Requests);
        Assert.Equal("/reverse/v1/chat/completions", request.Uri.AbsolutePath);
        Assert.Equal("Bearer central-secret", request.Authorization);
        Assert.Contains($"\"model\":\"{relay.ModelId.Value}\"", Encoding.UTF8.GetString(buffered.PayloadUtf8.Span));
        Assert.Contains($"\"model\":\"{SharedProviderRelayTestData.UpstreamModel}\"", request.Body);
    }

    [Fact]
    public async Task OpenAiAdapter_RoutesResponsesAndRewritesPublicModel()
    {
        await using var relay = DirectRelayFixture.Create(
            baseUri: new Uri("https://upstream.example.test/openai/v1"));

        var result = await relay.DispatchAsync(
            "provider.openai",
            SharedProviderPurpose.Chat,
            SharedProviderRelayOperation.Responses,
            ResponsesJson(relay.ModelId));

        var buffered = Assert.IsType<SharedProviderRelayDispatchResult.Buffered>(result);
        var request = Assert.Single(relay.Handler.Requests);
        Assert.Equal("/openai/v1/responses", request.Uri.AbsolutePath);
        using (var upstreamPayload = JsonDocument.Parse(request.Body))
        {
            Assert.Equal(
                JsonValueKind.False,
                upstreamPayload.RootElement.GetProperty("store").ValueKind);
        }

        Assert.Contains($"\"model\":\"{relay.ModelId.Value}\"", Encoding.UTF8.GetString(buffered.PayloadUtf8.Span));
    }

    [Fact]
    public async Task OllamaLocal_UsesOpenAiCompatibleChatRoute()
    {
        await using var relay = DirectRelayFixture.Create(
            baseUri: new Uri("http://127.0.0.1:11434/tenant/local"));

        var result = await relay.DispatchAsync(
            "provider.ollama.local",
            SharedProviderPurpose.Chat,
            SharedProviderRelayOperation.ChatCompletions,
            ChatJson(relay.ModelId));

        Assert.IsType<SharedProviderRelayDispatchResult.Buffered>(result);
        Assert.Equal("/tenant/local/v1/chat/completions", Assert.Single(relay.Handler.Requests).Uri.AbsolutePath);
    }

    [Fact]
    public async Task OllamaRemote_PreservesBasePathAndUsesOpenAiCompatibleChatRoute()
    {
        await using var relay = DirectRelayFixture.Create(
            baseUri: new Uri("https://ollama.example.test/reverse/proxy"));

        var result = await relay.DispatchAsync(
            "provider.ollama.remote",
            SharedProviderPurpose.Chat,
            SharedProviderRelayOperation.ChatCompletions,
            ChatJson(relay.ModelId));

        Assert.IsType<SharedProviderRelayDispatchResult.Buffered>(result);
        var request = Assert.Single(relay.Handler.Requests);
        Assert.Equal("/reverse/proxy/v1/chat/completions", request.Uri.AbsolutePath);
        Assert.Equal("Bearer central-secret", request.Authorization);
    }

    [Fact]
    public async Task ComfyUiAdapter_MapsExistingImageCapabilityToBase64()
    {
        var imageRelay = new RecordingImageCapabilityRelay(
            [new SharedProviderGeneratedImage("image/png", new byte[] { 1, 2, 3, 4 }, "safe prompt")]);
        await using var relay = DirectRelayFixture.Create(imageRelay: imageRelay);

        var result = await relay.DispatchAsync(
            "provider.comfyui.local",
            SharedProviderPurpose.ImageGeneration,
            SharedProviderRelayOperation.ImageGenerations,
            ImagesJson(relay.ModelId));

        var buffered = Assert.IsType<SharedProviderRelayDispatchResult.Buffered>(result);
        string body = Encoding.UTF8.GetString(buffered.PayloadUtf8.Span);
        Assert.Equal(Convert.ToBase64String([1, 2, 3, 4]), JsonDocument.Parse(body)
            .RootElement.GetProperty("data")[0].GetProperty("b64_json").GetString());
        Assert.DoesNotContain("file:", body, StringComparison.OrdinalIgnoreCase);
        var imageRequest = Assert.Single(imageRelay.Requests);
        Assert.Equal(relay.PublicationId, imageRequest.PublicationId);
        Assert.Equal(SharedProviderRelayTestData.UpstreamModel, imageRequest.Model);
        Assert.Empty(relay.Handler.Requests);
    }

    [Fact]
    public async Task FunctionTools_RoundTripWithoutCentralExecution()
    {
        await using var relay = DirectRelayFixture.Create();
        string payload = ChatJson(
            relay.ModelId,
            """
            "tools":[{"type":"function","function":{"name":"weather","parameters":{"type":"object","properties":{"city":{"type":"string"}},"required":["city"]}}}],"tool_choice":{"type":"function","function":{"name":"weather"}}
            """);

        var result = await relay.DispatchAsync(
            "provider.openai",
            SharedProviderPurpose.Chat,
            SharedProviderRelayOperation.ChatCompletions,
            payload);

        Assert.IsType<SharedProviderRelayDispatchResult.Buffered>(result);
        string upstream = Assert.Single(relay.Handler.Requests).Body;
        Assert.Contains("\"name\":\"weather\"", upstream, StringComparison.Ordinal);
        Assert.Contains("\"required\":[\"city\"]", upstream, StringComparison.Ordinal);
        Assert.Contains("\"tool_choice\"", upstream, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StructuredOutput_IsRelayedWhenAdvertised()
    {
        await using var relay = DirectRelayFixture.Create();
        string payload = ChatJson(
            relay.ModelId,
            """
            "response_format":{"type":"json_schema","json_schema":{"name":"answer","schema":{"type":"object","properties":{"value":{"type":"string"}},"required":["value"]},"strict":true}}
            """);

        var result = await relay.DispatchAsync(
            "provider.openai",
            SharedProviderPurpose.Chat,
            SharedProviderRelayOperation.ChatCompletions,
            payload);

        Assert.IsType<SharedProviderRelayDispatchResult.Buffered>(result);
        Assert.Contains("\"json_schema\"", Assert.Single(relay.Handler.Requests).Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StructuredOutput_IsRejectedWhenNotAdvertised()
    {
        fixture.OpenHarness.Reset();
        string payload = ChatJson(
            SharedProviderRelayTestData.LimitedChatModelId,
            "\"response_format\":{\"type\":\"json_object\"}");

        using var response = await PostAsync(
            fixture.OpenHost.Client,
            SharedProviderRoutes.ChatCompletions,
            payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, fixture.OpenHarness.DispatchCount);
        await AssertOpenAiErrorAsync(response, SharedProviderFailureCategory.Validation);
    }

    [Fact]
    public async Task VisionInput_IsRejectedWithoutCapability()
    {
        fixture.OpenHarness.Reset();
        string payload =
            $"{{\"model\":\"{SharedProviderRelayTestData.ChatModelId.Value}\",\"messages\":[{{\"role\":\"user\",\"content\":[{{\"type\":\"text\",\"text\":\"describe\"}},{{\"type\":\"image_url\",\"image_url\":{{\"url\":\"data:image/png;base64,iVBORw0KGgo=\"}}}}]}}]}}";

        using var response = await PostAsync(
            fixture.OpenHost.Client,
            SharedProviderRoutes.ChatCompletions,
            payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, fixture.OpenHarness.DispatchCount);
    }

    [Fact]
    public async Task UnknownAndUnpublishedModels_ReturnNotFoundWithoutUpstream()
    {
        fixture.OpenHarness.Reset();
        var unpublished = SharedProviderRoutingModelIdCodec.Create(
            new SharedProviderPublicationId(Guid.Parse("1f7bc086-919d-4d57-a110-03fbb00581be")),
            SharedProviderRelayTestData.UpstreamModel);

        foreach (string model in new[] { "../../private", unpublished.Value })
        {
            using var response = await PostAsync(
                fixture.OpenHost.Client,
                SharedProviderRoutes.ChatCompletions,
                ChatJson(model));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            await AssertOpenAiErrorAsync(response, SharedProviderFailureCategory.NotFound);
        }

        Assert.Equal(0, fixture.OpenHarness.DispatchCount);
    }

    [Fact]
    public async Task OperationAndPurposeMismatch_ReturnsConflictWithoutUpstream()
    {
        fixture.OpenHarness.Reset();
        var requests = new[]
        {
            (SharedProviderRoutes.ChatCompletions, ChatJson(SharedProviderRelayTestData.ImageModelId)),
            (SharedProviderRoutes.ImageGenerations, ImagesJson(SharedProviderRelayTestData.ChatModelId))
        };

        foreach (var (route, payload) in requests)
        {
            using var response = await PostAsync(fixture.OpenHost.Client, route, payload);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await AssertOpenAiErrorAsync(response, SharedProviderFailureCategory.Conflict);
        }

        Assert.Equal(0, fixture.OpenHarness.DispatchCount);
    }

    [Fact]
    public async Task MalformedUnknownDuplicateAndOversizedRequests_AreRejectedBeforeDispatch()
    {
        fixture.OpenHarness.Reset();
        string oversizedContent = new('x', 4 * 1024 * 1024 + 1);
        string oversized = $$"""
            {"model":"{{SharedProviderRelayTestData.ChatModelId.Value}}","messages":[{"role":"user","content":"{{oversizedContent}}"}]}
            """;
        string duplicate = $$"""
            {"model":"{{SharedProviderRelayTestData.ChatModelId.Value}}","model":"{{SharedProviderRelayTestData.ChatModelId.Value}}","messages":[]}
            """;
        foreach (string payload in new[]
        {
            "{",
            ChatJson(SharedProviderRelayTestData.ChatModelId, "\"endpoint\":\"https://attacker.test/v1\""),
            duplicate,
            oversized
        })
        {
            using var response = await PostAsync(
                fixture.OpenHost.Client,
                SharedProviderRoutes.ChatCompletions,
                payload);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        foreach (string member in new[]
        {
            "\"store\":true",
            "\"store\":null",
            "\"store\":\"false\""
        })
        {
            using var response = await PostAsync(
                fixture.OpenHost.Client,
                SharedProviderRoutes.Responses,
                ResponsesJson(SharedProviderRelayTestData.ChatModelId, member));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        Assert.Equal(0, fixture.OpenHarness.DispatchCount);
    }

    [Fact]
    public async Task ImageBoundsAndUrlOutput_AreRejectedBeforeDispatch()
    {
        fixture.OpenHarness.Reset();
        foreach (string extra in new[]
        {
            "\"n\":5",
            "\"size\":\"123x456\"",
            "\"response_format\":\"url\"",
            "\"output_format\":\"gif\""
        })
        {
            using var response = await PostAsync(
                fixture.OpenHost.Client,
                SharedProviderRoutes.ImageGenerations,
                ImagesJson(SharedProviderRelayTestData.ImageModelId, extra));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        Assert.Equal(0, fixture.OpenHarness.DispatchCount);
    }

    [Fact]
    public async Task CallerCannotOverrideStoredUriHeadersOrUpstreamModel()
    {
        await using var relay = DirectRelayFixture.Create(
            baseUri: new Uri("https://stored.example.test/private-base/v1"));
        var descriptor = relay.GetSupport("provider.openai", SharedProviderPurpose.Chat);
        foreach (string payload in new[]
        {
            ChatJson(relay.ModelId, "\"base_url\":\"https://attacker.test/v1\""),
            ChatJson(relay.ModelId, "\"headers\":{\"Authorization\":\"Bearer attacker\"}"),
            ChatJson("upstream-model")
        })
        {
            Assert.IsType<SharedProviderRelayRequestPolicyResult.Rejected>(
                new SharedProviderRelayRequestPolicy().Normalize(
                    SharedProviderRelayOperation.ChatCompletions,
                    Encoding.UTF8.GetBytes(payload),
                    descriptor));
        }

        var result = await relay.DispatchAsync(
            "provider.openai",
            SharedProviderPurpose.Chat,
            SharedProviderRelayOperation.ChatCompletions,
            ChatJson(relay.ModelId));
        Assert.IsType<SharedProviderRelayDispatchResult.Buffered>(result);
        var request = Assert.Single(relay.Handler.Requests);
        Assert.Equal("stored.example.test", request.Uri.Host);
        Assert.Equal("/private-base/v1/chat/completions", request.Uri.AbsolutePath);
        Assert.Equal("Bearer central-secret", request.Authorization);
        Assert.DoesNotContain(SharedProviderHeaders.AccessContextReference, request.Headers.Keys);
    }

    [Fact]
    public async Task AccessContextAndSubject_AreRecordedButAbsentFromUpstreamShape()
    {
        fixture.SecureHarness.Reset();
        const string subject = "relay-audit-subject";
        const string accessContext = "opaque-context-reference";
        using var request = CreatePost(
            SharedProviderRoutes.ChatCompletions,
            ChatJson(SharedProviderRelayTestData.ChatModelId));
        request.Headers.Authorization = IssueToken(
            fixture.SecureHost,
            ApiAccessScopeNames.InvokeSharedProviders,
            subject);
        request.Headers.TryAddWithoutValidation(
            SharedProviderHeaders.AccessContextReference,
            accessContext);
        request.Headers.TryAddWithoutValidation("Cookie", "private-cookie=value");

        using var response = await fixture.SecureHost.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var captured = Assert.Single(fixture.SecureHarness.Requests);
        Assert.Equal(subject, captured.Context.AuthenticatedSubject);
        Assert.Equal(accessContext, captured.Context.AccessContextReference?.Value);
        string normalized = Encoding.UTF8.GetString(Assert.Single(fixture.SecureHarness.Accepted).CanonicalPayloadUtf8.Span);
        Assert.DoesNotContain(subject, normalized, StringComparison.Ordinal);
        Assert.DoesNotContain(accessContext, normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("private-cookie", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAndUmbrellaScopes_AuthorizeAllPostSurfaces()
    {
        foreach (string scope in new[]
        {
            ApiAccessScopeNames.InvokeSharedProviders,
            ApiAccessScopeNames.Api
        })
        {
            foreach (var (route, payload) in ValidRequests())
            {
                using var request = CreatePost(route, payload);
                request.Headers.Authorization = IssueToken(
                    fixture.SecureHost,
                    scope,
                    $"allowed-{scope}");
                using var response = await fixture.SecureHost.Client.SendAsync(request);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }
    }

    [Fact]
    public async Task MissingMalformedExpiredAndCatalogOnlyTokens_AreRejected()
    {
        fixture.SecureHarness.Reset();
        var vectors = new (AuthenticationHeaderValue? Authorization, HttpStatusCode Status)[]
        {
            (null, HttpStatusCode.Unauthorized),
            (new AuthenticationHeaderValue("Bearer", "not-a-jwt"), HttpStatusCode.Unauthorized),
            (new AuthenticationHeaderValue("Bearer", CreateExpiredToken()), HttpStatusCode.Unauthorized),
            (IssueToken(
                fixture.SecureHost,
                ApiAccessScopeNames.ReadSharedProviderCatalog,
                "catalog-only"), HttpStatusCode.Forbidden)
        };

        foreach (var vector in vectors)
        {
            using var request = CreatePost(
                SharedProviderRoutes.ChatCompletions,
                ChatJson(SharedProviderRelayTestData.ChatModelId));
            request.Headers.Authorization = vector.Authorization;
            using var response = await fixture.SecureHost.Client.SendAsync(request);
            Assert.Equal(vector.Status, response.StatusCode);
            var error = await ReadErrorAsync(response);
            Assert.Equal(
                vector.Status == HttpStatusCode.Unauthorized
                    ? SharedProviderOpenAiConstants.AuthenticationErrorType
                    : SharedProviderOpenAiConstants.PermissionErrorType,
                error.Error.Type);
            AssertInferenceHeaders(response);
        }

        Assert.Empty(fixture.SecureHarness.Requests);
    }

    [Fact]
    public async Task ConflictRateUnavailableAndTimeout_MapToSanitizedErrorsAndSafeHeaders()
    {
        var failures = new[]
        {
            (SharedProviderFailureCategory.Conflict, HttpStatusCode.Conflict, (TimeSpan?)null),
            (SharedProviderFailureCategory.RateLimited, HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds(7)),
            (SharedProviderFailureCategory.Unavailable, HttpStatusCode.ServiceUnavailable, (TimeSpan?)null),
            (SharedProviderFailureCategory.Timeout, HttpStatusCode.GatewayTimeout, (TimeSpan?)null)
        };

        foreach (var (category, status, retryAfter) in failures)
        {
            fixture.OpenHarness.Reset();
            fixture.OpenHarness.NextResult = new SharedProviderRelayDispatchResult.Failed(
                Failure(category, "A sanitized relay failure.", retryAfter));
            using var response = await PostAsync(
                fixture.OpenHost.Client,
                SharedProviderRoutes.ChatCompletions,
                ChatJson(SharedProviderRelayTestData.ChatModelId));
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(status, response.StatusCode);
            Assert.DoesNotContain("10.0.0.4", body, StringComparison.Ordinal);
            Assert.DoesNotContain("central-secret", body, StringComparison.Ordinal);
            Assert.False(response.Headers.Contains("Set-Cookie"));
            Assert.False(response.Headers.Contains("Location"));
            AssertInferenceHeaders(response);
            if (retryAfter.HasValue)
            {
                Assert.Equal(retryAfter, response.Headers.RetryAfter?.Delta);
            }
        }

        await using var relay = DirectRelayFixture.Create(responseFactory: _ =>
        {
            var response = JsonResponse(
                "{\"error\":{\"message\":\"central-secret at http://10.0.0.4/private\"}}",
                HttpStatusCode.TooManyRequests);
            response.Headers.TryAddWithoutValidation("Retry-After", "999999999");
            response.Headers.TryAddWithoutValidation("Set-Cookie", "private=value");
            response.Headers.TryAddWithoutValidation("Location", "http://10.0.0.4/private");
            return response;
        });
        var direct = Assert.IsType<SharedProviderRelayDispatchResult.Failed>(await relay.DispatchAsync(
            "provider.openai",
            SharedProviderPurpose.Chat,
            SharedProviderRelayOperation.ChatCompletions,
            ChatJson(relay.ModelId)));
        Assert.Equal(SharedProviderFailure.MaximumRetryAfter, direct.Failure.RetryAfter);
        Assert.DoesNotContain("central-secret", direct.Failure.SanitizedMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("10.0.0.4", direct.Failure.SanitizedMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApi_ContainsExactlyThreePostSurfacesWithoutAudioOrEtag()
    {
        using var response = await fixture.OpenHost.Client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paths = document.RootElement.GetProperty("paths");
        var actualPosts = paths.EnumerateObject()
            .Where(path => path.Value.TryGetProperty("post", out _))
            .Select(path => path.Name)
            .Where(path => path.StartsWith(SharedProviderRoutes.OpenAiBase, StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
        [
            SharedProviderRoutes.ChatCompletions,
            SharedProviderRoutes.ImageGenerations,
            SharedProviderRoutes.Responses
        ], actualPosts);
        Assert.DoesNotContain(paths.EnumerateObject(), path =>
            path.Name.Contains("audio", StringComparison.OrdinalIgnoreCase));

        foreach (string path in actualPosts)
        {
            var operation = paths.GetProperty(path).GetProperty("post");
            Assert.True(operation.GetProperty("requestBody").GetProperty("required").GetBoolean());
            Assert.True(operation.GetProperty("requestBody").GetProperty("content")
                .TryGetProperty("application/json", out _));
            string operationJson = operation.GetRawText();
            Assert.DoesNotContain("If-None-Match", operationJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ETag", operationJson, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static IEnumerable<(string Route, string Payload)> ValidRequests()
    {
        yield return (
            SharedProviderRoutes.ChatCompletions,
            ChatJson(SharedProviderRelayTestData.ChatModelId));
        yield return (
            SharedProviderRoutes.Responses,
            ResponsesJson(SharedProviderRelayTestData.ChatModelId));
        yield return (
            SharedProviderRoutes.ImageGenerations,
            ImagesJson(SharedProviderRelayTestData.ImageModelId));
    }

    private string CreateExpiredToken()
    {
        var hostClock = fixture.SecureHost.App.Services.GetRequiredService<IClock>();
        var tokenService = new ApiTokenService(
            fixture.SecureHost.App.Services.GetRequiredService<IOptions<ApiAccessOptions>>(),
            new FixedClock(hostClock.GetUtcNow().AddHours(-2)));
        return tokenService.IssueToken(new ApiTokenIssueRequest
        {
            Subject = "expired-relay-client",
            DisplayName = "Expired relay client",
            LifetimeMinutes = 1,
            Scopes = [ApiAccessScopeNames.InvokeSharedProviders]
        }).Token;
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
                DisplayName = "Shared-provider relay test client",
                Scopes = [scope]
            });
        return new AuthenticationHeaderValue(token.TokenType, token.Token);
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

    private static async Task AssertOpenAiErrorAsync(
        HttpResponseMessage response,
        SharedProviderFailureCategory category)
    {
        var error = await ReadErrorAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(error.Error.Code));
        Assert.False(string.IsNullOrWhiteSpace(error.Error.Message));
        Assert.Equal(ExpectedErrorType(category), error.Error.Type);
        AssertInferenceHeaders(response);
    }

    private static async Task<SharedProviderOpenAiErrorEnvelope> ReadErrorAsync(
        HttpResponseMessage response)
        => JsonSerializer.Deserialize<SharedProviderOpenAiErrorEnvelope>(
            await response.Content.ReadAsStringAsync(),
            SharedProviderProtocolJson.Options) ??
            throw new InvalidOperationException("The OpenAI error envelope was empty.");

    private static string ExpectedErrorType(SharedProviderFailureCategory category)
        => category switch
        {
            SharedProviderFailureCategory.Validation or SharedProviderFailureCategory.NotFound =>
                SharedProviderOpenAiConstants.InvalidRequestErrorType,
            SharedProviderFailureCategory.Conflict => SharedProviderOpenAiConstants.ConflictErrorType,
            SharedProviderFailureCategory.RateLimited => SharedProviderOpenAiConstants.RateLimitErrorType,
            SharedProviderFailureCategory.Timeout => SharedProviderOpenAiConstants.TimeoutErrorType,
            _ => SharedProviderOpenAiConstants.ApiErrorType
        };

    private static void AssertInferenceHeaders(HttpResponseMessage response)
    {
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.False(response.Headers.Contains("ETag"));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.False(string.IsNullOrWhiteSpace(
            Assert.Single(response.Headers.GetValues(SharedProviderHeaders.RequestId))));
    }

    private static string ChatJson(
        SharedProviderRoutingModelId model,
        string? extra = null)
        => ChatJson(model.Value, extra);

    private static string ChatJson(string model, string? extra = null)
        => extra is null
            ? $$"""{"model":"{{model}}","messages":[{"role":"user","content":"hello"}]}"""
            : $$"""{"model":"{{model}}","messages":[{"role":"user","content":"hello"}],{{extra}}}""";

    private static string ResponsesJson(
        SharedProviderRoutingModelId model,
        string? extra = null)
        => extra is null
            ? $$"""{"model":"{{model.Value}}","input":"hello","instructions":"be concise"}"""
            : $$"""{"model":"{{model.Value}}","input":"hello","instructions":"be concise",{{extra}}}""";

    private static string ImagesJson(
        SharedProviderRoutingModelId model,
        string? extra = null)
        => extra is null
            ? $$"""{"model":"{{model.Value}}","prompt":"a blue square","n":1,"size":"1024x1024","response_format":"b64_json","output_format":"png"}"""
            : $$"""{"model":"{{model.Value}}","prompt":"a blue square",{{extra}}}""";

    private static SharedProviderFailure Failure(
        SharedProviderFailureCategory category,
        string message,
        TimeSpan? retryAfter = null)
        => new(
            category,
            new SharedProviderFailureCode($"relay_{category.ToString().ToLowerInvariant()}"),
            message,
            retryAfter: retryAfter);

    private static HttpResponseMessage JsonResponse(
        string body,
        HttpStatusCode status = HttpStatusCode.OK)
        => new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset GetUtcNow() => now;
    }
}

public sealed class SharedProviderOpenAiCompatibilityFixture : IAsyncLifetime
{
    private ApiTestHost? openHost;
    private ApiTestHost? secureHost;
    private ApiTestHost? persistedHost;

    internal const string PersistedCredential = "persisted-central-secret";
    internal const string PersistedChatUpstreamModel = "persisted-chat-model";
    internal const string PersistedResponsesUpstreamModel = "persisted-responses-model";
    internal const string PersistedImageModel = "persisted-image-model";

    internal string StaleInvocationRequestId { get; } = "persisted-recovery-stale";

    internal string FreshInvocationRequestId { get; } = "persisted-recovery-fresh";

    internal string TerminalInvocationRequestId { get; } = "persisted-recovery-terminal";

    internal Guid PersistedChatProfileId { get; } =
        Guid.Parse("0481cfad-8308-43af-a71a-1ed3670bc62d");

    internal Guid PersistedResponsesProfileId { get; } =
        Guid.Parse("c1e6eb5f-8f15-49c2-8992-978494241403");

    internal Guid PersistedImageProfileId { get; } =
        Guid.Parse("fb62a003-8391-49fd-9f5d-52123647fd37");

    internal Guid PersistedImageSecretId { get; private set; }

    internal SharedProviderPublicationId PersistedChatPublicationId { get; } = new(
        Guid.Parse("d6b079c6-f6cf-4658-baea-42de90b3ad7d"));

    internal SharedProviderPublicationId PersistedResponsesPublicationId { get; } = new(
        Guid.Parse("ab064696-bb0e-4757-8852-23376d1a6f20"));

    internal SharedProviderPublicationId PersistedImagePublicationId { get; } = new(
        Guid.Parse("a003ce7f-854d-49cf-bbab-1ca32f9ea70f"));

    internal ApiTestHost OpenHost
        => openHost ?? throw new InvalidOperationException("The open relay host is not initialized.");

    internal ApiTestHost SecureHost
        => secureHost ?? throw new InvalidOperationException("The secure relay host is not initialized.");

    internal ApiTestHost PersistedHost
        => persistedHost ?? throw new InvalidOperationException("The persisted relay host is not initialized.");

    internal SharedProviderRoutingModelId PersistedChatModelId
        => SharedProviderRoutingModelIdCodec.Create(
            PersistedChatPublicationId,
            PersistedChatUpstreamModel);

    internal SharedProviderRoutingModelId PersistedResponsesModelId
        => SharedProviderRoutingModelIdCodec.Create(
            PersistedResponsesPublicationId,
            PersistedResponsesUpstreamModel);

    internal SharedProviderRoutingModelId PersistedImageRoutingModelId
        => SharedProviderRoutingModelIdCodec.Create(
            PersistedImagePublicationId,
            PersistedImageModel);

    internal RecordingRelayApplicationService OpenHarness { get; } = new();

    internal RecordingRelayApplicationService SecureHarness { get; } = new();

    internal RecordingPersistedRelayDispatcher PersistedDispatcher { get; } = new();

    public async Task InitializeAsync()
    {
        openHost = await CreateHostAsync(jwtEnabled: false, OpenHarness);
        secureHost = await CreateHostAsync(jwtEnabled: true, SecureHarness);
        persistedHost = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            services =>
            {
                services.RemoveAll<ISharedProviderRelayDispatcher>();
                services.AddSingleton<ISharedProviderRelayDispatcher>(PersistedDispatcher);
                services.RemoveAll<SharedProviderInvocationRecoverySchedule>();
                services.AddSingleton(new SharedProviderInvocationRecoverySchedule(
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromMilliseconds(100)));
            });
        await SeedPersistedProviderGraphAsync();
    }

    public async Task DisposeAsync()
    {
        if (persistedHost is not null)
        {
            await persistedHost.DisposeAsync();
        }

        if (secureHost is not null)
        {
            await secureHost.DisposeAsync();
        }

        if (openHost is not null)
        {
            await openHost.DisposeAsync();
        }
    }

    private static Task<ApiTestHost> CreateHostAsync(
        bool jwtEnabled,
        RecordingRelayApplicationService harness)
        => ApiTestHost.CreateAsync(
            jwtEnabled,
            services =>
            {
                services.RemoveAll<ISharedProviderRelayApplicationService>();
                services.AddSingleton(harness);
                services.AddScoped<ISharedProviderRelayApplicationService>(provider =>
                    provider.GetRequiredService<RecordingRelayApplicationService>());
            },
            useInMemoryDatabase: true);

    internal SharedProviderImageCapabilityRequest CreatePersistedImageRequest()
        => new(
            PersistedImagePublicationId,
            PersistedImageProfileId,
            PersistedImageModel,
            "a persisted blue square",
            "1024x1024",
            "standard",
            "png",
            Count: 1);

    internal async Task WaitForInterruptedInvocationRecoveryAsync()
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var scope = PersistedHost.App.Services.CreateAsyncScope();
            var dbContextFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var outcome = await dbContext.Set<SharedProviderInvocationRecord>()
                .AsNoTracking()
                .Where(record => record.RequestId == StaleInvocationRequestId)
                .Select(record => record.Outcome)
                .SingleAsync();
            if (outcome == SharedProviderInvocationOutcome.Failed)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        Assert.Fail("The hosted shared-provider invocation recovery worker did not recover the stale record.");
    }

    private async Task SeedPersistedProviderGraphAsync()
    {
        var now = DateTimeOffset.UtcNow;
        await using var scope = PersistedHost.App.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var chatSecret = CreateSecret(
            "Persisted chat relay credential",
            protector.Protect(PersistedCredential),
            now);
        var responsesSecret = CreateSecret(
            "Persisted responses relay credential",
            protector.Protect(PersistedCredential),
            now);
        var imageSecret = CreateSecret(
            "Persisted image relay credential",
            protector.Protect(PersistedCredential),
            now);
        PersistedImageSecretId = imageSecret.Id;
        var chatProfile = CreateOpenAiProfile(
            PersistedChatProfileId,
            "Persisted chat relay",
            PersistedChatUpstreamModel,
            chatSecret.Id,
            ProviderTransportKind.ChatCompletions,
            ProviderProfilePurpose.Chat,
            supportsStreaming: true,
            supportsTools: true,
            supportsStructuredOutput: true);
        var responsesProfile = CreateOpenAiProfile(
            PersistedResponsesProfileId,
            "Persisted responses relay",
            PersistedResponsesUpstreamModel,
            responsesSecret.Id,
            ProviderTransportKind.Responses,
            ProviderProfilePurpose.Chat,
            supportsStreaming: true,
            supportsTools: true,
            supportsStructuredOutput: true);
        var imageProfile = CreateOpenAiProfile(
            PersistedImageProfileId,
            "Persisted image relay",
            PersistedImageModel,
            imageSecret.Id,
            ProviderTransportKind.Responses,
            ProviderProfilePurpose.ImageGeneration,
            supportsStreaming: false,
            supportsTools: false,
            supportsStructuredOutput: false);
        var chatPublication = CreatePublished(
            PersistedChatProfileId,
            PersistedChatPublicationId,
            now);
        var responsesPublication = CreatePublished(
            PersistedResponsesProfileId,
            PersistedResponsesPublicationId,
            now);
        var imagePublication = CreatePublished(
            PersistedImageProfileId,
            PersistedImagePublicationId,
            now);

        dbContext.AddRange(chatSecret, responsesSecret, imageSecret);
        dbContext.AddRange(chatProfile, responsesProfile, imageProfile);
        dbContext.AddRange(chatPublication, responsesPublication, imagePublication);
        await dbContext.SaveChangesAsync();

        var staleStartedAt = now - SharedProviderRelayTarget.MaximumTimeout - TimeSpan.FromMinutes(6);
        var freshStartedAt = now - SharedProviderRelayTarget.MaximumTimeout - TimeSpan.FromMinutes(3);
        var stale = CreateInvocation(StaleInvocationRequestId, staleStartedAt, now.AddDays(30));
        var fresh = CreateInvocation(FreshInvocationRequestId, freshStartedAt, now.AddDays(30));
        var terminal = CreateInvocation(
            TerminalInvocationRequestId,
            staleStartedAt,
            now.AddDays(30));
        SharedProviderInvocationTransitions.Finalize(
            terminal,
            new SharedProviderInvocationCompletion(
                SharedProviderInvocationOutcome.Succeeded,
                now.AddMinutes(-1),
                FailureCategory: null,
                InputTokenCount: 2,
                OutputTokenCount: 3,
                SharedProviderMetadataCompleteness.Complete,
                Price: null,
                SharedProviderMetadataCompleteness.Unavailable));
        dbContext.AddRange(stale, fresh, terminal);
        await dbContext.SaveChangesAsync();
    }

    private SharedProviderInvocationRecord CreateInvocation(
        string requestId,
        DateTimeOffset startedAtUtc,
        DateTimeOffset deleteAfterUtc)
        => SharedProviderInvocationTransitions.Create(
            requestId,
            PersistedChatPublicationId,
            PersistedChatProfileId,
            "persisted-recovery-subject",
            accessContextReference: null,
            traceId: $"trace-{requestId}",
            correlationId: $"correlation-{requestId}",
            SharedProviderRelayOperation.ChatCompletions,
            PersistedChatModelId,
            PersistedChatUpstreamModel,
            startedAtUtc,
            deleteAfterUtc);

    private static SecretRecord CreateSecret(
        string name,
        string encryptedPayload,
        DateTimeOffset now)
        => new()
        {
            Name = name,
            Kind = SecretKind.ApiKey,
            EncryptedPayload = encryptedPayload,
            Scope = "workspace",
            MetadataJson = "{}",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

    private static PersistedProviderProfile CreateOpenAiProfile(
        Guid id,
        string name,
        string upstreamModel,
        Guid secretId,
        ProviderTransportKind transport,
        ProviderProfilePurpose purpose,
        bool supportsStreaming,
        bool supportsTools,
        bool supportsStructuredOutput)
        => new()
        {
            Id = id,
            Name = name,
            ProviderKind = PersistedProviderKind.OpenAi,
            ConnectorPluginKey = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys.OpenAi,
            ConfigSchemaVersion = "1.0",
            BaseUrl = "https://persisted-upstream.example.test/private/v1",
            ApiKeySecretId = secretId,
            DefaultModel = upstreamModel,
            TimeoutSeconds = 37,
            IsEnabled = true,
            SupportsStreaming = supportsStreaming,
            SupportsToolCalling = supportsTools,
            SupportsStructuredOutput = supportsStructuredOutput,
            SupportsVision = false,
            ExtraSettingsJson = CanDoItAll.Modules.AgentFramework.ProviderManagement.SharedProviderProfilePublicationMetadataWriter.Write(
                "{}",
                AgentFrameworkProviderKind.OpenAi,
                transport,
                purpose,
                upstreamModel)
        };

    private static ProviderSharePublication CreatePublished(
        Guid profileId,
        SharedProviderPublicationId publicationId,
        DateTimeOffset now)
    {
        var publication = SharedProviderPublicationTransitions.Create(
            profileId,
            publicationId,
            now);
        SharedProviderPublicationTransitions.Publish(publication, now);
        return publication;
    }
}

internal sealed class RecordingPersistedRelayDispatcher : ISharedProviderRelayDispatcher
{
    public ConcurrentQueue<SharedProviderRelayDispatchRequest> Requests { get; } = new();

    public ValueTask<SharedProviderRelayDispatchResult> DispatchAsync(
        SharedProviderRelayDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Enqueue(request);
        string body = request.Request.Operation switch
        {
            SharedProviderRelayOperation.ChatCompletions =>
                $$"""{"id":"chatcmpl-persisted","object":"chat.completion","model":"{{request.Target.PublicModelId.Value}}","choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}]}""",
            SharedProviderRelayOperation.Responses =>
                $$"""{"id":"resp-persisted","object":"response","status":"completed","model":"{{request.Target.PublicModelId.Value}}","output":[]}""",
            SharedProviderRelayOperation.ImageGenerations =>
                $$"""{"created":1787533200,"data":[{"b64_json":"{{Convert.ToBase64String([1, 2, 3])}}"}]}""",
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };
        var usage = request.Request.Operation switch
        {
            SharedProviderRelayOperation.ChatCompletions => new SharedProviderRelayUsage(
                inputTokens: 2,
                outputTokens: 3,
                imageCount: null,
                SharedProviderRelayUsageCompleteness.Complete),
            SharedProviderRelayOperation.Responses => new SharedProviderRelayUsage(
                inputTokens: 4,
                outputTokens: 5,
                imageCount: null,
                SharedProviderRelayUsageCompleteness.Complete),
            SharedProviderRelayOperation.ImageGenerations => new SharedProviderRelayUsage(
                inputTokens: null,
                outputTokens: null,
                imageCount: 1,
                SharedProviderRelayUsageCompleteness.Complete),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };
        return ValueTask.FromResult<SharedProviderRelayDispatchResult>(
            new SharedProviderRelayDispatchResult.Buffered(
                Encoding.UTF8.GetBytes(body),
                "application/json",
                SharedProviderRelayResponseHeaders.Empty,
                usage));
    }

    public void Reset() => Requests.Clear();
}

internal static class SharedProviderRelayTestData
{
    public const string UpstreamModel = "upstream-model";

    private static readonly SharedProviderPublicationId ChatPublicationId = new(
        Guid.Parse("27789043-3970-43bd-a442-af274743d5e4"));
    private static readonly SharedProviderPublicationId LimitedPublicationId = new(
        Guid.Parse("fcce372b-d74b-44b8-b276-4251285a0117"));
    private static readonly SharedProviderPublicationId ImagePublicationId = new(
        Guid.Parse("0b47ce86-29a7-4cb9-95da-4c2b9c59b768"));

    public static SharedProviderRoutingModelId ChatModelId { get; } =
        SharedProviderRoutingModelIdCodec.Create(ChatPublicationId, UpstreamModel);

    public static SharedProviderRoutingModelId LimitedChatModelId { get; } =
        SharedProviderRoutingModelIdCodec.Create(LimitedPublicationId, UpstreamModel);

    public static SharedProviderRoutingModelId ImageModelId { get; } =
        SharedProviderRoutingModelIdCodec.Create(ImagePublicationId, "image-model");
}

internal sealed class RecordingRelayApplicationService : ISharedProviderRelayApplicationService
{
    private readonly SharedProviderRelayRequestPolicy policy = new();
    private int dispatchCount;

    public ConcurrentQueue<SharedProviderRelayApplicationRequest> Requests { get; } = new();

    public ConcurrentQueue<SharedProviderRelayNormalizedRequest> Accepted { get; } = new();

    public SharedProviderRelayDispatchResult? NextResult { get; set; }

    public int DispatchCount => Volatile.Read(ref dispatchCount);

    public ValueTask<SharedProviderRelayDispatchResult> InvokeAsync(
        SharedProviderRelayApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Enqueue(request);
        var lookup = policy.Normalize(request.Operation, request.PayloadUtf8, LookupSupport);
        if (lookup is SharedProviderRelayRequestPolicyResult.Rejected lookupFailure)
        {
            return ValueTask.FromResult<SharedProviderRelayDispatchResult>(
                new SharedProviderRelayDispatchResult.Failed(lookupFailure.Failure));
        }

        var normalized = ((SharedProviderRelayRequestPolicyResult.Accepted)lookup).Request;
        var support = ResolveSupport(normalized.RoutingModelId);
        if (support is null)
        {
            return ValueTask.FromResult<SharedProviderRelayDispatchResult>(Failed(
                SharedProviderFailureCategory.NotFound,
                "shared_provider_model_not_found",
                "The requested shared-provider model was not found.",
                "model"));
        }

        if (!support.Operations.Contains(request.Operation))
        {
            return ValueTask.FromResult<SharedProviderRelayDispatchResult>(Failed(
                SharedProviderFailureCategory.Conflict,
                "shared_provider_operation_mismatch",
                "The published model does not support this operation."));
        }

        var exact = policy.Normalize(request.Operation, request.PayloadUtf8, support);
        if (exact is SharedProviderRelayRequestPolicyResult.Rejected exactFailure)
        {
            return ValueTask.FromResult<SharedProviderRelayDispatchResult>(
                new SharedProviderRelayDispatchResult.Failed(exactFailure.Failure));
        }

        normalized = ((SharedProviderRelayRequestPolicyResult.Accepted)exact).Request;
        Accepted.Enqueue(normalized);
        Interlocked.Increment(ref dispatchCount);
        if (NextResult is { } configured)
        {
            NextResult = null;
            return ValueTask.FromResult(configured);
        }

        return ValueTask.FromResult<SharedProviderRelayDispatchResult>(Success(normalized));
    }

    public void Reset()
    {
        Requests.Clear();
        Accepted.Clear();
        NextResult = null;
        Volatile.Write(ref dispatchCount, 0);
    }

    private static SharedProviderRelaySupportDescriptor? ResolveSupport(
        SharedProviderRoutingModelId modelId)
        => modelId == SharedProviderRelayTestData.ChatModelId
            ? ChatSupport(supportsStructuredOutput: true)
            : modelId == SharedProviderRelayTestData.LimitedChatModelId
                ? ChatSupport(supportsStructuredOutput: false)
                : modelId == SharedProviderRelayTestData.ImageModelId
                    ? ImageSupport
                    : null;

    private static SharedProviderRelayDispatchResult Success(
        SharedProviderRelayNormalizedRequest request)
    {
        string body = request.Operation switch
        {
            SharedProviderRelayOperation.ChatCompletions =>
                $$"""{"id":"chatcmpl-test","object":"chat.completion","model":"{{request.RoutingModelId.Value}}","choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}]}""",
            SharedProviderRelayOperation.Responses =>
                $$"""{"id":"resp-test","object":"response","status":"completed","model":"{{request.RoutingModelId.Value}}","output":[]}""",
            SharedProviderRelayOperation.ImageGenerations =>
                $$"""{"created":1787533200,"data":[{"b64_json":"{{Convert.ToBase64String([1, 2, 3])}}"}]}""",
            _ => throw new ArgumentOutOfRangeException()
        };
        var usage = request.Operation == SharedProviderRelayOperation.ImageGenerations
            ? new SharedProviderRelayUsage(
                inputTokens: null,
                outputTokens: null,
                imageCount: 1,
                SharedProviderRelayUsageCompleteness.Complete)
            : SharedProviderRelayUsage.Unavailable;
        return new SharedProviderRelayDispatchResult.Buffered(
            Encoding.UTF8.GetBytes(body),
            "application/json",
            SharedProviderRelayResponseHeaders.Empty,
            usage);
    }

    private static SharedProviderRelayDispatchResult.Failed Failed(
        SharedProviderFailureCategory category,
        string code,
        string message,
        string? parameter = null)
        => new(new SharedProviderFailure(
            category,
            new SharedProviderFailureCode(code),
            message,
            parameter));

    private static SharedProviderRelaySupportDescriptor ChatSupport(bool supportsStructuredOutput)
        => new(
            new HashSet<SharedProviderRelayOperation>
            {
                SharedProviderRelayOperation.ChatCompletions,
                SharedProviderRelayOperation.Responses
            },
            SharedProviderStreamingMode.ServerSentEvents,
            supportsFunctionTools: true,
            supportsParallelFunctionTools: true,
            supportsStructuredOutput,
            supportsVisionInput: false,
            supportsBase64Images: false,
            maximumRequestBytes: 4 * 1024 * 1024,
            maximumOutputTokens: 4096,
            maximumImageCount: 1);

    private static SharedProviderRelaySupportDescriptor ImageSupport { get; } = new(
        new HashSet<SharedProviderRelayOperation> { SharedProviderRelayOperation.ImageGenerations },
        SharedProviderStreamingMode.None,
        supportsFunctionTools: false,
        supportsParallelFunctionTools: false,
        supportsStructuredOutput: false,
        supportsVisionInput: false,
        supportsBase64Images: true,
        maximumRequestBytes: 1024 * 1024,
        maximumOutputTokens: 1,
        maximumImageCount: 4);

    private static SharedProviderRelaySupportDescriptor LookupSupport { get; } = new(
        new HashSet<SharedProviderRelayOperation>
        {
            SharedProviderRelayOperation.ChatCompletions,
            SharedProviderRelayOperation.Responses,
            SharedProviderRelayOperation.ImageGenerations
        },
        SharedProviderStreamingMode.ServerSentEvents,
        supportsFunctionTools: true,
        supportsParallelFunctionTools: true,
        supportsStructuredOutput: true,
        supportsVisionInput: false,
        supportsBase64Images: true,
        SharedProviderRelaySupportDescriptor.MaximumAllowedRequestBytes,
        SharedProviderRelaySupportDescriptor.MaximumAllowedOutputTokens,
        SharedProviderRelaySupportDescriptor.MaximumAllowedImageCount);
}

internal sealed class DirectRelayFixture : IAsyncDisposable
{
    private readonly ServiceProvider provider;
    private readonly AsyncServiceScope scope;

    private DirectRelayFixture(
        RecordingUpstreamHandler handler,
        RecordingImageCapabilityRelay imageRelay,
        Uri baseUri)
    {
        Handler = handler;
        ImageRelay = imageRelay;
        BaseUri = baseUri;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISharedProviderImageCapabilityRelay>(imageRelay);
        services.AddSharedProviderHttpDescriptors();
        services.AddSingleton<
            IProviderInferenceRelayRuntime,
            DirectProviderInferenceRelayRuntime>();
        services.AddHttpClient("SharedProviderRelay")
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        provider = services.BuildServiceProvider();
        scope = provider.CreateAsyncScope();
        Dispatcher = scope.ServiceProvider.GetRequiredService<ISharedProviderRelayDispatcher>();
        Catalog = scope.ServiceProvider.GetRequiredService<ISharedProviderRelaySupportCatalog>();
        PublicationId = new SharedProviderPublicationId(
            Guid.Parse("d62ef6d5-f673-4656-b9f1-8e48722fd0a8"));
        ModelId = SharedProviderRoutingModelIdCodec.Create(
            PublicationId,
            SharedProviderRelayTestData.UpstreamModel);
    }

    public RecordingUpstreamHandler Handler { get; }

    public RecordingImageCapabilityRelay ImageRelay { get; }

    public Uri BaseUri { get; }

    public SharedProviderPublicationId PublicationId { get; }

    public SharedProviderRoutingModelId ModelId { get; }

    private ISharedProviderRelayDispatcher Dispatcher { get; }

    private ISharedProviderRelaySupportCatalog Catalog { get; }

    public static DirectRelayFixture Create(
        Uri? baseUri = null,
        RecordingImageCapabilityRelay? imageRelay = null,
        Func<CapturedUpstreamRequest, HttpResponseMessage>? responseFactory = null)
        => new(
            new RecordingUpstreamHandler(responseFactory),
            imageRelay ?? new RecordingImageCapabilityRelay(
                [new SharedProviderGeneratedImage("image/png", new byte[] { 1, 2, 3 }, null)]),
            baseUri ?? new Uri("https://upstream.example.test/v1"));

    public SharedProviderRelaySupportDescriptor GetSupport(
        string connectorPluginKey,
        SharedProviderPurpose purpose)
    {
        Assert.True(Catalog.TryGet(connectorPluginKey, purpose, out var descriptor));
        return descriptor.Support;
    }

    public async ValueTask<SharedProviderRelayDispatchResult> DispatchAsync(
        string connectorPluginKey,
        SharedProviderPurpose purpose,
        SharedProviderRelayOperation operation,
        string payload)
    {
        Assert.True(Catalog.TryGet(connectorPluginKey, purpose, out var descriptor));
        var normalized = new SharedProviderRelayRequestPolicy().Normalize(
            operation,
            Encoding.UTF8.GetBytes(payload),
            descriptor.Support);
        if (normalized is SharedProviderRelayRequestPolicyResult.Rejected rejected)
        {
            return new SharedProviderRelayDispatchResult.Failed(rejected.Failure);
        }

        var request = ((SharedProviderRelayRequestPolicyResult.Accepted)normalized).Request;
        var target = new SharedProviderRelayTarget(
            PublicationId,
            Guid.Parse("248b26d4-394d-4d62-ad2e-aa58fe2fd9bd"),
            connectorPluginKey,
            purpose,
            BaseUri,
            SharedProviderRelayTestData.UpstreamModel,
            ModelId,
            TimeSpan.FromSeconds(10),
            "{}",
            new SharedProviderRelayCredential("central-secret"),
            descriptor.Support);
        return await Dispatcher.DispatchAsync(
            new SharedProviderRelayDispatchRequest(target, request));
    }

    public async ValueTask DisposeAsync()
    {
        await scope.DisposeAsync();
        await provider.DisposeAsync();
    }
}

internal sealed class DirectProviderInferenceRelayRuntime :
    IProviderInferenceRelayRuntime,
    IDisposable
{
    private readonly HttpClient openAiClient = new();
    private readonly HttpClient ollamaClient = new();
    private readonly IAgentProviderFactory providerFactory;

    public DirectProviderInferenceRelayRuntime(
        IProviderInferenceRelayTransport transport)
    {
        providerFactory = new AgentProviderDriverRegistryBuilder()
            .AddOpenAiProviderDriver(
                openAiClient,
                new EnvironmentProviderDriverCredentialResolver(),
                inferenceRelayTransport: transport)
            .AddOllamaProviderDriver(ollamaClient, transport)
            .Build();
    }

    public Task<ProviderInferenceRelayTransportResponse> SendAsync(
        ProviderInferenceRelayRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return providerFactory
            .Resolve<IProviderInferenceRelayDriver>(request.Provider.Kind)
            .RelayAsync(request, cancellationToken);
    }

    public void Dispose()
    {
        openAiClient.Dispose();
        ollamaClient.Dispose();
    }
}

internal sealed record CapturedUpstreamRequest(
    Uri Uri,
    string Authorization,
    IReadOnlyDictionary<string, string[]> Headers,
    string Body);

internal sealed class RecordingUpstreamHandler(
    Func<CapturedUpstreamRequest, HttpResponseMessage>? responseFactory) : HttpMessageHandler
{
    public ConcurrentQueue<CapturedUpstreamRequest> Requests { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var captured = new CapturedUpstreamRequest(
            request.RequestUri ?? throw new InvalidOperationException("The upstream URI was absent."),
            request.Headers.Authorization?.ToString() ?? string.Empty,
            request.Headers.ToDictionary(
                header => header.Key,
                header => header.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase),
            request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
        Requests.Enqueue(captured);
        if (responseFactory is not null)
        {
            return responseFactory(captured);
        }

        string body = captured.Uri.AbsolutePath.EndsWith("/images/generations", StringComparison.Ordinal)
            ? """{"created":1787533200,"data":[{"b64_json":"AQID"}]}"""
            : captured.Uri.AbsolutePath.EndsWith("/responses", StringComparison.Ordinal)
                ? """{"id":"resp-upstream","object":"response","status":"completed","model":"upstream-model","output":[],"usage":{"input_tokens":1,"output_tokens":2}}"""
                : """{"id":"chatcmpl-upstream","object":"chat.completion","model":"upstream-model","choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":2}}""";
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        response.Headers.TryAddWithoutValidation("x-request-id", "upstream-request-id");
        return response;
    }
}

internal sealed class RecordingImageCapabilityRelay(
    IReadOnlyList<SharedProviderGeneratedImage> images) : ISharedProviderImageCapabilityRelay
{
    public ConcurrentQueue<SharedProviderImageCapabilityRequest> Requests { get; } = new();

    public ValueTask<IReadOnlyList<SharedProviderGeneratedImage>> GenerateAsync(
        SharedProviderImageCapabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Enqueue(request);
        return ValueTask.FromResult(images);
    }
}
