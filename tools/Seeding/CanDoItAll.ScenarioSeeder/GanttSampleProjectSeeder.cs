using System.Text.Json;
using CanDoItAll.Components.Gantt;
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

        var projection = await ValidateProjectionAsync(projectId, taskNodeIds, cancellationToken);
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
            var metadataJson = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
            {
                WorkItem = new ProjectWorkItemMetadata
                {
                    WorkItemKind = ProjectWorkItemKind.Task,
                    Description = spec.Subtitle,
                    DueUtc = spec.EndUtc,
                    ExpectedEffortHours = spec.ExpectedEffortHours,
                    ExpectedEffortUnit = spec.ExpectedEffortUnit,
                    ExpectedCostAmount = spec.ExpectedCostAmount,
                    ExpectedCostCurrencyCode = spec.ExpectedCostCurrencyCode
                }
            });
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
                        metadataJson,
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
                        metadataJson,
                        spec.DurationSeconds),
                    cancellationToken)
                    ?? throw new InvalidOperationException($"Task '{spec.Alias}' disappeared while the Gantt sample was being updated.");
            }

            var updatedTask = await projectWorkbenchService.UpdateObjectMetadataAsync(
                projectId,
                task.Id,
                metadataJson,
                spec.Notes,
                ResolveTaskStatus(spec.ProgressPercent),
                null,
                cancellationToken)
                ?? throw new InvalidOperationException($"Task '{spec.Alias}' disappeared while its estimate metadata was being updated.");
            var progressUpdates = await projectWorkbenchService.UpdateObjectProgressDetailedAsync(
                projectId,
                [updatedTask.Id],
                spec.ProgressPercent == 100 ? "complete" : "progress",
                spec.ProgressPercent,
                cancellationToken);
            if (progressUpdates.Count != 1)
            {
                throw new InvalidOperationException($"Task '{spec.Alias}' did not accept its sample progress value.");
            }

            taskNodeIds[spec.Alias] = updatedTask.Id;
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

    private static string ResolveTaskStatus(int progressPercent)
    {
        return progressPercent switch
        {
            100 => "Completed",
            > 0 => "In progress",
            _ => "Planned"
        };
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
        foreach (var taskAssignments in GanttSampleProjectCatalog.BuildAssignments().GroupBy(assignment => assignment.TaskAlias))
        {
            var taskNodeId = taskNodeIds[taskAssignments.Key];
            var desiredAssignments = taskAssignments
                .Select(assignment => new ProjectPartyAssignmentUpsertRequest
                {
                    ProjectId = projectId,
                    PartyId = parties[assignment.PartyExternalCode],
                    Role = ProjectPartyAssignmentRole.WorkItemAssignee,
                    NodeKey = taskNodeId,
                    IsPrimary = assignment.IsPrimary,
                    AllocationPercent = assignment.AllocationPercent,
                    StartsOn = DateOnly.FromDateTime(assignment.StartsAtUtc.UtcDateTime),
                    EndsOn = DateOnly.FromDateTime(assignment.EndsAtUtc.UtcDateTime),
                    Source = "scenario-seeder",
                    Notes = assignment.Notes
                })
                .ToList();
            var result = await projectPartyIntegrationBridge.ReplaceNodeAssignmentsAsync(
                projectId,
                new ProjectNodeReference(taskNodeId),
                desiredAssignments,
                [ProjectPartyAssignmentRole.WorkItemAssignee],
                cancellationToken);

            EnsureSuccess(result);
        }
    }

    private async Task<ProjectStructureGanttProjectionResult> ValidateProjectionAsync(
        Guid projectId,
        IReadOnlyDictionary<string, string> taskNodeIds,
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

        var sampleTaskIds = taskNodeIds.Values
            .Select(static taskNodeId => new GanttTaskId(taskNodeId))
            .ToHashSet();
        var sampleTasks = projection.Tasks
            .Where(task => sampleTaskIds.Contains(task.Id))
            .ToArray();
        var expectedTaskCount = GanttSampleProjectCatalog.BuildTasks().Count;
        if (sampleTasks.Length != expectedTaskCount)
        {
            throw new InvalidOperationException(
                $"The Gantt projection contained {sampleTasks.Length} of the {expectedTaskCount} required sample tasks.");
        }

        ValidateSampleDependencies(projection, taskNodeIds);
        ValidateSampleAssignments(sampleTasks);
        ValidateSampleMetrics(projection, sampleTasks);

        return projection;
    }

    private static void ValidateSampleDependencies(
        ProjectStructureGanttProjectionResult projection,
        IReadOnlyDictionary<string, string> taskNodeIds)
    {
        foreach (var dependency in GanttSampleProjectCatalog.BuildDependencies())
        {
            var predecessorId = taskNodeIds[dependency.PredecessorAlias];
            var successorId = taskNodeIds[dependency.SuccessorAlias];
            if (!projection.Dependencies.Any(candidate =>
                    string.Equals(candidate.PredecessorId.Value, predecessorId, StringComparison.Ordinal) &&
                    string.Equals(candidate.SuccessorId.Value, successorId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"The seeded Gantt projection is missing dependency '{dependency.PredecessorAlias}' -> '{dependency.SuccessorAlias}'.");
            }
        }
    }

    private static void ValidateSampleAssignments(
        IReadOnlyCollection<GanttTask> sampleTasks)
    {
        var partyNames = GanttSampleProjectCatalog.BuildParties()
            .ToDictionary(party => party.ExternalCode, party => party.DisplayName, StringComparer.OrdinalIgnoreCase);
        var tasksByAlias = GanttSampleProjectCatalog.BuildTasks()
            .ToDictionary(task => task.Alias, task => task.Title, StringComparer.Ordinal);

        foreach (var assignment in GanttSampleProjectCatalog.BuildAssignments())
        {
            var taskTitle = tasksByAlias[assignment.TaskAlias];
            var partyName = partyNames[assignment.PartyExternalCode];
            var task = sampleTasks.Single(candidate => string.Equals(candidate.Title, taskTitle, StringComparison.Ordinal));
            if (!task.Assignments.Any(candidate => string.Equals(candidate.Name, partyName, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"The seeded Gantt task '{taskTitle}' is missing assignment '{partyName}'.");
            }
        }
    }

    private static void ValidateSampleMetrics(
        ProjectStructureGanttProjectionResult projection,
        IReadOnlyCollection<GanttTask> sampleTasks)
    {
        var sampleTaskSpecs = GanttSampleProjectCatalog.BuildTasks();
        var acceptanceSpec = sampleTaskSpecs.Single(task => task.Alias == GanttSampleProjectCatalog.AcceptanceTaskAlias);
        var acceptanceTask = sampleTasks.Single(task => string.Equals(task.Title, acceptanceSpec.Title, StringComparison.Ordinal));
        if (acceptanceTask.Duration != TimeSpan.FromDays(7) ||
            acceptanceTask.ExpectedEffort != TimeSpan.FromHours(8) ||
            acceptanceTask.ProgressPercent != acceptanceSpec.ProgressPercent)
        {
            throw new InvalidOperationException(
                "The seeded customer-acceptance task must project one man-day of pure effort across a one-week delivery interval with its configured progress.");
        }

        var expectedCostTotals = sampleTaskSpecs
            .Where(static task => task.ExpectedCostAmount.HasValue)
            .GroupBy(static task => task.ExpectedCostCurrencyCode, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.Sum(task => task.ExpectedCostAmount!.Value),
                StringComparer.Ordinal);
        var projectedCostTotals = projection.ExpectedCostTotals.ToDictionary(
            static total => total.CurrencyCode,
            static total => total.Amount,
            StringComparer.Ordinal);
        if (expectedCostTotals.Any(expected =>
                !projectedCostTotals.TryGetValue(expected.Key, out var actual) ||
                actual < expected.Value))
        {
            throw new InvalidOperationException("The seeded Gantt projection did not preserve the expected task cost totals.");
        }
    }

    private static Guid EnsureSuccess(Result<Guid> result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(FormatErrors(result.Errors));
        }

        return result.Value;
    }

    private static void EnsureSuccess(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(FormatErrors(result.Errors));
        }
    }

    private static string FormatErrors(IEnumerable<Error> errors)
    {
        return string.Join(" | ", errors.Select(error => $"{error.Code}: {error.Message}"));
    }
}

