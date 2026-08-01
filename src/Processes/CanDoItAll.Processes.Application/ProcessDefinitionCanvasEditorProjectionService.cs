using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed partial class ProcessDefinitionCanvasEditorProjectionService
{
    private const double StepWidth = 256;
    private const double StepHeight = 200;
    private const double BranchWidth = 276;
    private const double BranchHeight = 188;
    private const double RoleWidth = 268;
    private const double RoleHeight = 184;
    private const double ArtifactWidth = 240;
    private const double ArtifactHeight = 180;
    private const double SubprocessWidth = 288;
    private const double SubprocessHeight = 200;
    private const double Margin = 48;
    private const string VersionConflictSummary = "Canvas command was rejected because the definition canvas projection changed before submission.";

    private readonly ProcessTemplatePackLoader templatePackLoader;
    private readonly IProcessProjectionClock clock;
    private readonly object stateGate = new();
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

        ProcessDefinitionCanvasEditorProjection projection;
        lock (stateGate)
        {
            var stateKey = ProcessDefinitionCanvasStateKey.From(scope, definitionKey);
            if (snapshots.TryGetValue(stateKey, out var snapshot))
            {
                projection = CreateProjection(snapshot, lastReceipt: null);
            }
            else
            {
                var created = CreateTemplateSnapshot(scope, FindTemplateDefinition(definitionKey));
                snapshots[stateKey] = created;
                projection = CreateProjection(created, lastReceipt: null);
            }
        }

        return Task.FromResult(projection);
    }

    public Task<ProcessDefinitionCanvasCommandResult> ExecuteCommandAsync(
        ProcessDefinitionCanvasCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Scope);
        ValidateScope(command.Scope);

        ProcessDefinitionCanvasCommandResult result;
        lock (stateGate)
        {
            var stateKey = ProcessDefinitionCanvasStateKey.From(command.Scope, command.DefinitionKey);
            var baseline = snapshots.TryGetValue(stateKey, out var existing)
                ? existing
                : CreateTemplateSnapshot(command.Scope, FindTemplateDefinition(command.DefinitionKey));
            snapshots.TryAdd(stateKey, baseline);
            var observedAtUtc = clock.GetUtcNow();

            if (command.ExpectedVersionToken is not { } expected)
            {
                result = CreateRejectedResult(
                    baseline,
                    command.CommandKind,
                    observedAtUtc,
                    "Canvas command was rejected because the expected definition canvas version is required.");
                return Task.FromResult(result);
            }

            if (expected != baseline.VersionToken)
            {
                result = CreateRejectedResult(
                    baseline,
                    command.CommandKind,
                    observedAtUtc,
                    VersionConflictSummary);
                return Task.FromResult(result);
            }

            result = command.CommandKind switch
            {
                ProcessDefinitionCanvasCommandKind.MoveNodes => ExecuteMoveNodes(stateKey, baseline, command, observedAtUtc),
                ProcessDefinitionCanvasCommandKind.AddStep => ExecuteAddStep(stateKey, baseline, command, observedAtUtc),
                ProcessDefinitionCanvasCommandKind.AddBranchRouter => ExecuteAddBranchRouter(stateKey, baseline, command, observedAtUtc),
                ProcessDefinitionCanvasCommandKind.AddRoleBinding => ExecuteAddRoleBinding(stateKey, baseline, command, observedAtUtc),
                ProcessDefinitionCanvasCommandKind.AddArtifactExpectation => ExecuteAddArtifactExpectation(stateKey, baseline, command, observedAtUtc),
                ProcessDefinitionCanvasCommandKind.AddSubprocessBoundary => ExecuteAddSubprocessBoundary(stateKey, baseline, command, observedAtUtc),
                ProcessDefinitionCanvasCommandKind.CloneArtifactReference => ExecuteCloneArtifactReference(stateKey, baseline, command, observedAtUtc),
                ProcessDefinitionCanvasCommandKind.CloneRoleReference => ExecuteCloneRoleReference(stateKey, baseline, command, observedAtUtc),
                ProcessDefinitionCanvasCommandKind.Recompose => ExecuteRecompose(stateKey, baseline, command, observedAtUtc),
                _ => throw new ArgumentOutOfRangeException(nameof(command), command.CommandKind, "Unknown definition canvas command.")
            };
        }

        return Task.FromResult(result);
    }
}
