using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Common;

public static class LlmChatFingerprints
{
    public static LlmChatSettingsFingerprint CreateSettings(
        Guid providerProfileId,
        ProviderKind providerKind,
        string model,
        LlmModelSettings? settings = null,
        TimeSpan? timeout = null,
        LlmResponseFormat? responseFormat = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(providerProfileId, Guid.Empty);
        if (!Enum.IsDefined(providerKind))
        {
            throw new ArgumentOutOfRangeException(nameof(providerKind), providerKind, "Unknown provider kind.");
        }

        var normalizedModel = LlmChatDefinitionValidation.NormalizeRequired(
            model,
            LlmChatDefinitionValidation.MaximumModelLength,
            nameof(model));

        try
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("providerProfileId", providerProfileId);
                writer.WriteString("providerKind", providerKind.ToString());
                writer.WriteString("model", normalizedModel);
                if (settings?.Temperature is { } temperature)
                {
                    writer.WriteNumber("temperature", temperature);
                }
                else
                {
                    writer.WriteNull("temperature");
                }

                if (settings?.ThinkingEffort is { } thinkingEffort)
                {
                    if (!Enum.IsDefined(thinkingEffort))
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(settings),
                            thinkingEffort,
                            "Unknown thinking effort.");
                    }

                    writer.WriteString("thinkingEffort", thinkingEffort.ToString());
                }
                else
                {
                    writer.WriteNull("thinkingEffort");
                }

                writer.WritePropertyName("modelParameters");
                WriteCanonicalJson(writer, settings?.ModelParameterConfigurationJson, emptyObjectWhenBlank: true);

                if (timeout is { } deadline)
                {
                    writer.WriteNumber("timeoutTicks", deadline.Ticks);
                }
                else
                {
                    writer.WriteNull("timeoutTicks");
                }

                writer.WritePropertyName("responseFormat");
                WriteResponseFormat(writer, responseFormat);
                writer.WriteEndObject();
            }

            return new LlmChatSettingsFingerprint(Hash(stream.ToArray()));
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Model parameter or response schema JSON is invalid.", nameof(settings), exception);
        }
    }

    public static LlmChatRequestFingerprint CreateRequest(
        LlmChatConversationId conversationId,
        long expectedTranscriptRevision,
        string userText,
        LlmChatSettingsFingerprint settingsFingerprint,
        WorkspaceScopeDescriptor? attributionScope = null)
    {
        if (conversationId.Value == Guid.Empty)
        {
            throw new ArgumentException("A request fingerprint requires a conversation id.", nameof(conversationId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(expectedTranscriptRevision);
        ArgumentException.ThrowIfNullOrWhiteSpace(userText);
        if (string.IsNullOrWhiteSpace(settingsFingerprint.Value))
        {
            throw new ArgumentException("A request fingerprint requires immutable revision settings.", nameof(settingsFingerprint));
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("conversationId", conversationId.Value);
            writer.WriteNumber("expectedTranscriptRevision", expectedTranscriptRevision);
            writer.WriteString("userText", userText);
            writer.WriteString("settingsFingerprint", settingsFingerprint.Value);
            if (attributionScope is not null)
            {
                writer.WriteStartObject("attributionScope");
                writer.WriteString("kind", attributionScope.Kind.ToString());
                writer.WriteString("key", attributionScope.Key);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return new LlmChatRequestFingerprint(Hash(stream.ToArray()));
    }

    private static void WriteResponseFormat(Utf8JsonWriter writer, LlmResponseFormat? responseFormat)
    {
        if (responseFormat is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteBoolean("requireJson", responseFormat.RequireJson);
        writer.WriteString("schemaName", responseFormat.SchemaName?.Trim() ?? string.Empty);
        writer.WriteString("schemaDescription", responseFormat.SchemaDescription?.Trim() ?? string.Empty);
        writer.WritePropertyName("schema");
        WriteCanonicalJson(writer, responseFormat.SchemaJson, emptyObjectWhenBlank: false);
        writer.WriteEndObject();
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, string? json, bool emptyObjectWhenBlank)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            if (emptyObjectWhenBlank)
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteNullValue();
            }

            return;
        }

        using var document = JsonDocument.Parse(json);
        WriteCanonicalElement(writer, document.RootElement);
    }

    private static void WriteCanonicalElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var properties = element.EnumerateObject().ToArray();
                if (properties.GroupBy(property => property.Name, StringComparer.Ordinal).Any(group => group.Count() > 1))
                {
                    throw new JsonException("Duplicate JSON property names are not allowed.");
                }

                writer.WriteStartObject();
                foreach (var property in properties.OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalElement(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            }
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonicalElement(writer, item);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                element.WriteTo(writer);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new JsonException($"Unsupported JSON value kind '{element.ValueKind}'.");
        }
    }

    private static string Hash(ReadOnlySpan<byte> value)
        => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
}
