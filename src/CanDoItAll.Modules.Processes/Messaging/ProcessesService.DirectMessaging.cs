using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    public async Task<Result<Guid>> SendDirectMessageAsync(ProcessDirectMessageRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ProcessRunId == Guid.Empty) {
            return Result<Guid>.Failure(Error.Validation("A process run is required before sending direct messages.", "processes.direct-message-run-required"));
        }

        if (request.SourceRoleRequirementId == Guid.Empty || request.TargetRoleRequirementId == Guid.Empty) {
            return Result<Guid>.Failure(Error.Validation("Source and target process roles are required for direct messaging.", "processes.direct-message-role-required"));
        }

        if (request.SourceRoleRequirementId == request.TargetRoleRequirementId) {
            return Result<Guid>.Failure(Error.Validation("Direct messages must target a different process role.", "processes.direct-message-self-reference"));
        }

        if (string.IsNullOrWhiteSpace(request.MessageBody)) {
            return Result<Guid>.Failure(Error.Validation("The direct message body is required.", "processes.direct-message-body-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var run = await dbContext.Set<ProcessRun>()
            .SingleOrDefaultAsync(item => item.Id == request.ProcessRunId, cancellationToken);
        if (run is null) {
            return Result<Guid>.Failure(Error.Validation("Process run was not found.", "processes.direct-message-run-not-found"));
        }

        var roles = await dbContext.Set<ProcessRoleRequirement>()
            .Where(item =>
                item.ProcessDefinitionVersionId == run.ProcessDefinitionVersionId &&
                (item.Id == request.SourceRoleRequirementId || item.Id == request.TargetRoleRequirementId))
            .ToListAsync(cancellationToken);
        var sourceRole = roles.FirstOrDefault(item => item.Id == request.SourceRoleRequirementId);
        var targetRole = roles.FirstOrDefault(item => item.Id == request.TargetRoleRequirementId);
        if (sourceRole is null || targetRole is null) {
            return Result<Guid>.Failure(Error.Validation("Direct messages must resolve to roles in the same published process version.", "processes.direct-message-role-invalid"));
        }

        var assignments = await dbContext.Set<ProcessRunAssignment>()
            .Where(item =>
                item.ProcessRunId == run.Id &&
                !item.StepDefinitionId.HasValue &&
                (item.RoleRequirementId == request.SourceRoleRequirementId || item.RoleRequirementId == request.TargetRoleRequirementId))
            .ToListAsync(cancellationToken);
        var sourceAssignment = assignments.FirstOrDefault(item => item.RoleRequirementId == request.SourceRoleRequirementId);
        var targetAssignment = assignments.FirstOrDefault(item => item.RoleRequirementId == request.TargetRoleRequirementId);
        var messagingPolicy = await dbContext.Set<ProcessRoleMessagingPolicyDefinition>()
            .FirstOrDefaultAsync(
                item =>
                    item.ProcessDefinitionVersionId == run.ProcessDefinitionVersionId &&
                    item.SourceRoleRequirementId == request.SourceRoleRequirementId &&
                    item.TargetRoleRequirementId == request.TargetRoleRequirementId,
                cancellationToken);
        var authorization = AuthorizeDirectMessage(run, sourceRole, targetRole, sourceAssignment, targetAssignment, messagingPolicy);
        var now = clock.GetUtcNow();
        var body = request.MessageBody.Trim();
        var decisionTitle = BuildDirectMessageDecisionTitle(sourceRole, targetRole);
        var decidedBy = string.IsNullOrWhiteSpace(sourceAssignment?.DisplayName)
            ? sourceRole.DisplayName
            : sourceAssignment.DisplayName;

        if (!authorization.IsAllowed) {
            await dbContext.Set<ProcessDecisionRecord>().AddAsync(
                new ProcessDecisionRecord {
                    ProcessRunId = run.Id,
                    DecisionKind = ProcessDecisionKind.DirectMessage,
                    Outcome = ProcessDecisionOutcome.Rejected,
                    Title = decisionTitle,
                    Reason = BuildDirectMessageDecisionReason(body),
                    PolicyEvaluation = authorization.PolicyEvaluation,
                    DecidedBy = decidedBy,
                    OperatingMode = run.OperatingMode,
                    CreatedAtUtc = now
                },
                cancellationToken);
            await dbContext.Set<ProcessJournalEntry>().AddAsync(
                BuildJournalEntry(
                    run.Id,
                    null,
                    "direct-message-denied",
                    "Direct message denied",
                    authorization.UserMessage,
                    run.OperatingMode,
                    $"definition-version:{run.ProcessDefinitionVersionId:D}",
                    $"{sourceRole.Key}->{targetRole.Key}"),
                cancellationToken);
            await dbContext.Set<ProcessConformanceObservation>().AddAsync(
                new ProcessConformanceObservation {
                    ProcessRunId = run.Id,
                    Severity = authorization.ConformanceSeverity,
                    Category = "DirectMessagingPolicy",
                    Observation = authorization.UserMessage,
                    DeviationReason = BuildDirectMessageDecisionReason(body),
                    IsSafeNonAction = true,
                    ContainsSensitiveAssessment = false,
                    CreatedAtUtc = now
                },
                cancellationToken);
            run.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            NotifyRunObservationChanged(run.ProjectId, run.ProcessDefinitionId, run.Id);
            return Result<Guid>.Failure(Error.Validation(authorization.UserMessage, authorization.ErrorCode));
        }

        var threadId = await AppendDirectMessageTranscriptAsync(
            dbContext,
            run,
            sourceRole,
            targetRole,
            body,
            now,
            cancellationToken);
        await dbContext.Set<ProcessDecisionRecord>().AddAsync(
            new ProcessDecisionRecord {
                ProcessRunId = run.Id,
                DecisionKind = ProcessDecisionKind.DirectMessage,
                Outcome = ProcessDecisionOutcome.Accepted,
                Title = decisionTitle,
                Reason = BuildDirectMessageDecisionReason(body),
                PolicyEvaluation = authorization.PolicyEvaluation,
                DecidedBy = decidedBy,
                OperatingMode = run.OperatingMode,
                CreatedAtUtc = now
            },
            cancellationToken);
        await dbContext.Set<ProcessJournalEntry>().AddAsync(
            BuildJournalEntry(
                run.Id,
                null,
                "direct-message-allowed",
                "Direct message recorded",
                $"{sourceRole.DisplayName} delivered a direct message to {targetRole.DisplayName}.",
                run.OperatingMode,
                $"definition-version:{run.ProcessDefinitionVersionId:D}",
                $"{threadId:D}:{sourceRole.Key}->{targetRole.Key}"),
            cancellationToken);
        run.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        NotifyRunObservationChanged(run.ProjectId, run.ProcessDefinitionId, run.Id);
        return Result<Guid>.Success(threadId);
    }

    private async Task<Guid> AppendDirectMessageTranscriptAsync(
        AppDbContext dbContext,
        ProcessRun run,
        ProcessRoleRequirement sourceRole,
        ProcessRoleRequirement targetRole,
        string body,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var subject = BuildDirectMessageSubject(sourceRole, targetRole);
        var thread = await dbContext.Set<CollaborationThreadRecord>()
            .FirstOrDefaultAsync(
                item =>
                    item.ContextKind == CollaborationContextKind.ProcessRun &&
                    item.ContextId == run.Id &&
                    item.Subject == subject,
                cancellationToken);
        CollaborationInboxItemRecord inboxItem;
        if (thread is null) {
            thread = new CollaborationThreadRecord {
                Subject = subject,
                ContextKind = CollaborationContextKind.ProcessRun,
                ContextId = run.Id,
                ProjectId = run.ProjectId,
                ContextLabel = run.Name,
                ContextRoute = BuildRunRoute(run),
                PrimaryItemKind = CollaborationInboxItemKind.Notification,
                State = CollaborationThreadState.Open,
                CreatedAtUtc = now,
                LastActivityAtUtc = now
            };
            inboxItem = new CollaborationInboxItemRecord {
                ThreadId = thread.Id,
                ItemKind = CollaborationInboxItemKind.Notification,
                Title = subject,
                PreviewText = BuildDirectMessagePreview(body),
                Route = BuildCollaborationThreadRoute(thread.Id),
                IsUnread = true,
                UnreadCount = 1,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            await dbContext.Set<CollaborationThreadRecord>().AddAsync(thread, cancellationToken);
            await dbContext.Set<CollaborationInboxItemRecord>().AddAsync(inboxItem, cancellationToken);
        } else {
            inboxItem = await dbContext.Set<CollaborationInboxItemRecord>()
                .SingleAsync(item => item.ThreadId == thread.Id, cancellationToken);
            thread.LastActivityAtUtc = now;
            thread.PrimaryItemKind = CollaborationInboxItemKind.Notification;
            inboxItem.ItemKind = CollaborationInboxItemKind.Notification;
            inboxItem.PreviewText = BuildDirectMessagePreview(body);
            inboxItem.IsUnread = true;
            inboxItem.UnreadCount = Math.Max(1, inboxItem.UnreadCount + 1);
            inboxItem.UpdatedAtUtc = now;
        }

        await EnsureCollaborationParticipantAsync(dbContext, thread.Id, sourceRole, now, cancellationToken);
        await EnsureCollaborationParticipantAsync(dbContext, thread.Id, targetRole, now, cancellationToken);
        await dbContext.Set<CollaborationMessageRecord>().AddAsync(
            new CollaborationMessageRecord {
                ThreadId = thread.Id,
                Kind = CollaborationMessageKind.Standard,
                AuthorKind = CollaborationMessageAuthorKind.Role,
                AuthorKey = BuildRoleParticipantKey(sourceRole.Id),
                AuthorName = sourceRole.DisplayName,
                Body = body,
                RaisesEscalation = false,
                CreatedAtUtc = now
            },
            cancellationToken);
        return thread.Id;
    }

    private static ProcessDirectMessageAuthorizationResult AuthorizeDirectMessage(
        ProcessRun run,
        ProcessRoleRequirement sourceRole,
        ProcessRoleRequirement targetRole,
        ProcessRunAssignment? sourceAssignment,
        ProcessRunAssignment? targetAssignment,
        ProcessRoleMessagingPolicyDefinition? messagingPolicy)
    {
        if (messagingPolicy is null) {
            return ProcessDirectMessageAuthorizationResult.Denied(
                $"Direct messaging from {sourceRole.DisplayName} to {targetRole.DisplayName} is blocked because the process definition has no explicit Messaging link for that direction.",
                "processes.direct-message-policy-missing",
                "Process policy denied the request because no source-to-target Messaging link exists.",
                ProcessConformanceSeverity.Moderate);
        }

        if (sourceAssignment is null || sourceAssignment.IsCapabilityGap || !sourceAssignment.AllowsDirectMessaging) {
            return ProcessDirectMessageAuthorizationResult.Denied(
                $"{sourceRole.DisplayName} cannot send direct messages in this run because its assignment is unresolved or direct messaging is disabled.",
                "processes.direct-message-source-permission-denied",
                "Process policy allowed the direction, but the source assignment snapshot does not permit direct messaging.",
                ProcessConformanceSeverity.Moderate);
        }

        if (targetAssignment is null || targetAssignment.IsCapabilityGap || !targetAssignment.AllowsDirectMessaging) {
            return ProcessDirectMessageAuthorizationResult.Denied(
                $"{targetRole.DisplayName} cannot receive direct messages in this run because its assignment is unresolved or direct messaging is disabled.",
                "processes.direct-message-target-permission-denied",
                "Process policy allowed the direction, but the target assignment snapshot does not permit direct messaging.",
                ProcessConformanceSeverity.Moderate);
        }

        if (run.Status != ProcessRunStatus.Active || run.OperatingMode == ProcessOperatingMode.Emergency) {
            return ProcessDirectMessageAuthorizationResult.Denied(
                $"Direct messaging is not allowed while the run is {run.Status} in {run.OperatingMode} mode.",
                "processes.direct-message-governance-denied",
                $"Process policy and assignment permissions allowed the request, but governance blocked direct messaging for run state {run.Status} / {run.OperatingMode}.",
                ProcessConformanceSeverity.High);
        }

        return ProcessDirectMessageAuthorizationResult.Allowed(
            "Process policy link exists, both assignment snapshots permit direct messaging, and the run governance state allows direct communication.");
    }

    private static async Task EnsureCollaborationParticipantAsync(
        AppDbContext dbContext,
        Guid threadId,
        ProcessRoleRequirement role,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var participantKey = BuildRoleParticipantKey(role.Id);
        var exists = await dbContext.Set<CollaborationParticipantRecord>()
            .AnyAsync(
                item => item.ThreadId == threadId && item.ParticipantKey == participantKey,
                cancellationToken);
        if (exists) {
            return;
        }

        await dbContext.Set<CollaborationParticipantRecord>().AddAsync(
            new CollaborationParticipantRecord {
                ThreadId = threadId,
                ParticipantKind = CollaborationParticipantKind.Role,
                ParticipantKey = participantKey,
                DisplayName = role.DisplayName,
                RoleLabel = role.DisplayName,
                AddedAtUtc = now
            },
            cancellationToken);
    }

    private static string BuildDirectMessageSubject(ProcessRoleRequirement sourceRole, ProcessRoleRequirement targetRole)
    {
        return $"Direct messaging: {sourceRole.DisplayName} -> {targetRole.DisplayName}";
    }

    private static string BuildDirectMessageDecisionTitle(ProcessRoleRequirement sourceRole, ProcessRoleRequirement targetRole)
    {
        return $"Direct message {sourceRole.DisplayName} -> {targetRole.DisplayName}";
    }

    private static string BuildDirectMessageDecisionReason(string body)
    {
        return BuildDirectMessagePreview(body);
    }

    private static string BuildDirectMessagePreview(string body)
    {
        var normalized = body.Trim().ReplaceLineEndings(" ");
        return normalized.Length <= 160
            ? normalized
            : normalized[..157] + "...";
    }

    private static string BuildCollaborationThreadRoute(Guid threadId)
    {
        return $"/collaboration?threadId={threadId:D}";
    }

    private static string BuildRoleParticipantKey(Guid roleId)
    {
        return $"process-role:{roleId:D}";
    }

    private sealed record ProcessDirectMessageAuthorizationResult(
        bool IsAllowed,
        string UserMessage,
        string ErrorCode,
        string PolicyEvaluation,
        ProcessConformanceSeverity ConformanceSeverity)
    {
        public static ProcessDirectMessageAuthorizationResult Allowed(string policyEvaluation)
        {
            return new ProcessDirectMessageAuthorizationResult(true, string.Empty, string.Empty, policyEvaluation, ProcessConformanceSeverity.Low);
        }

        public static ProcessDirectMessageAuthorizationResult Denied(
            string userMessage,
            string errorCode,
            string policyEvaluation,
            ProcessConformanceSeverity conformanceSeverity)
        {
            return new ProcessDirectMessageAuthorizationResult(false, userMessage, errorCode, policyEvaluation, conformanceSeverity);
        }
    }
}
