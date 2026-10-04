using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.AgentFramework.WorkflowAuthoring.UiSandbox.Scenarios;

public enum WorkflowScenarioMode { Ready, Held, Rejected, Unknown, Unavailable, LargeGraph, LongInput }

public sealed class WorkflowScenario : IDisposable {
    private readonly CancellationTokenSource lifetime = new();
    private readonly List<TaskCompletionSource> held = [];
    public WorkflowScenarioMode Mode { get; set; }
    public CancellationToken OwnerToken => lifetime.Token;
    public WorkflowDefinition Definition { get; }
    public WorkflowDefinition? Accepted { get; private set; }
    public WorkflowRunSnapshot? Run { get; private set; }
    public int SaveCount { get; private set; }
    public int RunCount { get; private set; }
    public int BindingCount { get; private set; }
    public int PendingCount => held.Count;
    public static WorkflowProviderOption Provider { get; } = new(Guid.NewGuid(), "Deterministic source",
        ProviderKind.OpenAi, ProviderTransportKind.Responses, ProviderProfilePurpose.Chat, "Route", ["Route", "route"],
        true, true, true, true, true, false) {
        IsSourceManaged = true, ModelCatalog = [new("Route", "Native default"), new("route", "Native custom model")]
    };
    public static IReadOnlyList<WorkflowRuntimeBackendDescriptor> Backends { get; } = [
        new(WorkflowRuntimeBackendKind.InProcess, "In-process", false, true, true, true, "Fixture preview only.")
    ];
    public static WorkflowExecutorDescriptor ImageExecutor { get; } = ReadImageDescriptor();
    private static WorkflowExecutorDescriptor ReadImageDescriptor() {
        using var stream = typeof(WorkflowScenario).Assembly.GetManifestResourceStream(
            "CanDoItAll.AgentFramework.WorkflowAuthoring.UiSandbox.Fixtures.image-executor.json")
            ?? throw new InvalidOperationException("The native image executor fixture is missing.");
        return JsonSerializer.Deserialize<WorkflowExecutorDescriptor>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("The native image executor fixture is empty.");
    }
    public IReadOnlyList<WorkflowExecutorDescriptor> Executors => Mode == WorkflowScenarioMode.Unavailable
        ? [ImageExecutor with { Availability = WorkflowExecutorAvailabilityDescriptor.Planned("Fixture dependency unavailable.") }]
        : [ImageExecutor];
    public static LlmCallComponent Component { get; } = new(WorkflowComponentId.New(), "Pinned summary prompt",
        Provider.ProviderProfileId, Provider.DefaultModel, WorkflowModality.Text, new(.2, 800, false, string.Empty),
        "Summarize the input.", WorkflowValueShape.Text, WorkflowValueShape.Text, AgentPermissionsPolicy.Default,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    public WorkflowScenario(WorkflowScenarioMode mode) {
        Mode = mode;
        var document = WorkflowCanvasDefinitionMapper.CreateDraft([Component]);
        document.Name = "Workflow authoring scenario";
        document.Description = mode == WorkflowScenarioMode.LongInput ? new string('W', 1200) : "Independent rendering fixture. No native execution.";
        document.Edges.Clear();
        var previous = document.Nodes[0];
        var count = mode == WorkflowScenarioMode.LargeGraph ? 80 : 4;
        for (var i = 0; i < count; i++) {
            var kind = (i % 4) switch {
                0 => WorkflowNodeKind.LlmCall,
                1 => WorkflowNodeKind.StrictLogic,
                2 => WorkflowNodeKind.Executor,
                _ => WorkflowNodeKind.HumanInput
            };
            var node = WorkflowCanvasDefinitionMapper.CreateNode(kind, document.Nodes, [Component], 400 + i * 280, 180 + i % 3 * 130);
            if (kind == WorkflowNodeKind.Executor) {
                WorkflowCanvasDefinitionMapper.ApplyExecutor(node, ImageExecutor);
            }
            if (kind == WorkflowNodeKind.LlmCall) {
                WorkflowCanvasDefinitionMapper.ApplyComponent(node, Component);
            }
            document.Nodes.Insert(document.Nodes.Count - 1, node);
            document.Edges.Add(new(new($"route-{i}"), previous.Id, node.Id));
            previous = node;
        }
        var end = document.Nodes[^1];
        end.CanvasX = 400 + count * 280;
        document.Edges.Add(new(new("route-end"), previous.Id, end.Id));
        Definition = WorkflowCanvasDefinitionMapper.ToDefinition(document);
    }

    public WorkflowDocumentOperations DocumentOperations => new(SaveAsync, async (_, _) => {
        await HoldAsync();
        return WorkflowValidationResult.Success;
    });
    public WorkflowPreviewOperations PreviewOperations => new(_ => new([], [
        new(new("executor"), "Generate image", WorkflowExecutorIds.ImageGeneration, "Fixture simulation", "{}")
    ]), _ => Mode == WorkflowScenarioMode.Unavailable
        ? Task.FromException<IReadOnlyList<WorkflowPreviewProject>>(new InvalidOperationException("Fixture unavailable"))
        : Task.FromResult<IReadOnlyList<WorkflowPreviewProject>>([]), PreviewAsync);

    private async Task<WorkflowDefinitionSaveOutcome> SaveAsync(WorkflowDefinitionSaveRequest request, CancellationToken owner) {
        SaveCount++;
        await HoldAsync();
        if (Mode == WorkflowScenarioMode.Unknown) {
            return new WorkflowDefinitionSaveOutcome.Unknown();
        }
        if (Mode == WorkflowScenarioMode.Rejected) {
            return new WorkflowDefinitionSaveOutcome.Rejected(WorkflowSaveRejection.Conflict);
        }
        Accepted = Definition with { Id = request.Id ?? WorkflowId.New(), VersionId = WorkflowVersionId.New(),
            Name = request.Name, Description = request.Description, Graph = request.Graph, Status = request.Status,
            RuntimePolicy = request.RuntimePolicy, InputParameters = request.InputParameters, UpdatedAtUtc = DateTimeOffset.UtcNow };
        return new WorkflowDefinitionSaveOutcome.Accepted(Accepted);
    }

    public async Task<WorkflowPromptBindingOutcome> BindAsync(WorkflowPromptBindingRequest request, CancellationToken owner) {
        BindingCount++;
        await HoldAsync();
        return Mode == WorkflowScenarioMode.Unknown ? new WorkflowPromptBindingOutcome.Unknown()
            : new WorkflowPromptBindingOutcome.Accepted(Component with { Id = WorkflowComponentId.New(),
                Name = request.Selection.Title, PromptArtifactId = request.Selection.ArtifactId, PromptVersionId = request.Selection.VersionId,
                ProviderProfileId = request.Provider.ProviderProfileId, Model = request.Model });
    }

    private async Task<WorkflowPreviewOutcome> PreviewAsync(WorkflowPreviewSubmission request, Func<WorkflowNodeId, Task> progress, CancellationToken owner) {
        RunCount++;
        await HoldAsync();
        if (Mode == WorkflowScenarioMode.Unknown) {
            return new WorkflowPreviewOutcome.Unknown(WorkflowRunId.New());
        }
        foreach (var node in request.Definition.Graph.Nodes) {
            await progress(node.Id);
        }
        Run = new(WorkflowRunId.New(), request.Definition.Id, request.Definition.VersionId, WorkflowRunState.Completed,
            WorkflowRuntimeBackendKind.InProcess, "sandbox", "Deterministic fixture complete.", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        return new WorkflowPreviewOutcome.Completed(WorkflowValidationResult.Success, Run, true, true, Run.Summary);
    }

    private async Task HoldAsync() {
        if (Mode != WorkflowScenarioMode.Held) {
            return;
        }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        held.Add(completion);
        await completion.Task;
    }
    public void Release() {
        foreach (var completion in held) {
            completion.TrySetResult();
        }
        held.Clear();
    }
    public void Dispose() {
        lifetime.Cancel();
        Release();
        lifetime.Dispose();
    }
}
