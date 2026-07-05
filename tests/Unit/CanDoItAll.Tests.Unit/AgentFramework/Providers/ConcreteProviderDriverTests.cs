using System.Net;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.Tests.Unit.AgentFramework.Providers;

public sealed class ConcreteProviderDriverTests
{
    [Fact]
    public void ConcreteDrivers_RegisterThroughProviderBuilder()
    {
        using var httpClient = new HttpClient(new CapturingHandler((request, body) => JsonResponse("{}")));
        var resolver = new FixedCredentialResolver("test-key");

        var factory = new AgentProviderDriverRegistryBuilder()
            .AddOpenAiProviderDriver(httpClient, resolver)
            .AddAzureOpenAiProviderDriver(httpClient, resolver)
            .AddOllamaProviderDriver(httpClient)
            .AddComfyUiProviderDriver(httpClient)
            .Build();

        Assert.True(factory.Supports(ProviderKind.OpenAi, AgentProviderCapabilityKind.ImageGeneration));
        Assert.True(factory.Supports(ProviderKind.OpenAi, AgentProviderCapabilityKind.SpeechToText));
        Assert.True(factory.Supports(ProviderKind.AzureOpenAi, AgentProviderCapabilityKind.ChatCompletion));
        Assert.True(factory.Supports(ProviderKind.Ollama, AgentProviderCapabilityKind.ModelMaintenance));
        Assert.True(factory.Supports(ProviderKind.ComfyUi, AgentProviderCapabilityKind.ImageGeneration));
        Assert.False(factory.Supports(ProviderKind.ComfyUi, AgentProviderCapabilityKind.ChatCompletion));
        Assert.False(factory.Supports(ProviderKind.Ollama, AgentProviderCapabilityKind.ImageGeneration));

        var exception = Assert.Throws<UnsupportedProviderCapabilityException>(
            () => factory.Resolve<IProviderImageGenerationDriver>(ProviderKind.Ollama));
        Assert.Equal(ProviderKind.Ollama, exception.ProviderKind);
        Assert.Equal(AgentProviderCapabilityKind.ImageGeneration, exception.Capability);
    }

