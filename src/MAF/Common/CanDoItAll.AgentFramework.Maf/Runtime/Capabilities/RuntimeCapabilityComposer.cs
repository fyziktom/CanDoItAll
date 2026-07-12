using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Access;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Tools.Documents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CapabilityAccessPolicyEvaluatorContract = CanDoItAll.AgentFramework.Capabilities.Abstractions.ICapabilityAccessPolicyEvaluator;

namespace CanDoItAll.AgentFramework.Maf;

internal interface IRuntimeCapabilityComposer
{
    Task<RuntimeCapabilityState> CreateCapabilityStateAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements = false);

    Task<RuntimeCapabilityState> CreateCapabilityStateCoreAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        string model,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements,
        WorkspaceScopeDescriptor contextWorkspaceScope,
        AgentRuntimeContextIntent contextIntent,
        string runtimeSessionKey = "");
}

internal sealed class RuntimeCapabilityComposer : IRuntimeCapabilityComposer
{
    private const int DefaultCompactionSlidingWindowTurns = 32;
    private const int DefaultCompactionTruncationTokenLimit = 64000;
    private const int DefaultToolCompactionMessageThreshold = 40;
    private const string OpenAiApiKeyEnvironmentVariable = "OPENAI_API_KEY";

    private static readonly ProviderProfileService ProviderFeatureService = new();

    private readonly string workspaceRoot;
    private readonly IServiceProvider services;
    private readonly WorkspaceScopeDescriptor workspaceScope;
    private readonly IMafRuntimeDependencyResolver dependencyResolver;
    private readonly IMafProviderCredentialService providerCredentialService;
    private readonly IAgentImageAnalysisService imageAnalysisService;
    private readonly IRuntimeToolProviderComposer runtimeToolProviderComposer;
    private readonly IMafRuntimeCompositionMetrics compositionMetrics;
    private readonly RuntimeCapabilityDescriptorCatalog descriptorCatalog;
    private readonly RuntimeCapabilityAccessPlanner capabilityAccessPlanner;
    private readonly RuntimeRegisteredToolProviderAttacher registeredToolProviderAttacher;

