using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Tools.StaticHost;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

public sealed class WorkspaceValidationToolProvider : IAgentRuntimeToolProvider {
    private const string ProviderKey = "workspace-published-output";
    private static readonly string[] ToolNames = [ToolContractCatalog.WorkspaceDotNetPublish, ToolContractCatalog.WorkspaceStaticServe];

    public int Order => 0;

    public AgentRuntimeToolProviderDescriptor Descriptor { get; } = new(
        ProviderKey, "Published-output validation", "Publish and serve generated output for validation.", ["configured", "workspace"]) {
        AttachmentPhase = AgentRuntimeToolAttachmentPhase.ConfiguredWorkspace
    };

    public AgentRuntimeConfiguredWorkspacePolicy GetConfiguredWorkspacePolicy(AgentWorkspaceToolAccessSettings workspaceToolAccess, AgentRuntimeContextIntent contextIntent) {
        var access = AgentWorkspaceToolAccessMetadata.Normalize(workspaceToolAccess);
        var allowed = access.CanRunValidationCommands && contextIntent.WorkspaceToolsEnabled;
        var descriptors = allowed ? ToolNames.Select(name => RuntimeToolCapabilityDescriptorFactory.CreateRuntimeToolCapabilityDescriptor(
            name, "Published-output validation", "Workspace validation tool exposed from agent workspace settings.", ["configured", "workspace"])).ToArray() : [];
        var rules = allowed ? Array.Empty<CapabilityAccessRule>() : ToolNames.Select(name => new CapabilityAccessRule(
            CapabilityRuleId.Create($"deny-runtime-tool-{name.Replace('_', '-')}"), CapabilityAccessEffect.Deny,
            CapabilityAccessScope.RuntimeOverride, CapabilitySelector.ByRuntimeToolName(RuntimeToolName.Create(name)),
            "Workspace validation commands are disabled by agent settings or execution context.")).ToArray();
        return new(descriptors, [new CapabilityAccessPolicy(rules)]);
    }

    public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(AgentRuntimeToolProviderContext context, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsEnabled(context)) {
            return ValueTask.FromResult<IReadOnlyList<AITool>>([]);
        }
        var commands = context.PublishedOutputCommands;
        if (!context.ToolInventoryOnly && commands is null) {
            throw new WorkspaceRuntimeCompositionException("Published-output tools require the current execution's workspace command service.");
        }
        Task<WorkspaceCommandExecutionResult> Publish(string targetPath, string configuration = "Release", bool noRestore = false, string? workingDirectory = null, int timeoutSeconds = 600)
            => RequireCommands(commands).DotnetPublish(targetPath, configuration, noRestore, workingDirectory, timeoutSeconds);
        Task<WorkspaceCommandExecutionResult> Serve(string directoryPath, string? url = null, bool spaFallback = true, int startupTimeoutSeconds = 45)
            => RequireCommands(commands).ServeStaticFiles(typeof(StaticFileHost).Assembly.Location, directoryPath, url, spaFallback, startupTimeoutSeconds);
        return ValueTask.FromResult<IReadOnlyList<AITool>>([
            AIFunctionFactory.Create(Publish, ToolContractCatalog.WorkspaceDotNetPublish,
                "Publishes one .NET project to a fresh managed artifact directory and returns its managed reference, its static web root when the output has one, and command receipts. Serve that static web root for published-output validation; this does not deploy to an external service."),
            AIFunctionFactory.Create(Serve, ToolContractCatalog.WorkspaceStaticServe,
                "Serves an authorized directory read-only over loopback HTTP for validation. For a published web app, pass its generated wwwroot directory. Returns a startup.json receipt and URL; stop it with workspace_dotnet_stop. The owned host is also cleaned up when this execution ends. Uses no-cache, max-age=0, must-revalidate headers; optional SPA fallback applies only to extensionless routes. This is disposable test infrastructure, not production deployment.")
        ]);
    }

    public IReadOnlyList<AgentRuntimeToolMetadata> GetToolMetadata(AgentRuntimeToolProviderContext context)
        => IsEnabled(context) ? ToolNames.Select(name => new AgentRuntimeToolMetadata(
            ProviderKey, name, AgentRuntimeToolOperationKind.Validation, false, ["configured", "workspace"])).ToArray() : [];

    internal static bool Owns(string toolName) => ToolNames.Contains(toolName, StringComparer.Ordinal);

    private static bool IsEnabled(AgentRuntimeToolProviderContext context)
        => context.Agent.Permissions.CanUseTools && context.ContextIntent.WorkspaceToolsEnabled &&
            AgentWorkspaceToolAccessMetadata.Normalize(context.WorkspaceToolAccess
                ?? throw new WorkspaceRuntimeCompositionException("Published-output tools require resolved workspace access.")).CanRunValidationCommands;

    private static IWorkspacePublishedOutputCommands RequireCommands(IWorkspacePublishedOutputCommands? commands)
        => commands ?? throw new WorkspaceRuntimeCompositionException("Inventory-only workspace tools cannot be invoked.");
}