    [Fact]
    public async Task OpenAiProviderDriver_BuildsModelChatImageAndVoiceRequests()
    {
        var imageBytes = new byte[] { 1, 2, 3 };
        var handler = new CapturingHandler((request, body) =>
        {
            return request.RequestUri!.AbsolutePath switch
            {
                "/v1/models" => JsonResponse("""{"data":[{"id":"gpt-test"}]}"""),
                "/v1/chat/completions" => JsonResponse("""{"choices":[{"message":{"content":"chat response"}}],"usage":{"prompt_tokens":3,"completion_tokens":4}}"""),
                "/v1/images/generations" => JsonResponse($$"""{"data":[{"b64_json":"{{Convert.ToBase64String(imageBytes)}}","revised_prompt":"revised"}]}"""),
                "/v1/audio/speech" => BinaryResponse(new byte[] { 9, 8, 7 }, "audio/mpeg"),
                _ => JsonResponse("""{"error":{"message":"unexpected endpoint"}}""", HttpStatusCode.NotFound)
            };
        });
        using var httpClient = new HttpClient(handler);
        var driver = new OpenAiProviderDriver(httpClient, new FixedCredentialResolver("openai-key"));
        var provider = CreateProvider(ProviderKind.OpenAi, "https://api.openai.test/v1", "gpt-test");
        var imageProvider = CreateProvider(
            ProviderKind.OpenAi,
            "https://api.openai.test/v1",
            "gpt-image",
            purpose: ProviderProfilePurpose.ImageGeneration);

        var models = await driver.ListModelsAsync(new ProviderModelCatalogRequest(provider, AgentProviderCapabilityKind.ChatCompletion));
        var chat = await driver.CompleteChatAsync(CreateChatRequest(provider, "gpt-test"));
        var image = await driver.GenerateImageAsync(new ProviderImageGenerationRequest(
            imageProvider,
            "gpt-image",
            "draw a cube",
            "1024x1024",
            "standard",
            ProviderGeneratedImageFormat.Png,
            []));
        var speech = await driver.SynthesizeSpeechAsync(new ProviderTextToSpeechRequest(
            provider,
            "gpt-tts",
            "hello",
            "alloy",
            "mp3",
            string.Empty));

        Assert.Equal("gpt-test", Assert.Single(models).Model);
        Assert.Equal("chat response", chat.ResponseText);
        Assert.Equal(3, chat.InputTokens);
        Assert.Equal(4, chat.OutputTokens);
        Assert.Equal(imageBytes, Assert.Single(image.Images).Bytes);
        Assert.Equal("revised", Assert.Single(image.Images).RevisedPrompt);
        Assert.Equal(new byte[] { 9, 8, 7 }, speech.AudioBytes);
        Assert.All(handler.Requests, request => Assert.Equal("Bearer openai-key", request.Authorization));
        Assert.Contains(handler.Requests, request => request.PathAndQuery == "/v1/images/generations" && request.Body.Contains("\"output_format\":\"png\"", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, request => request.PathAndQuery == "/v1/audio/speech" && request.Body.Contains("\"voice\":\"alloy\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AzureOpenAiProviderDriver_UsesDeploymentEndpointAndApiKey()
    {
        var handler = new CapturingHandler((request, body) =>
            JsonResponse("""{"choices":[{"message":{"content":"azure response"}}],"usage":{"prompt_tokens":5,"completion_tokens":6}}"""));
        using var httpClient = new HttpClient(handler);
        var driver = new AzureOpenAiProviderDriver(httpClient, new FixedCredentialResolver("azure-key"));
        var provider = CreateProvider(
            ProviderKind.AzureOpenAi,
            "https://azure-openai.test",
            "gpt-4o",
            configurationJson: """{"apiVersion":"2025-01-01-preview"}""");

        var result = await driver.CompleteChatAsync(CreateChatRequest(provider, "gpt-4o"));

        Assert.Equal("azure response", result.ResponseText);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/openai/deployments/gpt-4o/chat/completions?api-version=2025-01-01-preview", request.PathAndQuery);
        Assert.Equal("azure-key", request.Headers["api-key"]);
        Assert.Contains("\"messages\"", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAiProviderDriver_SerializesImageAttachmentsAsVisionContent()
    {
        var handler = new CapturingHandler((request, body) =>
            JsonResponse("""{"choices":[{"message":{"content":"vision response"}}],"usage":{"prompt_tokens":11,"completion_tokens":12}}"""));
        using var httpClient = new HttpClient(handler);
        var driver = new OpenAiProviderDriver(httpClient, new FixedCredentialResolver("openai-key"));
        var provider = CreateProvider(ProviderKind.OpenAi, "https://api.openai.test/v1", "gpt-4o");

        var result = await driver.CompleteChatAsync(new ProviderChatCompletionRequest(
            provider,
            "gpt-4o",
            "system",
            [],
            "Describe the screenshot.",
            [new ProviderChatAttachment("screen.png", "image/png", [1, 2, 3])]));

        Assert.Equal("vision response", result.ResponseText);
        var request = Assert.Single(handler.Requests);
        Assert.Contains("\"type\":\"text\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"image_url\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"url\":\"data:image/png;base64,AQID\"", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OllamaProviderDriver_SerializesImageAttachmentsAsNativeImages()
    {
        var handler = new CapturingHandler((request, body) =>
            JsonResponse("""{"message":{"content":"vision response"},"prompt_eval_count":13,"eval_count":14}"""));
        using var httpClient = new HttpClient(handler);
        var driver = new OllamaProviderDriver(httpClient);
        var provider = CreateProvider(ProviderKind.Ollama, "http://ollama.test", "llava");

        var result = await driver.CompleteChatAsync(new ProviderChatCompletionRequest(
            provider,
            "llava",
            "system",
            [],
            "Describe the screenshot.",
            [new ProviderChatAttachment("screen.png", "image/png", [1, 2, 3])]));

        Assert.Equal("vision response", result.ResponseText);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/chat", request.PathAndQuery);
        Assert.Contains("\"content\":\"Describe the screenshot.\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"images\":[\"AQID\"]", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OllamaProviderDriver_UsesThinkingWhenContentIsEmpty()
    {
        var handler = new CapturingHandler((request, body) =>
            JsonResponse("""{"message":{"content":"","thinking":"red circle and blue square"},"prompt_eval_count":13,"eval_count":14}"""));
        using var httpClient = new HttpClient(handler);
        var driver = new OllamaProviderDriver(httpClient);
        var provider = CreateProvider(ProviderKind.Ollama, "http://ollama.test", "qwen3.5:2b");

        var result = await driver.CompleteChatAsync(new ProviderChatCompletionRequest(
            provider,
            "qwen3.5:2b",
            "system",
            [],
            "Describe the screenshot.",
            [new ProviderChatAttachment("screen.png", "image/png", [1, 2, 3])]));

        Assert.Equal("red circle and blue square", result.ResponseText);
        Assert.Equal(13, result.InputTokens);
        Assert.Equal(14, result.OutputTokens);
    }

    [Fact]
    public async Task OllamaProviderDriver_SerializesConfiguredNumPredictAsChatOptions()
    {
        var handler = new CapturingHandler((request, body) =>
            JsonResponse("""{"message":{"content":"vision response"},"prompt_eval_count":13,"eval_count":14}"""));
        using var httpClient = new HttpClient(handler);
        var driver = new OllamaProviderDriver(httpClient);
        var provider = CreateProvider(
            ProviderKind.Ollama,
            "http://ollama.test",
            "qwen3.5:2b",
            """{"modelParameters":{"numPredict":80,"think":false}}""");

        var result = await driver.CompleteChatAsync(new ProviderChatCompletionRequest(
            provider,
            "qwen3.5:2b",
            "system",
            [],
            "Describe the screenshot.",
            [new ProviderChatAttachment("screen.png", "image/png", [1, 2, 3])]));

        Assert.Equal("vision response", result.ResponseText);
        var request = Assert.Single(handler.Requests);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal(80, body.RootElement.GetProperty("options").GetProperty("num_predict").GetInt32());
        Assert.False(body.RootElement.GetProperty("think").GetBoolean());
    }

    [Fact]
    public async Task OllamaProviderDriver_SerializesDefaultFastGenerationOptions()
    {
        var handler = new CapturingHandler((request, body) =>
            JsonResponse("""{"message":{"content":"fast response"},"prompt_eval_count":13,"eval_count":14}"""));
        using var httpClient = new HttpClient(handler);
        var driver = new OllamaProviderDriver(httpClient);
        var provider = CreateProvider(
            ProviderKind.Ollama,
            "http://ollama.test",
            "qwen3.5:2b",
            "{}");

        var result = await driver.CompleteChatAsync(new ProviderChatCompletionRequest(
            provider,
            "qwen3.5:2b",
            "system",
            [],
            "Reply with OK."));

        Assert.Equal("fast response", result.ResponseText);
        var request = Assert.Single(handler.Requests);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal(
            AgentProviderModelParameterPolicy.DefaultOllamaMaxOutputTokens,
            body.RootElement.GetProperty("options").GetProperty("num_predict").GetInt32());
        Assert.Equal(
            AgentProviderModelParameterPolicy.DefaultOllamaThinkEnabled,
            body.RootElement.GetProperty("think").GetBoolean());
    }

    [Fact]
    public async Task OllamaProviderDriver_PrefersRequestModelParametersOverProviderDefaults()
    {
        var handler = new CapturingHandler((request, body) =>
            JsonResponse("""{"message":{"content":"vision response"},"prompt_eval_count":13,"eval_count":14}"""));
        using var httpClient = new HttpClient(handler);
        var driver = new OllamaProviderDriver(httpClient);
        var provider = CreateProvider(
            ProviderKind.Ollama,
            "http://ollama.test",
            "qwen3.5:2b",
            """{"modelParameters":{"numPredict":4096,"think":true}}""");

        var result = await driver.CompleteChatAsync(new ProviderChatCompletionRequest(
            provider,
            "qwen3.5:2b",
            "system",
            [],
            "Describe the screenshot.",
            [new ProviderChatAttachment("screen.png", "image/png", [1, 2, 3])],
            """{"modelParameters":{"numPredict":512,"think":false}}"""));

        Assert.Equal("vision response", result.ResponseText);
        var request = Assert.Single(handler.Requests);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal(512, body.RootElement.GetProperty("options").GetProperty("num_predict").GetInt32());
        Assert.False(body.RootElement.GetProperty("think").GetBoolean());
    }

    [Fact]
    public async Task ConcreteDrivers_HealthReturnsStructuredFailureInsteadOfThrowing()
    {
        var handler = new CapturingHandler((request, body) =>
        {
            return request.RequestUri!.AbsolutePath switch
            {
                "/v1/models" => JsonResponse("""{"data":[{"id":"gpt-test"}]}"""),
                "/v1/chat/completions" => JsonResponse("""{"error":{"message":"chat probe failed"}}""", HttpStatusCode.BadRequest),
                _ => JsonResponse("{}", HttpStatusCode.NotFound)
            };
        });
        using var httpClient = new HttpClient(handler);
        var driver = new OpenAiProviderDriver(httpClient, new FixedCredentialResolver("openai-key"));

        var result = await driver.TestHealthAsync(CreateProvider(ProviderKind.OpenAi, "https://api.openai.test/v1", "gpt-test"));

        Assert.False(result.Success);
        Assert.Contains("chat probe failed", result.Summary, StringComparison.Ordinal);
        Assert.Contains("gpt-test", result.SuggestedModels);
    }

    [Fact]
    public async Task OllamaProviderDriver_UsesTagsChatAndCreateEndpoints()
    {
        var handler = new CapturingHandler((request, body) =>
        {
            return request.RequestUri!.AbsolutePath switch
            {
                "/api/tags" => JsonResponse("""{"models":[{"name":"llama3.1"},{"name":"qwen"}]}"""),
                "/api/chat" => JsonResponse("""{"message":{"content":"ollama response"},"prompt_eval_count":7,"eval_count":8}"""),
                "/api/create" => JsonResponse("""{"status":"success"}"""),
                _ => JsonResponse("{}", HttpStatusCode.NotFound)
            };
        });
        using var httpClient = new HttpClient(handler);
        var driver = new OllamaProviderDriver(httpClient);
        var provider = CreateProvider(ProviderKind.Ollama, "http://ollama.test", "llama3.1");

        var models = await driver.ListModelsAsync(new ProviderModelCatalogRequest(provider, AgentProviderCapabilityKind.ChatCompletion));
        var chat = await driver.CompleteChatAsync(CreateChatRequest(provider, "llama3.1"));
        var maintenance = await driver.CreateOrUpdateModelAsync(new ProviderModelMaintenanceRequest(
            provider,
            "custom-model",
            "llama3.1",
            "system prompt",
            4096));

        Assert.Equal(new[] { "llama3.1", "qwen" }, models.Select(model => model.Model).ToArray());
        Assert.Equal("ollama response", chat.ResponseText);
        Assert.Equal(7, chat.InputTokens);
        Assert.Equal(8, chat.OutputTokens);
        Assert.Equal("custom-model", maintenance.Model);
        Assert.Equal("success", maintenance.StatusMessage);
        Assert.Contains(handler.Requests, request => request.PathAndQuery == "/api/create" &&
                                                    request.Body.Contains("\"from\":\"llama3.1\"", StringComparison.Ordinal) &&
                                                    request.Body.Contains("\"num_ctx\":4096", StringComparison.Ordinal));
    }

    [Fact]
    public void ConcreteDrivers_ReportProviderSpecificDispatchLimits()
    {
        using var httpClient = new HttpClient(new CapturingHandler((request, body) => JsonResponse("{}")));
        var openAi = new OpenAiProviderDriver(httpClient, new FixedCredentialResolver("openai-key"));
        var azure = new AzureOpenAiProviderDriver(httpClient, new FixedCredentialResolver("azure-key"));
        var ollama = new OllamaProviderDriver(httpClient);

        var openAiLimits = openAi.GetDispatchLimits(new ProviderDispatchQuery(
            CreateProvider(ProviderKind.OpenAi, "https://api.openai.test/v1", "gpt-test", """{"timeoutSeconds":30}"""),
            AgentProviderCapabilityKind.ChatCompletion,
            AgentProviderOperationKind.CompleteChat,
            "gpt-test"));
        var azureLimits = azure.GetDispatchLimits(new ProviderDispatchQuery(
            CreateProvider(ProviderKind.AzureOpenAi, "https://azure-openai.test", "gpt-4o"),
            AgentProviderCapabilityKind.ChatCompletion,
            AgentProviderOperationKind.CompleteChat,
            "gpt-4o"));
        var ollamaLimits = ollama.GetDispatchLimits(new ProviderDispatchQuery(
            CreateProvider(ProviderKind.Ollama, "http://ollama.test", "llama3.1"),
            AgentProviderCapabilityKind.ChatCompletion,
            AgentProviderOperationKind.CompleteChat,
            "llama3.1"));

        Assert.False(openAiLimits.SupportsBatching);
        Assert.Equal(1, openAiLimits.MaxBatchSize);
        Assert.Equal(TimeSpan.FromSeconds(30), openAiLimits.RequestTimeout);
        Assert.Equal(TimeSpan.FromMinutes(2), azureLimits.RequestTimeout);
        Assert.Equal(TimeSpan.FromMinutes(5), ollamaLimits.RequestTimeout);
    }

    [Fact]
    public async Task ConcreteDrivers_NormalizeHttpFailureMessages()
    {
        var azureHandler = new CapturingHandler((request, body) =>
            JsonResponse("""{"error":{"message":"deployment missing"}}""", HttpStatusCode.BadRequest));
        using var azureClient = new HttpClient(azureHandler);
        var azure = new AzureOpenAiProviderDriver(azureClient, new FixedCredentialResolver("azure-key"));

        var azureException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => azure.CompleteChatAsync(CreateChatRequest(
                CreateProvider(ProviderKind.AzureOpenAi, "https://azure-openai.test", "gpt-4o"),
                "gpt-4o")));
        Assert.Contains("deployment missing", azureException.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("azure-key", azureException.Message, StringComparison.Ordinal);

        var ollamaHandler = new CapturingHandler((request, body) =>
            JsonResponse("""{"error":"model not found"}""", HttpStatusCode.InternalServerError));
        using var ollamaClient = new HttpClient(ollamaHandler);
        var ollama = new OllamaProviderDriver(ollamaClient);

        var ollamaException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ollama.CompleteChatAsync(CreateChatRequest(
                CreateProvider(ProviderKind.Ollama, "http://ollama.test", "llama3.1"),
                "llama3.1")));
        Assert.Contains("model not found", ollamaException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAiProviderDriver_PropagatesCancellationToken()
    {
        using var handler = new CancellationObservingHandler();
        using var httpClient = new HttpClient(handler);
        var driver = new OpenAiProviderDriver(httpClient, new FixedCredentialResolver("openai-key"));
        using var cts = new CancellationTokenSource();

        var requestTask = driver.ListModelsAsync(
            new ProviderModelCatalogRequest(
                CreateProvider(ProviderKind.OpenAi, "https://api.openai.test/v1", "gpt-test"),
                AgentProviderCapabilityKind.ChatCompletion),
            cts.Token);
        var observedToken = await handler.ObservedToken.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(observedToken.CanBeCanceled);

        cts.Cancel();
        Assert.True(observedToken.IsCancellationRequested);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => requestTask);
    }

    [Fact]
    public void ConcreteDrivers_ConsumersUseProviderRuntimeAdoptionBoundaries()
    {
        var root = FindRepositoryRoot();
        var filesThatMustNotAdoptProviderProject = new[]
        {
            "src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj",
            "src/Modules/CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj"
        };

        foreach (var relativePath in filesThatMustNotAdoptProviderProject)
        {
            var text = File.ReadAllText(Path.Combine(root, relativePath));
            Assert.DoesNotContain("CanDoItAll.AgentFramework.Providers", text, StringComparison.Ordinal);
        }

        Assert.Contains(
            "MafProviderRuntimeGateway",
            File.ReadAllText(Path.Combine(root, "src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderRuntimeGateway.cs")),
            StringComparison.Ordinal);
        Assert.Contains(
            "IAgentImageGenerationService",
            File.ReadAllText(Path.Combine(root, "src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/ImageGenerationAgentRuntimeToolProvider.cs")),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GenerateOpenAiImageAsync",
            File.ReadAllText(Path.Combine(root, "src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/ImageGenerationAgentRuntimeToolProvider.cs")),
            StringComparison.Ordinal);
        Assert.Contains(
            "ProviderRuntimeVoiceDriver",
            File.ReadAllText(Path.Combine(root, "src/MAF/Common/CanDoItAll.AgentFramework.Voice/AgentVoiceDriverFactory.cs")),
            StringComparison.Ordinal);
    }

    private static ProviderChatCompletionRequest CreateChatRequest(
        ProviderProfile provider,
        string model)
    {
        return new ProviderChatCompletionRequest(
            provider,
            model,
            "system",
            [new ProviderTestChatMessage(ChatMessageRole.User, "hello", DateTimeOffset.UnixEpoch)],
            "prompt");
    }

    private static ProviderProfile CreateProvider(
        ProviderKind kind,
        string baseUrl,
        string defaultModel,
        string configurationJson = "{}",
        ProviderProfilePurpose purpose = ProviderProfilePurpose.Chat)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            $"{kind} provider",
            kind,
            baseUrl,
            "TEST_API_KEY",
            defaultModel,
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: false,
            SupportsTools: false,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: configurationJson,
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: [defaultModel],
            Purpose: purpose);
    }

    private static HttpResponseMessage JsonResponse(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage BinaryResponse(
        byte[] bytes,
        string contentType)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
            {
                Headers =
                {
                    ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType)
                }
            }
        };
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private sealed class FixedCredentialResolver(string apiKey) : IProviderDriverCredentialResolver
    {
        public ProviderDriverCredential Resolve(ProviderProfile provider)
        {
            return ProviderDriverCredential.Resolved(apiKey);
        }
    }

    private sealed class CapturingHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var headers = request.Headers.ToDictionary(
                header => header.Key,
                header => string.Join(",", header.Value),
                StringComparer.OrdinalIgnoreCase);
            if (request.Content is not null)
            {
                foreach (var header in request.Content.Headers)
                {
                    headers[header.Key] = string.Join(",", header.Value);
                }
            }

            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri?.PathAndQuery ?? string.Empty,
                request.Headers.Authorization?.ToString() ?? string.Empty,
                headers,
                body));
            return respond(request, body);
        }
    }

    private sealed class CancellationObservingHandler : HttpMessageHandler, IDisposable
    {
        private readonly TaskCompletionSource<CancellationToken> observedToken = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<CancellationToken> ObservedToken => observedToken.Task;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            observedToken.TrySetResult(cancellationToken);
            var pendingResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellationToken.Register(
                static state =>
                {
                    var source = (TaskCompletionSource<HttpResponseMessage>)state!;
                    source.TrySetCanceled();
                },
                pendingResponse);
            return await pendingResponse.Task.ConfigureAwait(false);
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        string PathAndQuery,
        string Authorization,
        IReadOnlyDictionary<string, string> Headers,
        string Body);
}
