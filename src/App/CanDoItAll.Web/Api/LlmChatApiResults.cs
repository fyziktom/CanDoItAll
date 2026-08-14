using System.Globalization;
using System.Text;
using CanDoItAll.Modules.LlmChats.Common;
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

    public static IResult FromOperationFailure(CanDoItAll.Modules.LlmChats.Operations.LlmChatOperation operation)
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
            LlmChatErrorCodes.ProviderUnavailable => StatusCodes.Status503ServiceUnavailable,
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
    private const string Prefix = "v1:";

    public static string Encode(int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        var bytes = Encoding.UTF8.GetBytes($"{Prefix}{offset.ToString(CultureInfo.InvariantCulture)}");
        return WebEncoders.Base64UrlEncode(bytes);
    }

    public static bool TryDecode(string? cursor, out int offset)
    {
        offset = 0;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return true;
        }

        try
        {
            var value = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(cursor));
            return value.StartsWith(Prefix, StringComparison.Ordinal) &&
                   int.TryParse(value[Prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out offset) &&
                   offset >= 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
