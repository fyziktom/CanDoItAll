using System.Text.Json;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.ScenarioSeeder;

internal sealed class GanttSampleProjectSeeder(
    ILogger<GanttSampleProjectSeeder> logger,
    ProjectsService projectsService,
    ProjectWorkbenchService projectWorkbenchService,
    PartyDirectoryService partyDirectoryService,
    IProjectPartyIntegrationBridge projectPartyIntegrationBridge,
    ProjectStructureGanttProjectionAdapter projectionAdapter)
{
    private const string ProjectName = "Interactive Gantt Delivery Sample";
    private const string ProjectMarker = "sample:interactive-gantt";

    public async Task<GanttSampleProjectSeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        var projectId = await EnsureProjectAsync(cancellationToken);
        var parties = await EnsurePartiesAsync(cancellationToken);
        var taskNodeIds = await EnsureTasksAsync(projectId, cancellationToken);
        await EnsureDependenciesAsync(projectId, taskNodeIds, cancellationToken);
        await EnsureAssignmentsAsync(projectId, taskNodeIds, parties, cancellationToken);

        var projection = await ValidateProjectionAsync(projectId, cancellationToken);
        var route = $"/projects/{projectId:D}/structure";

        logger.LogInformation(
            "Seeded interactive Gantt sample with {TaskCount} tasks, {DependencyCount} dependencies, and {AssignmentCount} assignments. Route={ProjectRoute}",
            projection.Tasks.Count,
            projection.Dependencies.Count,
            projection.Tasks.Sum(task => task.Assignments.Count),
            route);

        return new GanttSampleProjectSeedResult(
            projectId,
            ProjectName,
            route,
            projection.Tasks.Count,
            projection.Dependencies.Count,
            projection.Tasks.Sum(task => task.Assignments.Count));
    }

    private async Task<Guid> EnsureProjectAsync(CancellationToken cancellationToken)
    {
        var existing = (await projectsService.ListAsync(cancellationToken))
            .FirstOrDefault(project => string.Equals(project.Name, ProjectName, StringComparison.Ordinal));
        var result = await projectsService.SaveAsync(
            new ProjectEditorModel
            {
                Id = existing?.Id,
                Name = ProjectName,
                Description = $"Development sample for the reusable interactive Gantt chart. Marker: {ProjectMarker}",
                Objective = "Exercise parallel task lanes, multiple predecessors, critical-path propagation, assignments, and export from the Project Structure workbench.",
                Status = ProjectStatus.Active,
                CurrentPhase = "Interactive schedule validation",
                TargetDateUtc = GanttSampleProjectCatalog.ScheduleEnd.UtcDateTime,
                Phases = GanttSampleProjectCatalog.BuildPhases().ToList(),
                Options = GanttSampleProjectCatalog.BuildOptions().ToList()
            },
            cancellationToken);

        return EnsureSuccess(result);
    }

    private async Task<IReadOnlyDictionary<string, Guid>> EnsurePartiesAsync(CancellationToken cancellationToken)
    {
        var existing = (await partyDirectoryService.ListDirectoryAsync(cancellationToken))
            .Where(party => !string.IsNullOrWhiteSpace(party.ExternalCode))
            .ToDictionary(party => party.ExternalCode, party => party, StringComparer.OrdinalIgnoreCase);
        var parties = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var spec in GanttSampleProjectCatalog.BuildParties())
        {
            existing.TryGetValue(spec.ExternalCode, out var existingParty);
            var result = await partyDirectoryService.SavePartyAsync(
                new PartyEditorModel
                {
                    Id = existingParty?.Id,
                    PartyType = spec.PartyType,
                    LifecycleStatus = PartyLifecycleStatus.Active,
                    DisplayName = spec.DisplayName,
                    LegalName = spec.DisplayName,
                    PreferredName = spec.DisplayName,
                    ExternalCode = spec.ExternalCode,
                    Summary = spec.Summary,
                    Notes = "Development-only interactive Gantt sample participant.",
                    Tags = [ProjectMarker, "development-sample"],
                    Region = "Development workspace",
                    CountryCode = "US",
                    TimeZone = "UTC",
                    ExtendedDataJson = JsonSerializer.Serialize(new { sample = ProjectMarker }),
                    LastChangedBy = "scenario-seeder",
                    Roles =
                    [
                        new PartyRoleAssignmentEditorModel
                        {
                            RoleKind = spec.PartyType == PartyType.AiAgent
                                ? PartyRoleKind.AiSteward
                                : PartyRoleKind.Employee,
                            Title = spec.DisplayName,
                            IsPrimary = true,
                            Notes = spec.Summary
                        }
                    ]
                },
                cancellationToken);

            parties[spec.ExternalCode] = EnsureSuccess(result);
        }

        return parties;
    }

    private async Task<IReadOnlyDictionary<string, string>> EnsureTasksAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var rootNodeId = surface.Nodes
            .First(node => node.ObjectType == ProjectObjectType.ProjectRoot)
            .Id;
        var taskNodeIds = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var spec in GanttSampleProjectCatalog.BuildTasks())
        {
            var existing = surface.Nodes.FirstOrDefault(node =>
                node.ObjectType == ProjectObjectType.WorkItem &&
                string.Equals(node.ObjectSubtype, GanttSampleProjectCatalog.TaskSubtype, StringComparison.OrdinalIgnoreCase) &&
                (MatchesTaskMarker(node, spec.Alias) ||
                 string.Equals(node.Title, spec.Title, StringComparison.Ordinal)) &&
                string.Equals(node.ParentId, rootNodeId, StringComparison.Ordinal));
            ProjectStructureNode task;
            if (existing is null)
            {
                task = await projectWorkbenchService.CreateObjectAsync(
                    projectId,
                    new ProjectObjectCreateRequest(
                        ProjectObjectType.WorkItem,
                        spec.Title,
                        spec.Subtitle,
                        spec.Notes,
                        rootNodeId,
                        spec.X,
                        spec.Y,
                        spec.StartUtc,
                        spec.EndUtc,
                        GanttSampleProjectCatalog.TaskSubtype,
                        null,
                        "{}",
                        spec.DurationSeconds),
                    cancellationToken);
            }
            else
            {
                task = await projectWorkbenchService.UpdateObjectAsync(
                    projectId,
                    existing.Id,
                    new ProjectObjectEditRequest(
                        spec.Title,
                        spec.Subtitle,
                        spec.Notes,
                        spec.StartUtc,
                        spec.EndUtc,
                        "{}",
                        spec.DurationSeconds),
                    cancellationToken)
                    ?? throw new InvalidOperationException($"Task '{spec.Alias}' disappeared while the Gantt sample was being updated.");
            }

            await projectWorkbenchService.UpdateObjectMetadataAsync(
                projectId,
                task.Id,
                "{}",
                spec.Notes,
                "Planned",
                null,
                cancellationToken);
            taskNodeIds[spec.Alias] = task.Id;
            surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        }

        return taskNodeIds;
    }

    private static bool MatchesTaskMarker(ProjectStructureNode node, string alias)
    {
        return node.Notes.Contains(
            $"Marker: {ProjectMarker};task:{alias}",
            StringComparison.Ordinal);
    }

    private async Task EnsureDependenciesAsync(
        Guid projectId,
        IReadOnlyDictionary<string, string> taskNodeIds,
        CancellationToken cancellationToken)
    {
        foreach (var dependency in GanttSampleProjectCatalog.BuildDependencies())
        {
            await projectWorkbenchService.LinkObjectsAsync(
                projectId,
                taskNodeIds[dependency.SuccessorAlias],
                taskNodeIds[dependency.PredecessorAlias],
                ProjectObjectLinkKind.DependsOn,
                cancellationToken);
        }
    }

    private async Task EnsureAssignmentsAsync(
        Guid projectId,
        IReadOnlyDictionary<string, string> taskNodeIds,
        IReadOnlyDictionary<string, Guid> parties,
        CancellationToken cancellationToken)
    {
        foreach (var assignment in GanttSampleProjectCatalog.BuildAssignments())
        {
            var result = await projectPartyIntegrationBridge.SaveAssignmentAsync(
                new ProjectPartyAssignmentUpsertRequest
                {
                    ProjectId = projectId,
                    PartyId = parties[assignment.PartyExternalCode],
                    Role = ProjectPartyAssignmentRole.WorkItemAssignee,
                    NodeKey = taskNodeIds[assignment.TaskAlias],
                    IsPrimary = assignment.IsPrimary,
                    AllocationPercent = assignment.AllocationPercent,
                    StartsOn = DateOnly.FromDateTime(assignment.StartsAtUtc.UtcDateTime),
                    EndsOn = DateOnly.FromDateTime(assignment.EndsAtUtc.UtcDateTime),
                    Source = "scenario-seeder",
                    Notes = assignment.Notes
                },
                cancellationToken);

            EnsureSuccess(result);
        }
    }

    private async Task<ProjectStructureGanttProjectionResult> ValidateProjectionAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var assignments = await projectPartyIntegrationBridge.ListAssignmentsDetailedAsync(projectId, cancellationToken);
        var projection = projectionAdapter.Build(
            surface,
            assignments,
            new ProjectStructureGanttProjectionOptions(
                GanttSampleProjectCatalog.ScheduleStart,
                TimeSpan.FromHours(8)));

        if (!projection.IsValid)
        {
            var errors = projection.Issues
                .Where(issue => issue.Severity == ProjectStructureGanttProjectionIssueSeverity.Error)
                .Select(issue => $"{issue.Code}: {issue.Message}");
            throw new InvalidOperationException(
                $"The seeded Gantt projection is invalid: {string.Join(" | ", errors)}");
        }

        var expectedTaskCount = GanttSampleProjectCatalog.BuildTasks().Count;
        var expectedDependencyCount = GanttSampleProjectCatalog.BuildDependencies().Count;
        var expectedAssignmentCount = GanttSampleProjectCatalog.BuildAssignments().Count;
        var assignmentCount = projection.Tasks.Sum(task => task.Assignments.Count);
        if (projection.Tasks.Count != expectedTaskCount ||
            projection.Dependencies.Count != expectedDependencyCount ||
            assignmentCount != expectedAssignmentCount)
        {
            throw new InvalidOperationException(
                $"The seeded Gantt projection contained {projection.Tasks.Count} tasks, {projection.Dependencies.Count} dependencies, and {assignmentCount} assignments; expected {expectedTaskCount}, {expectedDependencyCount}, and {expectedAssignmentCount}.");
        }

        return projection;
    }

    private static Guid EnsureSuccess(Result<Guid> result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(FormatErrors(result.Errors));
        }

        return result.Value;
    }

    private static string FormatErrors(IEnumerable<Error> errors)
    {
        return string.Join(" | ", errors.Select(error => $"{error.Code}: {error.Message}"));
    }
}

