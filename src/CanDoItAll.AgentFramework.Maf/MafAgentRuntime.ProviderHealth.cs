using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
{
    public Task<ProviderHealthResult> TestProviderAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken = default)
    {
        return provider.Kind switch
        {
            ProviderKind.OpenAi => TestOpenAiProviderAsync(provider, cancellationToken),
            ProviderKind.AzureOpenAi => TestAzureOpenAiProviderAsync(provider, cancellationToken),
            ProviderKind.Ollama => TestOllamaProviderAsync(provider, cancellationToken),
            _ => Task.FromResult(new ProviderHealthResult(false, $"Unsupported provider kind '{provider.Kind}'.", provider.SuggestedModels))
        };
    }

    public async Task<ProviderTestChatResult> RunProviderTestChatAsync(
        ProviderProfile provider,
        ProviderTestChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var model = ResolveProviderTestModel(provider, request.Model);
        var instructions = string.IsNullOrWhiteSpace(request.SystemPrompt)
            ? "You are validating a provider profile. Reply clearly and directly to the user's request."
            : request.SystemPrompt.Trim();

        var options = new ChatClientAgentOptions
        {
            Name = "Provider Test Chat",
            Description = "Runs a lightweight provider validation conversation without changing sandbox chat history.",
            ChatOptions = new()
            {
                Instructions = instructions,
                Temperature = 0.2f
            }
        };

        var frameworkManagedHistory = provider.PreferFrameworkManagedChatHistory || !SupportsServiceManagedConversations(provider);
        var agent = CreateFrameworkAgent(provider, model, options, frameworkManagedHistory);
        var session = await agent.CreateSessionAsync(cancellationToken);
        var inputMessages = request.Messages
            .OrderBy(item => item.CreatedAtUtc)
            .Where(message => !string.IsNullOrWhiteSpace(message.Content))
            .Select(message => new ChatMessage(MapRole(message.Role), message.Content.Trim()))
            .ToList();

        if (!string.IsNullOrWhiteSpace(request.Prompt))
        {
            inputMessages.Add(new ChatMessage(ChatRole.User, request.Prompt.Trim()));
        }

        if (inputMessages.Count == 0)
        {
            throw new InvalidOperationException("Add a prompt before running the provider test chat.");
        }

        var updates = new List<AgentResponseUpdate>();
        var runOptions = new ChatClientAgentRunOptions(new ChatOptions
        {
            Temperature = 0.2f,
            AllowMultipleToolCalls = false
        })
        {
            AllowBackgroundResponses = false
        };

        await foreach (var update in RunStreamingAsync(agent, session, inputMessages, runOptions, cancellationToken))
        {
            updates.Add(SnapshotUpdate(update));
        }

        var response = updates.ToAgentResponse();
        var responseText = ResolveResponseText(response, []);
        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new InvalidOperationException("The provider returned an empty response to the test chat.");
        }

        return new ProviderTestChatResult(
            model,
            responseText,
            (int)(response.Usage?.InputTokenCount ?? 0),
            (int)(response.Usage?.OutputTokenCount ?? 0));
    }

    public async Task<OllamaModelfileResult> CreateOrUpdateOllamaModelAsync(
        ProviderProfile provider,
        OllamaModelfileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (provider.Kind != ProviderKind.Ollama)
        {
            throw new InvalidOperationException("Modelfile creation is only available for Ollama providers.");
        }

        var baseModel = OllamaModelfileBuilder.NormalizeRequired(request.BaseModel, "Ollama base model");
        var targetModel = OllamaModelfileBuilder.NormalizeRequired(request.TargetModel, "Ollama target model");
        var systemPrompt = OllamaModelfileBuilder.NormalizeRequired(request.SystemPrompt, "Ollama system prompt");
        var contextLength = OllamaModelfileBuilder.ValidateContextLength(request.ContextLength);

        using var response = await HttpClient.PostAsJsonAsync(
            $"{provider.BaseUrl.TrimEnd('/')}/api/create",
            new
            {
                model = targetModel,
                from = baseModel,
                system = systemPrompt,
                parameters = new
                {
                    num_ctx = contextLength
                },
                stream = false
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OllamaCreateResponse>(SerializerOptions, cancellationToken);
        if (!string.IsNullOrWhiteSpace(payload?.Error))
        {
            throw new InvalidOperationException(payload.Error);
        }

        return new OllamaModelfileResult(
            targetModel,
            baseModel,
            systemPrompt,
            contextLength,
            OllamaModelfileBuilder.Build(baseModel, systemPrompt, contextLength),
            payload?.Status);
    }

    private Task<ProviderHealthResult> TestOpenAiProviderAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken)
    {
        return TestCloudProviderAsync(
            provider,
            fallbackModel: "gpt-4o-mini",
            providerLabel: provider.Transport == ProviderTransportKind.Responses ? "OpenAI Responses" : "OpenAI Chat Completions",
            cancellationToken);
    }

    private Task<ProviderHealthResult> TestAzureOpenAiProviderAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken)
    {
        return TestCloudProviderAsync(
            provider,
            fallbackModel: "gpt-4o-mini",
            providerLabel: provider.Transport == ProviderTransportKind.Responses ? "Azure OpenAI Responses" : "Azure OpenAI Chat Completions",
            cancellationToken);
    }

    private async Task<ProviderHealthResult> TestOllamaProviderAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await HttpClient.GetAsync($"{provider.BaseUrl.TrimEnd('/')}/api/tags", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new ProviderHealthResult(false, $"Ollama health check returned {(int)response.StatusCode}.", provider.SuggestedModels);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<OllamaTagsResponse>(stream, SerializerOptions, cancellationToken);
            var modelNames = payload?.Models?
                .Select(item => item.Name)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? provider.SuggestedModels.ToList();

            var model = ResolveHealthCheckModel(provider, modelNames, "qwen3.5:9b");
            var agent = CreateHealthProbeAgent(provider, model);

            await RunProviderProbeAsync(agent, cancellationToken);

            return new ProviderHealthResult(
                true,
                $"Ollama responded to /api/tags and completed a Microsoft Agent Framework probe run with model '{model}'.",
                modelNames);
        }
        catch (Exception exception)
        {
            return new ProviderHealthResult(false, $"Ollama health check failed: {exception.Message}", provider.SuggestedModels);
        }
    }

    private async Task<ProviderHealthResult> TestCloudProviderAsync(
        ProviderProfile provider,
        string fallbackModel,
        string providerLabel,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var credential = ResolveProviderCredential(provider);
        var suggestedModels = provider.SuggestedModels.Count > 0
            ? provider.SuggestedModels
            : [ResolveHealthCheckModel(provider, [], fallbackModel)];

        if (!credential.IsResolved)
        {
            return new ProviderHealthResult(false, credential.FailureMessage, suggestedModels);
        }

        try
        {
            var model = ResolveHealthCheckModel(provider, suggestedModels, fallbackModel);
            var agent = CreateHealthProbeAgent(provider, model);

            await RunProviderProbeAsync(agent, cancellationToken);

            return new ProviderHealthResult(
                true,
                $"{providerLabel} completed a Microsoft Agent Framework probe run with model '{model}'.",
                suggestedModels);
        }
        catch (Exception exception)
        {
            return new ProviderHealthResult(false, $"{providerLabel} health check failed: {exception.Message}", suggestedModels);
        }
    }

    private AIAgent CreateHealthProbeAgent(ProviderProfile provider, string model)
    {
        var options = new ChatClientAgentOptions
        {
            Name = "Provider Health Probe",
            Description = "Verifies that the configured provider can execute a simple Microsoft Agent Framework run.",
            ChatOptions = new()
            {
                Instructions = "Reply with a short confirmation.",
                Temperature = 0
            }
        };

        return CreateFrameworkAgent(provider, model, options, frameworkManagedHistory: false);
    }

    private static async Task RunProviderProbeAsync(AIAgent agent, CancellationToken cancellationToken)
    {
        var response = await agent.RunAsync("Reply with the single word OK.", cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(response.Text))
        {
            throw new InvalidOperationException("The provider returned an empty response to the probe run.");
        }
    }

    private static string ResolveProviderTestModel(ProviderProfile provider, string? requestedModel)
    {
        if (!string.IsNullOrWhiteSpace(requestedModel))
        {
            return requestedModel.Trim();
        }

        return provider.Kind switch
        {
            ProviderKind.Ollama => ResolveHealthCheckModel(provider, provider.SuggestedModels, "qwen3.5:9b"),
            _ => ResolveHealthCheckModel(provider, provider.SuggestedModels, "gpt-4o-mini")
        };
    }

    private sealed class OllamaTagsResponse
    {
        public List<OllamaTagModel> Models { get; set; } = [];
    }

    private sealed class OllamaTagModel
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class OllamaCreateResponse
    {
        public string? Status { get; set; }

        public string? Error { get; set; }
    }
}
