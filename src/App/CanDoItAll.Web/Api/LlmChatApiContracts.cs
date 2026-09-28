using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;

namespace CanDoItAll.Web.Api;

/// <summary>
/// Request body of <c>POST /api/llm-chats</c> (create) and <c>PUT /api/llm-chats/{definitionId}</c> (update): the
/// complete editable configuration of an LLM Chat definition. On update every member replaces the stored value, so read
/// <c>GET /api/llm-chats/{definitionId}/editor</c> first and send back the values you do not intend to change. Take the
/// provider profile, model and thinking effort from <c>GET /api/llm-chats/provider-options</c>. Members not listed here
/// are rejected.
/// </summary>
/// <param name="Name">Display name of the definition. Required: not blank, trimmed, at most 200 characters.</param>
/// <param name="Summary">
/// Short description of the definition. Trimmed, at most 2,000 characters; null or empty stores no summary.
/// </param>
/// <param name="AvatarImageUrl">
/// Image shown for the definition: an absolute <c>http</c> or <c>https</c> URL, or a bundled avatar path such as
/// <c>_content/CanDoItAll.Components.BaseLib/assets/identity/avatars/avatar-01.jpg</c> (avatars 01 to 08). At most
/// 2,048 characters; null or empty stores no image.
/// </param>
/// <param name="SystemPrompt">
/// Instructions sent to the model as the system message of every turn, stored exactly as sent. Send it on every create
/// and update: null is rejected, an empty string means no system prompt. At most 400,000 characters. Only the
/// manage-scoped editor read returns it.
/// </param>
/// <param name="ProviderProfileId">
/// Identifier of the provider profile to use, from <c>providerProfileId</c> in
/// <c>GET /api/llm-chats/provider-options</c>. The profile must exist, be enabled and serve chat.
/// </param>
/// <param name="Model">
/// Model identifier offered by that profile (<c>models[].model</c> in the provider options), matched exactly after
/// trimming. Required.
/// </param>
/// <param name="ThinkingEffort">
/// Typed thinking (reasoning) effort as a JSON string, matched case-insensitively: <c>none</c>, <c>minimal</c>,
/// <c>low</c>, <c>medium</c>, <c>high</c>, <c>extraHigh</c> or <c>max</c>, for example <c>"low"</c>. These are the
/// names of the shared schema's integer values (0 None, 6 Minimal, 1 Low, 2 Medium, 3 High, 4 ExtraHigh, 5 Max), but a
/// JSON number is rejected here. Null or omitted uses the provider's default for the model when a turn runs;
/// <c>"none"</c> explicitly disables thinking. A value must be one of the model's <c>allowedEfforts</c>, otherwise the
/// request fails with 422 <c>llm-chat.thinking-effort-not-supported</c>; it is never silently downgraded.
/// </param>
/// <param name="ModelSettings">
/// Sampling, model-parameter and timeout settings. Null or omitted stores no temperature, an empty parameter object and
/// no per-invocation timeout.
/// </param>
/// <param name="Tags">
/// Labels of the definition. Each tag is trimmed and lower-cased; at most 20 tags of at most 100 characters, unique
/// after that normalization. Null or an empty array stores no tags; on update the list replaces all existing tags.
/// </param>
/// <param name="RevisionReason">
/// Reason recorded with the new revision, for example <c>Shorter answers</c>. Trimmed, at most 500 characters; null or
/// empty records no reason.
/// </param>
/// <param name="ResponseFormat">
/// Structured-output settings for the model's replies. Null or omitted stores none, so replies are free text.
/// </param>
/// <param name="ExpectedConcurrencyToken">
/// Update only: the definition's <c>concurrencyToken</c> from your last read, the number inside its <c>ETag</c> header.
/// Send it here or as <c>If-Match</c>; when both are present they must be equal. Ignored on create.
/// </param>
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

