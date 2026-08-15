using CanDoItAll.Modules.LlmChats.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.LlmChats.Persistence.DatabaseTransfer;

internal sealed record LlmChatsTransferDocument(
    int SchemaVersion,
    IReadOnlyList<LlmChatDefinitionRow> Definitions,
    IReadOnlyList<LlmChatDefinitionRevisionRow> Revisions,
    IReadOnlyList<LlmChatDefinitionTagRow> Tags,
    IReadOnlyList<LlmChatConversationRow> Conversations,
    IReadOnlyList<LlmChatTranscriptRow> Transcripts,
    IReadOnlyList<LlmChatMessageRow> Messages,
    IReadOnlyList<LlmChatOperationRow> Operations,
    IReadOnlyList<LlmChatInvocationRecordRow> InvocationRecords)
{
    public const int CurrentSchemaVersion = 4;

    public int RecordCount =>
        Definitions.Count + Revisions.Count + Tags.Count + Conversations.Count + Transcripts.Count +
        Messages.Count + Operations.Count + InvocationRecords.Count;

    public static async Task<LlmChatsTransferDocument> LoadAsync(
        DbContext dbContext,
        CancellationToken cancellationToken)
        => new(
            CurrentSchemaVersion,
            await dbContext.Set<LlmChatDefinitionRow>().AsNoTracking().ToArrayAsync(cancellationToken).ConfigureAwait(false),
            await dbContext.Set<LlmChatDefinitionRevisionRow>().AsNoTracking().ToArrayAsync(cancellationToken).ConfigureAwait(false),
            await dbContext.Set<LlmChatDefinitionTagRow>().AsNoTracking().ToArrayAsync(cancellationToken).ConfigureAwait(false),
            await dbContext.Set<LlmChatConversationRow>().AsNoTracking().ToArrayAsync(cancellationToken).ConfigureAwait(false),
            await dbContext.Set<LlmChatTranscriptRow>().AsNoTracking().ToArrayAsync(cancellationToken).ConfigureAwait(false),
            await dbContext.Set<LlmChatMessageRow>().AsNoTracking().ToArrayAsync(cancellationToken).ConfigureAwait(false),
            await dbContext.Set<LlmChatOperationRow>().AsNoTracking().ToArrayAsync(cancellationToken).ConfigureAwait(false),
            await dbContext.Set<LlmChatInvocationRecordRow>().AsNoTracking().ToArrayAsync(cancellationToken).ConfigureAwait(false));

    public void ValidateForImport()
    {
        if (SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported LLM Chats transfer schema version {SchemaVersion}.");
        }

        EnsureUnique(Definitions.Select(row => row.Id), "definition id");
        EnsureUnique(Revisions.Select(row => (row.DefinitionId, row.Revision)), "definition revision");
        EnsureUnique(Tags.Select(row => (row.DefinitionId, row.Tag)), "definition tag");
        EnsureUnique(Conversations.Select(row => row.Id), "conversation id");
        EnsureUnique(Transcripts.Select(row => row.ConversationId), "transcript id");
        EnsureUnique(Messages.Select(row => row.EntryId), "message entry id");
        EnsureUnique(Operations.Select(row => row.Id), "operation id");
        EnsureUnique(InvocationRecords.Select(row => (row.OperationId, row.Ordinal)), "invocation ordinal");

        var definitionIds = Definitions.Select(row => row.Id).ToHashSet();
        var revisionIds = Revisions.Select(row => (row.DefinitionId, row.Revision)).ToHashSet();
        var transcriptIds = Transcripts.Select(row => row.ConversationId).ToHashSet();
        var conversationIds = Conversations.Select(row => row.Id).ToHashSet();
        var operationIds = Operations.Select(row => row.Id).ToHashSet();
        if (Revisions.Any(row => !definitionIds.Contains(row.DefinitionId)) ||
            Tags.Any(row => !definitionIds.Contains(row.DefinitionId)))
        {
            throw new InvalidDataException("An LLM Chat revision or tag references a missing definition.");
        }

        if (Definitions.Any(row => !revisionIds.Contains((row.Id, row.CurrentRevision))))
        {
            throw new InvalidDataException("An LLM Chat definition references a missing current revision.");
        }

        if (Conversations.Any(row =>
                !transcriptIds.Contains(row.Id) || !revisionIds.Contains((row.DefinitionId, row.DefinitionRevision))))
        {
            throw new InvalidDataException("An LLM Chat conversation references a missing transcript or definition revision.");
        }

        if (Transcripts.Any(row => !conversationIds.Contains(row.ConversationId)) ||
            Messages.Any(row => !transcriptIds.Contains(row.ConversationId)))
        {
            throw new InvalidDataException("An LLM Chat transcript or message is detached from its product conversation.");
        }

        foreach (var transcript in Transcripts)
        {
            var messages = Messages
                .Where(row => row.ConversationId == transcript.ConversationId)
                .OrderBy(row => row.Sequence)
                .ToArray();
            if (messages.Length != transcript.EntryCount ||
                messages.Where((message, index) => message.Sequence != index + 1).Any())
            {
                throw new InvalidDataException("An LLM Chat transcript contains a non-contiguous message sequence.");
            }
        }

        if (Operations.Any(row => !conversationIds.Contains(row.ConversationId)) ||
            InvocationRecords.Any(row => !operationIds.Contains(row.OperationId)))
        {
            throw new InvalidDataException("An LLM Chat operation or invocation record is detached from its parent.");
        }

        if (Operations.Any(row =>
                row.ExecutionEpoch < 0 ||
                !Enum.IsDefined(row.DispatchPhase) ||
                (row.ExecutionOwnerId is null) != (row.ClaimedAtUtc is null) ||
                (row.ExecutionOwnerId is null) != (row.HeartbeatAtUtc is null) ||
                (row.ExecutionOwnerId is null) != (row.LeaseExpiresAtUtc is null)))
        {
            throw new InvalidDataException("An LLM Chat operation contains an invalid execution lease.");
        }
    }

    public async Task SaveAsync(DbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.AddRangeAsync(Definitions, cancellationToken).ConfigureAwait(false);
        await dbContext.AddRangeAsync(Revisions, cancellationToken).ConfigureAwait(false);
        await dbContext.AddRangeAsync(Tags, cancellationToken).ConfigureAwait(false);
        await dbContext.AddRangeAsync(Transcripts, cancellationToken).ConfigureAwait(false);
        await dbContext.AddRangeAsync(Conversations, cancellationToken).ConfigureAwait(false);
        await dbContext.AddRangeAsync(Messages, cancellationToken).ConfigureAwait(false);
        await dbContext.AddRangeAsync(Operations, cancellationToken).ConfigureAwait(false);
        await dbContext.AddRangeAsync(InvocationRecords, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task ClearAsync(DbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Set<LlmChatInvocationRecordRow>().ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Set<LlmChatOperationRow>().ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Set<LlmChatMessageRow>().ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Set<LlmChatConversationRow>().ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Set<LlmChatTranscriptRow>().ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Set<LlmChatDefinitionTagRow>().ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Set<LlmChatDefinitionRevisionRow>().ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Set<LlmChatDefinitionRow>().ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureUnique<T>(IEnumerable<T> values, string label) where T : notnull
    {
        var unique = new HashSet<T>();
        if (values.Any(value => !unique.Add(value)))
        {
            throw new InvalidDataException($"The LLM Chats transfer contains a duplicate {label}.");
        }
    }
}