    public RuntimeCapabilityComposer(
        string workspaceRoot,
        IServiceProvider services,
        WorkspaceScopeDescriptor workspaceScope,
        IMafRuntimeDependencyResolver dependencyResolver,
        IMafProviderCredentialService providerCredentialService,
        IAgentImageAnalysisService imageAnalysisService,
        IRuntimeToolProviderComposer runtimeToolProviderComposer,
        IMafRuntimeCompositionMetrics compositionMetrics)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root must be provided.", nameof(workspaceRoot));
        }

        this.workspaceRoot = Path.GetFullPath(workspaceRoot);
        this.services = services ?? throw new ArgumentNullException(nameof(services));
        this.workspaceScope = workspaceScope;
        this.dependencyResolver = dependencyResolver ?? throw new ArgumentNullException(nameof(dependencyResolver));
        this.providerCredentialService = providerCredentialService ?? throw new ArgumentNullException(nameof(providerCredentialService));
        this.imageAnalysisService = imageAnalysisService ?? throw new ArgumentNullException(nameof(imageAnalysisService));
        this.runtimeToolProviderComposer = runtimeToolProviderComposer ?? throw new ArgumentNullException(nameof(runtimeToolProviderComposer));
        this.compositionMetrics = compositionMetrics ?? throw new ArgumentNullException(nameof(compositionMetrics));
        var capabilityAccessPolicyEvaluator = services.GetService(typeof(CapabilityAccessPolicyEvaluatorContract)) as CapabilityAccessPolicyEvaluatorContract
            ?? new CapabilityAccessPolicyEvaluator();
        descriptorCatalog = new RuntimeCapabilityDescriptorCatalog();
        capabilityAccessPlanner = new RuntimeCapabilityAccessPlanner(
            descriptorCatalog,
            capabilityAccessPolicyEvaluator);
        registeredToolProviderAttacher = new RuntimeRegisteredToolProviderAttacher(runtimeToolProviderComposer);
    }

    public static RuntimeCapabilityComposer CreateDefault(
        string workspaceRoot,
        IServiceProvider services,
        WorkspaceScopeDescriptor? workspaceScope = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var effectiveWorkspaceScope = workspaceScope ?? WorkspaceScopeDescriptor.Sandbox;
        var dependencyResolver = MafRuntimeDependencyResolver.Resolve(services);
        var providerCredentialService = services.GetService(typeof(IMafProviderCredentialService)) is IMafProviderCredentialService resolvedCredentialService
            ? resolvedCredentialService
            : new MafProviderCredentialService(services);
        var providerDependencies = dependencyResolver.ResolveProviderDependencies(services);
        var runtimeToolProviderComposer = services.GetService(typeof(IRuntimeToolProviderComposer)) is IRuntimeToolProviderComposer resolvedComposer
            ? resolvedComposer
            : RuntimeToolProviderComposer.Default;
        var compositionMetrics = services.GetService(typeof(IMafRuntimeCompositionMetrics)) is IMafRuntimeCompositionMetrics resolvedMetrics
            ? resolvedMetrics
            : NoOpMafRuntimeCompositionMetrics.Instance;

        return new RuntimeCapabilityComposer(
            workspaceRoot,
            services,
            effectiveWorkspaceScope,
            dependencyResolver,
            providerCredentialService,
            providerDependencies.ImageAnalysisService,
            runtimeToolProviderComposer,
            compositionMetrics);
    }

    public async Task<RuntimeCapabilityState> CreateCapabilityStateAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements = false)
    {
        var model = MafModelParametersBuilder.ResolveRuntimeModel(agent, provider);
        return await CreateCapabilityStateCoreAsync(
            agent,
            provider,
            model,
            capabilities,
            memory,
            progressCallback,
            cancellationToken,
            suppressApprovalRequirements,
            workspaceScope,
            AgentRuntimeContextIntent.Empty,
            runtimeSessionKey: string.Empty);
    }

    internal RuntimeCapabilityAccessPlan CreateRuntimeCapabilityAccessPlan(
        AgentDefinition agent,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        AgentWorkspaceToolAccessSettings workspaceToolAccess,
        AgentRuntimeContextIntent contextIntent,
        bool storageToolsAvailable)
        => capabilityAccessPlanner.CreateRuntimeCapabilityAccessPlan(
            agent,
            capabilities,
            workspaceToolAccess,
            contextIntent,
            storageToolsAvailable);

    public async Task<RuntimeCapabilityState> CreateCapabilityStateCoreAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        string model,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements,
        WorkspaceScopeDescriptor contextWorkspaceScope,
        AgentRuntimeContextIntent contextIntent,
        string runtimeSessionKey = "")
    {
        var totalStopwatch = Stopwatch.StartNew();
        try
        {
            var agentConfiguration = MafRuntimeJson.DeserializeConfiguration<AgentRuntimeConfiguration>(agent.ConfigurationJson) ?? new AgentRuntimeConfiguration();
            var workspaceToolAccess = ResolveWorkspaceToolAccessForRuntime(agent);
            var storageToolsAvailable = HasStorageRuntimePluginServices();
            var capabilityAccessPlan = TrackResult(
                "capability.access-plan",
                () => capabilityAccessPlanner.CreateRuntimeCapabilityAccessPlan(
                    agent,
                    capabilities,
                    workspaceToolAccess,
                    contextIntent,
                    storageToolsAvailable));
            var effectiveCapabilities = capabilityAccessPlan.AllowedCatalogCapabilities;
            var composition = TrackResult(
                "capability.composition",
                () => CreateCapabilityComposition(
                    agent,
                    provider,
                    model,
                    effectiveCapabilities,
                    contextIntent,
                    agentConfiguration,
                    workspaceToolAccess,
                    capabilityAccessPlan));

            TrackAction(
                "capability.initial-access-state",
                () => RuntimeCapabilityAccessPlanner.AttachInitialCapabilityAccessState(composition.State, capabilityAccessPlan));

            await TrackAsync(
                "capability.workspace-memory",
                () => AttachWorkspaceMemoryAsync(composition, agent, memory, progressCallback));
            await TrackAsync(
                "capability.context-contributors",
                () => AttachContextContributorsAsync(
                    composition,
                    agent,
                    provider,
                    progressCallback,
                    suppressApprovalRequirements,
                    contextWorkspaceScope,
                    contextIntent));
            await TrackAsync(
                "capability.skills",
                () => AttachSkillsAsync(composition, effectiveCapabilities, progressCallback, suppressApprovalRequirements));
            await TrackAsync(
                "capability.configured-workspace-tools",
                () => AttachConfiguredWorkspaceToolsAsync(composition, agent, progressCallback, suppressApprovalRequirements));
            await TrackAsync(
                "capability.runtime-tool-providers",
                () => registeredToolProviderAttacher.AttachAsync(
                    composition,
                    agent,
                    provider,
                    effectiveCapabilities,
                    progressCallback,
                    cancellationToken,
                    suppressApprovalRequirements,
                    ResolveContextPolicyKind(agent, suppressApprovalRequirements),
                    contextWorkspaceScope,
                    contextIntent,
                    runtimeSessionKey));
            await TrackAsync(
                "capability.a2a-tools",
                () => AttachA2ARemoteAgentToolsAsync(composition, agent, progressCallback, cancellationToken, suppressApprovalRequirements));
            await TrackAsync(
                "capability.catalog-capabilities",
                () => AttachCatalogCapabilitiesAsync(
                    composition,
                    agent,
                    provider,
                    effectiveCapabilities,
                    progressCallback,
                    cancellationToken,
                    suppressApprovalRequirements));
            await TrackAsync(
                "capability.compaction",
                () => AttachCompactionAsync(composition, agent, progressCallback, suppressApprovalRequirements));

            TrackAction("capability.deduplicate-tools", () => DeduplicateTools(composition.State.Tools));
            return composition.State;
        }
        finally
        {
            RecordCompositionMetric("capability.total", totalStopwatch.Elapsed);
        }

        async Task TrackAsync(string stage, Func<Task> action)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await action();
            }
            finally
            {
                RecordCompositionMetric(stage, stopwatch.Elapsed);
            }
        }

        void TrackAction(string stage, Action action)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                action();
            }
            finally
            {
                RecordCompositionMetric(stage, stopwatch.Elapsed);
            }
        }

        TResult TrackResult<TResult>(string stage, Func<TResult> action)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                return action();
            }
            finally
            {
                RecordCompositionMetric(stage, stopwatch.Elapsed);
            }
        }
    }

    private RuntimeCapabilityComposition CreateCapabilityComposition(
        AgentDefinition agent,
        ProviderProfile provider,
        string model,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        AgentRuntimeContextIntent contextIntent,
        AgentRuntimeConfiguration agentConfiguration,
        AgentWorkspaceToolAccessSettings workspaceToolAccess,
        RuntimeCapabilityAccessPlan capabilityAccessPlan)
    {
        var workspaceServices = dependencyResolver.ResolveWorkspaceServices(services, workspaceRoot, workspaceScope);
        var effectiveWorkspaceScope = contextIntent.WorkspaceScope ?? workspaceScope;
        var filesystemPlugin = new WorkspaceFilesystemRuntimePlugin(
            workspaceServices.FileService,
            workspaceRoot,
            effectiveWorkspaceScope,
            workspaceToolAccess);
        var workspacePlugin = new WorkspaceRuntimePlugin(
            workspaceServices.CommandExecutionService,
            workspaceServices.ArtifactToolService,
            workspaceRoot,
            effectiveWorkspaceScope,
            workspaceToolAccess,
            provider,
            model,
            imageAnalysisService);
        var spreadsheetPlugin = new WorkspaceSpreadsheetRuntimePlugin(
            ResolveSpreadsheetDocumentService(),
            workspaceRoot,
            effectiveWorkspaceScope,
            workspaceToolAccess);
        var storagePlugin = CreateStorageRuntimePlugin(workspaceToolAccess);
        var skillBuilder = new SkillCapabilityBuilder(workspaceRoot, services);
        var contextBuilder = new ContextCapabilityBuilder(workspaceRoot);
        var contextContributors = services.GetServices<IAgentContextContributor>().ToList();
        var runtimeToolProviders = runtimeToolProviderComposer.ComposeRegistrations(
            services.GetServices<IAgentRuntimeToolProvider>());
        var mcpBuilder = new McpCapabilityBuilder(
            services,
            workspaceRoot,
            workspaceScope,
            dependencyResolver,
            providerCredentialService,
            descriptorCatalog.ResolveCatalogOperationClassifications,
            RuntimeCapabilityDescriptorCatalog.ResolveMcpServerKey,
            RuntimeCapabilityDescriptorCatalog.ResolveCapabilityDisplayName,
            RuntimeCapabilityDescriptorCatalog.ResolveCapabilityDescription,
            RuntimeCapabilityDescriptorCatalog.ResolveMcpAllowedTools,
            RuntimeCapabilityDescriptorCatalog.ResolveMcpApprovalMode,
            RuntimeCapabilityDescriptorCatalog.ResolveMcpTimeout,
            RuntimeCapabilityDescriptorCatalog.ResolveCatalogTags,
            RuntimeCapabilityDescriptorCatalog.ResolveMcpMessageFraming);
        var fileSkillExecutionPolicies = skillBuilder.ResolveScriptExecutionPolicies(capabilities);
        var toolBuilder = new ToolCapabilityBuilder(
            services,
            workspaceRoot,
            workspaceScope,
            providerCredentialService,
            filesystemPlugin,
            workspacePlugin,
            spreadsheetPlugin,
            storagePlugin,
            workspaceServices.CommandExecutionService,
            workspaceToolAccess,
            fileSkillExecutionPolicies,
            capabilityAccessPlan);

        return new RuntimeCapabilityComposition(
            new RuntimeCapabilityState(),
            agentConfiguration,
            skillBuilder,
            contextBuilder,
            contextContributors,
            runtimeToolProviders,
            mcpBuilder,
            toolBuilder,
            capabilityAccessPlan);
    }

    private bool HasStorageRuntimePluginServices()
    {
        return services.GetService(typeof(IStorageCatalogService)) is not null &&
               services.GetService(typeof(IStorageDriverRegistry)) is not null;
    }

    private ISpreadsheetDocumentService ResolveSpreadsheetDocumentService()
        => services.GetService(typeof(ISpreadsheetDocumentService)) is ISpreadsheetDocumentService resolved
            ? resolved
            : new ClosedXmlSpreadsheetDocumentService();

    private void RecordCompositionMetric(string stage, TimeSpan elapsed)
    {
        compositionMetrics.Record(new MafRuntimeCompositionMeasurement(
            stage,
            elapsed,
            nameof(RuntimeCapabilityComposer)));
    }

    private static AgentWorkspaceToolAccessSettings ResolveWorkspaceToolAccessForRuntime(AgentDefinition agent)
    {
        var configured = AgentWorkspaceToolAccessMetadata.Read(agent.ConfigurationJson);
        var overrideProfile = WorkspaceExecutionAuditContext.Current?.WorkspaceToolProfileOverride;
        if (!overrideProfile.HasValue)
        {
            return AgentWorkspaceToolAccessMetadata.Normalize(configured);
        }

        var processProfile = AgentWorkspaceToolAccessProfiles.CreateSettings(overrideProfile.Value);
        processProfile.AllowedExternalTargetAliases = configured.AllowedExternalTargetAliases.ToList();
        processProfile.CanReadStorage = configured.CanReadStorage;
        processProfile.CanWriteStorage = configured.CanWriteStorage;
        processProfile.AllowAllStorageCatalogs = configured.AllowAllStorageCatalogs;
        processProfile.AllowedStorageCatalogIds = configured.AllowedStorageCatalogIds.ToList();

        return AgentWorkspaceToolAccessMetadata.Normalize(processProfile);
    }

    private StorageRuntimePlugin? CreateStorageRuntimePlugin(AgentWorkspaceToolAccessSettings accessSettings)
    {
        var catalogService = services.GetService(typeof(IStorageCatalogService)) as IStorageCatalogService;
        var driverRegistry = services.GetService(typeof(IStorageDriverRegistry)) as IStorageDriverRegistry;
        return catalogService is null || driverRegistry is null
            ? null
            : new StorageRuntimePlugin(catalogService, driverRegistry, accessSettings);
    }

    private Task AttachWorkspaceMemoryAsync(
        RuntimeCapabilityComposition composition,
        AgentDefinition agent,
        IReadOnlyList<AgentMemoryRecord> memory,
        Func<ExecutionState, string, string, Task> progressCallback)
    {
        if (memory.Count == 0)
        {
            return Task.CompletedTask;
        }

        var memoryAccess = AgentMemoryAccessMetadata.Read(agent.ConfigurationJson);
        composition.State.ContextSources.Add(AgentRuntimeContextManifestSource.Excluded(
            AgentRuntimeContextSourceCategories.Memory,
            "workspace-memory",
            memoryAccess.InvocationMode == AgentMemoryInvocationMode.Disabled
                ? "agent memory invocation is disabled; legacy workspace memory injection is not permitted"
                : "generic agent memory provider invocation owns memory context for this run"));
        return Task.CompletedTask;
    }

    private async Task AttachContextContributorsAsync(
        RuntimeCapabilityComposition composition,
        AgentDefinition agent,
        ProviderProfile provider,
        Func<ExecutionState, string, string, Task> progressCallback,
        bool suppressApprovalRequirements,
        WorkspaceScopeDescriptor contextWorkspaceScope,
        AgentRuntimeContextIntent contextIntent)
    {
        if (composition.ContextContributors.Count == 0)
        {
            return;
        }

        var duplicateContributorIds = composition.ContextContributors
            .GroupBy(contributor => contributor.Descriptor.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.Value)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        if (duplicateContributorIds.Count > 0)
        {
            throw new InvalidOperationException($"Agent context contributor id(s) must be unique: {string.Join(", ", duplicateContributorIds)}.");
        }

        var enabledContributors = composition.ContextContributors
            .Where(contributor => contributor.Descriptor.Enabled)
            .OrderBy(contributor => contributor.Descriptor.Order)
            .ThenBy(contributor => contributor.Descriptor.Id.Value, StringComparer.Ordinal)
            .ToList();
        if (enabledContributors.Count == 0)
        {
            return;
        }

        var policy = new AgentContextContributionPolicy(
            MapContextContributionExecutionMode(ResolveContextPolicyKind(agent, suppressApprovalRequirements)),
            suppressApprovalRequirements,
            contextWorkspaceScope);

        foreach (var contributor in enabledContributors)
        {
            composition.State.ContextSources.Add(AgentRuntimeContextManifestSource.Included(
                AgentRuntimeContextSourceCategories.ContextContributor,
                contributor.Descriptor.Id.Value,
                "enabled registered context contributor",
                1));
            composition.State.ContextProviders.Add(new MafAgentContextContributionProvider(
                contributor,
                agent,
                provider,
                policy,
                contextIntent,
                composition.State.ContextContributionTraceCollector));
        }

        await progressCallback(
            ExecutionState.Preparing,
            "Context contributors",
            $"Attached {enabledContributors.Count} registered agent context contributor(s) in deterministic order.");
    }

    private async Task AttachSkillsAsync(
        RuntimeCapabilityComposition composition,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        Func<ExecutionState, string, string, Task> progressCallback,
        bool suppressApprovalRequirements)
    {
        var skillRoots = composition.SkillBuilder.ResolveSkillRoots(capabilities, composition.AgentConfiguration);
        var inlineSkills = composition.SkillBuilder.ResolveInlineSkills(capabilities);
        var serviceSkills = composition.SkillBuilder.ResolveRegisteredSkills(capabilities);

        if (skillRoots.Count == 0 && inlineSkills.Count == 0 && serviceSkills.Count == 0)
        {
            return;
        }

        var requiresSkillScriptApproval =
            !suppressApprovalRequirements &&
            capabilities.Any(composition.SkillBuilder.RequiresSkillScriptApproval);

        var skillsBuilder = new AgentSkillsProviderBuilder()
            .UseFileScriptRunner(composition.ToolBuilder.RunSkillScriptAsync)
            .UseOptions(options =>
            {
                options.DisableLoadSkillApproval = true;
                options.DisableReadSkillResourceApproval = true;
                options.DisableRunSkillScriptApproval = !requiresSkillScriptApproval;
            });

        foreach (var skillRoot in skillRoots)
        {
            skillsBuilder.UseFileSkill(skillRoot);
        }

        if (inlineSkills.Count > 0)
        {
            skillsBuilder.UseSkills(inlineSkills);
        }

        if (serviceSkills.Count > 0)
        {
            skillsBuilder.UseSkills(serviceSkills);
        }

        if (requiresSkillScriptApproval)
        {
            composition.State.HasApprovalTools = true;
        }

        composition.State.ContextSources.Add(AgentRuntimeContextManifestSource.Included(
            AgentRuntimeContextSourceCategories.Skills,
            "agent-skills-provider",
            "agent capabilities or configuration resolved skills for this run",
            skillRoots.Count + inlineSkills.Count + serviceSkills.Count,
            skillRoots.Sum(path => path.Length)));
        composition.State.ContextProviders.Add(skillsBuilder.Build());
        composition.State.FrameworkToolNames.Add(AgentToolInvocationPolicyMetadata.LoadSkill);
        composition.State.FrameworkToolNames.Add(AgentToolInvocationPolicyMetadata.ReadSkillResource);
        composition.State.FrameworkToolNames.Add(AgentToolInvocationPolicyMetadata.RunSkillScript);
        await progressCallback(
            ExecutionState.Preparing,
            "Skills",
            $"Loaded {skillRoots.Count} file skill root(s), {inlineSkills.Count} inline skill(s), and {serviceSkills.Count} DI-provided skill(s) through AgentSkillsProvider.");
    }

    private async Task AttachCatalogCapabilitiesAsync(
        RuntimeCapabilityComposition composition,
        AgentDefinition agent,
        ProviderProfile provider,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements)
    {
        foreach (var capability in capabilities)
        {
            await AttachCapabilityAsync(
                composition,
                capability,
                agent,
                provider,
                progressCallback,
                cancellationToken,
                suppressApprovalRequirements);
        }
    }

    private static bool IsWorkspaceCatalogToolCapability(CapabilityCatalogItem capability)
    {
        if (capability.Kind != CapabilityKind.Tool)
        {
            return false;
        }

        var configuration = DeserializeConfiguration<BuiltInToolConfiguration>(capability.ConfigurationJson);
        return IsWorkspaceToolKey(configuration?.Tool) || IsWorkspaceToolKey(capability.Key);
    }

    private static bool IsWorkspaceToolKey(string? toolKey)
    {
        if (string.IsNullOrWhiteSpace(toolKey))
        {
            return false;
        }

        var normalized = toolKey.Replace('-', '_');
        return string.Equals(normalized, "workspace_plugin", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("workspace_", StringComparison.OrdinalIgnoreCase) ||
               ToolContractCatalog.WorkspaceToolNames.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    private async Task AttachConfiguredWorkspaceToolsAsync(
        RuntimeCapabilityComposition composition,
        AgentDefinition agent,
        Func<ExecutionState, string, string, Task> progressCallback,
        bool suppressApprovalRequirements)
    {
        if (!agent.Permissions.CanUseTools)
        {
            return;
        }

        var tools = composition.ToolBuilder.CreateConfiguredWorkspaceTools(agent, suppressApprovalRequirements);
        if (tools.Count == 0)
        {
            composition.State.ContextSources.Add(AgentRuntimeContextManifestSource.Excluded(
                AgentRuntimeContextSourceCategories.WorkspaceTools,
                "configured-workspace-tools",
                "agent settings or process context profile selected no configured workspace tools"));
            return;
        }

        composition.State.Tools.AddRange(tools);
        composition.State.ContextSources.Add(AgentRuntimeContextManifestSource.Included(
            AgentRuntimeContextSourceCategories.WorkspaceTools,
            "configured-workspace-tools",
            "workspace tools allowed by agent settings and context profile",
            tools.Count,
            MafContextManifestBuilder.EstimateToolSchemaChars(tools)));
        composition.State.HasApprovalTools |= tools.Any(tool => tool is ApprovalRequiredAIFunction);
        var overrideProfile = WorkspaceExecutionAuditContext.Current?.WorkspaceToolProfileOverride;
        var profileSuffix = overrideProfile.HasValue
            ? $" Process dispatch override profile: {AgentWorkspaceToolAccessProfiles.GetProfileKey(overrideProfile.Value)}."
            : string.Empty;
        await progressCallback(
            ExecutionState.Preparing,
            "Workspace tools",
            "Attached configured workspace file and storage tools from the current agent settings." + profileSuffix);
    }

    private async Task AttachA2ARemoteAgentToolsAsync(
        RuntimeCapabilityComposition composition,
        AgentDefinition agent,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements)
    {
        if (!agent.Permissions.CanUseTools ||
            !agent.Permissions.CanAskOtherAgents)
        {
            return;
        }

        var settings = AgentA2AMetadata.Read(agent.ConfigurationJson);
        var endpoints = settings.RemoteEndpoints
            .Where(endpoint => endpoint.Enabled && endpoint.ExposeSkillsAsTools)
            .ToList();
        if (endpoints.Count == 0)
        {
            return;
        }

        var validation = AgentA2AMetadata.Validate(settings);
        if (!validation.Succeeded)
        {
            throw new InvalidOperationException("Agent A2A configuration is invalid: " + string.Join(" ", validation.Errors));
        }

        var factory = new A2ARemoteAgentToolFactory(
            services.GetService<IConfiguration>(),
            services.GetService<ILoggerFactory>());
        var result = await factory.CreateSkillToolsAsync(endpoints, cancellationToken);
        var approvalRequired = agent.Permissions.RequiresApprovalForExternalCalls;
        var tools = ApplyApprovalRequirement(
                result.Tools,
                approvalRequired,
                suppressApprovalRequirements)
            .ToList();

        composition.State.Tools.AddRange(tools);
        composition.State.ContextSources.Add(AgentRuntimeContextManifestSource.Included(
            AgentRuntimeContextSourceCategories.A2ARemoteAgents,
            "a2a-remote-agent-tools",
            "enabled A2A remote endpoints exposed skills as tools",
            tools.Count,
            MafContextManifestBuilder.EstimateToolSchemaChars(tools)));
        composition.State.Disposables.AddRange(result.Disposables);
        composition.State.HasApprovalTools |= !suppressApprovalRequirements && approvalRequired;
        await progressCallback(
            ExecutionState.Preparing,
            "A2A",
            $"Attached {tools.Count} A2A skill tool(s) from {endpoints.Count} configured remote endpoint(s).");
    }

    private async Task AttachCapabilityAsync(
        RuntimeCapabilityComposition composition,
        CapabilityCatalogItem capability,
        AgentDefinition agent,
        ProviderProfile provider,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements)
    {
        if (await TrySkipUnsupportedProviderNativeCapabilityAsync(capability, provider, progressCallback))
        {
            return;
        }

        switch (capability.Kind)
        {
            case CapabilityKind.Tool:
                var tools = composition.ToolBuilder.CreateTools(capability, provider, agent, suppressApprovalRequirements);
                foreach (var tool in tools)
                {
                    composition.State.Tools.Add(tool);
                }

                RecordCatalogCapabilitySource(composition.State, capability, tools.Count, MafContextManifestBuilder.EstimateToolSchemaChars(tools));
                composition.State.HasApprovalTools |= composition.ToolBuilder.CapabilityHasApprovalTools(capability, suppressApprovalRequirements);
                break;
            case CapabilityKind.Plugin:
                var pluginTools = composition.ToolBuilder.CreatePluginTools(capability, provider, agent, suppressApprovalRequirements);
                foreach (var tool in pluginTools)
                {
                    composition.State.Tools.Add(tool);
                }

                RecordCatalogCapabilitySource(composition.State, capability, pluginTools.Count, MafContextManifestBuilder.EstimateToolSchemaChars(pluginTools));
                composition.State.HasApprovalTools |= !suppressApprovalRequirements
                    && DeserializeConfiguration<PluginCapabilityConfiguration>(capability.ConfigurationJson)?.ApprovalRequired == true;
                break;
            case CapabilityKind.McpServer:
                await composition.McpBuilder.AddMcpToolsAsync(
                    composition.State,
                    capability,
                    agent,
                    provider,
                    progressCallback,
                    cancellationToken,
                    suppressApprovalRequirements);
                break;
            case CapabilityKind.Rag:
                composition.ContextBuilder.AddRagProvider(composition.State, capability, composition.AgentConfiguration);
                RecordCatalogCapabilitySource(composition.State, capability, 1, capability.Description.Length);
                break;
            case CapabilityKind.AiContext:
                composition.ContextBuilder.AddConfiguredAiContextProvider(composition.State, capability);
                RecordCatalogCapabilitySource(composition.State, capability, 1, capability.Description.Length);
                break;
            case CapabilityKind.Memory:
                throw LegacyMemoryCapabilityPolicy.CreateException(capability.Name);
        }
    }

    private static void RecordCatalogCapabilitySource(
        RuntimeCapabilityState state,
        CapabilityCatalogItem capability,
        int itemCount,
        int estimatedChars)
    {
        if (itemCount <= 0)
        {
            state.ContextSources.Add(AgentRuntimeContextManifestSource.Excluded(
                AgentRuntimeContextSourceCategories.CatalogCapability,
                capability.Key,
                $"catalog capability '{capability.Name}' produced no runtime context or tools"));
            return;
        }

        state.ContextSources.Add(AgentRuntimeContextManifestSource.Included(
            AgentRuntimeContextSourceCategories.CatalogCapability,
            capability.Key,
            $"catalog capability '{capability.Name}' attached runtime context or tools",
            itemCount,
            estimatedChars));
    }

    private static async Task<bool> TrySkipUnsupportedProviderNativeCapabilityAsync(
        CapabilityCatalogItem capability,
        ProviderProfile provider,
        Func<ExecutionState, string, string, Task> progressCallback)
    {
        if (capability.Kind == CapabilityKind.Tool)
        {
            var configuration = DeserializeConfiguration<BuiltInToolConfiguration>(capability.ConfigurationJson) ?? new BuiltInToolConfiguration();
            var toolKey = configuration.Tool ?? capability.Key;
            if (ProviderNativeToolKeys.TryResolveFamily(toolKey, out var family))
            {
                var support = ProviderFeatureService.GetNativeToolSupport(provider, family);
                if (!support.IsSupported)
                {
                    await progressCallback(
                        ExecutionState.Preparing,
                        "Capability compatibility",
                        $"Skipping capability '{capability.Name}' for provider '{provider.Name}'. {support.Summary} {support.Remediation}");
                    return true;
                }
            }
        }
        else if (capability.Kind == CapabilityKind.McpServer)
        {
            var configuration = DeserializeConfiguration<McpCapabilityConfiguration>(capability.ConfigurationJson);
            if (configuration?.Hosted == true)
            {
                var support = ProviderFeatureService.GetNativeToolSupport(provider, ProviderNativeToolFamily.HostedMcpServer);
                if (!support.IsSupported)
                {
                    await progressCallback(
                        ExecutionState.Preparing,
                        "Capability compatibility",
                        $"Skipping capability '{capability.Name}' for provider '{provider.Name}'. {support.Summary} {support.Remediation}");
                    return true;
                }
            }
        }

        return false;
    }

    private async Task AttachCompactionAsync(
        RuntimeCapabilityComposition composition,
        AgentDefinition agent,
        Func<ExecutionState, string, string, Task> progressCallback,
        bool suppressApprovalRequirements)
    {
        var decision = ResolveCompactionDecision(agent, composition.AgentConfiguration, suppressApprovalRequirements);
        if (!decision.ShouldAttachCompaction)
        {
            composition.State.ContextSources.Add(AgentRuntimeContextManifestSource.Excluded(
                AgentRuntimeContextSourceCategories.Compaction,
                "microsoft-agent-framework-compaction",
                decision.Message));
            await progressCallback(
                ExecutionState.Preparing,
                "Compaction",
                decision.Message);
            return;
        }

        if (!EnsureCompactionCredentialAvailable())
        {
            composition.State.ContextSources.Add(AgentRuntimeContextManifestSource.Excluded(
                AgentRuntimeContextSourceCategories.Compaction,
                "microsoft-agent-framework-compaction",
                "OPENAI_API_KEY unavailable for compaction provider"));
            await progressCallback(
                ExecutionState.Preparing,
                "Compaction",
                "Skipped Microsoft Agent Framework compaction because OPENAI_API_KEY is not available to the runtime. Core provider execution will resolve credentials through the configured provider profile.");
            return;
        }

        composition.State.ContextProviders.Add(CreateCompactionProvider(composition.AgentConfiguration));
        composition.State.ContextSources.Add(AgentRuntimeContextManifestSource.Included(
            AgentRuntimeContextSourceCategories.Compaction,
            "microsoft-agent-framework-compaction",
            decision.Message,
            1));
        await progressCallback(
            ExecutionState.Preparing,
            "Compaction",
            $"Attached Microsoft Agent Framework compaction for {decision.PolicyKind} context with defaults of {DefaultCompactionSlidingWindowTurns} turns, {DefaultCompactionTruncationTokenLimit} tokens, and {DefaultToolCompactionMessageThreshold} tool messages unless overridden by agent configuration.");
    }

    private static RuntimeCompactionDecision ResolveCompactionDecision(
        AgentDefinition agent,
        AgentRuntimeConfiguration configuration,
        bool suppressApprovalRequirements)
    {
        var policyKind = ResolveContextPolicyKind(agent, suppressApprovalRequirements);
        if (configuration.EnableCompaction.HasValue)
        {
            if (configuration.EnableCompaction.Value)
            {
                if (policyKind == AgentRuntimeContextPolicyKind.GovernedProcessAutomation)
                {
                    return RuntimeCompactionDecision.Skip(
                        policyKind,
                        "Skipped Microsoft Agent Framework compaction for governed process automation even though the agent requested compaction. Process-step prompts carry required artifact paths and tool-evidence rules, so they must not be summarized before the run starts.");
                }

                if (policyKind == AgentRuntimeContextPolicyKind.AutoApprovedNonInteractive)
                {
                    return RuntimeCompactionDecision.Skip(
                        policyKind,
                        "Skipped Microsoft Agent Framework compaction for auto-approved non-interactive execution even though the agent requested compaction. Compaction is optional and must not block unattended tool continuations before the agent session starts.");
                }

                return RuntimeCompactionDecision.Attach(
                    policyKind,
                    "Agent configuration explicitly enabled Microsoft Agent Framework compaction.");
            }

            return RuntimeCompactionDecision.Skip(
                policyKind,
                "Skipped Microsoft Agent Framework compaction because agent configuration explicitly disabled it.");
        }

        if (policyKind == AgentRuntimeContextPolicyKind.GovernedProcessAutomation)
        {
            return RuntimeCompactionDecision.Skip(
                policyKind,
                "Skipped Microsoft Agent Framework compaction for governed process automation. Process-step prompts are bounded and include required artifact paths, so compaction must not trim evidence context before the agent session starts.");
        }

        if (policyKind == AgentRuntimeContextPolicyKind.AutoApprovedNonInteractive)
        {
            return RuntimeCompactionDecision.Skip(
                policyKind,
                "Skipped Microsoft Agent Framework compaction for auto-approved non-interactive execution. Compaction is optional and must not block unattended runs before the agent session starts.");
        }

        if (agent.ChatHistoryMode == AgentChatHistoryMode.FrameworkManaged ||
            agent.Workload is AgentWorkloadKind.Programming or AgentWorkloadKind.Research)
        {
            return RuntimeCompactionDecision.Attach(
                policyKind,
                "Default context policy enables Microsoft Agent Framework compaction for framework-managed, programming, and research histories.");
        }

        return RuntimeCompactionDecision.Skip(
            policyKind,
            $"Skipped Microsoft Agent Framework compaction because the {policyKind} context policy does not require it for workload '{agent.Workload}'.");
    }

    private static AgentRuntimeContextPolicyKind ResolveContextPolicyKind(
        AgentDefinition agent,
        bool suppressApprovalRequirements)
    {
        if (IsGovernedProcessAutomationRun())
        {
            return AgentRuntimeContextPolicyKind.GovernedProcessAutomation;
        }

        if (suppressApprovalRequirements)
        {
            return AgentRuntimeContextPolicyKind.AutoApprovedNonInteractive;
        }

        var a2aSettings = AgentA2AMetadata.Read(agent.ConfigurationJson);
        return a2aSettings.Hosting.Enabled
            ? AgentRuntimeContextPolicyKind.A2AEndpoint
            : AgentRuntimeContextPolicyKind.InteractiveChat;
    }

    private static AgentContextExecutionMode MapContextContributionExecutionMode(AgentRuntimeContextPolicyKind policyKind)
        => policyKind switch
        {
            AgentRuntimeContextPolicyKind.GovernedProcessAutomation => AgentContextExecutionMode.GovernedProcessAutomation,
            AgentRuntimeContextPolicyKind.AutoApprovedNonInteractive => AgentContextExecutionMode.AutoApprovedNonInteractive,
            AgentRuntimeContextPolicyKind.A2AEndpoint => AgentContextExecutionMode.A2AEndpoint,
            AgentRuntimeContextPolicyKind.InteractiveChat => AgentContextExecutionMode.InteractiveChat,
            _ => AgentContextExecutionMode.InteractiveChat
        };

    internal static bool IsGovernedProcessAutomationRun()
    {
        var auditScope = WorkspaceExecutionAuditContext.Current;
        return auditScope is not null &&
               (!string.IsNullOrWhiteSpace(auditScope.ProcessRunId) ||
                !string.IsNullOrWhiteSpace(auditScope.ProcessStepId));
    }

    private bool EnsureCompactionCredentialAvailable()
    {
        var processValue = AgentProviderEnvironmentCredential.ResolveAndPromote(OpenAiApiKeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(processValue))
        {
            return true;
        }

        var configuredValue = services.GetService<IConfiguration>()?[OpenAiApiKeyEnvironmentVariable];
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return false;
        }

        AgentProviderEnvironmentCredential.PromoteProcessValue(OpenAiApiKeyEnvironmentVariable, configuredValue.Trim());
        return true;
    }

    private static CompactionProvider CreateCompactionProvider(AgentRuntimeConfiguration configuration)
    {
        var slidingWindowTurns = configuration.SlidingWindowTurns ?? DefaultCompactionSlidingWindowTurns;
        var truncationTokenLimit = configuration.TruncationTokenLimit ?? DefaultCompactionTruncationTokenLimit;
        var toolMessageThreshold = configuration.ToolCompactionMessageThreshold ?? DefaultToolCompactionMessageThreshold;

        var pipeline = new PipelineCompactionStrategy(
            new ToolResultCompactionStrategy(CompactionTriggers.MessagesExceed(toolMessageThreshold)),
            new SlidingWindowCompactionStrategy(CompactionTriggers.TurnsExceed(slidingWindowTurns)),
            new TruncationCompactionStrategy(CompactionTriggers.TokensExceed(truncationTokenLimit)));

        return new CompactionProvider(pipeline);
    }

    private static void DeduplicateTools(List<AITool> tools)
    {
        var deduplicated = tools
            .GroupBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        tools.Clear();
        tools.AddRange(deduplicated);
    }

    private static IEnumerable<AITool> ApplyApprovalRequirement(
        IEnumerable<AITool> tools,
        bool approvalRequired,
        bool suppressApprovalRequirements = false)
    {
        if (!approvalRequired || suppressApprovalRequirements)
        {
            return tools;
        }

        return tools.Select(tool => tool is AIFunction function
            ? new ApprovalRequiredAIFunction(function)
            : tool);
    }

    private static ChatRole ParseChatRole(string? role)
    {
        if (string.Equals(role, nameof(ChatRole.User), StringComparison.OrdinalIgnoreCase))
        {
            return ChatRole.User;
        }

        if (string.Equals(role, nameof(ChatRole.Assistant), StringComparison.OrdinalIgnoreCase))
        {
            return ChatRole.Assistant;
        }

        return ChatRole.System;
    }

    private string ResolvePathFromWorkspace(string path, bool allowExternal, IReadOnlyList<string>? allowedExternalRoots = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return workspaceRoot;
        }

        var expandedPath = ExpandPortablePath(path);
        var fullPath = Path.GetFullPath(Path.IsPathRooted(expandedPath) ? expandedPath : Path.Combine(workspaceRoot, expandedPath));
        if (IsPathWithinRoot(fullPath, workspaceRoot))
        {
            return fullPath;
        }

        if (!allowExternal)
        {
            throw new InvalidOperationException($"Path '{path}' resolves outside the workspace root. Use a workspace-relative path or import the external file into chat attachments first.");
        }

        var allowedRoots = allowedExternalRoots?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(ExpandPortablePath)
            .Select(item => Path.GetFullPath(Path.IsPathRooted(item) ? item : Path.Combine(workspaceRoot, item)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];

        if (allowedRoots.Any(allowedRoot => IsPathWithinRoot(fullPath, allowedRoot)))
        {
            return fullPath;
        }

        throw new InvalidOperationException($"Path '{path}' resolves outside the workspace root and is not covered by an explicit external-root allowlist.");
    }

    private static string ExpandPortablePath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        if (string.Equals(expanded, "~", StringComparison.Ordinal))
        {
            var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrWhiteSpace(homeDirectory)
                ? expanded
                : homeDirectory;
        }

        if (!expanded.StartsWith("~" + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
            !expanded.StartsWith("~" + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            return expanded;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            return expanded;
        }

        var relativePath = expanded[2..]
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return Path.Combine(home, relativePath);
    }

    private static bool IsPathWithinRoot(string fullPath, string rootPath)
    {
        var normalizedRoot = Path.GetFullPath(rootPath);
        if (string.Equals(fullPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var rootWithSeparator = normalizedRoot.EndsWith(Path.DirectorySeparatorChar) || normalizedRoot.EndsWith(Path.AltDirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static TConfiguration? DeserializeConfiguration<TConfiguration>(string? json)
        where TConfiguration : class
        => MafRuntimeJson.DeserializeConfiguration<TConfiguration>(json);
}
