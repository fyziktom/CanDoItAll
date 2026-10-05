using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench.ProjectStructure;
using CanDoItAll.Workbench.Planning.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectCalendarPage : IDisposable {
    [Inject] private ProjectWorkbenchService Workbench { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IAgentChatContextRegistry ContextRegistry { get; set; } = default!;
    [Inject] private ILogger<ProjectCalendarPage> Logger { get; set; } = default!;
    [Parameter] public Guid ProjectId { get; set; }

    private CalendarPresentation presentation = new(Guid.NewGuid());
    private ProjectCalendarSurface? surface;
    private ProjectCalendarViewState viewState = ProjectCalendarViewState.Empty;
    private AgentChatContextAccessState agentChatContextAccessState = AgentChatContextAccessState.Loading;
    private Guid? selectedEventId;
    private Guid? requestedProjectId;
    private CancellationTokenSource? readCancellation;
    private Task viewWriteTail = Task.CompletedTask;
    private bool disposed;

    private ProjectCalendarEvent? SelectedEvent => surface?.Events.FirstOrDefault(item => item.Id == selectedEventId);
    private IReadOnlyList<AgentChatContextEntityReference> SelectedAgentContextEntities
        => SelectedEvent is { NodeKey.Length: > 0 } selected
            ? [new("project-node", selected.NodeKey, selected.Title)]
            : [];
    private AgentChatNavigationIdentity AgentChatNavigationFence
        => AgentChatNavigationIdentity.CreateForLocation(Navigation.BaseUri, Navigation.Uri);

    protected override Task OnParametersSetAsync() {
        if (disposed || requestedProjectId == ProjectId) {
            return Task.CompletedTask;
        }
        requestedProjectId = ProjectId;
        return LoadAsync(preserveAccepted: false);
    }

    private async Task LoadAsync(bool preserveAccepted) {
        RetireRead();
        using var cancellation = new CancellationTokenSource();
        readCancellation = cancellation;
        var projectId = ProjectId;
        var retained = preserveAccepted ? surface : null;
        var retainedPresentation = preserveAccepted ? presentation : null;
        var preferredSelection = preserveAccepted ? selectedEventId : null;
        var origin = Guid.NewGuid();
        presentation = new(origin);
        surface = retained;
        if (!preserveAccepted) {
            selectedEventId = null;
            viewState = ProjectCalendarViewState.Empty;
        }
        agentChatContextAccessState = AgentChatContextAccessState.Loading;
        try {
            var result = await Workbench.TryGetCalendarAsync(projectId, cancellation.Token);
            if (!IsCurrent(origin)) {
                return;
            }
            if (result.Surface is not { } accepted) {
                surface = null;
                selectedEventId = null;
                presentation = new(origin) {
                    State = PlanningReadState.Unavailable,
                    Error = result.UnavailableState?.Description ?? "The requested project is unavailable in the active database profile."
                };
                agentChatContextAccessState = AgentChatContextAccessState.Failed;
                return;
            }
            ProjectAssignmentAdmission.Require(projectId, accepted.ExpectedProjectAdmission);
            if (retained?.ExpectedProjectAdmission != accepted.ExpectedProjectAdmission) {
                preferredSelection = null;
            }
            var state = ProjectCalendarStateParser.Parse(accepted.ViewStateJson);
            var selection = ResolveSelection(accepted, preferredSelection, state.SelectedEventId);
            state = state with { SelectedEventId = selection };
            var next = ProjectCalendarPresentationAdapter.Build(accepted, state, origin);
            surface = accepted;
            viewState = state;
            selectedEventId = selection;
            presentation = next;
            agentChatContextAccessState = AgentChatContextAccessState.Ready;
        } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
        } catch (Exception exception) {
            Logger.LogWarning("Calendar read failed for project {ProjectId}; failure type {FailureType}.", projectId, exception.GetType().Name);
            if (IsCurrent(origin)) {
                surface = retained;
                presentation = retainedPresentation is null
                    ? new(origin) { State = PlanningReadState.Failed, Error = "The calendar could not be loaded. Retry to read the current project schedule." }
                    : retainedPresentation with { Origin = origin, State = PlanningReadState.Stale, Error = "The calendar could not be refreshed. The displayed schedule is stale." };
                agentChatContextAccessState = AgentChatContextAccessState.Failed;
            }
        } finally {
            if (ReferenceEquals(readCancellation, cancellation)) {
                readCancellation = null;
            }
        }
    }

    private Task HandleSelectionAsync(CalendarSelectionIntent intent) {
        if (!CanAccept(intent.Origin) || surface is null ||
            intent.EventId is { } id && !surface.Events.Any(item => item.Id == id)) {
            return Task.CompletedTask;
        }
        selectedEventId = intent.EventId;
        viewState = viewState with { SelectedEventId = selectedEventId };
        presentation = ProjectCalendarPresentationAdapter.Build(surface, viewState, intent.Origin) with {
            PersistenceWarning = presentation.PersistenceWarning
        };
        return Task.CompletedTask;
    }

    private async Task HandleStateAsync(CalendarStateIntent intent) {
        if (!CanAccept(intent.Origin) || surface is not { ExpectedProjectAdmission: { } admission } accepted) {
            return;
        }
        var next = ProjectCalendarStateParser.FromStateChanged(intent.State);
        if (next.SelectedEventId is { } id && !accepted.Events.Any(item => item.Id == id)) {
            return;
        }
        try {
            TimeZoneInfo.FindSystemTimeZoneById(next.Timezone);
        } catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException) {
            presentation = presentation with { PersistenceWarning = "The selected timezone is unavailable. The saved view was not changed." };
            return;
        }

        string stateJson;
        try {
            stateJson = ProjectCalendarStateParser.Serialize(next, intent.State.StateJson);
        } catch (JsonException) {
            presentation = presentation with { PersistenceWarning = "The calendar supplied invalid view state. The saved view was not changed." };
            return;
        }
        viewState = next;
        selectedEventId = next.SelectedEventId;
        presentation = ProjectCalendarPresentationAdapter.Build(accepted, next, intent.Origin);
        var predecessor = viewWriteTail;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        viewWriteTail = completion.Task;
        try {
            await predecessor;
            if (!CanAccept(intent.Origin)) {
                return;
            }
            await Workbench.SaveCalendarViewStateAsync(admission, stateJson);
            if (CanAccept(intent.Origin) && ReferenceEquals(viewWriteTail, completion.Task)) {
                surface = accepted with { ViewStateJson = stateJson };
                presentation = presentation with { PersistenceWarning = null };
            }
        } catch (ProjectWriteAdmissionRejectedException) {
            if (IsCurrent(intent.Origin)) {
                presentation = presentation with {
                    State = PlanningReadState.Stale,
                    PersistenceWarning = "The displayed project lifetime was rejected. This view was not saved; refresh before continuing."
                };
                agentChatContextAccessState = AgentChatContextAccessState.Failed;
            }
        } catch (Exception exception) {
            Logger.LogWarning("Calendar view save could not be confirmed for project {ProjectId}, origin {Origin}; failure type {FailureType}.",
                accepted.ProjectId, intent.Origin, exception.GetType().Name);
            if (IsCurrent(intent.Origin)) {
                presentation = presentation with { PersistenceWarning = "The saved view could not be confirmed. Refresh to read its durable state before trying again." };
            }
        } finally {
            completion.SetResult();
        }
    }

    private Task HandleOpenAsync(CalendarOpenIntent intent) {
        if (!CanAccept(intent.Origin)) {
            return Task.CompletedTask;
        }
        var selected = surface?.Events.FirstOrDefault(item => item.Id == intent.EventId);
        if (selected is not null && IsInternalRoute(selected.Route)) {
            Navigation.NavigateTo(selected.Route);
        }
        return Task.CompletedTask;
    }

    private Task HandleRetryAsync(PlanningOriginIntent intent)
        => IsCurrent(intent.Origin) ? LoadAsync(preserveAccepted: surface is not null) : Task.CompletedTask;

    private Task HandleProjectsAsync(PlanningOriginIntent intent) {
        if (IsCurrent(intent.Origin)) {
            Navigation.NavigateTo("/projects");
        }
        return Task.CompletedTask;
    }

    private Task HandleAgentExecutionCompletedAsync(AgentChatExecutionCompleted notification, Guid origin) {
        if (!CanAccept(origin) || ContextRegistry.Capture()?.Scope.Id != notification.ScopeId ||
            notification.Source.Kind.Value != ProjectStructureAgentChatContextBuilder.SourceKind ||
            !Guid.TryParse(notification.Source.Id.Value, out var projectId) || projectId != surface?.ProjectId) {
            return Task.CompletedTask;
        }
        return LoadAsync(preserveAccepted: true);
    }

    private bool IsCurrent(Guid origin) => !disposed && origin == presentation.Origin;
    private bool CanAccept(Guid origin) => IsCurrent(origin) && presentation.State == PlanningReadState.Ready;
    private static bool IsInternalRoute(string route)
        => route.StartsWith('/') && !route.StartsWith("//", StringComparison.Ordinal) && !route.Contains('\\');

    private static Guid? ResolveSelection(ProjectCalendarSurface accepted, Guid? preferred, Guid? fallback)
        => preferred.HasValue && accepted.Events.Any(item => item.Id == preferred)
            ? preferred
            : fallback.HasValue && accepted.Events.Any(item => item.Id == fallback)
                ? fallback
                : accepted.Events.FirstOrDefault()?.Id;

    private void RetireRead() {
        var retired = readCancellation;
        readCancellation = null;
        retired?.Cancel();
    }

    public void Dispose() {
        disposed = true;
        RetireRead();
    }
}
