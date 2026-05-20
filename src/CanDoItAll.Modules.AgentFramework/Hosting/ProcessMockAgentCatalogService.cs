using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.CrmHr;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.AgentFramework.Hosting;

public sealed class ProcessMockAgentCatalogService(
    ICanDoItAllAgentWorkspaceFactory workspaceFactory,
    IAiTechnicalAgentBridge technicalAgentBridge,
    IOptions<ProcessMockAgentOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<ProcessMockAgentCatalogContext?> EnsureCatalogAsync(CancellationToken cancellationToken = default)
    {
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var providers = (await workspaceService.ListProvidersAsync(cancellationToken)).ToList();
        var provider = providers.FirstOrDefault(ProcessMockAgentCatalog.IsProcessMockProvider);

        if (!options.Value.Enabled)
        {
            if (provider is not null)
            {
                await DisableCatalogAsync(workspaceService, provider, cancellationToken);
                await technicalAgentBridge.SynchronizeDirectoryProjectionAsync(cancellationToken);
            }

            return null;
        }

        provider = await EnsureProviderAsync(workspaceService, provider, cancellationToken);
        var agentIdsByRoleKey = await EnsureAgentsAsync(workspaceService, provider, cancellationToken);
        await technicalAgentBridge.SynchronizeDirectoryProjectionAsync(cancellationToken);

        return new ProcessMockAgentCatalogContext(provider.Id, agentIdsByRoleKey);
    }

    private static async Task<ProviderProfile> EnsureProviderAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        ProviderProfile? provider,
        CancellationToken cancellationToken)
    {
        var editor = provider is null
            ? await workspaceService.GetProviderEditorAsync(cancellationToken: cancellationToken)
            : await workspaceService.GetProviderEditorAsync(provider.Id, cancellationToken);

        editor.Name = ProcessMockAgentCatalog.ProviderName;
        editor.Kind = ProviderKind.OpenAi;
        editor.BaseUrl = ProcessMockAgentCatalog.ProviderBaseUrl;
        editor.ApiKeyEnvironmentVariable = string.Empty;
        editor.DefaultModel = ProcessMockAgentCatalog.Model;
        editor.Transport = ProviderTransportKind.Responses;
        editor.IsEnabled = true;
        editor.SupportsStreaming = false;
        editor.SupportsTools = true;
        editor.PreferFrameworkManagedChatHistory = true;
        editor.SupportsBackgroundResponses = false;
        editor.ConfigurationJson = JsonSerializer.Serialize(
            new
            {
                processMockAgents = true
            },
            JsonOptions);
        editor.Notes = "Settings-gated deterministic mock provider for process automation flow tuning.";
        editor.SuggestedModels =
        [
            ProcessMockAgentCatalog.Model
        ];

        var providerId = await workspaceService.SaveProviderAsync(editor, cancellationToken);
        var providers = await workspaceService.ListProvidersAsync(cancellationToken);
        return providers.First(item => item.Id == providerId);
    }

    private static async Task<IReadOnlyDictionary<string, Guid>> EnsureAgentsAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        ProviderProfile provider,
        CancellationToken cancellationToken)
    {
        var agents = (await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken)).ToList();
        var agentIdsByRoleKey = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in ProcessMockAgentCatalog.Roles)
        {
            var roleTag = ProcessMockAgentCatalog.CreateRoleTag(role.RoleKey);
            var agent = agents.FirstOrDefault(item =>
                    item.ProviderProfileId == provider.Id &&
                    item.Tags.Contains(roleTag, StringComparer.OrdinalIgnoreCase)) ??
                agents.FirstOrDefault(item =>
                    item.ProviderProfileId == provider.Id &&
                    string.Equals(item.Name, role.AgentName, StringComparison.Ordinal));

            var editor = agent is null
                ? await workspaceService.GetAgentEditorAsync(cancellationToken: cancellationToken)
                : await workspaceService.GetAgentEditorAsync(agent.Id, cancellationToken);

            editor.Name = role.AgentName;
            editor.RoleTitle = role.RoleTitle;
            editor.Summary = role.Summary;
            editor.Instructions = role.Instructions;
            editor.Status = AgentLifecycleStatus.Active;
            editor.ProviderProfileId = provider.Id;
            editor.Model = string.Empty;
            editor.Workload = role.Workload;
            editor.ChatHistoryMode = AgentChatHistoryMode.ProviderDefault;
            editor.Temperature = 0d;
            editor.RequirePerServiceCallChatHistoryPersistence = false;
            editor.EnableBackgroundResponses = false;
            editor.ConfigurationJson = JsonSerializer.Serialize(
                new
                {
                    processMockAgent = true,
                    roleKey = role.RoleKey
                },
                JsonOptions);
            editor.ConfigurationJson = AgentFrameworkCrmHrMetadata.Write(
                editor.ConfigurationJson,
                role.PartyId,
                AiExecutionMode.Remote,
                []);
            editor.IsTemplate = false;
            editor.TemplateKey = string.Empty;
            editor.Permissions = AgentPermissionsPolicy.Default with
            {
                CanUseTools = true,
                CanAskOtherAgents = true,
                CanEscalateToHuman = true,
                RequiresApprovalForExternalCalls = false,
                AutoApproveExternalCallsByDefault = true
            };
            editor.ProjectStructureAccess.CanRead = true;
            editor.ProjectStructureAccess.CanWrite = false;
            editor.ProjectStructureAccess.AllowAllProjects = true;
            editor.ProcessAccess.CanRead = true;
            editor.ProcessAccess.CanWrite = false;
            editor.ProcessAccess.AllowAllDefinitions = true;
            editor.SelectedCapabilityIds = [];
            var tags = AgentFrameworkCrmHrMetadata.EnsurePartyTag(
                [
                    ProcessMockAgentCatalog.AgentTag,
                    roleTag
                ],
                role.PartyId);
            editor.Tags = tags
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var agentId = await workspaceService.SaveAgentAsync(editor, cancellationToken);
            agentIdsByRoleKey[role.RoleKey] = agentId;
        }

        return agentIdsByRoleKey;
    }

    private static async Task DisableCatalogAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        ProviderProfile provider,
        CancellationToken cancellationToken)
    {
        if (provider.IsEnabled)
        {
            var providerEditor = await workspaceService.GetProviderEditorAsync(provider.Id, cancellationToken);
            providerEditor.IsEnabled = false;
            await workspaceService.SaveProviderAsync(providerEditor, cancellationToken);
        }

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken);
        var processMockAgents = agents
            .Where(item =>
                item.ProviderProfileId == provider.Id ||
                item.Tags.Contains(ProcessMockAgentCatalog.AgentTag, StringComparer.OrdinalIgnoreCase))
            .Where(item => item.Status != AgentLifecycleStatus.Suspended)
            .ToList();

        foreach (var agent in processMockAgents)
        {
            var agentEditor = await workspaceService.GetAgentEditorAsync(agent.Id, cancellationToken);
            agentEditor.Status = AgentLifecycleStatus.Suspended;
            await workspaceService.SaveAgentAsync(agentEditor, cancellationToken);
        }
    }
}
