using CanDoItAll.Modules.Projects.Pages.Components;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects.Pages;

public sealed record ProjectNativeEditorAcquisition(ProjectEditorModel Model, ProjectPendingScope Scope);

public sealed class ProjectNativeDefinitionOperations(
    ProjectsService projects,
    ProjectDefinitionReader definitions,
    ProjectDefinitionWriter writer,
    ProjectDefinitionOperationProtocol protocol,
    ProjectDefinitionReceiptReader receipts,
    IProjectDefinitionRecoveryCodec codec) {
    private ProjectDefinitionInvocation Invocation => new(ProjectMutationCallerScope.LocalWorkspace, new NativeReceiptAccess(definitions));

    public Task<ProjectDefinitionWriteContext> GetContextAsync(CancellationToken cancellationToken = default)
        => protocol.GetWriteContextAsync(ProjectMutationCallerScope.LocalWorkspace, cancellationToken);

    public async Task<ProjectNativeEditorAcquisition> AcquireEditorAsync(Guid? projectId, CancellationToken cancellationToken = default) {
        var before = await GetContextAsync(cancellationToken);
        ProjectEditorModel model;
        if (projectId is { } id) {
            var definition = await definitions.ReadAsync(id, cancellationToken);
            if (definition.IsFailure) {
                throw new InvalidOperationException("The requested project definition is unavailable.");
            }
            model = ProjectEditorDraft.CreateModel(definition.Value!);
        } else {
            model = await projects.GetAsync(null, cancellationToken);
        }
        var after = await GetContextAsync(cancellationToken);
        RequireScope(Scope(before), after);
        return new(model, Scope(before));
    }

    public async Task<Result<ProjectDefinitionSnapshot>> ReadCurrentAsync(Guid projectId, ProjectPendingScope original,
        CancellationToken cancellationToken = default) {
        RequireScope(original, await GetContextAsync(cancellationToken));
        var result = await definitions.ReadAsync(projectId, cancellationToken);
        RequireScope(original, await GetContextAsync(cancellationToken));
        return result;
    }

    public ProjectDefinitionFrozenSubmission Freeze(ProjectEditorSubmission submitted, ProjectPendingScope original,
        ProjectDefinitionWriteContext context) {
        RequireScope(original, context);
        var model = submitted.Model;
        ProjectDefinitionInputBounds.Validate(model, context.Limits);
        var scalars = new ProjectDefinitionScalars(model.Name, model.Description, model.Objective, model.Status, model.CurrentPhase, model.TargetDateUtc);
        var phases = model.Phases.Select(row => new ProjectDefinitionPhaseInput(row.Id, row.Name, row.Goal, row.Status,
            row.StartDateUtc, row.EndDateUtc)).ToArray();
        var options = model.Options.Select(row => new ProjectDefinitionOptionInput(row.Id, row.Category, row.OptionName, row.Notes)).ToArray();
        var external = string.IsNullOrWhiteSpace(model.ExternalNamespace) && string.IsNullOrWhiteSpace(model.ExternalKey)
            ? null : new ProjectDefinitionExternalIdentity(model.ExternalNamespace ?? string.Empty, model.ExternalKey ?? string.Empty);
        var operation = new ProjectDefinitionOperation(context.Namespace, context.Epoch, Guid.NewGuid(), context.CallerBinding);
        ProjectDefinitionSubmission command = model.Id is null
            ? new ProjectDefinitionSubmission.Create(new(operation, ProjectDefinitionRequestVersion.VersionOne, new(scalars, phases, options, external)))
            : model.ExpectedDefinition is { } expected && expected.Project.ProjectId == model.Id
                ? new ProjectDefinitionSubmission.Replace(new(operation, ProjectDefinitionRequestVersion.VersionOne, new(expected, scalars, phases, options, external)))
                : throw new InvalidOperationException("The original definition expectation is required before saving this draft.");
        return codec.Freeze(command, context.Limits);
    }

    public async Task<ProjectPendingDispatchOutcome> DispatchAsync(ProjectDefinitionSubmission submitted, CancellationToken cancellationToken = default)
        => submitted switch {
            ProjectDefinitionSubmission.Create create => new ProjectPendingDispatchOutcome.Full(await writer.CreateAsync(create.Request, Invocation, cancellationToken)),
            ProjectDefinitionSubmission.Replace replace => new ProjectPendingDispatchOutcome.Full(await writer.ReplaceAsync(replace.Request, Invocation, cancellationToken)),
            _ => throw new InvalidOperationException("The native full-definition editor cannot dispatch a scalar-only submission.")
        };

    public Task<ProjectDefinitionLookup> LookupAsync(ProjectDefinitionOperation operation, CancellationToken cancellationToken = default)
        => receipts.LookupAsync(operation, Invocation, cancellationToken);

    public static ProjectPendingScope Scope(ProjectDefinitionWriteContext context) => new(context.Namespace, context.CallerBinding);

    private static void RequireScope(ProjectPendingScope original, ProjectDefinitionWriteContext current) {
        if (original != Scope(current)) {
            throw new InvalidOperationException("The dataset or caller changed after this draft was opened. Reopen the current project explicitly; this draft cannot be rebound.");
        }
    }

    private sealed class NativeReceiptAccess(ProjectDefinitionReader reader) : IProjectDefinitionReceiptAccess {
        public async Task<bool> CanReadAsync(ProjectWriteAdmission target, CancellationToken cancellationToken) {
            var current = await reader.ReadAsync(target.ProjectId, cancellationToken);
            return current.IsSuccess && current.Value!.Expectation.Project == target;
        }
    }
}
