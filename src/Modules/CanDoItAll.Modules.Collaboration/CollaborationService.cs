using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Collaboration;

public sealed partial class CollaborationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    IActivityStream activityStream,
    ILogger<CollaborationService> logger)
{
    private const string LocalOperatorKey = "local-user";
    private const string LocalOperatorName = "Local operator";

    public event EventHandler? Changed;
}
