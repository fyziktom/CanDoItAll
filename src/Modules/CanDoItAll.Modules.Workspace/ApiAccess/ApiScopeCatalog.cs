namespace CanDoItAll.Modules.Workspace.ApiAccess;

public sealed record ApiScopeDefinition(string Name, string Label, string Description);

public static class ApiScopeCatalog {
    public static IReadOnlyList<ApiScopeDefinition> All { get; } = [
        new(ApiAccessScopeNames.Api, "General API", "General API access; explicit privileged scopes remain separate."),
        new(ApiAccessScopeNames.IssueTokens, "Token administration", "Create and manage API tokens. Grant only to administrators."),
        new(ApiAccessScopeNames.ReadMemoryProviders, "Read memory providers", "View memory provider configuration and status."),
        new(ApiAccessScopeNames.WriteMemoryProviders, "Manage memory providers", "Create and update memory provider configuration."),
        new(ApiAccessScopeNames.QueryMemoryProviders, "Query memory providers", "Query memory provider content."),
        new(ApiAccessScopeNames.WriteProjectStructure, "Project structure", "Write project structures and acquire editing leases."),
        new(ApiAccessScopeNames.ReadLlmChats, "Read Simple Chats", "View reusable chat definitions and chat state."),
        new(ApiAccessScopeNames.ManageLlmChats, "Manage Simple Chats", "Create and update reusable chat definitions."),
        new(ApiAccessScopeNames.ExecuteLlmChats, "Execute Simple Chats", "Start and respond to chat turns."),
        new(ApiAccessScopeNames.RespondWorkflows, "Respond to workflows", "Submit human responses to workflow requests."),
        new(ApiAccessScopeNames.ReadSharedProviderCatalog, "Discover shared providers", "Read the shared provider catalog."),
        new(ApiAccessScopeNames.InvokeSharedProviders, "Use shared providers", "Invoke published providers for chat and images.")
    ];

    public static List<string> Parse(string text) => text
        .Split([' ', ',', ';', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(scope => scope, StringComparer.OrdinalIgnoreCase).ToList();
}

public static class ApiManagedTokenClaims {
    public const string Version = "cda_token_version";
    public const string CurrentVersion = "1";
    public const string TokenId = "jti";
}
