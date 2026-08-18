using System.Globalization;
using System.Text;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class LlmChatApiResults
{
    public static IResult FromFailure(IReadOnlyList<Error> errors, Guid? operationId = null)
    {
        var code = errors.FirstOrDefault()?.Code ?? LlmChatErrorCodes.InvalidRequest;
        return Problem(
            StatusCodeFor(code),
            code,
            operationId: operationId,
            retryable: operationId is null ? null : IsRetryable(code));
    }

    public static IResult FromOperationFailure(CanDoItAll.AgentFramework.Llm.SimpleChats.Operations.LlmChatOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var code = string.IsNullOrWhiteSpace(operation.FailureCode)
            ? LlmChatErrorCodes.ProviderUnavailable
            : operation.FailureCode;
        return Problem(
            StatusCodeFor(code),
            code,
            operationId: operation.Id.Value,
            retryable: IsRetryable(code));
    }

    public static bool IsRetryable(string code)
        => code is
            LlmChatErrorCodes.ProviderUnavailable or
            LlmChatErrorCodes.DispatcherUnavailable or
            LlmChatErrorCodes.DeadlineExceeded or
            LlmChatErrorCodes.RuntimeProfileChanged or
            LlmChatErrorCodes.StorageConflict;

    private static int StatusCodeFor(string code)
        => code switch
        {
            LlmChatErrorCodes.DefinitionNotFound or
            LlmChatErrorCodes.ConversationNotFound or
            LlmChatErrorCodes.OperationNotFound => StatusCodes.Status404NotFound,
            LlmChatErrorCodes.DefinitionConcurrencyConflict or
            LlmChatErrorCodes.DefinitionNotActive or
            LlmChatErrorCodes.ConversationArchived or
            LlmChatErrorCodes.TranscriptRevisionConflict or
            LlmChatErrorCodes.ActiveTurnConflict or
            LlmChatErrorCodes.OperationIdConflict or
            LlmChatErrorCodes.OperationRecoveryRequired or
            LlmChatErrorCodes.RuntimeProfileChanged or
            LlmChatErrorCodes.Cancelled or
            LlmChatErrorCodes.StorageConflict => StatusCodes.Status409Conflict,
            LlmChatErrorCodes.ProviderNotFound or
            LlmChatErrorCodes.ProviderKindMismatch or
            LlmChatErrorCodes.ModelNotSupported or
            LlmChatErrorCodes.ModelSettingsInvalid or
            LlmChatErrorCodes.ThinkingEffortNotSupported => StatusCodes.Status422UnprocessableEntity,
            LlmChatErrorCodes.DeadlineExceeded => StatusCodes.Status504GatewayTimeout,
            LlmChatErrorCodes.ProviderUnavailable or
            LlmChatErrorCodes.DispatcherUnavailable => StatusCodes.Status503ServiceUnavailable,
            LlmChatErrorCodes.StorageCorrupted => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status400BadRequest
        };

    public static IResult InvalidRequest(string detail)
        => Problem(StatusCodes.Status400BadRequest, LlmChatErrorCodes.InvalidRequest, detail);

    public static bool TryResolveExpectedConcurrencyToken(
        long? bodyToken,
        string? ifMatch,
        out long expectedToken,
        out IResult? error)
    {
        expectedToken = default;
        error = null;
        long? headerToken = null;
        if (!string.IsNullOrWhiteSpace(ifMatch))
        {
            var normalized = ifMatch.Trim();
            if (normalized.StartsWith("W/", StringComparison.OrdinalIgnoreCase) ||
                normalized.Length < 3 ||
                normalized[0] != '"' ||
                normalized[^1] != '"' ||
                !long.TryParse(normalized[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
                parsed < 0)
            {
                error = InvalidRequest("If-Match must contain one strong numeric LLM Chat ETag.");
                return false;
            }

            headerToken = parsed;
        }

        if (bodyToken is null && headerToken is null)
        {
            error = InvalidRequest("An expected concurrency token or If-Match header is required.");
            return false;
        }

        if (bodyToken is < 0 || bodyToken is { } supplied && headerToken is { } header && supplied != header)
        {
            error = InvalidRequest("The supplied concurrency tokens are invalid or inconsistent.");
            return false;
        }

        expectedToken = bodyToken ?? headerToken!.Value;
        return true;
    }

    public static void SetEtag(HttpResponse response, long concurrencyToken)
        => response.Headers.ETag = $"\"{concurrencyToken.ToString(CultureInfo.InvariantCulture)}\"";

    private static IResult Problem(
        int statusCode,
        string code,
        string? detail = null,
        Guid? operationId = null,
        bool? retryable = null)
    {
        var extensions = new Dictionary<string, object?>
        {
            ["code"] = code
        };
        if (operationId is { } id)
        {
            extensions["operationId"] = id;
        }

        if (retryable is { } canRetry)
        {
            extensions["retryable"] = canRetry;
        }

        return Results.Problem(
            statusCode: statusCode,
            title: Title(statusCode),
            detail: detail,
            type: $"https://candoitall.invalid/problems/{Uri.EscapeDataString(code)}",
            extensions: extensions);
    }

    private static string Title(int statusCode)
        => statusCode switch
        {
            StatusCodes.Status404NotFound => "LLM Chat resource not found",
            StatusCodes.Status409Conflict => "LLM Chat state conflict",
            StatusCodes.Status422UnprocessableEntity => "LLM Chat configuration is not supported",
            StatusCodes.Status503ServiceUnavailable => "LLM Chat provider unavailable",
            StatusCodes.Status504GatewayTimeout => "LLM Chat provider deadline exceeded",
            StatusCodes.Status500InternalServerError => "LLM Chat state is unavailable",
            _ => "Invalid LLM Chat request"
        };
}

internal static class LlmChatApiCursorCodec
{
    private const int Version = 2;

    public static string Encode(LlmChatDefinitionCursor cursor)
        => Encode(CursorKind.Definition, cursor.UpdatedAtUtc.UtcTicks, cursor.DefinitionId.Value);

    public static string Encode(LlmChatConversationCursor cursor)
        => Encode(CursorKind.Conversation, cursor.UpdatedAtUtc.UtcTicks, cursor.ConversationId.Value);

    public static string Encode(LlmChatTranscriptCursor cursor)
        => Encode(CursorKind.Transcript, cursor.Sequence, null);

    public static bool TryDecodeDefinition(string? cursor, out LlmChatDefinitionCursor? position)
    {
        position = null;
        if (!TryDecode(cursor, CursorKind.Definition, out var value, out var id))
        {
            return false;
        }

        if (id is { } definitionId)
        {
            if (!IsValidUtcTicks(value))
            {
                return false;
            }

            position = new LlmChatDefinitionCursor(
                new DateTimeOffset(value, TimeSpan.Zero),
                new LlmChatDefinitionId(definitionId));
        }

        return true;
    }

    public static bool TryDecodeConversation(string? cursor, out LlmChatConversationCursor? position)
    {
        position = null;
        if (!TryDecode(cursor, CursorKind.Conversation, out var value, out var id))
        {
            return false;
        }

        if (id is { } conversationId)
        {
            if (!IsValidUtcTicks(value))
            {
                return false;
            }

            position = new LlmChatConversationCursor(
                new DateTimeOffset(value, TimeSpan.Zero),
                new LlmChatConversationId(conversationId));
        }

        return true;
    }

    public static bool TryDecodeTranscript(string? cursor, out LlmChatTranscriptCursor? position)
    {
        position = null;
        if (!TryDecode(cursor, CursorKind.Transcript, out var value, out _))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            position = new LlmChatTranscriptCursor(value);
        }

        return true;
    }

    private static string Encode(CursorKind kind, long value, Guid? id)
    {
        var payload = string.Join(
            ':',
            Version.ToString(CultureInfo.InvariantCulture),
            ((int)kind).ToString(CultureInfo.InvariantCulture),
            value.ToString(CultureInfo.InvariantCulture),
            id?.ToString("N") ?? string.Empty);
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
    }

    private static bool TryDecode(
        string? cursor,
        CursorKind expectedKind,
        out long value,
        out Guid? id)
    {
        value = 0;
        id = null;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return true;
        }

        try
        {
            var fields = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(cursor)).Split(':');
            if (fields.Length != 4 ||
                !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var version) ||
                version != Version ||
                !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var rawKind) ||
                rawKind != (int)expectedKind ||
                !long.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out value) ||
                value < 1)
            {
                return false;
            }

            if (expectedKind == CursorKind.Transcript)
            {
                return fields[3].Length == 0;
            }

            if (!Guid.TryParseExact(fields[3], "N", out var parsedId) || parsedId == Guid.Empty)
            {
                return false;
            }

            id = parsedId;
            return true;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool IsValidUtcTicks(long value)
        => value >= DateTimeOffset.MinValue.UtcTicks && value <= DateTimeOffset.MaxValue.UtcTicks;

    private enum CursorKind
    {
        Definition = 1,
        Conversation = 2,
        Transcript = 3
    }
}
