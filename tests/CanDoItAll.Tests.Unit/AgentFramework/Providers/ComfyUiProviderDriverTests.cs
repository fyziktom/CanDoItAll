using System.Net;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.Tests.Unit.AgentFramework.Providers;

public sealed class ComfyUiProviderDriverTests
{
    [Fact]
    public void ComfyUiProviderDriver_RegistersImageAndHealthCapabilitiesOnly()
    {
        using var httpClient = new HttpClient(new ComfyUiHandler(_ => JsonResponse("{}")));

        var factory = new AgentProviderDriverRegistryBuilder()
            .AddComfyUiProviderDriver(httpClient)
            .Build();

        Assert.True(factory.Supports(ProviderKind.ComfyUi, AgentProviderCapabilityKind.ImageGeneration));
        Assert.True(factory.Supports(ProviderKind.ComfyUi, AgentProviderCapabilityKind.Health));
        Assert.False(factory.Supports(ProviderKind.ComfyUi, AgentProviderCapabilityKind.ChatCompletion));
        Assert.False(factory.Supports(ProviderKind.ComfyUi, AgentProviderCapabilityKind.TextToSpeech));

        var exception = Assert.Throws<UnsupportedProviderCapabilityException>(
            () => factory.Resolve<IProviderChatCompletionDriver>(ProviderKind.ComfyUi));
        Assert.Equal(ProviderKind.ComfyUi, exception.ProviderKind);
        Assert.Equal(AgentProviderCapabilityKind.ChatCompletion, exception.Capability);
    }

    [Fact]
    public async Task ComfyUiProviderDriver_EnqueuesPollsParsesHistoryAndDownloadsImage()
    {
        var imageBytes = new byte[] { 9, 8, 7 };
        var historyCalls = 0;
        var handler = new ComfyUiHandler(request =>
        {
            return request.RequestUri!.AbsolutePath switch
            {
                "/prompt" => JsonResponse("""{"prompt_id":"prompt-123"}"""),
                "/history/prompt-123" when Interlocked.Increment(ref historyCalls) == 1 => JsonResponse("{}"),
                "/history/prompt-123" => JsonResponse("""
                    {
                      "prompt-123": {
                        "outputs": {
                          "9": {
                            "images": [
                              {
                                "filename": "ComfyUI_00001_.png",
                                "subfolder": "final",
                                "type": "output"
                              }
                            ]
                          }
                        }
                      }
                    }
                    """),
                "/view" => BinaryResponse(imageBytes, "image/png"),
                _ => JsonResponse("""{"error":"unexpected endpoint"}""", HttpStatusCode.NotFound)
            };
        });
        using var httpClient = new HttpClient(handler);
        var driver = new ComfyUiProviderDriver(httpClient);
        var provider = CreateProvider(CreateConfigurationJson(
            positivePromptNodeId: "6",
            negativePromptNodeId: "8",
            negativePrompt: "blurry",
            samplerNodeId: "3",
            seed: 123,
            widthNodeId: "5",
            heightNodeId: "5",
            outputNodeId: "9"));

        var result = await driver.GenerateImageAsync(new ProviderImageGenerationRequest(
            provider,
            "comfyui-workflow",
            "draw a precise workflow diagram",
            "1024x1536",
            "low",
            ProviderGeneratedImageFormat.Png,
            []));

        Assert.Equal("comfyui-workflow", result.Model);
        Assert.Equal(imageBytes, Assert.Single(result.Images).Bytes);
        Assert.Equal("image/png", Assert.Single(result.Images).ContentType);
        var promptRequest = Assert.Single(handler.Requests, request => request.PathAndQuery == "/prompt");
        using var promptDocument = JsonDocument.Parse(promptRequest.Body);
        var prompt = promptDocument.RootElement.GetProperty("prompt");
        Assert.Equal("draw a precise workflow diagram", prompt.GetProperty("6").GetProperty("inputs").GetProperty("text").GetString());
        Assert.Equal("blurry", prompt.GetProperty("8").GetProperty("inputs").GetProperty("text").GetString());
        Assert.Equal(123, prompt.GetProperty("3").GetProperty("inputs").GetProperty("seed").GetInt64());
        Assert.Equal(1024, prompt.GetProperty("5").GetProperty("inputs").GetProperty("width").GetInt32());
        Assert.Equal(1536, prompt.GetProperty("5").GetProperty("inputs").GetProperty("height").GetInt32());
        Assert.Contains(handler.Requests, request =>
            request.PathAndQuery == "/view?filename=ComfyUI_00001_.png&subfolder=final&type=output");
        Assert.Equal(2, historyCalls);
    }

