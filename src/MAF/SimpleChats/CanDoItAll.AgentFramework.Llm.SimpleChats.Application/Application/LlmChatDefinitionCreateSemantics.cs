using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

internal static class LlmChatDefinitionCreateSemantics {
    public static CreateLlmChatDefinitionCommand Snapshot(CreateLlmChatDefinitionCommand command) {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.SystemPrompt);
        if (command.SystemPrompt.Length > LlmMessage.MaximumTextLength) {
            throw new ArgumentException("The system prompt exceeds the maximum message length.", nameof(command));
        }

        LlmChatDefinitionValidation.ValidateSettings(command.Settings, command.Timeout);
        return command with {
            Name = LlmChatDefinitionValidation.NormalizeRequired(command.Name,
                LlmChatDefinitionValidation.MaximumNameLength, nameof(command.Name)),
            Summary = LlmChatDefinitionValidation.NormalizeOptional(command.Summary,
                LlmChatDefinitionValidation.MaximumSummaryLength, nameof(command.Summary)),
            AvatarImageUrl = LlmChatDefinitionValidation.NormalizeAvatarImageUrl(command.AvatarImageUrl),
            Model = LlmChatDefinitionValidation.NormalizeRequired(command.Model,
                LlmChatDefinitionValidation.MaximumModelLength, nameof(command.Model)),
            RevisionReason = LlmChatDefinitionValidation.NormalizeOptional(command.RevisionReason,
                LlmChatDefinitionValidation.MaximumRevisionReasonLength, nameof(command.RevisionReason)),
            Tags = LlmChatDefinitionValidation.NormalizeTags(command.Tags)
        };
    }

    public static LlmChatDefinitionCreateFingerprint Fingerprint(CreateLlmChatDefinitionCommand command) {
        var settings = LlmChatFingerprints.CreateRequestedSettings(command.ProviderProfileId, command.Model,
            command.Settings, command.Timeout, command.ResponseFormat);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) {
            writer.WriteStartObject();
            writer.WriteNumber("semanticVersion", LlmChatDefinitionCreateClaim.SemanticVersion);
            writer.WriteString("name", command.Name);
            writer.WriteString("summary", command.Summary);
            writer.WriteString("avatarImageUrl", command.AvatarImageUrl);
            writer.WriteString("systemPrompt", command.SystemPrompt);
            writer.WriteString("requestedSettingsFingerprint", settings.Value);
            writer.WriteString("revisionReason", command.RevisionReason);
            writer.WriteStartArray("tags");
            foreach (var tag in command.Tags ?? []) {
                writer.WriteStringValue(tag);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return new(Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant());
    }
}
