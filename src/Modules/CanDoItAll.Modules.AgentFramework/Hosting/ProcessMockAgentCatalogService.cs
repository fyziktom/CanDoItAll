using System.Text.Json;
using System.Text.Json.Nodes;
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
    private static readonly AgentPermissionsPolicy ProcessMockAgentPermissions = AgentPermissionsPolicy.Default with
    {
        CanUseTools = true,
        CanAskOtherAgents = true,
        CanEscalateToHuman = true,
        RequiresApprovalForExternalCalls = false,
        AutoApproveExternalCallsByDefault = true,
        AllowedSecrets = []
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

        var providerResult = await EnsureProviderAsync(workspaceService, provider, cancellationToken);
        var agentResult = await EnsureAgentsAsync(workspaceService, providerResult.Provider, cancellationToken);
        if (providerResult.Changed || agentResult.Changed)
        {
            await technicalAgentBridge.SynchronizeDirectoryProjectionAsync(cancellationToken);
        }

        return new ProcessMockAgentCatalogContext(providerResult.Provider.Id, agentResult.AgentIdsByRoleKey);
    }

    private static async Task<ProviderEnsureResult> EnsureProviderAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        ProviderProfile? provider,
        CancellationToken cancellationToken)
    {
        if (provider is not null && IsProcessMockProviderCurrent(provider))
        {
            return new ProviderEnsureResult(provider, Changed: false);
        }

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
        editor.ConfigurationJson = CreateProviderConfigurationJson();
        editor.Notes = "Settings-gated deterministic mock provider for process automation flow tuning.";
        editor.SuggestedModels =
        [
            ProcessMockAgentCatalog.Model
        ];

        var providerId = await workspaceService.SaveProviderAsync(editor, cancellationToken);
        var providers = await workspaceService.ListProvidersAsync(cancellationToken);
        return new ProviderEnsureResult(providers.First(item => item.Id == providerId), Changed: true);
    }

    private static async Task<AgentCatalogEnsureResult> EnsureAgentsAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        ProviderProfile provider,
        CancellationToken cancellationToken)
    {
        var agents = (await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken)).ToList();
        var agentIdsByRoleKey = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var role in ProcessMockAgentCatalog.Roles)
        {
            var roleTag = ProcessMockAgentCatalog.CreateRoleTag(role.RoleKey);
            var agent = agents.FirstOrDefault(item =>
                    item.ProviderProfileId == provider.Id &&
                    item.Tags.Contains(roleTag, StringComparer.OrdinalIgnoreCase)) ??
                agents.FirstOrDefault(item =>
                    item.ProviderProfileId == provider.Id &&
                    string.Equals(item.Name, role.AgentName, StringComparison.Ordinal));
            if (agent is not null && IsProcessMockAgentCurrent(agent, provider, role))
            {
                agentIdsByRoleKey[role.RoleKey] = agent.Id;
                continue;
            }

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
            editor.ConfigurationJson = CreateAgentConfigurationJson(role);
            editor.IsTemplate = false;
            editor.TemplateKey = string.Empty;
            editor.Permissions = ProcessMockAgentPermissions;
            editor.ProjectStructureAccess.CanRead = true;
            editor.ProjectStructureAccess.CanWrite = false;
            editor.ProjectStructureAccess.CanWriteNonTaskStructure = false;
            editor.ProjectStructureAccess.CanWriteTasks = false;
            editor.ProjectStructureAccess.CanCreateProjects = false;
            editor.ProjectStructureAccess.CanCreateSubprojects = false;
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
            changed = true;
        }

        return new AgentCatalogEnsureResult(agentIdsByRoleKey, changed);
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

    private static string CreateProviderConfigurationJson()
    {
        return JsonSerializer.Serialize(
            new
            {
                processMockAgents = true
            },
            JsonOptions);
    }

    private static string CreateAgentConfigurationJson(ProcessMockAgentRoleDefinition role)
    {
        var configurationJson = JsonSerializer.Serialize(
            new
            {
                processMockAgent = true,
                roleKey = role.RoleKey
            },
            JsonOptions);
        return AgentFrameworkCrmHrMetadata.Write(
            configurationJson,
            role.PartyId,
            AiExecutionMode.Remote,
            []);
    }

    private static bool IsProcessMockProviderCurrent(ProviderProfile provider)
    {
        return string.Equals(provider.Name, ProcessMockAgentCatalog.ProviderName, StringComparison.Ordinal) &&
               provider.Kind == ProviderKind.OpenAi &&
               string.Equals(provider.BaseUrl, ProcessMockAgentCatalog.ProviderBaseUrl, StringComparison.OrdinalIgnoreCase) &&
               string.IsNullOrWhiteSpace(provider.ApiKeyEnvironmentVariable) &&
               string.Equals(provider.DefaultModel, ProcessMockAgentCatalog.Model, StringComparison.Ordinal) &&
               provider.Transport == ProviderTransportKind.Responses &&
               provider.IsEnabled &&
               !provider.SupportsStreaming &&
               provider.SupportsTools &&
               provider.PreferFrameworkManagedChatHistory &&
               !provider.SupportsBackgroundResponses &&
               JsonContentEquals(provider.ConfigurationJson, CreateProviderConfigurationJson()) &&
               string.Equals(provider.Notes, "Settings-gated deterministic mock provider for process automation flow tuning.", StringComparison.Ordinal) &&
               provider.SuggestedModels.SequenceEqual([ProcessMockAgentCatalog.Model], StringComparer.Ordinal);
    }

    private static bool IsProcessMockAgentCurrent(
        AgentDefinition agent,
        ProviderProfile provider,
        ProcessMockAgentRoleDefinition role)
    {
        var projectStructureAccess = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
        var processAccess = AgentProcessAccessMetadata.Read(agent.ConfigurationJson);
        var crmHrMetadata = AgentFrameworkCrmHrMetadata.Read(agent.ConfigurationJson);
        var desiredTags = CreateAgentTags(role);

        return string.Equals(agent.Name, role.AgentName, StringComparison.Ordinal) &&
               string.Equals(agent.RoleTitle, role.RoleTitle, StringComparison.Ordinal) &&
               string.Equals(agent.Summary, role.Summary, StringComparison.Ordinal) &&
               string.Equals(agent.Instructions, role.Instructions, StringComparison.Ordinal) &&
               agent.Status == AgentLifecycleStatus.Active &&
               agent.ProviderProfileId == provider.Id &&
               string.IsNullOrWhiteSpace(agent.Model) &&
               agent.Workload == role.Workload &&
               agent.ChatHistoryMode == AgentChatHistoryMode.ProviderDefault &&
               agent.Temperature == 0d &&
               !agent.RequirePerServiceCallChatHistoryPersistence &&
               !agent.EnableBackgroundResponses &&
               !agent.IsTemplate &&
               string.IsNullOrWhiteSpace(agent.TemplateKey) &&
               PermissionsMatch(agent.Permissions, ProcessMockAgentPermissions) &&
               agent.Capabilities.Count == 0 &&
               TagsMatch(agent.Tags, desiredTags) &&
               string.Equals(ProcessMockAgentCatalog.ResolveRoleKey(agent), role.RoleKey, StringComparison.OrdinalIgnoreCase) &&
               crmHrMetadata?.PartyId == role.PartyId &&
               crmHrMetadata.ExecutionMode == AiExecutionMode.Remote &&
               crmHrMetadata.Capabilities.Count == 0 &&
                projectStructureAccess.CanRead &&
                 !projectStructureAccess.CanWrite &&
                 !projectStructureAccess.CanWriteNonTaskStructure &&
                 !projectStructureAccess.CanWriteTasks &&
                 !projectStructureAccess.CanCreateProjects &&
                 !projectStructureAccess.CanCreateSubprojects &&
                 projectStructureAccess.AllowAllProjects &&
               projectStructureAccess.AllowedProjectIds.Count == 0 &&
               processAccess.CanRead &&
               !processAccess.CanWrite &&
               processAccess.AllowAllDefinitions &&
               processAccess.AllowedDefinitionIds.Count == 0;
    }

    private static IReadOnlyList<string> CreateAgentTags(ProcessMockAgentRoleDefinition role)
    {
        var roleTag = ProcessMockAgentCatalog.CreateRoleTag(role.RoleKey);
        return AgentFrameworkCrmHrMetadata.EnsurePartyTag(
                [
                    ProcessMockAgentCatalog.AgentTag,
                    roleTag
                ],
                role.PartyId)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static bool PermissionsMatch(AgentPermissionsPolicy current, AgentPermissionsPolicy desired)
    {
        return current.CanUseTools == desired.CanUseTools &&
               current.CanAskOtherAgents == desired.CanAskOtherAgents &&
               current.CanEscalateToHuman == desired.CanEscalateToHuman &&
               current.CanObserveOtherAgents == desired.CanObserveOtherAgents &&
               current.CanScheduleWork == desired.CanScheduleWork &&
               current.RequiresApprovalForExternalCalls == desired.RequiresApprovalForExternalCalls &&
               current.AutoApproveExternalCallsByDefault == desired.AutoApproveExternalCallsByDefault &&
               current.NormalizedAllowedSecrets.SequenceEqual(desired.NormalizedAllowedSecrets);
    }

    private static bool TagsMatch(IReadOnlyList<string> current, IReadOnlyList<string> desired)
    {
        return current.Count == desired.Count &&
               current.All(item => desired.Contains(item, StringComparer.OrdinalIgnoreCase));
    }

    private static bool JsonContentEquals(string? current, string desired)
    {
        try
        {
            return string.Equals(
                JsonNode.Parse(current ?? string.Empty)?.ToJsonString(),
                JsonNode.Parse(desired)?.ToJsonString(),
                StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record ProviderEnsureResult(ProviderProfile Provider, bool Changed);

    private sealed record AgentCatalogEnsureResult(
        IReadOnlyDictionary<string, Guid> AgentIdsByRoleKey,
        bool Changed);
}
