using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AppComponents;
using CanDoItAll.CrmHr.UI.Agents;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UiSandbox;

// The Agents workspace surface has no create action (agents are provisioned only in AgentFramework) and its record
// dialog owns its own tab index internally, so this scenario set has no NewDraft or NonDefaultSection token; see the
// sandbox README for the exact contract reason.
public enum CrmHrAgentsWorkspaceSandboxScenario
{
    Catalog,
    SelectedExisting,
    Loading,
    UnavailableReferences,
    Empty
}

public sealed record CrmHrAgentsWorkspaceSandboxContext(
    CrmHrAgentsWorkspaceSandboxScenario Scenario = CrmHrAgentsWorkspaceSandboxScenario.Catalog,
    CrmHrHomeSandboxLayout Layout = CrmHrHomeSandboxLayout.Matched)
{
    public static CrmHrAgentsWorkspaceSandboxContext Parse(string? scenario, string? layout)
        => new(ParseScenario(scenario), CrmHrHomeSandboxContext.ParseLayout(layout));

    public static CrmHrAgentsWorkspaceSandboxScenario ParseScenario(string? token)
        => Enum.GetValues<CrmHrAgentsWorkspaceSandboxScenario>()
            .FirstOrDefault(scenario => string.Equals(Token(scenario), token?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Token(CrmHrAgentsWorkspaceSandboxScenario scenario)
        => string.Concat(scenario.ToString().Select((character, index) =>
            char.IsUpper(character) && index > 0 ? $"-{char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));

    public IReadOnlyDictionary<string, object?> ToQuery() => new Dictionary<string, object?>
    {
        ["scenario"] = Token(Scenario),
        ["layout"] = CrmHrHomeSandboxContext.Token(Layout)
    };
}

// Deterministic local state for the real Agents workspace surface: a small projected agent catalog this view owns
// end to end (LoadAgentPageAsync pages the same local list). Nothing here calls a query service, persists, navigates
// or reaches a live agent context.
public sealed class CrmHrAgentsSandboxView : ICrmHrAgentsWorkspaceView
{
    private static readonly ProviderProfile SandboxProvider = new(
        CrmHrWorkspaceSandboxIds.Id("sandbox-provider"), "Northwind Workspace Provider", ProviderKind.AzureOpenAi,
        "https://sandbox.example.test", "SANDBOX_PROVIDER_KEY", "gpt-5-mini", ProviderTransportKind.Responses,
        true, true, true, true, false, "{}", "Synthetic sandbox provider.", "Healthy", CrmHrSandboxData.BaseUpdatedAtUtc, ["gpt-5-mini"]);

    private IReadOnlyList<AiAgentDirectoryItemModel> items = BuildItems();

    public Action? RenderRequested { get; set; }

    public string IntentLog { get; private set; } = "No intent yet.";

    public AiAgentDirectoryPage AgentPage { get; private set; } = new([], 0, AiAgentDirectoryQueryLimits.DefaultPageSize, 0);

    public AiAgentDirectoryItemModel? SelectedDirectoryItem { get; private set; }

    public AiAgentWorkspaceModel? SelectedWorkspace { get; private set; }

    public bool IsWorkspaceLoading { get; private set; }

    public bool IsRecordDialogOpen { get; private set; }

    public PagedRecordSelection<Guid>? AgentSelection => SelectedDirectoryItem is { } item ? new PagedRecordSelection<Guid>(item.PartyId) : null;

    public bool ShouldShowListLoadingState { get; private set; }

    public void RequestRender() => RenderRequested?.Invoke();

    // Host-only navigation: the secondary tabs slot only logs the destination in this sandbox.
    public void LogNavigation(string route) => Log($"Navigate: {route}");

    public Task<PagedRecordPage<Guid>> LoadAgentPageAsync(PagedRecordRequest<AiAgentValidationFilter> request, CancellationToken cancellationToken)
    {
        var matches = items.Where(item => request.Filter == AiAgentValidationFilter.All || Matches(item.Governance.ValidationStatus, request.Filter));
        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var searchText = request.SearchText.Trim();
            matches = matches.Where(item => item.Agent.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                item.Agent.RoleTitle.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = matches.OrderBy(item => item.Agent.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var pageSize = request.PageSize <= 0 ? AiAgentDirectoryQueryLimits.DefaultPageSize : request.PageSize;
        var page = ordered.Skip(request.PageIndex * pageSize).Take(pageSize)
            .Select(item => new PagedRecordOption<Guid>(item.PartyId, item.Agent.Name, item.Agent.RoleTitle) { TestId = "crmhr-agent-item" })
            .ToList();
        AgentPage = new AiAgentDirectoryPage(items.ToList(), request.PageIndex, pageSize, items.Count);
        return Task.FromResult(new PagedRecordPage<Guid>(page, request.PageIndex, pageSize, ordered.Count));
    }

    public void HandleAgentDirectoryLoadFailed(Exception _) => Log("Agent directory load failed");

    public AiAgentDirectoryItemModel GetCurrentPageItem(Guid partyId)
        => items.First(item => item.PartyId == partyId);

    public Task SelectPartyAsync(Guid partyId)
    {
        var item = items.FirstOrDefault(candidate => candidate.PartyId == partyId);
        if (item is null)
        {
            Log($"Select agent ignored: {partyId:D}");
            return Task.CompletedTask;
        }

        SelectedDirectoryItem = item;
        IsRecordDialogOpen = true;
        SelectedWorkspace = new AiAgentWorkspaceModel(
            item.PartyId, item.Agent.Name, item.Agent.Summary, item.Governance.LifecycleStatus, "", "",
            item.Governance.BindingStatus == AiResourceBindingStatus.Bound ? item.Agent.Id : null, item.Governance.BindingStatus,
            item.Governance.BindingReason, "/agents", item.ProviderName, item.Governance.OwnerName, item.CapabilityCount,
            new AiAgentProfileEditorModel
            {
                PartyId = item.PartyId,
                ProviderProfileId = item.Agent.ProviderProfileId,
                DefaultModel = item.Agent.Model,
                ExecutionMode = item.Governance.ExecutionMode ?? AiExecutionMode.Remote,
                ValidationStatus = item.Governance.ValidationStatus,
                Notes = $"Synthetic governance note for {item.Agent.Name}."
            });
        Log($"Select agent: {item.Agent.Name}");
        return Task.CompletedTask;
    }

    public Task CloseRecordDialogAsync()
    {
        IsRecordDialogOpen = false;
        Log("Close agent record dialog");
        return Task.CompletedTask;
    }

    public void OpenTechnicalCatalog() => Log("Open technical catalog");

    public void OpenTechnicalRecord() => Log($"Open technical record: {SelectedDirectoryItem?.Agent.Name}");

    public void OpenDirectoryRecord() => Log($"Open directory record: {SelectedDirectoryItem?.Agent.Name}");

    public void Apply(CrmHrAgentsWorkspaceSandboxScenario next)
    {
        IntentLog = "No intent yet.";
        IsRecordDialogOpen = false;
        SelectedDirectoryItem = null;
        SelectedWorkspace = null;
        IsWorkspaceLoading = false;
        ShouldShowListLoadingState = false;
        items = next == CrmHrAgentsWorkspaceSandboxScenario.Empty ? [] : BuildItems();
        AgentPage = new AiAgentDirectoryPage(items.ToList(), 0, AiAgentDirectoryQueryLimits.DefaultPageSize, items.Count);

        switch (next)
        {
            case CrmHrAgentsWorkspaceSandboxScenario.SelectedExisting:
                _ = SelectPartyAsync(CrmHrSandboxData.AtlasOpsAgent);
                break;
            case CrmHrAgentsWorkspaceSandboxScenario.Loading:
                ShouldShowListLoadingState = true;
                _ = SelectPartyAsync(CrmHrSandboxData.AtlasOpsAgent);
                IsWorkspaceLoading = true;
                SelectedWorkspace = null;
                break;
            case CrmHrAgentsWorkspaceSandboxScenario.UnavailableReferences:
                _ = SelectPartyAsync(CrmHrSandboxData.SchedulerAgent);
                break;
            default:
                break;
        }
    }

    private static bool Matches(AiValidationStatus status, AiAgentValidationFilter filter)
        => filter switch
        {
            AiAgentValidationFilter.Draft => status == AiValidationStatus.Draft,
            AiAgentValidationFilter.ReviewRequired => status == AiValidationStatus.ReviewRequired,
            AiAgentValidationFilter.Approved => status == AiValidationStatus.Approved,
            AiAgentValidationFilter.Suspended => status == AiValidationStatus.Suspended,
            _ => true
        };

    private static IReadOnlyList<AiAgentDirectoryItemModel> BuildItems() =>
    [
        BuildItem(CrmHrSandboxData.AtlasOpsAgent, "Atlas Ops Agent", "Operations Copilot",
            "Operations copilot bound to a workspace agent.", AiValidationStatus.Approved, AiResourceBindingStatus.Bound,
            "Marcus Chen", bound: true),
        BuildItem(CrmHrSandboxData.LedgerInsightsAgent, "Ledger Insights Agent", "Finance Analyst Copilot",
            "Finance analytics agent bound to a workspace agent.", AiValidationStatus.Approved, AiResourceBindingStatus.Bound,
            "Victor Hughes", bound: true),
        BuildItem(CrmHrSandboxData.SupportCopilotAgent, "Support Copilot Agent", "Support Copilot",
            "Support desk copilot awaiting governance review.", AiValidationStatus.ReviewRequired, AiResourceBindingStatus.Bound,
            "Noah Fischer", bound: true),
        BuildItem(CrmHrSandboxData.SchedulerAgent, "Scheduler Agent", "Scheduling Assistant",
            "Draft scheduling agent; not yet technically bound.", AiValidationStatus.Draft, AiResourceBindingStatus.Unbound,
            "Priya Natarajan", bound: false)
    ];

    private static AiAgentDirectoryItemModel BuildItem(Guid partyId, string name, string roleTitle, string summary,
        AiValidationStatus validationStatus, AiResourceBindingStatus bindingStatus, string ownerName, bool bound)
    {
        var agent = new AgentDefinition(
            partyId, name, roleTitle, summary, $"You are {name}, a {roleTitle.ToLowerInvariant()}.", AgentLifecycleStatus.Active,
            bound ? SandboxProvider.Id : null, bound ? SandboxProvider.DefaultModel : "", AgentWorkloadKind.Support,
            AgentChatHistoryMode.FrameworkManaged, 0.3, false, false, "{}", false, "", AgentPermissionsPolicy.Default,
            [new AgentCapabilityAssignment(CrmHrWorkspaceSandboxIds.Id($"capability-{partyId}"), "workspace-tools", CapabilityKind.Tool,
                CapabilityProofStatus.Verified, CrmHrSandboxData.BaseUpdatedAtUtc, "")],
            ["CrmHr"], CrmHrSandboxData.BaseUpdatedAtUtc, CrmHrSandboxData.BaseUpdatedAtUtc);
        var governance = new AiAgentDirectoryGovernanceModel(
            PartyLifecycleStatus.Active, false, bindingStatus,
            bound ? "Bound to a workspace agent." : "No technical binding yet.", bound ? AiExecutionMode.Remote : null,
            true, validationStatus, ownerName, CrmHrSandboxData.BaseUpdatedAtUtc);
        return new AiAgentDirectoryItemModel(partyId, agent, governance, bound ? SandboxProvider : null, CrmHrSandboxData.BaseUpdatedAtUtc);
    }

    private void Log(string message) => IntentLog = message;
}
