using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class RuntimeBuildResult(
    AIAgent agent,
    ProviderProfile provider,
    string model,
    IReadOnlyList<IAsyncDisposable> asyncDisposables,
    IReadOnlyList<IDisposable> disposables,
    bool hasApprovalTools,
    bool isTemperatureOmitted,
    FinalizerCapture? finalizerCapture,
    ToolInvocationTraceRecorder? toolInvocationTraceRecorder,
    AgentContextContributionTraceCollector? contextContributionTraceCollector,
    Func<IReadOnlyList<AgentFinalizerInvocation>>? snapshotFinalizerInvocations = null,
    Func<IReadOnlyList<AgentToolInvocationTrace>>? snapshotToolInvocationTraces = null,
    Func<IReadOnlyList<AgentContextContributionTrace>>? snapshotContextContributionTraces = null,
    RuntimeCapabilityState? runtimeCapabilityState = null) : IAsyncDisposable
{
    public AIAgent Agent { get; } = agent;

    public ProviderProfile Provider { get; } = provider;

    public string Model { get; } = model;

    public bool HasApprovalTools { get; } = hasApprovalTools;

    public bool IsTemperatureOmitted { get; } = isTemperatureOmitted;

    public RuntimeCapabilityState? CapabilityState { get; } = runtimeCapabilityState;

    public IReadOnlyList<AITool> FinalizerTools { get; } = finalizerCapture?.Tools ?? [];

    public ToolInvocationTraceRecorder? ToolInvocationTraceRecorder { get; } = toolInvocationTraceRecorder;

    public IReadOnlyList<AgentFinalizerInvocation> SnapshotFinalizerInvocations()
        => snapshotFinalizerInvocations?.Invoke() ?? finalizerCapture?.Snapshot() ?? [];

    public IReadOnlyList<AgentToolInvocationTrace> SnapshotToolInvocationTraces()
        => snapshotToolInvocationTraces?.Invoke() ?? ToolInvocationTraceRecorder?.Snapshot() ?? [];

    public IReadOnlyList<AgentContextContributionTrace> SnapshotContextContributionTraces()
        => snapshotContextContributionTraces?.Invoke() ?? contextContributionTraceCollector?.Snapshot() ?? [];

    public async ValueTask DisposeAsync()
    {
        foreach (var disposable in asyncDisposables)
        {
            await disposable.DisposeAsync();
        }

        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }

        if (Agent is IAsyncDisposable asyncDisposableAgent)
        {
            await asyncDisposableAgent.DisposeAsync();
        }
        else if (Agent is IDisposable disposableAgent)
        {
            disposableAgent.Dispose();
        }
    }
}

internal sealed class HostedRuntimeAgent(RuntimeBuildResult runtimeBuild)
    : DelegatingAIAgent(runtimeBuild.Agent), IAsyncDisposable, IDisposable
{
    public ValueTask DisposeAsync()
    {
        return runtimeBuild.DisposeAsync();
    }

    public void Dispose()
    {
        _ = DisposeAsync();
    }
}

internal sealed record ScriptContentInspection(string Content, string FailureMessage)
{
    public static ScriptContentInspection Empty { get; } = new(string.Empty, string.Empty);
}

internal sealed class ToolInvocationTraceRecorder
{
    private readonly object gate = new();
    private readonly List<AgentToolInvocationTrace> traces = [];
    private int nextSequence;

    public int Start(
        string toolName,
        ToolInvocationClassification classification,
        string signature,
        AgentRuntimeToolOwnership? runtimeToolOwnership)
    {
        lock (gate)
        {
            nextSequence++;
            traces.Add(new AgentToolInvocationTrace(
                toolName,
                classification,
                nextSequence,
                DateTimeOffset.UtcNow,
                CompletedAtUtc: null,
                Succeeded: false,
                FailureMessage: string.Empty)
            {
                RuntimeToolProviderKey = runtimeToolOwnership?.ProviderKey ?? string.Empty,
                RuntimeToolProviderName = runtimeToolOwnership?.ProviderName ?? string.Empty,
                Signature = signature
            });
            return nextSequence;
        }
    }

