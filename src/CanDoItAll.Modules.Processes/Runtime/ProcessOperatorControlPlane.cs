using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public enum ProcessEscalationKind
{
    BlockedStep,
    FailedStep,
    SafeRefusal,
    ApprovalRequired,
    OutboxDeadLetter,
    RetryBudgetExhausted,
    ToolPolicyBlocked,
    OperatorRequestedRework
}

public enum ProcessEscalationSeverity
{
    Low,
    Moderate,
    High,
    Critical
}

public enum ProcessEscalationStatus
{
    Open,
    Assigned,
    ReworkRequested,
    Resolved,
    Reopened
}

public enum ProcessEscalationSourceKind
{
    Journal,
    OutboxRecord
}

public enum ProcessOperatorApprovalKind
{
    ExecutionTool,
    LaunchPlan
}

public enum ProcessOperatorApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    ChangesRequested
}

public enum ProcessAttemptTimelineKind
{
    ExecutionRun,
    Approval,
    Outbox,
    Escalation,
    ManagerDirective,
    Recovery,
    ReworkPacket,
    ManualRerun
}

public sealed record ProcessEscalationViewModel(
    Guid Id,
    Guid ProcessRunId,
    Guid? StepRunId,
    string StepTitle,
    ProcessEscalationKind Kind,
    ProcessEscalationSeverity Severity,
    ProcessEscalationStatus Status,
    ProcessEscalationSourceKind SourceKind,
    string Title,
    string Reason,
    string Owner,
    string Resolution,
    Guid? ReworkPacketId,
    string SourceExecutionRunId,
    string SourceApprovalId,
    string SourceToolName,
    string CorrelationId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? DueAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    string UpdatedBy)
{
    public bool IsOpen => Status != ProcessEscalationStatus.Resolved;

    public bool IsJournalBacked => SourceKind == ProcessEscalationSourceKind.Journal;
}

public sealed record ProcessOperatorApprovalViewModel(
    ProcessOperatorApprovalKind Kind,
    Guid? ProcessRunId,
    Guid? StepRunId,
    string StepTitle,
    Guid? ExecutionRunId,
    Guid? LaunchPlanId,
    string ExternalApprovalId,
    string Title,
    string Details,
    string Source,
    ProcessOperatorApprovalStatus Status,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    bool CanDecide);

public sealed record ProcessAttemptTimelineEntryViewModel(
    ProcessAttemptTimelineKind Kind,
    Guid? StepRunId,
    string StepTitle,
    Guid? ExecutionRunId,
    Guid? OutboxRecordId,
    Guid? EscalationId,
    string Title,
    string Status,
    string StatusTone,
    string Summary,
    string ProviderName,
    string Model,
    string ProofSummary,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc);

public sealed class ProcessEscalationCreateRequest
{
    public Guid ProcessRunId { get; set; }

    public Guid? StepRunId { get; set; }

    public ProcessEscalationKind Kind { get; set; } = ProcessEscalationKind.BlockedStep;

    public ProcessEscalationSeverity Severity { get; set; } = ProcessEscalationSeverity.Moderate;

