using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Mcp.Processes;

public interface IProcessMcpDatabaseBootstrapper
{
    Task EnsureCurrentProfileReadyAsync(CancellationToken cancellationToken = default);
}

public sealed class ProcessMcpDatabaseBootstrapper(
    IDatabaseProfileRuntimeAccessor profileAccessor,
    ISwitchableAppDbContextFactory dbContextFactory,
    ILogger<ProcessMcpDatabaseBootstrapper> logger) : IProcessMcpDatabaseBootstrapper
{
    public Task EnsureCurrentProfileReadyAsync(CancellationToken cancellationToken = default)
    {
        return EnsureProfileReadyAsync(profileAccessor.ResolveCurrentProfile(), cancellationToken);
    }

    private async Task EnsureProfileReadyAsync(ResolvedDatabaseProfile profile, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextForProfileAsync(profile, cancellationToken);
        if (!dbContext.Database.IsRelational())
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            return;
        }

        await CanDoItAllDatabaseMigrationBootstrap.PrepareLegacySqliteAsync(dbContext, logger, cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);
        await CrmHrSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
    }
}
