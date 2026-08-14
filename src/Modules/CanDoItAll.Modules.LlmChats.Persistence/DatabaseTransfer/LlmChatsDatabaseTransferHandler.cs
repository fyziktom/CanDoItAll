using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.LlmChats.Persistence.DatabaseTransfer;

public sealed class LlmChatsDatabaseTransferHandler : IDatabaseTransferHandler
{
    public DatabaseTransferItemDescriptor Descriptor { get; } = new(
        "llm-chats",
        "LLM chats",
        "Copies versioned LLM Chat definitions, transcripts, operations, and invocation audit.",
        SortOrder: 30);

    public async Task<DatabaseTransferItemPreview> PreviewAsync(
        DatabaseTransferContext context,
        CancellationToken cancellationToken = default)
    {
        var source = await LlmChatsTransferDocument.LoadAsync(context.SourceDbContext, cancellationToken)
            .ConfigureAwait(false);
        var target = await LlmChatsTransferDocument.LoadAsync(context.TargetDbContext, cancellationToken)
            .ConfigureAwait(false);
        return new DatabaseTransferItemPreview(
            Descriptor,
            source.RecordCount > 0,
            $"{source.Definitions.Count} definition(s), {source.Conversations.Count} conversation(s), and {source.Operations.Count} operation(s) are available.",
            source.RecordCount == 0 ? "The source database does not contain LLM Chats data." : null,
            source.RecordCount,
            target.RecordCount);
    }

    public async Task<DatabaseTransferItemResult> TransferAsync(
        DatabaseTransferContext context,
        CancellationToken cancellationToken = default)
    {
        var document = await LlmChatsTransferDocument.LoadAsync(context.SourceDbContext, cancellationToken)
            .ConfigureAwait(false);
        document.ValidateForImport();
        if (document.RecordCount == 0)
        {
            return new DatabaseTransferItemResult(
                Descriptor.Key,
                Descriptor.Label,
                false,
                "The source database has no LLM Chats data to transfer.",
                0);
        }

        var target = await LlmChatsTransferDocument.LoadAsync(context.TargetDbContext, cancellationToken)
            .ConfigureAwait(false);
        if (target.RecordCount > 0 && !context.ReplaceExisting)
        {
            return new DatabaseTransferItemResult(
                Descriptor.Key,
                Descriptor.Label,
                false,
                "The target already contains LLM Chats data and replacement was not requested.",
                0);
        }

        await using var transaction = await context.TargetDbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        if (target.RecordCount > 0)
        {
            await LlmChatsTransferDocument.ClearAsync(context.TargetDbContext, cancellationToken).ConfigureAwait(false);
        }

        await document.SaveAsync(context.TargetDbContext, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new DatabaseTransferItemResult(
            Descriptor.Key,
            Descriptor.Label,
            true,
            $"Copied {document.Definitions.Count} definition(s), {document.Conversations.Count} conversation(s), and their versioned transcript/operation audit graph.",
            document.RecordCount);
    }
}