    public string Title { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string Owner { get; set; } = string.Empty;

    public DateTimeOffset? DueAtUtc { get; set; }

    public string SourceExecutionRunId { get; set; } = string.Empty;

    public string SourceApprovalId { get; set; } = string.Empty;

    public string SourceToolName { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = "process-workspace";
}

public sealed class ProcessEscalationAssignmentRequest
{
    public Guid EscalationId { get; set; }

    public string Owner { get; set; } = string.Empty;

    public string AssignedBy { get; set; } = "process-workspace";
}

public sealed class ProcessEscalationResolutionRequest
{
    public Guid EscalationId { get; set; }

    public string Resolution { get; set; } = string.Empty;

    public string ResolvedBy { get; set; } = "process-workspace";
}

public sealed class ProcessEscalationReopenRequest
{
    public Guid EscalationId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string ReopenedBy { get; set; } = "process-workspace";
}

public sealed class ProcessEscalationReworkRequest
{
    public Guid EscalationId { get; set; }

    public Guid? StepRunConcurrencyToken { get; set; }

    public string Directive { get; set; } = string.Empty;

    public string RequestedBy { get; set; } = "process-workspace";
}

public sealed class ProcessOperatorApprovalDecisionRequest
{
    public Guid ProcessRunId { get; set; }

    public Guid? StepRunId { get; set; }

    public Guid? ExecutionRunId { get; set; }

    public Guid? LaunchPlanId { get; set; }

    public string ExternalApprovalId { get; set; } = string.Empty;

    public ProcessOperatorApprovalStatus Status { get; set; } = ProcessOperatorApprovalStatus.Approved;

    public string Summary { get; set; } = string.Empty;

    public string DecidedBy { get; set; } = "process-workspace";
}

public interface IProcessEscalationService
{
    Task<IReadOnlyList<ProcessEscalationViewModel>> ListAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<ProcessEscalationViewModel>>> ListForRunsAsync(
        IReadOnlyCollection<Guid> runIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessAttemptTimelineEntryViewModel>> ListJournalTimelineAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<Result<Guid>> CreateAsync(
        ProcessEscalationCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> AssignAsync(
        ProcessEscalationAssignmentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> ResolveAsync(
        ProcessEscalationResolutionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> ReopenAsync(
        ProcessEscalationReopenRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<Guid?>> RequestReworkAsync(
        ProcessEscalationReworkRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> RecordApprovalDecisionAsync(
        ProcessOperatorApprovalDecisionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ProcessEscalationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    ProcessesService processesService) : IProcessEscalationService
{
    private static readonly string[] TimelineJournalEventTypeValues =
    [
        .. ProcessEscalationJournal.EventTypeValues,
        ProcessRuntimeEventTypes.ManagerDirectiveRecorded,
        ProcessRuntimeEventTypes.AgentRecoveryAttemptRecorded,
        ProcessRuntimeEventTypes.AgentReworkPacketCreated,
        ProcessRuntimeEventTypes.ManualAgentStepRerun,
        ProcessRuntimeEventTypes.ProcessOperatorApprovalDecided
    ];

    public async Task<IReadOnlyList<ProcessEscalationViewModel>> ListAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty)
        {
            return [];
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var eventTypes = ProcessEscalationJournal.EventTypeValues;
        var entries = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .Where(entry => entry.ProcessRunId == runId && eventTypes.Contains(entry.EventType))
            .ToListAsync(cancellationToken);

        return ProcessEscalationJournal.Project(entries)
            .OrderBy(item => item.Status == ProcessEscalationStatus.Resolved)
            .ThenByDescending(item => item.Severity)
            .ThenBy(item => item.DueAtUtc ?? DateTimeOffset.MaxValue)
            .ThenByDescending(item => item.UpdatedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ProcessEscalationViewModel>>> ListForRunsAsync(
        IReadOnlyCollection<Guid> runIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runIds);

        var normalizedRunIds = runIds
            .Where(runId => runId != Guid.Empty)
            .Distinct()
            .ToArray();
        if (normalizedRunIds.Length == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<ProcessEscalationViewModel>>();
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var eventTypes = ProcessEscalationJournal.EventTypeValues;
        var entries = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .Where(entry => normalizedRunIds.Contains(entry.ProcessRunId) && eventTypes.Contains(entry.EventType))
            .ToListAsync(cancellationToken);

        return ProcessEscalationJournal.Project(entries)
            .GroupBy(item => item.ProcessRunId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProcessEscalationViewModel>)group
                    .OrderBy(item => item.Status == ProcessEscalationStatus.Resolved)
                    .ThenByDescending(item => item.Severity)
                    .ThenBy(item => item.DueAtUtc ?? DateTimeOffset.MaxValue)
                    .ThenByDescending(item => item.UpdatedAtUtc)
                    .ToList());
    }

    public async Task<IReadOnlyList<ProcessAttemptTimelineEntryViewModel>> ListJournalTimelineAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty)
        {
            return [];
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var eventTypes = TimelineJournalEventTypeValues;
        var entries = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .Where(entry => entry.ProcessRunId == runId && eventTypes.Contains(entry.EventType))
            .ToListAsync(cancellationToken);
        var stepRunIds = entries
            .Select(entry => entry.StepRunId)
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var stepTitlesById = stepRunIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Set<ProcessStepRun>()
                .AsNoTracking()
                .Where(stepRun => stepRunIds.Contains(stepRun.Id))
                .ToDictionaryAsync(stepRun => stepRun.Id, stepRun => stepRun.Title, cancellationToken);

        return entries
            .Select(entry => MapJournalTimelineEntry(entry, stepTitlesById))
            .OrderByDescending(item => item.OccurredAtUtc)
            .ToList();
    }

    public async Task<Result<Guid>> CreateAsync(
        ProcessEscalationCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ProcessRunId == Guid.Empty)
        {
            return Result<Guid>.Failure(Error.Validation("Select a process run before creating an escalation.", "processes.escalation-run-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await dbContext.Set<ProcessRun>()
            .SingleOrDefaultAsync(item => item.Id == request.ProcessRunId, cancellationToken);
        if (run is null)
        {
            return Result<Guid>.Failure(Error.Validation("Process run was not found.", "processes.escalation-run-not-found"));
        }

        ProcessStepRun? stepRun = null;
        if (request.StepRunId.HasValue)
        {
            stepRun = await dbContext.Set<ProcessStepRun>()
                .SingleOrDefaultAsync(item => item.Id == request.StepRunId.Value, cancellationToken);
            if (stepRun is null || stepRun.ProcessRunId != run.Id)
            {
                return Result<Guid>.Failure(Error.Validation("Process step run was not found for this run.", "processes.escalation-step-not-found"));
            }
        }

        var escalationId = Guid.NewGuid();
        var now = clock.GetUtcNow();
        await dbContext.Set<ProcessJournalEntry>().AddAsync(
            ProcessEscalationJournal.BuildCreatedEntry(run, stepRun, request, escalationId, now),
            cancellationToken);
        run.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(escalationId);
    }

    public async Task<Result> AssignAsync(
        ProcessEscalationAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EscalationId == Guid.Empty)
        {
            return Result.Failure(Error.Validation("Select an escalation before assigning it.", "processes.escalation-required"));
        }

        var owner = NormalizeOrDefault(request.Owner, "process-workspace");
        return await AppendEscalationStateAsync(
            request.EscalationId,
            ProcessRuntimeEventTypes.ProcessEscalationAssigned,
            ProcessEscalationStatus.Assigned,
            owner,
            resolution: null,
            reworkPacketId: null,
            updatedBy: NormalizeOrDefault(request.AssignedBy, "process-workspace"),
            decisionRecordFactory: null,
            cancellationToken);
    }

    public async Task<Result> ResolveAsync(
        ProcessEscalationResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EscalationId == Guid.Empty)
        {
            return Result.Failure(Error.Validation("Select an escalation before resolving it.", "processes.escalation-required"));
        }

        var resolution = NormalizeOrDefault(request.Resolution, "Operator marked the escalation resolved.");
        var resolvedBy = NormalizeOrDefault(request.ResolvedBy, "process-workspace");
        return await AppendEscalationStateAsync(
            request.EscalationId,
            ProcessRuntimeEventTypes.ProcessEscalationResolved,
            ProcessEscalationStatus.Resolved,
            owner: null,
            resolution,
            reworkPacketId: null,
            resolvedBy,
            (escalation, now) => new ProcessDecisionRecord
            {
                ProcessRunId = escalation.ProcessRunId,
                StepRunId = escalation.StepRunId,
                DecisionKind = ProcessDecisionKind.Escalation,
                Outcome = ProcessDecisionOutcome.Accepted,
                Title = $"Resolved escalation: {escalation.Title}",
                Reason = resolution,
                PolicyEvaluation = $"Escalation {escalation.Id:D} resolved from the operator control plane.",
                DecidedBy = resolvedBy,
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                CreatedAtUtc = now
            },
            cancellationToken);
    }

    public async Task<Result> ReopenAsync(
        ProcessEscalationReopenRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EscalationId == Guid.Empty)
        {
            return Result.Failure(Error.Validation("Select an escalation before reopening it.", "processes.escalation-required"));
        }

        var reopenedBy = NormalizeOrDefault(request.ReopenedBy, "process-workspace");
        var reason = NormalizeOrDefault(request.Reason, "Operator reopened this escalation.");
        return await AppendEscalationStateAsync(
            request.EscalationId,
            ProcessRuntimeEventTypes.ProcessEscalationReopened,
            ProcessEscalationStatus.Reopened,
            owner: null,
            reason,
            reworkPacketId: null,
            reopenedBy,
            decisionRecordFactory: null,
            cancellationToken);
    }

    public async Task<Result<Guid?>> RequestReworkAsync(
        ProcessEscalationReworkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentEscalation = await LoadEscalationAsync(request.EscalationId, cancellationToken);
        if (currentEscalation is null)
        {
            return Result<Guid?>.Failure(CreateEscalationNotFoundError());
        }

        if (!currentEscalation.StepRunId.HasValue)
        {
            return Result<Guid?>.Failure(Error.Validation("Only step-scoped escalations can request agent rework.", "processes.escalation-step-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var stepRun = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == currentEscalation.StepRunId.Value, cancellationToken);
        if (stepRun is null)
        {
            return Result<Guid?>.Failure(Error.Validation("Process step run was not found.", "processes.escalation-step-not-found"));
        }

        var directive = NormalizeOrDefault(
            request.Directive,
            $"Repair the step for escalation '{currentEscalation.Title}'. Cause: {currentEscalation.Reason}");
        var rerunResult = await processesService.RerunAgentStepAsync(
            new ProcessAgentStepRerunRequest
            {
                StepRunId = stepRun.Id,
                StepRunConcurrencyToken = request.StepRunConcurrencyToken ?? stepRun.ConcurrencyToken,
                OperatorReason = directive
            },
            cancellationToken);
        if (rerunResult.IsFailure)
        {
            return Result<Guid?>.Failure(rerunResult.Errors);
        }

        Guid? reworkPacketId = null;
        var packetEntries = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .Where(entry =>
                entry.ProcessRunId == currentEscalation.ProcessRunId &&
                entry.StepRunId == stepRun.Id &&
                entry.EventType == ProcessRuntimeEventTypes.AgentReworkPacketCreated)
            .ToListAsync(cancellationToken);
        var packetEntry = packetEntries
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .ThenByDescending(entry => entry.Id)
            .FirstOrDefault();
        if (packetEntry is not null && Guid.TryParse(packetEntry.CorrelationId, out var parsedPacketId))
        {
            reworkPacketId = parsedPacketId;
        }

        var stateResult = await AppendEscalationStateAsync(
            request.EscalationId,
            ProcessRuntimeEventTypes.ProcessEscalationReworkRequested,
            ProcessEscalationStatus.ReworkRequested,
            owner: null,
            directive,
            reworkPacketId,
            NormalizeOrDefault(request.RequestedBy, "process-workspace"),
            decisionRecordFactory: null,
            cancellationToken);
        if (stateResult.IsFailure)
        {
            return Result<Guid?>.Failure(stateResult.Errors);
        }

        return Result<Guid?>.Success(reworkPacketId);
    }

    public async Task<Result> RecordApprovalDecisionAsync(
        ProcessOperatorApprovalDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ProcessRunId == Guid.Empty)
        {
            return Result.Failure(Error.Validation("Select a process run before recording an approval decision.", "processes.approval-run-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await dbContext.Set<ProcessRun>()
            .SingleOrDefaultAsync(item => item.Id == request.ProcessRunId, cancellationToken);
        if (run is null)
        {
            return Result.Failure(Error.Validation("Process run was not found.", "processes.approval-run-not-found"));
        }

        var now = clock.GetUtcNow();
        var decidedBy = NormalizeOrDefault(request.DecidedBy, "process-workspace");
        var summary = NormalizeOrDefault(request.Summary, $"{request.Status} from the operator control plane.");
        var outcome = request.Status == ProcessOperatorApprovalStatus.Approved
            ? ProcessDecisionOutcome.Approved
            : ProcessDecisionOutcome.Rejected;
        var payload = new
        {
            request.ProcessRunId,
            request.StepRunId,
            request.ExecutionRunId,
            request.LaunchPlanId,
            request.ExternalApprovalId,
            Status = request.Status.ToString(),
            Summary = summary,
            DecidedBy = decidedBy
        };

        await dbContext.Set<ProcessJournalEntry>().AddAsync(
            new ProcessJournalEntry
            {
                ProcessRunId = run.Id,
                StepRunId = request.StepRunId,
                EventType = ProcessRuntimeEventTypes.ProcessOperatorApprovalDecided,
                Title = $"Operator approval {request.Status}",
                Description = summary,
                CorrelationId = request.ExecutionRunId?.ToString("N") ?? request.LaunchPlanId?.ToString("N") ?? Guid.NewGuid().ToString("N"),
                OperatingMode = run.OperatingMode,
                PolicyVersion = $"definition-version:{run.ProcessDefinitionVersionId:D}",
                EnvironmentMode = run.OperatingMode.ToString(),
                ReplayContextJson = JsonSerializer.Serialize(payload, ProcessEscalationJournal.SerializerOptions),
                OccurredAtUtc = now
            },
            cancellationToken);
        await dbContext.Set<ProcessDecisionRecord>().AddAsync(
            new ProcessDecisionRecord
            {
                ProcessRunId = run.Id,
                StepRunId = request.StepRunId,
                DecisionKind = ProcessDecisionKind.Approval,
                Outcome = outcome,
                Title = $"Operator approval {request.Status}",
                Reason = summary,
                PolicyEvaluation = string.IsNullOrWhiteSpace(request.ExternalApprovalId)
                    ? "Approval was decided from the operator control plane."
                    : $"External approval id: {request.ExternalApprovalId}.",
                DecidedBy = decidedBy,
                OperatingMode = run.OperatingMode,
                CreatedAtUtc = now
            },
            cancellationToken);
        run.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Result> AppendEscalationStateAsync(
        Guid escalationId,
        string eventType,
        ProcessEscalationStatus status,
        string? owner,
        string? resolution,
        Guid? reworkPacketId,
        string updatedBy,
        Func<ProcessEscalationViewModel, DateTimeOffset, ProcessDecisionRecord>? decisionRecordFactory,
        CancellationToken cancellationToken)
    {
        var currentEscalation = await LoadEscalationAsync(escalationId, cancellationToken);
        if (currentEscalation is null)
        {
            return Result.Failure(CreateEscalationNotFoundError());
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await dbContext.Set<ProcessRun>()
            .SingleAsync(item => item.Id == currentEscalation.ProcessRunId, cancellationToken);
        var now = clock.GetUtcNow();
        await dbContext.Set<ProcessJournalEntry>().AddAsync(
            ProcessEscalationJournal.BuildStateEntry(
                run,
                currentEscalation,
                eventType,
                status,
                owner,
                resolution,
                reworkPacketId,
                updatedBy,
                now),
            cancellationToken);

        if (decisionRecordFactory is not null)
        {
            await dbContext.Set<ProcessDecisionRecord>().AddAsync(
                decisionRecordFactory(currentEscalation, now),
                cancellationToken);
        }

        run.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<ProcessEscalationViewModel?> LoadEscalationAsync(
        Guid escalationId,
        CancellationToken cancellationToken)
    {
        if (escalationId == Guid.Empty)
        {
            return null;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var eventTypes = ProcessEscalationJournal.EventTypeValues;
        var runId = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .Where(entry => eventTypes.Contains(entry.EventType))
            .Where(entry => entry.CorrelationId == escalationId.ToString("N") || entry.CorrelationId == escalationId.ToString("D"))
            .Select(entry => (Guid?)entry.ProcessRunId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!runId.HasValue)
        {
            return null;
        }

        return (await ListAsync(runId.Value, cancellationToken))
            .SingleOrDefault(item => item.Id == escalationId);
    }

    private static ProcessAttemptTimelineEntryViewModel MapJournalTimelineEntry(
        ProcessJournalEntry entry,
        IReadOnlyDictionary<Guid, string> stepTitlesById)
    {
        var kind = entry.EventType switch
        {
            ProcessRuntimeEventTypes.ManagerDirectiveRecorded => ProcessAttemptTimelineKind.ManagerDirective,
            ProcessRuntimeEventTypes.AgentRecoveryAttemptRecorded => ProcessAttemptTimelineKind.Recovery,
            ProcessRuntimeEventTypes.AgentReworkPacketCreated => ProcessAttemptTimelineKind.ReworkPacket,
            ProcessRuntimeEventTypes.ManualAgentStepRerun => ProcessAttemptTimelineKind.ManualRerun,
            ProcessRuntimeEventTypes.ProcessOperatorApprovalDecided => ProcessAttemptTimelineKind.Approval,
            _ => ProcessAttemptTimelineKind.Escalation
        };
        var tone = entry.EventType switch
        {
            ProcessRuntimeEventTypes.ProcessEscalationResolved => "mint",
            ProcessRuntimeEventTypes.ManagerDirectiveRecorded => "info",
            ProcessRuntimeEventTypes.ProcessEscalationReworkRequested => "warning",
            ProcessRuntimeEventTypes.AgentReworkPacketCreated => "info",
            ProcessRuntimeEventTypes.ManualAgentStepRerun => "warning",
            ProcessRuntimeEventTypes.ProcessOperatorApprovalDecided when entry.Title.Contains("Approved", StringComparison.OrdinalIgnoreCase) => "mint",
            ProcessRuntimeEventTypes.ProcessOperatorApprovalDecided => "danger",
            _ => "info"
        };

        return new ProcessAttemptTimelineEntryViewModel(
            kind,
            entry.StepRunId,
            entry.StepRunId.HasValue ? stepTitlesById.GetValueOrDefault(entry.StepRunId.Value, string.Empty) : string.Empty,
            ExecutionRunId: null,
            OutboxRecordId: null,
            EscalationId: kind == ProcessAttemptTimelineKind.Escalation && Guid.TryParse(entry.CorrelationId, out var escalationId)
                ? escalationId
                : null,
            entry.Title,
            entry.EventType,
            tone,
            entry.Description,
            ProviderName: string.Empty,
            Model: string.Empty,
            ProofSummary: string.Empty,
            entry.CorrelationId,
            entry.OccurredAtUtc);
    }

    private static Error CreateEscalationNotFoundError()
    {
        return Error.Validation("Escalation was not found.", "processes.escalation-not-found");
    }

    private static string NormalizeOrDefault(string? value, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : value.Trim();
    }
}

internal static class ProcessEscalationJournal
{
    internal static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    internal static readonly string[] EventTypeValues =
    [
        ProcessRuntimeEventTypes.ProcessEscalationCreated,
        ProcessRuntimeEventTypes.ProcessEscalationAssigned,
        ProcessRuntimeEventTypes.ProcessEscalationResolved,
        ProcessRuntimeEventTypes.ProcessEscalationReopened,
        ProcessRuntimeEventTypes.ProcessEscalationReworkRequested
    ];

    internal static readonly IReadOnlySet<string> EventTypes = new HashSet<string>(
        EventTypeValues,
        StringComparer.Ordinal);

    internal static ProcessJournalEntry BuildTransitionCreatedEntry(
        ProcessRun run,
        ProcessStepRun stepRun,
        ProcessStepRunStatus status,
        string reason,
        DateTimeOffset now)
    {
        var escalationId = Guid.NewGuid();
        var payload = new ProcessEscalationPayload(
            escalationId,
            run.Id,
            stepRun.Id,
            stepRun.Title,
            ResolveKind(status),
            ResolveSeverity(status),
            ProcessEscalationStatus.Open,
            ResolveTitle(status),
            Normalize(reason),
            Owner: string.Empty,
            Resolution: string.Empty,
            ReworkPacketId: null,
            SourceExecutionRunId: string.Empty,
            SourceApprovalId: string.Empty,
            SourceToolName: string.Empty,
            escalationId.ToString("N"),
            now,
            now,
            ResolveDueAt(status, now),
            ResolvedAtUtc: null,
            UpdatedBy: "process-runtime");

        return BuildEntry(
            run,
            stepRun.Id,
            ProcessRuntimeEventTypes.ProcessEscalationCreated,
            payload.Title,
            payload.Reason,
            payload);
    }

    internal static ProcessJournalEntry BuildCreatedEntry(
        ProcessRun run,
        ProcessStepRun? stepRun,
        ProcessEscalationCreateRequest request,
        Guid escalationId,
        DateTimeOffset now)
    {
        var payload = new ProcessEscalationPayload(
            escalationId,
            run.Id,
            stepRun?.Id,
            stepRun?.Title ?? string.Empty,
            request.Kind,
            request.Severity,
            ProcessEscalationStatus.Open,
            NormalizeOrDefault(request.Title, ResolveTitle(request.Kind)),
            NormalizeOrDefault(request.Reason, "Operator created an escalation."),
            Normalize(request.Owner),
            Resolution: string.Empty,
            ReworkPacketId: null,
            Normalize(request.SourceExecutionRunId),
            Normalize(request.SourceApprovalId),
            Normalize(request.SourceToolName),
            escalationId.ToString("N"),
            now,
            now,
            request.DueAtUtc ?? ResolveDueAt(request.Severity, now),
            ResolvedAtUtc: null,
            UpdatedBy: NormalizeOrDefault(request.CreatedBy, "process-workspace"));

        return BuildEntry(
            run,
            stepRun?.Id,
            ProcessRuntimeEventTypes.ProcessEscalationCreated,
            payload.Title,
            payload.Reason,
            payload);
    }

    internal static ProcessJournalEntry BuildStateEntry(
        ProcessRun run,
        ProcessEscalationViewModel current,
        string eventType,
        ProcessEscalationStatus status,
        string? owner,
        string? resolution,
        Guid? reworkPacketId,
        string updatedBy,
        DateTimeOffset now)
    {
        var payload = new ProcessEscalationPayload(
            current.Id,
            current.ProcessRunId,
            current.StepRunId,
            current.StepTitle,
            current.Kind,
            current.Severity,
            status,
            current.Title,
            current.Reason,
            Normalize(owner) == string.Empty ? current.Owner : Normalize(owner),
            Normalize(resolution) == string.Empty ? current.Resolution : Normalize(resolution),
            reworkPacketId ?? current.ReworkPacketId,
            current.SourceExecutionRunId,
            current.SourceApprovalId,
            current.SourceToolName,
            current.CorrelationId,
            current.CreatedAtUtc,
            now,
            current.DueAtUtc,
            status == ProcessEscalationStatus.Resolved ? now : null,
            NormalizeOrDefault(updatedBy, "process-workspace"));

        return BuildEntry(
            run,
            current.StepRunId,
            eventType,
            ResolveStateTitle(status, current.Title),
            payload.Resolution == string.Empty ? payload.Reason : payload.Resolution,
            payload);
    }

    internal static IReadOnlyList<ProcessEscalationViewModel> Project(
        IReadOnlyList<ProcessJournalEntry> entries)
    {
        var projected = new Dictionary<Guid, ProcessEscalationPayload>();
        foreach (var entry in entries
            .Where(item => EventTypes.Contains(item.EventType))
            .OrderBy(item => item.OccurredAtUtc)
            .ThenBy(item => item.Id))
        {
            var payload = ReadPayload(entry);
            projected[payload.EscalationId] = payload;
        }

        return projected.Values
            .Select(payload => new ProcessEscalationViewModel(
                payload.EscalationId,
                payload.ProcessRunId,
                payload.StepRunId,
                payload.StepTitle,
                payload.Kind,
                payload.Severity,
                payload.Status,
                ProcessEscalationSourceKind.Journal,
                payload.Title,
                payload.Reason,
                payload.Owner,
                payload.Resolution,
                payload.ReworkPacketId,
                payload.SourceExecutionRunId,
                payload.SourceApprovalId,
                payload.SourceToolName,
                payload.CorrelationId,
                payload.CreatedAtUtc,
                payload.UpdatedAtUtc,
                payload.DueAtUtc,
                payload.ResolvedAtUtc,
                payload.UpdatedBy))
            .ToList();
    }

    private static ProcessJournalEntry BuildEntry(
        ProcessRun run,
        Guid? stepRunId,
        string eventType,
        string title,
        string description,
        ProcessEscalationPayload payload)
    {
        return new ProcessJournalEntry
        {
            ProcessRunId = run.Id,
            StepRunId = stepRunId,
            EventType = eventType,
            Title = title,
            Description = description,
            CorrelationId = payload.EscalationId.ToString("N"),
            OperatingMode = run.OperatingMode,
            PolicyVersion = $"definition-version:{run.ProcessDefinitionVersionId:D}",
            EnvironmentMode = run.OperatingMode.ToString(),
            ReplayContextJson = JsonSerializer.Serialize(payload, SerializerOptions),
            OccurredAtUtc = payload.UpdatedAtUtc
        };
    }

    private static ProcessEscalationPayload ReadPayload(ProcessJournalEntry entry)
    {
        try
        {
            return JsonSerializer.Deserialize<ProcessEscalationPayload>(entry.ReplayContextJson, SerializerOptions)
                ?? throw new InvalidOperationException("Escalation journal payload was empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Escalation journal entry '{entry.Id:D}' has invalid payload for event '{entry.EventType}'.",
                exception);
        }
    }

    private static ProcessEscalationKind ResolveKind(ProcessStepRunStatus status)
    {
        return status switch
        {
            ProcessStepRunStatus.Failed => ProcessEscalationKind.FailedStep,
            ProcessStepRunStatus.Refused => ProcessEscalationKind.SafeRefusal,
            ProcessStepRunStatus.WaitingApproval => ProcessEscalationKind.ApprovalRequired,
            _ => ProcessEscalationKind.BlockedStep
        };
    }

    private static ProcessEscalationSeverity ResolveSeverity(ProcessStepRunStatus status)
    {
        return status switch
        {
            ProcessStepRunStatus.Failed => ProcessEscalationSeverity.High,
            ProcessStepRunStatus.Refused => ProcessEscalationSeverity.Moderate,
            ProcessStepRunStatus.WaitingApproval => ProcessEscalationSeverity.Moderate,
            _ => ProcessEscalationSeverity.Moderate
        };
    }

    private static DateTimeOffset ResolveDueAt(ProcessStepRunStatus status, DateTimeOffset now)
    {
        return ResolveDueAt(ResolveSeverity(status), now);
    }

    private static DateTimeOffset ResolveDueAt(ProcessEscalationSeverity severity, DateTimeOffset now)
    {
        return severity switch
        {
            ProcessEscalationSeverity.Critical => now.AddHours(1),
            ProcessEscalationSeverity.High => now.AddHours(4),
            ProcessEscalationSeverity.Moderate => now.AddDays(1),
            _ => now.AddDays(3)
        };
    }

    private static string ResolveTitle(ProcessStepRunStatus status)
    {
        return status switch
        {
            ProcessStepRunStatus.Failed => "Failed step needs operator review",
            ProcessStepRunStatus.Refused => "Safe refusal needs operator review",
            ProcessStepRunStatus.WaitingApproval => "Approval required before continuation",
            _ => "Blocked step needs operator review"
        };
    }

    private static string ResolveTitle(ProcessEscalationKind kind)
    {
        return kind switch
        {
            ProcessEscalationKind.FailedStep => "Failed step needs operator review",
            ProcessEscalationKind.SafeRefusal => "Safe refusal needs operator review",
            ProcessEscalationKind.ApprovalRequired => "Approval required before continuation",
            ProcessEscalationKind.OutboxDeadLetter => "Dead-lettered automation dispatch",
            ProcessEscalationKind.RetryBudgetExhausted => "Retry budget exhausted",
            ProcessEscalationKind.ToolPolicyBlocked => "Tool policy blocked execution",
            ProcessEscalationKind.OperatorRequestedRework => "Operator requested rework",
            _ => "Blocked step needs operator review"
        };
    }

    private static string ResolveStateTitle(ProcessEscalationStatus status, string title)
    {
        return status switch
        {
            ProcessEscalationStatus.Assigned => $"Assigned escalation: {title}",
            ProcessEscalationStatus.Resolved => $"Resolved escalation: {title}",
            ProcessEscalationStatus.Reopened => $"Reopened escalation: {title}",
            ProcessEscalationStatus.ReworkRequested => $"Requested rework: {title}",
            _ => title
        };
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static string NormalizeOrDefault(string? value, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : value.Trim();
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private sealed record ProcessEscalationPayload(
        Guid EscalationId,
        Guid ProcessRunId,
        Guid? StepRunId,
        string StepTitle,
        ProcessEscalationKind Kind,
        ProcessEscalationSeverity Severity,
        ProcessEscalationStatus Status,
        string Title,
        string Reason,
        string Owner,
        string Resolution,
        Guid? ReworkPacketId,
        string SourceExecutionRunId,
        string SourceApprovalId,
        string SourceToolName,
        string CorrelationId,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        DateTimeOffset? DueAtUtc,
        DateTimeOffset? ResolvedAtUtc,
        string UpdatedBy);
}
