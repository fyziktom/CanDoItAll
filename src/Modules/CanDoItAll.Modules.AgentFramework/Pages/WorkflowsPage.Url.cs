using CanDoItAll.Modules.AgentFramework.Pages.Navigation;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages;

public partial class WorkflowsPage {
    [SupplyParameterFromQuery(Name = WorkflowsUrlCodec.TabKey)] public string? TabQuery { get; set; }
    [SupplyParameterFromQuery(Name = WorkflowsUrlCodec.ProjectKey)] public string? ProjectIdQuery { get; set; }
    [SupplyParameterFromQuery(Name = WorkflowsUrlCodec.WorkflowKey)] public string? WorkflowIdQuery { get; set; }
    [SupplyParameterFromQuery(Name = WorkflowsUrlCodec.RunKey)] public string? RunIdQuery { get; set; }
    [SupplyParameterFromQuery(Name = WorkflowsUrlCodec.DialogKey)] public string? DialogQuery { get; set; }
}
