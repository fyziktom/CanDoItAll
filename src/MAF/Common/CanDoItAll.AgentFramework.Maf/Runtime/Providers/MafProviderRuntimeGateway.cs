using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.ProviderHistory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.Maf;

internal interface IMafProviderRuntimeGateway
{
    Task<ProviderHealthResult> TestProviderAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken = default);

    Task<ProviderTestChatResult> RunProviderTestChatAsync(
        ProviderProfile provider,
        ProviderTestChatRequest request,
        string model,
        CancellationToken cancellationToken = default);

    Task<ProviderTestChatResult> RunProviderImageChatAsync(
        ProviderProfile provider,
        ProviderTestChatRequest request,
        string model,
        IReadOnlyList<ProviderChatAttachment> attachments,
        string modelParameterConfigurationJson = "",
        CancellationToken cancellationToken = default);

    Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
        ProviderProfile provider,
        ProviderModelMaintenanceEditorRequest request,
        CancellationToken cancellationToken = default);
}

public static class MafProviderRuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddMafProviderRuntimeServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMafRuntimeArchitectureServices();
        services.TryAddSingleton<IProviderRuntimeDescriptorStore, ProviderProfileRuntimeDescriptorStore>();
        services.TryAddSingleton<IProviderRuntimeDescriptorSource>(
            serviceProvider => serviceProvider.GetRequiredService<IProviderRuntimeDescriptorStore>());
        services.TryAddSingleton<IProviderDriverCredentialResolver, MafProviderDriverCredentialResolver>();
        services.TryAddSingleton<MafProviderDriverHttpClientPool>();
        services.TryAddSingleton<IProviderHistorySecrets, ProviderHistorySecrets>();
        services.TryAddSingleton<IProviderHistoryRecorder, MissingProviderHistoryRecorder>();
        services.TryAddSingleton<IAgentProviderFactory>(serviceProvider =>
        {
            return new HistoryProviderDriverFactory(CreateDefaultProviderFactory(
                serviceProvider.GetRequiredService<MafProviderDriverHttpClientPool>(),
                serviceProvider.GetRequiredService<IProviderDriverCredentialResolver>(),
                serviceProvider.GetService<IProviderHttpClientSelector>(),
                serviceProvider.GetService<IProviderInferenceRelayTransport>()),
                serviceProvider.GetRequiredService<IProviderHistoryRecorder>(),
                serviceProvider.GetRequiredService<TimeProvider>());
        });
        services.TryAddSingleton<IProviderDispatchLaneGate, ProviderDispatchLaneGate>();
        services.TryAddSingleton<IMafProviderStreamingDispatchGate, MafProviderStreamingDispatchGate>();
        services.TryAddSingleton<IMafProviderAgentFactory, MafProviderAgentFactory>();
        services.TryAddSingleton<IProviderRuntimeHandleFactory, ProviderRuntimeHandleFactory>();
        services.TryAddSingleton<IProviderRuntimePool, ProviderRuntimePool>();
        services.TryAddSingleton<IProviderBatchJobBalancer, ProviderBatchJobBalancer>();
        services.TryAddSingleton<IAgentImageAnalysisService, ProviderRuntimeImageAnalysisService>();
        services.TryAddSingleton<IAgentImageGenerationService, ProviderRuntimeImageGenerationService>();
        services.TryAddSingleton<IMafProviderRuntimeGateway, MafProviderRuntimeGateway>();

        return services;
    }

    private static IAgentProviderFactory CreateDefaultProviderFactory(
        MafProviderDriverHttpClientPool httpClients,
        IProviderDriverCredentialResolver credentialResolver,
        IProviderHttpClientSelector? httpClientSelector,
        IProviderInferenceRelayTransport? inferenceRelayTransport)
    {
        return new AgentProviderDriverRegistryBuilder()
            .AddOpenAiProviderDriver(
                httpClients.OpenAi,
                credentialResolver,
                httpClientSelector,
                inferenceRelayTransport)
            .AddAzureOpenAiProviderDriver(httpClients.AzureOpenAi, credentialResolver)
            .AddOllamaProviderDriver(httpClients.Ollama, inferenceRelayTransport)
            .AddComfyUiProviderDriver(httpClients.ComfyUi)
            .Build();
    }
}

