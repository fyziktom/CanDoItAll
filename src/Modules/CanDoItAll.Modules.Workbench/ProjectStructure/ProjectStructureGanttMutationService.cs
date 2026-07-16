using System.Data;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureGanttMutationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    ILogger<ProjectStructureGanttMutationService> logger)
{
    private const string TaskSubtype = "task";
    private const int MaximumTitleLength = 200;

    public async Task<ProjectStructureGanttMutationResult> ApplyTitleAsync(
        Guid projectId,
        GanttTaskTitleChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await ExecuteAsync(
            projectId,
            (context, state, now, _) =>
            {
                var task = RequireEditableTask(state, request.TaskId);
                if (!string.Equals(task.Title, request.CurrentTitle, StringComparison.Ordinal))
                {
                    throw StaleTask(request.TaskId, "title");
                }

                if (request.ProposedTitle.Length > MaximumTitleLength)
                {
                    throw new ProjectStructureGanttMutationException(
                        ProjectStructureGanttMutationErrorCode.InvalidTitle,
                        $"Task '{Mask(request.TaskId)}' title exceeds {MaximumTitleLength} characters.");
                }

                task.Title = request.ProposedTitle;
                task.UpdatedAtUtc = now;
                return Task.FromResult(Result([request.TaskId]));
            },
            cancellationToken);

        logger.LogInformation(
            "Applied Gantt title mutation for project {ProjectId} and task {TaskId}.",
            Mask(projectId),
            Mask(request.TaskId));
        return result;
    }

    public async Task<ProjectStructureGanttMutationResult> ApplyScheduleAsync(
        Guid projectId,
        GanttTaskScheduleChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await ExecuteAsync(
            projectId,
            (_, state, now, _) => Task.FromResult(ApplyScheduleChanges(state, request, now)),
            cancellationToken);

        logger.LogInformation(
            "Applied Gantt schedule mutation for project {ProjectId}, task {TaskId}, and {AffectedCount} affected tasks.",
            Mask(projectId),
            Mask(request.TaskId),
            result.AffectedTaskIds.Count);
        return result;
    }

    public async Task<ProjectStructureGanttMutationResult> ApplyTaskDetailsAsync(
        Guid projectId,
        ProjectStructureTaskDetailsUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var proposedTitle = request.ProposedTitle.Trim();
        if (proposedTitle.Length == 0 || proposedTitle.Length > MaximumTitleLength)
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.InvalidTitle,
                $"Task title must contain between 1 and {MaximumTitleLength} characters.");
        }

        if ((request.CurrentProgressPercent != ProjectProgressPolicy.UntrackedPercent &&
             !ProjectProgressPolicy.IsTrackedPercent(request.CurrentProgressPercent)) ||
            !ProjectProgressPolicy.IsTrackedPercent(request.ProposedProgressPercent))
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.InvalidTask,
                "Current task progress must be untracked (-1) or between 0 and 100 percent; proposed progress must be between 0 and 100 percent.");
        }

        var currentEstimate = ProjectTaskEstimatePolicy.ValidateAndNormalize(request.CurrentEstimate);
        var proposedEstimate = ProjectTaskEstimatePolicy.ValidateAndNormalize(request.ProposedEstimate);
        var result = await ExecuteAsync(
            projectId,
            (_, state, now, _) =>
            {
                var task = RequireEditableTask(state, request.TaskId);
                if (!string.Equals(task.Title, request.CurrentTitle, StringComparison.Ordinal))
                {
                    throw StaleTask(request.TaskId, "title");
                }

                if (task.ProgressPercent != request.CurrentProgressPercent)
                {
                    throw StaleTask(request.TaskId, "progress");
                }

                var metadata = ProjectObjectMetadataSerializer.Parse(task.MetadataJson);
                var persistedEstimate = ReadEstimate(metadata.WorkItem);
                if (persistedEstimate != currentEstimate)
                {
                    throw StaleTask(request.TaskId, "estimate");
                }

                var affectedTaskIds = new HashSet<GanttTaskId> { request.TaskId };
                if (request.ScheduleChange is not null)
                {
                    if (request.ScheduleChange.TaskId != request.TaskId)
                    {
                        throw new ProjectStructureGanttMutationException(
                            ProjectStructureGanttMutationErrorCode.InvalidSchedule,
                            "The schedule change does not belong to the edited task.");
                    }

                    var scheduleResult = ApplyScheduleChanges(state, request.ScheduleChange, now);
                    affectedTaskIds.UnionWith(scheduleResult.AffectedTaskIds);
                }

                metadata.WorkItem ??= new ProjectWorkItemMetadata
                {
                    WorkItemKind = ProjectWorkItemKind.Task
                };
                WriteEstimate(metadata.WorkItem, proposedEstimate);
                ProjectObjectMetadataSerializer.Validate(task.ObjectType, task.ObjectSubtype, metadata);

                task.Title = proposedTitle;
                task.ProgressPercent = request.ProposedProgressPercent;
                task.ProgressMode = request.ProposedProgressPercent == 100 ? "complete" : "progress";
                task.MetadataJson = ProjectObjectMetadataSerializer.Serialize(metadata);
                task.UpdatedAtUtc = now;
                return Task.FromResult(Result(affectedTaskIds));
            },
            cancellationToken);

        logger.LogInformation(
            "Applied Gantt task detail mutation for project {ProjectId}, task {TaskId}, and {AffectedCount} affected tasks.",
            Mask(projectId),
            Mask(request.TaskId),
            result.AffectedTaskIds.Count);
        return result;
    }

    public async Task<ProjectStructureGanttMutationResult> ApplyDependencyAsync(
        Guid projectId,
        GanttDependencyMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await ExecuteAsync(
            projectId,
            (context, state, now, token) => request.Mutation switch
            {
                GanttDependencyMutationKind.Add => AddDependencyAsync(context, state, request, now, token),
                GanttDependencyMutationKind.Remove => Task.FromResult(RemoveDependency(context, state, request, now)),
                GanttDependencyMutationKind.Reconnect => Task.FromResult(ReconnectDependency(state, request, now)),
                _ => throw new ArgumentOutOfRangeException(nameof(request), request.Mutation, "The dependency mutation is not supported.")
            },
            cancellationToken);

        logger.LogInformation(
            "Applied Gantt dependency mutation {Mutation} for project {ProjectId}; added {AddedCount}, removed {RemovedCount}, affected {AffectedCount}.",
            request.Mutation,
            Mask(projectId),
            result.AddedDependencyCount,
            result.RemovedDependencyCount,
            result.AffectedTaskIds.Count);
        return result;
    }

    public async Task<ProjectStructureGanttMutationResult> ApplyInsertionAsync(
        Guid projectId,
        GanttTaskInsertionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await ExecuteAsync(
            projectId,
            (context, state, now, token) => InsertTaskAsync(context, state, request, now, token),
            cancellationToken);

        logger.LogInformation(
            "Applied Gantt insertion for project {ProjectId}, task {TaskId}, and {AffectedCount} affected tasks.",
            Mask(projectId),
            Mask(request.InsertedTask.Id),
            result.AffectedTaskIds.Count);
        return result;
    }

    private async Task<ProjectStructureGanttMutationResult> ExecuteAsync(
        Guid projectId,
        Func<AppDbContext, MutationState, DateTimeOffset, CancellationToken, Task<ProjectStructureGanttMutationResult>> mutation,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project identifier is required.", nameof(projectId));
        }

        try
        {
            await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await ProjectWorkbenchSchemaInitializer.EnsureAsync(context, cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var state = await LoadStateAsync(context, projectId, cancellationToken);
            var result = await mutation(context, state, clock.GetUtcNow(), cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (ProjectStructureGanttMutationException exception)
        {
            logger.LogWarning(
                "Rejected Gantt mutation for project {ProjectId} with error {ErrorCode}.",
                Mask(projectId),
                exception.Code);
            throw;
        }
    }

    private static async Task<MutationState> LoadStateAsync(
        AppDbContext context,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (!await context.Set<Project>().AnyAsync(project => project.Id == projectId, cancellationToken))
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.ProjectNotFound,
                $"Project '{Mask(projectId)}' does not exist.");
        }

        var objects = await context.Set<ProjectObjectRecord>()
            .Where(record => record.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var allObjects = objects.ToDictionary(static record => record.NodeKey, StringComparer.Ordinal);
        var tasks = objects
            .Where(IsCanonicalTask)
            .ToDictionary(static record => record.NodeKey, StringComparer.Ordinal);
        var links = await context.Set<ProjectObjectLinkRecord>()
            .Where(link => link.ProjectId == projectId && link.LinkKind == ProjectObjectLinkKind.DependsOn)
            .ToListAsync(cancellationToken);
        var dependencies = links
            .Where(link => tasks.ContainsKey(link.SourceNodeKey) && tasks.ContainsKey(link.TargetNodeKey))
            .Select(static link => new DependencyEdge(
                link,
                link.TargetNodeKey,
                link.SourceNodeKey))
            .ToList();
        return new MutationState(projectId, allObjects, tasks, dependencies);
    }

    private static async Task<ProjectStructureGanttMutationResult> AddDependencyAsync(
        AppDbContext context,
        MutationState state,
        GanttDependencyMutationRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ValidateGraphAndSchedule(state);
        var dependency = request.ProposedDependency
            ?? throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.InvalidDependency,
                "An added dependency requires proposed endpoints.");
        RequireEditableTask(state, dependency.PredecessorId);
        RequireEditableTask(state, dependency.SuccessorId);
        RejectDuplicateDependency(state.Dependencies, dependency.PredecessorId.Value, dependency.SuccessorId.Value);

        var recordId = ProjectStructureGanttMutationConventions.RequireNewDependencyRecordId(dependency.Id);
        if (await context.Set<ProjectObjectLinkRecord>().AnyAsync(link => link.Id == recordId, cancellationToken))
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.DuplicateDependency,
                $"Dependency identifier '{ProjectStructureGanttMutationConventions.Mask(dependency.Id.Value)}' already exists.");
        }

        var record = NewDependencyRecord(
            recordId,
            state.ProjectId,
            dependency.PredecessorId.Value,
            dependency.SuccessorId.Value,
            now);
        var finalDependencies = state.Dependencies
            .Append(new DependencyEdge(record, dependency.PredecessorId.Value, dependency.SuccessorId.Value))
            .ToList();
        ValidateGraph(state.TaskIds, finalDependencies);
        var shifted = ShiftConstrainedSuccessors(state, finalDependencies, now);
        ValidateScheduleConstraints(BuildSchedule(state.TasksByNodeKey.Values), finalDependencies);
        await context.Set<ProjectObjectLinkRecord>().AddAsync(record, cancellationToken);

        var touched = shifted
            .Append(dependency.PredecessorId.Value)
            .Append(dependency.SuccessorId.Value)
            .ToHashSet(StringComparer.Ordinal);
        TouchTasks(state, touched, now);
        return Result(touched.Select(static taskId => new GanttTaskId(taskId)), addedDependencyCount: 1);
    }

    private static ProjectStructureGanttMutationResult RemoveDependency(
        AppDbContext context,
        MutationState state,
        GanttDependencyMutationRequest request,
        DateTimeOffset now)
    {
        ValidateGraphAndSchedule(state);
        var previous = request.PreviousDependency
            ?? throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.InvalidDependency,
                "A removed dependency requires previous endpoints.");
        var existing = RequirePersistedDependency(state, previous);
        RejectSystemManaged(existing);
        context.Set<ProjectObjectLinkRecord>().Remove(existing.Record);
        var touched = new HashSet<string>(StringComparer.Ordinal)
        {
            existing.PredecessorId,
            existing.SuccessorId
        };
        TouchTasks(state, touched, now);
        return Result(touched.Select(static taskId => new GanttTaskId(taskId)), removedDependencyCount: 1);
    }

    private static ProjectStructureGanttMutationResult ReconnectDependency(
        MutationState state,
        GanttDependencyMutationRequest request,
        DateTimeOffset now)
    {
        ValidateGraphAndSchedule(state);
        var previous = request.PreviousDependency
            ?? throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.InvalidDependency,
                "A reconnected dependency requires previous endpoints.");
        var proposed = request.ProposedDependency
            ?? throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.InvalidDependency,
                "A reconnected dependency requires proposed endpoints.");
        var existing = RequirePersistedDependency(state, previous);
        RejectSystemManaged(existing);
        RequireEditableTask(state, proposed.PredecessorId);
        RequireEditableTask(state, proposed.SuccessorId);
        RejectDuplicateDependency(
            state.Dependencies.Where(edge => edge.Record.Id != existing.Record.Id),
            proposed.PredecessorId.Value,
            proposed.SuccessorId.Value);

        var finalDependencies = state.Dependencies
            .Select(edge => edge.Record.Id == existing.Record.Id
                ? edge with
                {
                    PredecessorId = proposed.PredecessorId.Value,
                    SuccessorId = proposed.SuccessorId.Value
                }
                : edge)
            .ToList();
        ValidateGraph(state.TaskIds, finalDependencies);
        existing.Record.TargetNodeKey = proposed.PredecessorId.Value;
        existing.Record.SourceNodeKey = proposed.SuccessorId.Value;
        var shifted = ShiftConstrainedSuccessors(state, finalDependencies, now);
        ValidateScheduleConstraints(BuildSchedule(state.TasksByNodeKey.Values), finalDependencies);

        var touched = shifted
            .Append(previous.PredecessorId.Value)
            .Append(previous.SuccessorId.Value)
            .Append(proposed.PredecessorId.Value)
            .Append(proposed.SuccessorId.Value)
            .ToHashSet(StringComparer.Ordinal);
        TouchTasks(state, touched, now);
        return Result(touched.Select(static taskId => new GanttTaskId(taskId)));
    }

    private static async Task<ProjectStructureGanttMutationResult> InsertTaskAsync(
        AppDbContext context,
        MutationState state,
        GanttTaskInsertionRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ValidateGraphAndSchedule(state);
        ProjectStructureGanttMutationConventions.ValidateNewTaskNodeKey(request.InsertedTask.Id);
        ValidateInsertedTask(request);
        if (state.AllObjectsByNodeKey.ContainsKey(request.InsertedTask.Id.Value))
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.InvalidInsertion,
                $"Task '{Mask(request.InsertedTask.Id)}' already exists in the project.");
        }

        var predecessor = RequireEditableTask(state, request.PredecessorId);
        var successor = RequireEditableTask(state, request.SuccessorId);
        var dependencyPlan = ValidateInsertionDependencyPlan(state, request);
        var changes = RequireDistinctChanges(request.AffectedTasks);
        if (!changes.TryGetValue(request.InsertedTask.Id.Value, out var insertedChange) ||
            insertedChange.ProposedStart != request.InsertedTask.Start ||
            insertedChange.ProposedEnd != request.InsertedTask.End)
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.InvalidInsertion,
                "Insertion affected dates must contain the inserted task's proposed interval.");
        }

        var newDependencyIds = dependencyPlan.AddedDependencies
            .Select(dependency => ProjectStructureGanttMutationConventions.RequireNewDependencyRecordId(dependency.Id))
            .ToArray();
        if (newDependencyIds.Distinct().Count() != newDependencyIds.Length ||
            await context.Set<ProjectObjectLinkRecord>().AnyAsync(link => newDependencyIds.Contains(link.Id), cancellationToken))
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.DuplicateDependency,
                "Insertion dependency identifiers must be new and distinct.");
        }

        var insertedRecord = CreateInsertedTask(state.ProjectId, request.InsertedTask, predecessor, successor, now);
        var finalTaskIds = state.TaskIds.Append(insertedRecord.NodeKey).ToHashSet(StringComparer.Ordinal);
        var newRecords = dependencyPlan.AddedDependencies
            .Zip(newDependencyIds)
            .Select(pair => NewDependencyRecord(
                pair.Second,
                state.ProjectId,
                pair.First.PredecessorId.Value,
                pair.First.SuccessorId.Value,
                now))
            .ToArray();
        var finalDependencies = state.Dependencies
            .Where(edge => edge.Record.Id != dependencyPlan.Bridge.Record.Id)
            .Concat(newRecords.Select(static record => new DependencyEdge(
                record,
                record.TargetNodeKey,
                record.SourceNodeKey)))
            .ToList();
        ValidateGraph(finalTaskIds, finalDependencies);

        var allowedAffectedTasks = ReachableTaskIds(finalDependencies, request.InsertedTask.Id.Value);
        allowedAffectedTasks.Add(request.InsertedTask.Id.Value);
        if (changes.Keys.Any(taskId => !allowedAffectedTasks.Contains(taskId)))
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.InvalidInsertion,
                "Insertion can only update the inserted task and its dependent tasks.");
        }

        var schedule = BuildSchedule(state.TasksByNodeKey.Values);
        schedule[insertedRecord.NodeKey] = new TaskInterval(request.InsertedTask.Start, request.InsertedTask.End);
        foreach (var change in changes.Values.Where(change => change.TaskId != request.InsertedTask.Id))
        {
            var task = RequireEditableTask(state, change.TaskId);
            ValidateScheduledStaleState(task, change);
            schedule[task.NodeKey] = new TaskInterval(change.ProposedStart, change.ProposedEnd);
        }

        ValidateScheduleConstraints(schedule, finalDependencies);
        context.Set<ProjectObjectLinkRecord>().Remove(dependencyPlan.Bridge.Record);
        await context.Set<ProjectObjectRecord>().AddAsync(insertedRecord, cancellationToken);
        await context.Set<ProjectObjectLinkRecord>().AddRangeAsync(newRecords, cancellationToken);
        foreach (var change in changes.Values.Where(change => change.TaskId != request.InsertedTask.Id))
        {
            ApplyDates(state.TasksByNodeKey[change.TaskId.Value], change.ProposedStart, change.ProposedEnd, now);
        }

        var touched = changes.Keys
            .Append(predecessor.NodeKey)
            .Append(successor.NodeKey)
            .ToHashSet(StringComparer.Ordinal);
        TouchTasks(state, touched.Where(state.TasksByNodeKey.ContainsKey), now);
        return Result(
            touched.Select(static taskId => new GanttTaskId(taskId)),
            addedDependencyCount: 2,
            removedDependencyCount: 1);
    }

    private static InsertionDependencyPlan ValidateInsertionDependencyPlan(
        MutationState state,
        GanttTaskInsertionRequest request)
    {
        if (request.DependencyChanges.Count != 3 ||
            request.DependencyChanges.Count(change => change.Mutation == GanttDependencyMutationKind.Remove) != 1 ||
            request.DependencyChanges.Count(change => change.Mutation == GanttDependencyMutationKind.Add) != 2)
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.InvalidInsertion,
                "Insertion must remove one bridge dependency and add exactly two replacement dependencies.");
        }

        var removal = request.DependencyChanges.Single(change => change.Mutation == GanttDependencyMutationKind.Remove);
        var removedDependency = removal.PreviousDependency!;
        if (removedDependency.PredecessorId != request.PredecessorId ||
            removedDependency.SuccessorId != request.SuccessorId)
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.InvalidInsertion,
                "Insertion bridge endpoints do not match the requested predecessor and successor.");
        }

        var bridge = RequirePersistedDependency(state, removedDependency);
        RejectSystemManaged(bridge);
        var additions = request.DependencyChanges
            .Where(change => change.Mutation == GanttDependencyMutationKind.Add)
            .Select(change => change.ProposedDependency!)
            .ToArray();
        var hasPredecessorToInserted = additions.Any(dependency =>
            dependency.PredecessorId == request.PredecessorId &&
            dependency.SuccessorId == request.InsertedTask.Id);
        var hasInsertedToSuccessor = additions.Any(dependency =>
            dependency.PredecessorId == request.InsertedTask.Id &&
            dependency.SuccessorId == request.SuccessorId);
        if (!hasPredecessorToInserted || !hasInsertedToSuccessor)
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.InvalidInsertion,
                "Insertion dependencies must connect predecessor to inserted task and inserted task to successor.");
        }

        return new InsertionDependencyPlan(bridge, additions);
    }

    private static void ValidateInsertedTask(GanttTaskInsertionRequest request)
    {
        if (request.InsertedTask.Title.Length > MaximumTitleLength)
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.InvalidTitle,
                $"Inserted task title exceeds {MaximumTitleLength} characters.");
        }

        if (request.InsertedTask.Assignments.Count != 0)
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.InvalidInsertion,
                "Task assignments require their authoritative project assignment services and cannot be inserted through the schedule mutation.");
        }
    }

    private static ProjectObjectRecord CreateInsertedTask(
        Guid projectId,
        GanttTask task,
        ProjectObjectRecord predecessor,
        ProjectObjectRecord successor,
        DateTimeOffset now)
    {
        var progress = ProjectWorkbenchObjectModeling.ResolveStatusBackedProgress("Draft");
        return new ProjectObjectRecord
        {
            ProjectId = projectId,
            NodeKey = task.Id.Value,
            ObjectType = ProjectObjectType.WorkItem,
            ObjectSubtype = TaskSubtype,
            Title = task.Title,
            Subtitle = string.Empty,
            Status = "Draft",
            Notes = string.Empty,
            ProgressMode = progress.Mode,
            ProgressPercent = progress.Percent,
            MarkersJson = "[]",
            MetadataJson = "{}",
            ParentNodeKey = successor.ParentNodeKey,
            PositionX = (predecessor.PositionX + successor.PositionX) / 2,
            PositionY = (predecessor.PositionY + successor.PositionY) / 2,
            StartUtc = task.Start,
            EndUtc = task.End,
            DurationSeconds = ProjectWorkbenchObjectModeling.CalculateDurationSeconds(task.Start, task.End),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private static ProjectObjectLinkRecord NewDependencyRecord(
        Guid id,
        Guid projectId,
        string predecessorId,
        string successorId,
        DateTimeOffset now)
        => new()
        {
            Id = id,
            ProjectId = projectId,
            SourceNodeKey = successorId,
            TargetNodeKey = predecessorId,
            LinkKind = ProjectObjectLinkKind.DependsOn,
            IsSystemManaged = false,
            CreatedAtUtc = now
        };

    private static IReadOnlySet<string> ShiftConstrainedSuccessors(
        MutationState state,
        IReadOnlyList<DependencyEdge> dependencies,
        DateTimeOffset now)
    {
        var schedule = BuildSchedule(state.TasksByNodeKey.Values);
        var shifted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var taskId in TopologicalOrder(state.TaskIds, dependencies))
        {
            if (!schedule.TryGetValue(taskId, out var interval))
            {
                continue;
            }

            var requiredStart = dependencies
                .Where(edge => edge.SuccessorId == taskId)
                .Select(edge => schedule.TryGetValue(edge.PredecessorId, out var predecessorInterval)
                    ? predecessorInterval.End
                    : (DateTimeOffset?)null)
                .Where(static value => value.HasValue)
                .Select(static value => value!.Value)
                .DefaultIfEmpty(interval.Start)
                .Max();
            if (requiredStart <= interval.Start)
            {
                continue;
            }

            var shiftedStart = requiredStart.ToOffset(interval.Start.Offset);
            var shiftedInterval = new TaskInterval(shiftedStart, shiftedStart + interval.Duration);
            schedule[taskId] = shiftedInterval;
            ApplyDates(state.TasksByNodeKey[taskId], shiftedInterval.Start, shiftedInterval.End, now);
            shifted.Add(taskId);
        }

        return shifted;
    }

    private static void ValidateGraphAndSchedule(MutationState state)
    {
        ValidateGraph(state.TaskIds, state.Dependencies);
        ValidateScheduleConstraints(BuildSchedule(state.TasksByNodeKey.Values), state.Dependencies);
    }

    private static void ValidateGraph(
        IReadOnlySet<string> taskIds,
        IReadOnlyList<DependencyEdge> dependencies)
    {
        var duplicate = dependencies
            .GroupBy(static edge => (edge.PredecessorId, edge.SuccessorId))
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.DuplicateDependency,
                $"Project contains duplicate dependency '{ProjectStructureGanttMutationConventions.Mask(duplicate.Key.PredecessorId)}' to '{ProjectStructureGanttMutationConventions.Mask(duplicate.Key.SuccessorId)}'.");
        }

        if (dependencies.Any(edge =>
            edge.PredecessorId == edge.SuccessorId ||
            !taskIds.Contains(edge.PredecessorId) ||
            !taskIds.Contains(edge.SuccessorId)))
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.InvalidDependency,
                "Project contains a dependency with invalid task endpoints.");
        }

        TopologicalOrder(taskIds, dependencies);
    }

    private static IReadOnlyList<string> TopologicalOrder(
        IReadOnlySet<string> taskIds,
        IReadOnlyList<DependencyEdge> dependencies)
    {
        var incoming = taskIds.ToDictionary(static id => id, static _ => 0, StringComparer.Ordinal);
        var successors = taskIds.ToDictionary(
            static id => id,
            static _ => new List<string>(),
            StringComparer.Ordinal);
        foreach (var dependency in dependencies)
        {
            incoming[dependency.SuccessorId]++;
            successors[dependency.PredecessorId].Add(dependency.SuccessorId);
        }

        var ready = new Queue<string>(incoming.Where(static pair => pair.Value == 0).Select(static pair => pair.Key));
        var ordered = new List<string>(taskIds.Count);
        while (ready.TryDequeue(out var taskId))
        {
            ordered.Add(taskId);
            foreach (var successorId in successors[taskId])
            {
                incoming[successorId]--;
                if (incoming[successorId] == 0)
                {
                    ready.Enqueue(successorId);
                }
            }
        }

        if (ordered.Count != taskIds.Count)
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.CycleDetected,
                "The requested dependency change would create a task cycle.");
        }

        return ordered;
    }

    private static Dictionary<string, TaskInterval> BuildSchedule(IEnumerable<ProjectObjectRecord> tasks)
    {
        var schedule = new Dictionary<string, TaskInterval>(StringComparer.Ordinal);
        foreach (var task in tasks)
        {
            if (!task.StartUtc.HasValue && !task.EndUtc.HasValue)
            {
                continue;
            }

            if (!task.StartUtc.HasValue || !task.EndUtc.HasValue || task.EndUtc <= task.StartUtc)
            {
                throw new ProjectStructureGanttMutationException(
                    ProjectStructureGanttMutationErrorCode.InvalidSchedule,
                    $"Task '{ProjectStructureGanttMutationConventions.Mask(task.NodeKey)}' has an invalid persisted interval.");
            }

            schedule.Add(task.NodeKey, new TaskInterval(task.StartUtc.Value, task.EndUtc.Value));
        }

        return schedule;
    }

    private static void ValidateScheduleConstraints(
        IReadOnlyDictionary<string, TaskInterval> schedule,
        IReadOnlyList<DependencyEdge> dependencies)
    {
        var violation = dependencies.FirstOrDefault(edge =>
            schedule.TryGetValue(edge.PredecessorId, out var predecessor) &&
            schedule.TryGetValue(edge.SuccessorId, out var successor) &&
            successor.Start < predecessor.End);
        if (violation is not null)
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.InvalidSchedule,
                $"Task '{ProjectStructureGanttMutationConventions.Mask(violation.SuccessorId)}' starts before predecessor '{ProjectStructureGanttMutationConventions.Mask(violation.PredecessorId)}' finishes.");
        }
    }

    private static Dictionary<string, GanttTaskDateChange> RequireDistinctChanges(
        IReadOnlyList<GanttTaskDateChange> changes)
    {
        var distinct = new Dictionary<string, GanttTaskDateChange>(StringComparer.Ordinal);
        foreach (var change in changes)
        {
            if (!distinct.TryAdd(change.TaskId.Value, change))
            {
                throw new ProjectStructureGanttMutationException(
                    ProjectStructureGanttMutationErrorCode.InvalidSchedule,
                    $"Task '{Mask(change.TaskId)}' has duplicate affected dates.");
            }
        }

        return distinct;
    }

    private static ProjectStructureGanttMutationResult ApplyScheduleChanges(
        MutationState state,
        GanttTaskScheduleChangeRequest request,
        DateTimeOffset now)
    {
        ValidateGraph(state.TaskIds, state.Dependencies);
        var changes = RequireDistinctChanges(request.AffectedTasks);
        if (changes.Count == 0 || !changes.ContainsKey(request.TaskId.Value))
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.InvalidSchedule,
                $"Schedule request for task '{Mask(request.TaskId)}' must include that task in its affected dates.");
        }

        RequireEditableTask(state, request.TaskId);
        var allowedTaskIds = ReachableTaskIds(state.Dependencies, request.TaskId.Value);
        allowedTaskIds.Add(request.TaskId.Value);
        if (changes.Keys.Any(taskId => !allowedTaskIds.Contains(taskId)))
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.InvalidSchedule,
                "A schedule request can only update the selected task and its dependent tasks.");
        }

        var schedule = BuildSchedule(state.TasksByNodeKey.Values);
        ValidateScheduleConstraints(schedule, state.Dependencies);
        foreach (var change in changes.Values)
        {
            var task = RequireEditableTask(state, change.TaskId);
            ValidateScheduledStaleState(task, change);
            schedule[task.NodeKey] = new TaskInterval(change.ProposedStart, change.ProposedEnd);
        }

        ValidateScheduleConstraints(schedule, state.Dependencies);
        foreach (var change in changes.Values)
        {
            ApplyDates(state.TasksByNodeKey[change.TaskId.Value], change.ProposedStart, change.ProposedEnd, now);
        }

        return Result(changes.Values.Select(static change => change.TaskId));
    }

    private static ProjectTaskEstimate ReadEstimate(ProjectWorkItemMetadata? metadata)
    {
        var estimate = metadata is null
            ? new ProjectTaskEstimate(null, ProjectWorkItemEffortUnit.Hours, null, string.Empty)
            : new ProjectTaskEstimate(
                metadata.ExpectedEffortHours,
                metadata.ExpectedEffortUnit,
                metadata.ExpectedCostAmount,
                metadata.ExpectedCostCurrencyCode);
        return ProjectTaskEstimatePolicy.ValidateAndNormalize(estimate);
    }

    private static void WriteEstimate(ProjectWorkItemMetadata metadata, ProjectTaskEstimate estimate)
    {
        metadata.ExpectedEffortHours = estimate.ExpectedEffortHours;
        metadata.ExpectedEffortUnit = estimate.ExpectedEffortUnit;
        metadata.ExpectedCostAmount = estimate.ExpectedCostAmount;
        metadata.ExpectedCostCurrencyCode = estimate.ExpectedCostCurrencyCode;
    }

    private static void ValidateScheduledStaleState(
        ProjectObjectRecord task,
        GanttTaskDateChange change)
    {
        if (!task.StartUtc.HasValue || !task.EndUtc.HasValue)
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.ProjectionOnlySchedule,
                $"Task '{Mask(change.TaskId)}' has no authoritative persisted schedule.");
        }

        if (task.StartUtc.Value != change.PreviousStart || task.EndUtc.Value != change.PreviousEnd)
        {
            throw StaleTask(change.TaskId, "schedule");
        }
    }

    private static DependencyEdge RequirePersistedDependency(
        MutationState state,
        GanttDependency dependency)
    {
        var recordId = ProjectStructureGanttMutationConventions.RequirePersistedDependencyRecordId(dependency.Id);
        var existing = state.Dependencies.FirstOrDefault(edge => edge.Record.Id == recordId);
        if (existing is null)
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.DependencyNotFound,
                $"Dependency '{ProjectStructureGanttMutationConventions.Mask(dependency.Id.Value)}' no longer exists.");
        }

        if (existing.PredecessorId != dependency.PredecessorId.Value ||
            existing.SuccessorId != dependency.SuccessorId.Value)
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.StaleTask,
                $"Dependency '{ProjectStructureGanttMutationConventions.Mask(dependency.Id.Value)}' endpoints changed since the Gantt projection was loaded.");
        }

        return existing;
    }

    private static void RejectSystemManaged(DependencyEdge dependency)
    {
        if (dependency.Record.IsSystemManaged)
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.SystemManagedDependency,
                "System-managed project dependencies cannot be changed from the Gantt projection.");
        }
    }

    private static void RejectDuplicateDependency(
        IEnumerable<DependencyEdge> dependencies,
        string predecessorId,
        string successorId)
    {
        if (dependencies.Any(edge =>
            edge.PredecessorId == predecessorId &&
            edge.SuccessorId == successorId))
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.DuplicateDependency,
                "The requested finish-to-start dependency already exists.");
        }
    }

    private static ProjectObjectRecord RequireEditableTask(MutationState state, GanttTaskId taskId)
    {
        if (!state.AllObjectsByNodeKey.TryGetValue(taskId.Value, out var record))
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.TaskNotFound,
                $"Task '{Mask(taskId)}' does not exist in this project.");
        }

        if (!IsCanonicalTask(record) || record.IsSystemManaged)
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.InvalidTask,
                $"Node '{Mask(taskId)}' is not an editable canonical project task.");
        }

        return record;
    }

    private static bool IsCanonicalTask(ProjectObjectRecord record)
        => record.ObjectType == ProjectObjectType.WorkItem &&
            string.Equals(record.ObjectSubtype, TaskSubtype, StringComparison.OrdinalIgnoreCase);

    private static HashSet<string> ReachableTaskIds(
        IReadOnlyList<DependencyEdge> dependencies,
        string taskId)
    {
        var successors = dependencies
            .GroupBy(static edge => edge.PredecessorId, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static edge => edge.SuccessorId).ToArray(),
                StringComparer.Ordinal);
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>();
        pending.Enqueue(taskId);
        while (pending.TryDequeue(out var current))
        {
            if (!successors.TryGetValue(current, out var currentSuccessors))
            {
                continue;
            }

            foreach (var successor in currentSuccessors)
            {
                if (reachable.Add(successor))
                {
                    pending.Enqueue(successor);
                }
            }
        }

        return reachable;
    }

    private static void ApplyDates(
        ProjectObjectRecord task,
        DateTimeOffset start,
        DateTimeOffset end,
        DateTimeOffset now)
    {
        task.StartUtc = start;
        task.EndUtc = end;
        task.DurationSeconds = ProjectWorkbenchObjectModeling.CalculateDurationSeconds(start, end);
        task.UpdatedAtUtc = now;
    }

    private static void TouchTasks(
        MutationState state,
        IEnumerable<string> taskIds,
        DateTimeOffset now)
    {
        foreach (var taskId in taskIds.Distinct(StringComparer.Ordinal))
        {
            if (state.TasksByNodeKey.TryGetValue(taskId, out var task))
            {
                task.UpdatedAtUtc = now;
            }
        }
    }

    private static ProjectStructureGanttMutationException StaleTask(GanttTaskId taskId, string field)
        => new(
            ProjectStructureGanttMutationErrorCode.StaleTask,
            $"Task '{Mask(taskId)}' {field} changed since the Gantt projection was loaded.");

    private static string Mask(GanttTaskId taskId)
        => ProjectStructureGanttMutationConventions.Mask(taskId.Value);

    private static string Mask(Guid projectId)
        => ProjectStructureGanttMutationConventions.Mask(projectId.ToString("N"));

    private static ProjectStructureGanttMutationResult Result(
        IEnumerable<GanttTaskId> taskIds,
        int addedDependencyCount = 0,
        int removedDependencyCount = 0)
        => new(
            taskIds
                .Distinct()
                .OrderBy(static taskId => taskId.Value, StringComparer.Ordinal)
                .ToArray(),
            addedDependencyCount,
            removedDependencyCount);

    private sealed record MutationState(
        Guid ProjectId,
        IReadOnlyDictionary<string, ProjectObjectRecord> AllObjectsByNodeKey,
        IReadOnlyDictionary<string, ProjectObjectRecord> TasksByNodeKey,
        IReadOnlyList<DependencyEdge> Dependencies)
    {
        public IReadOnlySet<string> TaskIds => TasksByNodeKey.Keys.ToHashSet(StringComparer.Ordinal);
    }

    private sealed record DependencyEdge(
        ProjectObjectLinkRecord Record,
        string PredecessorId,
        string SuccessorId);

    private sealed record TaskInterval(DateTimeOffset Start, DateTimeOffset End)
    {
        public TimeSpan Duration => End - Start;
    }

    private sealed record InsertionDependencyPlan(
        DependencyEdge Bridge,
        IReadOnlyList<GanttDependency> AddedDependencies);
}
