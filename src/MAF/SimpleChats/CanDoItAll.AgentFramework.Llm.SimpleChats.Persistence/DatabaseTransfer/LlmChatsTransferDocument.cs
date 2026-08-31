using System.Data;
using System.Text;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.DatabaseTransfer;

internal sealed record LlmChatsTransferDocument(
    int SchemaVersion,
    IReadOnlyList<LlmChatDefinitionRow> Definitions,
    IReadOnlyList<LlmChatDefinitionRevisionRow> Revisions,
    IReadOnlyList<LlmChatDefinitionTagRow> Tags,
    IReadOnlyList<LlmChatConversationRow> Conversations,
    IReadOnlyList<LlmChatTranscriptRow> Transcripts,
    IReadOnlyList<LlmChatMessageRow> Messages,
    IReadOnlyList<LlmChatOperationRow> Operations,
    IReadOnlyList<LlmChatInvocationRecordRow> InvocationRecords,
    IReadOnlyList<LlmChatOperationEventRow> OperationEvents)
{
    public const int CurrentSchemaVersion = 8;

    public int RecordCount =>
        Definitions.Count + Revisions.Count + Tags.Count + Conversations.Count + Transcripts.Count +
        Messages.Count + Operations.Count + InvocationRecords.Count + OperationEvents.Count;

    public static async Task<LlmChatsTransferDocument> LoadAsync(
        DbContext dbContext,
        LlmChatTransferOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        var ambientTransaction = dbContext.Database.CurrentTransaction;
        if (dbContext.Database.IsRelational() &&
            ambientTransaction is not null &&
            !ProvidesRepeatableReads(ambientTransaction.GetDbTransaction().IsolationLevel))
        {
            throw new InvalidOperationException(
                "Loading an LLM Chats transfer inside an existing relational transaction requires repeatable-read or serializable isolation.");
        }

        await using var snapshot = dbContext.Database.IsRelational() && ambientTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
                .ConfigureAwait(false)
            : null;
        var counts = new (string Name, long Count)[]
        {
            ("definitions", await dbContext.Set<LlmChatDefinitionRow>().LongCountAsync(cancellationToken).ConfigureAwait(false)),
            ("definition revisions", await dbContext.Set<LlmChatDefinitionRevisionRow>().LongCountAsync(cancellationToken).ConfigureAwait(false)),
            ("definition tags", await dbContext.Set<LlmChatDefinitionTagRow>().LongCountAsync(cancellationToken).ConfigureAwait(false)),
            ("conversations", await dbContext.Set<LlmChatConversationRow>().LongCountAsync(cancellationToken).ConfigureAwait(false)),
            ("transcripts", await dbContext.Set<LlmChatTranscriptRow>().LongCountAsync(cancellationToken).ConfigureAwait(false)),
            ("messages", await dbContext.Set<LlmChatMessageRow>().LongCountAsync(cancellationToken).ConfigureAwait(false)),
            ("operations", await dbContext.Set<LlmChatOperationRow>().LongCountAsync(cancellationToken).ConfigureAwait(false)),
            ("invocation records", await dbContext.Set<LlmChatInvocationRecordRow>().LongCountAsync(cancellationToken).ConfigureAwait(false)),
            ("operation events", await dbContext.Set<LlmChatOperationEventRow>().LongCountAsync(cancellationToken).ConfigureAwait(false))
        };
        var overBound = counts.FirstOrDefault(item => item.Count > options.MaximumRecordsPerCollection);
        if (overBound.Count > options.MaximumRecordsPerCollection)
        {
            throw new InvalidDataException(
                $"The LLM Chats transfer contains {overBound.Count} {overBound.Name}, exceeding the configured collection limit of {options.MaximumRecordsPerCollection}.");
        }

        var total = counts.Sum(item => item.Count);
        if (total > options.MaximumTotalRecords)
        {
            throw new InvalidDataException(
                $"The LLM Chats transfer contains {total} records, exceeding the configured total limit of {options.MaximumTotalRecords}.");
        }

        var remainingTotalRecords = options.MaximumTotalRecords;
        var document = new LlmChatsTransferDocument(
            CurrentSchemaVersion,
            await LoadBoundedAsync(dbContext.Set<LlmChatDefinitionRow>(), "definitions").ConfigureAwait(false),
            await LoadBoundedAsync(dbContext.Set<LlmChatDefinitionRevisionRow>(), "definition revisions").ConfigureAwait(false),
            await LoadBoundedAsync(dbContext.Set<LlmChatDefinitionTagRow>(), "definition tags").ConfigureAwait(false),
            await LoadBoundedAsync(dbContext.Set<LlmChatConversationRow>(), "conversations").ConfigureAwait(false),
            await LoadBoundedAsync(dbContext.Set<LlmChatTranscriptRow>(), "transcripts").ConfigureAwait(false),
            await LoadBoundedAsync(dbContext.Set<LlmChatMessageRow>(), "messages").ConfigureAwait(false),
            await LoadBoundedAsync(dbContext.Set<LlmChatOperationRow>(), "operations").ConfigureAwait(false),
            await LoadBoundedAsync(dbContext.Set<LlmChatInvocationRecordRow>(), "invocation records").ConfigureAwait(false),
            await LoadBoundedAsync(dbContext.Set<LlmChatOperationEventRow>(), "operation events").ConfigureAwait(false));
        if (snapshot is not null)
        {
            await snapshot.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return document;

        async Task<T[]> LoadBoundedAsync<T>(IQueryable<T> query, string name) where T : class
        {
            var maximumLoadedRecords = Math.Min(options.MaximumRecordsPerCollection, remainingTotalRecords);
            var rows = await query
                .AsNoTracking()
                .Take(maximumLoadedRecords + 1)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            if (rows.Length > options.MaximumRecordsPerCollection)
            {
                throw new InvalidDataException(
                    $"The LLM Chats transfer contains at least {rows.Length} {name}, exceeding the configured collection limit of {options.MaximumRecordsPerCollection}.");
            }

            if (rows.Length > remainingTotalRecords)
            {
                var loadedRecords = options.MaximumTotalRecords - remainingTotalRecords + rows.Length;
                throw new InvalidDataException(
                    $"The LLM Chats transfer contains at least {loadedRecords} records, exceeding the configured total limit of {options.MaximumTotalRecords}.");
            }

            remainingTotalRecords -= rows.Length;
            return rows;
        }
    }

    private static bool ProvidesRepeatableReads(IsolationLevel isolationLevel)
        => isolationLevel is IsolationLevel.RepeatableRead or IsolationLevel.Snapshot or IsolationLevel.Serializable;

    public void ValidateForImport()
    {
        if (SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported LLM Chats transfer schema version {SchemaVersion}.");
        }

        ValidateRowValues();
        EnsureUnique(Definitions.Select(row => row.Id), "definition id");
        EnsureUnique(Revisions.Select(row => (row.DefinitionId, row.Revision)), "definition revision");
        EnsureUnique(Tags.Select(row => (row.DefinitionId, row.Tag)), "definition tag");
        EnsureUnique(Conversations.Select(row => row.Id), "conversation id");
        EnsureUnique(Transcripts.Select(row => row.ConversationId), "transcript id");
        EnsureUnique(Messages.Select(row => row.EntryId), "message entry id");
        EnsureUnique(Operations.Select(row => row.Id), "operation id");
        EnsureUnique(InvocationRecords.Select(row => (row.OperationId, row.Ordinal)), "invocation ordinal");
        EnsureUnique(OperationEvents.Select(row => (row.OperationId, row.Sequence)), "operation event sequence");

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
            InvocationRecords.Any(row => !operationIds.Contains(row.OperationId)) ||
            OperationEvents.Any(row => !operationIds.Contains(row.OperationId)))
        {
            throw new InvalidDataException("An LLM Chat operation or invocation record is detached from its parent.");
        }

        var operationById = Operations.ToDictionary(row => row.Id);
        var transcriptByConversation = Transcripts.ToDictionary(row => row.ConversationId);
        if (Transcripts.Any(row =>
                row.ActiveTurnId is { } activeTurnId &&
                (!operationById.TryGetValue(activeTurnId, out var operation) ||
                 operation.ConversationId != row.ConversationId ||
                 IsTerminal(operation.Status))) ||
            Operations.Any(row =>
                !transcriptByConversation.TryGetValue(row.ConversationId, out var transcript) ||
                (!IsTerminal(row.Status) && transcript.ActiveTurnId != row.Id) ||
                (IsTerminal(row.Status) && transcript.ActiveTurnId == row.Id)) ||
            Messages.Any(row =>
                row.Role != LlmMessageRole.System &&
                (!operationById.TryGetValue(row.TurnId, out var operation) ||
                 operation.ConversationId != row.ConversationId)))
        {
            throw new InvalidDataException("An LLM Chat operation graph contains an invalid active-turn or message relationship.");
        }

        var messageById = Messages.ToDictionary(row => row.EntryId);
        if (Operations.Any(row =>
                row.AssistantEntryId is { } assistantEntryId &&
                (!messageById.TryGetValue(assistantEntryId, out var message) ||
                 message.ConversationId != row.ConversationId ||
                 message.Role != LlmMessageRole.Assistant)) ||
            Operations.Where(row => !IsTerminal(row.Status) && row.Status != LlmChatOperationStatus.RecoveryRequired)
                .GroupBy(row => row.ConversationId)
                .Any(group => group.Count() > 1))
        {
            throw new InvalidDataException("An LLM Chat operation graph contains inconsistent conversation or result relationships.");
        }

        if (InvocationRecords.Any(row =>
                row.Ordinal < 1 ||
                row.ProviderProfileId == Guid.Empty ||
                !Enum.IsDefined(row.ProviderKind) ||
                row.RequestedThinkingEffort is { } requestedEffort && !Enum.IsDefined(requestedEffort) ||
                row.EffectiveThinkingEffort is { } effectiveEffort && !Enum.IsDefined(effectiveEffort) ||
                !Enum.IsDefined(row.DeliveryMode) ||
                !Enum.IsDefined(row.Outcome) ||
                row.InputTokens < 0 ||
                row.OutputTokens < 0 ||
                row.CachedInputTokens < 0 ||
                row.CachedInputTokens > row.InputTokens ||
                !Enum.IsDefined(row.UsageStatus) ||
                !Enum.IsDefined(row.PricingStatus) ||
                row.ProviderCostUsd is < 0m ||
                row.CalculatedCostUsd is < 0m ||
                row.PricingProfileHash.Length is not 0 and not ProviderPricingSnapshot.ProfileHashLength ||
                row.PricingVersion.Length > LlmChatInvocationRecord.MaximumPricingVersionLength ||
                row.PricingStatus == LlmChatInvocationPricingEvidenceStatus.ProviderReported &&
                row.ProviderCostUsd is null ||
                row.PricingStatus == LlmChatInvocationPricingEvidenceStatus.CalculatedAtExecution &&
                row.CalculatedCostUsd is null ||
                row.PricingStatus == LlmChatInvocationPricingEvidenceStatus.Unpriced &&
                (row.ProviderCostUsd is not null || row.CalculatedCostUsd is not null) ||
                row.PricingStatus != LlmChatInvocationPricingEvidenceStatus.Unpriced &&
                (row.PricingProfileHash.Length == 0 || row.PricingVersion.Length == 0) ||
                row.CompletedAtUtc < row.StartedAtUtc ||
                row.FinishReason.Length > LlmChatInvocationRecord.MaximumFinishReasonLength ||
                ((row.Outcome == LlmChatInvocationOutcome.Succeeded) != (row.FinishReason.Length > 0)) ||
                ((row.Outcome == LlmChatInvocationOutcome.Succeeded) == (row.FailureCode.Length > 0)) ||
                row.FailureCode.Length > 0 &&
                !row.FailureCode.StartsWith(LlmChatErrorCodes.Prefix, StringComparison.Ordinal)) ||
            InvocationRecords.GroupBy(row => row.OperationId).Any(group =>
                group.OrderBy(row => row.Ordinal)
                    .Select((row, index) => row.Ordinal != index + 1)
                    .Any(invalid => invalid)))
        {
            throw new InvalidDataException("An LLM Chat invocation record contains invalid completion evidence.");
        }

        if (OperationEvents.Any(row => row.Sequence < 1 || !IsValidEvent(row)) ||
            OperationEvents.GroupBy(row => row.OperationId).Any(group =>
                group.OrderBy(row => row.Sequence)
                    .Zip(group.OrderBy(row => row.Sequence).Skip(1), (left, right) => right.Sequence <= left.Sequence)
                    .Any(invalid => invalid)))
        {
            throw new InvalidDataException("An LLM Chat operation contains an invalid event journal.");
        }

        var eventHighWaterByOperation = OperationEvents
            .GroupBy(row => row.OperationId)
            .ToDictionary(group => group.Key, group => group.Max(row => row.Sequence));
        if (Operations.Any(row =>
                row.LastEventSequence < 0 ||
                eventHighWaterByOperation.GetValueOrDefault(row.Id) > row.LastEventSequence ||
                row.ExecutionEpoch < 0 ||
                !Enum.IsDefined(row.Kind) ||
                !Enum.IsDefined(row.Status) ||
                !Enum.IsDefined(row.DispatchPhase) ||
                (row.ExecutionOwnerId is null) != (row.ClaimedAtUtc is null) ||
                (row.ExecutionOwnerId is null) != (row.HeartbeatAtUtc is null) ||
                (row.ExecutionOwnerId is null) != (row.LeaseExpiresAtUtc is null) ||
                !IsValidOperationState(row)))
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
        await dbContext.AddRangeAsync(OperationEvents, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task ClearAsync(DbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Set<LlmChatOperationEventRow>().ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
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

    private void ValidateRowValues()
    {
        if (Definitions.Any(row =>
                row.Id == Guid.Empty ||
                !Enum.IsDefined(row.Status) ||
                row.CurrentRevision < 1 ||
                row.ConcurrencyToken < 0 ||
                row.UpdatedAtUtc < row.CreatedAtUtc) ||
            Revisions.Any(row =>
                row.DefinitionId == Guid.Empty ||
                row.Revision < 1 ||
                row.ProviderProfileId == Guid.Empty ||
                !Enum.IsDefined(row.ProviderKind) ||
                row.ThinkingEffort is { } effort && !Enum.IsDefined(effort) ||
                row.TimeoutTicks is <= 0) ||
            Conversations.Any(row =>
                row.Id == Guid.Empty ||
                row.DefinitionId == Guid.Empty ||
                row.DefinitionRevision < 1 ||
                !Enum.IsDefined(row.Status) ||
                !Enum.IsDefined(row.Origin) ||
                row.ConcurrencyToken < 0 ||
                row.UpdatedAtUtc < row.CreatedAtUtc) ||
            Transcripts.Any(row =>
                row.ConversationId == Guid.Empty ||
                row.ProviderId == Guid.Empty ||
                !Enum.IsDefined(row.ProviderKind) ||
                row.TranscriptRevision < 0 ||
                row.EntryCount < 0 ||
                (row.ActiveTurnId is null) != (row.PendingUserEntryId is null) ||
                (row.ActiveTurnId is null) != (row.TurnAdmittedAtUtc is null) ||
                (row.ActiveTurnId is null) != (row.TurnAdmittedRevision is null)) ||
            Messages.Any(row =>
                row.EntryId == Guid.Empty ||
                row.ConversationId == Guid.Empty ||
                row.TurnId == Guid.Empty ||
                row.Sequence < 1 ||
                !Enum.IsDefined(row.Role) ||
                row.Text.Length > LlmConversationTranscriptEntry.MaximumTextLength ||
                !IsValidUsage(row.InputTokens, row.OutputTokens, row.CachedInputTokens)))
        {
            throw new InvalidDataException("The LLM Chats transfer contains an invalid definition or conversation graph value.");
        }
    }

    private static bool IsValidOperationState(LlmChatOperationRow row)
    {
        if (row.Id == Guid.Empty ||
            row.ConversationId == Guid.Empty ||
            !HasValidAttributionScope(row) ||
            row.ExpectedTranscriptRevision < 0 ||
            row.CancellationGeneration < 0 ||
            row.ConcurrencyToken < 0 ||
            row.CompletedAtUtc is { } completedAtUtc && completedAtUtc < row.StartedAtUtc ||
            row.ProviderDispatchStartedAtUtc is { } dispatchStartedAtUtc &&
            row.TurnAdmittedAtUtc is { } turnAdmittedAtUtc && dispatchStartedAtUtc < turnAdmittedAtUtc ||
            row.ProviderDispatchReturnedAtUtc is { } dispatchReturnedAtUtc &&
            row.ProviderDispatchStartedAtUtc is { } startedAtUtc && dispatchReturnedAtUtc < startedAtUtc ||
            row.TranscriptCompletedAtUtc is { } transcriptCompletedAtUtc &&
            row.ProviderDispatchStartedAtUtc is { } providerStartedAtUtc && transcriptCompletedAtUtc < providerStartedAtUtc)
        {
            return false;
        }

        var hasOwner = row.ExecutionOwnerId is not null;
        var isTerminal = IsTerminal(row.Status);
        if ((isTerminal || row.Status == LlmChatOperationStatus.RecoveryRequired) && hasOwner)
        {
            return false;
        }

        var hasConsistentDispatchEvidence = row.DispatchPhase switch
        {
            LlmChatDispatchPhase.Queued =>
                row.TurnAdmittedAtUtc is not null &&
                row.ProviderDispatchStartedAtUtc is null &&
                row.ProviderDispatchReturnedAtUtc is null,
            LlmChatDispatchPhase.Claimed =>
                row.TurnAdmittedAtUtc is not null &&
                row.ProviderDispatchStartedAtUtc is null &&
                row.ProviderDispatchReturnedAtUtc is null,
            LlmChatDispatchPhase.ProviderDispatchStarted =>
                row.TurnAdmittedAtUtc is not null &&
                row.ProviderDispatchStartedAtUtc is not null &&
                row.ProviderDispatchReturnedAtUtc is null,
            LlmChatDispatchPhase.ProviderDispatchReturned =>
                row.TurnAdmittedAtUtc is not null &&
                row.ProviderDispatchStartedAtUtc is not null &&
                row.ProviderDispatchReturnedAtUtc is not null,
            _ => false
        };
        if (!hasConsistentDispatchEvidence)
        {
            return false;
        }

        return row.Status switch
        {
            LlmChatOperationStatus.Succeeded =>
                row.CompletedAtUtc is not null &&
                row.ResultingTranscriptRevision > row.ExpectedTranscriptRevision &&
                row.AssistantEntryId is not null &&
                row.TranscriptCompletedAtUtc is not null &&
                row.FailureCode.Length == 0,
            LlmChatOperationStatus.Failed or LlmChatOperationStatus.Cancelled =>
                row.CompletedAtUtc is not null &&
                row.ResultingTranscriptRevision is null &&
                row.AssistantEntryId is null &&
                row.FailureCode.StartsWith(LlmChatErrorCodes.Prefix, StringComparison.Ordinal),
            LlmChatOperationStatus.RecoveryRequired =>
                row.CompletedAtUtc is null &&
                row.ResultingTranscriptRevision is null &&
                row.AssistantEntryId is null &&
                row.FailureCode.StartsWith(LlmChatErrorCodes.Prefix, StringComparison.Ordinal),
            _ => row.CompletedAtUtc is null &&
                 row.ResultingTranscriptRevision is null &&
                 row.AssistantEntryId is null &&
                 row.FailureCode.Length == 0
        };
    }

    private static bool HasValidAttributionScope(LlmChatOperationRow row)
    {
        if (row.AttributionScopeKind is null)
        {
            return row.AttributionScopeKey.Length == 0;
        }

        if (!Enum.IsDefined(row.AttributionScopeKind.Value) ||
            row.AttributionScopeKey.Length > LlmChatOperation.MaximumAttributionScopeKeyLength)
        {
            return false;
        }

        try
        {
            _ = new WorkspaceScopeDescriptor(
                row.AttributionScopeKind.Value,
                row.AttributionScopeKey);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsValidUsage(int? inputTokens, int? outputTokens, int? cachedInputTokens)
    {
        if (inputTokens is null && outputTokens is null && cachedInputTokens is null)
        {
            return true;
        }

        return inputTokens is >= 0 &&
               outputTokens is >= 0 &&
               cachedInputTokens is >= 0 &&
               cachedInputTokens <= inputTokens;
    }

    private static bool IsTerminal(LlmChatOperationStatus status)
        => status is
            LlmChatOperationStatus.Succeeded or
            LlmChatOperationStatus.Failed or
            LlmChatOperationStatus.Cancelled;

    private static bool IsValidEvent(LlmChatOperationEventRow row)
    {
        if (!Enum.IsDefined(row.Kind) ||
            row.Status is { } candidateStatus && !Enum.IsDefined(candidateStatus) ||
            row.InvocationOutcome is { } candidateOutcome && !Enum.IsDefined(candidateOutcome) ||
            row.DeliveryMode is { } deliveryMode && !Enum.IsDefined(deliveryMode) ||
            row.FinishReason.Length > LlmChatInvocationRecord.MaximumFinishReasonLength ||
            Encoding.UTF8.GetByteCount(row.Text) > LlmChatStreamingLimits.MaximumPersistedEventTextBytes ||
            (!string.IsNullOrEmpty(row.FailureCode) &&
             !row.FailureCode.StartsWith(LlmChatErrorCodes.Prefix, StringComparison.Ordinal)))
        {
            return false;
        }

        var hasUsage = IsValidUsage(row.InputTokens, row.OutputTokens, row.CachedInputTokens) &&
                       row.InputTokens is not null;
        return row.Kind switch
        {
            LlmChatOperationEventKind.StateChanged =>
                row.Status is { } status &&
                row.AttemptOrdinal is null &&
                row.InvocationOutcome is null &&
                row.DeliveryMode is null &&
                row.FinishReason.Length == 0 &&
                row.Text.Length == 0 &&
                (status == LlmChatOperationStatus.Succeeded
                    ? row.Model.Length > 0 && hasUsage && row.FailureCode.Length == 0
                    : status is LlmChatOperationStatus.Failed or LlmChatOperationStatus.Cancelled
                        ? row.Model.Length == 0 && hasUsage && row.FailureCode.Length > 0
                        : status == LlmChatOperationStatus.RecoveryRequired
                            ? row.Model.Length == 0 && !hasUsage && row.FailureCode.Length > 0
                        : row.Model.Length == 0 && !hasUsage && row.FailureCode.Length == 0),
            LlmChatOperationEventKind.AttemptStarted =>
                row.Status is null &&
                row.AttemptOrdinal > 0 &&
                row.InvocationOutcome is null &&
                row.DeliveryMode is not null &&
                row.FinishReason.Length == 0 &&
                row.Text.Length == 0 &&
                row.Model.Length > 0 &&
                row.FailureCode.Length == 0 &&
                !hasUsage,
            LlmChatOperationEventKind.AttemptFinished =>
                row.Status is null &&
                row.AttemptOrdinal > 0 &&
                row.InvocationOutcome is { } outcome &&
                row.DeliveryMode is not null &&
                row.Text.Length == 0 &&
                row.Model.Length > 0 &&
                ((outcome == LlmChatInvocationOutcome.Succeeded) == (row.FinishReason.Length > 0)) &&
                hasUsage &&
                ((outcome == LlmChatInvocationOutcome.Succeeded) == (row.FailureCode.Length == 0)),
            LlmChatOperationEventKind.TextDelta =>
                row.Status is null &&
                row.AttemptOrdinal > 0 &&
                row.InvocationOutcome is null &&
                row.DeliveryMode is null &&
                row.FinishReason.Length == 0 &&
                row.Text.Length > 0 &&
                row.Model.Length == 0 &&
                row.FailureCode.Length == 0 &&
                !hasUsage,
            _ => false
        };
    }
}
