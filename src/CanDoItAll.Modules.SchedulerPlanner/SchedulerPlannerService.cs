using System.Globalization;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Automation;
using CanDoItAll.Modules.Processes;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CanDoItAll.Modules.SchedulerPlanner;

public interface ISchedulerPlannerService
{
    Task<SchedulerPlannerWorkspace> GetWorkspaceAsync(
        SchedulerHistoryQuery? historyQuery = null,
        CancellationToken cancellationToken = default);

    Task<SchedulerPlanEditorModel> CreateDefaultEditorAsync(CancellationToken cancellationToken = default);

    Task<SchedulerPlanSummary> SavePlanAsync(
        SchedulerPlanEditorModel editor,
        CancellationToken cancellationToken = default);

    Task SetPlanEnabledAsync(
        Guid planId,
        bool isEnabled,
        CancellationToken cancellationToken = default);
}

public interface ICronDescriptionService
{
    string Describe(string cronExpression, string timeZoneId);
}

public interface ISchedulerTargetLauncher
{
    Task<SchedulerTargetLaunchResult> LaunchAsync(
        SchedulerPlan plan,
        DateTimeOffset firedAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed class SchedulerTargetLaunchException : InvalidOperationException
{
    public SchedulerTargetLaunchException(
        string message,
        SchedulerPlanRunRetryCategory retryCategory,
        string route,
        Guid? targetRunId = null,
        SchedulerPlanTargetKind? targetKind = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        RetryCategory = retryCategory;
        Route = route;
        TargetRunId = targetRunId;
        TargetKind = targetKind;
    }

    public SchedulerPlanRunRetryCategory RetryCategory { get; }

    public string Route { get; }

    public Guid? TargetRunId { get; }

    public SchedulerPlanTargetKind? TargetKind { get; }
}

internal static class SchedulerPlanRunRetryClassifier
{
    public static SchedulerPlanRunRetryCategory Classify(Exception exception)
    {
        if (exception is SchedulerTargetLaunchException launchException)
        {
            return launchException.RetryCategory;
        }

        if (WorkflowExternalRequestPendingException.TryFind(exception, out _))
        {
            return SchedulerPlanRunRetryCategory.WorkflowWaitingForApproval;
        }

        if (exception is HttpRequestException or TimeoutException)
        {
            return SchedulerPlanRunRetryCategory.TransientExternalFailure;
        }

        return ClassifyMessage(exception.ToString());
    }

    public static SchedulerPlanRunRetryCategory ClassifyWorkflowFailure(string failureText)
    {
        var classified = ClassifyMessage(failureText);
        return classified == SchedulerPlanRunRetryCategory.SchedulerFailure
            ? SchedulerPlanRunRetryCategory.WorkflowFailure
            : classified;
    }

    private static SchedulerPlanRunRetryCategory ClassifyMessage(string failureText)
    {
        if (ContainsAny(failureText, "Microsoft Graph", "Office365", "OAuth"))
        {
            return SchedulerPlanRunRetryCategory.TransientExternalFailure;
        }

        if (ContainsAny(failureText, "project structure", "project-structure", "project node", "project asset"))
        {
            return SchedulerPlanRunRetryCategory.ProjectWriteFailure;
        }

        if (ContainsAny(failureText, "workflow executor", "workflow run", "workflow definition"))
        {
            return SchedulerPlanRunRetryCategory.WorkflowFailure;
        }

        return SchedulerPlanRunRetryCategory.SchedulerFailure;
    }

    private static bool ContainsAny(string value, params string[] candidates)
        => candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));
}

