using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media;

public sealed class ImageAnalyzeWorkflowExecutor(
    IProviderRuntimeProfileSource providerSource,
    IWorkspaceImageOperationService imageOperationService,
    IAgentImageAnalysisService imageAnalysisService,
    TimeProvider? timeProvider = null) : IWorkflowExecutor
{
    private static readonly ProviderProfileService ProviderFeatureService = new();

    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.ImageAnalyze;

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = WorkflowExecutorJson.Deserialize<WorkflowImageAnalyzeExecutorSettings>(context.SettingsJson);
        if (string.IsNullOrWhiteSpace(settings.Prompt))
        {
            throw new InvalidOperationException("Image-analysis executor setting 'Prompt' is required.");
        }

        if (settings.MaxBytes <= 0)
        {
            throw new InvalidOperationException("Image-analysis executor setting 'MaxBytes' must be greater than zero.");
        }

        var path = WorkflowInputJsonStringResolver.ResolveRequired(
            settings.Path,
            settings.PathJsonPath,
            input,
            "Image-analysis",
            nameof(settings.Path),
            nameof(settings.PathJsonPath));
        var providerSelection = await ResolveProviderAsync(settings, cancellationToken).ConfigureAwait(false);
        var image = await imageOperationService
            .ReadImageFile(path, settings.MaxBytes, WorkflowExecutorIds.ImageAnalyze.Value)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!image.Succeeded)
        {
            throw new InvalidOperationException(CreateImageLoadFailureMessage(image.Message, image.Diagnostics));
        }

        var prompt = settings.Prompt.Trim();
        var clock = timeProvider ?? TimeProvider.System;
        var startedAtUtc = clock.GetUtcNow();
        var invocationId = Guid.NewGuid();
        AgentImageAnalysisResult result;
        try
        {
            result = await imageAnalysisService.AnalyzeAsync(
                new AgentImageAnalysisRequest(
                    providerSelection.Provider,
                    providerSelection.Model,
                    prompt,
                    [new AgentImageAnalysisSource(
                        ResolveSourceName(image.Path),
                        image.ContentType,
                        image.Bytes)],
                    settings.ModelParameterConfigurationJson),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var failedAtUtc = clock.GetUtcNow();
            var unavailable = WorkflowUsageObservationFactory.FromProviderResponseMetrics(
                CreateUsageContext(context, invocationId, startedAtUtc, failedAtUtc),
                providerSelection.Provider,
                providerSelection.Model,
                inputTokens: 0,
                cachedInputTokens: 0,
                outputTokens: 0,
                reasoningTokens: 0,
                totalTokens: 0,
                toolCallCount: 0,
                failedAtUtc);
            throw new WorkflowUsageObservationException(exception.Message, exception, [unavailable]);
        }

        var completedAtUtc = clock.GetUtcNow();
        var resultModel = string.IsNullOrWhiteSpace(result.Model)
            ? providerSelection.Model
            : result.Model.Trim();
        var usageObservation = WorkflowUsageObservationFactory.FromProviderResponseMetrics(
            CreateUsageContext(context, invocationId, startedAtUtc, completedAtUtc),
            providerSelection.Provider,
            resultModel,
            result.InputTokens,
            cachedInputTokens: 0,
            result.OutputTokens,
            reasoningTokens: 0,
            totalTokens: result.InputTokens + result.OutputTokens,
            toolCallCount: 0,
            completedAtUtc);
        IReadOnlyList<WorkflowUsageObservation> usageObservations = [usageObservation];
        var payload = new WorkflowImageAnalyzeExecutorResult(
            Succeeded: true,
            ProviderProfileId: providerSelection.Provider.Id,
            ProviderName: providerSelection.Provider.Name,
            Model: resultModel,
            Path: image.Path,
            Prompt: prompt,
            Analysis: result.Analysis,
            InputTokens: result.InputTokens,
            OutputTokens: result.OutputTokens,
            Format: image.Format,
            ContentType: image.ContentType,
            SizeBytes: image.SizeBytes,
            Width: image.Width,
            Height: image.Height,
            Receipt: image.Receipt);

        return WorkflowExecutorJson.Result(context, payload) with
        {
            Usage = WorkflowUsageCompatibilityProjection.Project(
                usageObservations,
                providerSelection.Provider.Name,
                resultModel),
            UsageObservations = usageObservations
        };
    }

    private async Task<ProviderSelection> ResolveProviderAsync(
        WorkflowImageAnalyzeExecutorSettings settings,
        CancellationToken cancellationToken)
    {
        if (settings.ProviderProfileId is { } providerProfileId)
        {
            var provider = await providerSource
                .GetProviderAsync(providerProfileId, cancellationToken)
                .ConfigureAwait(false);
            if (provider is null)
            {
                throw new InvalidOperationException($"Image-analysis provider '{providerProfileId:D}' was not found.");
            }

            provider = ProviderFeatureService.NormalizeImportedProfile(provider);
            EnsureEnabledChatProvider(provider);
            if (!TryResolveVisionModel(provider, settings.Model, out var model))
            {
                throw new InvalidOperationException(
                    $"Image-analysis provider '{provider.Name}' does not define a vision-capable Chat model for this executor.");
            }

            return new ProviderSelection(provider, model);
        }

        var providers = await providerSource.ListProvidersAsync(cancellationToken).ConfigureAwait(false);
        foreach (var candidate in providers)
        {
            var provider = ProviderFeatureService.NormalizeImportedProfile(candidate);
            if (!provider.IsEnabled || provider.Purpose != ProviderProfilePurpose.Chat)
            {
                continue;
            }

            if (TryResolveVisionModel(provider, settings.Model, out var model))
            {
                return new ProviderSelection(provider, model);
            }
        }

        throw new InvalidOperationException("No enabled vision-capable Chat provider profile is configured for image analysis.");
    }

    private static void EnsureEnabledChatProvider(ProviderProfile provider)
    {
        if (!provider.IsEnabled)
        {
            throw new InvalidOperationException($"Image-analysis provider '{provider.Name}' is disabled.");
        }

        if (provider.Purpose != ProviderProfilePurpose.Chat)
        {
            throw new InvalidOperationException($"Provider '{provider.Name}' is not a Chat provider profile.");
        }
    }

    private static bool TryResolveVisionModel(
        ProviderProfile provider,
        string requestedModel,
        out string model)
    {
        var candidates = string.IsNullOrWhiteSpace(requestedModel)
            ? new[] { provider.DefaultModel }
                .Concat(provider.SuggestedModels)
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .Select(candidate => candidate.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
            : [requestedModel.Trim()];
        foreach (var candidate in candidates)
        {
            if (ProviderFeatureService.ResolveFeatureMatrixForModel(provider, candidate).SupportsVision)
            {
                model = candidate;
                return true;
            }
        }

        model = string.Empty;
        return false;
    }

    private static WorkflowUsageObservationContext CreateUsageContext(
        WorkflowExecutorExecutionContext context,
        Guid invocationId,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc)
        => new(
            context.RunId,
            context.Definition.Id,
            context.Definition.VersionId,
            context.Node.Id,
            context.Descriptor.Id,
            ComponentId: null,
            context.Descriptor.Source.Kind != WorkflowExecutorSourceKind.BuiltIn
                ? WorkflowUsageProducerKind.PluginExecutor
                : WorkflowUsageProducerKind.Executor,
            invocationId,
            Attempt: 1,
            startedAtUtc,
            completedAtUtc);

    private static string ResolveSourceName(string path)
    {
        var fileName = Path.GetFileName(path);
        return string.IsNullOrWhiteSpace(fileName) ? "image" : fileName;
    }

    private static string CreateImageLoadFailureMessage(string message, string diagnostics)
    {
        var detail = string.IsNullOrWhiteSpace(diagnostics) ? message : diagnostics;
        return string.IsNullOrWhiteSpace(detail)
            ? "Image analysis could not load the workspace image."
            : $"Image analysis could not load the workspace image: {detail}";
    }

    private sealed record ProviderSelection(ProviderProfile Provider, string Model);
}
