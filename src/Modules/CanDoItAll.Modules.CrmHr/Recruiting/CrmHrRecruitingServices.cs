using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public sealed class RecruitingService(
    IDbContextFactory<CrmHrDbContext> dbContextFactory,
    IProjectRecordQueryService projectRecordQueryService,
    IActivityStream activityStream,
    PartyRelationshipService relationships,
    RecruitmentApplicationEditingService applications, RecruitmentInterviewEditingService interviews, RecruitmentLifecycleEditingService lifecycle,
    RecruitmentConversionService conversion)
{
    private const string RecruitmentApplicationEntityType = "RecruitmentApplication";

    public async Task<RecruitmentApplicationPage> SearchRecruitmentApplicationsAsync(
        RecruitmentApplicationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var normalized = NormalizeRecruitmentApplicationQuery(query);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<RecruitmentApplication> applications = dbContext
            .Set<RecruitmentApplication>()
            .AsNoTracking();

        if (TryMapRecruitmentStage(normalized.Scope) is { } stage)
        {
            applications = applications.Where(application => application.Stage == stage);
        }

        if (!string.IsNullOrEmpty(normalized.SearchText))
        {
            var search = normalized.SearchText.ToUpperInvariant();
            applications = applications.Where(application =>
                application.DesiredRole.ToUpper().Contains(search) ||
                application.Source.ToUpper().Contains(search) ||
                dbContext.Set<Party>().Any(party =>
                    (party.Id == application.PartyId ||
                     party.Id == application.RecruiterPartyId ||
                     party.Id == application.HiringManagerPartyId ||
                     party.Id == application.TargetUnitPartyId) &&
                    party.DisplayName.ToUpper().Contains(search)) ||
                dbContext.Set<PartyContactPoint>().Any(contact =>
                    contact.PartyId == application.PartyId &&
                    contact.IsPublic &&
                    contact.ContactType == PartyContactType.Email &&
                    contact.Value.ToUpper().Contains(search)));
        }

        var totalCount = await applications.CountAsync(cancellationToken);
        var pageRows = await applications
            .OrderByDescending(application => application.UpdatedAtUtc)
            .ThenBy(application => application.Id)
            .Skip(normalized.PageIndex * normalized.PageSize)
            .Take(normalized.PageSize)
            .Select(application => new RecruitmentApplicationListProjection(
                application.Id,
                application.PartyId,
                dbContext.Set<Party>()
                    .Where(party => party.Id == application.PartyId)
                    .Select(party => party.PartyType)
                    .FirstOrDefault(),
                dbContext.Set<Party>()
                    .Where(party => party.Id == application.PartyId)
                    .Select(party => party.DisplayName)
                    .FirstOrDefault() ?? string.Empty,
                application.DesiredRole,
                application.Stage,
                application.Decision,
                dbContext.Set<Party>()
                    .Where(party => party.Id == application.RecruiterPartyId)
                    .Select(party => party.DisplayName)
                    .FirstOrDefault() ?? string.Empty,
                dbContext.Set<Party>()
                    .Where(party => party.Id == application.HiringManagerPartyId)
                    .Select(party => party.DisplayName)
                    .FirstOrDefault() ?? string.Empty,
                dbContext.Set<Party>()
                    .Where(party => party.Id == application.TargetUnitPartyId)
                    .Select(party => party.DisplayName)
                    .FirstOrDefault() ?? string.Empty,
                dbContext.Set<PartyContactPoint>()
                    .Where(contact =>
                        contact.PartyId == application.PartyId &&
                        contact.IsPublic &&
                        contact.ContactType == PartyContactType.Email)
                    .OrderByDescending(contact => contact.IsPrimary)
                    .ThenBy(contact => contact.Id)
                    .Select(contact => contact.Value)
                    .FirstOrDefault() ?? string.Empty,
                dbContext.Set<PartyContactPoint>()
                    .Where(contact =>
                        contact.PartyId == application.PartyId &&
                        contact.IsPublic &&
                        contact.ContactType == PartyContactType.Phone)
                    .OrderByDescending(contact => contact.IsPrimary)
                    .ThenBy(contact => contact.Id)
                    .Select(contact => contact.Value)
                    .FirstOrDefault() ?? string.Empty,
                application.AvailableFromUtc,
                dbContext.Set<WorkforceProfile>().Any(profile => profile.PartyId == application.PartyId),
                application.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new RecruitmentApplicationPage(
            pageRows.Select(MapRecruitmentApplicationListItem).ToList(),
            normalized.PageIndex,
            normalized.PageSize,
            totalCount);
    }

    public async Task<RecruitmentApplicationSummary> GetRecruitmentApplicationSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<RecruitmentApplication>()
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(applications => new RecruitmentApplicationSummary(
                applications.Count(),
                applications.Count(application => application.Stage == RecruitmentStage.Interviewing),
                applications.Count(application =>
                    application.Stage == RecruitmentStage.Offer ||
                    application.Stage == RecruitmentStage.Hired)))
            .SingleOrDefaultAsync(cancellationToken)
            ?? new RecruitmentApplicationSummary(0, 0, 0);
    }

    public async Task<RecruitmentWorkspaceModel> GetRecruitmentWorkspaceAsync(
        Guid? applicationId = null,
        Guid? partyId = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var snapshot = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken) : null;
        var resolvedApplicationId = applicationId;
        if (!resolvedApplicationId.HasValue && partyId.HasValue)
        {
            resolvedApplicationId = await dbContext.Set<RecruitmentApplication>()
                .AsNoTracking()
                .Where(item => item.PartyId == partyId.Value)
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ThenBy(item => item.Id)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!resolvedApplicationId.HasValue)
        {
            return new RecruitmentWorkspaceModel(
                CreateEmptyApplicationEditor(),
                false,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                AiResourceBindingStatus.Unbound,
                string.Empty,
                string.Empty,
                false,
                [],
                [],
                [],
                new RecruitmentSupportAssignmentsModel(Guid.Empty, null, string.Empty, null, string.Empty, null, string.Empty),
                CreateEmptyConversionEditor());
        }

        var application = await dbContext.Set<RecruitmentApplication>()
            .SingleOrDefaultAsync(item => item.Id == resolvedApplicationId.Value, cancellationToken);
        if (application is null ||
            partyId.HasValue && application.PartyId != partyId.Value)
        {
            return new RecruitmentWorkspaceModel(
                CreateEmptyApplicationEditor(),
                false,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                AiResourceBindingStatus.Unbound,
                string.Empty,
                string.Empty,
                false,
                [],
                [],
                [],
                new RecruitmentSupportAssignmentsModel(Guid.Empty, null, string.Empty, null, string.Empty, null, string.Empty),
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
            .Where(item => partyIds.Contains(item.PartyId) && item.IsPublic)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => new RecruitmentContactValue(item.PartyId, item.ContactType, item.Value, item.IsPrimary))
            .ToListAsync(cancellationToken);
        var candidateParty = await dbContext.Set<Party>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == application.PartyId, cancellationToken);
        var workloadProfile = await dbContext.Set<WorkforceProfile>()
            .SingleOrDefaultAsync(item => item.PartyId == application.PartyId, cancellationToken);
        var stageHistory = await LoadStageHistoryAsync(dbContext, application.Id, cancellationToken);
        var relatedProjectIds = tasks
            .Where(task => task.RelatedProjectId.HasValue && task.RelatedProjectId.Value != Guid.Empty)
            .Select(task => task.RelatedProjectId!.Value)
            .Distinct()
            .ToList();
        var relatedProjects = await projectRecordQueryService.GetManyAsync(
            relatedProjectIds,
            cancellationToken);
        var relatedProjectNames = relatedProjects.ToDictionary(
            project => project.Id,
            project => project.Name);

        var partyMap = parties.ToDictionary(item => item.Id);
        var contactMap = contacts
            .GroupBy(item => item.PartyId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<RecruitmentContactValue>)group.ToList());
        var candidateContacts = contactMap.GetValueOrDefault(application.PartyId) ?? [];
        var candidatePartyType = partyMap.GetValueOrDefault(application.PartyId)?.PartyType;
        var candidateBinding = candidatePartyType == PartyType.AiAgent
            ? await dbContext.Set<AiResourceBinding>()
                .AsNoTracking()
                .Where(item => item.PartyId == application.PartyId)
                .Select(item => new RecruitmentCandidateBindingProjection(
                    item.TechnicalAgentId,
                    item.BindingStatus))
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        return new RecruitmentWorkspaceModel(
            MapApplication(application, partyMap, contactMap),
            true,
            ResolvePartyDisplayName(partyMap, application.PartyId),
            parties.FirstOrDefault(item => item.Id == application.PartyId)?.DisplayName is not null
                ? await LoadCandidateSummaryAsync(dbContext, application.PartyId, cancellationToken)
                : string.Empty,
            ResolvePrimaryContactValue(candidateContacts, PartyContactType.Email),
            ResolvePrimaryContactValue(candidateContacts, PartyContactType.Phone),
            candidatePartyType,
            candidateBinding?.TechnicalAgentId,
            candidateBinding?.BindingStatus ?? AiResourceBindingStatus.Unbound,
            ResolvePartyDisplayName(partyMap, application.RecruiterPartyId),
            ResolvePartyDisplayName(partyMap, application.HiringManagerPartyId),
            workloadProfile is not null,
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
                item.Feedback) { Original = RecruitmentEditValues.Interview(item) }).ToList(),
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
                    item.RelatedProjectId.HasValue
                        ? relatedProjectNames.GetValueOrDefault(item.RelatedProjectId.Value) ?? string.Empty
                        : string.Empty,
                    item.Notes,
                    item.DueDateUtc.HasValue &&
                    item.Status is not LifecycleTaskStatus.Completed &&
                    item.Status is not LifecycleTaskStatus.Cancelled &&
                    item.DueDateUtc.Value.Date < DateTimeOffset.UtcNow.Date) { Original = RecruitmentEditValues.Task(item) })
                .ToList(),
            supportAssignments,
            CreateConversionEditor(application, supportAssignments, workloadProfile, candidateParty));
    }

    public Task<Result<Guid>> SaveRecruitmentApplicationAsync(RecruitmentApplicationEditorModel model, CancellationToken cancellationToken = default,
        Action<RecruitmentSaveReceipt<RecruitmentApplicationEditorModel>>? onCommitted = null) =>
        applications.SaveRecruitmentApplicationAsync(model, cancellationToken, onCommitted);

    public Task<Result<Guid>> SaveRecruitmentInterviewAsync(RecruitmentInterviewEditorModel model, CancellationToken cancellationToken = default,
        Action<RecruitmentSaveReceipt<RecruitmentInterviewEditorModel>>? onCommitted = null) =>
        interviews.SaveRecruitmentInterviewAsync(model, cancellationToken, onCommitted);

    public Task<Result> DeleteRecruitmentInterviewAsync(Guid id, CancellationToken cancellationToken = default,
        RecruitmentInterviewObservation? original = null, Action<RecruitmentDeleteReceipt>? onCommitted = null) =>
        interviews.DeleteRecruitmentInterviewAsync(id, cancellationToken, original, onCommitted);

    public Task<Result<Guid>> SaveLifecycleTaskAsync(LifecycleTaskEditorModel model, CancellationToken cancellationToken = default,
        Action<RecruitmentSaveReceipt<LifecycleTaskEditorModel>>? onCommitted = null) =>
        lifecycle.SaveLifecycleTaskAsync(model, cancellationToken, onCommitted);

    public Task<Result> DeleteLifecycleTaskAsync(Guid id, CancellationToken cancellationToken = default,
        LifecycleTaskObservation? original = null, Action<RecruitmentDeleteReceipt>? onCommitted = null) =>
        lifecycle.DeleteLifecycleTaskAsync(id, cancellationToken, original, onCommitted);

    public async Task<Result<PartyRelationshipSaveReceipt>> SaveSupportAssignmentsAsync(
        RecruitmentSupportAssignmentsEditorModel model, CancellationToken cancellationToken = default,
        Action<PartyRelationshipSaveReceipt>? onCommitted = null) {
        ArgumentNullException.ThrowIfNull(model);
        model = model.Snapshot();
        var result = await relationships.ReplaceSupportAsync(model, cancellationToken, onCommitted);
        if (result.IsFailure) {
            return result;
        }
        await activityStream.RecordAsync(new ActivityWriteRequest("CRM / HR", "RecruitmentSupportAssignmentsSaved",
            "Saved candidate support assignments", ArtifactKind: nameof(PartyRelationship), ArtifactId: model.PartyId,
            Route: RecruitmentEditValues.PartyRoute(model.PartyId), Actor: RecruitmentEditValues.Actor(model.LastChangedBy)), cancellationToken);
        return result;
    }

    public Task<Result<Guid>> ConvertCandidateAsync(RecruitmentConversionEditorModel model, CancellationToken cancellationToken = default,
        Action<RecruitmentConversionReceipt>? onCommitted = null) => conversion.ConvertCandidateAsync(model, cancellationToken, onCommitted);

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
            Original = RecruitmentEditValues.Application(application),
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
        WorkforceProfile? workforceProfile, Party? candidateParty)
    {
        return new RecruitmentConversionEditorModel
        {
            ApplicationId = application.Id,
            Original = candidateParty is null ? null : new(RecruitmentEditValues.Application(application),
                WorkforceEditValues.Profile(candidateParty.Id, candidateParty.PartyType, workforceProfile), candidateParty.LifecycleStatus, candidateParty.UpdatedAtUtc),
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

    private sealed record RecruitmentCandidateBindingProjection(
        Guid? TechnicalAgentId,
        AiResourceBindingStatus BindingStatus);

    private async Task<RecruitmentSupportAssignmentsModel> LoadSupportAssignmentsAsync(
        CrmHrDbContext dbContext, Guid partyId, CancellationToken cancellationToken) =>
        RecruitmentSupportRelationshipPolicy.ToModel(partyId, await PartyRelationshipService.ReadAsync(dbContext, partyId, cancellationToken, supportOnly: true));

    private async Task<IReadOnlyList<RecruitmentStageHistoryItemModel>> LoadStageHistoryAsync(
        CrmHrDbContext dbContext,
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

    private async Task<string> LoadCandidateSummaryAsync(CrmHrDbContext dbContext, Guid partyId, CancellationToken cancellationToken)
    {
        return await dbContext.Set<Party>()
            .Where(item => item.Id == partyId)
            .Select(item => item.Summary)
            .SingleOrDefaultAsync(cancellationToken)
            ?? string.Empty;
    }

    private static RecruitmentApplicationQuery NormalizeRecruitmentApplicationQuery(
        RecruitmentApplicationQuery query)
    {
        if (query.PageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageIndex,
                "Recruitment application page index cannot be negative.");
        }

        if (query.PageSize is < 1 or > RecruitmentApplicationQueryLimits.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageSize,
                $"Recruitment application page size must be between 1 and {RecruitmentApplicationQueryLimits.MaximumPageSize}.");
        }

        if (query.PageIndex > int.MaxValue / query.PageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageIndex,
                "Recruitment application page offset is too large.");
        }

        var searchText = query.SearchText?.Trim() ?? string.Empty;
        if (searchText.Length > RecruitmentApplicationQueryLimits.MaximumSearchLength)
        {
            throw new ArgumentException(
                $"Recruitment application search cannot exceed {RecruitmentApplicationQueryLimits.MaximumSearchLength} characters.",
                nameof(query));
        }

        if (!Enum.IsDefined(query.Scope))
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.Scope,
                "Recruitment application scope is not supported.");
        }

        return query with { SearchText = searchText };
    }

    private static RecruitmentStage? TryMapRecruitmentStage(RecruitmentApplicationScope scope)
    {
        return scope switch
        {
            RecruitmentApplicationScope.All => null,
            RecruitmentApplicationScope.Applied => RecruitmentStage.Applied,
            RecruitmentApplicationScope.Screening => RecruitmentStage.Screening,
            RecruitmentApplicationScope.Interviewing => RecruitmentStage.Interviewing,
            RecruitmentApplicationScope.Offer => RecruitmentStage.Offer,
            RecruitmentApplicationScope.Hired => RecruitmentStage.Hired,
            RecruitmentApplicationScope.Rejected => RecruitmentStage.Rejected,
            RecruitmentApplicationScope.Withdrawn => RecruitmentStage.Withdrawn,
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Recruitment application scope is not supported.")
        };
    }

    private static RecruitmentApplicationListItemModel MapRecruitmentApplicationListItem(
        RecruitmentApplicationListProjection item)
    {
        return new RecruitmentApplicationListItemModel(
            item.Id,
            item.PartyId,
            item.CandidatePartyType,
            item.CandidateName,
            item.DesiredRole,
            item.Stage,
            item.Decision,
            item.RecruiterName,
            item.HiringManagerName,
            item.TargetUnitName,
            item.PrimaryEmail,
            item.PrimaryPhone,
            ToDateOnly(item.AvailableFromUtc),
            item.HasWorkforceProfile,
            item.UpdatedAtUtc);
    }

    private sealed record RecruitmentApplicationListProjection(
        Guid Id,
        Guid PartyId,
        PartyType CandidatePartyType,
        string CandidateName,
        string DesiredRole,
        RecruitmentStage Stage,
        RecruitmentDecision Decision,
        string RecruiterName,
        string HiringManagerName,
        string TargetUnitName,
        string PrimaryEmail,
        string PrimaryPhone,
        DateTimeOffset? AvailableFromUtc,
        bool HasWorkforceProfile,
        DateTimeOffset UpdatedAtUtc);

    private sealed record RecruitmentContactValue(
        Guid PartyId,
        PartyContactType ContactType,
        string Value,
        bool IsPrimary);
}
