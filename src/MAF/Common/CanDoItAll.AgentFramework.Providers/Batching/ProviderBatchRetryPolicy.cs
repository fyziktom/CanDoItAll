using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.Providers;

internal static class ProviderBatchRetryPolicy {
    public static bool CanRetry(Exception exception) => !HasHistoryFailure(exception, 0);

    private static bool HasHistoryFailure(Exception exception, int depth) {
        if (exception is ProviderHistoryException || depth >= 32) {
            return true;
        }
        if (exception is AggregateException aggregate) {
            return aggregate.InnerExceptions.Any(inner => HasHistoryFailure(inner, depth + 1));
        }
        return exception.InnerException is { } cause && HasHistoryFailure(cause, depth + 1);
    }
}
