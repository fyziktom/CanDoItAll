namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessCanvasSurfaceFactory
{
    private static double ResolveDefinitionBranchNodeX(
        ProcessStepEditorModel step,
        IReadOnlyList<ProcessStepEditorModel> allSteps)
    {
        if (step.BranchCanvasX != 0)
        {
            return step.BranchCanvasX;
        }

        var stepX = ResolveDefinitionStepX(step, allSteps);
        var directDependents = allSteps
            .Where(candidate => ProcessCanvasBranching.GetOrderedDependencies(candidate)
                .Any(dependency => dependency.DependsOnStepId == step.Id))
            .Select(candidate => ResolveDefinitionStepX(candidate, allSteps))
            .ToList();
        if (directDependents.Count == 0)
        {
            return stepX + 320d;
        }

        var closestDependentX = directDependents.Min();
        return closestDependentX - stepX < 420d
            ? stepX + 320d
            : stepX + ((closestDependentX - stepX) / 2d);
    }

    private static double ResolveDefinitionBranchNodeY(
        ProcessStepEditorModel step,
        IReadOnlyList<ProcessStepEditorModel> allSteps)
    {
        if (step.BranchCanvasY != 0)
        {
            return step.BranchCanvasY;
        }

        var stepY = ResolveDefinitionStepY(step);
        var directDependents = allSteps
            .Where(candidate => ProcessCanvasBranching.GetOrderedDependencies(candidate)
                .Any(dependency => dependency.DependsOnStepId == step.Id))
            .ToList();
        if (directDependents.Count == 0)
        {
            return stepY;
        }

        return directDependents.All(candidate => Math.Abs(ResolveDefinitionStepY(candidate) - stepY) < 90d)
            ? stepY + 220d
            : directDependents.Average(ResolveDefinitionStepY);
    }

    private static double ResolveDefinitionRoleNodeX(ProcessRoleEditorModel role, ProcessDefinitionEditorModel editor)
    {
        if (role.CanvasX != 0)
        {
            return role.CanvasX;
        }

        if (editor.Steps.Count == 0)
        {
            return -180d;
        }

        var leftMostStepX = editor.Steps
            .Select(step => step.CanvasX != 0 ? step.CanvasX : 140d)
            .Min();
        return leftMostStepX - 360d;
    }

    private static double ResolveDefinitionRoleNodeY(ProcessRoleEditorModel role, int index)
    {
        if (role.CanvasY != 0)
        {
            return role.CanvasY;
        }

        return 120d + (index * 210d);
    }

    private static double ResolveDefinitionStepX(ProcessStepEditorModel step, IReadOnlyList<ProcessStepEditorModel> allSteps)
    {
        var index = 0;
        for (var candidateIndex = 0; candidateIndex < allSteps.Count; candidateIndex++)
        {
            if (ReferenceEquals(allSteps[candidateIndex], step))
            {
                index = candidateIndex;
                break;
            }
        }

        return step.CanvasX != 0
            ? step.CanvasX
            : 140d + (index * 280d);
    }

    private static double ResolveDefinitionStepY(ProcessStepEditorModel step)
    {
        return step.CanvasY != 0
            ? step.CanvasY
            : 180d;
    }

    private static double ResolveRunBranchNodeX(
        ProcessStepRunViewModel stepRun,
        IReadOnlyList<ProcessStepRunViewModel> allSteps)
    {
        var directDependents = allSteps
            .Where(candidate => candidate.Dependencies.Any(dependency => dependency.DependsOnStepDefinitionId == stepRun.StepDefinitionId))
            .Select(candidate => 140d + ((candidate.Sequence - 1) * 280d))
            .ToList();
        var stepX = 140d + ((stepRun.Sequence - 1) * 280d);
        if (directDependents.Count == 0)
        {
            return stepX + 320d;
        }

        var closestDependentX = directDependents.Min();
        return closestDependentX - stepX < 420d
            ? stepX + 320d
            : stepX + ((closestDependentX - stepX) / 2d);
    }

    private static double ResolveRunBranchNodeY(
        ProcessStepRunViewModel stepRun,
        IReadOnlyList<ProcessStepRunViewModel> allSteps)
    {
        var directDependents = allSteps
            .Where(candidate => candidate.Dependencies.Any(dependency => dependency.DependsOnStepDefinitionId == stepRun.StepDefinitionId))
            .ToList();
        var stepY = stepRun.Status == ProcessStepRunStatus.Blocked ? 260d : 180d;
        if (directDependents.Count == 0)
        {
            return stepY;
        }

        return directDependents.All(candidate => candidate.Status != ProcessStepRunStatus.Blocked)
            ? stepY + 220d
            : 260d;
    }
}
