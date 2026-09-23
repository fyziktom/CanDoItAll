using CanDoItAll.AgentFramework.UI.Governance;
using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class AgentGovernancePanel : IDisposable {
    [Parameter]
    public Guid? PreferredAgentId { get; set; }

    [Parameter]
    public EventCallback<AgentDefinition?> SelectedAgentChanged { get; set; }

    [Parameter]
    public EventCallback<AgentChatContextAccessState> ContextAccessStateChanged { get; set; }

    [Inject]
    public IAgentGovernanceReads Reads { get; set; } = default!;

    [Inject]
    public ILogger<AgentGovernanceSession> Logger { get; set; } = default!;

    private AgentGovernanceSession session = default!;
    private GovernancePresentation presentation = GovernancePresentation.Empty;
    private GovernanceViewState state = GovernanceViewState.Initial;
    private AgentChatContextAccessState? publishedAccess;
    private long? publishedAccessRevision;
    private long? publishedAgentObservation;
    private bool disposed;
    private bool preferredApplied;
    private Guid? appliedPreferred;

    protected override void OnInitialized() => session = new(Reads, Logger, PublishAsync);

    protected override Task OnParametersSetAsync() {
        if (preferredApplied && appliedPreferred == PreferredAgentId) {
            return Task.CompletedTask;
        }
        preferredApplied = true;
        appliedPreferred = PreferredAgentId;
        return session.SetAgentAsync(PreferredAgentId);
    }

    private Task HandleIntentAsync(GovernanceIntent intent) => intent switch {
        GovernanceIntent.SelectAgent select => session.SetAgentAsync(select.AgentId),
        GovernanceIntent.SelectRun select => session.SelectRunAsync(select.RunId),
        GovernanceIntent.Refresh => session.RefreshAsync(),
        GovernanceIntent.RetryCatalog => session.RetryCatalogAsync(),
        GovernanceIntent.RetryList => session.RetryRunsAsync(),
        GovernanceIntent.RetryDetail => session.RetryDetailAsync(),
        _ => throw new ArgumentOutOfRangeException(nameof(intent))
    };

    private Task PublishAsync() => InvokeAsync(async () => {
        if (disposed) {
            return;
        }
        var revision = session.TargetRevision;
        var observation = session.AcceptedAgentObservationRevision;
        var acceptedAgent = session.AcceptedAgent;
        string AgentLabel(Guid id) => session.Agents.FirstOrDefault(agent => agent.Id == id)?.Name ?? "Unavailable agent";
        presentation = new(session.Agents.OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase)
                .Select(agent => new GovernanceAgentOption(agent.Id, GovernancePresentationMapping.Text(agent.Name))).ToImmutableArray(),
            session.Runs.Select(run => GovernancePresentationMapping.Run(run, AgentLabel(run.AgentId))).ToImmutableArray(),
            session.Detail is { } detail ? GovernancePresentationMapping.Detail(detail, AgentLabel(detail.Run.AgentId)) : null);
        state = new(session.DesiredAgentId, session.AcceptedAgentId, session.AgentResolved, session.DesiredRunId,
            session.AcceptedRunId, session.CatalogState, session.ListState, session.DetailState);
        StateHasChanged();
        if (publishedAccessRevision != revision || publishedAccess != session.AccessState) {
            publishedAccessRevision = revision;
            publishedAccess = session.AccessState;
            await ContextAccessStateChanged.InvokeAsync(publishedAccess.Value);
        }
        if (disposed || revision != session.TargetRevision || observation != session.AcceptedAgentObservationRevision || !session.AgentResolved) {
            return;
        }
        if (publishedAgentObservation != observation) {
            publishedAgentObservation = observation;
            await SelectedAgentChanged.InvokeAsync(acceptedAgent);
        }
    });

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        session.Dispose();
    }
}
