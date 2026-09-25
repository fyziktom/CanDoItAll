using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using static CanDoItAll.Modules.Processes.ProcessAgentRightsDiagnosticPolicy;

namespace CanDoItAll.Modules.Processes;

internal enum ProcessAgentFailureKind {
    Unknown,
    Transient,
    PermanentProvider,
    RightsBoundary,
    Canceled,
    OutputContract
}

internal static class ProcessRuntimeFailureClassifier {
    private const int MaximumInspectedTextLength = 8192;
    private const int MaximumInspectedExceptions = 32;
    private static readonly Regex HttpStatusPattern = new(
        @"\b(?:status(?:\s+code)?\s*[:=]?\s*|http\s+)([1-5][0-9]{2})\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    internal static bool LooksLikeAgentOutputContractFailure(Exception exception) =>
        Classify(exception) == ProcessAgentFailureKind.OutputContract;

    internal static bool LooksLikeTransientAgentExecutionFailure(Exception exception) =>
        Classify(exception) == ProcessAgentFailureKind.Transient;

    internal static bool LooksLikeTransientAgentExecutionFailure(string text) =>
        Classify(text) == ProcessAgentFailureKind.Transient;

    internal static ProcessAgentFailureKind Classify(Exception exception) {
        ArgumentNullException.ThrowIfNull(exception);
        var causes = EnumerateCauses(exception).ToArray();
        if (causes.Any(cause => cause is OperationCanceledException && cause.InnerException is not TimeoutException)) {
            return ProcessAgentFailureKind.Canceled;
        }
        if (causes.Any(cause => cause is AgentToolPolicyBlockedException)) {
            return ProcessAgentFailureKind.RightsBoundary;
        }
        var origins = causes.OfType<AgentRuntimeUsageException>().Select(cause => cause.FailureOrigin).ToArray();
        if (origins.Contains(AgentRuntimeFailureOrigin.Tool)) {
            return ProcessAgentFailureKind.Unknown;
        }
        if (origins.Contains(AgentRuntimeFailureOrigin.ProviderConfiguration) ||
            causes.OfType<AgentRunFailedException>().Any(cause => cause.FailureCategory is
                AgentProviderFailureCategory.ProviderConfiguration or AgentProviderFailureCategory.QuotaOrBilling or
                AgentProviderFailureCategory.RequestCompatibility)) {
            return ProcessAgentFailureKind.PermanentProvider;
        }

        var messageKinds = causes.Select(cause => Classify(cause.Message)).ToArray();
        if (messageKinds.Contains(ProcessAgentFailureKind.RightsBoundary)) {
            return ProcessAgentFailureKind.RightsBoundary;
        }
        if (messageKinds.Contains(ProcessAgentFailureKind.PermanentProvider)) {
            return ProcessAgentFailureKind.PermanentProvider;
        }

        var statuses = causes.Select(cause => cause switch {
            AgentRuntimeUsageException { ProviderStatusCode: { } status } => (int?)status,
            HttpRequestException { StatusCode: { } status } => (int)status,
            ProviderFailureBoundaryException boundary => boundary.DiagnosticStatusCode,
            _ => null
        }).Where(status => status is >= 100 and <= 599).Select(status => status!.Value).ToArray();
        if (statuses.Length > 0) {
            return ClassifyStatuses(statuses);
        }
        if (origins.Contains(AgentRuntimeFailureOrigin.Finalizer) && !origins.Contains(AgentRuntimeFailureOrigin.Provider)) {
            return ProcessAgentFailureKind.OutputContract;
        }
        if (causes.Any(cause => cause is TimeoutException or ProviderFailureBoundaryException { IsTimeout: true } or
            HttpRequestException { StatusCode: null })) {
            return ProcessAgentFailureKind.Transient;
        }
        var text = string.Join(" ", causes.Select(cause => LimitDiagnosticText(cause.Message, MaximumInspectedTextLength)));
        var kind = Classify(text);
        return kind == ProcessAgentFailureKind.OutputContract && origins.Contains(AgentRuntimeFailureOrigin.Provider)
            ? ProcessAgentFailureKind.Unknown
            : kind;
    }

    internal static ProcessAgentFailureKind Classify(string text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return ProcessAgentFailureKind.Unknown;
        }
        text = text[..Math.Min(text.Length, MaximumInspectedTextLength)];
        if (ContainsAny(text, "PolicyDenied", "blocked by policy", "permission denied", "access denied",
            "not authorized to use tool", "missing tool", "workspace boundary", "outside the current run boundary")) {
            return ProcessAgentFailureKind.RightsBoundary;
        }
        if (ContainsAny(text, "unauthorized", "invalid api key", "invalid_api_key", "insufficient_quota",
            "authentication failed", "model_not_found", "model does not exist")) {
            return ProcessAgentFailureKind.PermanentProvider;
        }
        var statuses = HttpStatusPattern.Matches(text).Select(match =>
            int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)).ToArray();
        if (statuses.Length > 0) {
            return ClassifyStatuses(statuses);
        }
        if (ContainsAny(text, "temporarily unavailable", "temporary failure", "transient", "rate limit",
            "timeout", "timed out", "connection reset", "connection refused", "transport error") &&
            (!LooksLikeRightsOrToolBoundary(text) || LooksLikeProviderRuntimeTransientFailure(text))) {
            return ProcessAgentFailureKind.Transient;
        }
        return ContainsAny(text, "submit_process_step_outcome", "Required finalizer tool", "process_step_outcome_result",
            "ProcessStepOutcomeResult", "process.step_outcome", "agent.finalizer", "agent.output")
            ? ProcessAgentFailureKind.OutputContract
            : ProcessAgentFailureKind.Unknown;
    }

    private static ProcessAgentFailureKind ClassifyStatuses(IReadOnlyList<int> statuses) {
        if (statuses.Any(status => status is (int)HttpStatusCode.BadRequest or (int)HttpStatusCode.Unauthorized or
            (int)HttpStatusCode.PaymentRequired or (int)HttpStatusCode.Forbidden or (int)HttpStatusCode.UnprocessableEntity)) {
            return ProcessAgentFailureKind.PermanentProvider;
        }
        return statuses.All(status => status is (int)HttpStatusCode.RequestTimeout or (int)HttpStatusCode.TooManyRequests or
            (int)HttpStatusCode.InternalServerError or (int)HttpStatusCode.BadGateway or (int)HttpStatusCode.ServiceUnavailable or
            (int)HttpStatusCode.GatewayTimeout or 520 or 529)
            ? ProcessAgentFailureKind.Transient
            : ProcessAgentFailureKind.Unknown;
    }

    internal static bool LooksLikeProviderRuntimeTransientFailure(string text) => ContainsAny(text,
        "initialization timed out", "initialisation timed out");

    private static IEnumerable<Exception> EnumerateCauses(Exception exception) {
        var pending = new Stack<Exception>();
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        pending.Push(exception);
        while (pending.Count > 0 && visited.Count < MaximumInspectedExceptions) {
            var current = pending.Pop();
            if (!visited.Add(current)) {
                continue;
            }
            yield return current;
            if (current is AggregateException aggregate) {
                foreach (var inner in aggregate.InnerExceptions) {
                    pending.Push(inner);
                }
            } else if (current.InnerException is { } inner) {
                pending.Push(inner);
            }
        }
    }

    internal static string LimitDiagnosticText(string text, int maxLength = 800) {
        var normalized = text.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
    }
}
