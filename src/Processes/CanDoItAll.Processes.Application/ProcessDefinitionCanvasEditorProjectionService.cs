using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed partial class ProcessDefinitionCanvasEditorProjectionService
{
    private const double StepWidth = 220;
    private const double StepHeight = 92;
    private const double BranchWidth = 168;
    private const double BranchHeight = 76;
    private const double RoleWidth = 196;
    private const double RoleHeight = 76;
    private const double ArtifactWidth = 196;
    private const double ArtifactHeight = 72;
    private const double SubprocessWidth = 218;
    private const double SubprocessHeight = 78;
    private const double Margin = 48;
    private const string VersionConflictSummary = "Canvas command was rejected because the definition canvas projection changed before submission.";

    private readonly ProcessTemplatePackLoader templatePackLoader;
    private readonly IProcessProjectionClock clock;
    private readonly Dictionary<ProcessDefinitionCanvasStateKey, ProcessDefinitionCanvasSnapshot> snapshots = [];

    public ProcessDefinitionCanvasEditorProjectionService(IProcessProjectionClock clock)
        : this(new ProcessTemplatePackLoader(), clock)
    {
    }

    public ProcessDefinitionCanvasEditorProjectionService(
        ProcessTemplatePackLoader templatePackLoader,
        IProcessProjectionClock clock)
    {
        this.templatePackLoader = templatePackLoader ?? throw new ArgumentNullException(nameof(templatePackLoader));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<ProcessDefinitionCanvasEditorProjection> GetCanvasAsync(
        ProcessWorkspaceShellScope scope,
        ProcessDefinitionCatalogItemKey definitionKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateScope(scope);

        var stateKey = ProcessDefinitionCanvasStateKey.From(scope, definitionKey);
        if (snapshots.TryGetValue(stateKey, out var snapshot))
        {
            return Task.FromResult(CreateProjection(snapshot, lastReceipt: null));
        }

        var template = FindTemplateDefinition(definitionKey);
        return Task.FromResult(CreateProjection(CreateTemplateSnapshot(scope, template), lastReceipt: null));
    }

    public Task<ProcessDefinitionCanvasCommandResult> ExecuteCommandAsync(
        ProcessDefinitionCanvasCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Scope);
        ValidateScope(command.Scope);

        var stateKey = ProcessDefinitionCanvasStateKey.From(command.Scope, command.DefinitionKey);
        var baseline = snapshots.TryGetValue(stateKey, out var existing)
            ? existing
            : CreateTemplateSnapshot(command.Scope, FindTemplateDefinition(command.DefinitionKey));
        var observedAtUtc = clock.GetUtcNow();

        if (RequiresCurrentVersion(command.CommandKind) &&
            command.ExpectedVersionToken is { } expected &&
            expected != baseline.VersionToken)
        {
            return Task.FromResult(CreateRejectedResult(
                baseline,
                command.CommandKind,
                observedAtUtc,
                VersionConflictSummary));
        }

        var result = command.CommandKind switch
        {
            ProcessDefinitionCanvasCommandKind.AddStep => ExecuteAddStep(stateKey, baseline, command, observedAtUtc),
            ProcessDefinitionCanvasCommandKind.AddBranchRouter => ExecuteAddBranchRouter(stateKey, baseline, command, observedAtUtc),
            ProcessDefinitionCanvasCommandKind.AddRoleBinding => ExecuteAddRoleBinding(stateKey, baseline, command, observedAtUtc),
            ProcessDefinitionCanvasCommandKind.AddArtifactExpectation => ExecuteAddArtifactExpectation(stateKey, baseline, command, observedAtUtc),
            ProcessDefinitionCanvasCommandKind.AddSubprocessBoundary => ExecuteAddSubprocessBoundary(stateKey, baseline, command, observedAtUtc),
            ProcessDefinitionCanvasCommandKind.CloneArtifactReference => ExecuteCloneArtifactReference(stateKey, baseline, command, observedAtUtc),
            ProcessDefinitionCanvasCommandKind.Recompose => ExecuteRecompose(stateKey, baseline, command, observedAtUtc),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command.CommandKind, "Unknown definition canvas command.")
        };

        return Task.FromResult(result);
    }

    private static bool RequiresCurrentVersion(ProcessDefinitionCanvasCommandKind commandKind)
        => commandKind != ProcessDefinitionCanvasCommandKind.Recompose;

}
