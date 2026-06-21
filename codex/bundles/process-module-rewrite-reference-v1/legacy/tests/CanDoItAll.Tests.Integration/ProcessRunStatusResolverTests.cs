using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessRunStatusResolverTests
{
    [Fact]
    public void Resolve_returns_blocked_when_completed_error_branch_has_no_handler()
    {
        var contractStepId = Guid.NewGuid();
        var implementationStepId = Guid.NewGuid();
        var errorOutcome = new ProcessStepBranchOutcomeDefinition
        {
            Id = Guid.NewGuid(),
            StepDefinitionId = contractStepId,
            Key = ProcessCanvasBranching.ErrorRouteKey,
            Title = ProcessCanvasBranching.ErrorRouteTitle,
            Description = "System exception route."
        };
        var defaultOutcome = new ProcessStepBranchOutcomeDefinition
        {
            Id = Guid.NewGuid(),
            StepDefinitionId = contractStepId,
            Key = ProcessCanvasBranching.DefaultRouteKey,
            Title = ProcessCanvasBranching.DefaultRouteTitle,
            Description = "Continue."
        };
        var stepRuns = new[]
        {
            new ProcessStepRun
            {
                StepDefinitionId = contractStepId,
                Status = ProcessStepRunStatus.Completed,
                SelectedBranchOutcomeId = errorOutcome.Id,
                SelectedBranchOutcomeTitle = errorOutcome.Title
            },
            new ProcessStepRun
            {
                StepDefinitionId = implementationStepId,
                Status = ProcessStepRunStatus.Skipped
            }
        };
        var dependenciesByStepId = new Dictionary<Guid, List<ProcessStepDependencyDefinition>>
        {
            [implementationStepId] =
            [
                new ProcessStepDependencyDefinition
                {
                    StepDefinitionId = implementationStepId,
                    DependsOnStepId = contractStepId,
                    DependsOnBranchOutcomeId = defaultOutcome.Id
                }
            ]
        };
        var branchOutcomesByStepId = new Dictionary<Guid, List<ProcessStepBranchOutcomeDefinition>>
        {
            [contractStepId] = [defaultOutcome, errorOutcome]
        };

        var status = ProcessRunStatusResolver.Resolve(stepRuns, dependenciesByStepId, branchOutcomesByStepId);

        Assert.Equal(ProcessRunStatus.Blocked, status);
    }

    [Fact]
    public void Resolve_returns_completed_when_completed_error_branch_has_completed_handler()
    {
        var contractStepId = Guid.NewGuid();
        var exceptionHandlerStepId = Guid.NewGuid();
        var errorOutcome = new ProcessStepBranchOutcomeDefinition
        {
            Id = Guid.NewGuid(),
            StepDefinitionId = contractStepId,
            Key = ProcessCanvasBranching.ErrorRouteKey,
            Title = ProcessCanvasBranching.ErrorRouteTitle,
            Description = "System exception route."
        };
        var stepRuns = new[]
        {
            new ProcessStepRun
            {
                StepDefinitionId = contractStepId,
                Status = ProcessStepRunStatus.Completed,
                SelectedBranchOutcomeId = errorOutcome.Id,
                SelectedBranchOutcomeTitle = errorOutcome.Title
            },
            new ProcessStepRun
            {
                StepDefinitionId = exceptionHandlerStepId,
                Status = ProcessStepRunStatus.Completed
            }
        };
        var dependenciesByStepId = new Dictionary<Guid, List<ProcessStepDependencyDefinition>>
        {
            [exceptionHandlerStepId] =
            [
                new ProcessStepDependencyDefinition
                {
                    StepDefinitionId = exceptionHandlerStepId,
                    DependsOnStepId = contractStepId,
                    DependsOnBranchOutcomeId = errorOutcome.Id
                }
            ]
        };
        var branchOutcomesByStepId = new Dictionary<Guid, List<ProcessStepBranchOutcomeDefinition>>
        {
            [contractStepId] = [errorOutcome]
        };

        var status = ProcessRunStatusResolver.Resolve(stepRuns, dependenciesByStepId, branchOutcomesByStepId);

        Assert.Equal(ProcessRunStatus.Completed, status);
    }
}