internal static class GanttSampleProjectCatalog
{
    public const string TaskSubtype = "task";
    public const string AcceptanceTaskAlias = "customer-acceptance";
    public static readonly DateTimeOffset ScheduleStart = new(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset ScheduleEnd = ScheduleStart.AddHours(216);

    public static IReadOnlyList<GanttSampleTaskSpec> BuildTasks()
    {
        return
        [
            Task("scope", "Define interactive Gantt scope", "Contracts and ownership", 0, 4, 120, 160, 4m, 480m, 100),
            Task("architecture", "Design schedule and dependency model", "Strongly typed DAG and mutation boundary", 4, 12, 420, 160, 8m, 1200m, 100),
            Task("ux", "Prototype compact timeline interactions", "Move, resize, connect, and inspect", 4, 10, 420, 360, 4m, 420m, 80),
            Task("canvas", "Implement CanvasLib Gantt interactions", "Routing, hit testing, and horizontal navigation", 12, 24, 760, 260, 10m, 1500m, 65),
            Task("host", "Integrate controlled Project Structure host", "Authoritative persistence and reload", 24, 32, 1120, 260, 8m, 1120m, 55),
            Task("docs", "Document reusable Gantt contracts", "Host events, ownership, and package usage", 24, 32, 1120, 460, 3m, 300m, 45),
            Task("dependency-proof", "Validate dependency and critical-path moves", "Branch, reconnect, insert, and propagate", 32, 38, 1480, 160, 5m, 650m, 35),
            Task("export-proof", "Verify PNG export and accessibility", "Export, keyboard detail, and aligned rows", 32, 36, 1480, 360, 3m, 360m, 25),
            Task("performance-proof", "Profile dense Gantt rendering", "Vertical and horizontal stress coverage", 32, 40, 1480, 560, 4m, 520m, 20),
            Task("keyboard-proof", "Verify keyboard task inspection", "Accessible details and editing workflow", 32, 38, 1480, 760, 3m, 330m, 10),
            Task("cost-review", "Review delivery and effort estimates", "Independent duration and project cost", 38, 42, 1780, 560, 4m, 720m, 5),
            Task("release", "Approve interactive Gantt rollout", "Final product review", 40, 44, 1940, 260, 4m, 600m, 0),
            Task(
                AcceptanceTaskAlias,
                "Customer acceptance window",
                "One man-day of review delivered across one calendar week",
                44,
                212,
                2240,
                260,
                8m,
                1400m,
                65,
                ProjectWorkItemEffortUnit.ManDays),
            Task("support-handoff", "Complete support handoff", "Close the delivery after customer acceptance", 212, 216, 2620, 260, 2m, 240m, 0)
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
            new("performance-proof", "host"),
            new("keyboard-proof", "host"),
            new("cost-review", "dependency-proof"),
            new("release", "performance-proof"),
            new("release", "dependency-proof"),
            new("release", "export-proof"),
            new(AcceptanceTaskAlias, "release"),
            new("support-handoff", AcceptanceTaskAlias)
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
            Assignment("docs", "GANTT-ARCHITECT", true, 60m, "Documents the reusable contract and ownership boundary."),
            Assignment("dependency-proof", "GANTT-QA", true, 100m, "Owns dependency and critical-path proof."),
            Assignment("export-proof", "GANTT-QA", true, 100m, "Owns export and accessibility proof."),
            Assignment("performance-proof", "GANTT-ENGINEER", true, 80m, "Owns dense-rendering performance proof."),
            Assignment("keyboard-proof", "GANTT-QA", true, 100m, "Owns keyboard inspection proof."),
            Assignment("cost-review", "GANTT-ARCHITECT", true, 50m, "Owns estimate and cost review."),
            Assignment("release", "GANTT-ARCHITECT", true, 50m, "Owns final architecture acceptance."),
            Assignment(AcceptanceTaskAlias, "GANTT-ARCHITECT", true, 20m, "Coordinates the one-week customer acceptance window."),
            Assignment("support-handoff", "GANTT-QA", true, 50m, "Owns delivery closeout proof.")
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
                EndDateUtc = ScheduleStart.AddHours(44).UtcDateTime
            },
            new ProjectPhaseEditorModel
            {
                Name = "Acceptance",
                Goal = "Represent a long delivery window separately from its pure effort and cost.",
                Status = ProjectPhaseStatus.Planned,
                StartDateUtc = ScheduleStart.AddHours(44).UtcDateTime,
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
        double y,
        decimal expectedEffortHours,
        decimal expectedCostAmount,
        int progressPercent,
        ProjectWorkItemEffortUnit expectedEffortUnit = ProjectWorkItemEffortUnit.Hours)
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
            y,
            expectedEffortHours,
            expectedEffortUnit,
            expectedCostAmount,
            "USD",
            progressPercent);
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
    double Y,
    decimal ExpectedEffortHours,
    ProjectWorkItemEffortUnit ExpectedEffortUnit,
    decimal? ExpectedCostAmount,
    string ExpectedCostCurrencyCode,
    int ProgressPercent);

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
