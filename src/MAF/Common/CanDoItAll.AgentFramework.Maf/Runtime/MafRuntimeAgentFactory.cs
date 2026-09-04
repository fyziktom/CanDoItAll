using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.FileSystem;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafRuntimeAgentFactory
{
    private static readonly ProviderProfileService ProviderFeatureService = new();
    private static readonly JsonSerializerOptions LoggingJsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly string workspaceRoot;
    private readonly WorkspaceScopeDescriptor workspaceScope;
    private readonly IMafProviderCredentialService providerCredentialService;
    private readonly IMafProviderAgentFactory providerAgentFactory;
    private readonly IRuntimeCapabilityComposer runtimeCapabilityComposer;
    private readonly ILoggerFactory loggerFactory;
    private readonly AgentToolInvocationPolicyPipeline toolInvocationPolicyPipeline;
    private readonly IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory;

    public MafRuntimeAgentFactory(
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        IMafProviderCredentialService providerCredentialService,
        IMafProviderAgentFactory providerAgentFactory,
        IRuntimeCapabilityComposer runtimeCapabilityComposer,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
        ILoggerFactory? loggerFactory = null,
        AgentToolInvocationPolicyPipeline? toolInvocationPolicyPipeline = null)
    {
        ArgumentNullException.ThrowIfNull(toolInvocationPolicyPipeline);
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root must be provided.", nameof(workspaceRoot));
        }

        this.workspaceRoot = workspaceRoot;
        this.workspaceScope = workspaceScope;
        this.loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        this.providerCredentialService = providerCredentialService ?? throw new ArgumentNullException(nameof(providerCredentialService));
        this.providerAgentFactory = providerAgentFactory ?? throw new ArgumentNullException(nameof(providerAgentFactory));
        this.runtimeCapabilityComposer = runtimeCapabilityComposer ?? throw new ArgumentNullException(nameof(runtimeCapabilityComposer));
        this.physicalPathPolicyFactory = physicalPathPolicyFactory ?? throw new ArgumentNullException(nameof(physicalPathPolicyFactory));
        // The injected governance pipeline is the only policy seam: the
        // factory never constructs a policy itself, so composition decides the
        // policy and its domain contributors in one place.
        this.toolInvocationPolicyPipeline = toolInvocationPolicyPipeline;
    }

    public async Task<AIAgent> CreateHostedAgentAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        WorkspaceRuntimeServices workspaceRuntimeServices,
        CancellationToken cancellationToken = default,
        bool suppressApprovalRequirements = false,
        bool forceOmitTemperature = false,
        AgentRuntimeExecutionOptions? executionOptions = null)
    {
        var runtimeBuild = await CreateRuntimeBuildAsync(
            agent,
            provider,
            capabilities,
            memory,
            workspaceRuntimeServices,
            static (_, _, _) => Task.CompletedTask,
            cancellationToken,
            suppressApprovalRequirements,
            forceOmitTemperature,
            executionOptions);

        return new HostedRuntimeAgent(runtimeBuild);
    }

    public async Task<RuntimeBuildResult> CreateRuntimeBuildAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        WorkspaceRuntimeServices workspaceRuntimeServices,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements = false,
        bool forceOmitTemperature = false,
        AgentRuntimeExecutionOptions? executionOptions = null,
        string runtimeSessionKey = "",
        bool ownsWorkspaceRuntimeServices = true)
    {
        ArgumentNullException.ThrowIfNull(workspaceRuntimeServices);
        var runtimeOptions = executionOptions ?? MafRuntimeExecutionOptionsResolver.CreateDisabled(null);
        if (runtimeOptions.Handoff is not null)
        {
            return await CreateHandoffRuntimeBuildAsync(
                agent,
                provider,
                workspaceRuntimeServices,
                progressCallback,
                cancellationToken,
                suppressApprovalRequirements,
                forceOmitTemperature,
                runtimeOptions,
                runtimeSessionKey,
                ownsWorkspaceRuntimeServices);
        }

        var toolInvocationTraceRecorder = new ToolInvocationTraceRecorder();
        var openAiCredentialOverride = ResolveOpenAiCredentialOverride(provider);
        var effectiveProvider = ManagedSeedProviderFallbacks.Apply(agent, provider, openAiCredentialOverride);
        var requestedModel = ManagedSeedProviderFallbacks.ResolveModel(agent, effectiveProvider, openAiCredentialOverride);
        var model = InputAttachmentSupport.ResolveRuntimeModel(effectiveProvider, requestedModel, runtimeOptions);
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException($"Provider '{effectiveProvider.Name}' does not have a default model and the agent '{agent.Name}' does not override one.");
        }

        if (!string.Equals(requestedModel, model, StringComparison.OrdinalIgnoreCase))
        {
            await progressCallback(
                ExecutionState.Preparing,
                "Input attachments",
                $"Using provider image-analysis model '{model}' for request-scoped image attachment(s) because runtime model '{requestedModel}' is not vision-capable.");
        }

        var requestedReasoningEffort = MafModelParametersBuilder.ResolveEffectiveThinkingEffort(
            effectiveProvider,
            model,
            agent.ConfigurationJson);

        MafRuntimeExecutionOptionsResolver.EnsureStructuredOutputCapability(effectiveProvider, runtimeOptions);
        var finalizerCapture = MafFinalizerToolFactory.CreateCapture(
            runtimeOptions.StructuredOutput,
            runtimeOptions.FinalizerMode);
        var contextWorkspaceScope = ResolveContextWorkspaceScope(
            runtimeOptions,
            workspaceScope);
        var capabilityState = await runtimeCapabilityComposer.CreateCapabilityStateCoreAsync(
            agent,
            effectiveProvider,
            model,
            capabilities,
            memory,
            progressCallback,
            cancellationToken,
            suppressApprovalRequirements,
            contextWorkspaceScope,
            runtimeOptions.ContextIntent ?? AgentRuntimeContextIntent.Empty,
            workspaceRuntimeServices,
            runtimeSessionKey,
            runtimeOptions.TransientContext?.Attachments,
            runtimeOptions.Governance);
        try
        {
            // Exactly one owner disposes the run's workspace bundle: the top-level
            // build result. Handoff participant builds share the bundle and must
            // not register a second ownership.
            if (ownsWorkspaceRuntimeServices)
            {
                capabilityState.AsyncDisposables.Add(workspaceRuntimeServices);
            }
            await FilterToolsOutsideExecutionGovernanceAsync(
                capabilityState,
                runtimeOptions.Governance,
                progressCallback);
            await FilterUnusableApprovalToolsAsync(
                capabilityState,
                effectiveProvider,
                suppressApprovalRequirements,
                progressCallback);
            AttachTransientContextProvider(
                capabilityState,
                runtimeOptions.TransientContext);

            if (finalizerCapture is not null)
            {
                capabilityState.Tools.AddRange(finalizerCapture.Tools);
                await progressCallback(
                    ExecutionState.Preparing,
                    "Finalizer policy",
                    $"Attached {runtimeOptions.FinalizerMode} finalizer tool '{finalizerCapture.Policy.ToolName}' for structured output contract '{finalizerCapture.Policy.OutputContract.ContractKey}'.");
            }

            var frameworkManagedHistory = MafRuntimeSessionBuilder.ShouldUseFrameworkManagedHistory(agent, effectiveProvider, runtimeOptions);
            var chatOptions = MafModelParametersBuilder.CreateModelCompatibleChatOptions(
                effectiveProvider,
                model,
                (float)agent.Temperature,
                forceOmitTemperature,
                agent.ConfigurationJson);
            chatOptions.Instructions = AppendFinalizerInstructions(
                agent.Instructions,
                finalizerCapture?.Policy,
                runtimeOptions.FinalizerMode,
                MafRuntimeSessionBuilder.ShouldApplyStructuredOutputResponseFormat(runtimeOptions));
            chatOptions.AllowMultipleToolCalls = MafFinalizerDriver.ResolveAllowMultipleToolCalls(
                capabilityState.Tools.Count > 0,
                ProviderFeatureService
                    .ResolveFeatureMatrixForModel(effectiveProvider, model)
                    .SupportsParallelFunctionTools,
                runtimeOptions.FinalizerMode,
                capabilityState.HasApprovalTools);

            if (capabilityState.Tools.Count > 0)
            {
                chatOptions.Tools = [.. capabilityState.Tools];
            }

            var requestCompatibilityEvidence = MafProviderRequestCompatibilityEvidenceFactory.Create(
                effectiveProvider,
                requestedModel,
                model,
                capabilityState.Tools,
                requestedReasoningEffort);
            var compatibilityProgressMessage =
                MafProviderRequestCompatibilityEvidenceFactory.CreateAdjustmentProgressMessage(
                    requestCompatibilityEvidence);
            if (compatibilityProgressMessage is not null)
            {
                await progressCallback(
                    ExecutionState.Preparing,
                    "Provider request compatibility",
                    $"Agent '{agent.Name}' ({agent.Id:D}): {compatibilityProgressMessage}");
            }

            var options = MafChatClientAgentOptionsFactory.Create(chatOptions);
            options.Id = agent.Id.ToString("D");
            options.Name = agent.Name;
            options.Description = agent.Summary;
            options.AIContextProviders = capabilityState.ContextProviders;
            options.ChatHistoryProvider = frameworkManagedHistory ? CreateChatHistoryProvider() : null;
            options.RequirePerServiceCallChatHistoryPersistence =
                MafChatClientAgentOptionsFactory.ResolvePerServiceCallHistoryPersistence(
                    agent.RequirePerServiceCallChatHistoryPersistence,
                    frameworkManagedHistory,
                    capabilityState.HasApprovalTools);

            var runtimeAgent = CreateInstrumentedAgent(
                providerAgentFactory.CreateFrameworkAgent(
                    effectiveProvider,
                    model,
                    options,
                    frameworkManagedHistory,
                    agent.EnableBackgroundResponses && MafRuntimeSessionBuilder.SupportsBackgroundResponses(effectiveProvider)),
                effectiveProvider,
                agent,
                capabilityState,
                suppressApprovalRequirements,
                toolInvocationTraceRecorder,
                finalizerCapture?.Policy,
                runtimeOptions.FinalizerMode,
                runtimeOptions.Governance,
                // Script inspection reads through the effective run scope so
                // policy evaluation inspects exactly the file the run's tools can
                // execute — never the runtime construction scope.
                new MafScriptPolicyInspectionService(
                    workspaceRoot,
                    contextWorkspaceScope,
                    physicalPathPolicyFactory,
                    workspaceRuntimeServices.ExternalTargetPathRegistry));
            return new RuntimeBuildResult(
                runtimeAgent,
                effectiveProvider,
                model,
                capabilityState.AsyncDisposables,
                capabilityState.Disposables,
                capabilityState.HasApprovalTools,
                MafModelParametersBuilder.ShouldOmitTemperature(effectiveProvider, model, forceOmitTemperature),
                finalizerCapture,
                toolInvocationTraceRecorder,
                capabilityState.ContextContributionTraceCollector,
                runtimeCapabilityState: capabilityState,
                entryAgentRequestCompatibilityEvidence: requestCompatibilityEvidence);
        }
        catch (Exception buildException)
        {
            var cleanupExceptions = await capabilityState.DisposeAcquiredResourcesAsync().ConfigureAwait(false);
            if (cleanupExceptions.Count > 0)
            {
                throw new AggregateException(
                    "Runtime agent construction failed and one or more acquired resources also failed cleanup.",
                    [buildException, .. cleanupExceptions]);
            }

            throw;
        }
    }

    internal static void AttachTransientContextProvider(
        RuntimeCapabilityState capabilityState,
        AgentRuntimeTransientContext? transientContext)
    {
        ArgumentNullException.ThrowIfNull(capabilityState);
        if (transientContext?.HasContent != true)
        {
            return;
        }

        capabilityState.ContextProviders.Add(
            new StaticMessageContextProvider(
                new ChatMessage(
                    ChatRole.User,
                    transientContext.Content),
                StaticMessageContextProvider.TransientAgentChatStateKey));
    }

    internal static WorkspaceScopeDescriptor ResolveContextWorkspaceScope(
        AgentRuntimeExecutionOptions runtimeOptions,
        WorkspaceScopeDescriptor fallbackWorkspaceScope)
    {
        ArgumentNullException.ThrowIfNull(runtimeOptions);
        ArgumentNullException.ThrowIfNull(fallbackWorkspaceScope);
        var optionsScope = runtimeOptions.ContextWorkspaceScope;
        var intentScope = runtimeOptions.ContextIntent?.WorkspaceScope;
        var transientScope = runtimeOptions.TransientContext?.WorkspaceScope;
        var resolvedScope = optionsScope ?? intentScope ?? transientScope ?? fallbackWorkspaceScope;
        if ((optionsScope is not null && optionsScope != resolvedScope) ||
            (intentScope is not null && intentScope != resolvedScope) ||
            (transientScope is not null && transientScope != resolvedScope))
        {
            throw new InvalidOperationException(
                "Runtime context intent and execution options have conflicting workspace scopes.");
        }

        return resolvedScope;
    }

    private async Task<RuntimeBuildResult> CreateHandoffRuntimeBuildAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        WorkspaceRuntimeServices workspaceRuntimeServices,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements,
        bool forceOmitTemperature,
        AgentRuntimeExecutionOptions runtimeOptions,
        string runtimeSessionKey,
        bool ownsWorkspaceRuntimeServices)
    {
        var handoffOptions = runtimeOptions.Handoff
            ?? throw new InvalidOperationException("Handoff runtime build requires handoff execution options.");
        var settings = AgentHandoffMetadata.Normalize(handoffOptions.Settings);
        var validation = AgentHandoffMetadata.Validate(settings);
        if (!validation.Succeeded)
        {
            throw new InvalidOperationException("Agent handoff configuration is invalid: " + string.Join(" ", validation.Errors));
        }

        var participantIds = AgentHandoffMetadata.ResolveParticipantAgentIds(settings, handoffOptions.EntryAgentId);
        var missingParticipantIds = participantIds
            .Where(participantId => handoffOptions.Participants.All(item => item.Agent.Id != participantId))
            .Select(participantId => participantId.ToString("D"))
            .ToList();
        if (missingParticipantIds.Count > 0)
        {
            throw new InvalidOperationException("Handoff participants are incomplete: " + string.Join(", ", missingParticipantIds));
        }

        await progressCallback(
            ExecutionState.Preparing,
            "Handoff",
            $"Composing a Microsoft Agent Framework handoff workflow with {participantIds.Count} local participant agent(s).");

        var participantBuilds = new List<RuntimeBuildResult>();
        try
        {
            var participantAgents = new Dictionary<Guid, AIAgent>();
            foreach (var participant in handoffOptions.Participants.Where(item => participantIds.Contains(item.Agent.Id)))
            {
                var effectiveParticipantOptions = ResolveHandoffParticipantExecutionOptions(
                    runtimeOptions,
                    participant.Agent.Id,
                    handoffOptions.EntryAgentId);
                var participantBuild = await CreateRuntimeBuildAsync(
                    participant.Agent,
                    participant.Provider,
                    participant.Capabilities,
                    participant.Memory,
                    workspaceRuntimeServices,
                    progressCallback,
                    cancellationToken,
                    suppressApprovalRequirements,
                    forceOmitTemperature,
                    effectiveParticipantOptions,
                    runtimeSessionKey,
                    ownsWorkspaceRuntimeServices: false);
                participantBuilds.Add(participantBuild);
                participantAgents[participant.Agent.Id] = participantBuild.Agent;
            }

            var entryBuild = participantBuilds.FirstOrDefault(item =>
                    string.Equals(item.Agent.Id, handoffOptions.EntryAgentId.ToString("D"), StringComparison.OrdinalIgnoreCase))
                ?? participantBuilds.FirstOrDefault(item => item.Agent.Id == agent.Id.ToString("D"))
                ?? throw new InvalidOperationException($"Handoff entry agent '{handoffOptions.EntryAgentId:D}' was not built.");
            var buildResult = MafHandoffWorkflowFactory.Build(
                settings,
                participantAgents,
                handoffOptions.EntryAgentId,
                handoffOptions.CorrelationId);

            // The handoff result is the single owner of the shared workspace
            // bundle (participant builds were created with ownership disabled).
            IReadOnlyList<IAsyncDisposable> handoffAsyncDisposables = ownsWorkspaceRuntimeServices
                ? [.. participantBuilds, workspaceRuntimeServices]
                : [.. participantBuilds];
            return new RuntimeBuildResult(
                buildResult.Agent,
                entryBuild.Provider,
                entryBuild.Model,
                handoffAsyncDisposables,
                [],
                participantBuilds.Any(item => item.HasApprovalTools),
                entryBuild.IsTemperatureOmitted,
                finalizerCapture: null,
                toolInvocationTraceRecorder: null,
                contextContributionTraceCollector: null,
                snapshotFinalizerInvocations: () => participantBuilds
                    .SelectMany(item => item.SnapshotFinalizerInvocations())
                    .OrderBy(item => item.Sequence)
                    .ToList(),
                snapshotToolInvocationTraces: () => participantBuilds
                    .SelectMany(item => item.SnapshotToolInvocationTraces())
                    .OrderBy(item => item.Sequence)
                    .ToList(),
                snapshotContextContributionTraces: () => participantBuilds
                    .SelectMany(item => item.SnapshotContextContributionTraces())
                    .ToList(),
                isTerminalResponseUpdate: buildResult.IsTerminalResponseUpdate,
                entryAgentRequestCompatibilityEvidence: entryBuild.EntryAgentRequestCompatibilityEvidence);
        }
        catch (Exception exception)
        {
            foreach (var participantBuild in participantBuilds)
            {
                await participantBuild.DisposeAsync(exception);
            }

            throw;
        }
    }

    internal static AgentRuntimeExecutionOptions ResolveHandoffParticipantExecutionOptions(
        AgentRuntimeExecutionOptions runtimeOptions,
        Guid participantAgentId,
        Guid entryAgentId)
    {
        ArgumentNullException.ThrowIfNull(runtimeOptions);
        var participantOptions = runtimeOptions with
        {
            Handoff = null
        };
        return participantAgentId == entryAgentId
            ? participantOptions
            : participantOptions with { TransientContext = null };
    }

    public AIAgent CreateInstrumentedAgent(
        AIAgent agent,
        ProviderProfile provider,
        AgentDefinition agentDefinition,
        RuntimeCapabilityState capabilityState,
        bool suppressApprovalRequirements,
        ToolInvocationTraceRecorder toolInvocationTraceRecorder,
        AgentFinalizerPolicy? finalizerPolicy,
        AgentFinalizerMode finalizerMode,
        AgentExecutionGovernanceSnapshot? executionGovernance,
        MafScriptPolicyInspectionService scriptPolicyInspectionService)
    {
        ArgumentNullException.ThrowIfNull(scriptPolicyInspectionService);
        var builder = agent.AsBuilder();
        var toolPolicy = toolInvocationPolicyPipeline;
        var knownToolNames = capabilityState.Tools
            .Select(tool => tool.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var toolName in capabilityState.FrameworkToolNames)
        {
            knownToolNames.Add(toolName);
        }

        var approvalWrappedToolNames = capabilityState.Tools
            .Where(tool => tool is ApprovalRequiredAIFunction)
            .Select(tool => tool.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var runtimeToolOwnershipByToolName = CreateRuntimeToolOwnershipByToolName(capabilityState);
        var featureMatrix = ProviderFeatureService.ResolveFeatureMatrix(provider);
        var logger = loggerFactory.CreateLogger<MafAgentRuntime>();
        var configuredWorkspaceAccess = AgentWorkspaceToolAccessMetadata.Read(agentDefinition.ConfigurationJson);
        builder.UseLogging(
            loggerFactory,
            logging => logging.JsonSerializerOptions = LoggingJsonSerializerOptions);
        builder.Use(async (innerAgent, context, next, cancellationToken) =>
        {
            var functionName = context.Function?.Name ?? "unknown";
            var invocationArguments = context.Arguments.ToArray();
            var redactedArguments = AgentToolInvocationPolicyMetadata.RedactArguments(functionName, invocationArguments);
            var pathArguments = ToolInvocationPathArgumentResolver.Resolve(functionName, invocationArguments);
            var isRequiredFinalizerTool = IsRequiredFinalizerTool(functionName, finalizerPolicy, finalizerMode);
            var classification = isRequiredFinalizerTool
                ? ToolInvocationClassification.Read
                : AgentToolInvocationPolicyMetadata.Classify(functionName);
            var auditScope = WorkspaceExecutionAuditContext.Current;
            var externalTargetAccess = EffectiveExternalTargetAccessResolver.Resolve(
                configuredWorkspaceAccess,
                auditScope?.AllowedExternalTargetAliases,
                auditScope?.ReadOnlyExternalTargetAliases,
                auditScope?.InvocationExternalTargetScopeIsAuthoritative == true);
            var scriptSideEffectManifestJson = MafScriptPolicyInspectionService.TryGetStringArgument(
                invocationArguments,
                GovernedScriptSideEffectManifest.ArgumentName) ?? string.Empty;
            var scriptInspection = scriptPolicyInspectionService.ResolveScriptContentInspectionForPolicy(
                functionName,
                invocationArguments,
                auditScope,
                scriptSideEffectManifestJson);
            // Provider-neutral policy context: the adapter maps generic run,
            // tool, workspace, and governance facts only. Domain restrictions
            // (for example governed process rules) are contributed by the
            // owning module through the injected policy pipeline; this adapter
            // does not interpret process fields.
            var neutralPolicyContext = new ToolInvocationPolicyContext(
                AgentId: agentDefinition.Id,
                AgentName: agentDefinition.Name,
                ToolName: functionName,
                RedactedArguments: redactedArguments,
                Classification: classification,
                IsKnownTool: isRequiredFinalizerTool || knownToolNames.Contains(functionName),
                AutoApprovalAllowed: suppressApprovalRequirements,
                ApprovalWrapperAvailable: approvalWrappedToolNames.Contains(functionName),
                ExecutionRunId: auditScope?.ExecutionRunId.ToString("D") ?? string.Empty,
                SourceKind: auditScope?.SourceKind ?? string.Empty,
                ProcessRunId: string.Empty,
                ProcessStepId: string.Empty,
                AllowedExternalTargetAliases: externalTargetAccess.WritableAliases,
                ReadOnlyExternalTargetAliases: externalTargetAccess.ReadOnlyAliases,
                ApprovalWrapperEffectiveForProvider: featureMatrix.SupportsApprovalRequiredAIFunction,
                ApplicationApprovalAvailable: false,
                ContextWorkspaceScopeKind: auditScope?.ContextWorkspaceScope?.Kind.ToString() ?? string.Empty,
                ContextWorkspaceScopeKey: auditScope?.ContextWorkspaceScope?.Key ?? string.Empty,
                InspectedScriptContent: scriptInspection.Content,
                ScriptInspectionFailure: scriptInspection.FailureMessage,
                ScriptSideEffectManifestJson: scriptSideEffectManifestJson,
                ToolInvocationTraces: toolInvocationTraceRecorder.Snapshot())
            {
                SourceId = auditScope?.SourceId ?? string.Empty,
                AllowedManagedArtifactReadRefs = auditScope?.AllowedManagedArtifactReadRefs ?? [],
                ExecutionGovernance = executionGovernance,
                PathArguments = pathArguments
            };
            var policyEvaluation = await toolPolicy
                .ComposeAndEvaluateAsync(neutralPolicyContext, auditScope, cancellationToken);
            var policyContext = policyEvaluation.EffectiveContext;
            var policyDecision = policyEvaluation.Decision;
            using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("maf.function.invoke", ActivityKind.Internal);
            AgentFrameworkTelemetry.ApplyCurrentAuditScope(activity);
            activity?.SetTag("agentframework.tool_name", functionName);
            activity?.SetTag("agentframework.tool_policy_decision", policyDecision.Kind.ToString());
            activity?.SetTag("agentframework.tool_policy_signature", policyDecision.Signature);
            activity?.SetTag("agentframework.tool_policy_reason", policyDecision.Reason);
            activity?.SetTag("agentframework.tool_approval_effective", policyContext.HasEffectiveApprovalPath);
            activity?.SetTag("agentframework.provider_supports_tool_approval", featureMatrix.SupportsApprovalRequiredAIFunction);
            activity?.SetTag("agentframework.tool_call_index", context.FunctionCallIndex);
            activity?.SetTag("agentframework.tool_iteration", context.Iteration);
            activity?.SetTag("agentframework.tool_count", context.FunctionCount);
            activity?.SetTag("agentframework.tool_is_streaming", context.IsStreaming);
            runtimeToolOwnershipByToolName.TryGetValue(functionName, out var runtimeToolOwnership);
            if (runtimeToolOwnership is not null)
            {
                activity?.SetTag("agentframework.runtime_tool_provider_key", runtimeToolOwnership.ProviderKey);
                activity?.SetTag("agentframework.runtime_tool_provider_name", runtimeToolOwnership.ProviderName);
            }

            logger?.LogInformation(
                "Agent tool policy decision {Decision} for tool {ToolName} on agent {AgentId}. ExecutionRunId={ExecutionRunId} SourceKind={SourceKind} ProcessRunId={ProcessRunId} ProcessStepId={ProcessStepId} RuntimeToolProviderKey={RuntimeToolProviderKey} Signature={Signature}",
                policyDecision.Kind,
                functionName,
                agentDefinition.Id,
                policyContext.ExecutionRunId,
                policyContext.SourceKind,
                policyContext.ProcessRunId,
                policyContext.ProcessStepId,
                runtimeToolOwnership?.ProviderKey ?? string.Empty,
                policyDecision.Signature);

            var traceSequence = toolInvocationTraceRecorder.Start(
                functionName,
                classification,
                policyDecision.Signature,
                runtimeToolOwnership,
                pathArguments,
                auditScope?.SourceKind ?? string.Empty,
                auditScope?.SourceId ?? string.Empty,
                MafToolInvocationCorrelationKey.Create(functionName, invocationArguments));
            var succeeded = false;
            var outcome = AgentToolInvocationOutcome.Unknown;
            var effectState = classification == ToolInvocationClassification.Read
                ? AgentToolEffectState.None
                : AgentToolEffectState.Unknown;
            var failureCode = string.Empty;
            var canRetryWithCorrectedInput = false;
            var failureMessage = string.Empty;
            var failureMessageSafeForPersistence = false;
            Guid? directReceiptExecutionRunId = null;
            var effectSourceKind = auditScope?.SourceKind ?? string.Empty;
            var effectSourceId = auditScope?.SourceId ?? string.Empty;
            using var runtimeToolOwnershipScope = AgentRuntimeToolOwnershipContext.BeginScope(runtimeToolOwnership);
            using var effectScope = AgentToolInvocationEffectScope.Begin();
            try
            {
                if (AgentToolPolicyBlockGuard.TryCreateRecoverableDeniedResult(
                        functionName,
                        policyDecision,
                        policyContext,
                        out var policyDeniedResult))
                {
                    failureMessage = policyDeniedResult;
                    failureMessageSafeForPersistence = true;
                    failureCode = "ToolPolicyDenied";
                    outcome = AgentToolInvocationOutcome.Failed;
                    effectState = AgentToolEffectState.NotCommitted;
                    activity?.SetTag("agentframework.tool_policy_recoverable_denial", true);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    logger?.LogInformation(
                        "Returning recoverable policy denial for tool {ToolName} on governed run {ProcessRunId}, step {ProcessStepId}. Reason={Reason}",
                        functionName,
                        policyContext.ProcessRunId,
                        policyContext.ProcessStepId,
                        policyDecision.Reason);
                    return policyDeniedResult;
                }

                AgentToolPolicyBlockGuard.ThrowIfBlocked(
                    functionName,
                    policyDecision,
                    policyContext.HasEffectiveApprovalPath);

                if (MafToolArgumentBindingFailureMapper.TryCreatePreInvocationFailure(
                        context.Function as AIFunction,
                        invocationArguments,
                        out var argumentFailure))
                {
                    failureMessage = $"{argumentFailure.ErrorCode}: {argumentFailure.Message}";
                    failureMessageSafeForPersistence = true;
                    failureCode = argumentFailure.ErrorCode;
                    canRetryWithCorrectedInput = argumentFailure.CanRetryWithCorrectedInput;
                    outcome = AgentToolInvocationOutcome.Failed;
                    effectState = argumentFailure.EffectState;
                    activity?.SetStatus(ActivityStatusCode.Error, failureMessage);
                    logger?.LogInformation(
                        "Returning sanitized pre-invocation argument failure for tool {ToolName} on agent {AgentId}.",
                        functionName,
                        agentDefinition.Id);
                    return argumentFailure;
                }

                var result = await next(context, cancellationToken);
                var assessment = MafRuntimeToolInvocationResultClassifier.Assess(
                    functionName,
                    classification,
                    result);
                directReceiptExecutionRunId = assessment.DirectReceiptExecutionRunId;
                succeeded = assessment.Succeeded;
                outcome = assessment.Outcome;
                effectState = assessment.EffectState;
                failureCode = assessment.FailureCode;
                canRetryWithCorrectedInput = assessment.CanRetryWithCorrectedInput;
                ApplyCommittedEffectCapture(
                    classification,
                    effectScope,
                    ref effectState,
                    ref effectSourceKind,
                    ref effectSourceId);
                if (classification == ToolInvocationClassification.Mutation &&
                    effectState == AgentToolEffectState.Committed)
                {
                    outcome = AgentToolInvocationOutcome.Succeeded;
                    succeeded = true;
                }

                if (succeeded)
                {
                    if (IsRequiredFinalizerTool(functionName, finalizerPolicy, finalizerMode))
                    {
                        logger?.LogInformation(
                            "Required finalizer tool {ToolName} was captured for agent {AgentId}. Ending the Microsoft Agent Framework turn without waiting for post-finalizer prose.",
                            functionName,
                            agentDefinition.Id);
                        throw new RequiredFinalizerCapturedException(functionName);
                    }
                }
                else
                {
                    failureMessage = assessment.FailureMessage;
                    failureMessageSafeForPersistence = true;
                    activity?.SetStatus(ActivityStatusCode.Error, failureMessage);
                    if (isRequiredFinalizerTool)
                    {
                        logger?.LogWarning(
                            "Required finalizer tool {ToolName} rejected a typed payload for agent {AgentId}. Failure={FailureMessage}",
                            functionName,
                            agentDefinition.Id,
                            failureMessage);
                    }
                }

                return result;
            }
            catch (RequiredFinalizerCapturedException)
            {
                throw;
            }
            catch (Exception exception)
                when (MafAgentToolFailureMapper.TryMap(exception, out var toolFailure))
            {
                failureMessage = $"{toolFailure.ErrorCode}: {toolFailure.Message}";
                failureMessageSafeForPersistence = true;
                failureCode = toolFailure.ErrorCode;
                canRetryWithCorrectedInput = toolFailure.CanRetryWithCorrectedInput;
                outcome = AgentToolInvocationOutcome.Failed;
                effectState = toolFailure.EffectState;
                ApplyCommittedEffectCapture(
                    classification,
                    effectScope,
                    ref effectState,
                    ref effectSourceKind,
                    ref effectSourceId);
                activity?.SetStatus(ActivityStatusCode.Error, failureMessage);
                logger?.LogInformation(
                    "Returning sanitized tool failure {ErrorCode} for tool {ToolName} on governed run {ProcessRunId}, step {ProcessStepId}. CanRetryWithCorrectedInput={CanRetryWithCorrectedInput}",
                    toolFailure.ErrorCode,
                    functionName,
                    policyContext.ProcessRunId,
                    policyContext.ProcessStepId,
                    toolFailure.CanRetryWithCorrectedInput);
                return toolFailure;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                outcome = AgentToolInvocationOutcome.Cancelled;
                failureCode = "ToolInvocationCancelled";
                ApplyCommittedEffectCapture(
                    classification,
                    effectScope,
                    ref effectState,
                    ref effectSourceKind,
                    ref effectSourceId);
                throw;
            }

            catch (Exception exception)
            {
                logger?.LogError(
                    "Unexpected tool invocation failure for {ToolName} on agent {AgentId}. ExceptionType={ExceptionType} InnerExceptionType={InnerExceptionType} StackTrace={StackTrace}",
                    functionName,
                    agentDefinition.Id,
                    exception.GetType().FullName,
                    exception.InnerException?.GetType().FullName ?? string.Empty,
                    exception.StackTrace ?? string.Empty);
                failureMessage = ToolInvocationTraceRecorder.UnexposedFailureMessage;
                failureCode = "ToolInvocationFailed";
                outcome = AgentToolInvocationOutcome.Failed;
                ApplyCommittedEffectCapture(
                    classification,
                    effectScope,
                    ref effectState,
                    ref effectSourceKind,
                    ref effectSourceId);
                activity?.SetStatus(
                    ActivityStatusCode.Error,
                    ToolInvocationTraceRecorder.UnexposedFailureMessage);
                throw new MafToolInvocationBoundaryException(functionName, exception);
            }
            finally
            {
                toolInvocationTraceRecorder.Complete(
                    traceSequence,
                    succeeded,
                    failureMessage,
                    failureMessageSafeForPersistence,
                    directReceiptExecutionRunId,
                    outcome,
                    effectState,
                    failureCode,
                    canRetryWithCorrectedInput,
                    effectSourceKind,
                    effectSourceId);
            }
        });
        builder.UseOpenTelemetry(
            $"{AgentFrameworkTelemetry.SourceName}.Maf.{provider.Kind}",
            telemetry => telemetry.EnableSensitiveData = false);
        return builder.Build();
    }

    internal static void ApplyCommittedEffectCapture(
        ToolInvocationClassification classification,
        AgentToolInvocationEffectScope effectScope,
        ref AgentToolEffectState effectState,
        ref string effectSourceKind,
        ref string effectSourceId)
    {
        if (classification != ToolInvocationClassification.Mutation ||
            effectScope.CommittedEffect is not { } committedEffect)
        {
            return;
        }

        effectState = AgentToolEffectState.Committed;
        effectSourceKind = committedEffect.SourceKind;
        effectSourceId = committedEffect.SourceId;
    }

    private static IReadOnlyDictionary<string, AgentRuntimeToolOwnership> CreateRuntimeToolOwnershipByToolName(
        RuntimeCapabilityState capabilityState)
    {
        if (capabilityState.RuntimeToolMetadata.Count == 0)
        {
            return new Dictionary<string, AgentRuntimeToolOwnership>(StringComparer.OrdinalIgnoreCase);
        }

        var descriptorsByKey = capabilityState.RuntimeToolProviderDescriptors
            .ToDictionary(
                descriptor => descriptor.ProviderKey,
                StringComparer.OrdinalIgnoreCase);
        var ownershipByToolName = new Dictionary<string, AgentRuntimeToolOwnership>(StringComparer.OrdinalIgnoreCase);
        foreach (var metadata in capabilityState.RuntimeToolMetadata)
        {
            descriptorsByKey.TryGetValue(metadata.ProviderKey, out var descriptor);
            ownershipByToolName[metadata.ToolName] = new AgentRuntimeToolOwnership(
                metadata.ProviderKey,
                descriptor?.DisplayName ?? metadata.ProviderKey,
                metadata.ToolName);
        }

        return ownershipByToolName;
    }

    private static bool IsRequiredFinalizerTool(
        string functionName,
        AgentFinalizerPolicy? finalizerPolicy,
        AgentFinalizerMode finalizerMode)
    {
        return finalizerMode == AgentFinalizerMode.Required &&
               finalizerPolicy is { IsRequired: true } &&
               string.Equals(functionName, finalizerPolicy.ToolName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Removes composed tools whose operation class exceeds the admitted
    /// execution governance snapshot. Composition may only narrow: a read-only
    /// authority never exposes mutation-classified tools to the model, and an
    /// authority without read access exposes no workspace tools at all. The
    /// invocation policy independently re-enforces the same snapshot at call
    /// time.
    /// </summary>
    internal static async Task FilterToolsOutsideExecutionGovernanceAsync(
        RuntimeCapabilityState capabilityState,
        AgentExecutionGovernanceSnapshot? governance,
        Func<ExecutionState, string, string, Task> progressCallback)
    {
        if (governance is null || (governance.MutationAllowed && governance.ReadAllowed))
        {
            return;
        }

        var removedTools = capabilityState.Tools
            .Where(tool => !string.IsNullOrWhiteSpace(tool.Name))
            .Where(tool =>
            {
                var classification = AgentToolInvocationPolicyMetadata.Classify(tool.Name);
                if (!governance.MutationAllowed && classification == ToolInvocationClassification.Mutation)
                {
                    return true;
                }

                return !governance.ReadAllowed &&
                       classification is ToolInvocationClassification.Read or ToolInvocationClassification.Mutation;
            })
            .ToList();
        if (removedTools.Count == 0)
        {
            return;
        }

        capabilityState.Tools.RemoveAll(removedTools.Contains);
        capabilityState.HasApprovalTools = capabilityState.Tools.Any(tool => tool is ApprovalRequiredAIFunction);
        var removedToolNames = string.Join(
            ", ",
            removedTools
                .Select(tool => tool.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
        var authorityDescription = governance.ReadAllowed
            ? "read-only authority"
            : "authority without workspace access";
        await progressCallback(
            ExecutionState.Preparing,
            "Execution authority",
            $"Excluded tool(s) outside the admitted execution authority ({authorityDescription}, scope '{governance.WorkspaceScope.DisplayName}'): {removedToolNames}.");
    }

    private async Task FilterUnusableApprovalToolsAsync(
        RuntimeCapabilityState capabilityState,
        ProviderProfile provider,
        bool suppressApprovalRequirements,
        Func<ExecutionState, string, string, Task> progressCallback)
    {
        if (suppressApprovalRequirements)
        {
            capabilityState.HasApprovalTools = capabilityState.Tools.Any(tool => tool is ApprovalRequiredAIFunction);
            return;
        }

        var featureMatrix = ProviderFeatureService.ResolveFeatureMatrix(provider);
        if (featureMatrix.SupportsApprovalRequiredAIFunction)
        {
            return;
        }

        var unusableMutationTools = capabilityState.Tools
            .Where(tool => tool is ApprovalRequiredAIFunction)
            .Where(tool => AgentToolInvocationPolicyMetadata.Classify(tool.Name) == ToolInvocationClassification.Mutation)
            .ToList();
        if (unusableMutationTools.Count == 0)
        {
            return;
        }

        var toolNames = unusableMutationTools
            .Select(tool => tool.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var toolList = string.Join(", ", toolNames);
        if (RuntimeCapabilityComposer.IsGovernedProcessAutomationRun())
        {
            throw new InvalidOperationException(
                $"Provider '{provider.Name}' using transport '{provider.Transport}' cannot expose mutation tools that require MAF approval because no effective approval path is available. Unusable tools: {toolList}.");
        }

        capabilityState.Tools.RemoveAll(unusableMutationTools.Contains);
        capabilityState.HasApprovalTools = capabilityState.Tools.Any(tool => tool is ApprovalRequiredAIFunction);
        await progressCallback(
            ExecutionState.Preparing,
            "Approval policy",
            $"Omitted mutation tool(s) that require MAF approval because provider '{provider.Name}' using transport '{provider.Transport}' has no effective approval path: {toolList}.");
    }

    private static string AppendFinalizerInstructions(
        string instructions,
        AgentFinalizerPolicy? finalizerPolicy,
        AgentFinalizerMode finalizerMode,
        bool hasStructuredResponseFormat)
    {
        if (finalizerPolicy is null || finalizerMode == AgentFinalizerMode.Disabled)
        {
            return instructions;
        }

        var header = finalizerMode == AgentFinalizerMode.Shadow
            ? "Finalizer tool shadow policy:"
            : "Finalizer tool policy:";
        var finalizerInstructions = finalizerMode == AgentFinalizerMode.Shadow
            ? $"{Environment.NewLine}{Environment.NewLine}{header}{Environment.NewLine}" +
              $"- You may call `{finalizerPolicy.ToolName}` at most once before finishing to produce a comparison copy.{Environment.NewLine}" +
              (hasStructuredResponseFormat
                  ? $"- The final assistant response JSON is the source of truth. Return exactly one JSON object matching `{finalizerPolicy.OutputContract.ContractKey}` through the configured structured response format.{Environment.NewLine}" +
                    "- Do not use Markdown, prose, code fences, or any extra text around the JSON object."
                  : "- The final assistant response remains the source of truth because no structured response format is attached.")
            : $"{Environment.NewLine}{Environment.NewLine}{header}{Environment.NewLine}" +
              $"- Call `{finalizerPolicy.ToolName}` exactly once after all other significant tool work is complete.{Environment.NewLine}" +
              "- A normal assistant response without that finalizer tool is invalid for this run and will fail the execution even if the work itself succeeded." + Environment.NewLine +
              $"- The finalizer arguments are the authoritative machine output for `{finalizerPolicy.OutputContract.ContractKey}`.{Environment.NewLine}" +
              MafFinalizerDriver.BuildRequiredFinalizerArgumentInstructions(finalizerPolicy) +
              (hasStructuredResponseFormat
                  ? $"- After the tool call, return exactly one JSON object matching the same `{finalizerPolicy.OutputContract.ContractKey}` schema through the configured structured response format.{Environment.NewLine}" +
                    "- Do not use Markdown, prose, code fences, or any extra text around the JSON object."
                  : "- Do not emit separate machine output after the finalizer call.") + Environment.NewLine +
              "- Do not call any other `submit_*` finalizer tool for this contract.";
        return string.IsNullOrWhiteSpace(instructions)
            ? finalizerInstructions.Trim()
            : instructions.TrimEnd() + finalizerInstructions;
    }

    private static ChatHistoryProvider CreateChatHistoryProvider()
    {
        return new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions
        {
            StorageInputRequestMessageFilter = messages => messages.Where(message =>
                message.GetAgentRequestMessageSourceType() != AgentRequestMessageSourceType.AIContextProvider &&
                message.GetAgentRequestMessageSourceType() != AgentRequestMessageSourceType.ChatHistory)
                .ToList()
        });
    }

    private string ResolveOpenAiCredentialOverride(ProviderProfile provider)
        => providerCredentialService.ResolveOpenAiCredentialOverride(provider);

}
