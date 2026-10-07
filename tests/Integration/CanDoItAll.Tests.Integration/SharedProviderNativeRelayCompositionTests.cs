using System.Net;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.SharedProviders.Http;
using Microsoft.Extensions.DependencyInjection;
using SharedProviderConnectorPluginKeys = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys;
using ProviderProfileEditorModel = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "HostPlatform")]
public sealed class SharedProviderNativeRelayCompositionTests {
    [Fact]
    public async Task Native_administration_save_preserves_relay_dispatch_and_canonical_timeout() {
        const string model = "native-saved-model";
        const string credential = "fixture-only-saved-provider-credential";
        var upstream = new UpstreamHandler(SharedProviderRelayOperation.ChatCompletions);
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: true,
            configureServices: services => services.AddHttpClient("SharedProviderRelay")
                .ConfigurePrimaryHttpMessageHandler(() => upstream));
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var secret = await services.GetRequiredService<SecretService>().SaveAsync(new SecretEditorModel {
            Name = "Native relay credential", Kind = SecretKind.ApiKey, SecretValue = credential, Scope = "workspace", MetadataJson = "{}"
        });
        Assert.True(secret.IsSuccess);
        var runtime = services.GetRequiredService<IProviderRuntimeAdministrationService>();
        var providerId = await runtime.SaveProviderAsync(new ProviderProfileEditorModel {
            Name = "Native saved relay", Kind = CanDoItAll.AgentFramework.Models.ProviderKind.OpenAi,
            BaseUrl = "https://upstream.example.test/v1", DefaultModel = model, SuggestedModels = [model],
            ApiKeyEnvironmentVariable = ProviderMetadata.CreateSecretReference(secret.Value),
            Transport = ProviderTransportKind.ChatCompletions, IsEnabled = true, SupportsStreaming = true,
            SupportsTools = true, ConfigurationJson = """{"timeoutSeconds":45}"""
        });
        var administration = services.GetRequiredService<IProviderAdministrationService>();
        var editor = await administration.GetProviderAsync(providerId);
        var saved = await administration.SaveProviderAsync(editor);
        Assert.True(saved.IsSuccess);
        var sharing = services.GetRequiredService<ISharedProviderManagementService>();
        var published = await sharing.SetPublicationAsync(providerId, SharedProviderPublicationAction.Publish, null);
        var publication = Assert.IsType<SharedProviderPublicationWriteResult>(published.Publication);
        var route = SharedProviderRoutingModelIdCodec.Create(publication.PublicId, model);
        var payload = Encoding.UTF8.GetBytes($$"""{"model":"{{route.Value}}","messages":[{"role":"user","content":"hello"}]}""");
        var relay = services.GetRequiredService<ISharedProviderRelayApplicationService>();

        var result = await relay.InvokeAsync(new(SharedProviderRelayOperation.ChatCompletions, payload,
            new("native-saved-relay", "fixture-operator", null, "native-trace", "native-correlation")));

        Assert.IsType<SharedProviderRelayDispatchResult.Buffered>(result);
        Assert.Equal(1, upstream.Calls);
        Assert.Equal(model, upstream.Model);
        Assert.Equal($"Bearer {credential}", upstream.Authorization);
        var descriptor = await services.GetRequiredService<IProviderRuntimeDescriptorStore>().GetRequiredAsync(providerId);
        Assert.Equal(45, descriptor.TimeoutSeconds);
    }

    [Theory]
    [InlineData(SharedProviderConnectorPluginKeys.OpenAi, SharedProviderPurpose.Chat, SharedProviderRelayOperation.ChatCompletions, "/v1/chat/completions")]
    [InlineData(SharedProviderConnectorPluginKeys.OpenAi, SharedProviderPurpose.Chat, SharedProviderRelayOperation.Responses, "/v1/responses")]
    [InlineData(SharedProviderConnectorPluginKeys.OpenAi, SharedProviderPurpose.ImageGeneration, SharedProviderRelayOperation.ImageGenerations, "/v1/images/generations")]
    [InlineData(SharedProviderConnectorPluginKeys.Ollama, SharedProviderPurpose.Chat, SharedProviderRelayOperation.ChatCompletions, "/v1/chat/completions")]
    public async Task Native_runtime_dispatches_exact_model_and_credential_through_registered_driver(
        string connector, SharedProviderPurpose purpose, SharedProviderRelayOperation operation, string expectedPath) {
        const string model = "native-relay-model";
        const string credential = "fixture-only-relay-credential";
        var upstream = new UpstreamHandler(operation);
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: true,
            configureServices: services => services.AddHttpClient("SharedProviderRelay")
                .ConfigurePrimaryHttpMessageHandler(() => upstream));
        await using var scope = host.App.Services.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<IProviderInferenceRelayRuntime>();
        Assert.Equal("AgentFrameworkProviderRuntimeGateway", runtime.GetType().Name);
        var supportCatalog = scope.ServiceProvider.GetRequiredService<ISharedProviderRelaySupportCatalog>();
        Assert.True(supportCatalog.TryGet(connector, purpose, out var descriptor));
        var publicationId = new SharedProviderPublicationId(Guid.NewGuid());
        var routingId = SharedProviderRoutingModelIdCodec.Create(publicationId, model);
        var payload = operation switch {
            SharedProviderRelayOperation.Responses => $$"""{"model":"{{routingId.Value}}","input":"hello"}""",
            SharedProviderRelayOperation.ImageGenerations => $$"""{"model":"{{routingId.Value}}","prompt":"hello"}""",
            _ => $$"""{"model":"{{routingId.Value}}","messages":[{"role":"user","content":"hello"}]}"""
        };
        var normalized = Assert.IsType<SharedProviderRelayRequestPolicyResult.Accepted>(
            new SharedProviderRelayRequestPolicy().Normalize(operation, Encoding.UTF8.GetBytes(payload), descriptor.Support));
        var target = new SharedProviderRelayTarget(publicationId, Guid.NewGuid(), connector, purpose,
            new Uri("https://upstream.example.test/v1"), model, routingId, TimeSpan.FromSeconds(10), "{}",
            new SharedProviderRelayCredential(credential), descriptor.Support);
        var dispatcher = scope.ServiceProvider.GetRequiredService<ISharedProviderRelayDispatcher>();

        var result = await dispatcher.DispatchAsync(new(target, normalized.Request));

        Assert.IsType<SharedProviderRelayDispatchResult.Buffered>(result);
        Assert.Equal(1, upstream.Calls);
        Assert.Equal(expectedPath, upstream.Path);
        Assert.Equal(model, upstream.Model);
        Assert.Equal($"Bearer {credential}", upstream.Authorization);
    }

    private sealed class UpstreamHandler(SharedProviderRelayOperation operation) : HttpMessageHandler {
        public int Calls { get; private set; }
        public string? Path { get; private set; }
        public string? Model { get; private set; }
        public string? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Calls++;
            Path = request.RequestUri!.AbsolutePath;
            Authorization = request.Headers.Authorization?.ToString();
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Model = payload.RootElement.GetProperty("model").GetString();
            var response = operation switch {
                SharedProviderRelayOperation.Responses => $$"""{"id":"resp-native","object":"response","status":"completed","model":"{{Model}}","output":[]}""",
                SharedProviderRelayOperation.ImageGenerations => """{"created":1787533200,"data":[{"b64_json":"AQID"}]}""",
                _ => $$"""{"id":"chatcmpl-native","object":"chat.completion","model":"{{Model}}","choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}]}"""
            };
            return new(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
}
