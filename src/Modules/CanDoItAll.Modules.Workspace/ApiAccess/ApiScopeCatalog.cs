using System.ComponentModel;

namespace CanDoItAll.Modules.Workspace.ApiAccess;

[Description("One server-defined capability and the credential kinds to which an operator may assign it.")]
public sealed record ApiScopeDefinition(
    [property: Description("Canonical capability identifier used in grant requests and authorization checks.")] string Name,
    [property: Description("Human-readable capability label.")] string Label,
    [property: Description("Behavior this capability permits and any additional authority requirements.")] string Description,
    [property: Description("Whether this capability can be assigned to an ordinary account.")] bool UserSelectable = true,
    [property: Description("Whether the capability permits a sensitive write, execution or privileged read.")] bool Sensitive = false,
    [property: Description("Whether this capability can be selected when issuing a machine credential.")] bool MachineSelectable = true) {
    [Description("API section used to group capabilities in the operator picker.")]
    public string Section => Name switch {
        ApiAccessScopeNames.Api => "general",
        ApiAccessScopeNames.ReviewAgentRecruiting => "agents",
        _ => Name.Split('.')[1]
    };
}

public static class ApiScopeCatalog {
    public static IReadOnlyList<ApiScopeDefinition> All { get; } = [
        new(ApiAccessScopeNames.ReadRuntime, "Read runtime status", "Read host capabilities and probe runtime readiness."),
        new(ApiAccessScopeNames.Session, "Current session", "Self-session operations only.", UserSelectable: false, MachineSelectable: false),
        new(ApiAccessScopeNames.ManageAccess, "Access administration", "Reserved for the validated configured administrator.", UserSelectable: false, Sensitive: true, MachineSelectable: false),
        new(ApiAccessScopeNames.Api, "Broad ordinary API compatibility", "Ordinary API access where supported; never administrative authority.", UserSelectable: false),
        new(ApiAccessScopeNames.IssueTokens, "Legacy issuance scope", "Compatibility label only. HTTP administration requires a configured administrator session.", UserSelectable: false, Sensitive: true),
        new(ApiAccessScopeNames.ReadProjects, "Read Projects", "Read this API section.", Sensitive: false),
        new(ApiAccessScopeNames.WriteProjects, "Write Projects", "Change this section's shared configuration or records.", Sensitive: true),
        new(ApiAccessScopeNames.ReadAgents, "Read Agents", "Read this API section.", Sensitive: false),
        new(ApiAccessScopeNames.WriteAgents, "Write Agents", "Change this section's shared configuration or records.", Sensitive: true),
        new(ApiAccessScopeNames.ExecuteAgents, "Execute Agents", "Execute configured operations; existing tool and approval policies still apply.", Sensitive: true),
        new(ApiAccessScopeNames.ReviewAgentRecruiting, "Review agent recruiting", "Append an authenticated human recruiting decision. Does not activate an agent; the exact reviewer grant remains required.", Sensitive: true),
        new(ApiAccessScopeNames.ReadWorkflows, "Read Workflows", "Read this API section.", Sensitive: false),
        new(ApiAccessScopeNames.WriteWorkflows, "Write Workflows", "Change this section's shared configuration or records.", Sensitive: true),
        new(ApiAccessScopeNames.ExecuteWorkflows, "Execute Workflows", "Execute configured operations; existing tool and approval policies still apply.", Sensitive: true),
        new(ApiAccessScopeNames.ReadProcesses, "Read Processes", "Read this API section.", Sensitive: false),
        new(ApiAccessScopeNames.WriteProcesses, "Write Processes", "Change this section's shared configuration or records.", Sensitive: true),
        new(ApiAccessScopeNames.ExecuteProcesses, "Execute Processes", "Execute configured operations; existing tool and approval policies still apply.", Sensitive: true),
        new(ApiAccessScopeNames.ReadPrompts, "Read Prompts", "Read this API section.", Sensitive: false),
        new(ApiAccessScopeNames.WritePrompts, "Write Prompts", "Change this section's shared configuration or records.", Sensitive: true),
        new(ApiAccessScopeNames.ReadCrmHr, "Read CRM / HR", "Read this API section.", Sensitive: false),
        new(ApiAccessScopeNames.WriteCrmHr, "Write CRM / HR", "Change this section's shared configuration or records.", Sensitive: true),
        new(ApiAccessScopeNames.ReadPlugins, "Read Plugins", "Read this API section.", Sensitive: false),
        new(ApiAccessScopeNames.WritePlugins, "Write Plugins", "Change this section's shared configuration or records.", Sensitive: true),
        new(ApiAccessScopeNames.ReadWorkspaceSettings, "Read workspace settings", "Read this API section.", Sensitive: false),
        new(ApiAccessScopeNames.WriteWorkspaceSettings, "Write workspace settings", "Change this section's shared configuration or records.", Sensitive: true),
        new(ApiAccessScopeNames.ReadMemoryProviders, "Read memory providers", "View memory provider configuration and status."),
        new(ApiAccessScopeNames.WriteMemoryProviders, "Manage memory providers", "Create and update memory provider configuration.", Sensitive: true),
        new(ApiAccessScopeNames.QueryMemoryProviders, "Query memory providers", "Query memory provider content.", Sensitive: true),
        new(ApiAccessScopeNames.WriteProjectStructure, "Project structure", "Write project structures and acquire editing leases.", Sensitive: true),
        new(ApiAccessScopeNames.ReadLlmChats, "Read Simple Chats", "View reusable chat definitions and chat state."),
        new(ApiAccessScopeNames.ManageLlmChats, "Manage Simple Chats", "Create and update reusable chat definitions.", Sensitive: true),
        new(ApiAccessScopeNames.ExecuteLlmChats, "Execute Simple Chats", "Start and respond to chat turns.", Sensitive: true),
        new(ApiAccessScopeNames.RespondWorkflows, "Respond to workflows", "Submit human responses to workflow requests.", Sensitive: true),
        new(ApiAccessScopeNames.ReadSharedProviderCatalog, "Discover shared providers", "Read the shared provider catalog."),
        new(ApiAccessScopeNames.InvokeSharedProviders, "Use shared providers", "Invoke published providers for chat and images.", Sensitive: true),
        new(ApiAccessScopeNames.ReadStoragePlacementRecovery, "Read Storage recovery", "Read safe placement status with current project read permission; does not expose content or storage addresses."),
        new(ApiAccessScopeNames.ReconcileStoragePlacement, "Reconcile Storage placements", "Verify the original prepared target with current project write permission; never starts another upload or native effect.", Sensitive: true),
        new(ApiAccessScopeNames.VerifyStorageExternalTermination, "Verify external dispatch termination", "Attest that an unresolved FTP dispatch has stopped before exact readback; grant only to responsible operators.", Sensitive: true),
        new(ApiAccessScopeNames.ReadProviderHistory, "Read provider history", "Search request metadata in this database; does not grant content access."),
        new(ApiAccessScopeNames.ReadProviderHistoryContent, "Read provider history content", "Read retained request content with metadata and canonical-owner permission.", Sensitive: true),
        new(ApiAccessScopeNames.ManageProviderHistory, "Manage provider history", "Read and apply this database's history retention and capture policy.", Sensitive: true)
    ];

