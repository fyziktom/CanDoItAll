using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessSubprocessRunObservationCoordinator(IServiceScopeFactory serviceScopeFactory)
{
    public async Task<Result<ProcessSubprocessRunStartResult>> EnsureRunForStepAsync(
        Guid stepRunId,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        return await processesService.EnsureSubprocessRunForStepAsync(stepRunId, cancellationToken);
    }
}