/// <summary>
/// Model settings of the definition revision written by a create or update request. Members not listed here are
/// rejected.
/// </summary>
/// <param name="Temperature">Sampling temperature from 0 through 2. Null leaves it to the provider.</param>
/// <param name="ModelParameterConfiguration">
/// Provider-neutral model-parameter overrides as a JSON object, not a string containing JSON, for example
/// <c>{ "maxOutputTokens": 1024 }</c>. Null or omitted stores an empty object. Any other JSON type is rejected, and so
/// is a member named <c>thinkingEffort</c>, <c>reasoningEffort</c> or <c>think</c> (any case) in the object or in a
/// nested object: set thinking effort only through the typed <c>thinkingEffort</c> member of the definition. The
/// values are interpreted by the provider driver when a turn runs; saving the definition does not validate them.
/// </param>
/// <param name="TimeoutSeconds">
/// Deadline of each provider invocation of a turn, in seconds: a finite number greater than 0 and at most 1,800 (30
/// minutes); fractions are allowed. Null sets no per-invocation deadline. An invocation that exceeds it fails with
/// <c>llm-chat.deadline-exceeded</c>.
/// </param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record LlmChatModelSettingsApiRequest(
    double? Temperature,
    JsonElement ModelParameterConfiguration,
    double? TimeoutSeconds);

/// <summary>
/// Structured-output settings of the definition revision written by a create or update request. They change the
/// provider request only when <c>requireJson</c> is true. Members not listed here are rejected.
/// </summary>
/// <param name="RequireJson">
/// True asks the model for a JSON reply, constrained by <c>schema</c> when one is given and the provider supports it.
/// False sends no response-format instruction; the other members are then stored but not used.
/// </param>
/// <param name="Schema">
/// JSON Schema that the model's reply should follow, as a JSON object (not a string containing JSON). Null or omitted
/// means no schema. Any other JSON type is rejected. It describes the model's reply, not a schema of this API.
/// </param>
/// <param name="SchemaName">
/// Name given to the schema in the provider request. Null or blank lets the provider driver choose a name.
/// </param>
/// <param name="SchemaDescription">Description of the schema passed to the provider. Null or blank sends none.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record LlmChatResponseFormatApiRequest(
    bool RequireJson,
    JsonElement Schema,
    string SchemaName,
    string SchemaDescription);

/// <summary>
/// Body of the definition lifecycle operations (<c>activate</c>, <c>suspend</c>, <c>archive</c>) and of conversation
/// archiving: the concurrency token from your last read. A JSON body is required even when the token is sent as
/// <c>If-Match</c>; <c>{}</c> is then enough. Members not listed here are rejected.
/// </summary>
/// <param name="ExpectedConcurrencyToken">
/// The resource's <c>concurrencyToken</c> from your last read, the number inside its <c>ETag</c> header; not negative.
/// Null or omitted is allowed only when <c>If-Match</c> carries the token; when both are present they must be equal.
/// </param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record LlmChatExpectedConcurrencyApiRequest(long? ExpectedConcurrencyToken);

/// <summary>
/// Body of <c>POST /api/llm-chats/{definitionId}/conversations</c>. Only <c>title</c> is accepted: the definition comes
/// from the route and the origin is always <c>api</c>.
/// </summary>
/// <param name="Title">
/// Title of the new conversation. Trimmed, at most 200 characters; null, empty or blank becomes <c>New chat</c>.
/// </param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record CreateLlmChatConversationApiRequest(
    string Title);

/// <summary>
/// Body of <c>PATCH /api/llm-conversations/{conversationId}/title</c>: the new title and the preconditions from your
/// last read of the conversation. Members not listed here are rejected.
/// </summary>
/// <param name="Title">
/// New title. Trimmed, at most 200 characters; null, empty or blank becomes <c>New chat</c>.
/// </param>
/// <param name="ExpectedTranscriptRevision">
/// The conversation's <c>transcriptRevision</c> from your last read. It must equal the current transcript revision.
/// </param>
/// <param name="ExpectedConcurrencyToken">
/// The conversation's <c>concurrencyToken</c> from your last read, the number inside its <c>ETag</c> header. Send it
/// here or as <c>If-Match</c>; when both are present they must be equal.
/// </param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record RenameLlmChatConversationApiRequest(
    string Title,
    long ExpectedTranscriptRevision,
    long? ExpectedConcurrencyToken = null);

