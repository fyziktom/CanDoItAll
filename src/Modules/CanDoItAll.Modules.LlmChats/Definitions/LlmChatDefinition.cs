using CanDoItAll.Modules.LlmChats.Common;

namespace CanDoItAll.Modules.LlmChats.Definitions;

public enum LlmChatDefinitionStatus
{
    Draft,
    Active,
    Suspended,
    Archived
}

public sealed record LlmChatDefinition
{
    public LlmChatDefinition(
        LlmChatDefinitionId id,
        string name,
        string summary,
        string avatarImageUrl,
        LlmChatDefinitionStatus status,
        LlmChatDefinitionRevisionNumber currentRevision,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        long concurrencyToken)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("A definition requires an id.", nameof(id));
        }

        if (currentRevision.Value < 1)
        {
            throw new ArgumentException("A definition requires a current revision.", nameof(currentRevision));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown definition status.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(concurrencyToken);
        if (updatedAtUtc < createdAtUtc)
        {
            throw new ArgumentException("Updated time cannot precede created time.", nameof(updatedAtUtc));
        }

        Id = id;
        Name = LlmChatDefinitionValidation.NormalizeRequired(
            name,
            LlmChatDefinitionValidation.MaximumNameLength,
            nameof(name));
        Summary = LlmChatDefinitionValidation.NormalizeOptional(
            summary,
            LlmChatDefinitionValidation.MaximumSummaryLength,
            nameof(summary));
        AvatarImageUrl = LlmChatDefinitionValidation.NormalizeAvatarImageUrl(avatarImageUrl);
        Status = status;
        CurrentRevision = currentRevision;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        ConcurrencyToken = concurrencyToken;
    }

    public LlmChatDefinitionId Id { get; }

    public string Name { get; }

    public string Summary { get; }

    public string AvatarImageUrl { get; }

    public LlmChatDefinitionStatus Status { get; }

    public LlmChatDefinitionRevisionNumber CurrentRevision { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

    public long ConcurrencyToken { get; }
}
