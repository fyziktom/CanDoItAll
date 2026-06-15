using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessDispatchExpectedArtifactLoader
{
    public static async Task<IReadOnlyList<ProcessRunAutomationDispatchService.DispatchArtifactExpectation>> LoadAsync(
        AppDbContext dbContext,
        Guid stepDefinitionId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<ProcessArtifactExpectation>()
            .AsNoTracking()
            .Where(item => item.StepDefinitionId == stepDefinitionId)
            .OrderBy(item => item.Title)
            .Select(item => new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                item.Id,
                item.ArtifactKind,
                item.Title,
                item.IsRequired,
                item.TrustRequirement,
                item.SensitivityLevel,
                item.ValidationRequirementSummary,
                item.AllowedFutureUsageSummary))
            .ToListAsync(cancellationToken);
    }
}
