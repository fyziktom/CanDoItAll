namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;

public sealed record LlmChatTemplateSummary(Guid Id, string Name, string Summary, string? AvatarImageUrl,
    bool HasEmbeddedAvatar, DateTimeOffset UpdatedAtUtc, int Revision);

public sealed record LlmChatTemplateAvatar(string ImageUrl, int Revision);

public interface ILlmChatTemplateSummaryReader {
    Task<IReadOnlyList<LlmChatTemplateSummary>> ListAsync(IReadOnlySet<Guid> sharedIds, CancellationToken token);
    Task<LlmChatTemplateAvatar?> ReadAvatarAsync(Guid id, CancellationToken token);
}
