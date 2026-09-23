using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Workspace.ApiAccess;

public static class ApiAccessScopeNames
{
    public const string Api = "api";

    public const string Session = "api.session";
    public const string ManageAccess = "api.access.manage";
    public const string ReadRuntime = "api.runtime.read";
    public const string ReadProjects = "api.projects.read";
    public const string WriteProjects = "api.projects.write";
    public const string ReadAgents = "api.agents.read";
    public const string WriteAgents = "api.agents.write";
    public const string ExecuteAgents = "api.agents.execute";
    public const string ReviewAgentRecruiting = AgentRecruitingAuthorizationScopes.HumanReview;
    public const string ReadWorkflows = "api.workflows.read";
    public const string WriteWorkflows = "api.workflows.write";
    public const string ExecuteWorkflows = "api.workflows.execute";
    public const string ReadProcesses = "api.processes.read";
    public const string WriteProcesses = "api.processes.write";
    public const string ExecuteProcesses = "api.processes.execute";
    public const string ReadPrompts = "api.prompts.read";
    public const string WritePrompts = "api.prompts.write";
    public const string ReadCrmHr = "api.crm-hr.read";
    public const string WriteCrmHr = "api.crm-hr.write";
    public const string ReadPlugins = "api.plugins.read";
    public const string WritePlugins = "api.plugins.write";
    public const string ReadWorkspaceSettings = "api.settings.workspace.read";
    public const string WriteWorkspaceSettings = "api.settings.workspace.write";

    public const string IssueTokens = "api.tokens.issue";

    public const string ReadMemoryProviders = "api.memory-providers.read";

    public const string WriteMemoryProviders = "api.memory-providers.write";

    public const string QueryMemoryProviders = "api.memory-providers.query";

    public const string WriteProjectStructure = "api.project-structure.write";

    public const string ReadLlmChats = "api.llm-chats.read";

    public const string ManageLlmChats = "api.llm-chats.manage";

    public const string ExecuteLlmChats = "api.llm-chats.execute";

    public const string RespondWorkflows = "api.workflows.respond";

    public const string ReadSharedProviderCatalog = "api.shared-providers.catalog.read";

    public const string InvokeSharedProviders = "api.shared-providers.invoke";

    public const string ReadProviderHistory = "api.provider-history.read";

    public const string ReadProviderHistoryContent = "api.provider-history.content.read";

    public const string ReadStoragePlacementRecovery = "api.storage-placement-recovery.read";
    public const string ReconcileStoragePlacement = "api.storage-placement-recovery.reconcile";
    public const string VerifyStorageExternalTermination = "api.storage-placement-recovery.verify-external-termination";

    public const string ManageProviderHistory = "api.provider-history.manage";
}
