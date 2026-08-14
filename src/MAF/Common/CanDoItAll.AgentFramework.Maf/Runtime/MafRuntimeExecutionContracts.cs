using System.Reflection;
using System.Text.Json;
using System.Runtime.ExceptionServices;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
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
    RuntimeCapabilityState? runtimeCapabilityState = null,
    Func<AgentResponseUpdate, bool>? isTerminalResponseUpdate = null,
    ProviderRequestCompatibilityEvidence? entryAgentRequestCompatibilityEvidence = null) : IAsyncDisposable
{
    private const string DisposalFailureTypesDataKey =
        "CanDoItAll.RuntimeBuildDisposalFailureTypes";
    private static readonly TimeSpan DisposalTimeout = TimeSpan.FromSeconds(5);
    private readonly object disposalLock = new();
    private Task<IReadOnlyList<Exception>>? disposalTask;

    public AIAgent Agent { get; } = agent;

    public ProviderProfile Provider { get; } = provider;

    public string Model { get; } = model;

    public bool HasApprovalTools { get; } = hasApprovalTools;

    public bool IsTemperatureOmitted { get; } = isTemperatureOmitted;

    public RuntimeCapabilityState? CapabilityState { get; } = runtimeCapabilityState;

    public Func<AgentResponseUpdate, bool>? IsTerminalResponseUpdate { get; } = isTerminalResponseUpdate;

    public ProviderRequestCompatibilityEvidence? EntryAgentRequestCompatibilityEvidence { get; } = entryAgentRequestCompatibilityEvidence;

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
        await CompleteDisposalAsync(primaryFailure: null).ConfigureAwait(false);
    }

    public async Task<TResult> ExecuteWithLifetimeAsync<TResult>(Func<Task<TResult>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Exception? primaryFailure = null;
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
            throw;
        }
        finally
        {
            await CompleteDisposalAsync(primaryFailure).ConfigureAwait(false);
        }
    }

    internal async ValueTask DisposeAsync(Exception? primaryFailure)
    {
        await CompleteDisposalAsync(primaryFailure).ConfigureAwait(false);
    }

    private async ValueTask CompleteDisposalAsync(Exception? primaryFailure)
    {
        var failures = await GetOrCreateDisposalTask().ConfigureAwait(false);
        if (failures.Count == 0)
        {
            return;
        }

        if (primaryFailure is not null)
        {
            var existingFailureTypes = primaryFailure.Data[DisposalFailureTypesDataKey] as string;
            primaryFailure.Data[DisposalFailureTypesDataKey] = string.Join(
                ",",
                (existingFailureTypes ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Concat(failures.Select(ResolveDiagnosticFailureType))
                    .Distinct(StringComparer.Ordinal)
                    .Take(16));
            return;
        }

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        throw new AggregateException("Runtime build cleanup failed.", failures);
    }

    private Task<IReadOnlyList<Exception>> GetOrCreateDisposalTask()
    {
        lock (disposalLock)
        {
            disposalTask ??= DisposeCoreAsync();
            return disposalTask;
        }
    }

    private async Task<IReadOnlyList<Exception>> DisposeCoreAsync()
    {
        var failures = new List<Exception>();

        if (Agent is IAsyncDisposable asyncDisposableAgent)
        {
            await CaptureAsyncDisposalFailureAsync(asyncDisposableAgent, failures).ConfigureAwait(false);
        }
        else if (Agent is IDisposable disposableAgent)
        {
            CaptureDisposalFailure(disposableAgent, failures);
        }

        foreach (var disposable in asyncDisposables.Reverse())
        {
            await CaptureAsyncDisposalFailureAsync(disposable, failures).ConfigureAwait(false);
        }

        foreach (var disposable in disposables.Reverse())
        {
            CaptureDisposalFailure(disposable, failures);
        }

        return failures;
    }

    private static async ValueTask CaptureAsyncDisposalFailureAsync(
        IAsyncDisposable disposable,
        ICollection<Exception> failures)
    {
        Task? disposal = null;
        try
        {
            disposal = disposable.DisposeAsync().AsTask();
            await disposal.WaitAsync(DisposalTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException exception) when (disposal is { IsCompleted: false })
        {
            ObserveLateDisposal(disposal);
            failures.Add(exception);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private static void CaptureDisposalFailure(
        IDisposable disposable,
        ICollection<Exception> failures)
    {
        try
        {
            disposable.Dispose();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private static void ObserveLateDisposal(Task disposal)
    {
        _ = disposal.ContinueWith(
            static completedDisposal =>
            {
                _ = completedDisposal.Exception;
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static string ResolveDiagnosticFailureType(Exception exception)
        => exception.GetType().FullName ?? exception.GetType().Name;
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
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}

internal sealed record ScriptContentInspection(string Content, string FailureMessage)
{
    public static ScriptContentInspection Empty { get; } = new(string.Empty, string.Empty);
}

internal sealed class ToolInvocationTraceRecorder
{
    internal const string UnexposedFailureMessage = "Tool invocation failed.";

    private readonly object gate = new();
    private readonly List<AgentToolInvocationTrace> traces = [];
    private int nextSequence;

    public int Start(
        string toolName,
        ToolInvocationClassification classification,
        string signature,
        AgentRuntimeToolOwnership? runtimeToolOwnership,
        ToolInvocationPathArgumentSet pathArguments)
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
                Signature = signature,
                TargetPath = ResolveTargetPath(pathArguments)
            });
            return nextSequence;
        }
    }

    private static string ResolveTargetPath(ToolInvocationPathArgumentSet pathArguments)
    {
        var targetPaths = pathArguments.Values
            .Where(argument => argument.ElementIndex is null)
            .Where(argument =>
                string.Equals(argument.Name, "path", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument.Name, "relativePath", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument.Name, "filePath", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument.Name, "targetPath", StringComparison.OrdinalIgnoreCase))
            .Select(argument => argument.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .Take(2)
            .ToArray();
        return targetPaths.Length == 1
            ? targetPaths[0]
            : string.Empty;
    }

    public void Complete(
        int sequence,
        bool succeeded,
        string failureMessage,
        bool failureMessageSafeForPersistence,
        Guid? directReceiptExecutionRunId = null)
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
                FailureMessage = succeeded
                    ? string.Empty
                    : failureMessageSafeForPersistence
                        ? failureMessage
                        : UnexposedFailureMessage,
                DirectReceiptExecutionRunId = directReceiptExecutionRunId
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

internal sealed record FinalizerSubmissionResult(bool Succeeded, string Message);

internal sealed class ExactFinalizerArgumentsAIFunction(
    AIFunction innerFunction,
    AgentFinalizerPolicy policy) : DelegatingAIFunction(innerFunction)
{
    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count == 1 &&
            string.Equals(arguments.Keys.Single(), "result", StringComparison.Ordinal))
        {
            return base.InvokeCoreAsync(arguments, cancellationToken);
        }

        var suppliedArguments = arguments.Count == 0
            ? "none"
            : string.Join(", ", arguments.Keys
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => $"`{name}`"));
        return ValueTask.FromResult<object?>(new FinalizerSubmissionResult(
            false,
            $"Finalizer payload for '{policy.ToolName}' was rejected before binding: expected exactly one argument named `result`; received {suppliedArguments}. Move every structured output field inside `result` and call '{policy.ToolName}' again."));
    }
}

internal sealed class FinalizerCapture(AgentFinalizerPolicy policy)
{
    private static readonly MethodInfo SubmitMethodDefinition =
        typeof(FinalizerCapture).GetMethod(nameof(Submit), BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException($"{nameof(FinalizerCapture)}.{nameof(Submit)}<T> was not found.");

    private readonly object gate = new();
    private readonly List<AgentFinalizerInvocation> invocations = [];
    private int nextSequence;

    public AgentFinalizerPolicy Policy { get; } = policy;

    public List<AITool> Tools { get; } = [];

    /// <summary>
    /// Builds the delegate the generic finalizer tool factory binds for this policy's contract. Contracts that
    /// declare a <see cref="AgentFinalizerPolicy.KnownOutputNormalizer"/> capture a raw <see cref="JsonElement"/>
    /// while the factory projects the policy's typed output schema; every other contract captures its own
    /// strictly typed output. Either way the closed generic method is reflected by
    /// <c>AIFunctionFactory.Create</c>, and the provider sees the governed CLR contract shape.
    /// </summary>
    internal Delegate CreateSubmitDelegate()
    {
        var parameterType = Policy.KnownOutputNormalizer is not null
            ? typeof(JsonElement)
            : Policy.OutputType;
        var closedMethod = SubmitMethodDefinition.MakeGenericMethod(parameterType);
        var delegateType = typeof(Func<,>).MakeGenericType(parameterType, typeof(FinalizerSubmissionResult));
        return closedMethod.CreateDelegate(delegateType, this);
    }

    public IReadOnlyList<AgentFinalizerInvocation> Snapshot()
    {
        lock (gate)
        {
            return invocations.ToList();
        }
    }

    /// <summary>The single generic capture entry point every contract's finalizer tool binds to (internal so tests
    /// can invoke it directly with the same generic-argument inference the reflected delegate uses).
    /// <c>where T : notnull</c> is required for schema fidelity, not just correctness: an unconstrained
    /// generic parameter reports "nullable-oblivious" through <c>NullabilityInfoContext</c> even once closed over
    /// a concrete reference type, which would add a spurious "null" branch to the generated JSON schema for every
    /// strictly typed (non-tolerant) contract and silently change the finalizer tool's advertised argument shape.
    /// </summary>
    internal FinalizerSubmissionResult Submit<T>(T result) where T : notnull
    {
        return typeof(T) == typeof(JsonElement)
            ? CaptureJsonElement((JsonElement)(object)result!, Policy.CaptureConfirmationMessage)
            : Capture(result, Policy.CaptureConfirmationMessage);
    }

    private FinalizerSubmissionResult Capture<TOutput>(TOutput result, string message)
    {
        ArgumentNullException.ThrowIfNull(result);

        var argumentsJson = JsonSerializer.Serialize(result, AgentOutputJson.SerializerOptions);
        return CaptureArgumentsJson(argumentsJson, message);
    }

    private FinalizerSubmissionResult CaptureJsonElement(JsonElement result, string message)
    {
        var rawJson = result.ValueKind == JsonValueKind.String
            ? result.GetString()
            : result.GetRawText();
        if (!MafFinalizerDriver.TryNormalizeFinalizerJsonRepairText(Policy, rawJson, out var argumentsJson, out var failureMessage))
        {
            return new FinalizerSubmissionResult(
                false,
                $"Finalizer payload for '{Policy.ToolName}' was rejected: {failureMessage} Correct the payload and call '{Policy.ToolName}' again.");
        }

        return CaptureArgumentsJson(argumentsJson, message);
    }

    private FinalizerSubmissionResult CaptureArgumentsJson(string argumentsJson, string message)
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
            return new FinalizerSubmissionResult(
                false,
                $"Finalizer payload for '{Policy.ToolName}' was rejected: {errorSummary} Correct the payload and call '{Policy.ToolName}' again.");
        }

        lock (gate)
        {
            if (invocations.Count > 0)
            {
                return new FinalizerSubmissionResult(
                    true,
                    $"Finalizer '{Policy.ToolName}' already captured; duplicate submission ignored.");
            }

            nextSequence++;
            invocations.Add(new AgentFinalizerInvocation(
                Policy.ToolName,
                argumentsJson,
                nextSequence));
        }

        return new FinalizerSubmissionResult(true, message);
    }
}