    public void Complete(
        int sequence,
        bool succeeded,
        string failureMessage)
    {
        lock (gate)
        {
            var index = traces.FindIndex(trace => trace.Sequence == sequence);
            if (index < 0)
            {
                return;
            }

            traces[index] = traces[index] with
            {
                CompletedAtUtc = DateTimeOffset.UtcNow,
                Succeeded = succeeded,
                FailureMessage = succeeded ? string.Empty : failureMessage
            };
        }
    }

    public IReadOnlyList<AgentToolInvocationTrace> Snapshot()
    {
        lock (gate)
        {
            return traces.ToList();
        }
    }
}

internal sealed class FinalizerCapture(AgentFinalizerPolicy policy)
{
    private readonly object gate = new();
    private readonly List<AgentFinalizerInvocation> invocations = [];
    private int nextSequence;

    public AgentFinalizerPolicy Policy { get; } = policy;

    public List<AITool> Tools { get; } = [];

    public string SubmitProcessStepOutcome(JsonElement result)
        => CaptureJsonElement<ProcessStepOutcomeResult>(result, "Process step outcome finalizer captured.");

    public string SubmitCodeReviewResult(CodeReviewResult result)
        => Capture(result, "Code review result finalizer captured.");

    public string SubmitArchitectureReviewResult(ArchitectureReviewResult result)
        => Capture(result, "Architecture review result finalizer captured.");

    public string SubmitImplementationPlan(ImplementationPlanResult result)
        => Capture(result, "Implementation plan finalizer captured.");

    public string SubmitTestPlan(TestPlanResult result)
        => Capture(result, "Test plan finalizer captured.");

    public string SubmitToolExecutionDecision(ToolExecutionDecisionResult result)
        => Capture(result, "Tool execution decision finalizer captured.");

    public string SubmitProcessStatePatch(ProcessStatePatch result)
        => Capture(result, "Process state patch finalizer captured.");

    public string SubmitHumanEscalationRequest(HumanEscalationRequest result)
        => Capture(result, "Human escalation request finalizer captured.");

    public IReadOnlyList<AgentFinalizerInvocation> Snapshot()
    {
        lock (gate)
        {
            return invocations.ToList();
        }
    }

    private string Capture<TOutput>(TOutput result, string message)
    {
        ArgumentNullException.ThrowIfNull(result);

        var argumentsJson = JsonSerializer.Serialize(result, AgentOutputJson.SerializerOptions);
        return CaptureArgumentsJson(argumentsJson, message);
    }

    private string CaptureJsonElement<TOutput>(JsonElement result, string message)
    {
        var rawJson = result.ValueKind == JsonValueKind.String
            ? result.GetString()
            : result.GetRawText();
        if (!MafFinalizerDriver.TryNormalizeFinalizerJsonRepairText(Policy, rawJson, out var argumentsJson, out var failureMessage))
        {
            throw new InvalidOperationException($"Finalizer payload for '{Policy.ToolName}' is invalid: {failureMessage}");
        }

        return CaptureArgumentsJson(argumentsJson, message);
    }

    private string CaptureArgumentsJson(string argumentsJson, string message)
    {
        var candidate = new AgentFinalizerInvocation(
            Policy.ToolName,
            argumentsJson,
            Sequence: 0);
        var validation = new DefaultAgentFinalizerValidator().Validate(Policy, [candidate]);
        if (!validation.Succeeded || validation.Output is null)
        {
            var errorSummary = string.Join(
                "; ",
                validation.Errors.Select(error => $"{error.Code}: {error.Message}"));
            throw new InvalidOperationException(
                $"Finalizer payload for '{Policy.ToolName}' failed validation: {errorSummary}");
        }

        lock (gate)
        {
            if (invocations.Count > 0)
            {
                return $"Finalizer '{Policy.ToolName}' already captured; duplicate submission ignored.";
            }

            nextSequence++;
            invocations.Add(new AgentFinalizerInvocation(
                Policy.ToolName,
                argumentsJson,
                nextSequence));
        }

        return message;
    }
}
