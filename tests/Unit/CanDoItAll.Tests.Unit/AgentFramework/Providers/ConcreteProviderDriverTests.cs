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
    public async Task OpenAiProviderDriver_UsesResponsesEndpointPayloadAndUsage()
    {
        var handler = new CapturingHandler((request, body) =>
            JsonResponse(
                """
                {
                  "status": "completed",
                  "output_text": "responses answer",
                  "usage": {
                    "input_tokens": 13,
                    "output_tokens": 8
                  }
                }
                """));
        using var httpClient = new HttpClient(handler);
        var driver = new OpenAiProviderDriver(httpClient, new FixedCredentialResolver("openai-key"));
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            "https://api.openai.test/v1",
            OpenAiModelIds.Gpt56Sol,
            transport: ProviderTransportKind.Responses);

        var result = await driver.CompleteChatAsync(CreateChatRequest(provider, OpenAiModelIds.Gpt56Sol));

        Assert.Equal("responses answer", result.ResponseText);
        Assert.Equal(13, result.InputTokens);
        Assert.Equal(8, result.OutputTokens);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v1/responses", request.PathAndQuery);
        using var body = JsonDocument.Parse(request.Body);
        var root = body.RootElement;
        Assert.Equal(OpenAiModelIds.Gpt56Sol, root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.False(root.GetProperty("store").GetBoolean());
        var input = root.GetProperty("input").EnumerateArray().ToArray();
        Assert.Collection(
            input,
            message => AssertResponseInputMessage(message, "system", "system"),
            message => AssertResponseInputMessage(message, "user", "hello"),
            message => AssertResponseInputMessage(message, "user", "prompt"));
    }

    [Fact]
    public async Task OpenAiProviderDriver_UsesResponsesWireNamesForReasoningAndMaxOutputTokens()
    {
        var handler = new CapturingHandler((request, body) =>
            JsonResponse("""{"status":"completed","output_text":"configured"}"""));
        using var httpClient = new HttpClient(handler);
        var driver = new OpenAiProviderDriver(httpClient, new FixedCredentialResolver("openai-key"));
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            "https://api.openai.test/v1",
            OpenAiModelIds.Gpt56Sol,
            """{"modelParameters":{"reasoningEffort":"max","maxOutputTokens":128000}}""",
            transport: ProviderTransportKind.Responses);

        var result = await driver.CompleteChatAsync(CreateChatRequest(provider, OpenAiModelIds.Gpt56Sol));

        Assert.Equal("configured", result.ResponseText);
        var request = Assert.Single(handler.Requests);
        using var body = JsonDocument.Parse(request.Body);
        var root = body.RootElement;
        Assert.Equal("max", root.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Equal(128_000, root.GetProperty("max_output_tokens").GetInt32());
        Assert.False(root.TryGetProperty("reasoning_effort", out _));
        Assert.False(root.TryGetProperty("max_completion_tokens", out _));
    }

    [Fact]
    public async Task OpenAiProviderDriver_UsesNativeMinimalReasoningValue()
    {
        var handler = new CapturingHandler((request, body) =>
            JsonResponse("""{"status":"completed","output_text":"configured"}"""));
        using var httpClient = new HttpClient(handler);
        var driver = new OpenAiProviderDriver(httpClient, new FixedCredentialResolver("openai-key"));
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            "https://api.openai.test/v1",
            "gpt-5",
            """{"modelParameters":{"reasoningEffort":"minimal"}}""",
            transport: ProviderTransportKind.Responses);

        await driver.CompleteChatAsync(CreateChatRequest(provider, "gpt-5"));

        var request = Assert.Single(handler.Requests);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal(
            "minimal",
            body.RootElement.GetProperty("reasoning").GetProperty("effort").GetString());
    }

    [Fact]
    public async Task OpenAiProviderDriver_UsesChatCompletionsWireNamesForReasoningAndMaxOutputTokens()
    {
        var handler = new CapturingHandler((request, body) =>
            JsonResponse("""{"choices":[{"message":{"content":"configured"}}]}"""));
        using var httpClient = new HttpClient(handler);
        var driver = new OpenAiProviderDriver(httpClient, new FixedCredentialResolver("openai-key"));
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            "https://api.openai.test/v1",
            OpenAiModelIds.Gpt56Luna,
            """{"modelParameters":{"reasoningEffort":"high","maxOutputTokens":128000}}""");

        var result = await driver.CompleteChatAsync(CreateChatRequest(provider, OpenAiModelIds.Gpt56Luna));

        Assert.Equal("configured", result.ResponseText);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/v1/chat/completions", request.PathAndQuery);
        using var body = JsonDocument.Parse(request.Body);
        var root = body.RootElement;
        Assert.Equal("high", root.GetProperty("reasoning_effort").GetString());
        Assert.Equal(128_000, root.GetProperty("max_completion_tokens").GetInt32());
        Assert.False(root.TryGetProperty("reasoning", out _));
        Assert.False(root.TryGetProperty("max_output_tokens", out _));
    }

    [Fact]
    public async Task OpenAiProviderDriver_ResponsesFiltersReasoningTextFromAssistantOutput()
    {
        var handler = new CapturingHandler((request, body) =>
            JsonResponse(
                """
                {
                  "status": "completed",
                  "output": [
                    {
                      "type": "reasoning",
                      "content": [
                        { "type": "reasoning_text", "text": "internal reasoning" }
                      ]
                    },
                    {
                      "type": "message",
                      "role": "assistant",
                      "content": [
                        { "type": "output_text", "text": "final answer" }
                      ]
                    }
                  ]
                }
                """));
        using var httpClient = new HttpClient(handler);
        var driver = new OpenAiProviderDriver(httpClient, new FixedCredentialResolver("openai-key"));
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            "https://api.openai.test/v1",
            OpenAiModelIds.Gpt56Sol,
            transport: ProviderTransportKind.Responses);

        var result = await driver.CompleteChatAsync(CreateChatRequest(provider, OpenAiModelIds.Gpt56Sol));

        Assert.Equal("final answer", result.ResponseText);
        Assert.DoesNotContain("internal reasoning", result.ResponseText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAiProviderDriver_ResponsesReturnsRefusalOutput()
    {
        var handler = new CapturingHandler((request, body) =>
            JsonResponse(
                """
                {
                  "status": "completed",
                  "output": [
                    {
                      "type": "message",
                      "role": "assistant",
                      "content": [
                        { "type": "refusal", "refusal": "I cannot help with that." }
                      ]
                    }
                  ]
                }
                """));
        using var httpClient = new HttpClient(handler);
        var driver = new OpenAiProviderDriver(httpClient, new FixedCredentialResolver("openai-key"));
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            "https://api.openai.test/v1",
            OpenAiModelIds.Gpt56Sol,
            transport: ProviderTransportKind.Responses);

        var result = await driver.CompleteChatAsync(CreateChatRequest(provider, OpenAiModelIds.Gpt56Sol));

        Assert.Equal("I cannot help with that.", result.ResponseText);
    }

    [Theory]
    [InlineData("{\"status\":\"failed\",\"error\":{\"message\":\"model overloaded\"}}", "failed", "model overloaded")]
    [InlineData("{\"status\":\"incomplete\",\"incomplete_details\":{\"reason\":\"max_output_tokens\"}}", "incomplete", "max_output_tokens")]
    public async Task OpenAiProviderDriver_ResponsesRejectsNonCompletedTwoHundredStatuses(
        string responseJson,
        string status,
        string detail)
    {
        var handler = new CapturingHandler((request, body) => JsonResponse(responseJson));
        using var httpClient = new HttpClient(handler);
        var driver = new OpenAiProviderDriver(httpClient, new FixedCredentialResolver("openai-key"));
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            "https://api.openai.test/v1",
            OpenAiModelIds.Gpt56Sol,
            transport: ProviderTransportKind.Responses);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.CompleteChatAsync(CreateChatRequest(provider, OpenAiModelIds.Gpt56Sol)));

        Assert.Contains($"status '{status}'", exception.Message, StringComparison.Ordinal);
        Assert.Contains(detail, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://azure-openai.test")]
    [InlineData("https://azure-openai.test/")]
    [InlineData("https://azure-openai.test/openai")]
    [InlineData("https://azure-openai.test/openai/")]
    [InlineData("https://azure-openai.test/openai/v1")]
    [InlineData("https://azure-openai.test/openai/v1/")]
    public async Task AzureOpenAiProviderDriver_InterpretsSupportedBaseUrlsConsistently(
        string baseUrl)
    {
        var handler = new CapturingHandler((request, body) =>
            JsonResponse("""{"choices":[{"message":{"content":"azure response"}}],"usage":{"prompt_tokens":5,"completion_tokens":6}}"""));
        using var httpClient = new HttpClient(handler);
        var driver = new AzureOpenAiProviderDriver(httpClient, new FixedCredentialResolver("azure-key"));
        var provider = CreateProvider(
            ProviderKind.AzureOpenAi,
            baseUrl,
            "gpt-4o",
            configurationJson: """{"apiVersion":"2025-01-01-preview"}""");

        var result = await driver.CompleteChatAsync(CreateChatRequest(provider, "gpt-4o"));

        Assert.Equal("azure response", result.ResponseText);
        Assert.Equal(
            "https://azure-openai.test/openai/v1/",
            AzureOpenAiEndpoint.Parse(provider).V1Endpoint.AbsoluteUri);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/openai/deployments/gpt-4o/chat/completions?api-version=2025-01-01-preview", request.PathAndQuery);
        Assert.Equal("azure-key", request.Headers["api-key"]);
        Assert.Contains("\"messages\"", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AzureOpenAiProviderDriver_ChatCompletionsUsesDeploymentEndpointAndAgentReasoningOverride()
    {
        const string deployment = "reasoning-deployment";
        var handler = new CapturingHandler((request, body) =>
            JsonResponse(
                """{"choices":[{"message":{"content":"azure chat response"}}],"usage":{"prompt_tokens":5,"completion_tokens":6}}"""));
        using var httpClient = new HttpClient(handler);
        var driver = new AzureOpenAiProviderDriver(httpClient, new FixedCredentialResolver("azure-key"));
        var provider = CreateAzureReasoningProvider(
            deployment,
            ProviderTransportKind.ChatCompletions);

        var result = await driver.CompleteChatAsync(new ProviderChatCompletionRequest(
            provider,
            deployment,
            "system",
            [new ProviderTestChatMessage(ChatMessageRole.User, "hello", DateTimeOffset.UnixEpoch)],
            "prompt",
            ModelParameterConfigurationJson: AgentThinkingEffortPolicy.WriteAgentOverride(
                "{}",
                AgentReasoningEffortLevel.High)));

        Assert.Equal("azure chat response", result.ResponseText);
        Assert.Equal(5, result.InputTokens);
        Assert.Equal(6, result.OutputTokens);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            "/openai/deployments/reasoning-deployment/chat/completions?api-version=2024-10-21",
            request.PathAndQuery);
        Assert.Equal("azure-key", request.Headers["api-key"]);
        using var body = JsonDocument.Parse(request.Body);
        var root = body.RootElement;
        Assert.Equal(3, root.EnumerateObject().Count());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal("high", root.GetProperty("reasoning_effort").GetString());
        Assert.False(root.TryGetProperty("model", out _));
        Assert.False(root.TryGetProperty("input", out _));
        Assert.False(root.TryGetProperty("reasoning", out _));
        Assert.False(root.TryGetProperty("store", out _));
        var messages = root.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Collection(
            messages,
            message => AssertResponseInputMessage(message, "system", "system"),
            message => AssertResponseInputMessage(message, "user", "hello"),
            message => AssertResponseInputMessage(message, "user", "prompt"));
    }

    [Fact]
    public async Task AzureOpenAiProviderDriver_ResponsesUsesV1EndpointAndAgentReasoningOverride()
    {
        const string deployment = "reasoning-deployment";
        var handler = new CapturingHandler((request, body) =>
            JsonResponse(
                """
                {
                  "status": "completed",
                  "output_text": "azure response",
                  "usage": {
                    "input_tokens": 7,
                    "output_tokens": 8
                  }
                }
                """));
        using var httpClient = new HttpClient(handler);
        var driver = new AzureOpenAiProviderDriver(httpClient, new FixedCredentialResolver("azure-key"));
        var provider = CreateAzureReasoningProvider(
            deployment,
            ProviderTransportKind.Responses);

        var result = await driver.CompleteChatAsync(new ProviderChatCompletionRequest(
            provider,
            deployment,
            "system",
            [new ProviderTestChatMessage(ChatMessageRole.User, "hello", DateTimeOffset.UnixEpoch)],
            "prompt",
            ModelParameterConfigurationJson: AgentThinkingEffortPolicy.WriteAgentOverride(
                "{}",
                AgentReasoningEffortLevel.High)));

        Assert.Equal("azure response", result.ResponseText);
        Assert.Equal(7, result.InputTokens);
        Assert.Equal(8, result.OutputTokens);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/openai/v1/responses", request.PathAndQuery);
        Assert.Equal("azure-key", request.Headers["api-key"]);
        using var body = JsonDocument.Parse(request.Body);
        var root = body.RootElement;
        Assert.Equal(5, root.EnumerateObject().Count());
        Assert.Equal(deployment, root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.False(root.GetProperty("store").GetBoolean());
        Assert.Equal("high", root.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.False(root.TryGetProperty("messages", out _));
        Assert.False(root.TryGetProperty("reasoning_effort", out _));
        var input = root.GetProperty("input").EnumerateArray().ToArray();
        Assert.Collection(
            input,
            message => AssertResponseInputMessage(message, "system", "system"),
            message => AssertResponseInputMessage(message, "user", "hello"),
            message => AssertResponseInputMessage(message, "user", "prompt"));
    }

    [Fact]
    public async Task AzureOpenAiProviderDriver_RejectsReasoningEffortWithoutProviderMetadataBeforeDispatch()
    {
        const string deployment = "unknown-deployment";
        var handler = new CapturingHandler((request, body) =>
            JsonResponse("""{"choices":[{"message":{"content":"unexpected"}}]}"""));
        using var httpClient = new HttpClient(handler);
        var driver = new AzureOpenAiProviderDriver(httpClient, new FixedCredentialResolver("azure-key"));
        var provider = CreateProvider(
            ProviderKind.AzureOpenAi,
            "https://azure-openai.test",
            deployment);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            driver.CompleteChatAsync(new ProviderChatCompletionRequest(
                provider,
                deployment,
                "system",
                [],
                "Reply with OK.",
                ModelParameterConfigurationJson: """{"modelParameters":{"reasoningEffort":"medium"}}""")));

        Assert.Contains("provider-scoped thinking-effort capability metadata", exception.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("https://azure-openai.test/openai/deployments/gpt-4o", "legacy deployment endpoint")]
    [InlineData("https://azure-openai.test?api-version=preview", "query parameters or fragments")]
    [InlineData("https://azure-openai.test#configuration", "query parameters or fragments")]
    [InlineData("https://azure-openai.test/openai/v1/chat/completions", "must use the resource base URL")]
    public async Task AzureOpenAiProviderDriver_RejectsUnsupportedBaseUrlsBeforeDispatch(
        string baseUrl,
        string expectedMessage)
    {
        var handler = new CapturingHandler((request, body) => JsonResponse("{}"));
        using var httpClient = new HttpClient(handler);
        var driver = new AzureOpenAiProviderDriver(httpClient, new FixedCredentialResolver("azure-key"));
        var provider = CreateProvider(
            ProviderKind.AzureOpenAi,
            baseUrl,
            "gpt-4o");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.CompleteChatAsync(CreateChatRequest(provider, "gpt-4o")));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
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
    public async Task OpenAiProviderDriver_IgnoresLegacyOllamaThinkingAliasesForGpt4o()
    {
        var handler = new CapturingHandler((request, body) =>
            JsonResponse("""{"choices":[{"message":{"content":"vision response"}}]}"""));
        using var httpClient = new HttpClient(handler);
        var driver = new OpenAiProviderDriver(httpClient, new FixedCredentialResolver("openai-key"));
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            "https://api.openai.test/v1",
            "gpt-4o",
            """{"think":true}""");

        var result = await driver.CompleteChatAsync(new ProviderChatCompletionRequest(
            provider,
            "gpt-4o",
            "system",
            [],
            "Describe the screenshot.",
            Attachments: [new ProviderChatAttachment("screen.png", "image/png", [1, 2, 3])],
            ModelParameterConfigurationJson: """{"modelParameters":{"think":false}}"""));

        Assert.Equal("vision response", result.ResponseText);
        var request = Assert.Single(handler.Requests);
        using var body = JsonDocument.Parse(request.Body);
        Assert.False(body.RootElement.TryGetProperty("reasoning_effort", out _));
        Assert.Contains("\"type\":\"image_url\"", request.Body, StringComparison.Ordinal);
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
    public async Task OllamaProviderDriver_DoesNotExposeThinkingWhenAssistantContentIsEmpty()
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

        Assert.Equal(string.Empty, result.ResponseText);
        Assert.Equal(13, result.InputTokens);
        Assert.Equal(14, result.OutputTokens);
    }

    [Fact]
    public async Task OllamaProviderDriver_SerializesExplicitNoneAsFalseAndConfiguredNumPredictAsChatOptions()
    {
        var handler = new CapturingHandler((request, body) =>
            JsonResponse("""{"message":{"content":"vision response"},"prompt_eval_count":13,"eval_count":14}"""));
        using var httpClient = new HttpClient(handler);
        var driver = new OllamaProviderDriver(httpClient);
        var provider = CreateProvider(
            ProviderKind.Ollama,
            "http://ollama.test",
            "qwen3.5:2b",
            """{"modelParameters":{"numPredict":80}}""");

        var result = await driver.CompleteChatAsync(new ProviderChatCompletionRequest(
            provider,
            "qwen3.5:2b",
            "system",
            [],
            "Describe the screenshot.",
            [new ProviderChatAttachment("screen.png", "image/png", [1, 2, 3])],
            """{"modelParameters":{"reasoningEffort":"none"}}"""));

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
        Assert.False(body.RootElement.TryGetProperty("think", out _));
    }

    [Fact]
    public async Task OllamaProviderDriver_MapsBinaryEnabledEffortToTrueAndPrefersRequestParameters()
    {
        var handler = new CapturingHandler((request, body) =>
            JsonResponse("""{"message":{"content":"vision response"},"prompt_eval_count":13,"eval_count":14}"""));
        using var httpClient = new HttpClient(handler);
        var driver = new OllamaProviderDriver(httpClient);
        var provider = CreateProvider(
            ProviderKind.Ollama,
            "http://ollama.test",
            "qwen3.5:2b",
            """{"modelParameters":{"numPredict":4096,"reasoningEffort":"none"}}""");

        var result = await driver.CompleteChatAsync(new ProviderChatCompletionRequest(
            provider,
            "qwen3.5:2b",
            "system",
            [],
            "Describe the screenshot.",
            [new ProviderChatAttachment("screen.png", "image/png", [1, 2, 3])],
            """{"modelParameters":{"numPredict":512,"reasoningEffort":"medium"}}"""));

        Assert.Equal("vision response", result.ResponseText);
        var request = Assert.Single(handler.Requests);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal(512, body.RootElement.GetProperty("options").GetProperty("num_predict").GetInt32());
        Assert.True(body.RootElement.GetProperty("think").GetBoolean());
    }

    [Fact]
    public async Task OllamaProviderDriver_MapsGptOssEffortToNativeLevel()
    {
        var handler = new CapturingHandler((request, body) =>
            JsonResponse("""{"message":{"content":"response"},"prompt_eval_count":13,"eval_count":14}"""));
        using var httpClient = new HttpClient(handler);
        var driver = new OllamaProviderDriver(httpClient);
        var provider = CreateProvider(ProviderKind.Ollama, "http://ollama.test", "gpt-oss:20b");

        await driver.CompleteChatAsync(new ProviderChatCompletionRequest(
            provider,
            "gpt-oss:20b",
            "system",
            [],
            "Reply with OK.",
            ModelParameterConfigurationJson: """{"modelParameters":{"reasoningEffort":"high"}}"""));

        var request = Assert.Single(handler.Requests);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("high", body.RootElement.GetProperty("think").GetString());
    }

    [Fact]
    public async Task OllamaProviderDriver_RejectsGptOssDisableBeforeHttpDispatch()
    {
        var handler = new CapturingHandler((request, body) =>
            JsonResponse("""{"message":{"content":"unexpected"}}"""));
        using var httpClient = new HttpClient(handler);
        var driver = new OllamaProviderDriver(httpClient);
        var provider = CreateProvider(ProviderKind.Ollama, "http://ollama.test", "gptoss32k:latest");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            driver.CompleteChatAsync(new ProviderChatCompletionRequest(
                provider,
                "gptoss32k:latest",
                "system",
                [],
                "Reply with OK.",
                ModelParameterConfigurationJson: """{"modelParameters":{"reasoningEffort":"none"}}""")));

        Assert.Contains("none", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Allowed values are low, medium, high", exception.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task OllamaProviderDriver_RejectsExtraHighBeforeHttpDispatch()
    {
        var handler = new CapturingHandler((request, body) =>
            JsonResponse("""{"message":{"content":"unexpected"}}"""));
        using var httpClient = new HttpClient(handler);
        var driver = new OllamaProviderDriver(httpClient);
        var provider = CreateProvider(ProviderKind.Ollama, "http://ollama.test", "qwen3.5:2b");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            driver.CompleteChatAsync(new ProviderChatCompletionRequest(
                provider,
                "qwen3.5:2b",
                "system",
                [],
                "Reply with OK.",
                ModelParameterConfigurationJson: """{"modelParameters":{"reasoningEffort":"xhigh"}}""")));

        Assert.Contains("xhigh", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Allowed values", exception.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
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
    public async Task OllamaProviderDriver_UsesShowCapabilitiesWhenTagsOmitThinkingMetadata()
    {
        var handler = new CapturingHandler((request, body) =>
            request.RequestUri!.AbsolutePath switch
            {
                "/api/tags" => JsonResponse(
                    """{"models":[{"name":"deepseek-r1:14b","details":{"family":"qwen2"}}]}"""),
                "/api/show" => JsonResponse(
                    """{"details":{"family":"qwen2"},"capabilities":["tools","thinking","completion"]}"""),
                _ => JsonResponse("{}", HttpStatusCode.NotFound)
            });
        using var httpClient = new HttpClient(handler);
        var driver = new OllamaProviderDriver(httpClient);
        var provider = CreateProvider(ProviderKind.Ollama, "http://ollama.test", "deepseek-r1:14b");

        var models = await driver.ListModelsAsync(
            new ProviderModelCatalogRequest(
                provider,
                AgentProviderCapabilityKind.ChatCompletion));

        var model = Assert.Single(models);
        var capability = Assert.IsType<ProviderModelThinkingEffortCapability>(
            model.ThinkingEffortCapability);
        Assert.Equal(AgentThinkingEffortSupportStatus.Supported, capability.Status);
        Assert.Equal(AgentThinkingEffortCapabilitySource.Discovered, capability.Source);
        Assert.Equal(AgentThinkingEffortControlMode.BooleanToggle, capability.ControlMode);
        Assert.Equal(
            [AgentReasoningEffortLevel.None, AgentReasoningEffortLevel.Medium],
            capability.AllowedEfforts);
        Assert.Collection(
            handler.Requests,
            request => Assert.Equal("/api/tags", request.PathAndQuery),
            request =>
            {
                Assert.Equal("/api/show", request.PathAndQuery);
                Assert.Contains("\"model\":\"deepseek-r1:14b\"", request.Body, StringComparison.Ordinal);
                Assert.Contains("\"verbose\":false", request.Body, StringComparison.Ordinal);
            });
    }

    [Fact]
    public async Task OllamaProviderDriver_RejectsInvalidShowCapabilityShape()
    {
        var handler = new CapturingHandler((request, body) =>
            request.RequestUri!.AbsolutePath switch
            {
                "/api/tags" => JsonResponse("""{"models":[{"name":"custom-model"}]}"""),
                "/api/show" => JsonResponse("""{"capabilities":"thinking"}"""),
                _ => JsonResponse("{}", HttpStatusCode.NotFound)
            });
        using var httpClient = new HttpClient(handler);
        var driver = new OllamaProviderDriver(httpClient);
        var provider = CreateProvider(ProviderKind.Ollama, "http://ollama.test", "custom-model");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            driver.ListModelsAsync(
                new ProviderModelCatalogRequest(
                    provider,
                    AgentProviderCapabilityKind.ChatCompletion)));

        Assert.Contains("model details for 'custom-model'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("must be an array", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OllamaProviderDriver_UsesTagsChatAndCreateEndpoints()
    {
        var handler = new CapturingHandler((request, body) =>
        {
            return request.RequestUri!.AbsolutePath switch
            {
                "/api/tags" => JsonResponse(
                    """
                    {
                      "models": [
                        {
                          "name": "llama3.1",
                          "details": { "family": "llama" },
                          "capabilities": ["completion"]
                        },
                        {
                          "name": "qwen3.5:2b",
                          "details": { "family": "qwen35" },
                          "capabilities": ["completion", "thinking"]
                        },
                        {
                          "name": "gptoss32k:latest",
                          "details": { "family": "gptoss" },
                          "capabilities": ["completion", "thinking"]
                        },
                        {
                          "name": "custom-unknown",
                          "details": { "family": "custom" }
                        }
                      ]
                    }
                    """),
                "/api/show" when body.Contains("\"model\":\"llama3.1\"", StringComparison.Ordinal) =>
                    JsonResponse("""{"details":{"family":"llama"},"capabilities":["completion"]}"""),
                "/api/show" when body.Contains("\"model\":\"custom-unknown\"", StringComparison.Ordinal) =>
                    JsonResponse("""{"details":{"family":"custom"}}"""),
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

        Assert.Equal(
            new[] { "llama3.1", "qwen3.5:2b", "gptoss32k:latest", "custom-unknown" },
            models.Select(model => model.Model).ToArray());
        var unsupportedCapability = Assert.IsType<ProviderModelThinkingEffortCapability>(
            Assert.Single(models, model => model.Model == "llama3.1").ThinkingEffortCapability);
        var supportedCapability = Assert.IsType<ProviderModelThinkingEffortCapability>(
            Assert.Single(models, model => model.Model == "qwen3.5:2b").ThinkingEffortCapability);
        var gptOssCapability = Assert.IsType<ProviderModelThinkingEffortCapability>(
            Assert.Single(models, model => model.Model == "gptoss32k:latest").ThinkingEffortCapability);
        var unknownCapability = Assert.IsType<ProviderModelThinkingEffortCapability>(
            Assert.Single(models, model => model.Model == "custom-unknown").ThinkingEffortCapability);
        Assert.Equal(AgentThinkingEffortSupportStatus.Unsupported, unsupportedCapability.Status);
        Assert.Equal("llama", unsupportedCapability.ModelFamily);
        Assert.Equal(AgentThinkingEffortSupportStatus.Supported, supportedCapability.Status);
        Assert.Equal("qwen35", supportedCapability.ModelFamily);
        Assert.Equal(
            [AgentReasoningEffortLevel.None, AgentReasoningEffortLevel.Medium],
            supportedCapability.AllowedEfforts);
        Assert.Equal(AgentThinkingEffortControlMode.BooleanToggle, supportedCapability.ControlMode);
        Assert.Equal(AgentThinkingEffortSupportStatus.Supported, gptOssCapability.Status);
        Assert.Equal("gptoss", gptOssCapability.ModelFamily);
        Assert.Equal(
            [
                AgentReasoningEffortLevel.Low,
                AgentReasoningEffortLevel.Medium,
                AgentReasoningEffortLevel.High
            ],
            gptOssCapability.AllowedEfforts);
        Assert.Equal(AgentThinkingEffortControlMode.EffortLevels, gptOssCapability.ControlMode);
        Assert.Equal(AgentThinkingEffortSupportStatus.Unknown, unknownCapability.Status);
        Assert.Equal("custom", unknownCapability.ModelFamily);
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

    private static void AssertResponseInputMessage(
        JsonElement message,
        string expectedRole,
        string expectedContent)
    {
        Assert.Equal(expectedRole, message.GetProperty("role").GetString());
        Assert.Equal(expectedContent, message.GetProperty("content").GetString());
    }

    private static ProviderProfile CreateProvider(
        ProviderKind kind,
        string baseUrl,
        string defaultModel,
        string configurationJson = "{}",
        ProviderTransportKind transport = ProviderTransportKind.ChatCompletions,
        ProviderProfilePurpose purpose = ProviderProfilePurpose.Chat)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            $"{kind} provider",
            kind,
            baseUrl,
            "TEST_API_KEY",
            defaultModel,
            transport,
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

    private static ProviderProfile CreateAzureReasoningProvider(
        string deployment,
        ProviderTransportKind transport)
    {
        return CreateProvider(
            ProviderKind.AzureOpenAi,
            "https://azure-openai.test",
            deployment,
            AgentThinkingEffortPolicy.WriteProviderDefault(
                "{}",
                AgentReasoningEffortLevel.Low),
            transport) with
        {
            ModelThinkingEffortCapabilities =
            [
                new ProviderModelThinkingEffortCapability(
                    deployment,
                    AgentThinkingEffortSupportStatus.Supported,
                    AgentThinkingEffortCapabilitySource.Defined,
                    [AgentReasoningEffortLevel.Low, AgentReasoningEffortLevel.High])
            ]
        };
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
