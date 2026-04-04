using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public sealed record RecruitmentApplicationListItemModel(
    Guid Id,
    Guid PartyId,
    string CandidateName,
    string DesiredRole,
    RecruitmentStage Stage,
    RecruitmentDecision Decision,
    string RecruiterName,
    string HiringManagerName,
    string TargetUnitName,
    string PrimaryEmail,
    string PrimaryPhone,
    DateOnly? AvailableFrom,
    bool HasWorkforceProfile,
    DateTimeOffset UpdatedAtUtc);

public sealed class RecruitmentApplicationEditorModel
{
    public Guid? Id { get; set; }
    public Guid? PartyId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public string CandidatePhone { get; set; } = string.Empty;
    public string CandidateSummary { get; set; } = string.Empty;
    public Guid? TargetUnitPartyId { get; set; }
    public Guid? RecruiterPartyId { get; set; }
    public Guid? HiringManagerPartyId { get; set; }
    public string DesiredRole { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public RecruitmentStage Stage { get; set; } = RecruitmentStage.Applied;
    public DateOnly? AvailableFrom { get; set; }
    public RecruitmentDecision Decision { get; set; } = RecruitmentDecision.Pending;
    public string StageNotes { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string LastChangedBy { get; set; } = "crm-hr-ui";
}

public sealed record RecruitmentStageHistoryItemModel(
    Guid Id,
    RecruitmentStage Stage,
    string Summary,
    string Notes,
    DateTimeOffset ChangedAtUtc,
    string ChangedBy);

public sealed class RecruitmentInterviewEditorModel
{
    public Guid? Id { get; set; }
    public Guid ApplicationId { get; set; }
    public DateTime? ScheduledAtLocal { get; set; }
    public RecruitmentInterviewType InterviewType { get; set; } = RecruitmentInterviewType.Screening;
    public Guid? InterviewerPartyId { get; set; }
    public RecruitmentInterviewOutcome Outcome { get; set; } = RecruitmentInterviewOutcome.Pending;
    public string Feedback { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

public sealed record RecruitmentInterviewItemModel(
    Guid Id,
    Guid ApplicationId,
    DateTimeOffset ScheduledAtUtc,
    RecruitmentInterviewType InterviewType,
    Guid? InterviewerPartyId,
    string InterviewerName,
    RecruitmentInterviewOutcome Outcome,
    string Recommendation,
    string Feedback);

public sealed class LifecycleTaskEditorModel
{
    public Guid? Id { get; set; }
    public Guid PartyId { get; set; }
    public LifecycleTaskKind TaskKind { get; set; } = LifecycleTaskKind.Onboarding;
    public string Title { get; set; } = string.Empty;
    public Guid? OwnerPartyId { get; set; }
    public DateOnly? DueDate { get; set; }
    public LifecycleTaskStatus Status { get; set; } = LifecycleTaskStatus.NotStarted;
    public Guid? RelatedProjectId { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed record LifecycleTaskItemModel(
    Guid Id,
    Guid PartyId,
    LifecycleTaskKind TaskKind,
    string Title,
    Guid? OwnerPartyId,
    string OwnerName,
    DateOnly? DueDate,
    LifecycleTaskStatus Status,
    Guid? RelatedProjectId,
    string RelatedProjectName,
    string Notes,
    bool IsOverdue);

public sealed class RecruitmentSupportAssignmentsEditorModel
{
    public Guid PartyId { get; set; }
    public Guid? ManagerPartyId { get; set; }
    public Guid? BuddyPartyId { get; set; }
    public Guid? MentorPartyId { get; set; }
    public string LastChangedBy { get; set; } = "crm-hr-ui";
}

public sealed record RecruitmentSupportAssignmentsModel(
    Guid PartyId,
    Guid? ManagerPartyId,
    string ManagerName,
    Guid? BuddyPartyId,
    string BuddyName,
    Guid? MentorPartyId,
    string MentorName);

public sealed record RecruitmentProjectOptionModel(
    Guid Id,
    string Name,
    string CurrentPhase,
    ProjectStatus Status);

public sealed class RecruitmentConversionEditorModel
{
    public Guid ApplicationId { get; set; }
    public WorkforceKind WorkforceKind { get; set; } = WorkforceKind.Employee;
    public string JobTitle { get; set; } = string.Empty;
    public string Discipline { get; set; } = string.Empty;
    public string Seniority { get; set; } = string.Empty;
    public Guid? HomeUnitPartyId { get; set; }
    public Guid? ManagerPartyId { get; set; }
    public DateOnly? StartDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public decimal CapacityHoursPerWeek { get; set; } = 40m;
    public string Status { get; set; } = "Active";
    public string Notes { get; set; } = string.Empty;
    public string LastChangedBy { get; set; } = "crm-hr-ui";
}

public sealed record RecruitmentWorkspaceModel(
    IReadOnlyList<RecruitmentApplicationListItemModel> Applications,
    RecruitmentApplicationEditorModel Application,
    bool HasSelectedApplication,
    string CandidateDisplayName,
    string CandidateSummary,
    string CandidatePrimaryEmail,
    string CandidatePrimaryPhone,
    bool HasWorkforceProfile,
    IReadOnlyList<PartyOptionModel> CandidateOptions,
    IReadOnlyList<PartyOptionModel> RecruiterOptions,
    IReadOnlyList<PartyOptionModel> HiringManagerOptions,
    IReadOnlyList<PartyOptionModel> TargetUnitOptions,
    IReadOnlyList<PartyOptionModel> SupportOptions,
    IReadOnlyList<RecruitmentStageHistoryItemModel> StageHistory,
    IReadOnlyList<RecruitmentInterviewItemModel> Interviews,
    IReadOnlyList<LifecycleTaskItemModel> LifecycleTasks,
    RecruitmentSupportAssignmentsModel SupportAssignments,
    IReadOnlyList<RecruitmentProjectOptionModel> ProjectOptions,
    RecruitmentConversionEditorModel Conversion);

public sealed partial class RecruitingService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    PartyDirectoryService partyDirectoryService,
    HrService hrService,
    ProjectsService projectsService,
    IActivityStream activityStream,
    ISearchIndexService searchIndexService)
{
    private const string RecruitmentApplicationEntityType = "RecruitmentApplication";
    private const string SupportBuddyLabel = "Buddy";
    private const string SupportMentorLabel = "Mentor";

    public async Task<IReadOnlyList<RecruitmentApplicationListItemModel>> ListRecruitmentApplicationsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var applications = await dbContext.Set<RecruitmentApplication>()
            .ToListAsync(cancellationToken);
        if (applications.Count == 0)
        {
            return [];
        }

        applications = applications
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.Stage)
            .ThenBy(item => item.DesiredRole)
            .ToList();

        var partyIds = applications
            .SelectMany(item => new[] { item.PartyId, item.TargetUnitPartyId, item.RecruiterPartyId, item.HiringManagerPartyId })
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToList();
        partyIds.AddRange(applications.Select(item => item.PartyId));
        partyIds = partyIds.Distinct().ToList();

        var parties = await dbContext.Set<Party>()
            .Where(item => partyIds.Contains(item.Id))
            .Select(item => new PartyOptionModel(item.Id, item.DisplayName, item.PartyType))
            .ToListAsync(cancellationToken);
        var contacts = await dbContext.Set<PartyContactPoint>()
            .Where(item => partyIds.Contains(item.PartyId))
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => new RecruitmentContactValue(item.PartyId, item.ContactType, item.Value, item.IsPrimary))
            .ToListAsync(cancellationToken);
        var workforcePartyIds = await dbContext.Set<WorkforceProfile>()
            .Where(item => applications.Select(application => application.PartyId).Contains(item.PartyId))
            .Select(item => item.PartyId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var partyMap = parties.ToDictionary(item => item.Id);
        var contactsByPartyId = contacts
            .GroupBy(item => item.PartyId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<RecruitmentContactValue>)group.ToList());
        var workforcePartySet = workforcePartyIds.ToHashSet();

        return applications.Select(item =>
        {
            var candidateContacts = contactsByPartyId.GetValueOrDefault(item.PartyId) ?? [];
            return new RecruitmentApplicationListItemModel(
                item.Id,
                item.PartyId,
                partyMap.GetValueOrDefault(item.PartyId)?.DisplayName ?? string.Empty,
                item.DesiredRole,
                item.Stage,
                item.Decision,
                ResolvePartyDisplayName(partyMap, item.RecruiterPartyId),
                ResolvePartyDisplayName(partyMap, item.HiringManagerPartyId),
                ResolvePartyDisplayName(partyMap, item.TargetUnitPartyId),
                ResolvePrimaryContactValue(candidateContacts, PartyContactType.Email),
                ResolvePrimaryContactValue(candidateContacts, PartyContactType.Phone),
                ToDateOnly(item.AvailableFromUtc),
                workforcePartySet.Contains(item.PartyId),
                item.UpdatedAtUtc);
        }).ToList();
    }

    public async Task<RecruitmentWorkspaceModel> GetRecruitmentWorkspaceAsync(
        Guid? applicationId = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var applications = await ListRecruitmentApplicationsAsync(cancellationToken);
        var peopleOptions = await LoadPartyOptionsAsync(
            dbContext,
            new[] { PartyType.Person },
            cancellationToken);
        var targetUnitOptions = await LoadPartyOptionsAsync(
            dbContext,
            new[] { PartyType.Organization, PartyType.OrganizationUnit },
            cancellationToken);
        var projectOptions = (await projectsService.ListAsync(cancellationToken))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new RecruitmentProjectOptionModel(item.Id, item.Name, item.CurrentPhase, item.Status))
            .ToList();

        if (!applicationId.HasValue)
        {
            return new RecruitmentWorkspaceModel(
                applications,
                CreateEmptyApplicationEditor(),
                false,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                false,
                peopleOptions,
                peopleOptions,
                peopleOptions,
                targetUnitOptions,
                peopleOptions,
                [],
                [],
                [],
                new RecruitmentSupportAssignmentsModel(Guid.Empty, null, string.Empty, null, string.Empty, null, string.Empty),
                projectOptions,
                CreateEmptyConversionEditor());
        }

        var application = await dbContext.Set<RecruitmentApplication>()
            .SingleOrDefaultAsync(item => item.Id == applicationId.Value, cancellationToken);
        if (application is null)
        {
            return new RecruitmentWorkspaceModel(
                applications,
                CreateEmptyApplicationEditor(),
                false,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                false,
                peopleOptions,
                peopleOptions,
                peopleOptions,
                targetUnitOptions,
                peopleOptions,
                [],
                [],
                [],
                new RecruitmentSupportAssignmentsModel(Guid.Empty, null, string.Empty, null, string.Empty, null, string.Empty),
                projectOptions,
                CreateEmptyConversionEditor());
        }

        var partyIds = new HashSet<Guid>(new[]
        {
            application.PartyId
        });
        AddIfHasValue(partyIds, application.TargetUnitPartyId);
        AddIfHasValue(partyIds, application.RecruiterPartyId);
        AddIfHasValue(partyIds, application.HiringManagerPartyId);

        var interviews = await dbContext.Set<RecruitmentInterview>()
            .Where(item => item.ApplicationId == application.Id)
            .ToListAsync(cancellationToken);
        interviews = interviews
            .OrderBy(item => item.ScheduledAtUtc)
            .ToList();
        foreach (var interview in interviews)
        {
            AddIfHasValue(partyIds, interview.InterviewerPartyId);
        }

        var tasks = await dbContext.Set<OnboardingTask>()
            .Where(item => item.PartyId == application.PartyId)
            .ToListAsync(cancellationToken);
        foreach (var task in tasks)
        {
            AddIfHasValue(partyIds, task.OwnerPartyId);
        }

        var supportAssignments = await LoadSupportAssignmentsAsync(dbContext, application.PartyId, cancellationToken);
        AddIfHasValue(partyIds, supportAssignments.ManagerPartyId);
        AddIfHasValue(partyIds, supportAssignments.BuddyPartyId);
        AddIfHasValue(partyIds, supportAssignments.MentorPartyId);

        var parties = await dbContext.Set<Party>()
            .Where(item => partyIds.Contains(item.Id))
            .Select(item => new PartyOptionModel(item.Id, item.DisplayName, item.PartyType))
            .ToListAsync(cancellationToken);
        var contacts = await dbContext.Set<PartyContactPoint>()
            .Where(item => partyIds.Contains(item.PartyId))
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => new RecruitmentContactValue(item.PartyId, item.ContactType, item.Value, item.IsPrimary))
            .ToListAsync(cancellationToken);
        var workloadProfile = await dbContext.Set<WorkforceProfile>()
            .SingleOrDefaultAsync(item => item.PartyId == application.PartyId, cancellationToken);
        var stageHistory = await LoadStageHistoryAsync(dbContext, application.Id, cancellationToken);

        var partyMap = parties.ToDictionary(item => item.Id);
        var contactMap = contacts
            .GroupBy(item => item.PartyId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<RecruitmentContactValue>)group.ToList());
        var candidateContacts = contactMap.GetValueOrDefault(application.PartyId) ?? [];

