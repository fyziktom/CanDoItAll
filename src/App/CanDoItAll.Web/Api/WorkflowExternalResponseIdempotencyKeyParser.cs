using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Web.Api;

internal sealed record WorkflowExternalResponseIdempotencyKeyParseResult(
    WorkflowExternalResponseIdempotencyKey? Key,
    string SafeMessage)
{
    public bool Succeeded => Key.HasValue;
}

internal static class WorkflowExternalResponseIdempotencyKeyParser
{
    public const string HeaderName = "Idempotency-Key";
    public const int MaximumLength = 256;

    public static WorkflowExternalResponseIdempotencyKeyParseResult Parse(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Headers.TryGetValue(HeaderName, out var values) ||
            values.Count != 1)
        {
            return Invalid();
        }

        var value = values[0];
        if (string.IsNullOrWhiteSpace(value) ||
            value.Contains(',', StringComparison.Ordinal))
        {
            return Invalid();
        }

        try
        {
            return new WorkflowExternalResponseIdempotencyKeyParseResult(
                new WorkflowExternalResponseIdempotencyKey(value),
                string.Empty);
        }
        catch (ArgumentException)
        {
            return Invalid();
        }

        static WorkflowExternalResponseIdempotencyKeyParseResult Invalid()
            => new(
                Key: null,
                $"{HeaderName} must contain exactly one non-empty value of at most {MaximumLength} characters.");
    }
}
