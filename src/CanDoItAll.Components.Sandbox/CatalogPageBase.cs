using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Components.Sandbox;

public abstract class CatalogPageBase : ComponentBase
{
    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    [Parameter, SupplyParameterFromQuery(Name = "scenario")]
    public string? ScenarioQuery { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "frame")]
    public string? FrameQuery { get; set; }

    protected SandboxScenarioKey CurrentScenario => SandboxScenarioKeyExtensions.Parse(ScenarioQuery);

    protected SandboxFramePreset CurrentFrame => SandboxFramePresetExtensions.Parse(FrameQuery);

    protected void NavigateScenario(SandboxScenarioKey scenario)
    {
        NavigationManager.NavigateTo(BuildUri(scenario, CurrentFrame));
    }

    protected void NavigateFrame(SandboxFramePreset frame)
    {
        NavigationManager.NavigateTo(BuildUri(CurrentScenario, frame));
    }

    protected string BuildUri(SandboxScenarioKey scenario, SandboxFramePreset frame)
    {
        var relativePath = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
        var route = relativePath.Split('?', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        var path = string.IsNullOrWhiteSpace(route)
            ? "/"
            : $"/{route.TrimStart('/')}";
        var query = new List<string>();

        if (scenario != SandboxScenarioKey.HappyPath)
        {
            query.Add($"scenario={Uri.EscapeDataString(scenario.ToSlug())}");
        }

        if (frame != SandboxFramePreset.LiveViewport)
        {
            query.Add($"frame={Uri.EscapeDataString(frame.ToSlug())}");
        }

        return query.Count == 0
            ? path
            : $"{path}?{string.Join("&", query)}";
    }
}