internal sealed class MafProviderRuntimeGateway(
    IProviderRuntimeDescriptorStore descriptorStore,
    IProviderRuntimePool runtimePool) : IMafProviderRuntimeGateway
{
    public async Task<ProviderHealthResult> TestProviderAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var handle = await GetRuntimeHandleAsync(provider, cancellationToken).ConfigureAwait(false);
        var query = new ProviderDispatchQuery(
            provider,
            AgentProviderCapabilityKind.Health,
            AgentProviderOperationKind.TestHealth,
            ResolveModel(provider, null));
        return await handle.DispatchAsync(
            new ProviderRuntimeDispatchRequest<ProviderProfile>(query, provider),
            async (context, token) =>
            {
                EnsureProviderKindMatches(context.Descriptor, context.Query.Provider);
                var driver = handle.ProviderFactory.Resolve<IProviderHealthDriver>(context.Query.Provider.Kind);
                return await driver.TestHealthAsync(context.Payload, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProviderTestChatResult> RunProviderTestChatAsync(
        ProviderProfile provider,
        ProviderTestChatRequest request,
        string model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Prompt) && (request.Messages is null || request.Messages.Count == 0))
        {
            throw new InvalidOperationException("Add a prompt before running the provider test chat.");
        }

        var handle = await GetRuntimeHandleAsync(provider, cancellationToken).ConfigureAwait(false);
        var query = new ProviderDispatchQuery(
            provider,
            AgentProviderCapabilityKind.ChatCompletion,
            AgentProviderOperationKind.CompleteChat,
            model);
        var payload = new ProviderChatCompletionRequest(
            provider,
            model,
            string.IsNullOrWhiteSpace(request.SystemPrompt)
                ? "You are validating a provider profile. Reply clearly and directly to the user's request."
                : request.SystemPrompt.Trim(),
            request.Messages ?? [],
            request.Prompt) { History = request.History };
        var result = await handle.DispatchAsync(
            new ProviderRuntimeDispatchRequest<ProviderChatCompletionRequest>(query, payload),
            async (context, token) =>
            {
                EnsureProviderKindMatches(context.Descriptor, context.Query.Provider);
                var driver = handle.ProviderFactory.Resolve<IProviderChatCompletionDriver>(context.Query.Provider.Kind);
                return await driver.CompleteChatAsync(context.Payload, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(result.ResponseText))
        {
            throw new InvalidOperationException("The provider returned an empty response to the test chat.");
        }

        return new ProviderTestChatResult(
            result.Model,
            result.ResponseText,
            result.InputTokens,
            result.OutputTokens);
    }

    public async Task<ProviderTestChatResult> RunProviderImageChatAsync(
        ProviderProfile provider,
        ProviderTestChatRequest request,
        string model,
        IReadOnlyList<ProviderChatAttachment> attachments,
        string modelParameterConfigurationJson = "",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(request);

        if (attachments.Count == 0)
        {
            throw new InvalidOperationException("Add at least one image attachment before running provider image chat.");
        }

        if (string.IsNullOrWhiteSpace(request.Prompt) && (request.Messages is null || request.Messages.Count == 0))
        {
            throw new InvalidOperationException("Add a prompt before running provider image chat.");
        }

        var handle = await GetRuntimeHandleAsync(provider, cancellationToken).ConfigureAwait(false);
        var query = new ProviderDispatchQuery(
            provider,
            AgentProviderCapabilityKind.ChatCompletion,
            AgentProviderOperationKind.AnalyzeImage,
            model);
        var payload = new ProviderChatCompletionRequest(
            provider,
            model,
            string.IsNullOrWhiteSpace(request.SystemPrompt) ? string.Empty : request.SystemPrompt.Trim(),
            request.Messages ?? [],
            request.Prompt,
            attachments,
            modelParameterConfigurationJson) { History = request.History };
        var result = await handle.DispatchAsync(
            new ProviderRuntimeDispatchRequest<ProviderChatCompletionRequest>(query, payload),
            async (context, token) =>
            {
                EnsureProviderKindMatches(context.Descriptor, context.Query.Provider);
                var driver = handle.ProviderFactory.Resolve<IProviderChatCompletionDriver>(context.Query.Provider.Kind);
                return await driver.CompleteChatAsync(context.Payload, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(result.ResponseText))
        {
            throw new InvalidOperationException("The provider returned an empty image analysis response.");
        }

        return new ProviderTestChatResult(
            result.Model,
            result.ResponseText,
            result.InputTokens,
            result.OutputTokens);
    }


    public async Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
        ProviderProfile provider,
        ProviderModelMaintenanceEditorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(request);

        var baseModel = OllamaModelfileBuilder.NormalizeRequired(request.BaseModel, "Provider base model");
        var targetModel = OllamaModelfileBuilder.NormalizeRequired(request.TargetModel, "Provider target model");
        var systemPrompt = OllamaModelfileBuilder.NormalizeRequired(request.SystemPrompt, "Provider system prompt");
        var contextLength = OllamaModelfileBuilder.ValidateContextLength(request.ContextLength);
        var handle = await GetRuntimeHandleAsync(provider, cancellationToken).ConfigureAwait(false);
        var query = new ProviderDispatchQuery(
            provider,
            AgentProviderCapabilityKind.ModelMaintenance,
            AgentProviderOperationKind.CreateOrUpdateModel,
            targetModel);
        var payload = new ProviderModelMaintenanceRequest(
            provider,
            targetModel,
            baseModel,
            systemPrompt,
            contextLength);
        var result = await handle.DispatchAsync(
            new ProviderRuntimeDispatchRequest<ProviderModelMaintenanceRequest>(query, payload),
            async (context, token) =>
            {
                EnsureProviderKindMatches(context.Descriptor, context.Query.Provider);
                var driver = handle.ProviderFactory.Resolve<IProviderModelMaintenanceDriver>(context.Query.Provider.Kind);
                return await driver.CreateOrUpdateModelAsync(context.Payload, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        return new ProviderModelMaintenanceEditorResult(
            result.Model,
            result.BaseModel,
            result.SystemPrompt,
            result.ContextLength,
            result.Definition,
            result.StatusMessage);
    }

    private async ValueTask<IProviderRuntimeHandle> GetRuntimeHandleAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken)
    {
        descriptorStore.Upsert(provider, secretReferenceIdentity: provider.ApiKeyEnvironmentVariable);
        return await runtimePool.GetRequiredAsync(provider.Id, cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureProviderKindMatches(
        ProviderRuntimeDescriptor descriptor,
        ProviderProfile provider)
    {
        if (descriptor.ProviderKind != provider.Kind)
        {
            throw new InvalidOperationException("Provider runtime descriptor kind does not match the request provider kind.");
        }
    }

    private static string ResolveModel(
        ProviderProfile provider,
        string? requestedModel)
    {
        if (!string.IsNullOrWhiteSpace(requestedModel))
        {
            return requestedModel.Trim();
        }

        if (!string.IsNullOrWhiteSpace(provider.DefaultModel))
        {
            return provider.DefaultModel.Trim();
        }

        return provider.SuggestedModels.FirstOrDefault(model => !string.IsNullOrWhiteSpace(model))?.Trim()
            ?? string.Empty;
    }
}

internal sealed class MafProviderDriverCredentialResolver(IAgentProviderCredentialResolver credentialResolver) : IProviderDriverCredentialResolver
{
    public ProviderDriverCredential Resolve(ProviderProfile provider)
    {
        var credential = credentialResolver.Resolve(provider);
        return credential.IsResolved
            ? ProviderDriverCredential.Resolved(credential.ApiKey)
            : ProviderDriverCredential.Missing(credential.FailureMessage);
    }
}

internal sealed class MafProviderDriverHttpClientPool : IDisposable
{
    public HttpClient OpenAi { get; } = CreateClient();

    public HttpClient AzureOpenAi { get; } = CreateClient();

    public HttpClient Ollama { get; } = CreateClient();

    public HttpClient ComfyUi { get; } = CreateClient();

    public void Dispose()
    {
        OpenAi.Dispose();
        AzureOpenAi.Dispose();
        Ollama.Dispose();
        ComfyUi.Dispose();
    }

    private static HttpClient CreateClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
    }
}