internal static class GanttSampleProjectCatalog
{
    public const string TaskSubtype = "task";
    public static readonly DateTimeOffset ScheduleStart = new(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset ScheduleEnd = new(2026, 7, 17, 2, 0, 0, TimeSpan.Zero);

    public static IReadOnlyList<GanttSampleTaskSpec> BuildTasks()
    {
        return
        [
            Task("scope", "Define interactive Gantt scope", "Contracts and ownership", 0, 4, 120, 160),
            Task("architecture", "Design schedule and dependency model", "Strongly typed DAG and mutation boundary", 4, 12, 420, 160),
            Task("ux", "Prototype compact timeline interactions", "Move, resize, connect, and inspect", 4, 10, 420, 360),
            Task("canvas", "Implement CanvasLib Gantt interactions", "Routing, hit testing, and horizontal navigation", 12, 24, 760, 260),
            Task("host", "Integrate controlled Project Structure host", "Authoritative persistence and reload", 24, 32, 1120, 260),
            Task("dependency-proof", "Validate dependency and critical-path moves", "Branch, reconnect, insert, and propagate", 32, 38, 1480, 160),
            Task("export-proof", "Verify PNG export and accessibility", "Export, keyboard detail, and aligned rows", 32, 36, 1480, 360),
            Task("release", "Approve interactive Gantt rollout", "Final product review", 38, 42, 1840, 260)
        ];
    }

    public static IReadOnlyList<GanttSampleDependencySpec> BuildDependencies()
    {
        return
        [
            new("architecture", "scope"),
            new("ux", "scope"),
            new("canvas", "architecture"),
            new("canvas", "ux"),
            new("host", "canvas"),
            new("dependency-proof", "host"),
            new("export-proof", "host"),
            new("release", "dependency-proof"),
            new("release", "export-proof")
        ];
    }

    public static IReadOnlyList<GanttSamplePartySpec> BuildParties()
    {
        return
        [
            new("GANTT-ARCHITECT", PartyType.Person, "Dana Reyes", "Solution architect for the reusable scheduling and dependency model."),
            new("GANTT-ENGINEER", PartyType.Person, "Morgan Lee", "Workbench engineer responsible for CanvasLib interaction and controlled host integration."),
            new("GANTT-QA", PartyType.Person, "Avery Chen", "Validation engineer responsible for dependency, export, and accessibility proof."),
            new("GANTT-AGENT", PartyType.AiAgent, "Atlas Build Agent", "Bounded development agent assisting with implementation and regression proof.")
        ];
    }

    public static IReadOnlyList<GanttSampleAssignmentSpec> BuildAssignments()
    {
        var tasks = BuildTasks().ToDictionary(task => task.Alias, StringComparer.Ordinal);
        GanttSampleAssignmentSpec Assignment(
            string taskAlias,
            string partyExternalCode,
            bool isPrimary,
            decimal allocationPercent,
            string notes)
        {
            var task = tasks[taskAlias];
            return new GanttSampleAssignmentSpec(
                taskAlias,
                partyExternalCode,
                isPrimary,
                allocationPercent,
                task.StartUtc,
                task.EndUtc,
                notes);
        }

        return
        [
            Assignment("scope", "GANTT-ARCHITECT", true, 100m, "Owns the task and event contract."),
            Assignment("architecture", "GANTT-ARCHITECT", true, 100m, "Owns DAG and scheduling design."),
            Assignment("ux", "GANTT-ENGINEER", true, 80m, "Owns compact interaction design."),
            Assignment("canvas", "GANTT-ENGINEER", true, 100m, "Owns CanvasLib implementation."),
            Assignment("canvas", "GANTT-AGENT", false, 50m, "Assists with bounded implementation and tests."),
            Assignment("host", "GANTT-ENGINEER", true, 100m, "Owns controlled Project Structure integration."),
            Assignment("dependency-proof", "GANTT-QA", true, 100m, "Owns dependency and critical-path proof."),
            Assignment("export-proof", "GANTT-QA", true, 100m, "Owns export and accessibility proof."),
            Assignment("release", "GANTT-ARCHITECT", true, 50m, "Owns final architecture acceptance.")
        ];
    }

    public static IReadOnlyList<ProjectPhaseEditorModel> BuildPhases()
    {
        return
        [
            new ProjectPhaseEditorModel
            {
                Name = "Design",
                Goal = "Fix reusable contracts, schedule rules, and compact interaction behavior.",
                Status = ProjectPhaseStatus.Active,
                StartDateUtc = ScheduleStart.UtcDateTime,
                EndDateUtc = ScheduleStart.AddHours(12).UtcDateTime
            },
            new ProjectPhaseEditorModel
            {
                Name = "Implementation",
                Goal = "Deliver CanvasLib behavior and the controlled Project Structure host.",
                Status = ProjectPhaseStatus.Active,
                StartDateUtc = ScheduleStart.AddHours(12).UtcDateTime,
                EndDateUtc = ScheduleStart.AddHours(32).UtcDateTime
            },
            new ProjectPhaseEditorModel
            {
                Name = "Validation",
                Goal = "Prove dependency editing, critical-path movement, export, and accessibility.",
                Status = ProjectPhaseStatus.Planned,
                StartDateUtc = ScheduleStart.AddHours(32).UtcDateTime,
                EndDateUtc = ScheduleEnd.UtcDateTime
            }
        ];
    }

    public static IReadOnlyList<ProjectOptionEditorModel> BuildOptions()
    {
        return
        [
            new ProjectOptionEditorModel
            {
                Category = ProjectOptionCategory.Language,
                OptionName = "C# / .NET 10",
                Notes = "Strongly typed contracts and controlled mutations."
            },
            new ProjectOptionEditorModel
            {
                Category = ProjectOptionCategory.Ui,
                OptionName = "Blazor with BaseLib, CanvasLib, and Gantt",
                Notes = "Shared components own rendering and interactions."
            },
            new ProjectOptionEditorModel
            {
                Category = ProjectOptionCategory.Testing,
                OptionName = "Component, scheduling, and browser acceptance",
                Notes = "Every interactive path has repeatable proof."
            }
        ];
    }

    private static GanttSampleTaskSpec Task(
        string alias,
        string title,
        string subtitle,
        int startHour,
        int endHour,
        double x,
        double y)
    {
        var start = ScheduleStart.AddHours(startHour);
        var end = ScheduleStart.AddHours(endHour);
        return new GanttSampleTaskSpec(
            alias,
            title,
            subtitle,
            $"Persisted sample task for the interactive Gantt development scenario. Marker: sample:interactive-gantt;task:{alias}",
            start,
            end,
            checked((int)(end - start).TotalSeconds),
            x,
            y);
    }
}

internal sealed record GanttSampleTaskSpec(
    string Alias,
    string Title,
    string Subtitle,
    string Notes,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int DurationSeconds,
    double X,
    double Y);

internal sealed record GanttSampleDependencySpec(
    string SuccessorAlias,
    string PredecessorAlias);

internal sealed record GanttSamplePartySpec(
    string ExternalCode,
    PartyType PartyType,
    string DisplayName,
    string Summary);

internal sealed record GanttSampleAssignmentSpec(
    string TaskAlias,
    string PartyExternalCode,
    bool IsPrimary,
    decimal AllocationPercent,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string Notes);

internal sealed record GanttSampleProjectSeedResult(
    Guid ProjectId,
    string ProjectName,
    string ProjectStructureRoute,
    int TaskCount,
    int DependencyCount,
    int AssignmentCount);
