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
        ProviderFailureOperation operation,
        Exception? diagnosticException = null,
        int? diagnosticStatusCode = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var diagnosticFailureType = diagnosticException is
        ProviderFailureBoundaryException
        {
            DiagnosticFailureType: { Length: > 0 } nestedFailureType
        }
            ? nestedFailureType
            : diagnosticException?.GetType().FullName ??
              diagnosticException?.GetType().Name;

        return new ProviderFailureBoundaryException(
            provider.Id,
            operation,
            diagnosticFailureType,
            diagnosticException is ProviderFailureBoundaryException nestedBoundary
                ? nestedBoundary.DiagnosticStatusCode
                : diagnosticStatusCode,
            HasTimeoutCause(diagnosticException));
    }

    private static bool HasTimeoutCause(Exception? exception) {
        for (var cause = exception; cause is not null; cause = cause.InnerException) {
            if (cause is TimeoutException or ProviderFailureBoundaryException { IsTimeout: true }) {
                return true;
            }
        }
        return false;
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
        : this(
            providerId,
            operation,
            diagnosticFailureType: null,
            diagnosticStatusCode: null)
    {
    }

    internal ProviderFailureBoundaryException(
        Guid providerId,
        ProviderFailureOperation operation,
        string? diagnosticFailureType,
        int? diagnosticStatusCode,
        bool isTimeout = false)
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
        DiagnosticFailureType = diagnosticFailureType;
        DiagnosticStatusCode = diagnosticStatusCode;
        IsTimeout = isTimeout;
    }

    public Guid ProviderId { get; }

    public ProviderFailureOperation Operation { get; }

    public string? DiagnosticFailureType { get; }

    public int? DiagnosticStatusCode { get; }

    public bool IsTimeout { get; }
}