/// <summary>
/// One page of a cursor-paged LLM Chat list: definitions (<c>GET /api/llm-chats</c>) or conversations
/// (<c>GET /api/llm-conversations</c>). Request the next page by sending <c>nextCursor</c> back as <c>cursor</c>.
/// </summary>
/// <typeparam name="T">Type of the listed items.</typeparam>
/// <param name="Items">The items of this page in list order; at most the requested <c>take</c>, possibly none.</param>
/// <param name="NextCursor">
/// Opaque continuation value for the next page, or null when no further items exist. Send it unchanged as
/// <c>cursor</c> with the same filters; it is not a page number or an offset.
/// </param>
internal sealed record LlmChatApiPage<T>(
    IReadOnlyList<T> Items,
    string? NextCursor);

/// <summary>
/// An LLM Chat definition: reusable chat configuration with its lifecycle status and the settings of its current
/// revision. Returned by <c>GET /api/llm-chats/{definitionId}</c>, by the create, update and lifecycle operations and,
/// as a summary without <c>modelSettings</c>, <c>responseFormat</c> and <c>revisionReason</c>, by the definition list.
/// It never contains the system prompt or provider credentials.
/// </summary>
/// <param name="Id">Identifier of the definition, assigned by the server on creation.</param>
/// <param name="Name">Display name of the definition.</param>
/// <param name="Summary">Short description of the definition; empty when none was given.</param>
/// <param name="AvatarImageUrl">Image URL or bundled avatar path of the definition; empty when none was given.</param>
/// <param name="Status">
/// Lifecycle status as a camel-case string: <c>draft</c>, <c>active</c>, <c>suspended</c> or <c>archived</c>. Only an
/// <c>active</c> definition starts conversations and turns.
/// </param>
/// <param name="CurrentRevision">
/// Number of the current immutable revision: 1 after creation, increased by one by every successful update. The
/// settings in this object belong to it. It is not the concurrency token.
/// </param>
/// <param name="ProviderProfileId">Identifier of the provider profile used by the current revision.</param>
/// <param name="ProviderName">Display name of that provider profile, recorded when the revision was saved.</param>
/// <param name="ProviderKind">
/// Provider kind as a camel-case JSON string: <c>openAi</c>, <c>azureOpenAi</c>, <c>ollama</c> or <c>comfyUi</c>. It is
/// written as a string, not as the integer of the shared <c>ProviderKind</c> schema (0 OpenAi, 1 AzureOpenAi, 2 Ollama,
/// 3 ComfyUi).
/// </param>
/// <param name="Model">Model identifier used by the current revision.</param>
/// <param name="ThinkingEffort">
/// Typed thinking effort of the current revision as a camel-case JSON string: <c>none</c>, <c>minimal</c>, <c>low</c>,
/// <c>medium</c>, <c>high</c>, <c>extraHigh</c> or <c>max</c>, not the integer of the shared schema (0 None, 6 Minimal,
/// 1 Low, 2 Medium, 3 High, 4 ExtraHigh, 5 Max). Null means the provider's default for the model applies when a turn
/// runs.
/// </param>
/// <param name="Tags">Tags of the definition, trimmed and lower-cased; empty when it has none.</param>
/// <param name="ConcurrencyToken">
/// Optimistic-concurrency token of the definition: a non-negative integer that starts at 0 and increases with every
/// update and lifecycle change. The response also carries it as the strong <c>ETag</c> header, for example
/// <c>"3"</c>. Send it back as <c>expectedConcurrencyToken</c> or <c>If-Match</c>.
/// </param>
/// <param name="CreatedAtUtc">Instant the definition was created, in UTC.</param>
/// <param name="UpdatedAtUtc">Instant of the last update or lifecycle change, in UTC.</param>
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
    /// <summary>Model settings of the current revision. Omitted in list items.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LlmChatModelSettingsApiResponse? ModelSettings { get; init; }

    /// <summary>
    /// Structured-output settings of the current revision. Omitted in list items and when the revision has none.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LlmChatResponseFormatApiResponse? ResponseFormat { get; init; }

    /// <summary>
    /// Reason recorded with the current revision; empty when none was given. Omitted in list items.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RevisionReason { get; init; }
}

