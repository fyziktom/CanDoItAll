using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public enum ProviderFailureOperation
{
    HealthCheck,
    RuntimeRequest
}

public static class ProviderFailureDisclosurePolicy
{
    public const string SanitizedHealthSuccessMessage =
        "The source-managed provider health check succeeded.";

    public const string SanitizedHealthFailureMessage =
        "The source-managed provider health check failed.";

    public const string SanitizedRuntimeFailureMessage =
        "The source-managed provider request failed.";

    public const string SanitizedProfileLookupFailureMessage =
        "The provider profile could not be loaded.";

    public static bool RequiresSanitization(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider.CredentialBinding?.Purpose ==
            ProviderCredentialPurpose.SourceAccessToken;
    }

    public static ProviderHealthResult SanitizeHealthResult(
        ProviderProfile provider,
        ProviderHealthResult result)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(result);
        if (!RequiresSanitization(provider))
        {
            return result;
        }

        return result with
        {
            Summary = result.Success
                ? SanitizedHealthSuccessMessage
                : SanitizedHealthFailureMessage,
            SuggestedModels = provider.SuggestedModels.ToArray(),
            ModelThinkingEffortCapabilities =
                provider.ModelThinkingEffortCapabilities.ToArray()
        };
    }

    public static string SelectMessage(
        ProviderProfile provider,
        ProviderFailureOperation operation,
        string detailedMessage)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(detailedMessage);
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        return RequiresSanitization(provider)
            ? GetSanitizedMessage(operation)
            : detailedMessage;
    }

    public static ProviderFailureBoundaryException CreateBoundaryException(
        ProviderProfile provider,
        ProviderFailureOperation operation)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return new ProviderFailureBoundaryException(provider.Id, operation);
    }

    internal static string GetSanitizedMessage(ProviderFailureOperation operation)
    {
        return operation switch
        {
            ProviderFailureOperation.HealthCheck => SanitizedHealthFailureMessage,
            ProviderFailureOperation.RuntimeRequest => SanitizedRuntimeFailureMessage,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }
}

public sealed class ProviderFailureBoundaryException : InvalidOperationException
{
    public ProviderFailureBoundaryException(
        Guid providerId,
        ProviderFailureOperation operation)
        : base(ProviderFailureDisclosurePolicy.GetSanitizedMessage(operation))
    {
        if (providerId == Guid.Empty)
        {
            throw new ArgumentException(
                "A provider failure boundary requires a provider identity.",
                nameof(providerId));
        }

        ProviderId = providerId;
        Operation = operation;
    }

    public Guid ProviderId { get; }

    public ProviderFailureOperation Operation { get; }
}
