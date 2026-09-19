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

/// <summary>
/// One shared model in the OpenAI-style model list of <c>GET /api/shared-providers/openai/v1/models</c>.
/// </summary>
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

    /// <summary>Routing identifier of the model; send it as <c>model</c> in the inference operations.</summary>
    [JsonPropertyName("id")]
    public SharedProviderRoutingModelId Id { get; }

    /// <summary>Object type, always <c>model</c>.</summary>
    [JsonPropertyName("object")]
    public string Object { get; }

    /// <summary>Creation time as Unix seconds; always 0, because the catalog records no creation time.</summary>
    [JsonPropertyName("created")]
    public long Created { get; }

    /// <summary>Owner of the model, always <c>candoitall-shared</c>.</summary>
    [JsonPropertyName("owned_by")]
    public string OwnedBy { get; }
}

/// <summary>
/// OpenAI-style list of every model the host shares, returned by <c>GET /api/shared-providers/openai/v1/models</c>.
/// </summary>
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

    /// <summary>Object type, always <c>list</c>.</summary>
    [JsonPropertyName("object")]
    public string Object { get; }

    /// <summary>
    /// The models of all shared publications, including models not suggested in pickers, sorted by <c>id</c>; empty
    /// when nothing is shared.
    /// </summary>
    [JsonPropertyName("data")]
    public IReadOnlyList<SharedProviderOpenAiModel> Data { get; }
}

/// <summary>
/// Details of a failed OpenAI-compatible shared-provider request. The message is sanitized: it never contains raw
/// upstream errors, credentials or internal diagnostics.
/// </summary>
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

    /// <summary>
    /// Human-readable explanation, at most 512 characters. Its wording can change; branch on <c>code</c> instead.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; }

    /// <summary>
    /// Error class in OpenAI terms: <c>invalid_request_error</c>, <c>authentication_error</c>,
    /// <c>permission_error</c>, <c>conflict_error</c>, <c>rate_limit_error</c>, <c>api_error</c> or
    /// <c>timeout_error</c>.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; }

    /// <summary>
    /// Request member or header the error refers to, for example <c>model</c>, <c>messages</c> or
    /// <c>CanDoItAll-Access-Context-Ref</c>, at most 128 characters; null when the error concerns no single member.
    /// </summary>
    [JsonPropertyName("param")]
    public string? Param { get; }

    /// <summary>
    /// Stable machine-readable reason, for example <c>shared_provider_request_invalid</c>,
    /// <c>shared_provider_model_not_found</c> or <c>shared_provider_upstream_rate_limited</c>; at most 128 ASCII
    /// letters, digits, dots, underscores and hyphens. Branch on this value.
    /// </summary>
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

/// <summary>
/// Error body of the OpenAI-compatible shared-provider operations, in the OpenAI error shape: an object whose
/// <c>error</c> member holds <c>message</c>, <c>type</c>, <c>param</c> and <c>code</c>. The native catalog operation
/// uses the general <c>errors</c> envelope instead.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SharedProviderOpenAiErrorEnvelope
{
    public SharedProviderOpenAiErrorEnvelope(SharedProviderOpenAiError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        Error = error;
    }

    /// <summary>Details of the failure.</summary>
    [JsonPropertyName("error")]
    public SharedProviderOpenAiError Error { get; }
}