        return new RecruitmentWorkspaceModel(
            applications,
            MapApplication(application, partyMap, contactMap),
            true,
            ResolvePartyDisplayName(partyMap, application.PartyId),
            parties.FirstOrDefault(item => item.Id == application.PartyId)?.DisplayName is not null
                ? await LoadCandidateSummaryAsync(dbContext, application.PartyId, cancellationToken)
                : string.Empty,
            ResolvePrimaryContactValue(candidateContacts, PartyContactType.Email),
            ResolvePrimaryContactValue(candidateContacts, PartyContactType.Phone),
            workloadProfile is not null,
            peopleOptions,
            peopleOptions,
            peopleOptions,
            targetUnitOptions,
            peopleOptions,
            stageHistory,
            interviews.Select(item => new RecruitmentInterviewItemModel(
                item.Id,
                item.ApplicationId,
                item.ScheduledAtUtc,
                item.InterviewType,
                item.InterviewerPartyId,
                ResolvePartyDisplayName(partyMap, item.InterviewerPartyId),
                item.Outcome,
                item.Recommendation,
                item.Feedback)).ToList(),
            tasks
                .OrderBy(item => item.TaskKind)
                .ThenBy(item => item.DueDateUtc ?? DateTimeOffset.MaxValue)
                .ThenBy(item => item.Title)
                .Select(item => new LifecycleTaskItemModel(
                    item.Id,
                    item.PartyId,
                    item.TaskKind,
                    item.Title,
                    item.OwnerPartyId,
                    ResolvePartyDisplayName(partyMap, item.OwnerPartyId),
                    ToDateOnly(item.DueDateUtc),
                    item.Status,
                    item.RelatedProjectId,
                    projectOptions.FirstOrDefault(project => project.Id == item.RelatedProjectId)?.Name ?? string.Empty,
                    item.Notes,
                    item.DueDateUtc.HasValue &&
                    item.Status is not LifecycleTaskStatus.Completed &&
                    item.Status is not LifecycleTaskStatus.Cancelled &&
                    item.DueDateUtc.Value.Date < DateTimeOffset.UtcNow.Date))
                .ToList(),
            supportAssignments,
            projectOptions,
            CreateConversionEditor(application, supportAssignments, workloadProfile));
    }

    public async Task<Result<Guid>> SaveRecruitmentApplicationAsync(
        RecruitmentApplicationEditorModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (string.IsNullOrWhiteSpace(model.DesiredRole))
        {
            return Result<Guid>.Failure(Error.Validation("Desired role is required.", "crmhr.recruiting.role-required"));
        }

        var normalizedActor = NormalizeActor(model.LastChangedBy);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var partyIdResult = await ResolveCandidatePartyIdAsync(dbContext, model, cancellationToken);
        if (!partyIdResult.IsSuccess)
        {
            return Result<Guid>.Failure(partyIdResult.Errors.ToArray());
        }

        var partyId = partyIdResult.Value;
        var validationError = await ValidateApplicationPartiesAsync(dbContext, model, partyId, cancellationToken);
        if (validationError is not null)
        {
            return Result<Guid>.Failure(validationError);
        }

        var entity = model.Id.HasValue
            ? await dbContext.Set<RecruitmentApplication>().SingleOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;
        var isNew = entity is null;
        var previousStage = entity?.Stage;

        if (entity is null)
        {
            entity = new RecruitmentApplication
            {
                PartyId = partyId,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            dbContext.Set<RecruitmentApplication>().Add(entity);
        }

        entity.PartyId = partyId;
        entity.TargetUnitPartyId = model.TargetUnitPartyId;
        entity.RecruiterPartyId = model.RecruiterPartyId;
        entity.HiringManagerPartyId = model.HiringManagerPartyId;
        entity.DesiredRole = model.DesiredRole.Trim();
        entity.Source = model.Source.Trim();
        entity.Stage = model.Stage;
        entity.AvailableFromUtc = ToUtcDate(model.AvailableFrom);
        entity.Decision = model.Decision;
        entity.Notes = model.Notes.Trim();
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        var candidateName = await LoadPartyDisplayNameAsync(dbContext, partyId, cancellationToken);
        if (isNew)
        {
            await AppendAuditEntryAsync(
                dbContext,
                entity.Id,
                "RecruitmentCreated",
                $"Created recruitment pipeline for {candidateName}",
                BuildStageAuditDetail(null, entity.Stage, model.StageNotes),
                normalizedActor,
                cancellationToken);
        }
        else if (previousStage.HasValue && previousStage.Value != entity.Stage)
        {
            await AppendAuditEntryAsync(
                dbContext,
                entity.Id,
                "RecruitmentStageChanged",
                $"Moved {candidateName} to {entity.Stage}",
                BuildStageAuditDetail(previousStage.Value, entity.Stage, model.StageNotes),
                normalizedActor,
                cancellationToken);
        }

        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                isNew ? "RecruitmentCreated" : "RecruitmentUpdated",
                isNew ? $"Created candidate {candidateName}" : $"Updated candidate {candidateName}",
                $"{entity.DesiredRole} / {entity.Stage}",
                ArtifactKind: RecruitmentApplicationEntityType,
                ArtifactId: entity.Id,
                Route: BuildRecruitingRoute(entity.Id),
                Actor: normalizedActor),
            cancellationToken);
        await UpsertRecruitmentApplicationSearchDocumentAsync(entity.Id, cancellationToken);

        return Result<Guid>.Success(entity.Id);
    }

    public async Task<Result<Guid>> SaveRecruitmentInterviewAsync(
        RecruitmentInterviewEditorModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.ApplicationId == Guid.Empty)
        {
            return Result<Guid>.Failure(Error.Validation("Choose a recruitment application first.", "crmhr.recruiting.interview.application-required"));
        }

        if (!model.ScheduledAtLocal.HasValue)
        {
            return Result<Guid>.Failure(Error.Validation("Interview schedule is required.", "crmhr.recruiting.interview.schedule-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var application = await dbContext.Set<RecruitmentApplication>()
            .SingleOrDefaultAsync(item => item.Id == model.ApplicationId, cancellationToken);
        if (application is null)
        {
            return Result<Guid>.Failure(Error.Validation("The recruitment application was not found.", "crmhr.recruiting.interview.application-not-found"));
        }

        if (model.InterviewerPartyId.HasValue)
        {
            var interviewerError = await ValidatePersonPartyAsync(
                dbContext,
                model.InterviewerPartyId.Value,
                "Interviewer must reference an existing person.",
                "crmhr.recruiting.interview.interviewer-invalid",
                cancellationToken);
            if (interviewerError is not null)
            {
                return Result<Guid>.Failure(interviewerError);
            }
        }

        var entity = model.Id.HasValue
            ? await dbContext.Set<RecruitmentInterview>().SingleOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;
        if (entity is null)
        {
            entity = new RecruitmentInterview
            {
                ApplicationId = model.ApplicationId
            };
            dbContext.Set<RecruitmentInterview>().Add(entity);
        }

        entity.ApplicationId = model.ApplicationId;
        entity.ScheduledAtUtc = ToUtcDateTimeOffset(model.ScheduledAtLocal.Value);
        entity.InterviewType = model.InterviewType;
        entity.InterviewerPartyId = model.InterviewerPartyId;
        entity.Outcome = model.Outcome;
        entity.Feedback = model.Feedback.Trim();
        entity.Recommendation = model.Recommendation.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                "RecruitmentInterviewSaved",
                $"Saved {model.InterviewType} interview",
                entity.Outcome.ToString(),
                ArtifactKind: nameof(RecruitmentInterview),
                ArtifactId: entity.Id,
                Route: BuildRecruitingRoute(model.ApplicationId)),
            cancellationToken);

        return Result<Guid>.Success(entity.Id);
    }

    public async Task DeleteRecruitmentInterviewAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<RecruitmentInterview>()
            .SingleOrDefaultAsync(item => item.Id == interviewId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        var applicationId = entity.ApplicationId;
        dbContext.Set<RecruitmentInterview>().Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                "RecruitmentInterviewDeleted",
                "Deleted recruitment interview",
                ArtifactKind: nameof(RecruitmentInterview),
                ArtifactId: interviewId,
                Route: BuildRecruitingRoute(applicationId)),
            cancellationToken);
    }

    public async Task<Result<Guid>> SaveLifecycleTaskAsync(
        LifecycleTaskEditorModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.PartyId == Guid.Empty)
        {
            return Result<Guid>.Failure(Error.Validation("Choose a candidate first.", "crmhr.recruiting.task.party-required"));
        }

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            return Result<Guid>.Failure(Error.Validation("Task title is required.", "crmhr.recruiting.task.title-required"));
        }

        if (!model.OwnerPartyId.HasValue)
        {
            return Result<Guid>.Failure(Error.Validation("Task owner is required.", "crmhr.recruiting.task.owner-required"));
        }

        if (!model.DueDate.HasValue)
        {
            return Result<Guid>.Failure(Error.Validation("Task due date is required.", "crmhr.recruiting.task.due-date-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var partyExists = await dbContext.Set<Party>().AnyAsync(item => item.Id == model.PartyId, cancellationToken);
        if (!partyExists)
        {
            return Result<Guid>.Failure(Error.Validation("The selected candidate was not found.", "crmhr.recruiting.task.party-not-found"));
        }

        var ownerError = await ValidatePersonPartyAsync(
            dbContext,
            model.OwnerPartyId.Value,
            "Task owner must reference an existing person.",
            "crmhr.recruiting.task.owner-invalid",
            cancellationToken);
        if (ownerError is not null)
        {
            return Result<Guid>.Failure(ownerError);
        }

        if (model.RelatedProjectId.HasValue)
        {
            var projectExists = await dbContext.Set<Project>().AnyAsync(item => item.Id == model.RelatedProjectId.Value, cancellationToken);
            if (!projectExists)
            {
                return Result<Guid>.Failure(Error.Validation("The related project was not found.", "crmhr.recruiting.task.project-not-found"));
            }
        }

        var entity = model.Id.HasValue
            ? await dbContext.Set<OnboardingTask>().SingleOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;
        if (entity is null)
        {
            entity = new OnboardingTask
            {
                PartyId = model.PartyId
            };
            dbContext.Set<OnboardingTask>().Add(entity);
        }

        entity.PartyId = model.PartyId;
        entity.TaskKind = model.TaskKind;
        entity.Title = model.Title.Trim();
        entity.OwnerPartyId = model.OwnerPartyId;
        entity.DueDateUtc = ToUtcDate(model.DueDate);
        entity.Status = model.Status;
        entity.RelatedProjectId = model.RelatedProjectId;
        entity.Notes = model.Notes.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                "RecruitmentLifecycleTaskSaved",
                $"Saved {model.TaskKind} task {entity.Title}",
                entity.Status.ToString(),
                ProjectId: entity.RelatedProjectId,
                ArtifactKind: nameof(OnboardingTask),
                ArtifactId: entity.Id,
                Route: BuildRecruitingRouteForParty(model.PartyId)),
            cancellationToken);

        return Result<Guid>.Success(entity.Id);
    }

    public async Task DeleteLifecycleTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<OnboardingTask>()
            .SingleOrDefaultAsync(item => item.Id == taskId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        var partyId = entity.PartyId;
        dbContext.Set<OnboardingTask>().Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                "RecruitmentLifecycleTaskDeleted",
                "Deleted lifecycle task",
                ArtifactKind: nameof(OnboardingTask),
                ArtifactId: taskId,
                Route: BuildRecruitingRouteForParty(partyId)),
            cancellationToken);
    }

    public async Task<Result> SaveSupportAssignmentsAsync(
        RecruitmentSupportAssignmentsEditorModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.PartyId == Guid.Empty)
        {
            return Result.Failure(Error.Validation("Choose a candidate first.", "crmhr.recruiting.support.party-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var partyExists = await dbContext.Set<Party>().AnyAsync(item => item.Id == model.PartyId, cancellationToken);
        if (!partyExists)
        {
            return Result.Failure(Error.Validation("The selected candidate was not found.", "crmhr.recruiting.support.party-not-found"));
        }

        foreach (var assignment in new[]
        {
            (model.ManagerPartyId, "Manager must reference an existing person.", "crmhr.recruiting.support.manager-invalid"),
            (model.BuddyPartyId, "Buddy must reference an existing person.", "crmhr.recruiting.support.buddy-invalid"),
            (model.MentorPartyId, "Mentor must reference an existing person.", "crmhr.recruiting.support.mentor-invalid")
        })
        {
            if (!assignment.Item1.HasValue)
            {
                continue;
            }

            if (assignment.Item1.Value == model.PartyId)
            {
                return Result.Failure(Error.Validation("Candidate support assignments cannot reference the same party.", "crmhr.recruiting.support.self-reference"));
            }

            var validationError = await ValidatePersonPartyAsync(
                dbContext,
                assignment.Item1.Value,
                assignment.Item2,
                assignment.Item3,
                cancellationToken);
            if (validationError is not null)
            {
                return Result.Failure(validationError);
            }
        }

        var managedByRelationships = await dbContext.Set<PartyRelationship>()
            .Where(item => item.SourcePartyId == model.PartyId && item.RelationshipKind == PartyRelationshipKind.ManagedBy)
            .ToListAsync(cancellationToken);
        var supportRelationships = await dbContext.Set<PartyRelationship>()
            .Where(item => item.TargetPartyId == model.PartyId &&
                           item.RelationshipKind == PartyRelationshipKind.Supports &&
                           (item.Notes == SupportBuddyLabel || item.Notes == SupportMentorLabel))
            .ToListAsync(cancellationToken);

        dbContext.Set<PartyRelationship>().RemoveRange(managedByRelationships);
        dbContext.Set<PartyRelationship>().RemoveRange(supportRelationships);

        if (model.ManagerPartyId.HasValue)
        {
            dbContext.Set<PartyRelationship>().Add(new PartyRelationship
            {
                SourcePartyId = model.PartyId,
                TargetPartyId = model.ManagerPartyId.Value,
                RelationshipKind = PartyRelationshipKind.ManagedBy,
                IsPrimary = true,
                Notes = "Recruiting support manager"
            });
        }

        if (model.BuddyPartyId.HasValue)
        {
            dbContext.Set<PartyRelationship>().Add(new PartyRelationship
            {
                SourcePartyId = model.BuddyPartyId.Value,
                TargetPartyId = model.PartyId,
                RelationshipKind = PartyRelationshipKind.Supports,
                IsPrimary = true,
                Notes = SupportBuddyLabel
            });
        }

        if (model.MentorPartyId.HasValue)
        {
            dbContext.Set<PartyRelationship>().Add(new PartyRelationship
            {
                SourcePartyId = model.MentorPartyId.Value,
                TargetPartyId = model.PartyId,
                RelationshipKind = PartyRelationshipKind.Supports,
                IsPrimary = true,
                Notes = SupportMentorLabel
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                "RecruitmentSupportAssignmentsSaved",
                "Saved candidate support assignments",
                ArtifactKind: nameof(PartyRelationship),
                ArtifactId: model.PartyId,
                Route: BuildRecruitingRouteForParty(model.PartyId),
                Actor: NormalizeActor(model.LastChangedBy)),
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result<Guid>> ConvertCandidateAsync(
        RecruitmentConversionEditorModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.ApplicationId == Guid.Empty)
        {
            return Result<Guid>.Failure(Error.Validation("Choose a recruitment application first.", "crmhr.recruiting.convert.application-required"));
        }

        await using var validationContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var application = await validationContext.Set<RecruitmentApplication>()
            .SingleOrDefaultAsync(item => item.Id == model.ApplicationId, cancellationToken);
        if (application is null)
        {
            return Result<Guid>.Failure(Error.Validation("The recruitment application was not found.", "crmhr.recruiting.convert.application-not-found"));
        }

        var candidate = await validationContext.Set<Party>()
            .SingleOrDefaultAsync(item => item.Id == application.PartyId, cancellationToken);
        if (candidate is null)
        {
            return Result<Guid>.Failure(Error.Validation("The candidate party was not found.", "crmhr.recruiting.convert.party-not-found"));
        }

        var supportAssignments = await LoadSupportAssignmentsAsync(validationContext, candidate.Id, cancellationToken);
        var workforceResult = await hrService.SaveWorkforceProfileAsync(new WorkforceProfileEditorModel
        {
            PartyId = candidate.Id,
            WorkforceKind = model.WorkforceKind,
            JobTitle = string.IsNullOrWhiteSpace(model.JobTitle) ? application.DesiredRole : model.JobTitle,
            Discipline = model.Discipline,
            Seniority = model.Seniority,
            HomeUnitPartyId = model.HomeUnitPartyId ?? application.TargetUnitPartyId,
            ManagerPartyId = model.ManagerPartyId ?? supportAssignments.ManagerPartyId ?? application.HiringManagerPartyId,
            StartDate = model.StartDate,
            Location = model.Location,
            TimeZone = model.TimeZone,
            CapacityHoursPerWeek = model.CapacityHoursPerWeek <= 0m ? 40m : model.CapacityHoursPerWeek,
            Status = string.IsNullOrWhiteSpace(model.Status) ? "Active" : model.Status.Trim(),
            Notes = model.Notes,
            LastChangedBy = model.LastChangedBy
        }, cancellationToken);
        if (!workforceResult.IsSuccess)
        {
            return Result<Guid>.Failure(workforceResult.Errors.ToArray());
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var refreshedApplication = await dbContext.Set<RecruitmentApplication>()
            .SingleAsync(item => item.Id == model.ApplicationId, cancellationToken);
        var refreshedParty = await dbContext.Set<Party>()
            .SingleAsync(item => item.Id == candidate.Id, cancellationToken);

        var previousStage = refreshedApplication.Stage;
        refreshedApplication.Stage = RecruitmentStage.Hired;
        refreshedApplication.Decision = RecruitmentDecision.Approved;
        refreshedApplication.UpdatedAtUtc = DateTimeOffset.UtcNow;
        refreshedParty.LifecycleStatus = PartyLifecycleStatus.Active;
        refreshedParty.LastChangedBy = NormalizeActor(model.LastChangedBy);
        refreshedParty.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        if (previousStage != RecruitmentStage.Hired)
        {
            await AppendAuditEntryAsync(
                dbContext,
                refreshedApplication.Id,
                "RecruitmentStageChanged",
                $"Moved {refreshedParty.DisplayName} to {RecruitmentStage.Hired}",
                BuildStageAuditDetail(previousStage, RecruitmentStage.Hired, "Converted to workforce profile."),
                NormalizeActor(model.LastChangedBy),
                cancellationToken);
        }

        await AppendAuditEntryAsync(
            dbContext,
            refreshedApplication.Id,
            "RecruitmentConverted",
            $"Converted {refreshedParty.DisplayName} to workforce",
            JsonSerializer.Serialize(new
            {
                WorkforceKind = model.WorkforceKind.ToString(),
                JobTitle = string.IsNullOrWhiteSpace(model.JobTitle) ? application.DesiredRole : model.JobTitle
            }),
            NormalizeActor(model.LastChangedBy),
            cancellationToken);

        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                "RecruitmentConverted",
                $"Converted {refreshedParty.DisplayName} to workforce",
                model.WorkforceKind.ToString(),
                ArtifactKind: RecruitmentApplicationEntityType,
                ArtifactId: refreshedApplication.Id,
                Route: BuildRecruitingRoute(refreshedApplication.Id),
                Actor: NormalizeActor(model.LastChangedBy)),
            cancellationToken);
        await UpsertRecruitmentApplicationSearchDocumentAsync(refreshedApplication.Id, cancellationToken);

        return Result<Guid>.Success(candidate.Id);
}

    private static RecruitmentApplicationEditorModel CreateEmptyApplicationEditor()
    {
        return new RecruitmentApplicationEditorModel
        {
            Stage = RecruitmentStage.Applied,
            Decision = RecruitmentDecision.Pending
        };
    }

    private static RecruitmentConversionEditorModel CreateEmptyConversionEditor()
    {
        return new RecruitmentConversionEditorModel
        {
            WorkforceKind = WorkforceKind.Employee,
            CapacityHoursPerWeek = 40m,
            Status = "Active"
        };
    }

    private static RecruitmentApplicationEditorModel MapApplication(
        RecruitmentApplication application,
        IReadOnlyDictionary<Guid, PartyOptionModel> partyMap,
        IReadOnlyDictionary<Guid, IReadOnlyList<RecruitmentContactValue>> contactMap)
    {
        var contacts = contactMap.GetValueOrDefault(application.PartyId) ?? [];
        return new RecruitmentApplicationEditorModel
        {
            Id = application.Id,
            PartyId = application.PartyId,
            CandidateName = ResolvePartyDisplayName(partyMap, application.PartyId),
            CandidateEmail = ResolvePrimaryContactValue(contacts, PartyContactType.Email),
            CandidatePhone = ResolvePrimaryContactValue(contacts, PartyContactType.Phone),
            TargetUnitPartyId = application.TargetUnitPartyId,
            RecruiterPartyId = application.RecruiterPartyId,
            HiringManagerPartyId = application.HiringManagerPartyId,
            DesiredRole = application.DesiredRole,
            Source = application.Source,
            Stage = application.Stage,
            AvailableFrom = ToDateOnly(application.AvailableFromUtc),
            Decision = application.Decision,
            Notes = application.Notes
        };
    }

    private static RecruitmentConversionEditorModel CreateConversionEditor(
        RecruitmentApplication application,
        RecruitmentSupportAssignmentsModel supportAssignments,
        WorkforceProfile? workforceProfile)
    {
        return new RecruitmentConversionEditorModel
        {
            ApplicationId = application.Id,
            WorkforceKind = workforceProfile?.WorkforceKind ?? WorkforceKind.Employee,
            JobTitle = workforceProfile?.JobTitle ?? application.DesiredRole,
            Discipline = workforceProfile?.Discipline ?? string.Empty,
            Seniority = workforceProfile?.Seniority ?? string.Empty,
            HomeUnitPartyId = workforceProfile?.HomeUnitPartyId ?? application.TargetUnitPartyId,
            ManagerPartyId = workforceProfile?.ManagerPartyId ?? supportAssignments.ManagerPartyId ?? application.HiringManagerPartyId,
            StartDate = ToDateOnly(workforceProfile?.StartDateUtc),
            Location = workforceProfile?.Location ?? string.Empty,
            TimeZone = workforceProfile?.TimeZone ?? string.Empty,
            CapacityHoursPerWeek = workforceProfile?.CapacityHoursPerWeek ?? 40m,
            Status = string.IsNullOrWhiteSpace(workforceProfile?.Status) ? "Active" : workforceProfile.Status,
            Notes = workforceProfile?.Notes ?? string.Empty
        };
    }

    private async Task<Result<Guid>> ResolveCandidatePartyIdAsync(
        AppDbContext dbContext,
        RecruitmentApplicationEditorModel model,
        CancellationToken cancellationToken)
    {
        if (model.PartyId.HasValue)
        {
            var candidateError = await ValidatePersonPartyAsync(
                dbContext,
                model.PartyId.Value,
                "Candidate must reference an existing person.",
                "crmhr.recruiting.candidate-invalid",
                cancellationToken);
            return candidateError is null
                ? Result<Guid>.Success(model.PartyId.Value)
                : Result<Guid>.Failure(candidateError);
        }

        if (string.IsNullOrWhiteSpace(model.CandidateName))
        {
            return Result<Guid>.Failure(Error.Validation("Choose an existing candidate or provide a new candidate name.", "crmhr.recruiting.candidate-required"));
        }

        var savePartyResult = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = model.CandidateName.Trim(),
            Summary = model.CandidateSummary.Trim(),
            LastChangedBy = NormalizeActor(model.LastChangedBy),
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = PartyRoleKind.Candidate,
                    Title = "Candidate",
                    IsPrimary = true
                }
            ],
            ContactPoints = BuildCandidateContacts(model)
        }, cancellationToken);

        return savePartyResult.IsSuccess
            ? Result<Guid>.Success(savePartyResult.Value)
            : Result<Guid>.Failure(savePartyResult.Errors.ToArray());
    }

    private static List<PartyContactPointEditorModel> BuildCandidateContacts(RecruitmentApplicationEditorModel model)
    {
        var contacts = new List<PartyContactPointEditorModel>();
        if (!string.IsNullOrWhiteSpace(model.CandidateEmail))
        {
            var email = model.CandidateEmail.Trim();
            contacts.Add(new PartyContactPointEditorModel
            {
                ContactType = PartyContactType.Email,
                Label = "Primary email",
                Value = email,
                NormalizedValue = email.ToLowerInvariant(),
                IsPrimary = true,
                IsPublic = true
            });
        }

        if (!string.IsNullOrWhiteSpace(model.CandidatePhone))
        {
            contacts.Add(new PartyContactPointEditorModel
            {
                ContactType = PartyContactType.Phone,
                Label = "Primary phone",
                Value = model.CandidatePhone.Trim(),
                NormalizedValue = model.CandidatePhone.Trim(),
                IsPrimary = string.IsNullOrWhiteSpace(model.CandidateEmail),
                IsPublic = true
            });
        }

        return contacts;
    }

    private async Task<Error?> ValidateApplicationPartiesAsync(
        AppDbContext dbContext,
        RecruitmentApplicationEditorModel model,
        Guid candidatePartyId,
        CancellationToken cancellationToken)
    {
        if (model.RecruiterPartyId.HasValue)
        {
            var recruiterError = await ValidatePersonPartyAsync(
                dbContext,
                model.RecruiterPartyId.Value,
                "Recruiter must reference an existing person.",
                "crmhr.recruiting.recruiter-invalid",
                cancellationToken);
            if (recruiterError is not null)
            {
                return recruiterError;
            }
        }

        if (model.HiringManagerPartyId.HasValue)
        {
            if (model.HiringManagerPartyId.Value == candidatePartyId)
            {
                return Error.Validation("Candidate cannot be the hiring manager.", "crmhr.recruiting.hiring-manager-self");
            }

            var hiringManagerError = await ValidatePersonPartyAsync(
                dbContext,
                model.HiringManagerPartyId.Value,
                "Hiring manager must reference an existing person.",
                "crmhr.recruiting.hiring-manager-invalid",
                cancellationToken);
            if (hiringManagerError is not null)
            {
                return hiringManagerError;
            }
        }

        if (model.TargetUnitPartyId.HasValue)
        {
            var targetUnit = await dbContext.Set<Party>()
                .Select(item => new
                {
                    item.Id,
                    item.PartyType
                })
                .SingleOrDefaultAsync(item => item.Id == model.TargetUnitPartyId.Value, cancellationToken);
            if (targetUnit is null ||
                (targetUnit.PartyType != PartyType.Organization && targetUnit.PartyType != PartyType.OrganizationUnit))
            {
                return Error.Validation("Target unit must reference an existing organization or organization unit.", "crmhr.recruiting.target-unit-invalid");
            }
        }

        return null;
    }

    private static async Task<Error?> ValidatePersonPartyAsync(
        AppDbContext dbContext,
        Guid partyId,
        string message,
        string code,
        CancellationToken cancellationToken)
    {
        var party = await dbContext.Set<Party>()
            .Select(item => new
            {
                item.Id,
                item.PartyType
            })
            .SingleOrDefaultAsync(item => item.Id == partyId, cancellationToken);
        return party is null || party.PartyType != PartyType.Person
            ? Error.Validation(message, code)
            : null;
    }

    private static async Task<IReadOnlyList<PartyOptionModel>> LoadPartyOptionsAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<PartyType> allowedTypes,
        CancellationToken cancellationToken)
    {
        if (allowedTypes.Count == 0)
        {
            return [];
        }

        return await dbContext.Set<Party>()
            .Where(item => allowedTypes.Contains(item.PartyType))
            .OrderBy(item => item.DisplayName)
            .Select(item => new PartyOptionModel(item.Id, item.DisplayName, item.PartyType))
            .ToListAsync(cancellationToken);
    }

    private async Task<RecruitmentSupportAssignmentsModel> LoadSupportAssignmentsAsync(
        AppDbContext dbContext,
        Guid partyId,
        CancellationToken cancellationToken)
    {
        var relationships = await dbContext.Set<PartyRelationship>()
            .Where(item => (item.SourcePartyId == partyId && item.RelationshipKind == PartyRelationshipKind.ManagedBy) ||
                           (item.TargetPartyId == partyId &&
                            item.RelationshipKind == PartyRelationshipKind.Supports &&
                            (item.Notes == SupportBuddyLabel || item.Notes == SupportMentorLabel)))
            .ToListAsync(cancellationToken);
        if (relationships.Count == 0)
        {
            return new RecruitmentSupportAssignmentsModel(partyId, null, string.Empty, null, string.Empty, null, string.Empty);
        }

        var relatedPartyIds = relationships
            .SelectMany(item => new[] { item.SourcePartyId, item.TargetPartyId })
            .Where(item => item != partyId)
            .Distinct()
            .ToList();
        var partyNames = relatedPartyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Set<Party>()
                .Where(item => relatedPartyIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);

        var manager = relationships.FirstOrDefault(item => item.RelationshipKind == PartyRelationshipKind.ManagedBy);
        var buddy = relationships.FirstOrDefault(item => item.RelationshipKind == PartyRelationshipKind.Supports && item.Notes == SupportBuddyLabel);
        var mentor = relationships.FirstOrDefault(item => item.RelationshipKind == PartyRelationshipKind.Supports && item.Notes == SupportMentorLabel);

        return new RecruitmentSupportAssignmentsModel(
            partyId,
            manager?.TargetPartyId,
            manager is null ? string.Empty : partyNames.GetValueOrDefault(manager.TargetPartyId) ?? string.Empty,
            buddy?.SourcePartyId,
            buddy is null ? string.Empty : partyNames.GetValueOrDefault(buddy.SourcePartyId) ?? string.Empty,
            mentor?.SourcePartyId,
            mentor is null ? string.Empty : partyNames.GetValueOrDefault(mentor.SourcePartyId) ?? string.Empty);
    }

    private async Task<IReadOnlyList<RecruitmentStageHistoryItemModel>> LoadStageHistoryAsync(
        AppDbContext dbContext,
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var entries = await dbContext.Set<CrmHrAuditEntry>()
            .Where(item => item.EntityType == RecruitmentApplicationEntityType &&
                           item.EntityId == applicationId &&
                           (item.Action == "RecruitmentCreated" || item.Action == "RecruitmentStageChanged"))
            .ToListAsync(cancellationToken);
        entries = entries
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();

        return entries.Select(MapStageHistory)
            .Where(item => item is not null)
            .Cast<RecruitmentStageHistoryItemModel>()
            .ToList();
    }

    private static RecruitmentStageHistoryItemModel? MapStageHistory(CrmHrAuditEntry entry)
    {
        if (!TryReadStageAuditDetail(entry.DetailJson, out var stage, out var notes))
        {
            return null;
        }

        return new RecruitmentStageHistoryItemModel(
            entry.Id,
            stage,
            entry.Summary,
            notes,
            entry.CreatedAtUtc,
            string.IsNullOrWhiteSpace(entry.Actor) ? "crm-hr-ui" : entry.Actor);
    }

    private static bool TryReadStageAuditDetail(string json, out RecruitmentStage stage, out string notes)
    {
        stage = RecruitmentStage.Applied;
        notes = string.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("Stage", out var stageElement))
            {
                return false;
            }

            var stageValue = stageElement.GetString();
            if (string.IsNullOrWhiteSpace(stageValue) || !Enum.TryParse(stageValue, true, out stage))
            {
                return false;
            }

            if (root.TryGetProperty("Notes", out var notesElement))
            {
                notes = notesElement.GetString() ?? string.Empty;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task AppendAuditEntryAsync(
        AppDbContext dbContext,
        Guid applicationId,
        string action,
        string summary,
        string detailJson,
        string actor,
        CancellationToken cancellationToken)
    {
        await dbContext.Set<CrmHrAuditEntry>().AddAsync(new CrmHrAuditEntry
        {
            EntityType = RecruitmentApplicationEntityType,
            EntityId = applicationId,
            Action = action,
            Summary = summary.Trim(),
            DetailJson = string.IsNullOrWhiteSpace(detailJson) ? "{}" : detailJson,
            Actor = actor,
            CreatedAtUtc = DateTimeOffset.UtcNow
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string BuildStageAuditDetail(RecruitmentStage? previousStage, RecruitmentStage stage, string notes)
    {
        return JsonSerializer.Serialize(new
        {
            PreviousStage = previousStage?.ToString(),
            Stage = stage.ToString(),
            Notes = notes?.Trim() ?? string.Empty
        });
    }

    private static string ResolvePartyDisplayName(IReadOnlyDictionary<Guid, PartyOptionModel> partyMap, Guid? partyId)
    {
        return partyId.HasValue && partyMap.TryGetValue(partyId.Value, out var party)
            ? party.DisplayName
            : string.Empty;
    }

    private static string ResolvePrimaryContactValue(IReadOnlyList<RecruitmentContactValue> contacts, PartyContactType contactType)
    {
        return contacts.FirstOrDefault(item => item.ContactType == contactType)?.Value ?? string.Empty;
    }

    private static void AddIfHasValue(ISet<Guid> values, Guid? candidate)
    {
        if (candidate.HasValue)
        {
            values.Add(candidate.Value);
        }
    }

    private static string NormalizeActor(string? actor)
    {
        return string.IsNullOrWhiteSpace(actor) ? "crm-hr-ui" : actor.Trim();
    }

    private static string BuildRecruitingRoute(Guid applicationId)
    {
        return $"/crm-hr/recruiting?applicationId={applicationId:D}";
    }

    private static string BuildRecruitingRouteForParty(Guid partyId)
    {
        return $"/crm-hr/recruiting?partyId={partyId:D}";
    }

    private static DateTimeOffset? ToUtcDate(DateOnly? value)
    {
        return value.HasValue
            ? new DateTimeOffset(value.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : null;
    }

    private static DateOnly? ToDateOnly(DateTimeOffset? value)
    {
        return value.HasValue
            ? DateOnly.FromDateTime(value.Value.UtcDateTime)
            : null;
    }

    private static DateTimeOffset ToUtcDateTimeOffset(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(value),
            DateTimeKind.Local => new DateTimeOffset(value).ToUniversalTime(),
            _ => new DateTimeOffset(value, TimeZoneInfo.Local.GetUtcOffset(value)).ToUniversalTime()
        };
    }

    private async Task<string> LoadPartyDisplayNameAsync(AppDbContext dbContext, Guid partyId, CancellationToken cancellationToken)
    {
        return await dbContext.Set<Party>()
            .Where(item => item.Id == partyId)
            .Select(item => item.DisplayName)
            .SingleAsync(cancellationToken);
    }

    private async Task<string> LoadCandidateSummaryAsync(AppDbContext dbContext, Guid partyId, CancellationToken cancellationToken)
    {
        return await dbContext.Set<Party>()
            .Where(item => item.Id == partyId)
            .Select(item => item.Summary)
            .SingleOrDefaultAsync(cancellationToken)
            ?? string.Empty;
    }

    private sealed record RecruitmentContactValue(
        Guid PartyId,
        PartyContactType ContactType,
        string Value,
        bool IsPrimary);
}
