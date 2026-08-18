using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;

public sealed record LlmChatDefinitionRevision
{
    public LlmChatDefinitionRevision(
        LlmChatDefinitionId definitionId,
        LlmChatDefinitionRevisionNumber revision,
        string name,
        string summary,
        string avatarImageUrl,
        string systemPrompt,
        Guid providerProfileId,
        ProviderKind providerKind,
        string providerName,
        string model,
        LlmModelSettings settings,
        TimeSpan? timeout,
        LlmResponseFormat? responseFormat,
        DateTimeOffset createdAtUtc,
        string reason)
    {
        if (definitionId.Value == Guid.Empty)
        {
            throw new ArgumentException("A definition revision requires a definition id.", nameof(definitionId));
        }

        if (revision.Value < 1)
        {
            throw new ArgumentException("A definition revision requires a positive revision number.", nameof(revision));
        }

        ArgumentOutOfRangeException.ThrowIfEqual(providerProfileId, Guid.Empty);
        if (!Enum.IsDefined(providerKind))
        {
            throw new ArgumentOutOfRangeException(nameof(providerKind), providerKind, "Unknown provider kind.");
        }

        ArgumentNullException.ThrowIfNull(systemPrompt);
        LlmChatDefinitionValidation.ValidateSettings(settings, timeout);
        if (systemPrompt.Length > LlmMessage.MaximumTextLength)
        {
            throw new ArgumentException(
                $"A system prompt cannot exceed {LlmMessage.MaximumTextLength} characters.",
                nameof(systemPrompt));
        }

        DefinitionId = definitionId;
        Revision = revision;
        Name = LlmChatDefinitionValidation.NormalizeRequired(
            name,
            LlmChatDefinitionValidation.MaximumNameLength,
            nameof(name));
        Summary = LlmChatDefinitionValidation.NormalizeOptional(
            summary,
            LlmChatDefinitionValidation.MaximumSummaryLength,
            nameof(summary));
        AvatarImageUrl = LlmChatDefinitionValidation.NormalizeAvatarImageUrl(avatarImageUrl);
        SystemPrompt = systemPrompt;
        ProviderProfileId = providerProfileId;
        ProviderKind = providerKind;
        ProviderName = LlmChatDefinitionValidation.NormalizeRequired(
            providerName,
            LlmChatDefinitionValidation.MaximumProviderNameLength,
            nameof(providerName));
        Model = LlmChatDefinitionValidation.NormalizeRequired(
            model,
            LlmChatDefinitionValidation.MaximumModelLength,
            nameof(model));
        Settings = settings;
        Timeout = timeout;
        ResponseFormat = responseFormat;
        CreatedAtUtc = createdAtUtc;
        Reason = LlmChatDefinitionValidation.NormalizeOptional(
            reason,
            LlmChatDefinitionValidation.MaximumRevisionReasonLength,
            nameof(reason));
        SettingsFingerprint = LlmChatFingerprints.CreateSettings(
            providerProfileId,
            providerKind,
            Model,
            settings,
            timeout,
            responseFormat);
    }

    public LlmChatDefinitionId DefinitionId { get; }

    public LlmChatDefinitionRevisionNumber Revision { get; }

    public string Name { get; }

    public string Summary { get; }

    public string AvatarImageUrl { get; }

    public string SystemPrompt { get; }

    public Guid ProviderProfileId { get; }

    public ProviderKind ProviderKind { get; }

    public string ProviderName { get; }

    public string Model { get; }

    public LlmModelSettings Settings { get; }

    public TimeSpan? Timeout { get; }

    public LlmResponseFormat? ResponseFormat { get; }

    public LlmChatSettingsFingerprint SettingsFingerprint { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public string Reason { get; }
}
