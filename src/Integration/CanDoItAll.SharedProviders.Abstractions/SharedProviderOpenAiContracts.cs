using System.Collections.Frozen;
using System.Text.Json.Serialization;

namespace CanDoItAll.SharedProviders.Abstractions;

public static class SharedProviderOpenAiConstants
{
    public const string ListObject = "list";
    public const string ModelObject = "model";
    public const string OwnedBy = "candoitall-shared";
    public const string InvalidRequestErrorType = "invalid_request_error";
    public const string AuthenticationErrorType = "authentication_error";
    public const string PermissionErrorType = "permission_error";
    public const string ConflictErrorType = "conflict_error";
    public const string RateLimitErrorType = "rate_limit_error";
    public const string ApiErrorType = "api_error";
    public const string TimeoutErrorType = "timeout_error";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SharedProviderOpenAiModel
{
    public SharedProviderOpenAiModel(
        SharedProviderRoutingModelId id,
        string @object,
        long created,
        string ownedBy)
    {
        if (!SharedProviderRoutingModelIdCodec.TryParse(id.Value, out _, out _))
        {
            throw new ArgumentException("The OpenAI model routing ID is invalid.", nameof(id));
        }

        if (!string.Equals(@object, SharedProviderOpenAiConstants.ModelObject, StringComparison.Ordinal))
        {
            throw new ArgumentException("The OpenAI model object discriminator is invalid.", nameof(@object));
        }

        if (created < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(created));
        }

        if (!string.Equals(ownedBy, SharedProviderOpenAiConstants.OwnedBy, StringComparison.Ordinal))
        {
            throw new ArgumentException("The OpenAI model owner is invalid.", nameof(ownedBy));
        }

        Id = id;
        Object = @object;
        Created = created;
        OwnedBy = ownedBy;
    }

    [JsonPropertyName("id")]
    public SharedProviderRoutingModelId Id { get; }

    [JsonPropertyName("object")]
    public string Object { get; }

    [JsonPropertyName("created")]
    public long Created { get; }

    [JsonPropertyName("owned_by")]
    public string OwnedBy { get; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SharedProviderOpenAiModelList
{
    public SharedProviderOpenAiModelList(
        string @object,
        IReadOnlyList<SharedProviderOpenAiModel> data)
    {
        if (!string.Equals(@object, SharedProviderOpenAiConstants.ListObject, StringComparison.Ordinal))
        {
            throw new ArgumentException("The OpenAI model-list object discriminator is invalid.", nameof(@object));
        }

        ArgumentNullException.ThrowIfNull(data);
        if (data.Any(model => model is null) ||
            data.Select(model => model.Id).Distinct().Count() != data.Count)
        {
            throw new ArgumentException("The OpenAI model list contains an invalid or duplicate model.", nameof(data));
        }

        Object = @object;
        Data = Array.AsReadOnly(data
            .OrderBy(model => model.Id.Value, StringComparer.Ordinal)
            .ToArray());
    }

    [JsonPropertyName("object")]
    public string Object { get; }

    [JsonPropertyName("data")]
    public IReadOnlyList<SharedProviderOpenAiModel> Data { get; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SharedProviderOpenAiError
{
    public const int MaximumMessageLength = SharedProviderFailure.MaximumMessageLength;
    public const int MaximumParameterLength = SharedProviderFailure.MaximumParameterLength;

    private static readonly FrozenSet<string> AllowedTypes = new[]
    {
        SharedProviderOpenAiConstants.InvalidRequestErrorType,
        SharedProviderOpenAiConstants.AuthenticationErrorType,
        SharedProviderOpenAiConstants.PermissionErrorType,
        SharedProviderOpenAiConstants.ConflictErrorType,
        SharedProviderOpenAiConstants.RateLimitErrorType,
        SharedProviderOpenAiConstants.ApiErrorType,
        SharedProviderOpenAiConstants.TimeoutErrorType
    }.ToFrozenSet(StringComparer.Ordinal);

    public SharedProviderOpenAiError(
        string message,
        string type,
        string? param,
        string code)
    {
        if (!IsBoundedText(message, MaximumMessageLength))
        {
            throw new ArgumentException("The OpenAI error message is invalid.", nameof(message));
        }

        if (!AllowedTypes.Contains(type))
        {
            throw new ArgumentException("The OpenAI error type is invalid.", nameof(type));
        }

        if (param is not null && !IsBoundedText(param, MaximumParameterLength))
        {
            throw new ArgumentException("The OpenAI error parameter is invalid.", nameof(param));
        }

        if (!IsCodeValid(code))
        {
            throw new ArgumentException("The OpenAI error code is invalid.", nameof(code));
        }

        Message = message;
        Type = type;
        Param = param;
        Code = code;
    }

    [JsonPropertyName("message")]
    public string Message { get; }

    [JsonPropertyName("type")]
    public string Type { get; }

    [JsonPropertyName("param")]
    public string? Param { get; }

    [JsonPropertyName("code")]
    public string Code { get; }

    private static bool IsBoundedText(string? value, int maximumLength)
        => value is { Length: > 0 } &&
            value.Length <= maximumLength &&
            value == value.Trim() &&
            !value.Any(char.IsControl);

    private static bool IsCodeValid(string? value)
        => value is { Length: > 0 and <= SharedProviderFailureCode.MaximumLength } &&
            value.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '.' or '_' or '-');
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SharedProviderOpenAiErrorEnvelope
{
    public SharedProviderOpenAiErrorEnvelope(SharedProviderOpenAiError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        Error = error;
    }

    [JsonPropertyName("error")]
    public SharedProviderOpenAiError Error { get; }
}
