using CanDoItAll.Modules.AgentFramework.Pages.Navigation;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages;

public partial class AgentsHomePage {
    [SupplyParameterFromQuery(Name = AgentsUrlCodec.TabKey)] public string? TabQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsUrlCodec.AgentKey)] public string? AgentIdQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsUrlCodec.AgentSectionKey)] public string? AgentSectionQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsUrlCodec.TeamKey)] public string? TeamIdQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsUrlCodec.SimpleChatViewKey)] public string? SimpleChatViewQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsUrlCodec.DefinitionKey)] public string? DefinitionIdQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsUrlCodec.ConversationKey)] public string? ConversationIdQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsUrlCodec.UsageScopeKey)] public string? UsageScopeQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsUrlCodec.UsagePeriodKey)] public string? UsagePeriodQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsUrlCodec.ProviderKey)] public string? ProviderIdQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsUrlCodec.ProviderSectionKey)] public string? ProviderSectionQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsUrlCodec.DialogKey)] public string? DialogQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsHistoryUrl.RangeKey)] public string? HistoryRangeQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsHistoryUrl.ToKey)] public string? HistoryToQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsHistoryUrl.FromKey)] public string? HistoryFromQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsHistoryUrl.ModelKey)] public string? HistoryModelQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsHistoryUrl.WorkloadKey)] public string? HistoryWorkloadQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsHistoryUrl.OperationKey)] public string? HistoryOperationQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsHistoryUrl.OutcomeKey)] public string? HistoryOutcomeQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsHistoryUrl.PriceKey)] public string? HistoryPriceQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsHistoryUrl.CredentialKey)] public string? HistoryCredentialQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsHistoryUrl.RequestKey)] public string? HistoryRequestQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsHistoryUrl.AttemptKey)] public string? HistoryAttemptQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsHistoryUrl.CorrelationKey)] public string? HistoryCorrelationQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsHistoryUrl.SizeKey)] public string? HistorySizeQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsHistoryUrl.CursorKey)] public string? HistoryCursorQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsHistoryUrl.PageKey)] public string? HistoryPageQuery { get; set; }
    [SupplyParameterFromQuery(Name = AgentsHistoryUrl.EntryKey)] public string? HistoryEntryQuery { get; set; }
}
