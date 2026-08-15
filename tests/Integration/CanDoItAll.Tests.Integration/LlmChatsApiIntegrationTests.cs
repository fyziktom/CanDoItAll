using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Ports;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.LlmChats;

public sealed class LlmChatsDefinitionApiIntegrationTests
{
    [Fact]
    public async Task DefinitionApi_ExposesSanitizedPerModelThinkingEffortAndRejectsDuplicateJsonEffort()
    {
        var services = new StubLlmChatDefinitionApplicationService();
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            configureServices: collection =>
            {
                collection.RemoveAll<ILlmChatDefinitionApplicationService>();
                collection.RemoveAll<ILlmChatConversationApplicationService>();
                collection.RemoveAll<ILlmChatProviderResolver>();
                collection.AddSingleton<ILlmChatDefinitionApplicationService>(services);
                collection.AddSingleton<ILlmChatConversationApplicationService, StubLlmChatConversationApplicationService>();
                collection.AddSingleton<ILlmChatProviderResolver, StubLlmChatProviderResolver>();
            },
            useInMemoryDatabase: true);

        using var optionsResponse = await host.Client.GetAsync("/api/llm-chats/provider-options");
        Assert.Equal(HttpStatusCode.OK, optionsResponse.StatusCode);
        using var options = JsonDocument.Parse(await optionsResponse.Content.ReadAsStringAsync());
        var models = options.RootElement[0].GetProperty("models");
        Assert.Equal(["none", "low", "high"], models[0]
            .GetProperty("thinkingEffort")
            .GetProperty("allowedEfforts")
            .EnumerateArray()
            .Select(item => item.GetString()));
        Assert.Equal(["medium"], models[1]
            .GetProperty("thinkingEffort")
            .GetProperty("allowedEfforts")
            .EnumerateArray()
            .Select(item => item.GetString()));
        var safeJson = options.RootElement.GetRawText();
        Assert.DoesNotContain("credential", safeJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint", safeJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("configuration", safeJson, StringComparison.OrdinalIgnoreCase);

        using var definitionResponse = await host.Client.GetAsync(
            $"/api/llm-chats/{StubLlmChatDefinitionApplicationService.DefinitionId.Value:D}");
        Assert.Equal(HttpStatusCode.OK, definitionResponse.StatusCode);
        var definitionJson = await definitionResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("systemPrompt", definitionJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Review carefully.", definitionJson, StringComparison.Ordinal);

        using var invalidResponse = await host.Client.PostAsJsonAsync("/api/llm-chats", new
        {
            name = "Architecture assistant",
            summary = "Review architecture.",
            avatarImageUrl = "https://example.invalid/avatar.png",
            systemPrompt = "Review carefully.",
            providerProfileId = StubLlmChatProviderResolver.ProviderId,
            model = "reasoning-model",
            thinkingEffort = "high",
            modelSettings = new
            {
                temperature = 0.2,
                modelParameterConfiguration = new
                {
                    modelParameters = new
                    {
                        reasoningEffort = "low"
                    }
                },
                timeoutSeconds = 60
            },
            tags = new[] { "architecture" },
            revisionReason = "Initial"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.False(services.CreateCalled);
        Assert.Contains(
            LlmChatErrorCodes.InvalidRequest,
            await invalidResponse.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        using var unknownMemberResponse = await host.Client.PostAsJsonAsync("/api/llm-chats", new
        {
            name = "Architecture assistant",
            summary = "Review architecture.",
            avatarImageUrl = "https://example.invalid/avatar.png",
            systemPrompt = "Review carefully.",
            providerProfileId = StubLlmChatProviderResolver.ProviderId,
            model = "reasoning-model",
            thinkingEffort = "high",
            revisionReason = "Initial",
            context = new[] { "not-supported" }
        });
        Assert.Equal(HttpStatusCode.BadRequest, unknownMemberResponse.StatusCode);

        using var openApiResponse = await host.Client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
        var openApi = await openApiResponse.Content.ReadAsStringAsync();
        Assert.Contains("ListLlmChatProviderOptions", openApi, StringComparison.Ordinal);
        Assert.Contains("thinkingEffort", openApi, StringComparison.Ordinal);
        Assert.Contains("providerDefault", openApi, StringComparison.Ordinal);
        using var openApiJson = JsonDocument.Parse(openApi);
        var schemas = openApiJson.RootElement
            .GetProperty("components")
            .GetProperty("schemas");
        Assert.True(schemas
            .GetProperty("LlmChatOperationApiResponse")
            .GetProperty("properties")
            .TryGetProperty("schema", out _));
        Assert.False(schemas.TryGetProperty("LlmChatOperation", out _));
        Assert.False(schemas.TryGetProperty("LlmChatConversation", out _));
        Assert.False(schemas.TryGetProperty("LlmChatDefinitionRevision", out _));
    }

    [Fact]
    public async Task DefinitionApi_WhenAuthorizationIsEnabled_InheritsApiGroupAuthorization()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: true,
            useInMemoryDatabase: true);

        using var response = await host.Client.GetAsync("/api/llm-chats");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public sealed class LlmChatsConversationApiIntegrationTests
{
    [Fact]
    public async Task ConversationApi_UsesBoundedPageAndExposesPinnedDefinitionRevision()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            configureServices: collection =>
            {
                collection.RemoveAll<ILlmChatDefinitionApplicationService>();
                collection.RemoveAll<ILlmChatConversationApplicationService>();
                collection.RemoveAll<ILlmChatProviderResolver>();
                collection.AddSingleton<ILlmChatDefinitionApplicationService, StubLlmChatDefinitionApplicationService>();
                collection.AddSingleton<ILlmChatConversationApplicationService, StubLlmChatConversationApplicationService>();
                collection.AddSingleton<ILlmChatProviderResolver, StubLlmChatProviderResolver>();
            },
            useInMemoryDatabase: true);

        using var createResponse = await host.Client.PostAsJsonAsync(
            $"/api/llm-chats/{StubLlmChatDefinitionApplicationService.DefinitionId.Value}/conversations",
            new
            {
                title = "Review Linux architecture"
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        Assert.Equal(3, created.RootElement.GetProperty("definitionRevision").GetInt32());
        Assert.Equal("Review Linux architecture", created.RootElement.GetProperty("title").GetString());
        Assert.NotNull(createResponse.Headers.ETag);

        using var listResponse = await host.Client.GetAsync("/api/llm-conversations?take=1");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var page = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, page.RootElement.GetProperty("items").GetArrayLength());
        Assert.True(page.RootElement.TryGetProperty("nextCursor", out _));

        var conversationId = created.RootElement.GetProperty("id").GetGuid();
        using var detailResponse = await host.Client.GetAsync(
            $"/api/llm-conversations/{conversationId}?messageTake=1");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        using var detail = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, detail.RootElement.GetProperty("messages").GetArrayLength());
        Assert.Equal("Review this design.", detail.RootElement
            .GetProperty("messages")[0]
            .GetProperty("content")
            .GetString());
    }

    [Fact]
    public async Task ConversationApi_OwnsApiOriginAndRejectsClientOriginSpoofing()
    {
        var conversations = new StubLlmChatConversationApplicationService();
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            configureServices: collection =>
            {
                collection.RemoveAll<ILlmChatConversationApplicationService>();
                collection.AddSingleton<ILlmChatConversationApplicationService>(conversations);
            },
            useInMemoryDatabase: true);
        var route =
            $"/api/llm-chats/{StubLlmChatDefinitionApplicationService.DefinitionId.Value:D}/conversations";

        using var spoofed = await host.Client.PostAsJsonAsync(route, new
        {
            title = "Spoofed origin",
            origin = "application"
        });

        Assert.Equal(HttpStatusCode.BadRequest, spoofed.StatusCode);
        Assert.Null(conversations.LastCreateCommand);

        using var created = await host.Client.PostAsJsonAsync(route, new
        {
            title = "Server-owned origin"
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(LlmChatConversationOrigin.Api, conversations.LastCreateCommand?.Origin);
    }
}

public sealed class LlmChatsSecurityApiIntegrationTests
{
    [Fact]
    public async Task AuthorizationEnabledHost_EnforcesDistinctScopesAndAuthenticatesSseOnlyThroughBearerHeader()
    {
        var operations = new StubLlmChatOperationApplicationService();
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: true,
            configureServices: collection =>
            {
                collection.RemoveAll<ILlmChatDefinitionApplicationService>();
                collection.RemoveAll<ILlmChatConversationApplicationService>();
                collection.RemoveAll<ILlmChatOperationApplicationService>();
                collection.AddSingleton<ILlmChatDefinitionApplicationService, StubLlmChatDefinitionApplicationService>();
                collection.AddSingleton<ILlmChatConversationApplicationService, StubLlmChatConversationApplicationService>();
                collection.AddSingleton<ILlmChatOperationApplicationService>(operations);
            },
            useInMemoryDatabase: true);
        var tokenService = host.App.Services.GetRequiredService<IApiTokenService>();
        var conversationRoute =
            $"/api/llm-chats/{StubLlmChatDefinitionApplicationService.DefinitionId.Value:D}/conversations";
        var turnRoute =
            $"/api/llm-conversations/{StubLlmChatOperationApplicationService.ConversationId.Value:D}/turns";
        var operationId = Guid.Parse("60000000-0000-0000-0000-000000000001");

        SetBearerToken(host, IssueToken(tokenService, ApiAccessScopeNames.Api));
        using var broadApiScope = await host.Client.GetAsync("/api/llm-chats");
        Assert.Equal(HttpStatusCode.Forbidden, broadApiScope.StatusCode);

        SetBearerToken(host, IssueToken(tokenService, ApiAccessScopeNames.ReadLlmChats));
        using var readDefinitions = await host.Client.GetAsync("/api/llm-chats");
        using var readCannotManage = await host.Client.PostAsJsonAsync(conversationRoute, new
        {
            title = "Denied manage"
        });
        using var readCannotExecute = await host.Client.PostAsJsonAsync(turnRoute, new
        {
            operationId,
            expectedTranscriptRevision = StubLlmChatOperationApplicationService.TranscriptRevision,
            message = "Denied execute"
        });
        Assert.Equal(HttpStatusCode.OK, readDefinitions.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, readCannotManage.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, readCannotExecute.StatusCode);

        var readToken = IssueToken(tokenService, ApiAccessScopeNames.ReadLlmChats);
        SetBearerToken(host, readToken);
        using var bearerSse = await host.Client.GetAsync(
            $"/api/llm-chat-operations/{Guid.NewGuid():D}/events");
        Assert.Equal(HttpStatusCode.NotFound, bearerSse.StatusCode);

        host.Client.DefaultRequestHeaders.Authorization = null;
        using var queryTokenSse = await host.Client.GetAsync(
            $"/api/llm-chat-operations/{Guid.NewGuid():D}/events?access_token={Uri.EscapeDataString(readToken.Token)}");
        Assert.Equal(HttpStatusCode.Unauthorized, queryTokenSse.StatusCode);

        SetBearerToken(host, IssueToken(tokenService, ApiAccessScopeNames.ManageLlmChats));
        using var managed = await host.Client.PostAsJsonAsync(conversationRoute, new
        {
            title = "Managed conversation"
        });
        using var manageCannotRead = await host.Client.GetAsync("/api/llm-chats");
        Assert.Equal(HttpStatusCode.Created, managed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, manageCannotRead.StatusCode);

        SetBearerToken(host, IssueToken(tokenService, ApiAccessScopeNames.ExecuteLlmChats));
        using var executed = await host.Client.PostAsJsonAsync(turnRoute, new
        {
            operationId,
            expectedTranscriptRevision = StubLlmChatOperationApplicationService.TranscriptRevision,
            message = "Execute turn"
        });
        using var executeCannotRead = await host.Client.GetAsync(
            $"/api/llm-chat-operations/{operationId:D}");
        using var cancelled = await host.Client.PostAsync(
            $"/api/llm-chat-operations/{operationId:D}/cancel",
            null);
        Assert.Equal(HttpStatusCode.Accepted, executed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, executeCannotRead.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
    }

    private static ApiTokenIssueResult IssueToken(
        IApiTokenService tokenService,
        string scope)
        => tokenService.IssueToken(new ApiTokenIssueRequest
        {
            Subject = $"llm-chat-{scope}",
            DisplayName = "LLM Chat API acceptance client",
            Scopes = [scope]
        });

    private static void SetBearerToken(
        ApiTestHost host,
        ApiTokenIssueResult token)
    {
        host.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(token.TokenType, token.Token);
    }
}

internal sealed class StubLlmChatDefinitionApplicationService : ILlmChatDefinitionApplicationService
{
    public static readonly LlmChatDefinitionId DefinitionId = new(new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 0, 0, 0, TimeSpan.Zero);

    public bool CreateCalled { get; private set; }

    public Task<Result<LlmChatDefinitionDetails>> CreateAsync(
        CreateLlmChatDefinitionCommand command,
        CancellationToken cancellationToken = default)
    {
        CreateCalled = true;
        return Task.FromResult(Result<LlmChatDefinitionDetails>.Success(CreateDetails(command.Settings)));
    }

    public Task<Result<LlmChatDefinitionDetails>> UpdateAsync(
        UpdateLlmChatDefinitionCommand command,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result<LlmChatDefinitionDetails>.Success(CreateDetails(command.Settings)));

    public Task<Result<LlmChatDefinitionDetails>> ChangeStatusAsync(
        ChangeLlmChatDefinitionStatusCommand command,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result<LlmChatDefinitionDetails>.Success(CreateDetails(
            new LlmModelSettings { ThinkingEffort = AgentReasoningEffortLevel.High })));

    public Task<Result<LlmChatDefinitionDetails>> GetAsync(
        LlmChatDefinitionId definitionId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result<LlmChatDefinitionDetails>.Success(CreateDetails(
            new LlmModelSettings { ThinkingEffort = AgentReasoningEffortLevel.High })));

    public Task<Result<IReadOnlyList<LlmChatDefinitionDetails>>> ListAsync(
        LlmChatDefinitionQuery query,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result<IReadOnlyList<LlmChatDefinitionDetails>>.Success(
            [CreateDetails(new LlmModelSettings { ThinkingEffort = AgentReasoningEffortLevel.High })]));

    public async Task<Result<LlmChatPage<LlmChatDefinitionDetails, LlmChatDefinitionCursor>>> ListPageAsync(
        LlmChatDefinitionQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = await ListAsync(query, cancellationToken);
        return Result<LlmChatPage<LlmChatDefinitionDetails, LlmChatDefinitionCursor>>.Success(
            new LlmChatPage<LlmChatDefinitionDetails, LlmChatDefinitionCursor>(result.Value!, null));
    }

    private static LlmChatDefinitionDetails CreateDetails(LlmModelSettings settings)
    {
        var revisionNumber = new LlmChatDefinitionRevisionNumber(3);
        var definition = new LlmChatDefinition(
            DefinitionId,
            "Architecture assistant",
            "Review architecture.",
            "https://example.invalid/avatar.png",
            LlmChatDefinitionStatus.Active,
            revisionNumber,
            Now,
            Now,
            4);
        var revision = new LlmChatDefinitionRevision(
            DefinitionId,
            revisionNumber,
            definition.Name,
            definition.Summary,
            definition.AvatarImageUrl,
            "Review carefully.",
            StubLlmChatProviderResolver.ProviderId,
            ProviderKind.OpenAi,
            "Private provider",
            "reasoning-model",
            settings,
            TimeSpan.FromMinutes(1),
            null,
            Now,
            "Initial");
        return new LlmChatDefinitionDetails(definition, revision);
    }
}

internal sealed class StubLlmChatConversationApplicationService : ILlmChatConversationApplicationService
{
    private static readonly LlmChatConversationId ConversationId = new(new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 0, 0, 0, TimeSpan.Zero);

    public CreateLlmChatConversationCommand? LastCreateCommand { get; private set; }

    public Task<Result<LlmChatConversationDetails>> CreateAsync(
        CreateLlmChatConversationCommand command,
        CancellationToken cancellationToken = default)
    {
        LastCreateCommand = command;
        return Task.FromResult(Result<LlmChatConversationDetails>.Success(CreateDetails(command.Title)));
    }

    public Task<Result<LlmChatConversationDetails>> RenameAsync(
        RenameLlmChatConversationCommand command,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result<LlmChatConversationDetails>.Success(CreateDetails(command.Title)));

    public Task<Result<LlmChatConversationDetails>> ArchiveAsync(
        ArchiveLlmChatConversationCommand command,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result<LlmChatConversationDetails>.Success(CreateDetails("Archived")));

    public Task<Result<LlmChatConversationDetails>> GetAsync(
        LlmChatConversationId conversationId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result<LlmChatConversationDetails>.Success(CreateDetails("Review Linux architecture")));

    public Task<Result<LlmChatConversationDetails>> GetAsync(
        LlmChatConversationId conversationId,
        LlmChatTranscriptQuery transcriptQuery,
        CancellationToken cancellationToken = default)
    {
        var details = CreateDetails("Review Linux architecture");
        return Task.FromResult(Result<LlmChatConversationDetails>.Success(details with
        {
            Messages =
            [
                new LlmChatTranscriptEntry(
                    new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                    new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                    LlmMessageRole.User,
                    "Review this design.",
                    Now,
                    string.Empty,
                    null)
            ]
        }));
    }

    public Task<Result<IReadOnlyList<LlmChatConversationDetails>>> ListAsync(
        LlmChatConversationQuery query,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result<IReadOnlyList<LlmChatConversationDetails>>.Success(
            [CreateDetails("Review Linux architecture")]));

    public async Task<Result<LlmChatPage<LlmChatConversationDetails, LlmChatConversationCursor>>> ListPageAsync(
        LlmChatConversationQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = await ListAsync(query, cancellationToken);
        return Result<LlmChatPage<LlmChatConversationDetails, LlmChatConversationCursor>>.Success(
            new LlmChatPage<LlmChatConversationDetails, LlmChatConversationCursor>(result.Value!, null));
    }

    private static LlmChatConversationDetails CreateDetails(string title)
    {
        var conversation = new LlmChatConversation(
            ConversationId,
            StubLlmChatDefinitionApplicationService.DefinitionId,
            new LlmChatDefinitionRevisionNumber(3),
            title,
            LlmChatConversationStatus.Active,
            LlmChatConversationOrigin.Api,
            Now,
            Now,
            2);
        return new LlmChatConversationDetails(
            conversation,
            "Architecture assistant",
            new LlmChatConversationEngineState(ConversationId, 5, false, Now, Now));
    }
}

internal sealed class StubLlmChatProviderResolver : ILlmChatProviderResolver
{
    public static readonly Guid ProviderId = new("cccccccc-cccc-cccc-cccc-cccccccccccc");

    public Task<Result<LlmChatResolvedProvider>> ResolveAsync(
        Guid providerProfileId,
        string model,
        AgentReasoningEffortLevel? thinkingEffort,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<Result<IReadOnlyList<LlmChatProviderOption>>> ListOptionsAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result<IReadOnlyList<LlmChatProviderOption>>.Success(
        [
            new LlmChatProviderOption(
                ProviderId,
                "Private provider",
                ProviderKind.OpenAi,
                [
                    new LlmChatModelOption(
                        "reasoning-model",
                        new LlmChatThinkingEffortOption(
                            AgentThinkingEffortSupportStatus.Supported,
                            AgentThinkingEffortControlMode.EffortLevels,
                            [
                                AgentReasoningEffortLevel.None,
                                AgentReasoningEffortLevel.Low,
                                AgentReasoningEffortLevel.High
                            ],
                            AgentReasoningEffortLevel.Low)),
                    new LlmChatModelOption(
                        "fixed-model",
                        new LlmChatThinkingEffortOption(
                            AgentThinkingEffortSupportStatus.Supported,
                            AgentThinkingEffortControlMode.EffortLevels,
                            [AgentReasoningEffortLevel.Medium],
                            AgentReasoningEffortLevel.Medium))
                ])
        ]));
}
