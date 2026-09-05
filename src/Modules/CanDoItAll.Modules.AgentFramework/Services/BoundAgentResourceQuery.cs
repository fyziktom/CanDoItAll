using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public interface IBoundAgentResourceQuery {
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}

public sealed class BoundAgentResourceQuery(IDbContextFactory<AppDbContext> dbContextFactory) : IBoundAgentResourceQuery {
    public async Task<int> CountAsync(CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<AiResourceBinding>().CountAsync(
            item => item.TechnicalAgentId.HasValue && item.BindingStatus == AiResourceBindingStatus.Bound,
            cancellationToken);
    }
}
