using System.Globalization;
using Microsoft.Extensions.Primitives;

namespace CanDoItAll.Web.Api.Streaming;

public static class ServerSentEventCursor
{
    public const string AfterQueryParameterName = "after";
    public const string LastEventIdHeaderName = "Last-Event-ID";

    public static bool TryResolve(
        HttpRequest request,
        out long afterExclusive,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryParseSingleValue(
                request.Query[AfterQueryParameterName],
                $"query parameter '{AfterQueryParameterName}'",
                out var queryCursor,
                out error))
        {
            afterExclusive = 0;
            return false;
        }

        if (!TryParseSingleValue(
                request.Headers[LastEventIdHeaderName],
                $"header '{LastEventIdHeaderName}'",
                out var headerCursor,
                out error))
        {
            afterExclusive = 0;
            return false;
        }

        if (queryCursor.HasValue &&
            headerCursor.HasValue &&
            queryCursor.Value != headerCursor.Value)
        {
            afterExclusive = 0;
            error = $"The '{AfterQueryParameterName}' query parameter and '{LastEventIdHeaderName}' header must identify the same cursor.";
            return false;
        }

        afterExclusive = headerCursor ?? queryCursor ?? 0;
        error = null;
        return true;
    }

    private static bool TryParseSingleValue(
        StringValues values,
        string source,
        out long? cursor,
        out string? error)
    {
        cursor = null;
        error = null;
        if (StringValues.IsNullOrEmpty(values))
        {
            return true;
        }

        if (values.Count != 1)
        {
            error = $"The SSE {source} must be specified at most once.";
            return false;
        }

        var value = values[0]?.Trim();
        if (string.IsNullOrEmpty(value) ||
            !long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < 0)
        {
            error = $"The SSE {source} must be a non-negative integer.";
            return false;
        }

        cursor = parsed;
        return true;
    }
}
