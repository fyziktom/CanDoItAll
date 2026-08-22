namespace CanDoItAll.Modules.Workspace.ApiAccess;

public static class ApiAccessScopeNames
{
    public const string Api = "api";

    public const string IssueTokens = "api.tokens.issue";

    public const string ReadMemoryProviders = "api.memory-providers.read";

    public const string WriteMemoryProviders = "api.memory-providers.write";

    public const string QueryMemoryProviders = "api.memory-providers.query";

    public const string WriteProjectStructure = "api.project-structure.write";

    public const string ReadLlmChats = "api.llm-chats.read";

    public const string ManageLlmChats = "api.llm-chats.manage";

    public const string ExecuteLlmChats = "api.llm-chats.execute";

    public const string RespondWorkflows = "api.workflows.respond";
}
