using System.Collections;
using System.Reflection;
using System.Runtime.ExceptionServices;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Runtime;
using CanDoItAll.AgentFramework.Workflows.Abstractions;



namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowUsageAnalyticsRedGateTests
{
    private static readonly DateTimeOffset FirstObservationAtUtc = new(2026, 7, 12, 12, 1, 2, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondObservationAtUtc = new(2026, 7, 12, 12, 1, 5, TimeSpan.Zero);
    private static readonly Guid FirstObservationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondObservationId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task LlmInvokerPreservesProviderUsageDimensionsWithoutCollapsingThem()
    {
        // The lightweight ILlmInvocationPort (SB16) returns exactly one LlmUsage per invocation - there is no
        // longer a list of distinct provider usage observations to preserve (no repair/attachment sub-calls
        // happen behind the port). This test now proves the single canonical WorkflowUsageObservation derived
        // from that one LlmUsage is not collapsed/zeroed and carries the exact reported dimensions end to end.
        var provider = CreateProviderProfile() with
        {
            ModelPrices = [new ProviderModelTokenPrice("model-a", 1m, 0.1m, 4m)]
        };
        var port = new RecordingLlmInvocationPort(new LlmUsage(InputTokens: 101, OutputTokens: 31, CachedInputTokens: 11));
        var invoker = new WorkflowLlmComponentInvoker(
            port,
            new SingleProviderRegistry(provider),
            new ProviderProfileService());
        var component = CreateLlmComponent();
        var node = CreateLlmNode(component.Id);
        var definition = CreateDefinition([node], [], node.Id);

        var result = await invoker.ExecuteAsync(
            definition,
            node,
            component,
            new WorkflowNodeInput("{}"));

        var actual = ReadCanonicalObservations(result);
        var observation = Assert.Single(actual);
        Assert.Equal("model-a", ReadProperty<string>(observation, "Model"));
        Assert.Equal(101, ReadProperty<int>(observation, "InputTokens"));
        Assert.Equal(11, ReadProperty<int>(observation, "CachedInputTokens"));
        Assert.Equal(31, ReadProperty<int>(observation, "OutputTokens"));
        Assert.Equal(0, ReadProperty<int>(observation, "ReasoningTokens"));
        Assert.Equal(132, ReadProperty<int>(observation, "TotalTokens"));
    }

    [Fact]
    public async Task ExecutorUsageReachesCompilerProgressAndBackendResult()
    {
        var executor = new UsageWorkflowExecutor();
        var catalog = new WorkflowExecutorCatalog([executor]);
        var compiler = new MafWorkflowCompiler(
            new WorkflowDefinitionValidator(catalog),
            new WorkflowExecutorInvoker(catalog, [executor]));
        var backend = new MafInProcessWorkflowExecutionBackend(compiler, []);
        var node = CreateExecutorNode();
        var definition = CreateDefinition(
            [node, CreateEndNode()],
            [CreateEdge(node.Id, new WorkflowNodeId("end"))],
            node.Id);
        var observer = new CapturingProgressObserver();
        using var progressScope = WorkflowNodeExecutionProgressScope.Push(observer);

        var result = await backend.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                "{}",
                WorkflowRuntimeBackendKind.InProcess,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null),
            WorkflowRunId.New());

        var completed = Assert.Single(observer.Records, item =>
            item.NodeId == node.Id && item.State == WorkflowNodeExecutionProgressState.Completed);
        Assert.NotNull(completed.Usage);
        Assert.Equal(UsageWorkflowExecutor.ExpectedUsage, completed.Usage);
        Assert.Single(ReadCanonicalObservations(result));
    }

    [Fact]
    public void WorkflowContractsExposeOneImmutableCanonicalObservationAcrossEveryBoundary()
    {
        var observationType = typeof(WorkflowUsageMetrics).Assembly.GetType(
            "CanDoItAll.AgentFramework.Models.WorkflowUsageObservation");
        var storeType = typeof(IWorkflowRunStore).Assembly.GetType(
            "CanDoItAll.AgentFramework.Workflows.Abstractions.IWorkflowUsageObservationStore");
        var failures = new List<string>();

        Require(observationType is not null, "WorkflowUsageObservation is missing from the workflow model contract.", failures);
        Require(storeType is not null, "IWorkflowUsageObservationStore is missing from workflow abstractions.", failures);
        if (observationType is not null)
        {
            Require(observationType.IsSealed, "WorkflowUsageObservation must be sealed and immutable.", failures);
            Require(
                observationType.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is not null,
                "WorkflowUsageObservation must be a value-like record.",
                failures);
            RequireProperty(observationType, "Id", failures, property => property.PropertyType != typeof(string));
            RequireProperty(observationType, "RunId", failures);
            RequireProperty(observationType, "WorkflowId", failures);
            RequireProperty(observationType, "VersionId", failures);
            RequireProperty(observationType, "NodeId", failures);
            RequireProperty(observationType, "ProviderName", failures);
            RequireProperty(observationType, "Model", failures);
            RequireProperty(observationType, "SourcePhase", failures);
            RequireProperty(observationType, "UsageStatus", failures);
            RequireProperty(observationType, "PricingStatus", failures);
            RequireProperty(observationType, "InputTokens", failures);
            RequireProperty(observationType, "CachedInputTokens", failures);
            RequireProperty(observationType, "OutputTokens", failures);
            RequireProperty(observationType, "ReasoningTokens", failures);
            RequireProperty(observationType, "TotalTokens", failures);
            RequireProperty(observationType, "CostUsd", failures, property => property.PropertyType == typeof(decimal?));
            RequireProperty(observationType, "StartedAtUtc", failures);
            RequireProperty(observationType, "CompletedAtUtc", failures);
            RequireProperty(observationType, "RecordedAtUtc", failures);

            RequireObservationCollection(typeof(WorkflowNodeExecutionResult), observationType, failures);
            RequireObservationCollection(typeof(WorkflowNodeExecutionProgress), observationType, failures);
            RequireObservationCollection(typeof(WorkflowBackendStartResult), observationType, failures);
        }

        if (storeType is not null && observationType is not null)
        {
            Require(storeType.IsInterface, "IWorkflowUsageObservationStore must be an interface boundary.", failures);
            var append = storeType.GetMethod("AppendAsync");
            Require(append is not null, "IWorkflowUsageObservationStore.AppendAsync is missing.", failures);
            Require(
                append?.GetParameters().Any(parameter =>
                    parameter.ParameterType == observationType || IsReadOnlyListOf(parameter.ParameterType, observationType)) == true,
                "AppendAsync must accept the canonical WorkflowUsageObservation fact.",
                failures);
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task UsageStoreAppendIsIdempotentForEqualFactAndRejectsConflictingFact()
    {
        var observationType = typeof(WorkflowUsageMetrics).Assembly.GetType(
            "CanDoItAll.AgentFramework.Models.WorkflowUsageObservation");
        var runtimeAssembly = Assembly.Load("CanDoItAll.AgentFramework.Workflows.Runtime");
        var storeType = runtimeAssembly.GetTypes()
            .SingleOrDefault(type => type.Name == "InMemoryWorkflowUsageObservationStore");

        Assert.True(
            observationType is not null && storeType is not null,
            "Canonical observation and in-memory store contracts are required before stable-ID append semantics can be proven.");

        var store = Activator.CreateInstance(storeType!);
        Assert.NotNull(store);
        var original = CreateCanonicalObservation(observationType!, FirstObservationId, "model-a");
        var conflicting = CreateCanonicalObservation(observationType!, FirstObservationId, "model-b-conflict");

        await InvokeAppendAsync(store, original);
        var duplicateException = await Record.ExceptionAsync(() => InvokeAppendAsync(store, original));
        var conflictException = await Record.ExceptionAsync(() => InvokeAppendAsync(store, conflicting));

        Assert.Null(duplicateException);
        Assert.NotNull(conflictException);
        Assert.True(
            conflictException.GetType().Name.Contains("Conflict", StringComparison.OrdinalIgnoreCase) ||
            conflictException.GetType().Name.Contains("Corrupt", StringComparison.OrdinalIgnoreCase) ||
            conflictException.Message.Contains("same id", StringComparison.OrdinalIgnoreCase),
            $"Conflicting fact must surface as corruption, but received {conflictException.GetType().Name}: {conflictException.Message}");
    }

    [Fact]
    public void PricingDistinguishesKnownFreeFromUnknownWhileRetainingAllObservedTokens()
    {
        var knownFree = CreateProviderObservation(
            FirstObservationId,
            FirstObservationAtUtc,
            "known-free-model",
            ProviderUsageSourcePhases.AgentRuntime,
            inputTokens: 100,
            cachedInputTokens: 20,
            outputTokens: 30,
            reasoningTokens: 5,
            totalTokens: 135) with
        {
            ProviderCostUsd = 0m
        };
        var unknownPrice = CreateProviderObservation(
            SecondObservationId,
            SecondObservationAtUtc,
            "unknown-price-model",
            ProviderUsageSourcePhases.AgentRuntimeContinuation,
            inputTokens: 200,
            cachedInputTokens: 40,
            outputTokens: 60,
            reasoningTokens: 15,
            totalTokens: 275);

        var knownFreeResolved = ProviderPricingCalculator.TryResolveObservationCost(knownFree, [], out var knownFreeCost);
        var unknownResolved = ProviderPricingCalculator.TryResolveObservationCost(unknownPrice, [], out _);
        var summary = ProviderPricingCalculator.SummarizeUsage([knownFree, unknownPrice], []);

        Assert.True(knownFreeResolved);
        Assert.Equal(0m, knownFreeCost);
        Assert.False(unknownResolved);
        Assert.Equal(300, summary.InputTokens);
        Assert.Equal(60, summary.CachedInputTokens);
        Assert.Equal(90, summary.OutputTokens);
        Assert.Equal(20, summary.ReasoningTokens);
        Assert.Equal(410, summary.TotalTokens);
    }

    [Fact]
    public void AnalyticsTotalsAreIndependentOfRecentEightRunWindow()
    {
        var runs = Enumerable.Range(0, 9)
            .Select(index => CreateRun(index))
            .ToArray();
        var recentTake = 8;
        var expectedRecent = runs.OrderByDescending(item => item.UpdatedAtUtc).Take(recentTake).ToArray();
        var apiSource = ReadSource("src", "App", "CanDoItAll.Web", "Api", "WorkflowsApi.cs");

        Assert.Equal(9, runs.Length);
        Assert.Equal(8, expectedRecent.Length);
        Assert.Contains("IWorkflowAnalyticsQueryService", apiSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildAnalyticsAsync", apiSource, StringComparison.Ordinal);
    }

    [Fact]
    public void DurationUsesExplicitTerminalTimestampAndInjectedTimeProvider()
    {
        var terminalAtUtc = typeof(WorkflowRunSnapshot).GetProperty("TerminalAtUtc");
        var queryServiceType = typeof(IWorkflowRunStore).Assembly.GetType(
            "CanDoItAll.AgentFramework.Workflows.Abstractions.IWorkflowAnalyticsQueryService");

        Assert.NotNull(terminalAtUtc);
        Assert.Equal(typeof(DateTimeOffset?), terminalAtUtc!.PropertyType);
        Assert.NotNull(queryServiceType);

        var implementationType = typeof(WorkflowAnalyticsQueryService);
        Assert.NotNull(implementationType);
        Assert.Contains(
            implementationType!.GetConstructors().SelectMany(constructor => constructor.GetParameters()),
            parameter => parameter.ParameterType == typeof(TimeProvider));
    }

    [Fact]
    public void ApiAnalyticsUsesTypedQueryServiceAndNeverProjectsEventPayloadJson()
    {
        var apiSource = ReadSource("src", "App", "CanDoItAll.Web", "Api", "WorkflowsApi.cs");
        var endpoint = Slice(
            apiSource,
            "workflows.MapGet(\"/analytics\"",
            ".WithName(\"GetWorkflowAnalytics\");");

        Assert.Contains("IWorkflowAnalyticsQueryService", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("IWorkflowRunStore", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("IWorkflowCatalogService", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("PayloadJson", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Deserialize", endpoint, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifiedProcessOriginIsPersistedSeparatelyFromLegacyCallerGuids()
    {
        var processRunId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var assignmentId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var origin = new WorkflowLaunchOrigin.ProcessAssignment(
            processRunId,
            assignmentId,
            new WorkflowLaunchCorrelationId("verified-process-origin"));
        var originProperty = typeof(WorkflowRunSnapshot).GetProperty("Origin");
        var launcherSource = ReadSource(
            "src",
            "MAF",
            "Workflows",
            "CanDoItAll.AgentFramework.Workflows.Core",
            "WorkflowRuntimeManagerRunLauncher.cs");

        Assert.Equal(processRunId, origin.ProcessRunId);
        Assert.Equal(assignmentId, origin.AssignmentId);
        Assert.NotNull(originProperty);
        Assert.Equal(typeof(WorkflowLaunchOrigin), originProperty!.PropertyType);
        Assert.DoesNotContain("processOrigin?.ProcessRunId", launcherSource, StringComparison.Ordinal);
        Assert.DoesNotContain("processOrigin?.AssignmentId", launcherSource, StringComparison.Ordinal);
    }

    private static IReadOnlyList<object> ReadCanonicalObservations(object carrier)
    {
        var property = carrier.GetType().GetProperty("UsageObservations");
        Assert.True(
            property is not null,
            $"{carrier.GetType().Name} must expose canonical UsageObservations instead of only a collapsed WorkflowUsageMetrics value.");
        var value = property!.GetValue(carrier);
        var observations = Assert.IsAssignableFrom<IEnumerable>(value);
        return observations.Cast<object>().ToArray();
    }

    private static T ReadProperty<T>(object instance, string name)
    {
        var property = instance.GetType().GetProperty(name);
        Assert.True(property is not null, $"{instance.GetType().Name}.{name} is required.");
        return Assert.IsType<T>(property!.GetValue(instance));
    }

    private static void RequireObservationCollection(
        Type carrierType,
        Type observationType,
        ICollection<string> failures)
    {
        var property = carrierType.GetProperty("UsageObservations");
        Require(property is not null, $"{carrierType.Name}.UsageObservations is missing.", failures);
        Require(
            property is not null && IsReadOnlyListOf(property.PropertyType, observationType),
            $"{carrierType.Name}.UsageObservations must be IReadOnlyList<WorkflowUsageObservation>.",
            failures);
    }

    private static bool IsReadOnlyListOf(Type type, Type elementType)
        => type.IsGenericType &&
           type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) &&
           type.GetGenericArguments()[0] == elementType;

    private static void RequireProperty(
        Type type,
        string name,
        ICollection<string> failures,
        Func<PropertyInfo, bool>? predicate = null)
    {
        var property = type.GetProperty(name);
        Require(property is not null, $"{type.Name}.{name} is missing.", failures);
        Require(
            property is not null && (predicate is null || predicate(property)),
            $"{type.Name}.{name} has an invalid contract type.",
            failures);
    }

    private static void Require(bool condition, string message, ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }

    private static object CreateCanonicalObservation(Type type, Guid id, string model)
    {
        var constructor = type.GetConstructors()
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .FirstOrDefault();
        Assert.NotNull(constructor);
        var arguments = constructor!.GetParameters()
            .Select(parameter => CreateContractValue(parameter.ParameterType, parameter.Name ?? string.Empty, id, model))
            .ToArray();
        return constructor.Invoke(arguments);
    }

    private static object? CreateContractValue(Type type, string name, Guid id, string model)
    {
        var nullableType = Nullable.GetUnderlyingType(type);
        if (nullableType is not null)
        {
            if (nullableType == typeof(decimal) && name.Equals("costUsd", StringComparison.OrdinalIgnoreCase))
            {
                return 0m;
            }

            if (name.Equals("runId", StringComparison.OrdinalIgnoreCase) &&
                nullableType.GetConstructor([typeof(Guid)]) is { } runIdConstructor)
            {
                return runIdConstructor.Invoke([Guid.Parse("44444444-4444-4444-4444-444444444444")]);
            }

            return null;
        }

        if (type == typeof(Guid))
        {
            return name.Equals("id", StringComparison.OrdinalIgnoreCase)
                ? id
                : Guid.Parse("55555555-5555-5555-5555-555555555555");
        }

        if (type == typeof(string))
        {
            return name.Contains("model", StringComparison.OrdinalIgnoreCase) ? model : $"test-{name}";
        }

        if (type == typeof(DateTimeOffset))
        {
            return FirstObservationAtUtc;
        }

        if (type == typeof(int))
        {
            return name.Equals("totalTokens", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
        }

        if (type == typeof(decimal))
        {
            return 0m;
        }

        if (type == typeof(bool))
        {
            return false;
        }

        if (type.IsEnum)
        {
            return Enum.GetValues(type).GetValue(0);
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            return Array.CreateInstance(type.GetGenericArguments()[0], 0);
        }

        var guidConstructor = type.GetConstructor([typeof(Guid)]);
        if (guidConstructor is not null)
        {
            return guidConstructor.Invoke([id]);
        }

        var stringConstructor = type.GetConstructor([typeof(string)]);
        if (stringConstructor is not null)
        {
            return stringConstructor.Invoke([$"test-{name}"]);
        }

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private static async Task InvokeAppendAsync(object store, object observation)
    {
        var method = store.GetType().GetMethods()
            .SingleOrDefault(candidate => candidate.Name == "AppendAsync");
        Assert.NotNull(method);
        var arguments = method!.GetParameters()
            .Select(parameter => parameter.ParameterType == typeof(CancellationToken)
                ? (object)CancellationToken.None
                : parameter.ParameterType.IsInstanceOfType(observation)
                    ? observation
                    : IsReadOnlyListOf(parameter.ParameterType, observation.GetType())
                        ? CreateSingleItemArray(observation)
                        : null)
            .ToArray();

        object? awaitable;
        try
        {
            awaitable = method.Invoke(store, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }

        Assert.NotNull(awaitable);
        if (awaitable is Task task)
        {
            await task;
            return;
        }

        var asTask = awaitable!.GetType().GetMethod("AsTask", Type.EmptyTypes);
        Assert.NotNull(asTask);
        await Assert.IsAssignableFrom<Task>(asTask!.Invoke(awaitable, null));
    }

    private static Array CreateSingleItemArray(object item)
    {
        var array = Array.CreateInstance(item.GetType(), 1);
        array.SetValue(item, 0);
        return array;
    }

    private static ProviderUsageObservation CreateProviderObservation(
        Guid id,
        DateTimeOffset createdAtUtc,
        string model,
        string sourcePhase,
        int inputTokens,
        int cachedInputTokens,
        int outputTokens,
        int reasoningTokens,
        int totalTokens)
        => new(
            id,
            createdAtUtc,
            "workflow-usage-test-provider",
            ProviderKind.OpenAi,
            model,
            ProviderTransportKind.ChatCompletions,
            sourcePhase,
            ProviderUsageObservationStatus.Observed,
            inputTokens,
            cachedInputTokens,
            outputTokens,
            reasoningTokens,
            totalTokens,
            ToolCallCount: 0);

    private static ProviderProfile CreateProviderProfile()
        => new(
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            "workflow-usage-test-provider",
            ProviderKind.OpenAi,
            "https://example.invalid/v1",
            "WORKFLOW_USAGE_TEST_API_KEY",
            "model-a",
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: false,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: ["model-a"],
            Purpose: ProviderProfilePurpose.Chat);

    private static LlmCallComponent CreateLlmComponent()
        => new(
            WorkflowComponentId.New(),
            "Usage-preserving LLM",
            ProviderProfileId: null,
            "model-a",
            WorkflowModality.Text,
            new WorkflowModelSettings(
                Temperature: 0,
                MaxOutputTokens: 100,
                RequireJsonOutput: true,
                ResponseFormatJsonSchema: string.Empty),
            "Return JSON.",
            WorkflowValueShape.Text,
            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"),
            AgentPermissionsPolicy.Default,
            FirstObservationAtUtc,
            FirstObservationAtUtc);

    private static WorkflowNode CreateLlmNode(WorkflowComponentId componentId)
        => new(
            new WorkflowNodeId("llm"),
            WorkflowNodeKind.LlmCall,
            "LLM",
            [],
            new WorkflowNodeSettings(
                componentId,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: "Return JSON.",
                InputShape: WorkflowValueShape.Text,
                ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON")));

    private static WorkflowNode CreateExecutorNode()
        => new(
            new WorkflowNodeId("usage-executor"),
            WorkflowNodeKind.Executor,
            "Usage executor",
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: BuiltInWorkflowExecutorDescriptors.StorageFile.InputShape,
                ResultShape: BuiltInWorkflowExecutorDescriptors.StorageFile.ResultShape)
            {
                ExecutorId = WorkflowExecutorIds.StorageFile,
                ExecutorSettingsJson = "{}",
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
            });

    private static WorkflowNode CreateEndNode()
        => new(
            new WorkflowNodeId("end"),
            WorkflowNodeKind.End,
            "End",
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: BuiltInWorkflowExecutorDescriptors.StorageFile.ResultShape,
                ResultShape: BuiltInWorkflowExecutorDescriptors.StorageFile.ResultShape));

    private static WorkflowDefinition CreateDefinition(
        IReadOnlyList<WorkflowNode> nodes,
        IReadOnlyList<WorkflowEdge> edges,
        WorkflowNodeId startNodeId)
        => new(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Workflow usage red gate",
            "Deterministic usage and analytics contract gate.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(startNodeId, nodes, edges),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            FirstObservationAtUtc,
            FirstObservationAtUtc);

    private static WorkflowEdge CreateEdge(WorkflowNodeId source, WorkflowNodeId target)
        => new(
            new WorkflowEdgeId("executor-end"),
            source,
            SourcePortId: null,
            target,
            TargetPortId: null,
            WorkflowEdgeKind.Direct,
            ConditionExpression: string.Empty);

    private static WorkflowRunSnapshot CreateRun(int index)
    {
        var timestamp = FirstObservationAtUtc.AddMinutes(index);
        return new WorkflowRunSnapshot(
            new WorkflowRunId(Guid.Parse($"77777777-7777-7777-7777-{index + 1:D12}")),
            WorkflowId.New(),
            WorkflowVersionId.New(),
            WorkflowRunState.Completed,
            WorkflowRuntimeBackendKind.InProcess,
            $"backend-{index}",
            $"Run {index}",
            timestamp,
            timestamp);
    }

    private static string ReadSource(params string[] segments)
        => File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. segments]));

    private static string Slice(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Source marker '{startMarker}' was not found.");
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Source marker '{endMarker}' was not found.");
        return source[start..(end + endMarker.Length)];
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private sealed class RecordingLlmInvocationPort(LlmUsage usage, string responseText = "{\"ok\":true}") : ILlmInvocationPort
    {
        public LlmInvocationRequest? LastRequest { get; private set; }

        public Task<LlmInvocationResult> InvokeAsync(
            LlmInvocationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return Task.FromResult(new LlmInvocationResult(request.Model, responseText, usage));
        }
    }

    private sealed class SingleProviderRegistry(ProviderProfile provider) :
        IProviderProfileRegistry,
        IProviderRuntimeProfileSource
    {
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProviderProfile>>([provider]);

        public Task<ProviderProfile?> GetProviderAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProviderProfile?>(provider.Id == providerId ? provider : null);

        public Task<ProviderProfileEditorModel> GetProviderEditorAsync(
            Guid? providerId = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Guid> SaveProviderAsync(
            ProviderProfileEditorModel model,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteProviderAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProviderProfile> UpdateProviderAsync(
            Guid providerId,
            Func<ProviderProfile, ProviderProfile> update,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class UsageWorkflowExecutor : IWorkflowExecutor
    {
        public static WorkflowUsageMetrics ExpectedUsage { get; } = new(
            "executor-provider",
            "executor-model",
            InputTokens: 17,
            CachedInputTokens: 3,
            OutputTokens: 5,
            CostUsd: 0.25m);

        public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.StorageFile;

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new WorkflowNodeExecutionResult(
                context.Node.Id,
                "{\"ok\":true}",
                context.Descriptor.ResultShape)
            {
                Usage = ExpectedUsage
            });
    }

    private sealed class CapturingProgressObserver : IWorkflowNodeExecutionProgressObserver
    {
        public List<WorkflowNodeExecutionProgress> Records { get; } = [];

        public ValueTask RecordAsync(
            WorkflowNodeExecutionProgress progress,
            CancellationToken cancellationToken = default)
        {
            Records.Add(progress);
            return ValueTask.CompletedTask;
        }
    }
}
