using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench.ProjectStructure;
using CanDoItAll.Workbench.Planning.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectCalendarPage : IDisposable {
    private const string MissingEventMessage = "The requested calendar event is unavailable in this project. Select another event.";

    [Inject] private ProjectWorkbenchService Workbench { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IAgentChatContextRegistry ContextRegistry { get; set; } = default!;
    [Inject] private TimeProvider TimeProvider { get; set; } = default!;
    [Inject] private ILogger<ProjectCalendarPage> Logger { get; set; } = default!;
    [Inject] private EditingChangeFeed EditingChanges { get; set; } = default!;
    [Inject] private IEditingUiAccess UiAccess { get; set; } = default!;
    [Inject] private ProjectChanges ProjectChanges { get; set; } = default!;
    [Inject] private ProjectWriteAdmissionService Admissions { get; set; } = default!;
    [Inject] private ICanonicalRuntimeDatabase Database { get; set; } = default!;
    [Inject] private ILogger<EditingObservation> EditingLogger { get; set; } = default!;
    [Inject] private NotificationService Notifications { get; set; } = default!;
    [CascadingParameter] public Task<AuthenticationState>? Authentication { get; set; }
    [Parameter] public Guid ProjectId { get; set; }
    [SupplyParameterFromQuery(Name = ProjectCalendarUrlCodec.ViewKey)] public string? ViewQuery { get; set; }
    [SupplyParameterFromQuery(Name = ProjectCalendarUrlCodec.DateKey)] public string? DateQuery { get; set; }
    [SupplyParameterFromQuery(Name = ProjectCalendarUrlCodec.EventKey)] public string? EventQuery { get; set; }

    private UrlStateBinding<ProjectCalendarUrlState> url = default!;
    private CalendarPresentation presentation = new(Guid.NewGuid());
    private ProjectCalendarSurface? surface;
    private ProjectCalendarViewState viewState = ProjectCalendarViewState.Empty;
    private AgentChatContextAccessState agentChatContextAccessState = AgentChatContextAccessState.Loading;
    private Guid? selectedEventId;
    private Guid? requestedProjectId;
    private CancellationTokenSource? readCancellation;
    private Task viewWriteTail = Task.CompletedTask;
    private bool selectionStatePending;
    private bool disposed;
    private Task<AuthenticationState>? observedActor;
    private Task<AuthenticationState>? displayedActor;
    private long displayedGeneration;

    private ProjectCalendarEvent? SelectedEvent => surface?.Events.FirstOrDefault(item => item.Id == selectedEventId);
    private IReadOnlyList<AgentChatContextEntityReference> SelectedAgentContextEntities
        => SelectedEvent is { NodeKey.Length: > 0 } selected
            ? [new("project-node", selected.NodeKey, selected.Title)]
            : [];
    private AgentChatNavigationIdentity AgentChatNavigationFence
        => AgentChatNavigationIdentity.CreateForLocation(Navigation.BaseUri, Navigation.Uri);

    protected override void OnInitialized()
        => url = new(Navigation, ProjectCalendarUrlCodec.Instance, null, typeof(ProjectCalendarPage));

    protected override Task OnParametersSetAsync() {
        if (disposed) {
            return Task.CompletedTask;
        }
        var change = url.Apply(canonicalize: RendererInfo.IsInteractive);
        var actorChanged = !ReferenceEquals(observedActor, Authentication);
        observedActor = Authentication;
        if (requestedProjectId != ProjectId || actorChanged || surface is not null && !OwnsDisplayedCalendar) {
            requestedProjectId = ProjectId;
            return LoadAsync(preserveAccepted: false);
        }
        if (change.IsChanged) {
            ApplyRouteToAcceptedCalendar();
        }
        return Task.CompletedTask;
    }

    private async Task LoadAsync(bool preserveAccepted) {
        RetireRead();
        using var cancellation = new CancellationTokenSource();
        readCancellation = cancellation;
        var projectId = ProjectId;
        var generation = Database.Generation;
        var actor = Authentication;
        var retained = preserveAccepted && OwnsDisplayedCalendar ? surface : null;
        var retainedView = retained is not null ? viewState : null;
        var retainedPresentation = retained is not null ? presentation : null;
        var preferredSelection = retained is not null ? selectedEventId : null;
        var origin = Guid.NewGuid();
        presentation = new(origin);
        selectionStatePending = false;
        surface = retained;
        if (retained is null) {
            selectedEventId = null;
            viewState = ProjectCalendarViewState.Empty;
        }
        agentChatContextAccessState = AgentChatContextAccessState.Loading;
        await using var access = ObserveAccess();
        try {
            await access.StartAsync(_ => Task.CompletedTask, () => Task.CompletedTask);
            await access.AuthorizeAsync(write: false, cancellation.Token);
            var result = await Workbench.TryGetCalendarAsync(projectId, cancellation.Token);
            await access.AuthorizeAsync(write: false, cancellation.Token);
            if (!IsCurrent(origin)) {
                return;
            }
            RequireReadOwner(generation, actor);
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
            await Admissions.RequireCurrentAsync(accepted.ExpectedProjectAdmission!, cancellation.Token);
            await access.AuthorizeAsync(write: false, cancellation.Token);
            if (!IsCurrent(origin)) {
                return;
            }
            RequireReadOwner(generation, actor);
            if (accepted.ExpectedProjectAdmission!.DatabaseProfileId != Database.Profile.Profile.Id) {
                throw new UnauthorizedAccessException("The calendar workspace changed while its data was loading.");
            }
            if (retained?.ExpectedProjectAdmission != accepted.ExpectedProjectAdmission) {
                preferredSelection = null;
            }
            var persisted = ProjectCalendarStateParser.Parse(accepted.ViewStateJson);
            var route = url.State;
            var missing = route.EventId is { } requested && accepted.Events.All(item => item.Id != requested);
            if (missing && preserveAccepted) {
                // The addressed event disappeared while it was open: the location drops it and the default selection applies.
                url.Navigate(route with { EventId = null }, UrlHistoryMode.Replace);
                route = url.State;
                missing = false;
            }
            var selection = missing ? null : ResolveSelection(accepted, route.EventId ?? preferredSelection, persisted.SelectedEventId);
            var preferences = retained?.ExpectedProjectAdmission == accepted.ExpectedProjectAdmission && retainedView is not null
                ? retainedView : persisted;
            var state = ResolveViewState(preferences, route) with { SelectedEventId = selection };
            var next = ProjectCalendarPresentationAdapter.Build(accepted, state, origin) with { Error = missing ? MissingEventMessage : null };
            surface = accepted;
            viewState = state;
            selectedEventId = selection;
            presentation = next;
            displayedActor = actor;
            displayedGeneration = generation;
            agentChatContextAccessState = AgentChatContextAccessState.Ready;
            PublishResolvedView(state);
        } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
        } catch (UnauthorizedAccessException) {
            if (IsCurrent(origin)) {
                var keepAccepted = OwnsDisplayedCalendar;
                surface = keepAccepted ? retained : null;
                const string message = "Current calendar access could not be verified. New schedule data was not displayed. Reopen with current access.";
                presentation = keepAccepted && retainedPresentation is not null
                    ? retainedPresentation with { Origin = origin, State = PlanningReadState.Stale, Error = message }
                    : new(origin) { State = PlanningReadState.Unavailable, Error = message };
                agentChatContextAccessState = AgentChatContextAccessState.Failed;
            }
            Logger.LogWarning("Calendar read access changed for project {ProjectId}; fresh data withheld.", projectId.ToString("N")[..8]);
        } catch (Exception exception) {
            Logger.LogWarning("Calendar read failed for project {ProjectId}; failure type {FailureType}.", projectId, exception.GetType().Name);
            if (IsCurrent(origin)) {
                surface = OwnsDisplayedCalendar ? retained : null;
                presentation = surface is null || retainedPresentation is null
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

    // Back, Forward or a pasted location re-creates the native calendar from the addressed view, date and event.
    private void ApplyRouteToAcceptedCalendar() {
        if (surface is not { } accepted || presentation.State != PlanningReadState.Ready) {
            return;
        }
        var route = url.State;
        var persisted = ProjectCalendarStateParser.Parse(accepted.ViewStateJson);
        var missing = route.EventId is { } requested && accepted.Events.All(item => item.Id != requested);
        var selection = missing ? null : ResolveSelection(accepted, route.EventId, persisted.SelectedEventId);
        var state = ResolveViewState(persisted, route) with { SelectedEventId = selection };
        viewState = state;
        selectedEventId = selection;
        selectionStatePending = false;
        presentation = ProjectCalendarPresentationAdapter.Build(accepted, state, Guid.NewGuid()) with { Error = missing ? MissingEventMessage : null };
        PublishResolvedView(state);
    }

    // URL first, then the persisted preference, then the product default; an unknown date starts on today's UTC date like the native calendar.
    private ProjectCalendarViewState ResolveViewState(ProjectCalendarViewState persisted, ProjectCalendarUrlState route) {
        var date = route.Date ?? ProjectCalendarUrlCodec.ParseDate(persisted.SelectedDate) ?? DateOnly.FromDateTime(TimeProvider.GetUtcNow().UtcDateTime);
        return persisted with {
            View = route.View is { } view ? ProjectCalendarUrlCodec.Views.Format(view) : persisted.View,
            SelectedDate = ProjectCalendarUrlCodec.FormatDate(date)
        };
    }

    // The resolved view and date become explicit with Replace, so Back and Forward return to the exact calendar range.
    // While prerendering the binding keeps this in its state and writes nothing.
    private void PublishResolvedView(ProjectCalendarViewState state) {
        var next = url.State with {
            View = ProjectCalendarUrlCodec.ParseView(state.View) ?? url.State.View,
            Date = ProjectCalendarUrlCodec.ParseDate(state.SelectedDate) ?? url.State.Date
        };
        if (next != url.State) {
            url.Navigate(next, UrlHistoryMode.Replace);
        }
    }

    private Task HandleSelectionAsync(CalendarSelectionIntent intent) {
        if (!CanAccept(intent.Origin) || surface is null ||
            intent.EventId is { } id && !surface.Events.Any(item => item.Id == id)) {
            return Task.CompletedTask;
        }
        var replacesShownSelection = selectedEventId != intent.EventId;
        selectedEventId = intent.EventId;
        viewState = viewState with { SelectedEventId = selectedEventId };
        presentation = ProjectCalendarPresentationAdapter.Build(surface, viewState, intent.Origin) with {
            PersistenceWarning = presentation.PersistenceWarning
        };
        if (url.State.EventId != intent.EventId) {
            // Selecting another event adds one entry; clearing it or making the shown default explicit does not.
            // The calendar announces the date that follows the selected event right after it, and that date joins this entry.
            selectionStatePending = true;
            url.Navigate(url.State with { EventId = intent.EventId },
                intent.EventId is not null && replacesShownSelection ? UrlHistoryMode.Push : UrlHistoryMode.Replace);
        }
        return Task.CompletedTask;
    }

    private async Task HandleStateAsync(CalendarStateIntent intent) {
        var followsSelection = selectionStatePending;
        selectionStatePending = false;
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
        var rangeChanged = !string.Equals(next.View, viewState.View, StringComparison.Ordinal) ||
            !string.Equals(next.SelectedDate, viewState.SelectedDate, StringComparison.Ordinal);
        viewState = next;
        selectedEventId = next.SelectedEventId;
        presentation = ProjectCalendarPresentationAdapter.Build(accepted, next, intent.Origin);
        if (rangeChanged) {
            url.Navigate(url.State with {
                View = ProjectCalendarUrlCodec.ParseView(next.View) ?? url.State.View,
                Date = ProjectCalendarUrlCodec.ParseDate(next.SelectedDate) ?? url.State.Date
            }, followsSelection ? UrlHistoryMode.Replace : UrlHistoryMode.Push);
        }
        var predecessor = viewWriteTail;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        viewWriteTail = completion.Task;
        try {
            await predecessor;
            if (!CanAccept(intent.Origin)) {
                return;
            }
            await using var access = ObserveAccess();
            await access.StartAsync(_ => Task.CompletedTask, () => Task.CompletedTask);
            async ValueTask AuthorizeWriteAsync(CancellationToken token) {
                if (!CanAccept(intent.Origin) || surface?.ExpectedProjectAdmission != admission) {
                    throw new UnauthorizedAccessException("The original calendar view is no longer current.");
                }
                await access.AuthorizeAsync(write: true, token);
                await Admissions.RequireCurrentAsync(admission, token);
                if (!CanAccept(intent.Origin) || surface?.ExpectedProjectAdmission != admission) {
                    throw new UnauthorizedAccessException("The original calendar view changed while access was checked.");
                }
            }
            await AuthorizeWriteAsync(CancellationToken.None);
            await Workbench.SaveCalendarViewStateAsync(admission, stateJson, authorizeCommit: AuthorizeWriteAsync);
            if (CanAccept(intent.Origin) && ReferenceEquals(viewWriteTail, completion.Task)) {
                surface = accepted with { ViewStateJson = stateJson };
                presentation = presentation with { PersistenceWarning = null };
            }
        } catch (UnauthorizedAccessException) {
            if (IsCurrent(intent.Origin)) {
                const string message = "The calendar view was not saved because current access or its original context changed. Refresh with current access before trying again.";
                presentation = presentation with { State = PlanningReadState.Stale, PersistenceWarning = message };
                agentChatContextAccessState = AgentChatContextAccessState.Failed;
                Notifications.Warning("Calendar view not saved", message);
            }
        } catch (ProjectWriteAdmissionRejectedException) {
            if (IsCurrent(intent.Origin)) {
                presentation = presentation with {
                    State = PlanningReadState.Stale,
                    PersistenceWarning = "The displayed project lifetime was rejected. This view was not saved; refresh before continuing."
                };
                agentChatContextAccessState = AgentChatContextAccessState.Failed;
                Notifications.Warning("Calendar view not saved", presentation.PersistenceWarning);
            }
        } catch (Exception exception) {
            Logger.LogWarning("Calendar view save could not be confirmed for project {ProjectId}, origin {Origin}; failure type {FailureType}.",
                accepted.ProjectId, intent.Origin, exception.GetType().Name);
            if (IsCurrent(intent.Origin)) {
                presentation = presentation with { PersistenceWarning = "The saved view could not be confirmed. Refresh to read its durable state before trying again." };
                Notifications.Warning("Calendar view outcome unconfirmed", presentation.PersistenceWarning);
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

    private EditingObservation ObserveAccess()
        => new(EditingChanges, UiAccess, new(ProjectChanges.Scope, EditingOwner.ProjectStructure), EditingLogger);

    private void RequireReadOwner(long generation, Task<AuthenticationState>? actor) {
        if (generation != Database.Generation || !ReferenceEquals(actor, Authentication)) {
            throw new UnauthorizedAccessException("The original calendar actor or workspace changed during its read.");
        }
    }

    private bool OwnsDisplayedCalendar => surface is { ExpectedProjectAdmission: { } admission } &&
        surface.ProjectId == ProjectId && admission.DatabaseProfileId == Database.Profile.Profile.Id &&
        displayedGeneration == Database.Generation && ReferenceEquals(displayedActor, Authentication);

    private bool IsCurrent(Guid origin) => !disposed && origin == presentation.Origin && ReferenceEquals(observedActor, Authentication);
    private bool CanAccept(Guid origin) => IsCurrent(origin) && presentation.State == PlanningReadState.Ready && OwnsDisplayedCalendar;
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
