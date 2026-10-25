using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Chat;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class AgentChatPanel {
    private AgentHistoricalReceiptReview? historicalReceiptPage;
    private CancellationTokenSource? historicalReceiptRead;
    private long historicalReceiptView = -1;
    private string? historicalReceiptError;

    private AgentChatHistoricalReceiptState? HistoricalReceiptPresentation {
        get {
            if (chatSession.HistoricalReceiptReference is not { } target) {
                return null;
            }
            var sameView = historicalReceiptView == chatSession.Generation;
            return new(target, sameView ? historicalReceiptPage : null,
                sameView && historicalReceiptRead is not null,
                !isDisposed && chatSession.IsCurrent(chatSession.Generation) && !chatSession.IsLoading && !IsChatInteractionBusy &&
                    !(sameView && historicalReceiptPage?.Stop == AgentHistoricalReceiptReviewStop.ContextChanged),
                sameView ? historicalReceiptError : null);
        }
    }

    private async Task ReadSavedEffectReceiptsAsync(AgentHistoricalReceiptReviewRequest? request, bool nextPage) {
        if (HistoricalReceiptPresentation is not { CanRead: true, Reading: false } presentation) {
            return;
        }
        var cursor = nextPage ? presentation.Page?.NextCursor : null;
        if (nextPage && cursor is null) {
            return;
        }
        var target = presentation.Target;
        var expected = new AgentHistoricalReceiptReviewRequest(target.ExecutionRunId, target.ChatSessionId,
            target.AgentId, target.ExecutionCreatedAtUtc, cursor);
        if (request != expected) {
            return;
        }
        var view = chatSession.Generation;
        var profile = ProfileGenerationSource.GetGeneration();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(chatSession.TargetCancellation);
        historicalReceiptRead = cancellation;
        historicalReceiptView = view;
        historicalReceiptError = null;
        try {
            var result = await ChatExecutionOrchestrator.ReadSavedEffectReceiptsAsync(expected, cancellation.Token);
            if (!OwnsRead()) {
                return;
            }
            var snapshotResponse = result.RunRevision.HasValue || result.Stop is
                AgentHistoricalReceiptReviewStop.ExhaustedSnapshot or AgentHistoricalReceiptReviewStop.LookupLimit;
            var mismatchedPage = cursor is not null && snapshotResponse &&
                (result.CurrentContext != cursor.CurrentContext || result.RunRevision != cursor.RunRevision ||
                    result.JournalRevision != cursor.JournalRevision);
            if (result.Request != request || result.CurrentContext.Generation != profile ||
                    mismatchedPage ||
                    result.Stop == AgentHistoricalReceiptReviewStop.LookupLimit &&
                    (result.NextCursor is null || result.NextCursor.NextProposalOrdinal <= (cursor?.NextProposalOrdinal ?? 0))) {
                throw new InvalidOperationException("Saved receipt review returned a different request, context or page position.");
            }
            historicalReceiptPage = result;
        } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
        } catch (Exception exception) {
            if (OwnsRead()) {
                historicalReceiptError = "Saved receipts could not be reviewed. No recovery or new operation was requested.";
                Logger.LogWarning("Saved receipt review failed. AgentId={AgentId} RunId={RunId} FailureType={FailureType}",
                    target.AgentId, target.ExecutionRunId, exception.GetType().Name);
            }
        } finally {
            if (ReferenceEquals(historicalReceiptRead, cancellation)) {
                historicalReceiptRead = null;
            }
        }

        bool OwnsRead() => !isDisposed && ReferenceEquals(historicalReceiptRead, cancellation) &&
            !cancellation.IsCancellationRequested && chatSession.IsCurrent(view) &&
            ProfileGenerationSource.GetGeneration() == profile && chatSession.HistoricalReceiptReference == target;
    }

    private void RetireHistoricalReceiptRead() {
        historicalReceiptRead?.Cancel();
        historicalReceiptRead = null;
        historicalReceiptPage = null;
        historicalReceiptError = null;
        historicalReceiptView = -1;
    }
}
