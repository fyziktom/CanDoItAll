using CanDoItAll.Processes.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessAuthoringMutationLock {
    public static Task AcquireAsync(ProcessPersistenceDbContext context, ProcessAuthoringAddress address, CancellationToken cancellationToken) {
        var key = $"process-authoring:{address.DatabaseProfileId:N}:{address.ProjectId:N}:{address.ProjectLifetimeId:N}:{address.DefinitionKey}";
        return context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }
}