/// <summary>
/// The editable current revision of an LLM Chat definition, returned by the manage-scoped
/// <c>GET /api/llm-chats/{definitionId}/editor</c>. It holds every value an update needs, including the system prompt,
/// and never provider credentials, endpoints or local paths.
/// </summary>
/// <param name="Id">Identifier of the definition.</param>
/// <param name="Name">Display name of the definition.</param>
/// <param name="Summary">Short description of the definition; empty when none was given.</param>
/// <param name="AvatarImageUrl">Image URL or bundled avatar path of the definition; empty when none was given.</param>
/// <param name="Status">
/// Lifecycle status as a camel-case string: <c>draft</c>, <c>active</c>, <c>suspended</c> or <c>archived</c>. An
/// <c>archived</c> definition cannot be updated.
/// </param>
/// <param name="Revision">
/// Number of the revision these values belong to, which is the definition's current revision.
/// </param>
/// <param name="SystemPrompt">
/// System prompt of the revision, exactly as stored; empty when none. Treat it as sensitive configuration.
/// </param>
/// <param name="ProviderProfileId">Identifier of the provider profile used by the revision.</param>
/// <param name="ProviderName">Display name of that provider profile, recorded when the revision was saved.</param>
/// <param name="ProviderKind">
/// Provider kind as a camel-case JSON string: <c>openAi</c>, <c>azureOpenAi</c>, <c>ollama</c> or <c>comfyUi</c>, not
/// the integer of the shared <c>ProviderKind</c> schema (0 OpenAi, 1 AzureOpenAi, 2 Ollama, 3 ComfyUi).
/// </param>
/// <param name="Model">Model identifier used by the revision.</param>
/// <param name="ThinkingEffort">
/// Typed thinking effort of the revision as a camel-case JSON string: <c>none</c>, <c>minimal</c>, <c>low</c>,
/// <c>medium</c>, <c>high</c>, <c>extraHigh</c> or <c>max</c>, not the integer of the shared schema (0 None, 6 Minimal,
/// 1 Low, 2 Medium, 3 High, 4 ExtraHigh, 5 Max). Null means the provider's default for the model applies.
/// </param>
/// <param name="ModelSettings">Model settings of the revision; always present.</param>
/// <param name="ResponseFormat">Structured-output settings of the revision, or null when it has none.</param>
/// <param name="Tags">Tags of the definition, trimmed and lower-cased; empty when it has none.</param>
/// <param name="RevisionReason">Reason recorded with the revision; empty when none was given.</param>
/// <param name="ConcurrencyToken">
/// Optimistic-concurrency token of the definition, also sent as the strong <c>ETag</c> header. Send it back as
/// <c>expectedConcurrencyToken</c> or <c>If-Match</c> with the update.
/// </param>
/// <param name="CreatedAtUtc">Instant the definition was created, in UTC.</param>
/// <param name="UpdatedAtUtc">Instant of the last update or lifecycle change, in UTC.</param>
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

/// <summary>Model settings stored with an LLM Chat definition revision.</summary>
/// <param name="Temperature">Sampling temperature from 0 through 2, or null when the provider decides.</param>
/// <param name="ModelParameterConfiguration">
/// Stored model-parameter overrides as a JSON object (not a string containing JSON); <c>{}</c> when none.
/// </param>
/// <param name="TimeoutSeconds">Deadline of each provider invocation in seconds, or null when none is set.</param>
internal sealed record LlmChatModelSettingsApiResponse(
    double? Temperature,
    JsonElement ModelParameterConfiguration,
    double? TimeoutSeconds);

/// <summary>Structured-output settings stored with an LLM Chat definition revision.</summary>
/// <param name="RequireJson">
/// True when replies are requested as JSON; false when no response-format instruction is sent to the provider.
/// </param>
/// <param name="Schema">
/// Stored JSON Schema for the reply as a JSON object (not a string containing JSON); <c>{}</c> when no schema is
/// stored.
/// </param>
/// <param name="SchemaName">Name given to the schema in provider requests; empty when none.</param>
/// <param name="SchemaDescription">Description of the schema; empty when none.</param>
internal sealed record LlmChatResponseFormatApiResponse(
    bool RequireJson,
    JsonElement Schema,
    string SchemaName,
    string SchemaDescription);

