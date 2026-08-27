using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime;

internal sealed record LlmChatProviderExecutionResolution(
    ProviderProfile Profile,
    LlmChatResolvedProvider Resolved);

internal interface ILlmChatProviderExecutionResolver
{
    Task<Result<LlmChatProviderExecutionResolution>> ResolveExecutionAsync(
        Guid providerProfileId,
        ProviderKind providerKind,
        string model,
        AgentReasoningEffortLevel? thinkingEffort,
        CancellationToken cancellationToken = default);
}

public sealed class CanonicalLlmChatProviderResolver(
    IProviderRuntimeProfileSource providerSource,
    IProviderModelCapabilityResolver capabilityResolver) :
    ILlmChatProviderResolver,
    ILlmChatProviderExecutionResolver
{
    public async Task<Result<LlmChatResolvedProvider>> ResolveAsync(
        Guid providerProfileId,
        string model,
        AgentReasoningEffortLevel? thinkingEffort,
        CancellationToken cancellationToken = default)
    {
        var result = await ResolveCoreAsync(
            providerProfileId,
            null,
            model,
            thinkingEffort,
            cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Result<LlmChatResolvedProvider>.Success(result.Value!.Resolved)
            : Result<LlmChatResolvedProvider>.Failure(result.Errors);
    }

    public async Task<Result<LlmChatResolvedProvider>> ResolveAsync(
        Guid providerProfileId,
        ProviderKind providerKind,
        string model,
        AgentReasoningEffortLevel? thinkingEffort,
        CancellationToken cancellationToken = default)
    {
        var result = await ResolveCoreAsync(
            providerProfileId,
            providerKind,
            model,
            thinkingEffort,
            cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Result<LlmChatResolvedProvider>.Success(result.Value!.Resolved)
            : Result<LlmChatResolvedProvider>.Failure(result.Errors);
    }

    public async Task<Result<IReadOnlyList<LlmChatProviderOption>>> ListOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var providers = await providerSource.ListProvidersAsync(cancellationToken).ConfigureAwait(false);
        var options = new List<LlmChatProviderOption>();
        foreach (var provider in providers
                     .Where(IsAvailableChatProvider)
                     .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            var models = new List<LlmChatModelOption>();
            foreach (var model in ListModels(provider))
            {
                var capabilityResult = ResolveCapability(provider, model, null);
                if (capabilityResult.IsFailure)
                {
                    return Result<IReadOnlyList<LlmChatProviderOption>>.Failure(capabilityResult.Errors);
                }

                var resolved = capabilityResult.Value!;
                models.Add(new LlmChatModelOption(
                    model,
                    new LlmChatThinkingEffortOption(
                        resolved.ThinkingEffortCapability.Status,
                        resolved.ThinkingEffortCapability.ControlMode,
                        [.. resolved.ThinkingEffortCapability.AllowedEfforts],
                        resolved.ProviderDefaultThinkingEffort)) {
                    DisplayName = provider.GetModelDisplayName(model)
                });
            }

            options.Add(new LlmChatProviderOption(
                provider.Id,
                provider.Name,
                provider.Kind,
                models) {
                IsSourceManaged = provider.IsSourceManaged
            });
        }

        return Result<IReadOnlyList<LlmChatProviderOption>>.Success(options);
    }

    async Task<Result<LlmChatProviderExecutionResolution>> ILlmChatProviderExecutionResolver.ResolveExecutionAsync(
        Guid providerProfileId,
        ProviderKind providerKind,
        string model,
        AgentReasoningEffortLevel? thinkingEffort,
        CancellationToken cancellationToken)
        => await ResolveCoreAsync(
            providerProfileId,
            providerKind,
            model,
            thinkingEffort,
            cancellationToken).ConfigureAwait(false);

    private async Task<Result<LlmChatProviderExecutionResolution>> ResolveCoreAsync(
        Guid providerProfileId,
        ProviderKind? providerKind,
        string model,
        AgentReasoningEffortLevel? thinkingEffort,
        CancellationToken cancellationToken)
    {
        if (providerProfileId == Guid.Empty || string.IsNullOrWhiteSpace(model))
        {
            return Failure(LlmChatErrorCodes.InvalidRequest, "A provider profile and model are required.");
        }

        var provider = await providerSource.GetProviderAsync(providerProfileId, cancellationToken)
            .ConfigureAwait(false);
        if (provider is null)
        {
            return Failure(LlmChatErrorCodes.ProviderNotFound, "The configured provider profile was not found.");
        }

        if (!IsAvailableChatProvider(provider))
        {
            return Failure(LlmChatErrorCodes.ProviderUnavailable, "The configured provider profile is unavailable for chat.");
        }

        if (providerKind is { } expectedProviderKind && provider.Kind != expectedProviderKind)
        {
            return Failure(LlmChatErrorCodes.ProviderKindMismatch, "The configured provider kind no longer matches the definition revision.");
        }

        var normalizedModel = model.Trim();
        var canonicalModel = ListModels(provider).FirstOrDefault(candidate =>
            string.Equals(candidate, normalizedModel, StringComparison.Ordinal));
        if (canonicalModel is null)
        {
            return Failure(LlmChatErrorCodes.ModelNotSupported, "The configured model is not available on the selected provider profile.");
        }

        var capability = ResolveCapability(provider, canonicalModel, thinkingEffort);
        return capability.IsSuccess
            ? Result<LlmChatProviderExecutionResolution>.Success(new LlmChatProviderExecutionResolution(
                provider,
                capability.Value!))
            : Result<LlmChatProviderExecutionResolution>.Failure(capability.Errors);
    }

    private Result<LlmChatResolvedProvider> ResolveCapability(
        ProviderProfile provider,
        string model,
        AgentReasoningEffortLevel? thinkingEffort)
    {
        try
        {
            var capability = capabilityResolver.ResolveThinkingEffort(provider, model);
            if (thinkingEffort is { } requestedEffort &&
                (capability.Status != AgentThinkingEffortSupportStatus.Supported ||
                 !capability.AllowedEfforts.Contains(requestedEffort)))
            {
                return Result<LlmChatResolvedProvider>.Failure(Error.Validation(
                    "The configured thinking effort is not supported by the selected provider model.",
                    LlmChatErrorCodes.ThinkingEffortNotSupported));
            }

            var providerDefault = capabilityResolver.ResolveProviderDefaultThinkingEffort(provider, model);
            return Result<LlmChatResolvedProvider>.Success(new LlmChatResolvedProvider(
                provider.Id,
                provider.Name,
                provider.Kind,
                model,
                capability,
                providerDefault));
        }
        catch (InvalidOperationException)
        {
            return Result<LlmChatResolvedProvider>.Failure(Error.Validation(
                "The selected provider model has invalid thinking-effort capability settings.",
                LlmChatErrorCodes.ModelSettingsInvalid));
        }
    }

    private static bool IsAvailableChatProvider(ProviderProfile provider)
        => provider.IsEnabled && provider.Purpose == ProviderProfilePurpose.Chat;

    private static IReadOnlyList<string> ListModels(ProviderProfile provider)
        => provider.SuggestedModels
            .Prepend(provider.DefaultModel)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Select(model => model.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static Result<LlmChatProviderExecutionResolution> Failure(string code, string message)
        => Result<LlmChatProviderExecutionResolution>.Failure(Error.Validation(message, code));
}
