using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentHistoricalReceiptReviewService {
    Task<AgentHistoricalReceiptReview> ReadSavedEffectReceiptsAsync(AgentHistoricalReceiptReviewRequest request,
        CancellationToken cancellationToken = default);
}