/// <summary>
/// An LLM Chat conversation: its identity, the definition revision it is pinned to, its lifecycle status and the state
/// of its transcript. <c>GET /api/llm-conversations/{conversationId}</c> adds one page of transcript messages; list
/// items and the create, rename and archive responses carry no messages.
/// </summary>
/// <param name="Id">Identifier of the conversation, assigned by the server on creation.</param>
/// <param name="DefinitionId">Identifier of the definition the conversation was created from.</param>
/// <param name="DefinitionRevision">
/// Definition revision the conversation is pinned to, fixed at creation. Its turns always use the settings of this
/// revision, even after the definition is updated.
/// </param>
/// <param name="DefinitionName">Current display name of the definition.</param>
/// <param name="Title">Title of the conversation.</param>
/// <param name="Status">
/// Lifecycle status as a camel-case string: <c>active</c> (accepts turns) or <c>archived</c> (read-only).
/// </param>
/// <param name="Origin">
/// Where the conversation was created, as a camel-case string: <c>application</c> (product UI) or <c>api</c> (this HTTP
/// API).
/// </param>
/// <param name="TranscriptRevision">
/// Revision of the conversation's transcript: 0 on creation, or 1 when the definition has a system prompt. It increases
/// by one whenever the transcript changes (a turn's user message is admitted, its reply is committed, or a failed,
/// cancelled or abandoned turn is rolled back) and with every rename. Send it as <c>expectedTranscriptRevision</c> with
/// the next turn or rename. It is neither the definition revision nor the concurrency token.
/// </param>
/// <param name="HasActiveTurn">
/// True while a turn holds the conversation, from its admission until its reply is committed or the turn is rolled
/// back. No other turn can start while it is true.
/// </param>
/// <param name="ConcurrencyToken">
/// Optimistic-concurrency token of the conversation: starts at 0 and increases with every rename and archive; turns do
/// not change it. The response also carries it as the strong <c>ETag</c> header. Send it back as
/// <c>expectedConcurrencyToken</c> or <c>If-Match</c> to rename or archive.
/// </param>
/// <param name="CreatedAtUtc">Instant the conversation was created, in UTC.</param>
/// <param name="UpdatedAtUtc">Instant of the last change to the conversation or its transcript, in UTC.</param>
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
    /// <summary>
    /// Operation identifier of the active turn, which is the <c>operationId</c> sent with it; omitted when no turn is
    /// active. Follow it with <c>GET /api/llm-chat-operations/{operationId}</c>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ActiveOperationId { get; init; }

    /// <summary>
    /// One page of the transcript's user and assistant messages, oldest first; system messages are never included.
    /// Present, possibly empty, only in <c>GET /api/llm-conversations/{conversationId}</c>; omitted elsewhere.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<LlmChatMessageApiResponse>? Messages { get; init; }

    /// <summary>
    /// Opaque cursor for the next page of messages; send it back as <c>messageCursor</c>. Omitted when no further
    /// messages exist or no messages were requested.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextMessageCursor { get; init; }
}

/// <summary>
/// One message entry of a conversation transcript: the user message of a turn or the assistant reply to it. The user
/// message and the reply of one turn share its <c>turnId</c>.
/// </summary>
/// <param name="EntryId">Identifier of this message entry, assigned by the server.</param>
/// <param name="TurnId">
/// Identifier of the turn the message belongs to, which is the <c>operationId</c> of that turn's operation.
/// </param>
/// <param name="Role">
/// Author of the message as a camel-case string: <c>user</c> or <c>assistant</c>. System messages are never returned.
/// </param>
/// <param name="Content">Text of the message, as stored in the transcript.</param>
/// <param name="CreatedAtUtc">Instant the message was recorded, in UTC.</param>
/// <param name="Model">Model reported for an assistant reply; empty for user messages.</param>
/// <param name="Usage">
/// Token usage of the provider call that produced an assistant reply; null for user messages.
/// </param>
internal sealed record LlmChatMessageApiResponse(
    Guid EntryId,
    Guid TurnId,
    LlmMessageRole Role,
    string Content,
    DateTimeOffset CreatedAtUtc,
    string Model,
    LlmChatUsageApiResponse? Usage);

