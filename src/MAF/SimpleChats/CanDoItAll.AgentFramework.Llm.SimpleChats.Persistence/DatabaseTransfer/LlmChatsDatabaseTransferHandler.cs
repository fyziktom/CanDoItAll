using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.DatabaseTransfer;

public sealed class LlmChatsDatabaseTransferHandler(LlmChatTransferOptions options, TimeProvider clock,
    DatabaseTransferOperationRunner operations, DatabaseTransferOwnerSessionRunner sessions) : IDatabaseTransferHandler {
    public DatabaseTransferItemDescriptor Descriptor { get; } = new(
        "llm-chats",
        "LLM chats",
        "Copies versioned LLM Chat definitions, transcripts, operations, and invocation audit.",
        SortOrder: 30);

    public async Task<DatabaseTransferItemPreview> PreviewAsync(
        DatabaseTransferOperation context,
        CancellationToken cancellationToken = default) {
        var source = await LoadAsync(context.SourceProfile, cancellationToken)
            .ConfigureAwait(false);
        var target = await LoadAsync(context.TargetProfile, cancellationToken)
            .ConfigureAwait(false);
        return new DatabaseTransferItemPreview(
            Descriptor,
            source.RecordCount > 0 && target.DefinitionCreateReceipts.Count == 0,
            $"{source.Definitions.Count} definition(s), {source.Conversations.Count} conversation(s), and {source.Operations.Count} operation(s) are available.",
            source.RecordCount == 0 ? "The source database does not contain LLM Chats data."
                : target.DefinitionCreateReceipts.Count > 0 ? LlmChatsTransferDocument.RetainedCreationReceiptReplacementError : null,
            source.RecordCount,
            target.RecordCount);
    }

    public Task<DatabaseTransferItemResult> TransferAsync(DatabaseTransferOperation context, CancellationToken cancellationToken = default)
        => operations.RunTransferAsync(context, (transfer, token) => TransferCoreAsync(context, transfer, token), cancellationToken: cancellationToken);

    private async Task<DatabaseTransferItemResult> TransferCoreAsync(DatabaseTransferOperation context,
        DatabaseTransferOwnerRequest transfer, CancellationToken cancellationToken) {
        await using var sourceOwner = await sessions.CreateSourceAsync<SimpleChatsDbContext>(transfer, static value => new SimpleChatsDbContext(value), cancellationToken);
        await using var targetOwner = await sessions.CreateTargetAsync<SimpleChatsDbContext>(transfer, static value => new SimpleChatsDbContext(value), cancellationToken);
        await sessions.AcquireTargetTableLocksAsync(transfer,
            targetOwner.Model.GetEntityTypes().Select(DatabaseTransferTable.From).ToArray(), cancellationToken);
        var document = await LlmChatsTransferDocument.LoadAsync(sourceOwner, options, cancellationToken);
        document.ValidateForImport();
        if (document.RecordCount == 0) {
            return new DatabaseTransferItemResult(
                Descriptor.Key,
                Descriptor.Label,
                false,
                "The source database has no LLM Chats data to transfer.",
                0);
        }

        var target = await LlmChatsTransferDocument.LoadAsync(targetOwner, options, cancellationToken)
            .ConfigureAwait(false);
        if (target.RecordCount > 0 && !context.ReplaceExisting) {
            return new DatabaseTransferItemResult(
                Descriptor.Key,
                Descriptor.Label,
                false,
                "The target already contains LLM Chats data and replacement was not requested.",
                0);
        }

        var history = new HistoryTargetWriteSession(context.TargetProfile, clock);
        using var coordination = history.Transactions.Enter(targetOwner);
        var partition = await LlmChatHistoryTransfer.ValidateAsync(history, target.RecordCount > 0, cancellationToken);
        await LlmChatHistoryTransfer.StageAsync(document.InvocationRecords, partition, history.Outbox, cancellationToken);
        if (target.RecordCount > 0) {
            await LlmChatsTransferDocument.ClearAsync(targetOwner, cancellationToken).ConfigureAwait(false);
        }

        await document.SaveAsync(targetOwner, cancellationToken).ConfigureAwait(false);
        return new DatabaseTransferItemResult(
            Descriptor.Key,
            Descriptor.Label,
            true,
            $"Copied {document.Definitions.Count} definition(s), {document.Conversations.Count} conversation(s), and their versioned transcript/operation audit graph.",
            document.RecordCount);
    }
    private Task<LlmChatsTransferDocument> LoadAsync(ResolvedDatabaseProfile profile, CancellationToken cancellationToken)
        => operations.RunIndependentAsync(profile, async (session, token) => {
            await using var owner = await operations.CreateOwnerAsync<SimpleChatsDbContext>(session, static value => new SimpleChatsDbContext(value), token);
            return await LlmChatsTransferDocument.LoadAsync(owner, options, token);
        }, cancellationToken);
}
