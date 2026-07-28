using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafRuntimeAgentFactory
{
    private static readonly ProviderProfileService ProviderFeatureService = new();
    private static readonly JsonSerializerOptions LoggingJsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly string workspaceRoot;
    private readonly IServiceProvider services;
    private readonly WorkspaceScopeDescriptor workspaceScope;
    private readonly IMafProviderCredentialService providerCredentialService;
    private readonly IMafProviderAgentFactory providerAgentFactory;
    private readonly IRuntimeCapabilityComposer runtimeCapabilityComposer;
    private readonly MafScriptPolicyInspectionService scriptPolicyInspectionService;

    public MafRuntimeAgentFactory(
        string workspaceRoot,
        IServiceProvider services,
        WorkspaceScopeDescriptor workspaceScope,
        IMafProviderCredentialService providerCredentialService,
        IMafProviderAgentFactory providerAgentFactory,
        IRuntimeCapabilityComposer runtimeCapabilityComposer)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root must be provided.", nameof(workspaceRoot));
        }

        this.workspaceRoot = workspaceRoot;
        this.services = services ?? throw new ArgumentNullException(nameof(services));
        this.workspaceScope = workspaceScope;
        this.providerCredentialService = providerCredentialService ?? throw new ArgumentNullException(nameof(providerCredentialService));
        this.providerAgentFactory = providerAgentFactory ?? throw new ArgumentNullException(nameof(providerAgentFactory));
        this.runtimeCapabilityComposer = runtimeCapabilityComposer ?? throw new ArgumentNullException(nameof(runtimeCapabilityComposer));
        scriptPolicyInspectionService = new MafScriptPolicyInspectionService(this.workspaceRoot, workspaceScope);
    }

    public async Task<AIAgent> CreateHostedAgentAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
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
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements = false,
        bool forceOmitTemperature = false,
        AgentRuntimeExecutionOptions? executionOptions = null,
        string runtimeSessionKey = "")
    {
        var runtimeOptions = executionOptions ?? MafRuntimeExecutionOptionsResolver.CreateDisabled(null);
        if (runtimeOptions.Handoff is not null)
        {
            return await CreateHandoffRuntimeBuildAsync(
                agent,
                provider,
                progressCallback,
                cancellationToken,
                suppressApprovalRequirements,
                forceOmitTemperature,
                runtimeOptions,
                runtimeSessionKey);
        }

        var toolInvocationTraceRecorder = new ToolInvocationTraceRecorder();
        var openAiCredentialOverride = ResolveOpenAiCredentialOverride(provider);
        var effectiveProvider = ManagedSeedProviderFallbacks.Apply(agent, provider, openAiCredentialOverride);
        PromoteResolvedProviderCredentialEnvironment(effectiveProvider);
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

        if (MafModelParametersBuilder.IsReasoningEffortConfiguredButTransportUnsupported(effectiveProvider, model, agent.ConfigurationJson))
        {
            throw new InvalidOperationException(MafModelParametersBuilder.BuildReasoningEffortUnsupportedTransportMessage(effectiveProvider, model));
        }

        MafRuntimeExecutionOptionsResolver.EnsureStructuredOutputCapability(effectiveProvider, runtimeOptions);
        var finalizerCapture = MafFinalizerToolFactory.CreateCapture(
            runtimeOptions.StructuredOutput,
            runtimeOptions.FinalizerMode);
        var capabilityState = await runtimeCapabilityComposer.CreateCapabilityStateCoreAsync(
            agent,
            effectiveProvider,
            model,
            capabilities,
            memory,
            progressCallback,
            cancellationToken,
            suppressApprovalRequirements,
            runtimeOptions.ContextWorkspaceScope ?? workspaceScope,
            runtimeOptions.ContextIntent ?? AgentRuntimeContextIntent.Empty,
            runtimeSessionKey,
            runtimeOptions.TransientContext?.Attachments);
        await FilterUnusableApprovalToolsAsync(
            capabilityState,
            effectiveProvider,
            suppressApprovalRequirements,
            progressCallback);
        if (runtimeOptions.TransientContext is not null)
        {
            capabilityState.ContextProviders.Add(
                new StaticMessageContextProvider(
                    new ChatMessage(
                        ChatRole.User,
                        runtimeOptions.TransientContext.Content),
                    StaticMessageContextProvider.TransientAgentChatStateKey));
        }

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
        chatOptions.AllowMultipleToolCalls = MafFinalizerDriver.ShouldAllowMultipleToolCalls(
            runtimeOptions.FinalizerMode,
            capabilityState.HasApprovalTools);

        if (capabilityState.Tools.Count > 0)
        {
            chatOptions.Tools = [.. capabilityState.Tools];
        }

        var options = new ChatClientAgentOptions
        {
            Id = agent.Id.ToString("D"),
            Name = agent.Name,
            Description = agent.Summary,
            ChatOptions = chatOptions,
            AIContextProviders = capabilityState.ContextProviders,
            ChatHistoryProvider = frameworkManagedHistory ? CreateChatHistoryProvider() : null,
            RequirePerServiceCallChatHistoryPersistence = agent.RequirePerServiceCallChatHistoryPersistence
        };

        var runtimeAgent = CreateInstrumentedAgent(
            providerAgentFactory.CreateFrameworkAgent(effectiveProvider, model, options, frameworkManagedHistory, services),
            effectiveProvider,
            agent,
            capabilityState,
            suppressApprovalRequirements,
            toolInvocationTraceRecorder,
            finalizerCapture?.Policy,
            runtimeOptions.FinalizerMode);
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
            runtimeCapabilityState: capabilityState);
    }

    private async Task<RuntimeBuildResult> CreateHandoffRuntimeBuildAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements,
        bool forceOmitTemperature,
        AgentRuntimeExecutionOptions runtimeOptions,
        string runtimeSessionKey)
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
                    progressCallback,
                    cancellationToken,
                    suppressApprovalRequirements,
                    forceOmitTemperature,
                    effectiveParticipantOptions,
                    runtimeSessionKey);
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

            return new RuntimeBuildResult(
                buildResult.Agent,
                entryBuild.Provider,
                entryBuild.Model,
                participantBuilds,
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
                    .ToList());
        }
        catch
        {
            foreach (var participantBuild in participantBuilds)
            {
                await participantBuild.DisposeAsync();
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
        AgentFinalizerMode finalizerMode)
    {
        var builder = agent.AsBuilder();
        var toolPolicy = new DefaultAgentToolInvocationPolicy();
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
        var logger = services.GetService<ILogger<MafAgentRuntime>>();
        var configuredWorkspaceAccess = AgentWorkspaceToolAccessMetadata.Read(agentDefinition.ConfigurationJson);
        builder.UseLogging(
            services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance,
            logging => logging.JsonSerializerOptions = LoggingJsonSerializerOptions);
        builder.Use(async (innerAgent, context, next, cancellationToken) =>
        {
            var functionName = context.Function?.Name ?? "unknown";
            var invocationArguments = MafScriptPolicyInspectionService.ResolveFunctionInvocationArguments(context).ToArray();
            var redactedArguments = AgentToolInvocationPolicyMetadata.RedactArguments(functionName, invocationArguments);
            var isRequiredFinalizerTool = IsRequiredFinalizerTool(functionName, finalizerPolicy, finalizerMode);
            var classification = isRequiredFinalizerTool
                ? ToolInvocationClassification.Read
                : AgentToolInvocationPolicyMetadata.Classify(functionName);
            var auditScope = WorkspaceExecutionAuditContext.Current;
            var hasRunScopedExternalTargets = auditScope is not null &&
                                              (auditScope.AllowedExternalTargetAliases.Count > 0 ||
                                               auditScope.ReadOnlyExternalTargetAliases.Count > 0);
            var allowedExternalTargetAliases = hasRunScopedExternalTargets
                ? auditScope!.AllowedExternalTargetAliases
                : configuredWorkspaceAccess.AllowedExternalTargetAliases;
            var readOnlyExternalTargetAliases = hasRunScopedExternalTargets
                ? auditScope!.ReadOnlyExternalTargetAliases
                : [];
            var scriptSideEffectManifestJson = MafScriptPolicyInspectionService.TryGetStringArgument(
                invocationArguments,
                GovernedScriptSideEffectManifest.ArgumentName) ?? string.Empty;
            var scriptInspection = scriptPolicyInspectionService.ResolveScriptContentInspectionForPolicy(
                functionName,
                invocationArguments,
                auditScope,
                scriptSideEffectManifestJson);
            var policyContext = new ToolInvocationPolicyContext(
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
                ProcessRunId: auditScope?.ProcessRunId ?? string.Empty,
                ProcessStepId: auditScope?.ProcessStepId ?? string.Empty,
                AllowedExternalTargetAliases: allowedExternalTargetAliases,
                ReadOnlyExternalTargetAliases: readOnlyExternalTargetAliases,
                ApprovalWrapperEffectiveForProvider: featureMatrix.SupportsApprovalRequiredAIFunction,
                ApplicationApprovalAvailable: false,
                ProcessAllowsProductMutation: auditScope?.ProcessAllowsProductMutation != false,
                ProcessRequiresProductMutationBeforeManagedOutput:
                    auditScope?.ProcessRequiresProductMutationBeforeManagedOutput == true,
                ProcessProductMutationToolNames: auditScope?.ProcessProductMutationToolNames ?? [],
                ProcessProductMutationRequiredBranchOutcomeKeys:
                    auditScope?.ProcessProductMutationRequiredBranchOutcomeKeys ?? [],
                ProcessStepAllowedOperations: auditScope?.ProcessStepAllowedOperations ?? [],
                ProcessStepTargetScope: auditScope?.ProcessStepTargetScope ?? string.Empty,
                ContextWorkspaceScopeKind: auditScope?.ContextWorkspaceScope?.Kind.ToString() ?? string.Empty,
                ContextWorkspaceScopeKey: auditScope?.ContextWorkspaceScope?.Key ?? string.Empty,
                InspectedScriptContent: scriptInspection.Content,
                ScriptInspectionFailure: scriptInspection.FailureMessage,
                ScriptSideEffectManifestJson: scriptSideEffectManifestJson,
                ToolInvocationTraces: toolInvocationTraceRecorder.Snapshot())
            {
                SourceId = auditScope?.SourceId ?? string.Empty,
                AllowedManagedArtifactReadRefs = auditScope?.AllowedManagedArtifactReadRefs ?? []
            };
            var policyDecision = await toolPolicy.EvaluateAsync(policyContext, cancellationToken);
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

            var traceSequence = toolInvocationTraceRecorder.Start(functionName, classification, policyDecision.Signature, runtimeToolOwnership);
            var succeeded = false;
            var failureMessage = string.Empty;
            using var runtimeToolOwnershipScope = AgentRuntimeToolOwnershipContext.BeginScope(runtimeToolOwnership);
            try
            {
                if (AgentToolPolicyBlockGuard.TryCreateRecoverableDeniedResult(
                        functionName,
                        policyDecision,
                        policyContext,
                        out var policyDeniedResult))
                {
                    failureMessage = policyDeniedResult;
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

                var result = await next(context, cancellationToken);
                succeeded = MafRuntimeToolInvocationResultClassifier.IsSuccessful(result);
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
                    failureMessage = MafRuntimeToolInvocationResultClassifier.ResolveFailureMessage(result);
                    activity?.SetStatus(ActivityStatusCode.Error, failureMessage);
                }

                return result;
            }
            catch (RequiredFinalizerCapturedException)
            {
                throw;
            }
            catch (Exception exception)
            {
                failureMessage = exception.Message;
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                throw;
            }
            finally
            {
                toolInvocationTraceRecorder.Complete(traceSequence, succeeded, failureMessage);
            }
        });
        builder.UseOpenTelemetry(
            $"{AgentFrameworkTelemetry.SourceName}.Maf.{provider.Kind}",
            telemetry => telemetry.EnableSensitiveData = false);
        return builder.Build(services);
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

    private void PromoteResolvedProviderCredentialEnvironment(
        ProviderProfile provider)
        => providerCredentialService.PromoteResolvedProviderCredentialEnvironment(provider);

}