public sealed class SchedulerPlannerService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IAutomationTriggerRegistry triggerRegistry,
    ICronDescriptionService cronDescriptionService,
    ProcessesService processesService,
    IWorkflowCatalogService workflowCatalogService,
    ISchedulerWorkflowInputSchemaService workflowInputSchemaService,
    IClock clock,
    ILogger<SchedulerPlannerService> logger) : ISchedulerPlannerService
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SchedulerPlannerWorkspace> GetWorkspaceAsync(
        SchedulerHistoryQuery? historyQuery = null,
        CancellationToken cancellationToken = default)
    {
        var targetOptions = await ListTargetOptionsAsync(cancellationToken);
        var triggerLookup = (await triggerRegistry.ListAsync(cancellationToken))
            .Where(item =>
                item.OwnerKind == AutomationTriggerOwnerKind.Module &&
                string.Equals(item.OwnerKey, SchedulerPlannerConstants.AutomationOwnerKey, StringComparison.Ordinal))
            .ToDictionary(item => item.Id);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var plans = await dbContext.Set<SchedulerPlan>()
            .AsNoTracking()
            .OrderByDescending(item => item.IsEnabled)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);
        var summaries = plans
            .Select(plan => MapPlan(plan, triggerLookup.GetValueOrDefault(plan.AutomationTriggerId)))
            .ToArray();
        var history = await SearchHistoryAsync(dbContext, historyQuery ?? new SchedulerHistoryQuery(), cancellationToken);

        return new SchedulerPlannerWorkspace(
            summaries,
            history,
            targetOptions,
            BuildCalendarSurface(summaries, history));
    }

    public async Task<SchedulerPlanEditorModel> CreateDefaultEditorAsync(CancellationToken cancellationToken = default)
    {
        var targets = await ListTargetOptionsAsync(cancellationToken);
        var firstProcess = targets.FirstOrDefault(item => item.Kind == SchedulerPlanTargetKind.Process);
        var firstTarget = firstProcess ?? targets.FirstOrDefault();
        return new SchedulerPlanEditorModel
        {
            Name = firstTarget is null ? "Scheduled run" : $"{firstTarget.Name} schedule",
            TargetKind = firstTarget?.Kind ?? SchedulerPlanTargetKind.Process,
            TargetId = firstTarget?.Id ?? Guid.Empty,
            TargetVersionId = firstTarget?.VersionId,
            TimeZoneId = ResolveDefaultTimeZoneId(),
            CronExpression = "0 0 9 ? * MON-FRI",
            MisfirePolicy = AutomationTriggerMisfirePolicy.FireOnceNow,
            InputJson = "{}",
            IsEnabled = true
        };
    }

    public async Task<SchedulerPlanSummary> SavePlanAsync(
        SchedulerPlanEditorModel editor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ValidateEditor(editor);

        var target = await ResolveTargetAsync(editor.TargetKind, editor.TargetId, editor.TargetVersionId, cancellationToken);
        var normalizedInputJson = await ResolveValidatedInputJsonAsync(editor, target, cancellationToken);
        var now = clock.GetUtcNow();
        var cronDescription = cronDescriptionService.Describe(editor.CronExpression, editor.TimeZoneId);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var plan = editor.Id.HasValue
            ? await dbContext.Set<SchedulerPlan>().SingleOrDefaultAsync(item => item.Id == editor.Id.Value, cancellationToken)
            : null;

        if (editor.Id.HasValue && plan is null)
        {
            throw new KeyNotFoundException($"Scheduler plan '{editor.Id.Value:D}' was not found.");
        }

        if (plan is null)
        {
            plan = new SchedulerPlan
            {
                Id = editor.Id.GetValueOrDefault(Guid.NewGuid()),
                AutomationTriggerId = Guid.NewGuid(),
                CreatedAtUtc = now
            };
            plan.AutomationTriggerKey = BuildTriggerKey(plan.Id);
            await dbContext.Set<SchedulerPlan>().AddAsync(plan, cancellationToken);
        }

        plan.Name = editor.Name.Trim();
        plan.Description = editor.Description.Trim();
        plan.TargetKind = editor.TargetKind;
        plan.TargetId = editor.TargetId;
        plan.TargetVersionId = editor.TargetKind == SchedulerPlanTargetKind.Workflow
            ? target.VersionId
            : null;
        plan.TargetNameSnapshot = target.Name;
        plan.CronExpression = editor.CronExpression.Trim();
        plan.CronDescription = cronDescription;
        plan.TimeZoneId = editor.TimeZoneId.Trim();
        plan.MisfirePolicy = editor.MisfirePolicy;
        plan.IsEnabled = editor.IsEnabled;
        plan.StartAtUtc = editor.StartAtUtc;
        plan.EndAtUtc = editor.EndAtUtc;
        plan.InputJson = normalizedInputJson;
        plan.LastError = string.Empty;
        plan.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        var savedTrigger = await triggerRegistry.SaveAsync(BuildTriggerDefinition(plan), cancellationToken);
        plan.NextPlannedFireAtUtc = savedTrigger.NextPlannedFireAtUtc;
        plan.LastFiredAtUtc = savedTrigger.LastFiredAtUtc;
        plan.UpdatedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Saved scheduler plan {PlanId} for {TargetKind} target {TargetId}. Enabled={IsEnabled}, Cron={CronExpression}, TimeZone={TimeZoneId}.",
            plan.Id,
            plan.TargetKind,
            plan.TargetId,
            plan.IsEnabled,
            plan.CronExpression,
            plan.TimeZoneId);

        return MapPlan(plan, savedTrigger);
    }

    public async Task SetPlanEnabledAsync(
        Guid planId,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        if (planId == Guid.Empty)
        {
            throw new ArgumentException("Scheduler plan id is required.", nameof(planId));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var plan = await dbContext.Set<SchedulerPlan>().SingleOrDefaultAsync(item => item.Id == planId, cancellationToken)
            ?? throw new KeyNotFoundException($"Scheduler plan '{planId:D}' was not found.");

        if (plan.IsEnabled == isEnabled)
        {
            return;
        }

        plan.IsEnabled = isEnabled;
        plan.UpdatedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        var savedTrigger = await triggerRegistry.SaveAsync(BuildTriggerDefinition(plan), cancellationToken);
        plan.NextPlannedFireAtUtc = savedTrigger.NextPlannedFireAtUtc;
        plan.LastFiredAtUtc = savedTrigger.LastFiredAtUtc;
        plan.UpdatedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<SchedulerTargetOption>> ListTargetOptionsAsync(CancellationToken cancellationToken)
    {
        var processTargets = (await processesService.ListDefinitionsAsync(cancellationToken: cancellationToken))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new SchedulerTargetOption(
                SchedulerPlanTargetKind.Process,
                item.Id,
                VersionId: null,
                item.Name,
                item.Summary,
                item.HasPublishedVersion ? "Published" : item.Status.ToString()));
        var workflowTargets = (await workflowCatalogService.ListDefinitionsAsync(cancellationToken))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new SchedulerTargetOption(
                SchedulerPlanTargetKind.Workflow,
                item.Id.Value,
                item.VersionId.Value,
                item.Name,
                item.Description,
                $"{item.Status} / {item.PreferredBackend}"));

        return processTargets
            .Concat(workflowTargets)
            .ToArray();
    }

    private async Task<SchedulerTargetOption> ResolveTargetAsync(
        SchedulerPlanTargetKind targetKind,
        Guid targetId,
        Guid? targetVersionId,
        CancellationToken cancellationToken)
    {
        var targets = await ListTargetOptionsAsync(cancellationToken);
        var target = targets.FirstOrDefault(item =>
            item.Kind == targetKind &&
            item.Id == targetId &&
            (targetKind != SchedulerPlanTargetKind.Workflow ||
             !targetVersionId.HasValue ||
             item.VersionId == targetVersionId.Value));
        if (target is not null)
        {
            return target;
        }

        throw new InvalidOperationException(
            $"Scheduler target '{targetKind}:{targetId:D}' was not found. Create or publish the target before scheduling it.");
    }

    private async Task<string> ResolveValidatedInputJsonAsync(
        SchedulerPlanEditorModel editor,
        SchedulerTargetOption target,
        CancellationToken cancellationToken)
    {
        if (editor.TargetKind != SchedulerPlanTargetKind.Workflow)
        {
            return NormalizeJson(editor.InputJson);
        }

        var validation = await workflowInputSchemaService.ValidateInputAsync(
            new WorkflowId(editor.TargetId),
            target.VersionId.HasValue ? new WorkflowVersionId(target.VersionId.Value) : null,
            editor.InputJson,
            cancellationToken);
        if (validation.Succeeded)
        {
            return validation.NormalizedInputJson;
        }

        var issues = string.Join(
            " ",
            validation.Issues.Select(issue =>
                string.IsNullOrWhiteSpace(issue.ParameterKey)
                    ? issue.Message
                    : $"{issue.ParameterKey}: {issue.Message}"));
        throw new InvalidOperationException($"Scheduler workflow input is invalid: {issues}");
    }

    private async Task<IReadOnlyList<SchedulerPlanRunSummary>> SearchHistoryAsync(
        AppDbContext dbContext,
        SchedulerHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var take = Math.Clamp(query.Take, 1, 250);
        var runsQuery = dbContext.Set<SchedulerPlanRun>()
            .AsNoTracking()
            .Join(
                dbContext.Set<SchedulerPlan>().AsNoTracking(),
                run => run.PlanId,
                plan => plan.Id,
                (run, plan) => new
                {
                    Run = run,
                    Plan = plan
                });

        if (query.Status.HasValue)
        {
            runsQuery = runsQuery.Where(item => item.Run.Status == query.Status.Value);
        }

        if (query.TargetKind.HasValue)
        {
            runsQuery = runsQuery.Where(item => item.Plan.TargetKind == query.TargetKind.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            runsQuery = runsQuery.Where(item =>
                item.Plan.Name.Contains(search) ||
                item.Plan.TargetNameSnapshot.Contains(search) ||
                item.Run.Summary.Contains(search) ||
                item.Run.ErrorMessage.Contains(search) ||
                item.Run.Route.Contains(search));
        }

        if (query.FromUtc.HasValue)
        {
            runsQuery = runsQuery.Where(item => item.Run.FiredAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            runsQuery = runsQuery.Where(item => item.Run.FiredAtUtc <= query.ToUtc.Value);
        }

        return await runsQuery
            .OrderByDescending(item => item.Run.FiredAtUtc)
            .Take(take)
            .Select(item => new SchedulerPlanRunSummary(
                item.Run.Id,
                item.Plan.Id,
                item.Plan.Name,
                item.Plan.TargetKind,
                item.Plan.TargetNameSnapshot,
                item.Run.FiredAtUtc,
                item.Run.Status,
                item.Run.AttemptCount,
                item.Run.TargetRunId,
                item.Run.Route,
                item.Run.RetryCategory,
                item.Run.Summary,
                item.Run.ErrorMessage,
                item.Run.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private CanvasCalendarSurface BuildCalendarSurface(
        IReadOnlyList<SchedulerPlanSummary> plans,
        IReadOnlyList<SchedulerPlanRunSummary> history)
    {
        var now = clock.GetUtcNow();
        var events = new List<CanvasCalendarEvent>();
        foreach (var plan in plans.Where(item => item.IsEnabled))
        {
            foreach (var occurrence in ProjectOccurrences(plan, now, now.AddDays(30), 18))
            {
                events.Add(CreateCalendarEvent(
                    $"planned-{plan.Id:N}-{occurrence.UtcTicks}",
                    plan.Name,
                    $"{plan.CronDescription} / {plan.TargetKind} / {plan.TargetName}",
                    occurrence,
                    "Scheduled",
                    plan.TargetKind,
                    "#2563eb"));
            }
        }

        foreach (var run in history.Where(item => item.FiredAtUtc >= now.AddDays(-14)))
        {
            var color = run.Status switch
            {
                SchedulerPlanRunDispatchStatus.Failed => "#dc2626",
                SchedulerPlanRunDispatchStatus.NoMessages => "#64748b",
                SchedulerPlanRunDispatchStatus.WaitingForApproval => "#d97706",
                _ => "#059669"
            };
            events.Add(CreateCalendarEvent(
                $"run-{run.Id:N}",
                run.PlanName,
                string.IsNullOrWhiteSpace(run.ErrorMessage) ? run.Summary : run.ErrorMessage,
                run.FiredAtUtc,
                run.Status.ToString(),
                run.TargetKind,
                color));
        }

        return new CanvasCalendarSurface
        {
            SurfaceId = "scheduler-planner-calendar",
            InitialView = "week",
            SelectedDate = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Timezone = ResolveDefaultTimeZoneId(),
            Locale = CultureInfo.CurrentCulture.Name,
            SlotMinutes = 30,
            BusinessHoursStart = 6,
            BusinessHoursEnd = 22,
            MiniMonthCount = 1,
            AllowCreate = false,
            AllowEdit = false,
            AllowDelete = false,
            AllowDragDrop = false,
            AllowResize = false,
            EnableListExport = true,
            WorkspaceModal = false,
            EventTypes = ["Process", "Workflow"],
            EventStatuses = ["Scheduled", "Dispatching", "Dispatched", "Failed", "NoMessages", "WaitingForApproval"],
            TimeZoneOptions = BuildTimeZoneOptions(),
            Events = events
                .OrderBy(item => item.StartUtc)
                .Take(160)
                .ToList()
        };
    }

    private static CanvasCalendarEvent CreateCalendarEvent(
        string id,
        string title,
        string description,
        DateTimeOffset startUtc,
        string status,
        SchedulerPlanTargetKind targetKind,
        string color)
    {
        return new CanvasCalendarEvent
        {
            Id = id,
            EventId = id,
            Title = title,
            Description = description,
            StartUtc = startUtc,
            EndUtc = startUtc.AddMinutes(30),
            Timezone = "UTC",
            TimezoneName = "UTC",
            EventType = targetKind.ToString(),
            Status = status,
            Category = targetKind.ToString(),
            Color = color,
            ReadOnly = true,
            Notes = description
        };
    }

    private static IEnumerable<DateTimeOffset> ProjectOccurrences(
        SchedulerPlanSummary plan,
        DateTimeOffset fromUtc,
        DateTimeOffset untilUtc,
        int maxCount)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(plan.TimeZoneId);
        var cron = new CronExpression(plan.CronExpression)
        {
            TimeZone = timeZone
        };

        var cursor = fromUtc.AddSeconds(-1);
        for (var index = 0; index < maxCount; index++)
        {
            var next = cron.GetNextValidTimeAfter(cursor);
            if (!next.HasValue || next.Value > untilUtc)
            {
                yield break;
            }

            if (plan.StartAtUtc.HasValue && next.Value < plan.StartAtUtc.Value)
            {
                cursor = plan.StartAtUtc.Value.AddSeconds(-1);
                continue;
            }

            if (plan.EndAtUtc.HasValue && next.Value > plan.EndAtUtc.Value)
            {
                yield break;
            }

            yield return next.Value;
            cursor = next.Value.AddSeconds(1);
        }
    }

    private static SchedulerPlanSummary MapPlan(
        SchedulerPlan plan,
        AutomationTriggerDefinition? trigger)
    {
        return new SchedulerPlanSummary(
            plan.Id,
            plan.Name,
            plan.Description,
            plan.TargetKind,
            plan.TargetId,
            plan.TargetVersionId,
            plan.TargetNameSnapshot,
            plan.CronExpression,
            plan.CronDescription,
            plan.TimeZoneId,
            plan.MisfirePolicy,
            plan.IsEnabled,
            plan.StartAtUtc,
            plan.EndAtUtc,
            trigger?.NextPlannedFireAtUtc ?? plan.NextPlannedFireAtUtc,
            trigger?.LastFiredAtUtc ?? plan.LastFiredAtUtc,
            plan.LastError,
            plan.UpdatedAtUtc);
    }

    private static AutomationTriggerDefinition BuildTriggerDefinition(SchedulerPlan plan)
    {
        var payloadJson = JsonSerializer.Serialize(
            new SchedulerPlanAutomationPayload(
                plan.Id,
                plan.TargetKind,
                plan.TargetId,
                plan.TargetVersionId),
            JsonOptions);

        return new AutomationTriggerDefinition(
            plan.AutomationTriggerId,
            AutomationTriggerOwnerKind.Module,
            SchedulerPlannerConstants.AutomationOwnerKey,
            plan.AutomationTriggerKey,
            plan.IsEnabled,
            AutomationTriggerKind.Cron,
            plan.CronExpression,
            plan.TimeZoneId,
            plan.StartAtUtc,
            plan.EndAtUtc,
            plan.MisfirePolicy,
            payloadJson,
            $"scheduler-planner:{plan.Id:N}",
            plan.NextPlannedFireAtUtc,
            plan.LastFiredAtUtc,
            plan.UpdatedAtUtc);
    }

    private static string BuildTriggerKey(Guid planId)
    {
        return $"scheduler-plan-{planId:N}";
    }

    private static void ValidateEditor(SchedulerPlanEditorModel editor)
    {
        if (string.IsNullOrWhiteSpace(editor.Name))
        {
            throw new InvalidOperationException("Schedule name is required.");
        }

        if (editor.TargetId == Guid.Empty)
        {
            throw new InvalidOperationException("A workflow or process target is required.");
        }

        if (string.IsNullOrWhiteSpace(editor.TimeZoneId))
        {
            throw new InvalidOperationException("Schedule time zone is required.");
        }

        _ = TimeZoneInfo.FindSystemTimeZoneById(editor.TimeZoneId);

        if (string.IsNullOrWhiteSpace(editor.CronExpression) ||
            !CronExpression.IsValidExpression(editor.CronExpression))
        {
            throw new InvalidOperationException($"CRON expression '{editor.CronExpression}' is not a valid Quartz CRON expression.");
        }

        if (editor.EndAtUtc.HasValue &&
            editor.StartAtUtc.HasValue &&
            editor.EndAtUtc.Value <= editor.StartAtUtc.Value)
        {
            throw new InvalidOperationException("Schedule end time must be later than start time.");
        }

        _ = NormalizeJson(editor.InputJson);
    }

    private static string NormalizeJson(string? inputJson)
    {
        var normalized = string.IsNullOrWhiteSpace(inputJson) ? "{}" : inputJson.Trim();
        using var _ = JsonDocument.Parse(normalized);
        return normalized;
    }

    private static string ResolveDefaultTimeZoneId()
    {
        var localTimeZoneId = TimeZoneInfo.Local.Id;
        if (string.IsNullOrWhiteSpace(localTimeZoneId))
        {
            return "UTC";
        }

        if (localTimeZoneId.Contains('/', StringComparison.Ordinal))
        {
            return localTimeZoneId;
        }

        return TimeZoneInfo.TryConvertWindowsIdToIanaId(localTimeZoneId, out var ianaTimeZoneId)
            ? ianaTimeZoneId
            : "UTC";
    }

    private static List<string> BuildTimeZoneOptions()
    {
        return new[]
            {
                "UTC",
                ResolveDefaultTimeZoneId(),
                "America/New_York",
                "America/Chicago",
                "America/Denver",
                "America/Los_Angeles",
                "Europe/London",
                "Europe/Prague"
            }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public sealed class QuartzCronDescriptionService : ICronDescriptionService
{
    private static readonly IReadOnlyDictionary<string, string> DayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["SUN"] = "Sunday",
        ["MON"] = "Monday",
        ["TUE"] = "Tuesday",
        ["WED"] = "Wednesday",
        ["THU"] = "Thursday",
        ["FRI"] = "Friday",
        ["SAT"] = "Saturday"
    };

    public string Describe(string cronExpression, string timeZoneId)
    {
        if (!CronExpression.IsValidExpression(cronExpression))
        {
            throw new InvalidOperationException($"CRON expression '{cronExpression}' is not a valid Quartz CRON expression.");
        }

        _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var fields = cronExpression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 6)
        {
            throw new InvalidOperationException("Quartz CRON expressions must include seconds, minutes, hours, day, month, and weekday fields.");
        }

        var seconds = fields[0];
        var minutes = fields[1];
        var hours = fields[2];
        var dayOfMonth = fields[3];
        var month = fields[4];
        var dayOfWeek = fields[5];

        var timeText = DescribeTime(seconds, minutes, hours);
        var dayText = DescribeDay(dayOfMonth, dayOfWeek);
        var monthText = month == "*" ? "every month" : $"in month field '{month}'";
        return $"{timeText} {dayText} {monthText} ({timeZoneId}).";
    }

    private static string DescribeTime(string seconds, string minutes, string hours)
    {
        var secondText = seconds == "0" ? string.Empty : $" at second {seconds}";
        if (IsEveryInterval(minutes, out var minuteInterval) && hours == "*")
        {
            return $"Every {minuteInterval} minutes{secondText}";
        }

        if (IsEveryInterval(hours, out var hourInterval) && minutes == "0")
        {
            return $"Every {hourInterval} hours{secondText}";
        }

        if (int.TryParse(hours, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour) &&
            int.TryParse(minutes, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minute))
        {
            return $"At {hour:00}:{minute:00}{secondText}";
        }

        if (hours == "*" && int.TryParse(minutes, NumberStyles.Integer, CultureInfo.InvariantCulture, out var exactMinute))
        {
            return $"At minute {exactMinute:00} of every hour{secondText}";
        }

        return $"At hour field '{hours}' and minute field '{minutes}'{secondText}";
    }

    private static string DescribeDay(string dayOfMonth, string dayOfWeek)
    {
        if ((dayOfMonth == "*" || dayOfMonth == "?") &&
            (dayOfWeek == "*" || dayOfWeek == "?"))
        {
            return "every day";
        }

        if ((dayOfMonth == "*" || dayOfMonth == "?") &&
            !string.IsNullOrWhiteSpace(dayOfWeek))
        {
            return $"on {DescribeDayOfWeek(dayOfWeek)}";
        }

        if (dayOfWeek == "?")
        {
            return $"on day {dayOfMonth}";
        }

        return $"on day field '{dayOfMonth}' and weekday field '{dayOfWeek}'";
    }

    private static string DescribeDayOfWeek(string value)
    {
        if (value.Contains('-', StringComparison.Ordinal))
        {
            var parts = value.Split('-', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 &&
                DayNames.TryGetValue(parts[0], out var start) &&
                DayNames.TryGetValue(parts[1], out var end))
            {
                return $"{start} through {end}";
            }
        }

        if (value.Contains(',', StringComparison.Ordinal))
        {
            return string.Join(
                ", ",
                value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => DayNames.GetValueOrDefault(part.Trim(), part.Trim())));
        }

        return DayNames.GetValueOrDefault(value, value);
    }

    private static bool IsEveryInterval(string value, out int interval)
    {
        interval = 0;
        if (value.StartsWith("0/", StringComparison.Ordinal) ||
            value.StartsWith("*/", StringComparison.Ordinal))
        {
            return int.TryParse(value[2..], NumberStyles.Integer, CultureInfo.InvariantCulture, out interval) && interval > 0;
        }

        return false;
    }
}

public sealed class SchedulerTargetLauncher(
    ProcessesService processesService,
    IWorkflowCatalogService workflowCatalogService,
    IWorkflowRuntimeManager workflowRuntimeManager) : ISchedulerTargetLauncher
{
    private const string WorkflowEventInlineJsonPropertyName = "inlineJson";
    private const string WorkflowNoMessagesPropertyName = "noMessages";
    private const string WorkflowRoutePropertyName = "route";
    private const string WorkflowSummaryPropertyName = "summary";

    public async Task<SchedulerTargetLaunchResult> LaunchAsync(
        SchedulerPlan plan,
        DateTimeOffset firedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return plan.TargetKind switch
        {
            SchedulerPlanTargetKind.Process => await LaunchProcessAsync(plan, firedAtUtc, cancellationToken),
            SchedulerPlanTargetKind.Workflow => await LaunchWorkflowAsync(plan, cancellationToken),
            _ => throw new InvalidOperationException($"Scheduler target kind '{plan.TargetKind}' is not supported.")
        };
    }

    private async Task<SchedulerTargetLaunchResult> LaunchProcessAsync(
        SchedulerPlan plan,
        DateTimeOffset firedAtUtc,
        CancellationToken cancellationToken)
    {
        var result = await processesService.StartRunFromTriggerAsync(
            new ProcessRunTriggerStartRequest
            {
                ProcessDefinitionId = plan.TargetId,
                RunName = $"{plan.Name} / {firedAtUtc:yyyy-MM-dd HH:mm} UTC",
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = $"Started by scheduler plan '{plan.Name}' ({plan.Id:D}) at {firedAtUtc:O}.",
                TriggerSourceKind = ProcessRunTriggerSourceKind.SchedulerPlan,
                TriggerSourceId = plan.Id,
                TriggerSourceName = plan.Name,
                RequestedBy = SchedulerPlannerConstants.AutomationOwnerKey
            },
            cancellationToken);
        if (result.IsFailure || result.Value == Guid.Empty)
        {
            var errors = result.Errors.Count == 0
                ? "Process run failed without an error message."
                : string.Join(" | ", result.Errors.Select(item => $"{item.Code}: {item.Message}"));
            throw new InvalidOperationException(errors);
        }

        return new SchedulerTargetLaunchResult(
            SchedulerPlanTargetKind.Process,
            result.Value,
            "Started",
            $"Started process run '{result.Value:D}'.");
    }

    private async Task<SchedulerTargetLaunchResult> LaunchWorkflowAsync(
        SchedulerPlan plan,
        CancellationToken cancellationToken)
    {
        var detail = await workflowCatalogService.GetDefinitionAsync(
            new WorkflowId(plan.TargetId),
            plan.TargetVersionId.HasValue ? new WorkflowVersionId(plan.TargetVersionId.Value) : null,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Workflow definition '{plan.TargetId:D}' was not found.");

        var validation = await workflowCatalogService.ValidateDefinitionAsync(detail.Definition, cancellationToken);
        if (!validation.Succeeded)
        {
            var issues = string.Join(" | ", validation.Issues.Take(5).Select(item => $"{item.Code}: {item.Message}"));
            throw new InvalidOperationException($"Workflow definition '{detail.Definition.Name}' is not valid for scheduled execution. {issues}");
        }

        var run = await workflowRuntimeManager.StartAsync(
            detail.Definition,
            new WorkflowRunStartRequest(
                detail.Definition.Id,
                detail.Definition.VersionId,
                string.IsNullOrWhiteSpace(plan.InputJson) ? "{}" : plan.InputJson,
                RequestedBackend: null,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null),
            cancellationToken);
        var events = await workflowRuntimeManager.ListEventsAsync(run.RunId, cancellationToken);
        var routeResult = ResolveWorkflowRouteResult(events);

        if (run.State == WorkflowRunState.WaitingForInput)
        {
            return new SchedulerTargetLaunchResult(
                SchedulerPlanTargetKind.Workflow,
                run.RunId.Value,
                run.State.ToString(),
                string.IsNullOrWhiteSpace(run.Summary)
                    ? $"Workflow run '{run.RunId.Value:D}' is waiting for approval."
                    : run.Summary,
                SchedulerPlanRunDispatchStatus.WaitingForApproval,
                SchedulerPlanRunRoutes.WaitingForApproval,
                SchedulerPlanRunRetryCategory.WorkflowWaitingForApproval);
        }

        if (run.State == WorkflowRunState.Failed)
        {
            var failureSummary = ResolveWorkflowFailureSummary(events, run.Summary);
            throw new SchedulerTargetLaunchException(
                $"Workflow run '{run.RunId.Value:D}' failed: {failureSummary}",
                SchedulerPlanRunRetryClassifier.ClassifyWorkflowFailure(failureSummary),
                SchedulerPlanRunRoutes.Failed,
                run.RunId.Value,
                SchedulerPlanTargetKind.Workflow);
        }

        var dispatchStatus = routeResult.IsNoMessages
            ? SchedulerPlanRunDispatchStatus.NoMessages
            : SchedulerPlanRunDispatchStatus.Dispatched;

        return new SchedulerTargetLaunchResult(
            SchedulerPlanTargetKind.Workflow,
            run.RunId.Value,
            routeResult.IsNoMessages ? SchedulerPlanRunDispatchStatus.NoMessages.ToString() : run.State.ToString(),
            routeResult.IsNoMessages
                ? ResolveNoMessagesSummary(routeResult.Summary)
                : !string.IsNullOrWhiteSpace(routeResult.Summary)
                ? routeResult.Summary
                : string.IsNullOrWhiteSpace(run.Summary)
                ? $"Started workflow run '{run.RunId.Value:D}'."
                : run.Summary,
            dispatchStatus,
            routeResult.Route,
            routeResult.IsNoMessages
                ? SchedulerPlanRunRetryCategory.NoAction
                : SchedulerPlanRunRetryCategory.None);
    }

    private static SchedulerWorkflowRouteLaunchResult ResolveWorkflowRouteResult(IReadOnlyList<WorkflowEventRecord> events)
    {
        foreach (var workflowEvent in events
                     .Reverse()
                     .Where(item => item.Kind is WorkflowEventKind.Output or WorkflowEventKind.ExecutorCompleted))
        {
            if (!TryResolveEventInlineJson(workflowEvent, out var payloadJson) ||
                !TryReadWorkflowRoutePayload(payloadJson, out var route, out var summary, out var isNoMessages))
            {
                continue;
            }

            return new SchedulerWorkflowRouteLaunchResult(
                string.IsNullOrWhiteSpace(route) ? SchedulerPlanRunRoutes.Processed : route,
                summary,
                isNoMessages);
        }

        return SchedulerWorkflowRouteLaunchResult.Processed;
    }

    private static bool TryResolveEventInlineJson(
        WorkflowEventRecord workflowEvent,
        out string payloadJson)
    {
        payloadJson = string.Empty;
        if (string.IsNullOrWhiteSpace(workflowEvent.PayloadJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(workflowEvent.PayloadJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty(WorkflowEventInlineJsonPropertyName, out var inlineJson) &&
                inlineJson.ValueKind == JsonValueKind.String)
            {
                payloadJson = inlineJson.GetString() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(payloadJson);
            }

            payloadJson = workflowEvent.PayloadJson;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadWorkflowRoutePayload(
        string payloadJson,
        out string route,
        out string summary,
        out bool isNoMessages)
    {
        route = string.Empty;
        summary = string.Empty;
        isNoMessages = false;
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (document.RootElement.TryGetProperty(WorkflowSummaryPropertyName, out var summaryElement) &&
                summaryElement.ValueKind == JsonValueKind.String)
            {
                summary = summaryElement.GetString() ?? string.Empty;
            }

            if (document.RootElement.TryGetProperty(WorkflowRoutePropertyName, out var routeElement) &&
                routeElement.ValueKind == JsonValueKind.String)
            {
                route = routeElement.GetString() ?? string.Empty;
            }

            if (document.RootElement.TryGetProperty(WorkflowNoMessagesPropertyName, out var noMessagesElement) &&
                noMessagesElement.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                noMessagesElement.GetBoolean())
            {
                route = SchedulerPlanRunRoutes.NoMessages;
                isNoMessages = true;
                return true;
            }

            if (string.Equals(route, SchedulerPlanRunRoutes.NoMessages, StringComparison.Ordinal))
            {
                isNoMessages = true;
            }

            return !string.IsNullOrWhiteSpace(route) || !string.IsNullOrWhiteSpace(summary);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string ResolveNoMessagesSummary(string summary)
        => string.IsNullOrWhiteSpace(summary)
            ? "No unprocessed Office365 email matched the configured address."
            : summary;

    private static string ResolveWorkflowFailureSummary(
        IReadOnlyList<WorkflowEventRecord> events,
        string runSummary)
    {
        var failureSummary = events
            .Reverse()
            .FirstOrDefault(item => item.Kind is WorkflowEventKind.Error or WorkflowEventKind.ExecutorFailed)
            ?.Message;
        if (!string.IsNullOrWhiteSpace(failureSummary))
        {
            return failureSummary;
        }

        return string.IsNullOrWhiteSpace(runSummary)
            ? "Workflow failed without an error summary."
            : runSummary;
    }

    private sealed record SchedulerWorkflowRouteLaunchResult(
        string Route,
        string Summary,
        bool IsNoMessages)
    {
        public static SchedulerWorkflowRouteLaunchResult Processed { get; } = new(
            SchedulerPlanRunRoutes.Processed,
            string.Empty,
            false);
    }
}

public sealed class SchedulerPlannerTriggerFireHandler(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ISchedulerTargetLauncher targetLauncher,
    IClock clock,
    ILogger<SchedulerPlannerTriggerFireHandler> logger) : AutomationMessageHandler<AutomationTriggerFireRequest>
{
    protected override async Task<AutomationMessageHandleResult> HandleAsync(
        AutomationTriggerFireRequest envelope,
        AutomationMessageContext context,
        CancellationToken cancellationToken)
    {
        if (envelope.OwnerKind != AutomationTriggerOwnerKind.Module ||
            !string.Equals(envelope.OwnerKey, SchedulerPlannerConstants.AutomationOwnerKey, StringComparison.Ordinal))
        {
            return AutomationMessageHandleResult.Completed();
        }

        SchedulerPlanAutomationPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<SchedulerPlanAutomationPayload>(envelope.PayloadJson, SerializerOptions)
                ?? throw new InvalidOperationException("Scheduler trigger payload was empty.");
        }
        catch (JsonException exception)
        {
            return AutomationMessageHandleResult.DeadLettered($"Scheduler trigger payload JSON is invalid: {exception.Message}");
        }

        if (payload.PlanId == Guid.Empty)
        {
            return AutomationMessageHandleResult.DeadLettered("Scheduler trigger payload does not contain a plan id.");
        }

        try
        {
            await DispatchAsync(payload.PlanId, envelope, context, cancellationToken);
            return AutomationMessageHandleResult.Completed();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Scheduler plan {PlanId} failed to dispatch after trigger {TriggerId} fired at {FiredAtUtc}.",
                payload.PlanId,
                envelope.TriggerId,
                envelope.FiredAtUtc);
            return AutomationMessageHandleResult.RetryScheduled(exception.Message);
        }
    }

    private async Task DispatchAsync(
        Guid planId,
        AutomationTriggerFireRequest envelope,
        AutomationMessageContext context,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var dedupeKey = context.DedupeKey ?? $"scheduler-planner:{planId:N}:{envelope.FiredAtUtc.UtcTicks}";

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var plan = await dbContext.Set<SchedulerPlan>().SingleOrDefaultAsync(item => item.Id == planId, cancellationToken)
            ?? throw new KeyNotFoundException($"Scheduler plan '{planId:D}' was not found.");
        if (!plan.IsEnabled)
        {
            logger.LogInformation(
                "Ignoring disabled scheduler plan {PlanId} after trigger {TriggerId} fired.",
                plan.Id,
                envelope.TriggerId);
            return;
        }

        var run = await dbContext.Set<SchedulerPlanRun>()
            .SingleOrDefaultAsync(item => item.DedupeKey == dedupeKey, cancellationToken);
        if (run is not null && IsNoRetryTerminalStatus(run.Status))
        {
            return;
        }

        if (run is null)
        {
            run = new SchedulerPlanRun
            {
                Id = Guid.NewGuid(),
                PlanId = plan.Id,
                DedupeKey = dedupeKey,
                AutomationEnvelopeId = context.EnvelopeId,
                CorrelationId = context.CorrelationId,
                FiredAtUtc = envelope.FiredAtUtc,
                CreatedAtUtc = now
            };
            await dbContext.Set<SchedulerPlanRun>().AddAsync(run, cancellationToken);
        }

        run.Status = SchedulerPlanRunDispatchStatus.Dispatching;
        run.AttemptCount++;
        run.TargetRunId = null;
        run.TargetRunKind = string.Empty;
        run.Summary = string.Empty;
        run.ErrorMessage = string.Empty;
        run.Route = string.Empty;
        run.RetryCategory = SchedulerPlanRunRetryCategory.None;
        run.DispatchedAtUtc = null;
        run.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var launchResult = await targetLauncher.LaunchAsync(plan, envelope.FiredAtUtc, cancellationToken);
            var completedAt = clock.GetUtcNow();
            run.Status = launchResult.DispatchStatus;
            run.TargetRunId = launchResult.TargetRunId;
            run.TargetRunKind = launchResult.TargetKind.ToString();
            run.Summary = launchResult.Summary;
            run.Route = launchResult.Route;
            run.RetryCategory = launchResult.RetryCategory;
            run.DispatchedAtUtc = completedAt;
            run.UpdatedAtUtc = completedAt;
            if (launchResult.DispatchStatus == SchedulerPlanRunDispatchStatus.Dispatched)
            {
                plan.LastError = string.Empty;
            }

            plan.LastFiredAtUtc = envelope.FiredAtUtc;
            plan.UpdatedAtUtc = completedAt;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (WorkflowExternalRequestPendingException.TryFind(exception, out _))
        {
            var waitingAt = clock.GetUtcNow();
            run.Status = SchedulerPlanRunDispatchStatus.WaitingForApproval;
            run.Summary = exception.GetBaseException().Message;
            run.ErrorMessage = string.Empty;
            run.Route = SchedulerPlanRunRoutes.WaitingForApproval;
            run.RetryCategory = SchedulerPlanRunRetryCategory.WorkflowWaitingForApproval;
            run.DispatchedAtUtc = waitingAt;
            run.UpdatedAtUtc = waitingAt;
            plan.LastFiredAtUtc = envelope.FiredAtUtc;
            plan.UpdatedAtUtc = waitingAt;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            var failedAt = clock.GetUtcNow();
            var retryCategory = SchedulerPlanRunRetryClassifier.Classify(exception);
            run.Status = SchedulerPlanRunDispatchStatus.Failed;
            if (exception is SchedulerTargetLaunchException launchException)
            {
                run.TargetRunId = launchException.TargetRunId;
                run.TargetRunKind = launchException.TargetKind?.ToString() ?? string.Empty;
            }

            run.ErrorMessage = exception.Message;
            run.Route = SchedulerPlanRunRoutes.Failed;
            run.RetryCategory = retryCategory;
            run.UpdatedAtUtc = failedAt;
            plan.LastError = exception.Message;
            plan.LastFiredAtUtc = envelope.FiredAtUtc;
            plan.UpdatedAtUtc = failedAt;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private static bool IsNoRetryTerminalStatus(SchedulerPlanRunDispatchStatus status)
        => status is SchedulerPlanRunDispatchStatus.Dispatched
            or SchedulerPlanRunDispatchStatus.NoMessages
            or SchedulerPlanRunDispatchStatus.WaitingForApproval;
}
