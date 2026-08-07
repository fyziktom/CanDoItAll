using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Tests.Integration;

public sealed class AgentApiFailureContractIntegrationTests
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(
        JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter<AgentProviderFailureCategory>(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false)
        }
    };

    [Fact]
    public async Task Chat_runtime_failure_returns_typed_identity_and_persists_the_exact_failed_run()
    {
        var runtime = new FailingAgentRuntime();
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true,
            agentRuntimeOverride: runtime);
        Assert.Same(
            runtime,
            host.App.Services.GetRequiredService<IFakeAgentRuntime>());

        Guid agentId;
        Guid chatSessionId;
        await using (var seedScope = host.App.Services.CreateAsyncScope())
        {
            var workspaceService = seedScope.ServiceProvider
                .GetRequiredService<IAgentFrameworkWorkspaceService>();
            var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
                .First(candidate => candidate.ProviderProfileId.HasValue);
            var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);
            agentId = agent.Id;
            chatSessionId = session.Id;
        }

        using var response = await host.Client.PostAsJsonAsync(
            $"/api/agents/{agentId:D}/chat",
            new AgentChatApiRequest(
                chatSessionId,
                "Inspect the current project structure."));

        Assert.Equal(1, runtime.RunInvocationCount);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var failure = JsonSerializer.Deserialize<ApiErrorResponse>(
            raw,
            ApiJsonOptions)!;
        Assert.Equal(ApiEndpointResults.RunFailedCode, Assert.Single(failure.Errors).Code);
        Assert.False(string.IsNullOrWhiteSpace(failure.CorrelationId));
        Assert.Equal(agentId, failure.AgentId);
        Assert.Equal(chatSessionId, failure.ChatSessionId);
        Assert.Null(failure.ProviderFailureCategory);
        var executionRunId = Assert.IsType<Guid>(failure.ExecutionRunId);
        Assert.DoesNotContain(FailingAgentRuntime.Secret, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(FailingAgentRuntime), raw, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", raw, StringComparison.Ordinal);

        await using var verificationScope = host.App.Services.CreateAsyncScope();
        var verificationService = verificationScope.ServiceProvider
            .GetRequiredService<IAgentFrameworkWorkspaceService>();
        var detail = await verificationService.GetExecutionRunDetailAsync(executionRunId);

        Assert.Equal(agentId, detail.Run.AgentId);
        Assert.Equal(chatSessionId, detail.Run.ChatSessionId);
        Assert.Equal(ExecutionState.Failed, detail.Run.State);
        Assert.Equal(RunOutcome.Failed, detail.Run.Outcome);
        Assert.DoesNotContain(FailingAgentRuntime.Secret, detail.Run.ResultSummary, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(FailingAgentRuntime), detail.Run.ResultSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_stream_runtime_failure_emits_typed_sanitized_terminal_event()
    {
        var runtime = new FailingAgentRuntime();
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true,
            agentRuntimeOverride: runtime);
        Assert.Same(
            runtime,
            host.App.Services.GetRequiredService<IFakeAgentRuntime>());

        Guid agentId;
        Guid chatSessionId;
        await using (var seedScope = host.App.Services.CreateAsyncScope())
        {
            var workspaceService = seedScope.ServiceProvider
                .GetRequiredService<IAgentFrameworkWorkspaceService>();
            var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
                .First(candidate => candidate.ProviderProfileId.HasValue);
            var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);
            agentId = agent.Id;
            chatSessionId = session.Id;
        }

        using var response = await host.Client.PostAsJsonAsync(
            $"/api/agents/{agentId:D}/chat/stream",
            new AgentChatApiRequest(
                chatSessionId,
                "Inspect the current project structure."));

        Assert.Equal(1, runtime.RunInvocationCount);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var failedFrame = Assert.Single(
            SplitServerSentEventFrames(raw),
            frame => frame.Contains(
                $"event: {AgentServerEventNames.CommandFailed}",
                StringComparison.Ordinal));
        var failure = JsonSerializer.Deserialize<AgentCommandFailed>(
            Assert.Single(
                failedFrame.Split('\n'),
                line => line.StartsWith("data: ", StringComparison.Ordinal))[6..],
            ApiJsonOptions)!;

        Assert.Equal(ApiEndpointResults.RunFailedCode, failure.Code);
        Assert.False(string.IsNullOrWhiteSpace(failure.CorrelationId));
        Assert.Equal(agentId, failure.AgentId);
        Assert.Equal(chatSessionId, failure.ChatSessionId);
        Assert.Null(failure.ProviderFailureCategory);
        var executionRunId = Assert.IsType<Guid>(failure.ExecutionRunId);
        Assert.DoesNotContain(FailingAgentRuntime.Secret, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(FailingAgentRuntime), raw, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", raw, StringComparison.Ordinal);

        await using var verificationScope = host.App.Services.CreateAsyncScope();
        var verificationService = verificationScope.ServiceProvider
            .GetRequiredService<IAgentFrameworkWorkspaceService>();
        var detail = await verificationService.GetExecutionRunDetailAsync(executionRunId);
        Assert.Equal(ExecutionState.Failed, detail.Run.State);
        Assert.Equal(agentId, detail.Run.AgentId);
        Assert.Equal(chatSessionId, detail.Run.ChatSessionId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Provider_configuration_failure_is_stable_in_sync_and_stream_contracts(
        bool stream)
    {
        var runtime = new FailingAgentRuntime(
            AgentRuntimeFailureOrigin.ProviderConfiguration);
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true,
            agentRuntimeOverride: runtime);
        Assert.Same(
            runtime,
            host.App.Services.GetRequiredService<IFakeAgentRuntime>());

        Guid agentId;
        Guid chatSessionId;
        await using (var seedScope = host.App.Services.CreateAsyncScope())
        {
            var workspaceService = seedScope.ServiceProvider
                .GetRequiredService<IAgentFrameworkWorkspaceService>();
            var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
                .First(candidate => candidate.ProviderProfileId.HasValue);
            var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);
            agentId = agent.Id;
            chatSessionId = session.Id;
        }

        var path = stream
            ? $"/api/agents/{agentId:D}/chat/stream"
            : $"/api/agents/{agentId:D}/chat";
        using var response = await host.Client.PostAsJsonAsync(
            path,
            new AgentChatApiRequest(
                chatSessionId,
                "Inspect the current project structure."));
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(1, runtime.RunInvocationCount);
        Guid executionRunId;
        if (stream)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var failedFrame = Assert.Single(
                SplitServerSentEventFrames(raw),
                frame => frame.Contains(
                    $"event: {AgentServerEventNames.CommandFailed}",
                    StringComparison.Ordinal));
            var failure = JsonSerializer.Deserialize<AgentCommandFailed>(
                Assert.Single(
                    failedFrame.Split('\n'),
                    line => line.StartsWith("data: ", StringComparison.Ordinal))[6..],
                ApiJsonOptions)!;

            Assert.Equal(
                ApiEndpointResults.ProviderConfigurationInvalidCode,
                failure.Code);
            Assert.False(string.IsNullOrWhiteSpace(failure.CorrelationId));
            Assert.Equal(agentId, failure.AgentId);
            Assert.Equal(chatSessionId, failure.ChatSessionId);
            Assert.Equal(
                AgentProviderFailureCategory.ProviderConfiguration,
                failure.ProviderFailureCategory);
            executionRunId = Assert.IsType<Guid>(failure.ExecutionRunId);
        }
        else
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            var failure = JsonSerializer.Deserialize<ApiErrorResponse>(
                raw,
                ApiJsonOptions)!;

            Assert.Equal(
                ApiEndpointResults.ProviderConfigurationInvalidCode,
                Assert.Single(failure.Errors).Code);
            Assert.False(string.IsNullOrWhiteSpace(failure.CorrelationId));
            Assert.Equal(agentId, failure.AgentId);
            Assert.Equal(chatSessionId, failure.ChatSessionId);
            Assert.Equal(
                AgentProviderFailureCategory.ProviderConfiguration,
                failure.ProviderFailureCategory);
            executionRunId = Assert.IsType<Guid>(failure.ExecutionRunId);
        }

        Assert.Contains(
            "\"providerFailureCategory\":\"providerConfiguration\"",
            raw,
            StringComparison.Ordinal);
        Assert.DoesNotContain(FailingAgentRuntime.Secret, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(FailingAgentRuntime), raw, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", raw, StringComparison.Ordinal);

        await using var verificationScope = host.App.Services.CreateAsyncScope();
        var verificationService = verificationScope.ServiceProvider
            .GetRequiredService<IAgentFrameworkWorkspaceService>();
        var detail = await verificationService.GetExecutionRunDetailAsync(executionRunId);
        Assert.Equal(ExecutionState.Failed, detail.Run.State);
        Assert.Equal(agentId, detail.Run.AgentId);
        Assert.Equal(chatSessionId, detail.Run.ChatSessionId);
        Assert.Contains("not ready", detail.Run.ResultSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            FailingAgentRuntime.Secret,
            detail.Run.ResultSummary,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_stream_validation_is_correlated_before_sse_starts()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);
        var agentId = Guid.NewGuid();

        using var response = await host.Client.PostAsJsonAsync(
            $"/api/agents/{agentId:D}/chat/stream",
            new AgentChatApiRequest(null, "  "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var failure = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(failure);
        Assert.Equal(
            AgentApiRequestValidation.InvalidRequestCode,
            Assert.Single(failure.Errors).Code);
        Assert.False(string.IsNullOrWhiteSpace(failure.CorrelationId));
        Assert.Equal(agentId, failure.AgentId);
        Assert.Null(failure.ExecutionRunId);
        Assert.Null(failure.ProviderFailureCategory);
    }

    [Fact]
    public async Task Agent_catalog_defaults_and_provider_save_validation_are_typed()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);

        using var omittedResponse = await host.Client.GetAsync("/api/agents");
        using var explicitResponse = await host.Client.GetAsync("/api/agents?includeTemplates=false");

        Assert.Equal(HttpStatusCode.OK, omittedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, explicitResponse.StatusCode);
        using var omitted = JsonDocument.Parse(await omittedResponse.Content.ReadAsStringAsync());
        using var explicitFalse = JsonDocument.Parse(await explicitResponse.Content.ReadAsStringAsync());
        Assert.True(JsonElement.DeepEquals(omitted.RootElement, explicitFalse.RootElement));

        var invalidProvider = CreateOtherwiseValidProviderEditor();
        invalidProvider.ModelThinkingEffortCapabilities =
        [
            new ProviderModelThinkingEffortCapability(
                Model: string.Empty,
                AgentThinkingEffortSupportStatus.Unknown,
                AgentThinkingEffortCapabilitySource.Defined,
                AllowedEfforts: [])
        ];
        using var invalidProviderResponse = await host.Client.PostAsJsonAsync(
            "/api/agents/providers",
            invalidProvider);

        Assert.Equal(HttpStatusCode.BadRequest, invalidProviderResponse.StatusCode);
        var rawProviderError = await invalidProviderResponse.Content.ReadAsStringAsync();
        var providerError = JsonSerializer.Deserialize<ApiErrorResponse>(
            rawProviderError,
            ApiJsonOptions)!;
        Assert.Equal(
            ApiEndpointResults.ProviderRequestInvalidCode,
            Assert.Single(providerError.Errors).Code);
        Assert.False(string.IsNullOrWhiteSpace(providerError.CorrelationId));
        Assert.Null(providerError.ExecutionRunId);
        Assert.Null(providerError.ChatSessionId);
        Assert.DoesNotContain("InvalidOperationException", rawProviderError, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", rawProviderError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provider_save_known_metadata_failures_are_typed_request_validation()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);

        var retainedSecretId = Guid.NewGuid();
        var retainedSecretProvider = CreateOtherwiseValidProviderEditor();
        retainedSecretProvider.ApiKeyEnvironmentVariable =
            $"secret:{retainedSecretId:D}";
        retainedSecretProvider.ConfigurationJson =
            """{"timeoutSeconds":5}""";
        using var retainedSecretResponse = await host.Client.PostAsJsonAsync(
            "/api/agents/providers",
            retainedSecretProvider);
        var retainedSecretResponseBody =
            await retainedSecretResponse.Content.ReadAsStringAsync();
        Assert.True(
            retainedSecretResponse.StatusCode == HttpStatusCode.OK,
            $"Provider validation setup returned {(int)retainedSecretResponse.StatusCode}: {retainedSecretResponseBody}");
        var retainedSecretProviderId = JsonSerializer.Deserialize<Guid>(
            retainedSecretResponseBody,
            ApiJsonOptions);

        (string Name, Action<ProviderProfileEditorModel> Configure)[] scenarios =
        [
            ("empty provider id", editor => editor.Id = Guid.Empty),
            ("blank provider name", editor => editor.Name = " "),
            ("blank provider base URL", editor => editor.BaseUrl = " "),
            ("relative provider base URL", editor =>
                editor.BaseUrl = "api.openai.com/v1"),
            ("provider base URL user information", editor =>
                editor.BaseUrl = "https://user@api.openai.com/v1"),
            ("provider base URL query", editor =>
                editor.BaseUrl = "https://api.openai.com/v1?tenant=unsafe"),
            ("provider base URL fragment", editor =>
                editor.BaseUrl = "https://api.openai.com/v1#unsafe"),
            ("non-http real-provider base URL", editor =>
                editor.BaseUrl = "ftp://api.openai.com/v1"),
            ("malformed configuration", editor => editor.ConfigurationJson = "{"),
            ("non-object configuration", editor => editor.ConfigurationJson = "[]"),
            ("duplicate connector aliases", editor =>
                editor.ConfigurationJson =
                    "{\"connectorPluginKey\":\"provider.openai\",\"ConnectorPluginKey\":\"provider.openai\"}"),
            ("duplicate schema aliases", editor =>
                editor.ConfigurationJson =
                    "{\"configSchemaVersion\":\"1\",\"ConfigSchemaVersion\":\"1\"}"),
            ("duplicate secret aliases", editor =>
                editor.ConfigurationJson =
                    "{\"secretRecordId\":\"72d10cca-63f7-4f6f-8055-938c2df2c170\",\"SecretRecordId\":\"72d10cca-63f7-4f6f-8055-938c2df2c170\"}"),
            ("duplicate timeout aliases", editor =>
                editor.ConfigurationJson =
                    "{\"timeoutSeconds\":45,\"TimeoutSeconds\":45}"),
            ("duplicate provider-kind aliases", editor =>
                editor.ConfigurationJson =
                    "{\"agentFrameworkProviderKind\":\"OpenAi\",\"AgentFrameworkProviderKind\":\"OpenAi\"}"),
            ("duplicate transport aliases", editor =>
                editor.ConfigurationJson =
                    "{\"providerTransport\":\"Responses\",\"ProviderTransport\":\"Responses\"}"),
            ("duplicate purpose aliases", editor =>
                editor.ConfigurationJson =
                    "{\"providerPurpose\":\"Chat\",\"ProviderPurpose\":\"Chat\"}"),
            ("duplicate tag aliases", editor =>
                editor.ConfigurationJson =
                    "{\"tags\":[\"planning\"],\"Tags\":[\"planning\"]}"),
            ("malformed connector key", editor =>
                editor.ConfigurationJson = """{"connectorPluginKey":42}"""),
            ("unknown connector key", editor =>
                editor.ConfigurationJson =
                    """{"connectorPluginKey":"missing.connector"}"""),
            ("malformed thinking capabilities", editor =>
                editor.ConfigurationJson =
                    """{"modelThinkingEffortCapabilities":42}"""),
            ("null thinking capabilities", editor =>
                editor.ConfigurationJson =
                    """{"modelThinkingEffortCapabilities":null}"""),
            ("null thinking capability item", editor =>
                editor.ConfigurationJson =
                    """{"modelThinkingEffortCapabilities":[null]}"""),
            ("unsupported provider kind", editor =>
                editor.Kind = (ProviderKind)int.MaxValue),
            ("wrong-type schema version", editor =>
                editor.ConfigurationJson = """{"configSchemaVersion":1}"""),
            ("blank schema version", editor =>
                editor.ConfigurationJson =
                    """{"configSchemaVersion":" "}"""),
            ("unsupported schema version", editor =>
                editor.ConfigurationJson =
                    """{"configSchemaVersion":"999"}"""),
            ("wrong-type timeout", editor =>
                editor.ConfigurationJson =
                    """{"timeoutSeconds":"45"}"""),
            ("out-of-range timeout", editor =>
                editor.ConfigurationJson = """{"timeoutSeconds":4}"""),
            ("invalid configured secret id", editor =>
            {
                editor.Id = retainedSecretProviderId;
                editor.ConfigurationJson =
                    """{"secretRecordId":"not-a-guid"}""";
            }),
            ("empty configured secret id", editor =>
            {
                editor.Id = retainedSecretProviderId;
                editor.ConfigurationJson =
                    """{"secretRecordId":"00000000-0000-0000-0000-000000000000"}""";
            }),
            ("conflicting explicit secret ids", editor =>
                editor.ConfigurationJson =
                    """{"secretRecordId":"16e5ada3-32a8-411b-9719-d24bfab0dd47"}"""),
            ("invalid inline secret reference", editor =>
            {
                editor.Id = retainedSecretProviderId;
                editor.ApiKeyEnvironmentVariable = "secret:not-a-guid";
            }),
            ("empty inline secret reference", editor =>
            {
                editor.Id = retainedSecretProviderId;
                editor.ApiKeyEnvironmentVariable =
                    "secret:00000000-0000-0000-0000-000000000000";
            }),
            ("ambient environment-variable credential", editor =>
                editor.ApiKeyEnvironmentVariable = "OPENAI_API_KEY"),
            ("unsupported transport", editor =>
                editor.Transport = (ProviderTransportKind)int.MaxValue),
            ("unsupported purpose", editor =>
                editor.Purpose = (ProviderProfilePurpose)int.MaxValue),
            ("incompatible Ollama transport", editor =>
            {
                editor.Kind = ProviderKind.Ollama;
                editor.Transport = ProviderTransportKind.Responses;
            })
        ];

        foreach (var scenario in scenarios)
        {
            var editor = CreateOtherwiseValidProviderEditor();
            scenario.Configure(editor);

            using var response = await host.Client.PostAsJsonAsync(
                "/api/agents/providers",
                editor);

            var raw = await response.Content.ReadAsStringAsync();
            Assert.True(
                response.StatusCode == HttpStatusCode.BadRequest,
                $"Provider validation scenario '{scenario.Name}' returned {(int)response.StatusCode}: {raw}");
            var failure = JsonSerializer.Deserialize<ApiErrorResponse>(
                raw,
                ApiJsonOptions)!;
            Assert.Equal(
                ApiEndpointResults.ProviderRequestInvalidCode,
                Assert.Single(failure.Errors).Code);
            Assert.False(string.IsNullOrWhiteSpace(failure.CorrelationId));
            Assert.Null(failure.AgentId);
            Assert.Null(failure.ExecutionRunId);
            Assert.Null(failure.ChatSessionId);
            Assert.Null(failure.ProviderFailureCategory);
            Assert.DoesNotContain(
                nameof(ProviderProfileValidationException),
                raw,
                StringComparison.Ordinal);
            Assert.DoesNotContain(nameof(InvalidOperationException), raw, StringComparison.Ordinal);
            Assert.DoesNotContain(nameof(JsonException), raw, StringComparison.Ordinal);
            Assert.DoesNotContain(" at ", raw, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Provider_save_storage_failure_is_not_misclassified_as_request_validation()
    {
        var workspaceService = DispatchProxy.Create<
            IAgentFrameworkWorkspaceService,
            ProviderSaveStorageFailureProxy>();
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            configureServices: services =>
            {
                services.RemoveAll<IAgentFrameworkWorkspaceService>();
                services.AddSingleton(workspaceService);
            },
            useInMemoryDatabase: true,
            environmentName: Environments.Production);

        using var response = await host.Client.PostAsJsonAsync(
            "/api/agents/providers",
            new ProviderProfileEditorModel
            {
                Name = "Storage failure probe"
            });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(
            ProviderSaveStorageFailureProxy.Secret,
            raw,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ApiEndpointResults.ProviderRequestInvalidCode,
            raw,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(
        AgentProviderFailureCategory.RequestCompatibility,
        StatusCodes.Status400BadRequest,
        ApiEndpointResults.ProviderRequestIncompatibleCode)]
    [InlineData(
        AgentProviderFailureCategory.QuotaOrBilling,
        StatusCodes.Status503ServiceUnavailable,
        ApiEndpointResults.ProviderQuotaUnavailableCode)]
    [InlineData(
        AgentProviderFailureCategory.RateLimit,
        StatusCodes.Status503ServiceUnavailable,
        ApiEndpointResults.ProviderRateLimitedCode)]
    [InlineData(
        AgentProviderFailureCategory.ProviderError,
        StatusCodes.Status503ServiceUnavailable,
        ApiEndpointResults.ProviderFailedCode)]
    [InlineData(
        AgentProviderFailureCategory.ProviderConfiguration,
        StatusCodes.Status422UnprocessableEntity,
        ApiEndpointResults.ProviderConfigurationInvalidCode)]
    public async Task Provider_failure_result_is_typed_correlated_and_sanitized(
        AgentProviderFailureCategory category,
        int expectedStatusCode,
        string expectedCode)
    {
        const string displaySecret = "api-display-secret";
        var agentId = Guid.NewGuid();
        var executionRunId = Guid.NewGuid();
        var chatSessionId = Guid.NewGuid();
        var context = CreateHttpContext("agent-api-correlation");
        var exception = new AgentChatRunFailedException(
            agentId,
            executionRunId,
            chatSessionId,
            "OpenAI default",
            "gpt-5.4-mini",
            new InvalidOperationException("api_key=provider-secret\n   at Provider.SendAsync()"),
            $"The configured provider rejected credential={displaySecret}.",
            category);

        await ApiEndpointResults.AgentRunFailure(context, exception).ExecuteAsync(context);

        Assert.Equal(expectedStatusCode, context.Response.StatusCode);
        var raw = await ReadResponseBodyAsync(context);
        var response = JsonSerializer.Deserialize<ApiErrorResponse>(
            raw,
            ApiJsonOptions)!;
        var error = Assert.Single(response.Errors);
        Assert.Equal(expectedCode, error.Code);
        Assert.Contains("[REDACTED]", error.Message, StringComparison.Ordinal);
        Assert.Equal("agent-api-correlation", response.CorrelationId);
        Assert.Equal(agentId, response.AgentId);
        Assert.Equal(executionRunId, response.ExecutionRunId);
        Assert.Equal(chatSessionId, response.ChatSessionId);
        Assert.Equal(category, response.ProviderFailureCategory);
        Assert.Contains(
            $"\"providerFailureCategory\":\"{JsonNamingPolicy.CamelCase.ConvertName(category.ToString())}\"",
            raw,
            StringComparison.Ordinal);
        Assert.DoesNotContain(displaySecret, raw, StringComparison.Ordinal);
        Assert.DoesNotContain("provider-secret", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("Provider.SendAsync", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Agent_validation_result_includes_correlation_without_fake_run_identity()
    {
        var agentId = Guid.NewGuid();
        var context = CreateHttpContext("agent-validation-correlation");
        var result = AgentApiRequestValidation.ValidateCommand(
            context,
            agentId,
            chatSessionId: null,
            prompt: "  ");

        await Assert.IsAssignableFrom<IResult>(result).ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        var response = JsonSerializer.Deserialize<ApiErrorResponse>(
            await ReadResponseBodyAsync(context),
            ApiJsonOptions)!;
        Assert.Equal("agent-validation-correlation", response.CorrelationId);
        Assert.Equal(agentId, response.AgentId);
        Assert.Null(response.ExecutionRunId);
        Assert.Null(response.ChatSessionId);
        Assert.Null(response.ProviderFailureCategory);
        Assert.Equal(
            AgentApiRequestValidation.InvalidRequestCode,
            Assert.Single(response.Errors).Code);
    }

    [Fact]
    public void Agent_command_failure_response_preserves_known_request_identity()
    {
        var agentId = Guid.NewGuid();
        var executionRunId = Guid.NewGuid();
        var chatSessionId = Guid.NewGuid();
        var context = CreateHttpContext("agent-command-correlation");

        var response = ApiEndpointResults.AgentCommandFailureResponse(
            context,
            agentId,
            executionRunId,
            chatSessionId);

        Assert.Equal("agent-command-correlation", response.CorrelationId);
        Assert.Equal(agentId, response.AgentId);
        Assert.Equal(executionRunId, response.ExecutionRunId);
        Assert.Equal(chatSessionId, response.ChatSessionId);
        Assert.Null(response.ProviderFailureCategory);
        Assert.Equal(
            ApiEndpointResults.CommandFailedCode,
            Assert.Single(response.Errors).Code);
    }

    private static DefaultHttpContext CreateHttpContext(string correlationId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddRouting();
        services.ConfigureAgentApiJson();
        return new DefaultHttpContext
        {
            TraceIdentifier = correlationId,
            RequestServices = services.BuildServiceProvider(),
            Response =
            {
                Body = new MemoryStream()
            }
        };
    }

    private static ProviderProfileEditorModel CreateOtherwiseValidProviderEditor()
    {
        return new ProviderProfileEditorModel
        {
            Name = "Provider validation probe",
            Kind = ProviderKind.OpenAi,
            BaseUrl = "https://api.openai.com/v1",
            ApiKeyEnvironmentVariable =
                "secret:970f8eb8-3596-4113-9c4b-5fd921dd4389",
            DefaultModel = "gpt-5",
            ConfigurationJson = "{}"
        };
    }

    private static async Task<string> ReadResponseBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static string[] SplitServerSentEventFrames(string body)
    {
        return body.Split(
            "\n\n",
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
    }

    private sealed class FailingAgentRuntime : IFakeAgentRuntime
    {
        public const string Secret = "http-provider-secret";

        private readonly AgentRuntimeFailureOrigin? failureOrigin;
        private int runInvocationCount;

        public FailingAgentRuntime()
        {
        }

        public FailingAgentRuntime(AgentRuntimeFailureOrigin failureOrigin)
        {
            this.failureOrigin = failureOrigin;
        }

        public int RunInvocationCount => Volatile.Read(ref runInvocationCount);

        public Task<ProviderHealthResult> TestProviderAsync(
            ProviderProfile provider,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ProviderHealthResult(true, "ok", []));

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ProviderTestChatResult(provider.DefaultModel, "ok", 1, 1));

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
            ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ProviderModelMaintenanceEditorResult(
                request.TargetModel,
                request.BaseModel,
                request.SystemPrompt,
                request.ContextLength,
                string.Empty,
                "ok"));

        public async Task<AgentRuntimeResponse> RunAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            string prompt,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
        {
            await Task.Yield();
            Interlocked.Increment(ref runInvocationCount);
            if (failureOrigin is { } origin)
            {
                throw new AgentRuntimeUsageException(
                    $"Provider configuration failed with api_key={Secret}.",
                    new InvalidOperationException(
                        $"api_key={Secret}\n   at Provider.Configure()"),
                    [],
                    failureOrigin: origin,
                    providerFailureIdentity: new AgentRuntimeProviderFailureIdentity(
                        provider.Id,
                        provider.Name,
                        provider.Kind,
                        provider.Transport,
                        agent.Model));
            }

            throw new InvalidOperationException($"api_key={Secret}");
        }

        public Task<AgentRuntimeResponse> RespondToPendingApprovalsAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            bool approved,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
            => throw new NotSupportedException(
                "Pending approval continuation is not used by this API contract test.");
    }

    private class ProviderSaveStorageFailureProxy : DispatchProxy
    {
        public const string Secret = "storage-provider-secret";

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IAgentFrameworkWorkspaceService.SaveProviderAsync))
            {
                return Task.FromException<Guid>(
                    new InvalidOperationException(Secret));
            }

            throw new InvalidOperationException(
                $"Workspace service member '{targetMethod?.Name}' was not expected in this API test.");
        }
    }
}
