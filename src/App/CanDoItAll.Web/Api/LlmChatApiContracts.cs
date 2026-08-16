using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Definitions;

namespace CanDoItAll.Web.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record LlmChatDefinitionMutationApiRequest(
    string Name,
    string Summary,
    string AvatarImageUrl,
    string SystemPrompt,
    Guid ProviderProfileId,
    string Model,
    [property: JsonConverter(typeof(LlmChatNullableCamelCaseEnumJsonConverter<AgentReasoningEffortLevel>))]
    AgentReasoningEffortLevel? ThinkingEffort,
    LlmChatModelSettingsApiRequest? ModelSettings,
    IReadOnlyList<string>? Tags,
    string RevisionReason,
    LlmChatResponseFormatApiRequest? ResponseFormat = null,
    long? ExpectedConcurrencyToken = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record LlmChatModelSettingsApiRequest(
    double? Temperature,
    JsonElement ModelParameterConfiguration,
    double? TimeoutSeconds);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record LlmChatResponseFormatApiRequest(
    bool RequireJson,
    JsonElement Schema,
    string SchemaName,
    string SchemaDescription);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record LlmChatExpectedConcurrencyApiRequest(long? ExpectedConcurrencyToken);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record CreateLlmChatConversationApiRequest(
    string Title);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record RenameLlmChatConversationApiRequest(
    string Title,
    long ExpectedTranscriptRevision,
    long? ExpectedConcurrencyToken = null);

internal sealed record LlmChatApiPage<T>(
    IReadOnlyList<T> Items,
    string? NextCursor);

internal sealed record LlmChatDefinitionApiResponse(
    Guid Id,
    string Name,
    string Summary,
    string AvatarImageUrl,
    LlmChatDefinitionStatus Status,
    int CurrentRevision,
    Guid ProviderProfileId,
    string ProviderName,
    [property: JsonConverter(typeof(LlmChatCamelCaseEnumJsonConverter<ProviderKind>))]
    ProviderKind ProviderKind,
    string Model,
    [property: JsonConverter(typeof(LlmChatNullableCamelCaseEnumJsonConverter<AgentReasoningEffortLevel>))]
    AgentReasoningEffortLevel? ThinkingEffort,
    IReadOnlyList<string> Tags,
    long ConcurrencyToken,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LlmChatModelSettingsApiResponse? ModelSettings { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LlmChatResponseFormatApiResponse? ResponseFormat { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RevisionReason { get; init; }
}

internal sealed record LlmChatDefinitionEditorApiResponse(
    Guid Id,
    string Name,
    string Summary,
    string AvatarImageUrl,
    LlmChatDefinitionStatus Status,
    int Revision,
    string SystemPrompt,
    Guid ProviderProfileId,
    string ProviderName,
    [property: JsonConverter(typeof(LlmChatCamelCaseEnumJsonConverter<ProviderKind>))]
    ProviderKind ProviderKind,
    string Model,
    [property: JsonConverter(typeof(LlmChatNullableCamelCaseEnumJsonConverter<AgentReasoningEffortLevel>))]
    AgentReasoningEffortLevel? ThinkingEffort,
    LlmChatModelSettingsApiResponse ModelSettings,
    LlmChatResponseFormatApiResponse? ResponseFormat,
    IReadOnlyList<string> Tags,
    string RevisionReason,
    long ConcurrencyToken,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

internal sealed record LlmChatModelSettingsApiResponse(
    double? Temperature,
    JsonElement ModelParameterConfiguration,
    double? TimeoutSeconds);

internal sealed record LlmChatResponseFormatApiResponse(
    bool RequireJson,
    JsonElement Schema,
    string SchemaName,
    string SchemaDescription);

internal sealed record LlmChatConversationApiResponse(
    Guid Id,
    Guid DefinitionId,
    int DefinitionRevision,
    string DefinitionName,
    string Title,
    LlmChatConversationStatus Status,
    LlmChatConversationOrigin Origin,
    long TranscriptRevision,
    bool HasActiveTurn,
    long ConcurrencyToken,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ActiveOperationId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<LlmChatMessageApiResponse>? Messages { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextMessageCursor { get; init; }
}

internal sealed record LlmChatMessageApiResponse(
    Guid EntryId,
    Guid TurnId,
    LlmMessageRole Role,
    string Content,
    DateTimeOffset CreatedAtUtc,
    string Model,
    LlmChatUsageApiResponse? Usage);

internal sealed record LlmChatUsageApiResponse(
    int InputTokens,
    int OutputTokens,
    int CachedInputTokens);

internal sealed record LlmChatProviderOptionApiResponse(
    Guid ProviderProfileId,
    string ProviderName,
    [property: JsonConverter(typeof(LlmChatCamelCaseEnumJsonConverter<ProviderKind>))]
    ProviderKind ProviderKind,
    IReadOnlyList<LlmChatModelOptionApiResponse> Models);

internal sealed record LlmChatModelOptionApiResponse(
    string Model,
    LlmChatThinkingEffortOptionApiResponse ThinkingEffort);

internal sealed record LlmChatThinkingEffortOptionApiResponse(
    [property: JsonConverter(typeof(LlmChatCamelCaseEnumJsonConverter<AgentThinkingEffortSupportStatus>))]
    AgentThinkingEffortSupportStatus Status,
    [property: JsonConverter(typeof(LlmChatCamelCaseEnumJsonConverter<AgentThinkingEffortControlMode>))]
    AgentThinkingEffortControlMode ControlMode,
    [property: JsonConverter(typeof(LlmChatCamelCaseEnumListJsonConverter<AgentReasoningEffortLevel>))]
    IReadOnlyList<AgentReasoningEffortLevel> AllowedEfforts,
    [property: JsonConverter(typeof(LlmChatNullableCamelCaseEnumJsonConverter<AgentReasoningEffortLevel>))]
    AgentReasoningEffortLevel? ProviderDefault);

internal sealed class LlmChatCamelCaseEnumJsonConverter<TEnum>
    : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
        => LlmChatEnumJson.Read<TEnum>(ref reader);

    public override void Write(
        Utf8JsonWriter writer,
        TEnum value,
        JsonSerializerOptions options)
        => LlmChatEnumJson.Write(writer, value);
}

internal sealed class LlmChatNullableCamelCaseEnumJsonConverter<TEnum>
    : JsonConverter<TEnum?>
    where TEnum : struct, Enum
{
    public override TEnum? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null
            ? null
            : LlmChatEnumJson.Read<TEnum>(ref reader);

    public override void Write(
        Utf8JsonWriter writer,
        TEnum? value,
        JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        LlmChatEnumJson.Write(writer, value.Value);
    }
}

internal sealed class LlmChatCamelCaseEnumListJsonConverter<TEnum>
    : JsonConverter<IReadOnlyList<TEnum>>
    where TEnum : struct, Enum
{
    public override IReadOnlyList<TEnum> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected an array of {typeof(TEnum).Name} values.");
        }

        var values = new List<TEnum>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            values.Add(LlmChatEnumJson.Read<TEnum>(ref reader));
        }

        if (reader.TokenType != JsonTokenType.EndArray)
        {
            throw new JsonException($"The {typeof(TEnum).Name} array is incomplete.");
        }

        return values;
    }

    public override void Write(
        Utf8JsonWriter writer,
        IReadOnlyList<TEnum> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            LlmChatEnumJson.Write(writer, item);
        }

        writer.WriteEndArray();
    }
}

internal static class LlmChatEnumJson
{
    public static TEnum Read<TEnum>(ref Utf8JsonReader reader)
        where TEnum : struct, Enum
    {
        if (reader.TokenType != JsonTokenType.String ||
            !Enum.TryParse<TEnum>(reader.GetString(), ignoreCase: true, out var value) ||
            !Enum.IsDefined(value))
        {
            throw new JsonException($"Expected a supported {typeof(TEnum).Name} string value.");
        }

        return value;
    }

    public static void Write<TEnum>(Utf8JsonWriter writer, TEnum value)
        where TEnum : struct, Enum
    {
        writer.WriteStringValue(JsonNamingPolicy.CamelCase.ConvertName(value.ToString()));
    }
}
