using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.LlmChats.Ui;

public static class LlmChatUiFailureCodes
{
    public const string Prefix = "llm-chat.ui.";
    public const string Forbidden = Prefix + "forbidden";
    public const string InvalidInput = Prefix + "invalid-input";
    public const string RequestFailed = Prefix + "request-failed";
}

public sealed record LlmChatUiFailure(string Code, string Message);

public sealed class LlmChatUiResult<T>
{
    private LlmChatUiResult(T? value, IReadOnlyList<LlmChatUiFailure> failures)
    {
        Value = value;
        Failures = failures;
    }

    public T? Value { get; }

    public IReadOnlyList<LlmChatUiFailure> Failures { get; }

    public bool IsSuccess => Failures.Count == 0;

    public bool IsFailure => !IsSuccess;

    public static LlmChatUiResult<T> Success(T value)
        => new(value, []);

    public static LlmChatUiResult<T> Failure(params LlmChatUiFailure[] failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        if (failures.Length == 0)
        {
            throw new ArgumentException("A failed UI result requires at least one failure.", nameof(failures));
        }

        return new(default, failures.ToArray());
    }
}

internal static class LlmChatUiResultMapper
{
    public static LlmChatUiResult<TTarget> Map<TSource, TTarget>(
        Result<TSource> result,
        Func<TSource, TTarget> map)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(map);
        return result.IsSuccess
            ? LlmChatUiResult<TTarget>.Success(map(result.Value!))
            : LlmChatUiResult<TTarget>.Failure([.. result.Errors.Select(MapFailure)]);
    }

    public static LlmChatUiResult<T> Forbidden<T>(LlmChatUiPermission permission)
        => LlmChatUiResult<T>.Failure(new LlmChatUiFailure(
            LlmChatUiFailureCodes.Forbidden,
            permission switch
            {
                LlmChatUiPermission.Read => "You are not authorized to read Simple Chats.",
                LlmChatUiPermission.Manage => "You are not authorized to manage Simple Chats.",
                LlmChatUiPermission.Execute => "You are not authorized to execute Simple Chats.",
                _ => throw new ArgumentOutOfRangeException(nameof(permission), permission, "Unknown permission.")
            }));

    public static LlmChatUiResult<T> Invalid<T>(string message)
        => LlmChatUiResult<T>.Failure(new LlmChatUiFailure(LlmChatUiFailureCodes.InvalidInput, message));

    public static LlmChatUiFailure FromFailureCode(string code)
        => MapFailure(new Error(code, string.Empty));

    private static LlmChatUiFailure MapFailure(Error error)
    {
        var message = error.Code switch
        {
            LlmChatErrorCodes.InvalidRequest => "Review the Simple Chat values and try again.",
            LlmChatErrorCodes.DefinitionNotFound => "The Simple Chat definition was not found.",
            LlmChatErrorCodes.DefinitionConcurrencyConflict => "The definition changed after it was opened. Reload it before saving again.",
            LlmChatErrorCodes.DefinitionNotActive => "The selected Simple Chat definition is not active.",
            LlmChatErrorCodes.ConversationNotFound => "The Simple Chat conversation was not found.",
            LlmChatErrorCodes.ConversationArchived => "The Simple Chat conversation is archived and read-only.",
            LlmChatErrorCodes.TranscriptRevisionConflict => "The conversation changed after it was loaded. Refresh it before trying again.",
            LlmChatErrorCodes.ActiveTurnConflict => "This conversation already has an active turn.",
            LlmChatErrorCodes.OperationNotFound => "The Simple Chat operation was not found.",
            LlmChatErrorCodes.OperationIdConflict => "The operation identity belongs to a different request.",
            LlmChatErrorCodes.OperationRecoveryRequired => "The active turn requires recovery before it can be abandoned.",
            LlmChatErrorCodes.DispatcherUnavailable => "No Simple Chat executor is available on this host.",
            LlmChatErrorCodes.QueueAgeExceeded => "The Simple Chat request waited too long to start.",
            LlmChatErrorCodes.OperationDurationExceeded => "The Simple Chat request exceeded its allowed duration.",
            LlmChatErrorCodes.ProviderNotFound => "The selected provider is unavailable.",
            LlmChatErrorCodes.ProviderKindMismatch => "The selected provider does not match this definition.",
            LlmChatErrorCodes.ModelNotSupported => "The selected model is unavailable for this provider.",
            LlmChatErrorCodes.ModelSettingsInvalid => "The selected model settings are invalid.",
            LlmChatErrorCodes.ThinkingEffortNotSupported => "The selected thinking effort is unsupported by this model.",
            LlmChatErrorCodes.RuntimeProfileChanged => "The active database profile changed. Reload Simple Chats before continuing.",
            LlmChatErrorCodes.Cancelled => "The Simple Chat request was cancelled.",
            LlmChatErrorCodes.DeadlineExceeded => "The Simple Chat request timed out.",
            LlmChatErrorCodes.ProviderUnavailable => "The selected provider is currently unavailable.",
            LlmChatErrorCodes.StreamLimitExceeded => "The streamed response exceeded its configured limit.",
            LlmChatErrorCodes.StreamCursorInvalid => "The live response can no longer resume from this position. Reload the conversation.",
            LlmChatErrorCodes.StorageConflict => "Simple Chat state changed concurrently. Reload and try again.",
            LlmChatErrorCodes.StorageCorrupted => "Required Simple Chat state is missing or inconsistent.",
            _ => "The Simple Chat request could not be completed."
        };
        var code = error.Code.StartsWith(LlmChatErrorCodes.Prefix, StringComparison.Ordinal)
            ? error.Code
            : LlmChatUiFailureCodes.RequestFailed;
        return new(code, message);
    }
}
