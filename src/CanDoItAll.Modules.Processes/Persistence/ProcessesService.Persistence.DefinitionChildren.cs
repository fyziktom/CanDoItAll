using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private async Task SaveDefinitionChildrenAsync(
        AppDbContext dbContext,
        Guid workingVersionId,
        ProcessDefinitionEditorModel model,
        CancellationToken cancellationToken)
    {
        var context = await LoadDefinitionChildrenSaveContextAsync(dbContext, workingVersionId, cancellationToken);
        var resolvedRoles = await PersistDefinitionRolesAsync(context, model, cancellationToken);
        var resolvedSteps = await PersistDefinitionStepsAsync(context, model, cancellationToken);

        await PersistDefinitionDependenciesAsync(context, resolvedSteps, cancellationToken);
        await PersistDefinitionAssignmentsAndArtifactsAsync(context, resolvedSteps, cancellationToken);
        RemoveDeletedDefinitionChildren(context);
    }

    private sealed record ResolvedProcessRole(Guid RoleId, ProcessRoleEditorModel Model);

    private sealed record ResolvedProcessStep(
        Guid StepId,
        bool ReusesExistingEntity,
        ProcessStepDefinition Entity,
        ProcessStepEditorModel Model,
        IReadOnlyList<ProcessStepDependencyEditorModel> Dependencies);
}
