using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Maf;

public sealed class MafInProcessWorkflowExecutionBackend :
    IWorkflowExecutionBackend,
    IWorkflowExternalResponseBackend
{
    private readonly IWorkflowMafCompiler compiler;
    private readonly MafLegacyWorkflowExecutionDriver legacyDriver;
    private readonly MafWorkflowNativeStartDriver? nativeStartDriver;
    private readonly MafWorkflowExternalResponseDriver? externalResponseDriver;
    private readonly IReadOnlyList<LlmCallComponent>? components;
    private readonly IWorkflowComponentLibraryService? componentLibrary;

    public MafInProcessWorkflowExecutionBackend(
        IWorkflowMafCompiler compiler,
        IReadOnlyList<LlmCallComponent> components,
        IMafWorkflowEventNormalizer? eventNormalizer = null,
        IWorkflowCheckpointFactory? checkpointFactory = null,
        IWorkflowPayloadPolicyService? payloadPolicyService = null,
        TimeProvider? timeProvider = null,
        IWorkflowBackendCheckpointPayloadStore? checkpointPayloadStore = null,
        IWorkflowCatalogService? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(components);

        this.compiler = compiler;
        this.components = components;
        var normalizer = eventNormalizer ?? new MafWorkflowEventNormalizer();
        var checkpointRecordFactory = checkpointFactory ?? new WorkflowCheckpointFactory();
        var payloadPolicy = payloadPolicyService ?? new WorkflowPayloadPolicyService();
        var clock = timeProvider ?? TimeProvider.System;
        legacyDriver = new MafLegacyWorkflowExecutionDriver(
            normalizer,
            checkpointRecordFactory,
            payloadPolicy,
            clock);
        (nativeStartDriver, externalResponseDriver) = CreateNativeDrivers(
            compiler,
            checkpointPayloadStore,
            catalog,
            normalizer,
            checkpointRecordFactory,
            payloadPolicy,
            clock);
        Descriptor = CreateDescriptor(externalResponseDriver is not null);
    }

    public MafInProcessWorkflowExecutionBackend(
        IWorkflowMafCompiler compiler,
        IWorkflowComponentLibraryService componentLibrary,
        IMafWorkflowEventNormalizer? eventNormalizer = null,
        IWorkflowCheckpointFactory? checkpointFactory = null,
        IWorkflowPayloadPolicyService? payloadPolicyService = null,
        TimeProvider? timeProvider = null,
        IWorkflowBackendCheckpointPayloadStore? checkpointPayloadStore = null,
        IWorkflowCatalogService? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(componentLibrary);

        this.compiler = compiler;
        this.componentLibrary = componentLibrary;
        var normalizer = eventNormalizer ?? new MafWorkflowEventNormalizer();
        var checkpointRecordFactory = checkpointFactory ?? new WorkflowCheckpointFactory();
        var payloadPolicy = payloadPolicyService ?? new WorkflowPayloadPolicyService();
        var clock = timeProvider ?? TimeProvider.System;
        legacyDriver = new MafLegacyWorkflowExecutionDriver(
            normalizer,
            checkpointRecordFactory,
            payloadPolicy,
            clock);
        (nativeStartDriver, externalResponseDriver) = CreateNativeDrivers(
            compiler,
            checkpointPayloadStore,
            catalog,
            normalizer,
            checkpointRecordFactory,
            payloadPolicy,
            clock);
        Descriptor = CreateDescriptor(externalResponseDriver is not null);
    }

    public WorkflowRuntimeBackendDescriptor Descriptor { get; }

    public async Task<WorkflowBackendStartResult> StartAsync(
        WorkflowDefinition definition,
        WorkflowRunStartRequest request,
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);

        var resolvedComponents = await ResolveComponentsAsync(definition, cancellationToken);
        var build = compiler.Compile(
            definition,
            resolvedComponents,
            request.PreviewSimulationPlan);
        if (build.Compilation.Succeeded &&
            build.Workflow is not null &&
            build.HasNativeExternalRequests)
        {
            if (nativeStartDriver is null)
            {
                throw new InvalidOperationException(
                    "MAF workflows with native external requests require both a checkpoint payload store and an exact workflow catalog.");
            }

            return await nativeStartDriver.StartAsync(
                definition,
                request,
                runId,
                build,
                cancellationToken);
        }

        return await legacyDriver.StartAsync(
            definition,
            request,
            runId,
            build,
            cancellationToken);
    }

    public async Task<WorkflowBackendStartResult> ResumeAsync(
        WorkflowRunSnapshot run,
        WorkflowExternalRequestRecord request,
        string responseJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseJson);
        using var document = JsonDocument.Parse(responseJson);
        return await ResumeAsync(
            new WorkflowBackendResumeRequest(
                run,
                request,
                document.RootElement.Clone()),
            cancellationToken);
    }

    public async Task<WorkflowBackendStartResult> ResumeAsync(
        WorkflowBackendResumeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (externalResponseDriver is null)
        {
            throw new InvalidOperationException(
                "MAF external-response resume requires both a checkpoint payload store and an exact workflow catalog.");
        }

        var resolvedComponents = await ResolveAllComponentsAsync(cancellationToken);
        return await externalResponseDriver.ResumeAsync(request, resolvedComponents, cancellationToken);
    }

    private async Task<IReadOnlyList<LlmCallComponent>> ResolveComponentsAsync(
        WorkflowDefinition definition,
        CancellationToken cancellationToken)
    {
        var resolved = componentLibrary is null
            ? components ?? []
            : await componentLibrary.ListComponentsAsync(cancellationToken);
        return FilterReferencedComponents(definition, resolved);
    }

    private async Task<IReadOnlyList<LlmCallComponent>> ResolveAllComponentsAsync(
        CancellationToken cancellationToken)
    {
        return componentLibrary is null
            ? components ?? []
            : await componentLibrary.ListComponentsAsync(cancellationToken);
    }

    private static (
        MafWorkflowNativeStartDriver? Start,
        MafWorkflowExternalResponseDriver? Response) CreateNativeDrivers(
        IWorkflowMafCompiler compiler,
        IWorkflowBackendCheckpointPayloadStore? checkpointPayloadStore,
        IWorkflowCatalogService? catalog,
        IMafWorkflowEventNormalizer eventNormalizer,
        IWorkflowCheckpointFactory checkpointFactory,
        IWorkflowPayloadPolicyService payloadPolicyService,
        TimeProvider timeProvider)
    {
        if (checkpointPayloadStore is null || catalog is null)
        {
            return (null, null);
        }

        var streamingDriver = new MafWorkflowStreamingRunDriver();
        var requestMapper = new MafWorkflowExternalRequestMapper(timeProvider);
        var turnResultMapper = new MafWorkflowTurnResultMapper(
            checkpointPayloadStore,
            requestMapper,
            eventNormalizer,
            checkpointFactory,
            payloadPolicyService,
            timeProvider);
        return (
            new MafWorkflowNativeStartDriver(
                checkpointPayloadStore,
                streamingDriver,
                turnResultMapper,
                timeProvider),
            new MafWorkflowExternalResponseDriver(
                compiler,
                catalog,
                checkpointPayloadStore,
                streamingDriver,
                new MafWorkflowRehydrationVerifier(),
                requestMapper,
                turnResultMapper));
    }

    private static WorkflowRuntimeBackendDescriptor CreateDescriptor(bool supportsResume)
    {
        return new WorkflowRuntimeBackendDescriptor(
            WorkflowRuntimeBackendKind.InProcess,
            "MAF in-process workflow runtime",
            IsDurable: false,
            SupportsStreaming: true,
            SupportsExternalRequests: true,
            SupportsDashboardObservability: false,
            OperationalNotes: "Use for local development, tests, previews, and approved short non-durable runs only.")
        {
            SupportsExternalResponseResume = supportsResume,
            SupportsActiveCancellation = true
        };
    }

    private static IReadOnlyList<LlmCallComponent> FilterReferencedComponents(
        WorkflowDefinition definition,
        IReadOnlyList<LlmCallComponent> resolvedComponents)
    {
        var referencedComponentIds = definition.Graph.Nodes
            .Where(node => node.Kind == WorkflowNodeKind.LlmCall && node.Settings.ComponentId.HasValue)
            .Select(node => node.Settings.ComponentId!.Value)
            .ToHashSet();
        if (referencedComponentIds.Count == 0)
        {
            return [];
        }

        return resolvedComponents
            .Where(component => referencedComponentIds.Contains(component.Id))
            .ToArray();
    }
}