    [Fact]
    public async Task ComfyUiProviderDriver_RejectsMissingWorkflowTemplate()
    {
        using var httpClient = new HttpClient(new ComfyUiHandler(_ => JsonResponse("{}")));
        var driver = new ComfyUiProviderDriver(httpClient);
        var provider = CreateProvider(JsonSerializer.Serialize(new
        {
            positivePromptNodeId = "6"
        }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => driver.GenerateImageAsync(CreateRequest(provider)));

        Assert.Contains("workflow template", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ComfyUiProviderDriver_PropagatesPromptHttpFailure()
    {
        using var httpClient = new HttpClient(new ComfyUiHandler(_ =>
            JsonResponse("""{"error":{"message":"bad workflow"}}""", HttpStatusCode.BadRequest)));
        var driver = new ComfyUiProviderDriver(httpClient);
        var provider = CreateProvider(CreateConfigurationJson("6"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => driver.GenerateImageAsync(CreateRequest(provider)));

        Assert.Contains("ComfyUI prompt enqueue failed with HTTP 400", exception.Message, StringComparison.Ordinal);
        Assert.Contains("bad workflow", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ComfyUiProviderDriver_TimesOutWhenHistoryDoesNotComplete()
    {
        using var httpClient = new HttpClient(new ComfyUiHandler(request =>
        {
            return request.RequestUri!.AbsolutePath switch
            {
                "/prompt" => JsonResponse("""{"prompt_id":"slow-prompt"}"""),
                "/history/slow-prompt" => JsonResponse("{}"),
                _ => JsonResponse("""{"error":"unexpected endpoint"}""", HttpStatusCode.NotFound)
            };
        }));
        var driver = new ComfyUiProviderDriver(httpClient);
        var provider = CreateProvider(CreateConfigurationJson(
            positivePromptNodeId: "6",
            timeoutSeconds: 1,
            pollIntervalMilliseconds: 100));

        var exception = await Assert.ThrowsAsync<TimeoutException>(() => driver.GenerateImageAsync(CreateRequest(provider)));

        Assert.Contains("slow-prompt", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ComfyUiProviderDriver_RejectsSourceImageEditRequests()
    {
        using var httpClient = new HttpClient(new ComfyUiHandler(_ => JsonResponse("{}")));
        var driver = new ComfyUiProviderDriver(httpClient);
        var provider = CreateProvider(CreateConfigurationJson("6"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => driver.GenerateImageAsync(new ProviderImageGenerationRequest(
            provider,
            "comfyui-workflow",
            "edit this image",
            "1024x1024",
            "low",
            ProviderGeneratedImageFormat.Png,
            [new ProviderImageSource("source.png", "image/png", [1, 2, 3])])));

        Assert.Contains("source images is not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ProviderImageGenerationRequest CreateRequest(ProviderProfile provider)
    {
        return new ProviderImageGenerationRequest(
            provider,
            "comfyui-workflow",
            "draw a cube",
            "1024x1024",
            "low",
            ProviderGeneratedImageFormat.Png,
            []);
    }

    private static ProviderProfile CreateProvider(string configurationJson)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            "Local ComfyUI",
            ProviderKind.ComfyUi,
            "http://comfy.test",
            string.Empty,
            "comfyui-workflow",
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
            SuggestedModels: ["comfyui-workflow"],
            Purpose: ProviderProfilePurpose.ImageGeneration)
        {
            IsPrivateProvider = true
        };
    }

    private static string CreateConfigurationJson(
        string positivePromptNodeId,
        string negativePromptNodeId = "",
        string negativePrompt = "",
        string samplerNodeId = "",
        long? seed = null,
        string widthNodeId = "",
        string heightNodeId = "",
        string outputNodeId = "",
        int timeoutSeconds = 5,
        int pollIntervalMilliseconds = 100)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            [ComfyUiProviderOptions.WorkflowTemplateJsonKey] = """
                {
                  "3": {
                    "inputs": {
                      "seed": 0
                    }
                  },
                  "5": {
                    "inputs": {
                      "width": 512,
                      "height": 512
                    }
                  },
                  "6": {
                    "inputs": {
                      "text": ""
                    }
                  },
                  "8": {
                    "inputs": {
                      "text": ""
                    }
                  },
                  "9": {
                    "inputs": {}
                  }
                }
                """,
            [ComfyUiProviderOptions.PositivePromptNodeIdKey] = positivePromptNodeId,
            [ComfyUiProviderOptions.NegativePromptNodeIdKey] = negativePromptNodeId,
            [ComfyUiProviderOptions.NegativePromptKey] = negativePrompt,
            [ComfyUiProviderOptions.SamplerNodeIdKey] = samplerNodeId,
            [ComfyUiProviderOptions.SeedKey] = seed,
            [ComfyUiProviderOptions.WidthNodeIdKey] = widthNodeId,
            [ComfyUiProviderOptions.HeightNodeIdKey] = heightNodeId,
            [ComfyUiProviderOptions.OutputNodeIdKey] = outputNodeId,
            [ComfyUiProviderOptions.TimeoutSecondsKey] = timeoutSeconds.ToString(),
            [ComfyUiProviderOptions.PollIntervalMillisecondsKey] = pollIntervalMilliseconds.ToString()
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
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

    private sealed class ComfyUiHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri?.PathAndQuery ?? string.Empty,
                body));
            return respond(request);
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        string PathAndQuery,
        string Body);
}
