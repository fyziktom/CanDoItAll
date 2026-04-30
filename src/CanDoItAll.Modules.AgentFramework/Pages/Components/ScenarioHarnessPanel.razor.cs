using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Hosting;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class ScenarioHarnessPanel
{
    [Inject]
    private ScenarioHarnessService ScenarioService { get; set; } = default!;

    [Inject]
    private NotificationService NotificationService { get; set; } = default!;

    private IReadOnlyList<ScenarioHarnessDefinition> Definitions { get; set; } = [];
    private readonly Dictionary<string, ScenarioHarnessSnapshot> scenarioSnapshots = new(StringComparer.OrdinalIgnoreCase);
    private Guid scenarioAgentId;
    private string selectedScenarioId = "SC03";
    private bool isBusy;
    private int selectedTabIndex;

    private ScenarioHarnessDefinition? SelectedDefinition => Definitions.FirstOrDefault(item =>
        string.Equals(item.Id, selectedScenarioId, StringComparison.OrdinalIgnoreCase));

    private ScenarioHarnessSnapshot? SelectedSnapshot => TryGetSnapshot(selectedScenarioId);

    protected override async Task OnInitializedAsync()
    {
        Definitions = ScenarioService.Definitions;
        var context = await ScenarioService.EnsureScenarioCatalogAsync();
        scenarioAgentId = context.AgentId;
        await RefreshAllScenarioSnapshotsAsync();
    }

    private async Task RefreshAllScenarioSnapshotsAsync()
    {
        foreach (var definition in Definitions)
        {
            scenarioSnapshots[definition.Id] = await ScenarioService.LoadScenarioSnapshotAsync(scenarioAgentId, definition.Id);
        }
    }

    private async Task SelectScenarioAsync(string scenarioId)
    {
        selectedScenarioId = scenarioId;
        await RefreshScenarioAsync();
    }

    private async Task RefreshScenarioAsync()
    {
        if (string.IsNullOrWhiteSpace(selectedScenarioId))
        {
            return;
        }

        scenarioSnapshots[selectedScenarioId] = await ScenarioService.LoadScenarioSnapshotAsync(scenarioAgentId, selectedScenarioId);
    }

    private async Task RunSelectedScenarioAsync()
    {
        if (SelectedDefinition is null)
        {
            return;
        }

        isBusy = true;
        try
        {
            scenarioSnapshots[selectedScenarioId] = await ScenarioService.RunScenarioAsync(scenarioAgentId, selectedScenarioId);
            var snapshot = scenarioSnapshots[selectedScenarioId];
            if (snapshot.HasPendingApprovals)
            {
                Notify("warning", $"{selectedScenarioId} is waiting on approval before the evidence pack can finish.");
            }
            else if (snapshot.LatestRun?.State == ExecutionState.Completed)
            {
                Notify("success", $"{selectedScenarioId} finished and refreshed its evidence pack.");
            }
            else
            {
                Notify("info", $"{selectedScenarioId} refreshed its latest scenario state.");
            }
        }
        catch (Exception exception)
        {
            Notify("danger", exception.Message);
            await RefreshScenarioAsync();
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task ApproveSelectedScenarioAsync()
    {
        await ContinueSelectedScenarioAsync(approved: true);
    }

    private async Task RejectSelectedScenarioAsync()
    {
        await ContinueSelectedScenarioAsync(approved: false);
    }

    private async Task ContinueSelectedScenarioAsync(bool approved)
    {
        if (SelectedSnapshot?.LatestRun is null)
        {
            return;
        }

        isBusy = true;
        try
        {
            scenarioSnapshots[selectedScenarioId] = await ScenarioService.ContinueScenarioAsync(
                scenarioAgentId,
                selectedScenarioId,
                SelectedSnapshot.LatestRun.Id,
                approved);

            Notify(
                approved ? "success" : "warning",
                approved
                    ? $"{selectedScenarioId} resumed after approval."
                    : $"{selectedScenarioId} was rejected and refreshed its evidence state.");
        }
        catch (Exception exception)
        {
            Notify("danger", exception.Message);
            await RefreshScenarioAsync();
        }
        finally
        {
            isBusy = false;
        }
    }

    private Task HandleSelectedTabIndexChanged(int value)
    {
        selectedTabIndex = value;
        return Task.CompletedTask;
    }

    private ScenarioHarnessSnapshot? TryGetSnapshot(string scenarioId)
        => scenarioSnapshots.TryGetValue(scenarioId, out var snapshot) ? snapshot : null;

    private static string ResolveScenarioStatus(ScenarioHarnessSnapshot? snapshot)
        => snapshot?.LatestStatusLabel ?? "Not run yet";

    private static string ResolveScenarioStatusTone(ScenarioHarnessSnapshot? snapshot)
    {
        if (snapshot?.LatestRun is null)
        {
            return "neutral";
        }

        return snapshot.LatestRun.State switch
        {
            ExecutionState.Completed => "success",
            ExecutionState.WaitingOnTool => "warning",
            ExecutionState.Failed => "danger",
            _ => "info"
        };
    }

    private void Notify(string tone, string message)
    {
        switch (tone)
        {
            case "success":
                NotificationService.Success("Ready", message);
                break;
            case "warning":
                NotificationService.Warning("Heads up", message);
                break;
            case "danger":
                NotificationService.Error("Attention", message);
                break;
            default:
                NotificationService.Info("Info", message);
                break;
        }
    }
}