    public static List<string> Parse(string text) => text
        .Split([' ', ',', ';', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(scope => scope, StringComparer.OrdinalIgnoreCase).ToList();

    public static IReadOnlyList<string> ValidateGrants(IReadOnlyCollection<string>? values, bool forUser) {
        if (values is null || values.Count > All.Count) {
            throw new InvalidOperationException("Supply a bounded list of API capabilities.");
        }
        var result = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var value in values) {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128) {
                throw new InvalidOperationException("API capabilities must be nonempty catalog names.");
            }
            var definition = All.FirstOrDefault(item => string.Equals(item.Name, value.Trim(), StringComparison.OrdinalIgnoreCase));
            if (definition is null || (forUser ? !definition.UserSelectable : !definition.MachineSelectable)) {
                throw new InvalidOperationException("An API capability is unknown or unavailable for this credential kind.");
            }
            result.Add(definition.Name);
        }
        return result.ToArray();
    }
}

public static class ApiManagedTokenClaims {
    public const string Version = "cda_token_version";
    public const string CurrentVersion = "1";
    public const string SessionVersion = "2";
    public const string Kind = "cda_credential_kind";
    public const string UserId = "cda_user_id";
    public const string AuthenticationRevision = "cda_auth_revision";
    public const string TokenId = "jti";
}