/// <summary>
/// Token counts reported by the model provider. They are quantities of model tokens, not money and not API bearer
/// tokens; a count the provider did not report is 0.
/// </summary>
/// <param name="InputTokens">Tokens counted for the prompt, including the transcript sent as context.</param>
/// <param name="OutputTokens">Tokens counted for the generated reply.</param>
/// <param name="CachedInputTokens">Input tokens the provider reported as served from its prompt cache.</param>
internal sealed record LlmChatUsageApiResponse(
    int InputTokens,
    int OutputTokens,
    int CachedInputTokens);

/// <summary>
/// A provider profile that LLM Chat definitions can use, with the models it offers. Returned by
/// <c>GET /api/llm-chats/provider-options</c>; it contains no endpoint, credential or local path.
/// </summary>
/// <param name="ProviderProfileId">
/// Identifier of the provider profile; send it as <c>providerProfileId</c> when creating or updating a definition.
/// </param>
/// <param name="ProviderName">Display name of the provider profile.</param>
/// <param name="ProviderKind">
/// Provider kind as a camel-case JSON string: <c>openAi</c>, <c>azureOpenAi</c>, <c>ollama</c> or <c>comfyUi</c>, not
/// the integer of the shared <c>ProviderKind</c> schema (0 OpenAi, 1 AzureOpenAi, 2 Ollama, 3 ComfyUi).
/// </param>
/// <param name="Models">
/// Models a definition may select with this profile: the profile's default model first, then its other allowed or
/// suggested models, without duplicates.
/// </param>
internal sealed record LlmChatProviderOptionApiResponse(
    Guid ProviderProfileId,
    string ProviderName,
    [property: JsonConverter(typeof(LlmChatCamelCaseEnumJsonConverter<ProviderKind>))]
    ProviderKind ProviderKind,
    IReadOnlyList<LlmChatModelOptionApiResponse> Models);

/// <summary>A model offered by a provider profile to LLM Chat definitions.</summary>
/// <param name="Model">Model identifier; send it as <c>model</c> when creating or updating a definition.</param>
/// <param name="ThinkingEffort">Which typed thinking-effort values a definition may set for this model.</param>
internal sealed record LlmChatModelOptionApiResponse(
    string Model,
    LlmChatThinkingEffortOptionApiResponse ThinkingEffort);

/// <summary>
/// Thinking-effort capability of one model: whether a definition may set <c>thinkingEffort</c>, and to which values.
/// All members are camel-case JSON strings, not the integers shown by the shared enum schemas.
/// </summary>
/// <param name="Status">
/// Support status as a camel-case string: <c>supported</c> (a definition may set one of <c>allowedEfforts</c>),
/// <c>unsupported</c> or <c>unknown</c> (only null is accepted). It is written as a string, not as the integer of the
/// shared schema (0 Supported, 1 Unsupported, 2 Unknown).
/// </param>
/// <param name="ControlMode">
/// How the model exposes thinking, as a camel-case string: <c>effortLevels</c> (graded levels), <c>booleanToggle</c>
/// (on or off, offered as <c>medium</c> and <c>none</c>) or <c>unspecified</c> (no control declared, as for models
/// without support). It is written as a string, not as the integer of the shared schema (0 Unspecified,
/// 1 BooleanToggle, 2 EffortLevels).
/// </param>
/// <param name="AllowedEfforts">
/// Values accepted as <c>thinkingEffort</c> for this model, as camel-case strings from <c>none</c>, <c>minimal</c>,
/// <c>low</c>, <c>medium</c>, <c>high</c>, <c>extraHigh</c> and <c>max</c>, not the integers of the shared schema
/// (0 None, 6 Minimal, 1 Low, 2 Medium, 3 High, 4 ExtraHigh, 5 Max). Empty when thinking effort is not supported.
/// </param>
/// <param name="ProviderDefault">
/// Effort the provider applies when a definition leaves <c>thinkingEffort</c> null, as a camel-case string
/// (<c>none</c>, <c>minimal</c>, <c>low</c>, <c>medium</c>, <c>high</c>, <c>extraHigh</c> or <c>max</c>, not the
/// integer of the shared schema: 0 None, 6 Minimal, 1 Low, 2 Medium, 3 High, 4 ExtraHigh, 5 Max). Null when no default
/// is configured.
/// </param>
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
